using System.Text;
using AI.MIS.Application.Rag;
using AI.MIS.Infrastructure.Rag;
using AI.MIS.UnitTests.Extended.Support;

namespace AI.MIS.UnitTests.Extended;

public sealed class LocalVectorStoreTests : IDisposable
{
    private readonly TestHostEnvironment env = new();
    private readonly RagOptions options = new() { StoragePath = "vec" };

    public void Dispose() => env.Dispose();

    private LocalVectorStore NewStore() => new(Microsoft.Extensions.Options.Options.Create(options), env);

    private static DocumentChunk Chunk(Guid documentId, string text, params float[] embedding) =>
        new(Guid.NewGuid(), documentId, "file.md", text, null, "S", embedding);

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNothingHasBeenIndexed()
    {
        Assert.Empty(await NewStore().SearchAsync([1f, 0f], 5));
    }

    [Fact]
    public async Task SearchAsync_RanksByCosineSimilarity_AndDropsOrthogonalChunks()
    {
        var doc = Guid.NewGuid();
        var store = NewStore();
        await store.ReplaceDocumentChunksAsync(doc, [Chunk(doc, "B", 0f, 1f), Chunk(doc, "C", 1f, 1f), Chunk(doc, "A", 1f, 0f)]);

        var results = await store.SearchAsync([1f, 0f], 10);

        Assert.Equal(new[] { "A", "C" }, results.Select(r => r.Text));        // "B" has similarity 0 and is dropped
        Assert.Equal(1.0, results[0].Similarity, 5);
        Assert.Equal(Math.Sqrt(0.5), results[1].Similarity, 5);
    }

    [Fact]
    public async Task SearchAsync_RespectsTopK()
    {
        var doc = Guid.NewGuid();
        var store = NewStore();
        await store.ReplaceDocumentChunksAsync(doc, Enumerable.Range(1, 6).Select(i => Chunk(doc, $"t{i}", 1f, i)).ToArray());

        Assert.Equal(2, (await store.SearchAsync([1f, 0f], 2)).Count);
    }

    [Fact]
    public async Task SearchAsync_CarriesCitationMetadata()
    {
        var doc = Guid.NewGuid();
        var store = NewStore();
        await store.ReplaceDocumentChunksAsync(doc, [new DocumentChunk(Guid.NewGuid(), doc, "policy.pdf", "text", 4, "Rules", [1f, 0f])]);

        var hit = Assert.Single(await store.SearchAsync([1f, 0f], 5));

        Assert.Equal(doc, hit.DocumentId);
        Assert.Equal("policy.pdf", hit.FileName);
        Assert.Equal(4, hit.PageNumber);
        Assert.Equal("Rules", hit.Section);
    }

    [Fact]
    public async Task ReplaceDocumentChunksAsync_ReplacesOnlyThatDocumentsChunks()
    {
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        var store = NewStore();
        await store.ReplaceDocumentChunksAsync(docA, [Chunk(docA, "A-old", 1f, 0f)]);
        await store.ReplaceDocumentChunksAsync(docB, [Chunk(docB, "B", 1f, 0f)]);

        await store.ReplaceDocumentChunksAsync(docA, [Chunk(docA, "A-new", 1f, 0f)]);

        var texts = (await store.SearchAsync([1f, 0f], 10)).Select(r => r.Text).OrderBy(t => t).ToArray();
        Assert.Equal(new[] { "A-new", "B" }, texts);
    }

    [Fact]
    public async Task Index_SurvivesRestart_BecauseItIsPersistedToDisk()
    {
        var doc = Guid.NewGuid();
        await NewStore().ReplaceDocumentChunksAsync(doc, [Chunk(doc, "persisted", 1f, 0f)]);

        var hit = Assert.Single(await NewStore().SearchAsync([1f, 0f], 5));

        Assert.Equal("persisted", hit.Text);
    }

    [Fact]
    public async Task SearchAsync_Throws_WhenEmbeddingDimensionsDiffer()
    {
        var doc = Guid.NewGuid();
        var store = NewStore();
        await store.ReplaceDocumentChunksAsync(doc, [Chunk(doc, "t", 1f, 0f)]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.SearchAsync([1f, 0f, 0f], 5));

        Assert.Contains("dimensions", ex.Message);
    }

    [Fact]
    public async Task SearchAsync_RejectsAnEmptyQueryEmbedding()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => NewStore().SearchAsync([], 5));
    }

    [Fact]
    public async Task SearchAsync_Throws_WhenTheIndexFileIsCorrupt()
    {
        Directory.CreateDirectory(Path.Combine(env.ContentRootPath, "vec"));
        await File.WriteAllTextAsync(Path.Combine(env.ContentRootPath, "vec", "vectors.json"), "null");

        await Assert.ThrowsAsync<InvalidDataException>(() => NewStore().SearchAsync([1f, 0f], 5));
    }

    [Fact]
    public async Task ReplaceDocumentChunksAsync_IsSafeUnderConcurrentWriters()
    {
        var store = NewStore();
        var documents = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(documents.Select(d => store.ReplaceDocumentChunksAsync(d, [Chunk(d, d.ToString("N"), 1f, 0f)])));

        Assert.Equal(8, (await store.SearchAsync([1f, 0f], 10)).Count);   // no lost updates
    }

    [Fact]
    public async Task ReplaceDocumentChunksAsync_LeavesNoTemporaryFilesBehind()
    {
        var doc = Guid.NewGuid();
        await NewStore().ReplaceDocumentChunksAsync(doc, [Chunk(doc, "t", 1f, 0f)]);

        var files = Directory.GetFiles(Path.Combine(env.ContentRootPath, "vec")).Select(Path.GetFileName).ToArray();

        Assert.Equal(new[] { "vectors.json" }, files);
    }
}

public sealed class LocalDocumentStoreTests : IDisposable
{
    private readonly TestHostEnvironment env = new();
    private readonly RagOptions options = new() { StoragePath = "docs", MaxUploadBytes = 100 };
    private string Root => Path.Combine(env.ContentRootPath, "docs");

    public void Dispose() => env.Dispose();

    private LocalDocumentStore NewStore() => new(Microsoft.Extensions.Options.Options.Create(options), env);

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    private static Task<StoredDocument> Save(LocalDocumentStore store, string text, string fileName = "note.txt", string contentType = "text/plain", long? length = null) =>
        store.SaveAsync(Bytes(text), fileName, contentType, length ?? Encoding.UTF8.GetByteCount(text), Guid.NewGuid());

    // ------------------------------------------------------------ happy path
    [Fact]
    public async Task SaveAsync_StoresTheFile_AndReturnsMetadata()
    {
        var uploader = Guid.NewGuid();
        var store = NewStore();

        var doc = await store.SaveAsync(Bytes("hello"), "note.txt", "text/plain", 5, uploader);

        Assert.Equal("note.txt", doc.FileName);
        Assert.Equal("text/plain", doc.ContentType);
        Assert.Equal(5, doc.Length);
        Assert.Equal(uploader, doc.UploadedBy);
        Assert.True(File.Exists(Path.Combine(Root, $"{doc.Id:N}.txt")));
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsTheStoredBytes()
    {
        var store = NewStore();
        var doc = await Save(store, "hello world");

        await using var stream = await store.OpenReadAsync(doc.Id);
        using var reader = new StreamReader(stream);

        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ListAsync_ReturnsEverySavedDocument_AndIsEmptyBeforeTheFirstUpload()
    {
        var store = NewStore();
        Assert.Empty(await store.ListAsync());

        await Save(store, "a", "a.txt");
        await Save(store, "b", "b.md");

        Assert.Equal(new[] { "a.txt", "b.md" }, (await store.ListAsync()).Select(d => d.FileName).OrderBy(n => n));
    }

    [Fact]
    public async Task SaveAsync_AcceptsUpperCaseExtensions()
    {
        var doc = await Save(NewStore(), "x", "REPORT.PDF");

        Assert.True(File.Exists(Path.Combine(Root, $"{doc.Id:N}.pdf")));
    }

    [Fact]
    public async Task SaveAsync_DefaultsContentType_WhenNoneIsSupplied()
    {
        var doc = await Save(NewStore(), "x", contentType: "  ");

        Assert.Equal("application/octet-stream", doc.ContentType);
    }

    // ------------------------------------------------------------ validation
    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.ps1")]
    [InlineData("archive.zip")]
    [InlineData("noextension")]
    [InlineData("double.txt.exe")]
    public async Task SaveAsync_RejectsUnsupportedExtensions(string fileName)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(NewStore(), "x", fileName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]                                     // MaxUploadBytes is 100
    public async Task SaveAsync_RejectsDeclaredSizesOutsideTheAllowedRange(long length)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(NewStore(), "x", length: length));
    }

    [Fact]
    public async Task SaveAsync_RejectsAStreamLargerThanTheLimit_EvenIfTheDeclaredLengthIsSmall()
    {
        // A client can lie about Content-Length. The copy loop enforces the limit on the bytes actually read.
        var store = NewStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(store, new string('x', 150), length: 50));

        Assert.Empty(Directory.GetFiles(Root).Where(f => !f.EndsWith("index.json")));    // partial file was deleted
        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task SaveAsync_RejectsAnEmptyStream_AndCleansUp()
    {
        var store = NewStore();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(store, "", length: 10));

        Assert.Contains("empty", ex.Message);
        Assert.Empty(Directory.GetFiles(Root));
    }

    // ------------------------------------------------------------ path safety
    [Theory]
    [InlineData("../../evil.txt")]
    [InlineData("/etc/passwd.txt")]
    [InlineData("sub/folder/note.md")]
    public async Task SaveAsync_StripsDirectoryPartsFromTheClientSuppliedFileName(string fileName)
    {
        var doc = await Save(NewStore(), "x", fileName);

        Assert.Equal(Path.GetFileName(doc.FileName), doc.FileName);                      // no directory separators survive
        Assert.True(File.Exists(Path.Combine(Root, $"{doc.Id:N}{Path.GetExtension(fileName)}")));   // always a GUID name inside Root
    }

    [Fact]
    public async Task SaveAsync_NeverWritesOutsideTheStorageFolder_EvenWithBackslashTraversal()
    {
        var doc = await Save(NewStore(), "x", "..\\..\\evil.txt");

        Assert.True(File.Exists(Path.Combine(Root, $"{doc.Id:N}.txt")));
        Assert.Empty(Directory.GetFiles(env.ContentRootPath, "evil.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(env.ContentRootPath)!, "evil.txt"));
    }

    // ------------------------------------------------------------ lookups
    [Fact]
    public async Task OpenReadAsync_Throws_ForAnUnknownDocument()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => NewStore().OpenReadAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OpenReadAsync_Throws_WhenTheFileWasRemovedFromDisk()
    {
        var store = NewStore();
        var doc = await Save(store, "x");
        File.Delete(Path.Combine(Root, $"{doc.Id:N}.txt"));

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadAsync(doc.Id));

        Assert.Contains("missing", ex.Message);
    }

    // ------------------------------------------------------------ concurrency
    [Fact]
    public async Task SaveAsync_IsSafeUnderConcurrentUploads()
    {
        var store = NewStore();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Save(store, $"file {i}", $"f{i}.txt")));

        Assert.Equal(10, (await store.ListAsync()).Count);                               // index.json lost no entries
    }
}
