using System.Collections.ObjectModel;

namespace TFM_ACCOUNTING.Business.Common;

public enum ErrorCode
{
    Validation = 1,
    NotFound = 2,
    Duplicate = 3,
    ConcurrencyConflict = 4,
    StorageUnavailable = 5,
    DatabaseUnavailable = 6,
    Unexpected = 7,
    ConfirmationRequired = 8
}

public sealed record ValidationError(string Field, string Message);
public sealed record RelatedRecord(int Id, string Name, string Identifier);
public sealed record ResponseWarning(string Code, string Message, IReadOnlyList<RelatedRecord> RelatedRecords);

public sealed class Response<T>
{
    private Response(bool isSuccess, T? data, string? message, ErrorCode? errorCode,
        IEnumerable<ValidationError>? validationErrors, IEnumerable<ResponseWarning>? warnings = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        Message = message;
        ErrorCode = errorCode;
        ValidationErrors = new ReadOnlyCollection<ValidationError>((validationErrors ?? []).ToArray());
        Warnings = new ReadOnlyCollection<ResponseWarning>((warnings ?? []).Select(w => w with
        {
            RelatedRecords = Array.AsReadOnly(w.RelatedRecords.ToArray())
        }).ToArray());
    }

    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Message { get; }
    public ErrorCode? ErrorCode { get; }
    public IReadOnlyList<ValidationError> ValidationErrors { get; }
    public IReadOnlyList<ResponseWarning> Warnings { get; }
    public bool RequiresConfirmation => ErrorCode == Common.ErrorCode.ConfirmationRequired;

    public static Response<T> ConfirmationRequired(IEnumerable<ResponseWarning> warnings, string message)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var items = warnings.ToArray();
        if (items.Length == 0) throw new ArgumentException("Legalább egy figyelmeztetés szükséges.", nameof(warnings));
        return new(false, default, message, Common.ErrorCode.ConfirmationRequired, null, items);
    }

    public static Response<T> Success(T data, string? message = null) => new(true, data, message, null, null);

    public static Response<T> Failure(ErrorCode errorCode, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!Enum.IsDefined(errorCode)) throw new ArgumentOutOfRangeException(nameof(errorCode));
        return new(false, default, message, errorCode, null);
    }

    public static Response<T> Invalid(IEnumerable<ValidationError> errors, string message = "Ellenőrizd a megadott adatokat.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var items = errors.ToArray();
        if (items.Length == 0) throw new ArgumentException("Legalább egy validációs hiba szükséges.", nameof(errors));
        return new(false, default, message, Common.ErrorCode.Validation, items);
    }
}

// Use Response<Unit> for successful operations with no result payload.
public readonly record struct Unit
{
    public static Unit Value => default;
}
