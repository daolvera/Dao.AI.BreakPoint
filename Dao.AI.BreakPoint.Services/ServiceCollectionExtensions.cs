using Azure.Storage.Blobs;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.Repositories;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.Extensions.DependencyInjection;

namespace Dao.AI.BreakPoint.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBreakPointServices(this IServiceCollection services)
    {
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        return services;
    }

    public static IServiceCollection AddBreakPointIdentityServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        return services;
    }

    public static IServiceCollection AddAnalysisServices(this IServiceCollection services)
    {
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IAnalysisProcessingService, AnalysisProcessingService>();
        services.AddScoped<IAnalysisRequestRepository, AnalysisRequestRepository>();
        services.AddScoped<IAnalysisResultRepository, AnalysisResultRepository>();
        return services;
    }

    public static IServiceCollection AddSwingAnalyzerServices(
        this IServiceCollection services,
        Action<SwingQualityModelOptions>? configure = null
    )
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SwingQualityModelOptions>(_ => { });
        }

        services.AddSingleton<ISkeletonOverlayService, SkeletonOverlayService>();
        services.AddScoped<ISwingAnalyzerService, SwingAnalyzerService>();
        return services;
    }

    public static IServiceCollection AddBlobStorage(
        this IServiceCollection services,
        Action<BlobStorageOptions>? configure = null
    )
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<BlobStorageOptions>(_ => { });
        }

        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();

        return services;
    }

    /// <summary>
    /// Registers blob storage service using the Aspire-injected BlobServiceClient.
    /// Use this when running with .NET Aspire orchestration.
    /// </summary>
    public static IServiceCollection AddAspirerBlobStorage(this IServiceCollection services)
    {
        services.Configure<BlobStorageOptions>(_ => { });
        services.AddScoped<IBlobStorageService>(sp =>
        {
            var blobServiceClient = sp.GetRequiredService<BlobServiceClient>();
            return new AzureBlobStorageService(blobServiceClient);
        });
        return services;
    }

    /// <summary>
    /// Add coaching service with Azure OpenAI integration
    /// Falls back to static tips if Azure OpenAI is not configured
    /// </summary>
    public static IServiceCollection AddCoachingService(
        this IServiceCollection services,
        Action<AzureOpenAIOptions>? configure = null
    )
    {
        if (configure is not null)
        {
            services.Configure(configure);
            services.AddScoped<ICoachingService, CoachingService>();
        }
        else
        {
            // Use static coaching service as fallback
            services.AddScoped<ICoachingService, StaticCoachingService>();
        }

        return services;
    }
}
