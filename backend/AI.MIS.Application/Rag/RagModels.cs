namespace AI.MIS.Application.Rag;

public sealed record DocumentSegment(string Text, int? PageNumber, string? Section);

public sealed record DocumentChunk(
    Guid Id,
    Guid DocumentId,
    string FileName,
    string Text,
    int? PageNumber,
    string? Section,
    float[] Embedding);

public sealed record RetrievedPassage(
    Guid DocumentId,
    string FileName,
    string Text,
    int? PageNumber,
    string? Section,
    double Similarity);

public sealed record RagCitation(Guid DocumentId, string FileName, int? PageNumber, string? Section, string Quote);

public sealed record RagAnswer(string Answer, IReadOnlyList<RagCitation> Citations);

public sealed record RagSearchRequest(string Query, int? TopK = null);

public interface IDocumentTextExtractor
{
    Task<IReadOnlyList<DocumentSegment>> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<float[]>> GenerateBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}

public interface IVectorStore
{
    Task ReplaceDocumentChunksAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetrievedPassage>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default);
}

public interface IRagService
{
    Task<int> IndexDocumentAsync(StoredDocument document, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetrievedPassage>> SearchAsync(RagSearchRequest request, CancellationToken cancellationToken = default);
    Task<RagAnswer> AnswerAsync(string question, CancellationToken cancellationToken = default);
}
