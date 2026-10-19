namespace AI.MIS.Application.Rag;

public interface IDocumentStore
{
    Task<StoredDocument> SaveAsync(Stream content, string fileName, string contentType, long length, Guid uploadedBy, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredDocument>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed record StoredDocument(
    Guid Id,
    string FileName,
    string ContentType,
    long Length,
    Guid UploadedBy,
    DateTimeOffset UploadedAt);
