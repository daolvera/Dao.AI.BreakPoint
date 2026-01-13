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
    private readonly bool _usesDevelopmentStorage;

    /// <summary>
    /// Creates a new AzureBlobStorageService using the provided options.
    /// Used for direct configuration (non-Aspire scenarios).
    /// </summary>
    public AzureBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var storageOptions = options.Value;
        _blobServiceClient = new BlobServiceClient(storageOptions.ConnectionString);
        _videoContainerClient = _blobServiceClient.GetBlobContainerClient(
            storageOptions.VideoContainerName
        );
        _imageContainerClient = _blobServiceClient.GetBlobContainerClient(
            storageOptions.ImageContainerName
        );
        _usesDevelopmentStorage =
            storageOptions.ConnectionString?.Contains(
                "UseDevelopmentStorage=true",
                StringComparison.OrdinalIgnoreCase
            ) ?? false;
    }

    /// <summary>
    /// Creates a new AzureBlobStorageService using the Aspire-injected BlobServiceClient.
    /// Used when running with .NET Aspire orchestration.
    /// </summary>
    public AzureBlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
        _videoContainerClient = _blobServiceClient.GetBlobContainerClient(
            BlobStorageOptions.DefaultVideoContainerName
        );
        _imageContainerClient = _blobServiceClient.GetBlobContainerClient(
            BlobStorageOptions.DefaultImageContainerName
        );
        // Aspire uses Azurite for local development
        _usesDevelopmentStorage =
            _blobServiceClient.Uri.Host.Contains("127.0.0.1")
            || _blobServiceClient.Uri.Host.Contains("localhost");
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

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
            // Allow HTTP for Azurite (local development), HTTPS only for production
            Protocol = _usesDevelopmentStorage 
                ? SasProtocol.HttpsAndHttp 
                : SasProtocol.Https,
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }

    private BlobClient GetBlobClientFromUrl(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Azurite URLs include account name as first segment: /devstoreaccount1/container/blob
        // Azure Storage URLs don't: /container/blob
        int containerIndex = 0;
        if (_usesDevelopmentStorage && pathSegments.Length > 0 && pathSegments[0] == "devstoreaccount1")
        {
            containerIndex = 1;
        }

        if (pathSegments.Length < containerIndex + 2)
        {
            throw new ArgumentException($"Invalid blob URL format: {blobUrl}");
        }

        var containerName = pathSegments[containerIndex];
        var blobName = string.Join("/", pathSegments.Skip(containerIndex + 1));

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        return containerClient.GetBlobClient(blobName);
    }
}
