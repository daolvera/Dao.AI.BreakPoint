namespace Dao.AI.BreakPoint.Services.Options;

public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";
    public const string DefaultVideoContainerName = "swing-analysis";
    public const string DefaultImageContainerName = "analysis-images";

    /// <summary>
    /// Connection string for Azure Blob Storage (use Azurite for local development)
    /// </summary>
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";

    /// <summary>
    /// Container name for video uploads
    /// </summary>
    public string VideoContainerName { get; set; } = DefaultVideoContainerName;

    /// <summary>
    /// Container name for generated images (skeleton overlays)
    /// </summary>
    public string ImageContainerName { get; set; } = DefaultImageContainerName;
}
