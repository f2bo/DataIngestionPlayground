using System.Diagnostics.CodeAnalysis;
using DataIngestionPlayground.Readers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;

namespace DataIngestionPlayground;

internal class VectorStoreCommands(
    Tokenizer tokenizer,
    VectorStore vectorStore,
    [FromKeyedServices("vector-dimensions")] object dimensions,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    public async Task UseDatabaseReader(string collectionName, string source)
    {
        Console.WriteLine($"Using a Database reader to process database: {source}");

        var optionsBuilder = new DbContextOptionsBuilder<DatabaseIngestionReader>().UseSqlite($"Data Source={source}");

        using var databaseReader = new DatabaseIngestionReader(optionsBuilder.Options);
        var vectorStoreWriter = CreateVectorStoreWriter(collectionName);
        var chunker = GetChunker();
        using IngestionPipeline<Article, string> pipeline = new(databaseReader, chunker, vectorStoreWriter);

        foreach (Article article in databaseReader.Articles)
        {
            Exception? failure = null;
            IngestionDocument? document = null;

            try
            {
                document = await pipeline.ProcessAsync(article, $"Article #{article.Id}");
            }
            catch (Exception e)
            {
                failure = e;
            }

            // cannot do this because the IngestionResult constructor is internal
            //LogResult(new IngestionResult(document?.Identifier ?? article.Id.ToString(), document, failure));

            LogResult(document?.Identifier ?? article.Id.ToString(), failure == null, failure);
        }
    }

    public async Task UseMarkdownReader(string collectionName, string source)
    {
        Console.WriteLine($"Using a Markdown reader to process: {Path.GetFullPath(source)}");

        var markdownReader = new MarkdownReader();
        var vectorStoreWriter = CreateVectorStoreWriter(collectionName);
        var chunker = GetChunker();
        using IngestionPipeline<FileInfo, string> pipeline = new(markdownReader, chunker, vectorStoreWriter);

        await foreach (IngestionResult result in pipeline.ProcessAsync(new DirectoryInfo(source)))
        {
            LogResult(result);
        }
    }

    public async Task UsePdfReader(string collectionName, string source)
    {
        Console.WriteLine($"Using a PDF reader to process: {Path.GetFullPath(source)}");

        var pdfReader = new PdfReader();
        var vectorStoreWriter = CreateVectorStoreWriter(collectionName);
        var chunker = GetChunker();
        using IngestionPipeline<FileInfo, string> pipeline = new(pdfReader, chunker, vectorStoreWriter);

        await foreach (IngestionResult result in pipeline.ProcessAsync(new DirectoryInfo(source)))
        {
            LogResult(result);
        }
    }

    public async Task SearchCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        const string KeyName = "key";
        const string EmbeddingName = "embedding";
        const string ContentName = "content";
        const string ContextName = "context";
        const string DocumentIdName = "documentid";

        VectorStoreCollectionDefinition collectionDefinition = new()
        {
            Properties =
            {
                new VectorStoreKeyProperty(KeyName, typeof(Guid)),
                new VectorStoreVectorProperty(EmbeddingName, typeof(string), (int)dimensions)
                {
                    DistanceFunction = DistanceFunction.CosineDistance,
                },
                new VectorStoreDataProperty(ContentName, typeof(string)),
                new VectorStoreDataProperty(ContextName, typeof(string)),
                new VectorStoreDataProperty(DocumentIdName, typeof(string)) { IsIndexed = true }
            }
        };

        VectorStoreCollection<object, Dictionary<string, object?>> collection = vectorStore.GetDynamicCollection(collectionName, collectionDefinition);

        Console.WriteLine($"Searching the vector store '{collectionName}' collection...");

        while (ReadQuery(out string? query))
        {
            await foreach (VectorSearchResult<Dictionary<string, object?>> result in collection.SearchAsync(query, 1, cancellationToken: cancellationToken))
            {
                DisplayField("score", result.Score);
                foreach ((string name, object? value) in result.Record)
                {
                    DisplayField(name, value);
                }

                Console.WriteLine();
            }
        }

        static void DisplayField(string name, object? value)
            => Console.WriteLine($"\e[93m{name,-12}:\e[0m {value}");

        static bool ReadQuery([NotNullWhen(true)] out string? query)
        {
            Console.WriteLine("".PadRight(Console.WindowWidth, '-'));
            Console.Write("\e[96mQ: ");

            query = Console.ReadLine();

            Console.Write("\e[0m");

            return !string.IsNullOrWhiteSpace(query);
        }
    }

    public async Task ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        await foreach (string name in vectorStore.ListCollectionNamesAsync(cancellationToken))
        {
            Console.WriteLine(name);
        }
    }

    private IngestionChunker<string> GetChunker()
        => new SemanticSimilarityChunker(embeddingGenerator, new IngestionChunkerOptions(tokenizer));

    private VectorStoreWriter<string> CreateVectorStoreWriter(string collectionName)
    {
        return new(vectorStore, (int)dimensions, new VectorStoreWriterOptions()
        {
            CollectionName = collectionName,
            DistanceFunction = DistanceFunction.CosineDistance,
            IncrementalIngestion = false,
        });
    }

    private static void LogResult(IngestionResult result)
        => LogResult(result.DocumentId, result.Succeeded, result.Exception);

    private static void LogResult(string documentId, bool succeeded, Exception? exception)
        => Console.WriteLine($"Processed {documentId}: {(succeeded ? "\e[92mSUCCESS\e[0m" : $"\e[91mFAILURE\e[0m - {GetFullExceptionMessage(exception)}")}");

    private static string GetFullExceptionMessage(Exception? exception)
    {
        static IEnumerable<string> GetExceptionMessages(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                yield return current.Message;
            }
        }

        return exception is not null ? string.Join(" ", GetExceptionMessages(exception)) : "Not available";
    }
}
