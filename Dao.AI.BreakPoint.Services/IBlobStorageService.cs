namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Service for uploading and retrieving files from blob storage
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a video file to blob storage
    /// </summary>
    /// <param name="stream">The video stream to upload</param>
    /// <param name="fileName">The file name (typically analysisEventId)</param>
    /// <param name="contentType">The content type of the file</param>
    /// <returns>The URL of the uploaded blob</returns>
    Task<string> UploadVideoAsync(Stream stream, string fileName, string contentType);

    /// <summary>
    /// Uploads an image (skeleton overlay) to blob storage
    /// </summary>
    /// <param name="stream">The image stream to upload</param>
    /// <param name="fileName">The file name</param>
    /// <param name="contentType">The content type of the file</param>
    /// <returns>The URL of the uploaded blob</returns>
    Task<string> UploadImageAsync(Stream stream, string fileName, string contentType);

    /// <summary>
    /// Downloads a file from blob storage
    /// </summary>
    /// <param name="blobUrl">The URL of the blob to download</param>
    /// <returns>The file stream</returns>
    Task<Stream> DownloadAsync(string blobUrl);

    /// <summary>
    /// Deletes a file from blob storage
    /// </summary>
    /// <param name="blobUrl">The URL of the blob to delete</param>
    Task DeleteAsync(string blobUrl);

    /// <summary>
    /// Generates a SAS URL for temporary access to a blob
    /// </summary>
    /// <param name="blobUrl">The URL of the blob</param>
    /// <param name="expiryMinutes">How long the SAS token should be valid</param>
    /// <returns>A URL with SAS token for temporary access</returns>
    Task<string> GetSasUrlAsync(string blobUrl, int expiryMinutes = 60);
}
