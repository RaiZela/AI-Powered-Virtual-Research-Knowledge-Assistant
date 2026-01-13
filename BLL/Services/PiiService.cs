using Azure.AI.TextAnalytics;
using Shared;

namespace BLL.Services;

public interface IPiiService
{
    Task<PiiRedactResult> RedactAsync(string text, CancellationToken ct = default);
}
public record PiiRedactResult(string RedactedText, List<PiiEntityOutput> Entities);
public class PiiService : IPiiService
{
    private readonly TextAnalyticsClient _client;

    public PiiService(TextAnalyticsClient client)
    {
        _client = client;
    }

    public async Task<PiiRedactResult> RedactAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new PiiRedactResult("", new());

        // Detect PII + return redacted text
        var resp = await _client.RecognizePiiEntitiesAsync(text, cancellationToken: ct);

        var entities = resp.Value.Select(e => new PiiEntityOutput
        {
            Text = e.Text,
            Category = e.Category.ToString(),
            ConfidenceScore = e.ConfidenceScore
        }).ToList();

        return new PiiRedactResult(resp.Value.RedactedText, entities);
    }
}

