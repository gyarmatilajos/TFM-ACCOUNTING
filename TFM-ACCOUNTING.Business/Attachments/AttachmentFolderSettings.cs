using TFM_ACCOUNTING.Business.Common;

namespace TFM_ACCOUNTING.Business.Attachments;

public sealed class AttachmentFolderSettings(string? settingsFile = null)
{
    private readonly string file = settingsFile ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TFM Accounting", "attachment-folder.txt");

    public Response<string?> Read()
    {
        try { return Response<string?>.Success(File.Exists(file) ? File.ReadAllText(file).Trim() : null); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Response<string?>.Failure(ErrorCode.StorageUnavailable, "A csatolmányok helyi beállítása nem olvasható.");
        }
    }

    public Response<string> Save(string folder)
    {
        var validated = Validate(folder);
        if (!validated.IsSuccess) return validated;
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(temporary, validated.Data!);
            File.Move(temporary, file, overwrite: true);
            return Response<string>.Success(validated.Data!, "A csatolmányok mappáját mentettük.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Response<string>.Failure(ErrorCode.StorageUnavailable, "A csatolmányok helyi beállítását nem sikerült menteni.");
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    public static Response<string> Validate(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder.Trim()))
            return Response<string>.Invalid([new("Folder", "Adj meg teljes helyi vagy hálózati mappaútvonalat.")]);

        try
        {
            var path = Path.GetFullPath(folder.Trim());
            // Enumerating also distinguishes a missing directory from an inaccessible one.
            _ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault();
            return Response<string>.Success(path);
        }
        catch (UnauthorizedAccessException)
        {
            return Response<string>.Failure(ErrorCode.StorageUnavailable, "Nincs hozzáférés a megadott csatolmányok mappájához.");
        }
        catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException)
        {
            return Response<string>.Failure(ErrorCode.StorageUnavailable,
                "A csatolmányok mappája nem érhető el. Ellenőrizd az útvonalat és a kapcsolatot.");
        }
    }

    internal Response<string> GetAvailableFolder()
    {
        var result = Read();
        if (!result.IsSuccess) return Response<string>.Failure(ErrorCode.StorageUnavailable, result.Message!);
        if (string.IsNullOrWhiteSpace(result.Data))
            return Response<string>.Failure(ErrorCode.StorageUnavailable, "Add meg a csatolmányok mappáját a Beállításokban.");
        return Validate(result.Data);
    }
}
