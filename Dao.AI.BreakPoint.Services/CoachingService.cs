using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.Options;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Dao.AI.BreakPoint.Services;

public class CoachingService : ICoachingService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly AzureOpenAIOptions _options;

    public CoachingService(IOptions<AzureOpenAIOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrEmpty(_options.Endpoint) || string.IsNullOrEmpty(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Azure OpenAI is not configured. Please set Endpoint and ApiKey."
            );
        }

        var builder = Kernel
            .CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: _options.DeploymentName,
                endpoint: _options.Endpoint,
                apiKey: _options.ApiKey
            );

        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<List<string>> GenerateCoachingTipsAsync(
        SwingType strokeType,
        double qualityScore,
        Dictionary<string, double> featureImportance,
        double ustaRating
    )
    {
        var strokeName = GetStrokeDisplayName(strokeType);
        var topFeatures = GetTopNegativeFeatures(featureImportance, 3);
        var playerLevel = GetPlayerLevelFromRating(ustaRating);

        var prompt = BuildCoachingPrompt(
            strokeName,
            qualityScore,
            topFeatures,
            ustaRating,
            playerLevel
        );

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(GetSystemPrompt());
        chatHistory.AddUserMessage(prompt);

        var result = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = _options.MaxTokens,
                    ["temperature"] = _options.Temperature,
                },
            },
            _kernel
        );

        return ParseCoachingTips(result.Content ?? string.Empty);
    }

    private static string GetSystemPrompt()
    {
        return """
            You are an expert tennis coach with decades of experience training players of all levels.
            You specialize in biomechanics and technique analysis. Your advice is practical, actionable,
            and tailored to the player's current skill level.

            When providing coaching tips:
            - Focus on one improvement at a time
            - Provide specific drills or exercises
            - Explain the "why" behind each suggestion
            - Use encouraging but honest language
            - Reference proper technique when helpful

            Format your response as a numbered list of 3-5 tips.
            Each tip should be 1-2 sentences.
            """;
    }

    private static string BuildCoachingPrompt(
        string strokeName,
        double qualityScore,
        List<(string feature, double importance)> problemFeatures,
        double ustaRating,
        string playerLevel
    )
    {
        var featuresDescription = string.Join(
            "\n",
            problemFeatures.Select(f =>
                $"- {FormatFeatureName(f.feature)}: {f.importance:P0} impact on score"
            )
        );

        return $"""
            I just analyzed a tennis player's {strokeName} and need coaching tips.

            USTA NTRP Rating: {ustaRating:F1} ({playerLevel})
            Quality Score: {qualityScore:F0}/100

            The AI model identified these key areas needing improvement:
            {featuresDescription}

            Please provide 3-5 specific, actionable coaching tips and drills appropriate for a {ustaRating:F1} NTRP rated player
            to help them improve their {strokeName} technique, focusing on the problem areas identified.
            """;
    }

    private static string GetPlayerLevelFromRating(double ustaRating)
    {
        return ustaRating switch
        {
            <= 2.0 => "beginner",
            <= 3.0 => "beginner-intermediate",
            <= 3.5 => "intermediate",
            <= 4.0 => "intermediate-advanced",
            <= 4.5 => "advanced",
            <= 5.0 => "advanced tournament player",
            <= 5.5 => "highly competitive player",
            <= 6.0 => "sectional ranked player",
            <= 6.5 => "nationally ranked player",
            _ => "professional level",
        };
    }

    private static List<(string feature, double importance)> GetTopNegativeFeatures(
        Dictionary<string, double> featureImportance,
        int count
    )
    {
        // Higher importance means this feature contributed more to a potentially lower score
        // We want the features with highest importance as they're likely problem areas
        return featureImportance
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    private static string GetStrokeDisplayName(SwingType strokeType)
    {
        return strokeType switch
        {
            SwingType.ForehandGroundStroke => "forehand groundstroke",
            SwingType.BackhandGroundStroke => "backhand groundstroke",
            _ => strokeType.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatFeatureName(string featureName)
    {
        // Convert technical feature names to readable format
        // e.g., "elbow_angle_at_contact" -> "Elbow angle at contact"
        return featureName.Replace("_", " ").Replace("-", " ").ToLowerInvariant();
    }

    private static List<string> ParseCoachingTips(string response)
    {
        var tips = new List<string>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines or lines that don't look like tips
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Remove common list prefixes like "1.", "1)", "-", "*"
            if (char.IsDigit(trimmed[0]))
            {
                var dotIndex = trimmed.IndexOf('.');
                var parenIndex = trimmed.IndexOf(')');
                var startIndex = Math.Max(dotIndex, parenIndex);
                if (startIndex > 0 && startIndex < 3)
                {
                    trimmed = trimmed[(startIndex + 1)..].Trim();
                }
            }
            else if (trimmed.StartsWith('-') || trimmed.StartsWith('*'))
            {
                trimmed = trimmed[1..].Trim();
            }

            if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 10)
            {
                tips.Add(trimmed);
            }
        }

        return tips;
    }
}
