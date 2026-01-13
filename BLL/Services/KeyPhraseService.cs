using Azure.AI.TextAnalytics;

namespace BLL.Services;

public interface IKeyPhraseService
{
    Task<List<string>> ExtractAsync(string text, CancellationToken ct = default);
    Task<List<List<string>>> ExtractBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
}
public class KeyPhraseService : IKeyPhraseService
{
    private readonly TextAnalyticsClient _client;

    public KeyPhraseService(TextAnalyticsClient client)
    {
        _client = client;
    }

    public async Task<List<string>> ExtractAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        var resp = await _client.ExtractKeyPhrasesAsync(text, cancellationToken: ct);
        return resp.Value.ToList();
    }

    public async Task<List<List<string>>> ExtractBatchAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var inputs = texts
            .Select((t, i) => new Azure.AI.TextAnalytics.TextDocumentInput((i + 1).ToString(), t))
            .ToList();

        var resp = await _client.ExtractKeyPhrasesBatchAsync(inputs, cancellationToken: ct);

        var results = new List<List<string>>();

        foreach (var doc in resp.Value)
        {
            if (doc.HasError)
            {
                results.Add(new List<string>());
                continue;
            }

            results.Add(doc.KeyPhrases.ToList());
        }

        return results;
    }
}
