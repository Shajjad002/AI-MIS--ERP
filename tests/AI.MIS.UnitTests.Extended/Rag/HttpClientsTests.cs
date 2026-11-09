using System.Net;
using System.Text.Json;
using AI.MIS.Infrastructure.AI;
using AI.MIS.Infrastructure.Rag;
using AI.MIS.UnitTests.Extended.Support;

namespace AI.MIS.UnitTests.Extended;

public sealed class OpenAiCompatibleLlmClientTests
{
    private const string Endpoint = "https://llm.example.test/v1/chat/completions";

    private static OpenAiCompatibleLlmClient Create(StubHttpMessageHandler handler, LlmOptions? options = null) =>
        new(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(
            options ?? new LlmOptions { Endpoint = Endpoint, Model = "test-model", ApiKey = "secret-key" }));

    private static StubHttpMessageHandler Replies(string content) =>
        new(_ => StubHttpMessageHandler.Json(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } })));

    [Theory]
    [InlineData("", "model")]
    [InlineData("https://x.test", "")]
    [InlineData("  ", "  ")]
    public async Task CompleteJsonAsync_Throws_WhenEndpointOrModelIsMissing(string endpoint, string model)
    {
        var handler = Replies("{}");
        var client = Create(handler, new LlmOptions { Endpoint = endpoint, Model = model });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CompleteJsonAsync("sys", "user"));

        Assert.Contains("LLM is not configured", ex.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CompleteJsonAsync_PostsAJsonModeRequest_AndReturnsTheMessageContent()
    {
        var handler = Replies("""{"intent":"x"}""");

        var content = await Create(handler).CompleteJsonAsync("SYSTEM PROMPT", "USER QUESTION");

        Assert.Equal("""{"intent":"x"}""", content);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Endpoint, request.Uri!.ToString());
        Assert.Equal("Bearer secret-key", request.Authorization);

        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.Equal(0, root.GetProperty("temperature").GetInt32());                         // deterministic SQL generation
        Assert.Equal("json_object", root.GetProperty("response_format").GetProperty("type").GetString());
        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("SYSTEM PROMPT", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("USER QUESTION", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task CompleteJsonAsync_OmitsTheAuthorizationHeader_WhenNoApiKeyIsConfigured()
    {
        var handler = Replies("{}");

        await Create(handler, new LlmOptions { Endpoint = Endpoint, Model = "m", ApiKey = "" }).CompleteJsonAsync("s", "u");

        Assert.Null(Assert.Single(handler.Requests).Authorization);                           // e.g. a local Ollama server
    }

    [Fact]
    public async Task CompleteJsonAsync_ReportsProviderErrors_WithStatusCode_AndABoundedBody()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(new string('E', 5000), HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).CompleteJsonAsync("s", "u"));

        Assert.Contains("429", ex.Message);
        Assert.True(ex.Message.Length < 800, "The provider body must be truncated so errors and logs stay small.");
        Assert.DoesNotContain("secret-key", ex.Message);
    }

    [Fact]
    public async Task CompleteJsonAsync_Throws_WhenContentIsNull()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("""{"choices":[{"message":{"content":null}}]}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).CompleteJsonAsync("s", "u"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"choices":[]}""")]
    [InlineData("not json at all")]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public async Task CompleteJsonAsync_UnexpectedProviderPayload_ShouldSurfaceAsInvalidOperation(string payload)
    {
        // SUSPECTED DEFECT: GetProperty/indexing/JsonDocument.Parse throw KeyNotFoundException,
        // IndexOutOfRangeException or JsonException. None are caught by the controllers -> HTTP 500.
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(payload));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).CompleteJsonAsync("s", "u"));
    }
}

public sealed class OpenAiEmbeddingGeneratorTests
{
    private const string Endpoint = "https://embeddings.example.test/v1/embeddings";

    private static OpenAiEmbeddingGenerator Create(StubHttpMessageHandler handler, RagOptions? options = null) =>
        new(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(
            options ?? new RagOptions { EmbeddingsEndpoint = Endpoint, EmbeddingsModel = "embed-model", EmbeddingsApiKey = "embed-key" }));

    private static StubHttpMessageHandler Replies(string json) => new(_ => StubHttpMessageHandler.Json(json));

    [Fact]
    public async Task GenerateBatchAsync_WithNoInput_ReturnsEmpty_WithoutCallingTheProvider()
    {
        var handler = Replies("{}");

        Assert.Empty(await Create(handler).GenerateBatchAsync([]));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("", "model")]
    [InlineData("not a url", "model")]
    [InlineData("ftp://host/embeddings", "model")]            // only http/https are accepted
    [InlineData("https://host/embeddings", "")]
    public async Task GenerateBatchAsync_Throws_WhenNotConfigured(string endpoint, string model)
    {
        var handler = Replies("{}");
        var generator = Create(handler, new RagOptions { EmbeddingsEndpoint = endpoint, EmbeddingsModel = model });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateBatchAsync(["text"]));

        Assert.Contains("not configured", ex.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GenerateBatchAsync_SendsModelAndInputs_WithBearerKey()
    {
        var handler = Replies("""{"data":[{"index":0,"embedding":[0.5,0.25]},{"index":1,"embedding":[1,0]}]}""");

        await Create(handler).GenerateBatchAsync(["first", "second"]);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer embed-key", request.Authorization);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("embed-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(new[] { "first", "second" }, body.RootElement.GetProperty("input").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task GenerateBatchAsync_ReturnsVectorsInInputOrder_EvenIfTheProviderReordersThem()
    {
        var handler = Replies("""{"data":[{"index":1,"embedding":[0,1]},{"index":0,"embedding":[1,0]}]}""");

        var vectors = await Create(handler).GenerateBatchAsync(["a", "b"]);

        Assert.Equal(new[] { 1f, 0f }, vectors[0]);
        Assert.Equal(new[] { 0f, 1f }, vectors[1]);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsTheSingleVector()
    {
        var handler = Replies("""{"data":[{"index":0,"embedding":[0.1,0.2,0.3]}]}""");

        var vector = await Create(handler).GenerateAsync("text");

        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, vector);
    }

    [Theory]
    [InlineData("{}")]                                                                           // no "data"
    [InlineData("""{"data":[]}""")]                                                              // fewer vectors than inputs
    [InlineData("""{"data":[{"index":5,"embedding":[1]}]}""")]                                   // index out of range
    [InlineData("""{"data":[{"index":-1,"embedding":[1]}]}""")]
    [InlineData("""{"data":[{"index":0}]}""")]                                                   // no embedding
    [InlineData("""{"data":[{"index":0,"embedding":[]}]}""")]                                    // empty vector
    public async Task GenerateAsync_RejectsInvalidProviderResponses(string json)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(Replies(json)).GenerateAsync("text"));
    }

    [Fact]
    public async Task GenerateBatchAsync_RejectsAnIncompleteBatch_WhenAnIndexIsRepeated()
    {
        var handler = Replies("""{"data":[{"index":0,"embedding":[1]},{"index":0,"embedding":[2]}]}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).GenerateBatchAsync(["a", "b"]));

        Assert.Contains("incomplete", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_ReportsProviderErrors_WithStatusCode()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(new string('x', 2000), HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).GenerateAsync("text"));

        Assert.Contains("401", ex.Message);
        Assert.True(ex.Message.Length < 600);
    }
}
