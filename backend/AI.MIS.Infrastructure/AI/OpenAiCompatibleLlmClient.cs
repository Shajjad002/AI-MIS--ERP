using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.MIS.Application.Copilot;
using Microsoft.Extensions.Options;

namespace AI.MIS.Infrastructure.AI;

public sealed class OpenAiCompatibleLlmClient(HttpClient httpClient, IOptions<LlmOptions> options) : ILlmClient
{
    public async Task<string> CompleteJsonAsync(string systemInstruction, string userMessage, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.Model))
            throw new InvalidOperationException("LLM is not configured. Set Llm__Endpoint and Llm__Model using user secrets or environment variables.");

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        var payload = new
        {
            model = settings.Model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new[] { new { role = "system", content = systemInstruction }, new { role = "user", content = userMessage } }
        };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var providerMessage = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"The configured LLM returned HTTP {(int)response.StatusCode} ({response.StatusCode}). Check the endpoint, model, and API key. Provider response: {providerMessage[..Math.Min(providerMessage.Length, 500)]}");
        }
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? throw new InvalidOperationException("LLM response did not contain message content.");
    }
}
