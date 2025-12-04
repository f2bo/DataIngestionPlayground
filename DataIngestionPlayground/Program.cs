using System.CommandLine;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace IngestionPlayground;

// Currently assumes a local ONNX embedding model. Update `EmbeddingGeneratorModelFilesPath`
// and `VectorDimensions` when using a different location or model type or replace with another
// embedding model in ConfigureServices.
internal class Program
{
    const string DefaultDatabasePath = "..\\..\\..\\Content\\Database\\CMS.DB";
    const string DefaultMarkdownPath = "..\\..\\..\\Content\\Markdown";
    const string DefaultPdfPath = "..\\..\\..\\Content\\Pdf";
    const string DefaultCollectionName = "documents";

    const string EmbeddingGeneratorModelFilesPath = "E:\\AI\\ONNX\\models\\optimum\\all-MiniLM-L6-v2";
    const string VectorStorePath = "VECTOR_STORE.DB";
    const int VectorDimensions = 384;

    static void Main(string[] args)
    {
        IServiceProvider services = ConfigureServices(args);

        Option<string> collectionOption = new("--collection") { Description = "Name of the vector store collection", DefaultValueFactory = static _ => DefaultCollectionName };
        Option<string> sourceOption = new("--source") { Description = "Directory path or database name containing the files to ingest" };
        Option<string> readerOption = new("--reader") { Description = "Ingestion document reader to use", Required = true };
        readerOption.AcceptOnlyFromAmong(["markdown", "pdf", "database"]);
        Command ingestCommand = new("ingest", "Ingests content into the vector store") { Options = { readerOption, collectionOption } };

        VectorStoreCommands ingestor = services.GetRequiredService<VectorStoreCommands>();

        ingestCommand.SetAction(async parseResult =>
        {
            string collectionName = parseResult.GetValue(collectionOption)!;
            string? source = parseResult.GetValue(sourceOption);

            switch (parseResult.GetRequiredValue(readerOption))
            {
                case "markdown":
                    await ingestor.UseMarkdownReader(collectionName, source ?? DefaultMarkdownPath);
                    break;
                case "pdf":
                    await ingestor.UsePdfReader(collectionName, source ?? DefaultPdfPath);
                    break;
                case "database":
                    await ingestor.UseDatabaseReader(collectionName, source ?? DefaultDatabasePath);
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

        string onnxModelPath = Path.Combine(EmbeddingGeneratorModelFilesPath, "model.onnx");
        string vocabPath = Path.Combine(EmbeddingGeneratorModelFilesPath, "vocab.txt");
        string vectorStorePath = Path.Combine(AppContext.BaseDirectory, "..\\..\\..", VectorStorePath);

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
            .AddKeyedSingleton<object>("vector-dimensions", VectorDimensions)

            // commands
            .AddSingleton<VectorStoreCommands>();

        return builder.Services.BuildServiceProvider();
    }
}
