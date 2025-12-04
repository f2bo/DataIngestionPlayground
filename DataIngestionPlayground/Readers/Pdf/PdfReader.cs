using Microsoft.Extensions.DataIngestion;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace DataIngestionPlayground.Readers;

internal class PdfReader : IngestionDocumentReader
{
    /// <inheritdoc/>
    public override Task<IngestionDocument> ReadAsync(Stream source, string identifier, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        using PdfDocument pdf = PdfDocument.Open(source);

        var document = new IngestionDocument(identifier);
        foreach (Page page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            document.Sections.Add(GetPageSection(page));
        }

        return Task.FromResult(document);
    }

    private static IngestionDocumentSection GetPageSection(Page pdfPage)
    {
        var section = new IngestionDocumentSection
        {
            PageNumber = pdfPage.Number,
        };

        var letters = pdfPage.Letters;
        var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);

        foreach (var textBlock in DocstrumBoundingBoxes.Instance.GetBlocks(words))
        {
            section.Elements.Add(new IngestionDocumentParagraph(textBlock.Text)
            {
                Text = textBlock.Text
            });
        }

        return section;
    }
}
