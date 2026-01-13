namespace Shared;

public sealed class OcrStoredResult
{
    public string DocumentId { get; set; } = "";
    public string BlobName { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string? Language { get; set; }

    public string TextOriginal { get; set; } = "";
    public string TextNormalized { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
