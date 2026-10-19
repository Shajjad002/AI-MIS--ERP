using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.MIS.Application.Rag;
using Microsoft.Extensions.Options;

namespace AI.MIS.Infrastructure.Rag;

public sealed class OpenAiEmbeddingGenerator(HttpClient httpClient, IOptions<RagOptions> options) : IEmbeddingGenerator
{
    public async Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
        => (await GenerateBatchAsync([text], cancellationToken))[0];

    public async Task<IReadOnlyList<float[]>> GenerateBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return [];
        var settings = options.Value;
        if (!Uri.TryCreate(settings.EmbeddingsEndpoint, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(settings.EmbeddingsModel))
            throw new InvalidOperationException("RAG embeddings are not configured. Set Rag:EmbeddingsEndpoint and Rag:EmbeddingsModel.");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(settings.EmbeddingsApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.EmbeddingsApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new { model = settings.EmbeddingsModel, input = texts }), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"The embeddings provider returned HTTP {(int)response.StatusCode}. Response: {message[..Math.Min(message.Length, 300)]}");
        }

        using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!result.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() != texts.Count)
            throw new InvalidOperationException("The embeddings provider returned no embedding vector.");
        var vectors = new float[]?[texts.Count];
        foreach (var item in data.EnumerateArray())
        {
            var index = item.GetProperty("index").GetInt32();
            if (index < 0 || index >= vectors.Length || !item.TryGetProperty("embedding", out var embedding))
                throw new InvalidOperationException("The embeddings provider returned an invalid vector index.");
            var vector = embedding.EnumerateArray().Select(value => value.GetSingle()).ToArray();
            if (vector.Length == 0 || vector.Any(value => !float.IsFinite(value)))
                throw new InvalidOperationException("The embeddings provider returned an invalid vector.");
            vectors[index] = vector;
        }
        if (vectors.Any(vector => vector is null))
            throw new InvalidOperationException("The embeddings provider returned an incomplete batch.");
        return vectors.Select(vector => vector!).ToArray();
    }
}
