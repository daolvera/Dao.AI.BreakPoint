using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Dao.AI.BreakPoint.Services.Options;
using Microsoft.Extensions.Options;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Azure Blob Storage implementation of IBlobStorageService.
/// Works with both Azure Blob Storage and Azurite for local development.
/// </summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _videoContainerClient;
    private readonly BlobContainerClient _imageContainerClient;
    private readonly BlobStorageOptions _options;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        _blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        _videoContainerClient = _blobServiceClient.GetBlobContainerClient(
            _options.VideoContainerName
        );
        _imageContainerClient = _blobServiceClient.GetBlobContainerClient(
            _options.ImageContainerName
        );
    }

    public async Task<string> UploadVideoAsync(Stream stream, string fileName, string contentType)
    {
        await _videoContainerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = _videoContainerClient.GetBlobClient(fileName);
        var headers = new BlobHttpHeaders { ContentType = contentType };

        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });

        return blobClient.Uri.ToString();
    }

    public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType)
    {
        await _imageContainerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = _imageContainerClient.GetBlobClient(fileName);
        var headers = new BlobHttpHeaders { ContentType = contentType };

        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });

        return blobClient.Uri.ToString();
    }

    public async Task<Stream> DownloadAsync(string blobUrl)
    {
        var blobClient = GetBlobClientFromUrl(blobUrl);
        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public async Task DeleteAsync(string blobUrl)
    {
        var blobClient = GetBlobClientFromUrl(blobUrl);
        await blobClient.DeleteIfExistsAsync();
    }

    public Task<string> GetSasUrlAsync(string blobUrl, int expiryMinutes = 60)
    {
        var blobClient = GetBlobClientFromUrl(blobUrl);

        // Check if the connection is using development storage (Azurite)
        // Azurite doesn't require SAS tokens for local development
        if (
            _options.ConnectionString.Contains(
                "UseDevelopmentStorage=true",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Task.FromResult(blobUrl);
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }

    private BlobClient GetBlobClientFromUrl(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length < 2)
        {
            throw new ArgumentException($"Invalid blob URL format: {blobUrl}");
        }

        var containerName = pathSegments[0];
        var blobName = string.Join("/", pathSegments.Skip(1));

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        return containerClient.GetBlobClient(blobName);
    }
}
