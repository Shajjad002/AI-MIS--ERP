using System.Data.Common;
using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.Application.Rag;
using AI.MIS.Application.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AI.MIS.UnitTests.Extended.Support;

// ---------------------------------------------------------------- Copilot fakes
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Func<string, string, string> respond;
    public FakeLlmClient(string json) : this((_, _) => json) { }
    public FakeLlmClient(Func<string, string, string> respond) => this.respond = respond;
    public List<(string System, string User)> Calls { get; } = [];

    public Task<string> CompleteJsonAsync(string systemInstruction, string userMessage, CancellationToken cancellationToken = default)
    {
        Calls.Add((systemInstruction, userMessage));
        return Task.FromResult(respond(systemInstruction, userMessage));
    }
}

public sealed class FakeSchemaProvider(IReadOnlyList<SchemaTable> schema) : ISchemaMetadataProvider
{
    public Task<IReadOnlyList<SchemaTable>> GetApprovedSchemaAsync(CancellationToken cancellationToken = default) => Task.FromResult(schema);
}

public sealed class FakePlanningService(Func<string, CopilotInterpretation> interpret) : ICopilotPlanningService
{
    public Task<CopilotInterpretation> InterpretAsync(string question, CancellationToken cancellationToken = default) =>
        Task.FromResult(interpret(question));
}

public sealed class ThrowingPlanningService(Exception exception) : ICopilotPlanningService
{
    public Task<CopilotInterpretation> InterpretAsync(string question, CancellationToken cancellationToken = default) => throw exception;
}

public sealed class RecordingValidator : ISqlQueryValidator
{
    public Exception? ToThrow { get; set; }
    public List<string> Seen { get; } = [];
    public void Validate(string sql, IReadOnlyList<SchemaTable> approvedSchema)
    {
        Seen.Add(sql);
        if (ToThrow is not null) throw ToThrow;
    }
}

public sealed class RecordingAuthorization : IQueryAuthorizationService
{
    public Exception? ToThrow { get; set; }
    public List<(string Sql, ClaimsPrincipal User)> Seen { get; } = [];
    public void Validate(string sql, ClaimsPrincipal user)
    {
        Seen.Add((sql, user));
        if (ToThrow is not null) throw ToThrow;
    }
}

public sealed class FakeExecutor : IReadOnlyQueryExecutor
{
    public Exception? ToThrow { get; set; }
    public List<string> Executed { get; } = [];
    public CopilotQueryResult Result { get; set; } = new(["Total"], [new Dictionary<string, object?> { ["Total"] = 42m }], 3, "ok");

    public Task<CopilotQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        Executed.Add(sql);
        if (ToThrow is not null) throw ToThrow;
        return Task.FromResult(Result);
    }
}

public sealed class RecordingAuditLogger : IAuditLogger
{
    public List<QueryAuditEntry> Entries { get; } = [];
    public Task LogQueryAsync(QueryAuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>Stands in for SqlException (which cannot be constructed in tests) - both derive from DbException.</summary>
public sealed class FakeDbException(string message) : DbException(message);

// ---------------------------------------------------------------- Users fakes
public sealed class FakeUserRepository(Func<string, AuthenticatedUser?> find) : IUserRepository
{
    public List<string> Lookups { get; } = [];
    public Task<AuthenticatedUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        Lookups.Add(userName);
        return Task.FromResult(find(userName));
    }
}

public sealed class FakeAuthenticationService(Func<string, string, AuthenticationResult?> authenticate) : IAuthenticationService
{
    public Task<AuthenticationResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default) =>
        Task.FromResult(authenticate(userName, password));
}

// ---------------------------------------------------------------- RAG fakes
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator
{
    public Func<string, float[]> Embed { get; set; } = _ => [1f, 0f];
    public Func<IReadOnlyList<string>, IReadOnlyList<float[]>>? BatchResponder { get; set; }
    public List<int> BatchSizes { get; } = [];
    public List<string> Queries { get; } = [];

    public Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        Queries.Add(text);
        return Task.FromResult(Embed(text));
    }

    public Task<IReadOnlyList<float[]>> GenerateBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        BatchSizes.Add(texts.Count);
        IReadOnlyList<float[]> result = BatchResponder?.Invoke(texts) ?? texts.Select(Embed).ToArray();
        return Task.FromResult(result);
    }
}

public sealed class FakeVectorStore : IVectorStore
{
    public List<(Guid DocumentId, IReadOnlyList<DocumentChunk> Chunks)> Replaced { get; } = [];
    public List<int> RequestedTopK { get; } = [];
    public IReadOnlyList<RetrievedPassage> Passages { get; set; } = [];

    public Task ReplaceDocumentChunksAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        Replaced.Add((documentId, chunks));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RetrievedPassage>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        RequestedTopK.Add(topK);
        return Task.FromResult(Passages);
    }
}

public sealed class FakeTextExtractor(IReadOnlyList<DocumentSegment> segments) : IDocumentTextExtractor
{
    public Task<IReadOnlyList<DocumentSegment>> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(segments);
}

public sealed class FakeDocumentStore : IDocumentStore
{
    public Exception? SaveThrows { get; set; }
    public List<StoredDocument> Documents { get; } = [];

    public Task<StoredDocument> SaveAsync(Stream content, string fileName, string contentType, long length, Guid uploadedBy, CancellationToken cancellationToken = default)
    {
        if (SaveThrows is not null) throw SaveThrows;
        var doc = new StoredDocument(Guid.NewGuid(), fileName, contentType, length, uploadedBy, DateTimeOffset.UtcNow);
        Documents.Add(doc);
        return Task.FromResult(doc);
    }

    public Task<Stream> OpenReadAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<IReadOnlyList<StoredDocument>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StoredDocument>>(Documents.ToArray());
}

public sealed class FakeRagService : IRagService
{
    public Func<StoredDocument, int> Index { get; set; } = _ => 3;
    public Func<RagSearchRequest, IReadOnlyList<RetrievedPassage>> Search { get; set; } = _ => [];
    public Func<string, RagAnswer> Answer { get; set; } = _ => new RagAnswer("answer", []);

    public Task<int> IndexDocumentAsync(StoredDocument document, CancellationToken cancellationToken = default) => Task.FromResult(Index(document));
    public Task<IReadOnlyList<RetrievedPassage>> SearchAsync(RagSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Search(request));
    public Task<RagAnswer> AnswerAsync(string question, CancellationToken cancellationToken = default) => Task.FromResult(Answer(question));
}

// ---------------------------------------------------------------- infrastructure helpers
/// <summary>A throw-away content root in the temp folder, deleted when the test class is disposed.</summary>
public sealed class TestHostEnvironment : IWebHostEnvironment, IDisposable
{
    public TestHostEnvironment()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "aimis-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ContentRootPath);
    }

    public string ApplicationName { get; set; } = "AI.MIS.Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public void Dispose()
    {
        try { Directory.Delete(ContentRootPath, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

public sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? Authorization, string Body);

public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri, request.Headers.Authorization?.ToString(), body));
        return responder(request);
    }

    public static HttpResponseMessage Json(string json, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}
