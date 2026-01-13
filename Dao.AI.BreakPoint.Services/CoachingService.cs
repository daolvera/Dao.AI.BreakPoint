using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;

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

        // Check if we have any meaningful deviations
        var hasDeviations = input.PhaseAnalyses.Any(p => p.Deviations.Count > 0);

        string prompt;
        if (hasDeviations)
        {
            // Build phase analysis section with coaching terminology
            var phaseAnalysisSections = BuildPhaseAnalysisSections(input.PhaseAnalyses);

            // Build recent drill feedback section
            var feedbackSection = BuildDrillFeedbackSection(input.RecentDrills);

            // Build player history section
            var historySection = BuildPlayerHistorySection(input.PlayerHistorySummary);

            prompt = BuildDrillPrompt(
                strokeName,
                input.OverallQualityScore,
                input.UstaRating,
                playerLevel,
                phaseAnalysisSections,
                feedbackSection,
                historySection,
                input.MaxDrills
            );
        }
        else
        {
            // No deviations - generate general drills based on stroke type and level
            prompt = BuildGeneralDrillPrompt(
                strokeName,
                input.OverallQualityScore,
                input.UstaRating,
                playerLevel,
                input.PlayerHistorySummary,
                input.MaxDrills
            );
        }

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(GetDrillSystemPrompt());
        chatHistory.AddUserMessage(prompt);

        var result = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            new OpenAIPromptExecutionSettings
            {
                MaxTokens = _options.MaxTokens,
                Temperature = _options.Temperature,
                ResponseFormat = "json_object",
            },
            _kernel
        );

        return ParseDrillRecommendations(result.Content ?? "{}");
    }

    public async Task<string> GenerateTrainingHistorySummaryAsync(TrainingHistoryInput input)
    {
        var strokeName = GetStrokeDisplayName(input.StrokeType);
        var playerLevel = GetPlayerLevelFromRating(input.UstaRating);

        var drillsList = string.Join(
            "\n",
            input.NewDrills.Select(d =>
                $"  - {d.DrillName} ({d.TargetPhase}, Priority {d.Priority}): {d.Description}"
            )
        );

        var previousSummarySection = string.IsNullOrWhiteSpace(input.PreviousHistorySummary)
            ? "This is the player's first analysis session - no previous training history exists."
            : $"""
                **Previous Training History Summary:**
                {input.PreviousHistorySummary}
                """;

        var prompt = $"""
            Generate an updated training history summary for this tennis player.

            **Player Profile:**
            - Name: {input.PlayerName}
            - USTA NTRP Rating: {input.UstaRating:F1} ({playerLevel})

            **Current Analysis:**
            - Stroke Type: {strokeName}
            - Overall Quality Score: {input.OverallQualityScore}/100

            **New Drills Recommended:**
            {drillsList}

            {previousSummarySection}

            Generate a concise training history summary (2-4 sentences) that:
            1. Incorporates key insights from the new analysis
            2. Tracks patterns in areas needing improvement
            3. Notes any progression or recurring challenges
            4. Maintains context from the previous summary if available
            5. Maintain a positive attitude about the players progress

            Respond with ONLY the summary text, no JSON or formatting.
            Keep it under 500 characters to maintain context efficiency.
            """;

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(GetTrainingHistorySystemPrompt());
        chatHistory.AddUserMessage(prompt);

        var result = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            new OpenAIPromptExecutionSettings { MaxTokens = 200, Temperature = 0.5 },
            _kernel
        );

        return result.Content?.Trim() ?? string.Empty;
    }

    private static string GetTrainingHistorySystemPrompt()
    {
        return """
            You are an expert tennis coach maintaining training notes for your players.
            Your summaries are concise, actionable, and track player progression over time.
            Focus on patterns in technique issues and improvements observed across sessions.
            Write in third person (e.g., "Player shows..." not "You show...").
            """;
    }

    private static string BuildGeneralDrillPrompt(
        string strokeName,
        int overallScore,
        double ustaRating,
        string playerLevel,
        string? playerHistorySummary,
        int maxDrills
    )
    {
        var historySection = BuildPlayerHistorySection(playerHistorySummary);

        return $"""
            I need {maxDrills} general drill recommendations for a tennis player to improve their {strokeName}.

            **Player Profile:**
            - USTA NTRP Rating: {ustaRating:F1} ({playerLevel})
            - Overall Quality Score: {overallScore}/100

            {historySection}

            Since no specific technique deviations were detected, please recommend {maxDrills} foundational drills that:
            1. Are fundamental practice exercises for improving {strokeName} technique
            2. Are appropriate for a {playerLevel} player
            3. Cover different aspects of the stroke (preparation, swing mechanics, follow-through)
            4. Focus on building consistency and proper form
            5. Consider the player's training history if available

            Respond with JSON only.
            """;
    }

    private static string BuildPlayerHistorySection(string? playerHistorySummary)
    {
        if (string.IsNullOrWhiteSpace(playerHistorySummary))
        {
            return "**Player Training History:** No previous training history available.";
        }

        return $"""
            **Player Training History:**
            {playerHistorySummary}
            """;
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
        string historySection,
        int maxDrills
    )
    {
        return $"""
            I need {maxDrills} drill recommendations for a tennis player's {strokeName}.

            **Player Profile:**
            - USTA NTRP Rating: {ustaRating:F1} ({playerLevel})
            - Overall Quality Score: {overallScore}/100

            {historySection}

            **Phase-by-Phase Analysis:**
            {phaseAnalysisSections}

            **Previous Drill Feedback:**
            {feedbackSection}

            Based on this analysis, recommend exactly {maxDrills} drills that:
            1. Target the phases/features with the largest deviations from optimal technique
            2. Are appropriate for a {playerLevel} player
            3. Build on drills the player found helpful (if any)
            4. Avoid patterns similar to drills marked as unhelpful
            5. Consider the player's training history for continuity

            Keep the drills practical and easy to implement in regular practice.

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

    private static string GetStrokeDisplayName(SwingType strokeType)
    {
        return strokeType switch
        {
            SwingType.ForehandGroundStroke => "forehand groundstroke",
            SwingType.BackhandGroundStroke => "backhand groundstroke",
            _ => strokeType.ToString().ToLowerInvariant(),
        };
    }
}
