namespace Dao.AI.BreakPoint.Services.Options;

public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>
    /// Connection string for Azure Blob Storage (use Azurite for local development)
    /// </summary>
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";

    /// <summary>
    /// Container name for video uploads
    /// </summary>
    public string VideoContainerName { get; set; } = "swing-analysis";

    /// <summary>
    /// Container name for generated images (skeleton overlays)
    /// </summary>
    public string ImageContainerName { get; set; } = "analysis-images";
}
