using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DataIngestion;

namespace DataIngestionPlayground.Readers;

/// <summary>
/// Implements a sample <see cref="IIngestionDocumentReader{T}"/> for 
/// ingesting <see cref="Article"/> documents from a database.
/// </summary>
public class DatabaseIngestionReader(DbContextOptions options)
    : DbContext(options), IIngestionDocumentReader<Article>
{
    public DbSet<Article> Articles { get; set; }

    /// <inheritdoc />
    public Task<IngestionDocument> ReadAsync(Article source, string identifier, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        var document = ParseDocument(source);

        return Task.FromResult(document);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title);
            entity.Property(e => e.Body);
        });

        // maps to the "Items" table in the **sample** database
        modelBuilder.Entity<Article>().ToTable("Items");

        base.OnModelCreating(modelBuilder);
    }

    private static IngestionDocument ParseDocument(Article article)
    {
        IngestionDocumentSection section = new();

        section.Elements.Add(new IngestionDocumentHeader($"# {article.Title}"));

        foreach (ReadOnlySpan<char> paragraph in article.Body.AsSpan().EnumerateLines())
        {
            if (!paragraph.Trim().IsEmpty)
            {
                section.Elements.Add(new IngestionDocumentParagraph(paragraph.ToString()));
            }
        }

        IngestionDocument document = new(article.Id.ToString());

        document.Sections.Add(section);

        return document;
    }
}
