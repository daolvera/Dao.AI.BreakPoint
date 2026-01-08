using Azure.Storage.Blobs;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.Repositories;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.Extensions.Configuration;
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
        services.AddScoped<ICoachingService, CoachingService>();
        services.AddScoped<IDrillRecommendationService, DrillRecommendationService>();
        return services;
    }

    public static IServiceCollection AddSwingAnalyzerServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Configure MoveNet options and resolve relative paths
        services.Configure<MoveNetOptions>(configuration.GetSection(MoveNetOptions.SectionName));
        services.PostConfigure<MoveNetOptions>(options =>
        {
            options.ModelPath = ResolveModelPath(options.ModelPath);
        });

        // Configure SwingPhaseClassifier options and resolve relative paths
        services.Configure<SwingPhaseClassifierOptions>(
            configuration.GetSection(SwingPhaseClassifierOptions.SectionName)
        );
        services.PostConfigure<SwingPhaseClassifierOptions>(options =>
        {
            options.ModelPath = ResolveModelPath(options.ModelPath);
        });

        // Configure SwingQualityModel options and resolve relative paths
        services.Configure<SwingQualityModelOptions>(
            configuration.GetSection(SwingQualityModelOptions.SectionName)
        );
        services.PostConfigure<SwingQualityModelOptions>(options =>
        {
            if (!string.IsNullOrEmpty(options.ModelsDirectory))
            {
                options.ModelsDirectory = ResolveModelPath(options.ModelsDirectory);
            }
            if (!string.IsNullOrEmpty(options.ReferenceProfilesPath))
            {
                options.ReferenceProfilesPath = ResolveModelPath(options.ReferenceProfilesPath);
            }
        });

        services.AddSingleton<ISkeletonOverlayService, SkeletonOverlayService>();
        services.AddScoped<ISwingAnalyzerService, SwingAnalyzerService>();
        return services;
    }

    /// <summary>
    /// Resolves a model path relative to the application's base directory.
    /// If the path is already absolute, returns it unchanged.
    /// </summary>
    private static string ResolveModelPath(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            return modelPath;
        }

        // If already absolute, return as-is
        if (Path.IsPathRooted(modelPath))
        {
            return modelPath;
        }

        // Resolve relative to the application's base directory
        var basePath = AppContext.BaseDirectory;
        return Path.Combine(basePath, modelPath);
    }

    /// <summary>
    /// Registers Azure OpenAI services for coaching functionality.
    /// </summary>
    public static IServiceCollection AddAzureOpenAIServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<AzureOpenAIOptions>(
            configuration.GetSection(AzureOpenAIOptions.SectionName)
        );
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
}
