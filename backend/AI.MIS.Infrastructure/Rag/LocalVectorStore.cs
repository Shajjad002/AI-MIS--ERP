using System.Text.Json;
using AI.MIS.Application.Rag;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace AI.MIS.Infrastructure.Rag;

public sealed class LocalVectorStore(IOptions<RagOptions> options, IWebHostEnvironment environment) : IVectorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task ReplaceDocumentChunksAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var all = await ReadAsync(cancellationToken);
            all.RemoveAll(chunk => chunk.DocumentId == documentId);
            all.AddRange(chunks);
            await WriteAsync(all, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<RetrievedPassage>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        if (queryEmbedding.Length == 0) throw new ArgumentException("The query embedding is empty.", nameof(queryEmbedding));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var chunks = await ReadAsync(cancellationToken);
            return chunks.Select(chunk => (Chunk: chunk, Score: CosineSimilarity(queryEmbedding, chunk.Embedding)))
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .Take(topK)
                .Select(item => new RetrievedPassage(item.Chunk.DocumentId, item.Chunk.FileName, item.Chunk.Text, item.Chunk.PageNumber, item.Chunk.Section, item.Score))
                .ToArray();
        }
        finally { gate.Release(); }
    }

    private async Task<List<DocumentChunk>> ReadAsync(CancellationToken cancellationToken)
    {
        var path = IndexPath;
        if (!File.Exists(path)) return [];
        var chunks = JsonSerializer.Deserialize<List<DocumentChunk>>(await File.ReadAllTextAsync(path, cancellationToken), JsonOptions);
        return chunks ?? throw new InvalidDataException("The local RAG vector index is invalid.");
    }

    private async Task WriteAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(IndexPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{IndexPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(chunks, JsonOptions), cancellationToken);
            File.Move(temporaryPath, IndexPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private string IndexPath => Path.Combine(Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.StoragePath)), "vectors.json");

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count != right.Count)
            throw new InvalidOperationException("Embedding dimensions do not match. Configure one consistent embedding model for all indexed documents.");
        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }
        return leftNorm == 0 || rightNorm == 0 ? 0 : dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}
