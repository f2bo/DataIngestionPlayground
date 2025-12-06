using System.CommandLine;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace DataIngestionPlayground;

// Currently assumes a local ONNX embedding model. Update appsettings.json when using a different
// location or ONNX model type or replace with a different embedding model in ConfigureServices.
internal class Program
{
    private static VectorStoreOptions VectorStoreOptions = new();

    static void Main(string[] args)
    {
        using ServiceProvider services = ConfigureServices(args);

        Option<string> collectionOption = new("--collection") { Description = "Name of the vector store collection", DefaultValueFactory = static _ => VectorStoreOptions.DefaultCollectionName };
        Option<string> sourceOption = new("--source") { Description = "Directory path or database name containing the files to ingest" };
        Option<string> readerOption = new("--reader") { Description = "Ingestion document reader to use", Required = true };
        readerOption.AcceptOnlyFromAmong(["markdown", "pdf", "database"]);
        Command ingestCommand = new("ingest", "Ingests content into the vector store") { Options = { readerOption, collectionOption, sourceOption } };

        VectorStoreCommands ingestor = services.GetRequiredService<VectorStoreCommands>();

        ingestCommand.SetAction(async parseResult =>
        {
            string collectionName = parseResult.GetValue(collectionOption)!;
            string? source = parseResult.GetValue(sourceOption);

            switch (parseResult.GetRequiredValue(readerOption))
            {
                case "markdown":
                    await ingestor.UseMarkdownReader(collectionName, source ?? VectorStoreOptions.DefaultMarkdownPath);
                    break;
                case "pdf":
                    await ingestor.UsePdfReader(collectionName, source ?? VectorStoreOptions.DefaultPdfPath);
                    break;
                case "database":
                    await ingestor.UseDatabaseReader(collectionName, source ?? VectorStoreOptions.DefaultDatabasePath);
                    break;
            }
        });

        Command searchCommand = new("search", "Searches the vector store") { Options = { collectionOption } };
        searchCommand.SetAction(parseResult => ingestor.SearchCollectionAsync(parseResult.GetValue(collectionOption)!));

        Command listCommand = new("list", "Lists vector store collections");
        listCommand.SetAction(parseResult => ingestor.ListCollectionsAsync());

        RootCommand rootCommand = new("Data Ingestion Playground") { Subcommands = { ingestCommand, searchCommand, listCommand } };
        rootCommand.Parse(args).Invoke();
    }

    private static ServiceProvider ConfigureServices(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        VectorStoreOptions = builder.Configuration.Get<VectorStoreOptions>() ?? new();

        string onnxModelPath = Path.Combine(VectorStoreOptions.EmbeddingGeneratorModelFilesPath, "model.onnx");
        string vocabPath = Path.Combine(VectorStoreOptions.EmbeddingGeneratorModelFilesPath, "vocab.txt");
        string vectorStorePath = Path.Combine(AppContext.BaseDirectory, VectorStoreOptions.VectorStorePath);

        builder.Services
            // tokenizer
            .AddSingleton<Tokenizer>(_ => BertTokenizer.Create(vocabPath))

            // embedding generator
            .AddBertOnnxEmbeddingGenerator(onnxModelPath, vocabPath)

            // vector store
            .AddSqliteVectorStore(_ => $"Data Source={vectorStorePath}", sp => new SqliteVectorStoreOptions()
            {
                EmbeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()
            })
            .AddKeyedSingleton<object>("vector-dimensions", VectorStoreOptions.VectorDimensions)

            // commands
            .AddSingleton<VectorStoreCommands>();

        return builder.Services.BuildServiceProvider();
    }
}
