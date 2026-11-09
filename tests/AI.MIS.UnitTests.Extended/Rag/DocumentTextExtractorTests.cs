using System.Text;
using AI.MIS.Infrastructure.Rag;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AI.MIS.UnitTests.Extended;

public sealed class DocumentTextExtractorTests
{
    private static DocumentTextExtractor Create(int maxCharacters = 2_000_000) =>
        new(Microsoft.Extensions.Options.Options.Create(new RagOptions { MaxExtractedCharacters = maxCharacters }));

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    // ================================================================ TXT / Markdown
    [Fact]
    public async Task Extract_Markdown_UsesHeadingsAndLabelsAsSections()
    {
        const string text = "# Policy Overview\nCollection means money received.\n\nDefinitions:\nRecoverable is money due.\n";

        var segments = await Create().ExtractAsync(Utf8(text), "policy.md");

        Assert.Equal(2, segments.Count);
        Assert.Equal("Collection means money received.", segments[0].Text);
        Assert.Equal("Policy Overview", segments[0].Section);
        Assert.Equal("Recoverable is money due.", segments[1].Text);
        Assert.Equal("Definitions", segments[1].Section);
        Assert.All(segments, s => Assert.Null(s.PageNumber));
    }

    [Fact]
    public async Task Extract_PlainText_UsesDefaultSectionAndSkipsBlankLines()
    {
        var segments = await Create().ExtractAsync(Utf8("first\r\n\r\n   \r\nsecond\r\n"), "notes.txt");

        Assert.Equal(new[] { "first", "second" }, segments.Select(s => s.Text));
        Assert.All(segments, s => Assert.Equal("Document", s.Section));
    }

    [Fact]
    public async Task Extract_LongLineEndingWithColon_IsTreatedAsTextNotAHeading()
    {
        var longLine = new string('a', 130) + ":";

        var segments = await Create().ExtractAsync(Utf8(longLine), "notes.txt");

        Assert.Equal(longLine, Assert.Single(segments).Text);
    }

    [Fact]
    public async Task Extract_PreservesNonLatinText_AndDetectsAByteOrderMark()
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("ঋণ আদায়ের নীতিমালা")).ToArray();

        var segments = await Create().ExtractAsync(new MemoryStream(bytes), "bangla.txt");

        Assert.Equal("ঋণ আদায়ের নীতিমালা", Assert.Single(segments).Text);
    }

    [Fact]
    public async Task Extract_EmptyTextFile_YieldsNoSegments()
    {
        Assert.Empty(await Create().ExtractAsync(Utf8(""), "empty.txt"));
    }

    [Fact]
    public async Task Extract_RejectsTextLargerThanTheConfiguredLimit()
    {
        // The minimum effective limit is 1000 characters.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(maxCharacters: 1000).ExtractAsync(Utf8(new string('a', 1500)), "big.txt"));

        Assert.Contains("exceeds the configured limit", ex.Message);
    }

    [Theory]
    [InlineData("tool.exe")]
    [InlineData("data.csv")]
    [InlineData("noextension")]
    public async Task Extract_RejectsUnsupportedFileTypes(string fileName)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create().ExtractAsync(Utf8("x"), fileName));

        Assert.Contains("Only PDF, DOCX, TXT, and Markdown", ex.Message);
    }

    [Fact]
    public async Task Extract_HonoursAnAlreadyCancelledToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().ExtractAsync(Utf8("x"), "a.txt", cts.Token));
    }

    // ================================================================ DOCX
    private static byte[] BuildDocx(params (string? Style, string Text)[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body();
            foreach (var (style, text) in paragraphs)
            {
                var paragraph = new Paragraph();
                if (style is not null)
                    paragraph.AppendChild(new ParagraphProperties(new ParagraphStyleId { Val = style }));
                paragraph.AppendChild(new Run(new Text(text)));
                body.AppendChild(paragraph);
            }
            main.Document = new Document(body);
            main.Document.Save();
        }
        return stream.ToArray();
    }

    [Fact]
    public async Task Extract_Docx_GroupsParagraphsUnderTheirHeadings()
    {
        var bytes = BuildDocx(
            (null, "Preamble text."),
            ("Heading1", "Introduction"),
            (null, "Line one."),
            (null, "Line two."),
            ("Heading2", "Details"),
            (null, "More text."));

        var segments = await Create().ExtractAsync(new MemoryStream(bytes), "policy.docx");

        Assert.Equal(3, segments.Count);
        Assert.Equal(("Document", "Preamble text."), (segments[0].Section, segments[0].Text));
        Assert.Equal(("Introduction", "Line one. Line two."), (segments[1].Section, segments[1].Text));
        Assert.Equal(("Details", "More text."), (segments[2].Section, segments[2].Text));
    }

    [Fact]
    public async Task Extract_Docx_RejectsDocumentsOverTheLimit()
    {
        var bytes = BuildDocx((null, new string('w', 1500)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(maxCharacters: 1000).ExtractAsync(new MemoryStream(bytes), "big.docx"));
    }

    [Fact]
    public async Task Extract_CorruptDocx_IsReportedAsUnreadable()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create().ExtractAsync(Utf8("this is not a zip archive"), "broken.docx"));

        Assert.Contains("could not be read", ex.Message);
    }

    // ================================================================ PDF
    /// <summary>Builds a minimal, valid one-page-per-string PDF with correct cross-reference offsets.</summary>
    private static byte[] BuildPdf(params string[] pageTexts)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', pageTexts.Select((_, i) => $"{3 + i * 2} 0 R"))}] /Count {pageTexts.Length} >>"
        };
        var fontObjectNumber = 3 + pageTexts.Length * 2;
        for (var i = 0; i < pageTexts.Length; i++)
        {
            var content = $"BT /F1 24 Tf 72 700 Td ({pageTexts[i]}) Tj ET";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {4 + i * 2} 0 R /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> >>");
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(builder.Length);
            builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = builder.Length;
        builder.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    [Fact]
    public async Task Extract_Pdf_ReturnsOneSegmentPerPageWithPageNumbers()
    {
        var segments = await Create().ExtractAsync(new MemoryStream(BuildPdf("Hello first page", "Hello second page")), "policy.pdf");

        Assert.Equal(2, segments.Count);
        Assert.Equal(1, segments[0].PageNumber);
        Assert.Contains("first page", segments[0].Text);
        Assert.Equal(2, segments[1].PageNumber);
        Assert.Contains("second page", segments[1].Text);
        Assert.All(segments, s => Assert.Null(s.Section));
    }

    [Fact]
    public async Task Extract_Pdf_RejectsDocumentsOverTheLimit()
    {
        var pages = Enumerable.Range(0, 3).Select(_ => new string('a', 600)).ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(maxCharacters: 1000).ExtractAsync(new MemoryStream(BuildPdf(pages)), "big.pdf"));
    }

    [Fact]
    public async Task Extract_CorruptPdf_IsReportedAsUnreadable()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create().ExtractAsync(Utf8("%PDF-garbage that is not a pdf"), "broken.pdf"));

        Assert.Contains("could not be read", ex.Message);
    }
}
