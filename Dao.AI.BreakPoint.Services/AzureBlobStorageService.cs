using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Dao.AI.BreakPoint.Services.Options;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AzureBlobStorageService>? _logger;

    /// <summary>
    /// Creates a new AzureBlobStorageService using the provided options.
    /// Used for direct configuration (non-Aspire scenarios).
    /// </summary>
    public AzureBlobStorageService(
        IOptions<BlobStorageOptions> options,
        ILogger<AzureBlobStorageService>? logger = null
    )
    {
        _logger = logger;
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

        _logger?.LogInformation(
            "AzureBlobStorageService initialized with connection string. Development storage: {IsDev}",
            _usesDevelopmentStorage
        );
    }

    /// <summary>
    /// Creates a new AzureBlobStorageService using the Aspire-injected BlobServiceClient.
    /// Used when running with .NET Aspire orchestration.
    /// </summary>
    public AzureBlobStorageService(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobStorageService>? logger = null
    )
    {
        _logger = logger;
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

        _logger?.LogInformation(
            "AzureBlobStorageService initialized with BlobServiceClient. Account: {Account}, CanGenerateSas: {CanGenerateSas}, Development storage: {IsDev}",
            _blobServiceClient.AccountName,
            _videoContainerClient.CanGenerateSasUri,
            _usesDevelopmentStorage
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

    public async Task<string> GetSasUrlAsync(string blobUrl, int expiryMinutes = 60)
    {
        var blobClient = GetBlobClientFromUrl(blobUrl);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
            // Allow HTTP for Azurite (local development), HTTPS only for production
            Protocol = _usesDevelopmentStorage ? SasProtocol.HttpsAndHttp : SasProtocol.Https,
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        // Try to generate SAS using shared key (works with connection strings)
        // If that fails (e.g., when using Managed Identity), use User Delegation SAS
        if (blobClient.CanGenerateSasUri)
        {
            _logger?.LogDebug(
                "Generating SAS URL using shared key for blob: {BlobName}",
                blobClient.Name
            );
            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }

        // Use User Delegation SAS for Azure AD/Managed Identity authentication
        _logger?.LogDebug(
            "Generating User Delegation SAS URL (Managed Identity) for blob: {BlobName}",
            blobClient.Name
        );
        var userDelegationKey = await _blobServiceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
        );

        var sasToken = sasBuilder.ToSasQueryParameters(
            userDelegationKey.Value,
            _blobServiceClient.AccountName
        );
        var uriBuilder = new UriBuilder(blobClient.Uri) { Query = sasToken.ToString() };

        return uriBuilder.Uri.ToString();
    }

    private BlobClient GetBlobClientFromUrl(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Azurite URLs include account name as first segment: /devstoreaccount1/container/blob
        // Azure Storage URLs don't: /container/blob
        int containerIndex = 0;
        if (
            _usesDevelopmentStorage
            && pathSegments.Length > 0
            && pathSegments[0] == "devstoreaccount1"
        )
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
