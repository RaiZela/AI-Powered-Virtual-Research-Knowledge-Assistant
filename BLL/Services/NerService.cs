using Azure.AI.TextAnalytics;
using Shared.NER;

namespace BLL.Services;

public interface INerService
{
    Task<List<EntityOutput>> RecognizeEntity(string text);
    Task<List<EntityOutputs>> RecognizeBatchEntity(IEnumerable<string> texts);
}
public class NerService : INerService
{
    private readonly TextAnalyticsClient _client;
    public NerService(TextAnalyticsClient client)
    {
        _client = client;
    }

    public async Task<List<EntityOutput>> RecognizeEntity(string text)
    {
        var response = await _client.RecognizeEntitiesAsync(text);

        return response.Value.Select(entity => new EntityOutput
        {
            Text = entity.Text,
            Category = entity.Category.ToString(),
            ConfidenceScore = entity.ConfidenceScore
        }).ToList();
    }

    public async Task<List<EntityOutputs>> RecognizeBatchEntity(IEnumerable<string> texts)
    {
        var inputs = texts.Select((t, i) => new TextDocumentInput((i + 1).ToString(), t)).ToList();

        var response = await _client.RecognizeEntitiesBatchAsync(inputs);

        var results = new List<EntityOutputs>();

        foreach (var doc in response.Value)
        {
            if (doc.HasError)
            {
                results.Add(new EntityOutputs { Entities = new List<EntityOutput>() });
                continue;
            }

            results.Add(new EntityOutputs
            {
                Entities = doc.Entities.Select(e => new EntityOutput
                {
                    Text = e.Text,
                    Category = e.Category.ToString(),
                    ConfidenceScore = e.ConfidenceScore
                }).ToList()
            });
        }

        return results;
    }
}
