using System.Diagnostics.CodeAnalysis;
using IngestionPlayground.Readers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;

namespace IngestionPlayground;

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

        Console.WriteLine($"\n# Ingests all documents in table: Articles\n");
        foreach (Article document in databaseReader.Articles)
        {
            IngestionDocument result = await pipeline.ProcessAsync(document, $"{document.Id}");
            LogFakeResult(result); // THIS IS WRONG - ProcessAsync should really return an IngestionResult
        }

        Console.WriteLine("\n# Ingests a filtered subset of documents containing the word 'civilization'.\n");
        foreach (Article document in databaseReader.Articles
            .Where(p => p.Body.Contains("civilization")))
        {
            IngestionDocument result = await pipeline.ProcessAsync(document, $"{document.Id}");
            LogFakeResult(result); // THIS IS WRONG - ProcessAsync should really return an IngestionResult
        }
    }

    public async Task UseMarkdownReader(string collectionName, string source)
    {
        Console.WriteLine($"Using a Markdown reader to process: {Path.GetFullPath(source)}");

        var markdownReader = new MarkdownReader();
        var vectorStoreWriter = CreateVectorStoreWriter(collectionName);
        var chunker = GetChunker();
        using IngestionPipeline<FileInfo, string> pipeline = new(markdownReader, chunker, vectorStoreWriter);

        Console.WriteLine($"\n# Ingests all markdown files.\n");
        foreach (FileInfo file in new DirectoryInfo(source).EnumerateFiles())
        {
            IngestionDocument result = await pipeline.ProcessAsync(file, $"{file.Name}");
            LogFakeResult(result); // THIS IS WRONG - ProcessAsync should really return an IngestionResult
        }

        Console.WriteLine("\n# Ingests a filtered subset of filenames that start with the letter 'p'.\n");
        foreach (FileInfo file in new DirectoryInfo(source).EnumerateFiles()
            .Where(p => p.Name.StartsWith("p", StringComparison.OrdinalIgnoreCase)))
        {
            IngestionDocument result = await pipeline.ProcessAsync(file, $"{file.Name}");
            LogFakeResult(result); // THIS IS WRONG - ProcessAsync should really return an IngestionResult
        }
    }

    public async Task UsePdfReader(string collectionName, string source)
    {
        Console.WriteLine($"Using a PDF reader to process: {Path.GetFullPath(source)}");

        var pdfReader = new PdfReader();
        var vectorStoreWriter = CreateVectorStoreWriter(collectionName);
        var chunker = GetChunker();
        using IngestionPipeline<FileInfo, string> pipeline = new(pdfReader, chunker, vectorStoreWriter);

        Console.WriteLine($"\n# Ingests all PDF files.\n");
        foreach (FileInfo file in new DirectoryInfo(source).EnumerateFiles())
        {
            IngestionDocument result = await pipeline.ProcessAsync(file, $"{file.Name}");
            LogFakeResult(result); // THIS IS WRONG - ProcessAsync should really return an IngestionResult
        }

        Console.WriteLine("\n# Ingests a filtered subset of filenames that start with the letter 'p'.\n");
        foreach (FileInfo file in new DirectoryInfo(source).EnumerateFiles()
            .Where(p => p.Name.StartsWith("p", StringComparison.OrdinalIgnoreCase)))
        {
            IngestionDocument result = await pipeline.ProcessAsync(file, $"{file.Name}");
            LogFakeResult(result); // THIS IS WRONG - ProcessAsync should really return an IngestionResult
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
        => Console.WriteLine($"Processed document {result.DocumentId}: {(result.Succeeded ? "\e[92mSUCCESS\e[0m" : "\e[91mFAILURE\e[0m")}");

    // NOTE: this is faking the result - should really be using LogResult instead
    private static void LogFakeResult(IngestionDocument document)
        => Console.WriteLine($"Processed document {document.Identifier}: {(true ? "\e[92mSUCCESS\e[0m" : "\e[91mFAILURE\e[0m")}");
}
