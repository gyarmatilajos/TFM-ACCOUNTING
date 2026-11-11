using System.Collections.Frozen;

namespace TFM_ACCOUNTING.Business.Attachments;

public static class AttachmentFileTypes
{
    private static readonly FrozenDictionary<string, string> Types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".csv"] = "text/csv"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool TryGetContentType(string fileName, out string? contentType) =>
        Types.TryGetValue(Path.GetExtension(fileName), out contentType);
}
