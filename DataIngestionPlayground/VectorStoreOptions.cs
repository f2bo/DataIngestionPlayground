namespace DataIngestionPlayground;

internal class VectorStoreOptions
{
    public string DefaultDatabasePath { get; set; } = "..\\..\\..\\Content\\Database\\CMS.DB";

    public string DefaultMarkdownPath { get; set; } = "..\\..\\..\\Content\\Markdown";

    public string DefaultPdfPath { get; set; } = "..\\..\\..\\Content\\Pdf";

    public string DefaultCollectionName { get; set; } = "documents";

    public string EmbeddingGeneratorModelFilesPath { get; set; } = ".\\models\\all-MiniLM-L6-v2";

    public string VectorStorePath { get; set; } = "..\\..\\..\\VECTOR_STORE.DB";

    public int VectorDimensions { get; set; } = 384;
}
