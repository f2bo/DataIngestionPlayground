namespace DataIngestionPlayground;

/// <summary>
/// An entity class defining the schema of documents in the sample database.
/// </summary>
public class Article
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
