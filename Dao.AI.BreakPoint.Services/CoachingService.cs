using System.Text.Json;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
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

    public async Task<List<GeneratedDrill>> GenerateDrillRecommendationsAsync(
        DrillRecommendationInput input
    )
    {
        var strokeName = GetStrokeDisplayName(input.StrokeType);
        var playerLevel = GetPlayerLevelFromRating(input.UstaRating);

        // Build phase analysis section with coaching terminology
        var phaseAnalysisSections = BuildPhaseAnalysisSections(input.PhaseAnalyses);

        // Build recent drill feedback section
        var feedbackSection = BuildDrillFeedbackSection(input.RecentDrills);

        var prompt = BuildDrillPrompt(
            strokeName,
            input.OverallQualityScore,
            input.UstaRating,
            playerLevel,
            phaseAnalysisSections,
            feedbackSection,
            input.MaxDrills
        );

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(GetDrillSystemPrompt());
        chatHistory.AddUserMessage(prompt);

        var result = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = _options.MaxTokens,
                    ["temperature"] = _options.Temperature,
                    ["response_format"] = new { type = "json_object" },
                },
            },
            _kernel
        );

        return ParseDrillRecommendations(result.Content ?? "{}");
    }

    private static string BuildPhaseAnalysisSections(List<PhaseAnalysisInput> phaseAnalyses)
    {
        var sections = new List<string>();

        foreach (var phase in phaseAnalyses.OrderBy(p => p.Phase))
        {
            var phaseName = phase.Phase.ToString();
            var score = phase.Score;
            var status = score switch
            {
                >= 85 => "Excellent",
                >= 70 => "Good",
                >= 55 => "Needs improvement",
                _ => "Requires significant work",
            };

            var deviationLines = new List<string>();
            foreach (
                var (featureKey, zScore) in phase
                    .Deviations.OrderByDescending(d => Math.Abs(d.Value))
                    .Take(3)
            )
            {
                var coachingTerm = CoachingFeatureMapper.GetCoachingTerm(featureKey, true); // Default to right-handed for now
                var direction = zScore > 0 ? "above" : "below";
                var severity = Math.Abs(zScore) switch
                {
                    >= 2.0 => "significantly",
                    >= 1.5 => "moderately",
                    _ => "slightly",
                };
                var guidance = CoachingFeatureMapper.GetCoachingGuidance(featureKey);

                deviationLines.Add(
                    $"  - {coachingTerm}: {severity} {direction} target ({zScore:F1}σ). {guidance}"
                );
            }

            sections.Add(
                $"""
                **{phaseName}** (Score: {score}/100 - {status})
                {string.Join("\n", deviationLines)}
                """
            );
        }

        return string.Join("\n\n", sections);
    }

    private static string BuildDrillFeedbackSection(
        List<Data.Models.DrillRecommendation> recentDrills
    )
    {
        if (recentDrills.Count == 0)
        {
            return "No previous drill history available.";
        }

        var feedbackLines = new List<string>();

        var helpfulDrills = recentDrills.Where(d => d.ThumbsUp == true).Take(3);

        var unhelpfulDrills = recentDrills.Where(d => d.ThumbsUp == false).Take(3);

        if (helpfulDrills.Any())
        {
            feedbackLines.Add("Drills the player found helpful:");
            foreach (var drill in helpfulDrills)
            {
                var feedback = string.IsNullOrEmpty(drill.FeedbackText)
                    ? ""
                    : $" - \"{drill.FeedbackText}\"";
                feedbackLines.Add($"  ✓ {drill.DrillName} ({drill.TargetPhase}){feedback}");
            }
        }

        if (unhelpfulDrills.Any())
        {
            feedbackLines.Add("Drills the player did NOT find helpful (avoid similar):");
            foreach (var drill in unhelpfulDrills)
            {
                var feedback = string.IsNullOrEmpty(drill.FeedbackText)
                    ? ""
                    : $" - \"{drill.FeedbackText}\"";
                feedbackLines.Add($"  ✗ {drill.DrillName} ({drill.TargetPhase}){feedback}");
            }
        }

        return string.Join("\n", feedbackLines);
    }

    private static string GetDrillSystemPrompt()
    {
        return """
            You are an expert tennis coach with decades of experience training players of all levels.
            You specialize in biomechanics and technique analysis. Your drill recommendations are practical,
            actionable, and tailored to the player's current skill level.

            When recommending drills:
            - Focus on the most impactful improvements first
            - Provide specific, actionable drill instructions
            - Consider the player's feedback on previous drills
            - Avoid recommending drills similar to ones the player found unhelpful
            - Include more drills similar to ones the player found helpful
            - Each drill should target a specific phase and feature

            You MUST respond with valid JSON in this exact format:
            {
                "drills": [
                    {
                        "targetPhase": "Preparation|Backswing|Contact|FollowThrough",
                        "targetFeature": "specific feature being targeted (e.g., 'racket head speed', 'hip rotation')",
                        "drillName": "Short, memorable drill name (3-5 words)",
                        "description": "Detailed 2-3 sentence description of how to perform the drill",
                        "suggestedDuration": "e.g., '3 sets of 10 reps' or '5 minutes daily'",
                        "priority": 1-3 (1 = highest priority)
                    }
                ]
            }
            """;
    }

    private static string BuildDrillPrompt(
        string strokeName,
        int overallScore,
        double ustaRating,
        string playerLevel,
        string phaseAnalysisSections,
        string feedbackSection,
        int maxDrills
    )
    {
        return $"""
            I need {maxDrills} targeted drill recommendations for a tennis player's {strokeName}.

            **Player Profile:**
            - USTA NTRP Rating: {ustaRating:F1} ({playerLevel})
            - Overall Quality Score: {overallScore}/100

            **Phase-by-Phase Analysis:**
            {phaseAnalysisSections}

            **Previous Drill Feedback:**
            {feedbackSection}

            Based on this analysis, recommend exactly {maxDrills} drills that:
            1. Target the phases/features with the largest deviations from optimal technique
            2. Are appropriate for a {playerLevel} player
            3. Build on drills the player found helpful (if any)
            4. Avoid patterns similar to drills marked as unhelpful

            Respond with JSON only.
            """;
    }

    private static List<GeneratedDrill> ParseDrillRecommendations(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var drills = new List<GeneratedDrill>();

            if (doc.RootElement.TryGetProperty("drills", out var drillsArray))
            {
                foreach (var drillElement in drillsArray.EnumerateArray())
                {
                    var targetPhaseStr =
                        drillElement.GetProperty("targetPhase").GetString() ?? "Contact";
                    if (!Enum.TryParse<SwingPhase>(targetPhaseStr, out var targetPhase))
                    {
                        targetPhase = SwingPhase.Contact;
                    }

                    drills.Add(
                        new GeneratedDrill(
                            TargetPhase: targetPhase,
                            TargetFeature: drillElement.GetProperty("targetFeature").GetString()
                            ?? "",
                            DrillName: drillElement.GetProperty("drillName").GetString() ?? "",
                            Description: drillElement.GetProperty("description").GetString() ?? "",
                            SuggestedDuration: drillElement
                                .GetProperty("suggestedDuration")
                                .GetString()
                            ?? "",
                            Priority: drillElement.GetProperty("priority").GetInt32()
                        )
                    );
                }
            }

            return drills;
        }
        catch (JsonException)
        {
            // Return empty list if parsing fails
            return [];
        }
    }

    // Legacy method maintained for backward compatibility
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
        return featureName.Replace("_", " ").Replace("-", " ").ToLowerInvariant();
    }

    private static List<string> ParseCoachingTips(string response)
    {
        var tips = new List<string>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
                continue;

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
