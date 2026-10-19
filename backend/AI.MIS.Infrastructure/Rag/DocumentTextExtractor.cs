using System.Text;
using AI.MIS.Application.Rag;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace AI.MIS.Infrastructure.Rag;

public sealed class DocumentTextExtractor(IOptions<RagOptions> options) : IDocumentTextExtractor
{
    private int MaximumCharacters => Math.Clamp(options.Value.MaxExtractedCharacters, 1000, 20_000_000);

    public async Task<IReadOnlyList<DocumentSegment>> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".txt" or ".md" => await ExtractTextAsync(content, MaximumCharacters, cancellationToken),
                ".pdf" => ExtractPdf(content, MaximumCharacters, cancellationToken),
                ".docx" => ExtractDocx(content, MaximumCharacters, cancellationToken),
                _ => throw new InvalidOperationException("Only PDF, DOCX, TXT, and Markdown documents can be indexed.")
            };
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or ArgumentException or
                                          UglyToad.PdfPig.Core.PdfDocumentFormatException or
                                          DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            throw new InvalidOperationException("The uploaded document could not be read. Verify that it is not corrupt or password-protected.", exception);
        }
    }

    private static async Task<IReadOnlyList<DocumentSegment>> ExtractTextAsync(Stream content, int maximumCharacters, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var builder = new StringBuilder(Math.Min(maximumCharacters, 16_384));
        var buffer = new char[8192];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            if (builder.Length + count > maximumCharacters)
                throw new InvalidOperationException($"Extracted document text exceeds the configured limit of {maximumCharacters} characters.");
            builder.Append(buffer, 0, count);
        }
        var text = builder.ToString();
        var section = "Document";
        var segments = new List<DocumentSegment>();
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith('#') || (trimmed.Length < 120 && trimmed.EndsWith(':')))
            {
                section = trimmed.TrimStart('#', ' ').TrimEnd(':').Trim();
                continue;
            }
            segments.Add(new DocumentSegment(trimmed, null, section));
        }
        return segments;
    }

    private static IReadOnlyList<DocumentSegment> ExtractPdf(Stream content, int maximumCharacters, CancellationToken cancellationToken)
    {
        using var pdf = PdfDocument.Open(content);
        var segments = new List<DocumentSegment>();
        var extractedCharacters = 0;
        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = page.Text.Trim();
            if (text.Length > 0)
            {
                extractedCharacters += text.Length;
                if (extractedCharacters > maximumCharacters)
                    throw new InvalidOperationException($"Extracted document text exceeds the configured limit of {maximumCharacters} characters.");
                segments.Add(new DocumentSegment(text, page.Number, null));
            }
        }
        return segments;
    }

    private static IReadOnlyList<DocumentSegment> ExtractDocx(Stream content, int maximumCharacters, CancellationToken cancellationToken)
    {
        using var document = WordprocessingDocument.Open(content, false);
        var mainPart = document.MainDocumentPart;
        var body = mainPart?.Document.Body
            ?? throw new InvalidOperationException("The DOCX document does not contain a readable body.");
        var segments = new List<DocumentSegment>();
        var section = "Document";
        var paragraphBuffer = new StringBuilder();
        var extractedCharacters = 0;
        void Flush()
        {
            var text = paragraphBuffer.ToString().Trim();
            if (text.Length > 0)
            {
                extractedCharacters += text.Length;
                if (extractedCharacters > maximumCharacters)
                    throw new InvalidOperationException($"Extracted document text exceeds the configured limit of {maximumCharacters} characters.");
                segments.Add(new DocumentSegment(text, null, section));
            }
            paragraphBuffer.Clear();
        }

        var paragraphs = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Concat(mainPart!.HeaderParts.SelectMany(part => part.Header.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()))
            .Concat(mainPart.FooterParts.SelectMany(part => part.Footer.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()));
        foreach (var paragraph in paragraphs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = paragraph.InnerText.Trim();
            if (text.Length == 0) continue;
            var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (style?.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) == true)
            {
                Flush();
                section = text;
            }
            else
            {
                if (paragraphBuffer.Length > 0) paragraphBuffer.Append(' ');
                paragraphBuffer.Append(text);
            }
        }
        Flush();
        return segments;
    }
}
