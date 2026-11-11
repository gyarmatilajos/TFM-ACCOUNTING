using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Business.Attachments;

public sealed record AttachmentRow(int Id, string OriginalFileName, string ContentType, long Size, DateTime CreatedAtUtc,
    string? Notes, byte[] Version);

public sealed class AttachmentService(IDbContextFactory<AccountingDbContext> factory, AttachmentFolderSettings settings)
{
    public const string DeletedFolder = "Torolt-dokumentumok";

    public Task<Response<IReadOnlyList<AttachmentRow>>> ListAsync(int obligationId) => Run<IReadOnlyList<AttachmentRow>>(async db =>
    {
        var rows = await db.Attachments.AsNoTracking().Where(x => x.ObligationId == obligationId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Select(x => new AttachmentRow(x.Id, x.OriginalFileName, x.ContentType, x.FileSizeBytes, x.CreatedAtUtc, x.Notes, x.RowVersion))
            .ToListAsync();
        return Response<IReadOnlyList<AttachmentRow>>.Success(rows);
    });

    public Task<Response<int>> UploadAsync(int obligationId, string sourcePath) => Run(async db =>
    {
        var originalName = Path.GetFileName(sourcePath);
        if (!ValidFileName(originalName) || originalName.Length > 255 ||
            !AttachmentFileTypes.TryGetContentType(originalName, out var contentType))
            return Response<int>.Invalid([new("File", "Válassz érvényes nevű PDF, JPG, JPEG, PNG, DOC, DOCX, XLS, XLSX vagy CSV fájlt.")]);

        if (!await db.Obligations.AnyAsync(x => x.Id == obligationId)) return Missing<int>();
        var root = settings.GetAvailableFolder();
        if (!root.IsSuccess) return Storage<int>(root.Message);
        var copied = await CopyWithUniqueNameAsync(db, root.Data!, sourcePath, originalName);
        var (relativePath, destination, length) = copied;
        if (length == 0)
        {
            TryDelete(destination);
            return Response<int>.Invalid([new("File", "Üres fájl nem csatolható.")]);
        }

        var attachment = new Attachment
        {
            ObligationId = obligationId, OriginalFileName = originalName, RelativePath = relativePath,
            ContentType = contentType!, FileSizeBytes = length
        };
        db.Attachments.Add(attachment);
        try
        {
            await db.SaveChangesAsync();
            return Response<int>.Success(attachment.Id, "A fájlt a beállított mappába mentettük.");
        }
        catch (Exception e) when (e is DbUpdateException or System.Data.Common.DbException or TimeoutException)
        {
            Log(e);
            try
            {
                await using var check = await factory.CreateDbContextAsync();
                var savedId = await check.Attachments.Where(x => x.RelativePath == relativePath).Select(x => (int?)x.Id).SingleOrDefaultAsync();
                if (savedId.HasValue) return Response<int>.Success(savedId.Value, "A fájlt a beállított mappába mentettük.");
                if (TryDelete(destination))
                    return Response<int>.Failure(ErrorCode.DatabaseUnavailable, "A bejegyzés mentése sikertelen. A másolt fájlt eltávolítottuk.");
            }
            catch (Exception checkError) { Log(checkError); }
            return Response<int>.Failure(ErrorCode.DatabaseUnavailable,
                "A csatolmány mentését nem sikerült ellenőrizni. A másolat megmaradt a mappában. Frissítsd a listát, mielőtt újra feltöltöd.");
        }
    });

    public Task<Response<string>> OpenPathAsync(int obligationId, int attachmentId) => Run(async db =>
    {
        var path = await FindPathAsync(db, obligationId, attachmentId);
        if (!path.IsSuccess) return path;
        await using var file = OpenRead(path.Data!);
        // Reading also checks that an online-only sync file can actually be downloaded.
        await file.ReadExactlyAsync(new byte[1]);
        return path;
    });

    public Task<Response<Unit>> SaveCopyAsync(int obligationId, int attachmentId, string destination) => Run(async db =>
    {
        var source = await FindPathAsync(db, obligationId, attachmentId);
        if (!source.IsSuccess) return Response<Unit>.Failure(source.ErrorCode!.Value, source.Message!);
        if (string.Equals(Path.GetFullPath(destination), source.Data, StringComparison.OrdinalIgnoreCase))
            return Response<Unit>.Invalid([new("Destination", "A másolatnak válassz másik fájlnevet vagy mappát.")]);
        await CopyAsync(source.Data!, destination, overwrite: true);
        return Response<Unit>.Success(Unit.Value, "A másolatot mentettük.");
    });

    public Task<Response<Unit>> UpdateNotesAsync(int obligationId, int attachmentId, byte[] version, string? notes) => Run(async db =>
    {
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (notes?.Length > 2000)
            return Response<Unit>.Invalid([new("Notes", "A megjegyzés legfeljebb 2000 karakteres lehet.")]);
        var row = await db.Attachments.SingleOrDefaultAsync(x => x.Id == attachmentId && x.ObligationId == obligationId);
        if (row is null) return Missing<Unit>();
        if (!row.RowVersion.AsSpan().SequenceEqual(version)) return Conflict<Unit>();
        row.Notes = notes;
        await db.SaveChangesAsync();
        return Response<Unit>.Success(Unit.Value, "A megjegyzést mentettük.");
    });

    public Task<Response<Unit>> RemoveAsync(int obligationId, int attachmentId, byte[] version) => Run(async context =>
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync();
            await using var transaction = await db.Database.BeginTransactionAsync();
            // Serialize the move with other changes to this record, including from another sync client.
            var row = await db.Attachments.FromSqlInterpolated($"""
                SELECT * FROM [Attachments] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] = {attachmentId} AND [ObligationId] = {obligationId}
                """).SingleOrDefaultAsync();
            if (row is null) return Missing<Unit>();
            if (!row.RowVersion.AsSpan().SequenceEqual(version)) return Conflict<Unit>();
            var root = settings.GetAvailableFolder();
            if (!root.IsSuccess) return Storage<Unit>(root.Message);
            var source = ResolvePath(root.Data!, row.RelativePath);
            var name = ValidFileName(row.OriginalFileName) ? row.OriginalFileName : Path.GetFileName(source);
            var trash = ResolvePath(root.Data!, $"{DeletedFolder}/{Path.ChangeExtension(row.RelativePath, null)}/{FitName(name, "")}");
            Directory.CreateDirectory(Path.GetDirectoryName(trash)!);
            File.Move(source, trash);
            try
            {
                db.Attachments.Remove(row);
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return Response<Unit>.Success(Unit.Value, "A fájl a Torolt-dokumentumok mappába került.");
            }
            catch (Exception e)
            {
                Log(e);
                // Once the file has moved, an uncertain database commit must not replay the move.
                try { await transaction.RollbackAsync(); }
                catch (Exception rollbackError) { Log(rollbackError); }
                try
                {
                    await using var check = await factory.CreateDbContextAsync();
                    if (!await check.Attachments.AnyAsync(x => x.Id == attachmentId))
                        return Response<Unit>.Success(Unit.Value, "A fájl a Torolt-dokumentumok mappába került.");
                    File.Move(trash, source);
                    return Response<Unit>.Failure(ErrorCode.DatabaseUnavailable,
                        "Az eltávolítás sikertelen. A fájlt visszahelyeztük az eredeti mappába.");
                }
                catch (Exception recoveryError) { Log(recoveryError); }
                return Response<Unit>.Failure(ErrorCode.DatabaseUnavailable,
                    "Az eltávolítás eredménye bizonytalan. Frissítsd a listát. A fájl a Torolt-dokumentumok mappában kereshető.");
            }
        }));

    private async Task<Response<string>> FindPathAsync(AccountingDbContext db, int obligationId, int attachmentId)
    {
        var relative = await db.Attachments.Where(x => x.Id == attachmentId && x.ObligationId == obligationId)
            .Select(x => x.RelativePath).SingleOrDefaultAsync();
        if (relative is null) return Missing<string>();
        var root = settings.GetAvailableFolder();
        if (!root.IsSuccess) return Storage<string>(root.Message);
        var path = ResolvePath(root.Data!, relative);
        if (!AttachmentFileTypes.TryGetContentType(path, out _))
            return Storage<string>("A csatolmány fájltípusa nem támogatott.");
        return Response<string>.Success(path);
    }

    private static bool ValidFileName(string name) => name.Length > 0 && name is not "." and not ".." &&
        !name.EndsWith('.') && !name.EndsWith(' ') && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string ResolvePath(string root, string relative)
    {
        var parts = relative.Split(['/', '\\']);
        if (parts.Any(part => !ValidFileName(part)))
            throw new IOException("Invalid attachment path.");
        return Path.Combine(root, Path.Combine(parts));
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

    private static async Task<(string RelativePath, string Destination, long Length)> CopyWithUniqueNameAsync(
        AccountingDbContext db, string root, string source, string originalName)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var suffix = "-" + Guid.NewGuid().ToString("N")[..12];
            var name = FitName(originalName, suffix);
            var path = ResolvePath(root, name);
            if (File.Exists(path) || await db.Attachments.AnyAsync(x => x.RelativePath == name))
                continue;
            try { return (name, path, await CopyAsync(source, path, overwrite: false)); }
            catch (IOException) when (File.Exists(path)) { }
        }
        throw new IOException("Could not allocate a unique attachment filename.");
    }

    private static string FitName(string originalName, string suffix)
    {
        var extension = Path.GetExtension(originalName);
        var stem = Path.GetFileNameWithoutExtension(originalName).TrimStart();
        var remaining = 255 - Encoding.UTF8.GetByteCount(suffix + extension);
        var fitted = new StringBuilder();
        foreach (var character in stem.EnumerateRunes())
        {
            if (character.Utf8SequenceLength > remaining) break;
            fitted.Append(character.ToString());
            remaining -= character.Utf8SequenceLength;
        }
        var name = fitted.ToString().TrimEnd(' ', '.');
        return (name.Length == 0 ? "Csatolmány" : name) + suffix + extension;
    }

    private static async Task<long> CopyAsync(string source, string destination, bool overwrite)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, Guid.NewGuid().ToString("N") + ".part");
        try
        {
            await using var input = OpenRead(source);
            long length;
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await input.CopyToAsync(output);
                length = output.Length;
            }
            File.Move(temporary, destination, overwrite);
            return length;
        }
        finally { TryDelete(temporary); }
    }

    private static bool TryDelete(string path)
    {
        try { File.Delete(path); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log(e); return false; }
    }

    private async Task<Response<T>> Run<T>(Func<AccountingDbContext, Task<Response<T>>> action)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            return await action(db);
        }
        catch (DbUpdateConcurrencyException) { return Conflict<T>(); }
        catch (FileNotFoundException)
        {
            return Storage<T>("A fájl ezen a gépen nem érhető el. Ellenőrizd az eredeti fájlt vagy várd meg a Synology Drive szinkronizálását.");
        }
        catch (DirectoryNotFoundException)
        {
            return Storage<T>("A fájl mappája nem érhető el. Ellenőrizd a kapcsolatot és a Synology Drive szinkronizálását.");
        }
        catch (UnauthorizedAccessException) { return Storage<T>("Nincs jogosultság a fájlművelethez."); }
        catch (IOException e) { Log(e); return Storage<T>("A fájlművelet sikertelen. Ellenőrizd a hozzáférést és a szinkronizálást."); }
        catch (Exception e)
        {
            Log(e);
            return Response<T>.Failure(ErrorCode.DatabaseUnavailable, "A csatolmány művelete sikertelen. Frissítsd a listát és ellenőrizd a kapcsolatot.");
        }
    }

    private static void Log(Exception e) => Trace.TraceError("Attachment operation failed ({0}).", e.GetType().Name);
    private static Response<T> Storage<T>(string? message) => Response<T>.Failure(ErrorCode.StorageUnavailable, message ?? "A mappa nem érhető el.");
    private static Response<T> Missing<T>() => Response<T>.Failure(ErrorCode.NotFound, "A tétel vagy a csatolmány már nem található.");
    private static Response<T> Conflict<T>() => Response<T>.Failure(ErrorCode.ConcurrencyConflict, "A csatolmány időközben módosult. Frissítsd a listát.");
}
