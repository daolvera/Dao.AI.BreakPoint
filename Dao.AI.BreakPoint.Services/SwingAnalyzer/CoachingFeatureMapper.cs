using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Maps technical feature indices to coaching-friendly terminology.
/// Handles handedness-aware naming (e.g., "dominant arm" vs "left/right arm").
/// </summary>
public static class CoachingFeatureMapper
{
    /// <summary>
    /// Feature mappings with coaching descriptions.
    /// Each entry contains: technical name, coaching term for right-handed, coaching term for left-handed, coaching guidance
    /// </summary>
    private static readonly (
        string TechnicalName,
        string RightHandedTerm,
        string LeftHandedTerm,
        string CoachingGuidance
    )[] FeatureMappings =
    [
        // Velocities (indices 0-5)
        (
            "Right Wrist Speed",
            "racket head speed",
            "racket head speed",
            "Focus on accelerating through the ball. A faster racket head generates more power and spin."
        ),
        (
            "Right Wrist Acceleration",
            "wrist snap timing",
            "wrist snap timing",
            "Work on the timing of your wrist acceleration at contact. The snap should occur just before impact."
        ),
        (
            "Left Wrist Speed",
            "non-dominant arm speed",
            "non-dominant arm speed",
            "Your non-hitting arm helps with balance and rotation. Pull it back to help rotate your body."
        ),
        (
            "Right Elbow Speed",
            "arm extension speed",
            "arm extension speed",
            "Your elbow should extend smoothly through the swing. Rushing or stopping the extension affects control."
        ),
        (
            "Right Shoulder Speed",
            "shoulder rotation speed",
            "shoulder rotation speed",
            "Shoulder rotation drives power in groundstrokes. A faster, controlled rotation generates more pace."
        ),
        (
            "Hip Rotation Speed",
            "hip rotation",
            "hip rotation",
            "Your hips initiate the kinetic chain. Rotate your hips toward the target before your upper body."
        ),
        // Angles (indices 6-11)
        (
            "Right Elbow Angle",
            "hitting arm elbow bend",
            "hitting arm elbow bend",
            "Maintain a slight bend in your hitting elbow. Too straight reduces control; too bent limits reach."
        ),
        (
            "Left Elbow Angle",
            "off-arm elbow position",
            "off-arm elbow position",
            "Your non-hitting arm should be raised and bent during the backswing, then pull back during the swing."
        ),
        (
            "Right Shoulder Angle",
            "shoulder turn",
            "shoulder turn",
            "A good shoulder turn creates the 'X-factor' coil. Turn your shoulders at least 90 degrees on the backswing."
        ),
        (
            "Left Shoulder Angle",
            "body alignment",
            "body alignment",
            "Your non-dominant shoulder points toward the incoming ball during preparation. This ensures proper alignment."
        ),
        (
            "Right Hip Angle",
            "hip coil",
            "hip coil",
            "Load your weight into your back hip during the backswing. This creates elastic energy for the forward swing."
        ),
        (
            "Right Knee Angle",
            "leg drive",
            "leg drive",
            "Bend your knees to get low to the ball and drive up through contact. This adds power from the ground up."
        ),
        // Relative Positions (indices 12-15)
        (
            "Right Wrist X (relative)",
            "racket position (horizontal)",
            "racket position (horizontal)",
            "Your racket should be behind you on the backswing and extend forward through contact. Watch the horizontal path."
        ),
        (
            "Right Wrist Y (relative)",
            "contact point height",
            "contact point height",
            "Strike the ball at the optimal height - typically between hip and shoulder level for groundstrokes."
        ),
        (
            "Right Elbow X (relative)",
            "elbow position",
            "elbow position",
            "Keep your elbow close to your body during the swing. A 'flying elbow' reduces power and control."
        ),
        (
            "Right Elbow Y (relative)",
            "elbow height",
            "elbow height",
            "Your elbow height affects the swing plane. For topspin, the elbow lifts through the contact zone."
        ),
        // Arm Configuration (indices 16-18)
        (
            "Wrist-to-Shoulder X",
            "arm extension (horizontal)",
            "arm extension (horizontal)",
            "Extend your arm toward the target at contact. This maximizes reach and ensures a clean hit."
        ),
        (
            "Wrist-to-Shoulder Y",
            "racket preparation height",
            "racket preparation height",
            "Your racket should drop below the ball level on the backswing to create low-to-high topspin swing."
        ),
        (
            "Wrist Height Diff",
            "swing path elevation",
            "swing path elevation",
            "A proper low-to-high swing path creates topspin. The wrist should finish above the shoulder."
        ),
        // Handedness (index 19)
        (
            "Handedness",
            "grip orientation",
            "grip orientation",
            "Ensure your grip matches your dominant hand. The correct grip is essential for all strokes."
        ),
    ];

    /// <summary>
    /// Lookup dictionary from technical name to index
    /// </summary>
    private static readonly Dictionary<string, int> _technicalNameToIndex = FeatureMappings
        .Select((m, idx) => (m.TechnicalName, Index: idx))
        .ToDictionary(x => x.TechnicalName, x => x.Index, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Get feature index from technical name
    /// </summary>
    public static int GetFeatureIndex(string technicalName)
    {
        return _technicalNameToIndex.TryGetValue(technicalName, out var index) ? index : -1;
    }

    /// <summary>
    /// Get coaching-friendly name for a feature by technical name
    /// </summary>
    public static string GetCoachingTerm(string technicalName, bool isRightHanded = true)
    {
        var index = GetFeatureIndex(technicalName);
        if (index < 0)
            return technicalName; // Return original if not found
        return GetCoachingTerm(index, isRightHanded);
    }

    /// <summary>
    /// Get coaching guidance for a feature by technical name
    /// </summary>
    public static string GetCoachingGuidance(string technicalName)
    {
        var index = GetFeatureIndex(technicalName);
        if (index < 0)
            return "Focus on overall technique improvement.";
        return GetCoachingGuidance(index);
    }

    /// <summary>
    /// Get coaching-friendly name for a feature index based on player handedness
    /// </summary>
    public static string GetCoachingTerm(int featureIndex, bool isRightHanded)
    {
        if (featureIndex < 0 || featureIndex >= FeatureMappings.Length)
            return "unknown feature";

        var mapping = FeatureMappings[featureIndex];
        return isRightHanded ? mapping.RightHandedTerm : mapping.LeftHandedTerm;
    }

    /// <summary>
    /// Get technical feature name
    /// </summary>
    public static string GetTechnicalName(int featureIndex)
    {
        if (featureIndex < 0 || featureIndex >= FeatureMappings.Length)
            return $"Feature {featureIndex}";

        return FeatureMappings[featureIndex].TechnicalName;
    }

    /// <summary>
    /// Get coaching guidance for improving a specific feature
    /// </summary>
    public static string GetCoachingGuidance(int featureIndex)
    {
        if (featureIndex < 0 || featureIndex >= FeatureMappings.Length)
            return "Focus on overall technique improvement.";

        return FeatureMappings[featureIndex].CoachingGuidance;
    }

    /// <summary>
    /// Get a complete feature description for coaching feedback
    /// </summary>
    public static FeatureCoachingInfo GetFeatureInfo(int featureIndex, bool isRightHanded)
    {
        return new FeatureCoachingInfo
        {
            FeatureIndex = featureIndex,
            TechnicalName = GetTechnicalName(featureIndex),
            CoachingTerm = GetCoachingTerm(featureIndex, isRightHanded),
            CoachingGuidance = GetCoachingGuidance(featureIndex),
        };
    }

    /// <summary>
    /// Map phase-specific deviations to coaching insights
    /// </summary>
    public static List<PhaseCoachingInsight> MapDeviationsToInsights(
        SwingPhase phase,
        float[] zScoreDeviations,
        bool isRightHanded,
        int topN = 3
    )
    {
        var insights = new List<PhaseCoachingInsight>();

        // Get indices sorted by absolute z-score (most deviant first)
        var sortedIndices = zScoreDeviations
            .Select((z, idx) => (ZScore: Math.Abs(z), Index: idx))
            .OrderByDescending(x => x.ZScore)
            .Take(topN)
            .Where(x => x.ZScore > 0.5f) // Only include significant deviations
            .ToList();

        foreach (var (zScore, featureIndex) in sortedIndices)
        {
            var direction = zScoreDeviations[featureIndex] > 0 ? "too high" : "too low";
            var severity = zScore switch
            {
                > 2.0f => DeviationSeverity.Major,
                > 1.0f => DeviationSeverity.Moderate,
                _ => DeviationSeverity.Minor,
            };

            insights.Add(
                new PhaseCoachingInsight
                {
                    Phase = phase,
                    FeatureIndex = featureIndex,
                    CoachingTerm = GetCoachingTerm(featureIndex, isRightHanded),
                    TechnicalName = GetTechnicalName(featureIndex),
                    ZScore = zScoreDeviations[featureIndex],
                    Direction = direction,
                    Severity = severity,
                    Guidance = GetCoachingGuidance(featureIndex),
                    PhaseSpecificTip = GetPhaseSpecificTip(phase, featureIndex, direction),
                }
            );
        }

        return insights;
    }

    /// <summary>
    /// Get phase-specific coaching tips for a deviation
    /// </summary>
    private static string GetPhaseSpecificTip(SwingPhase phase, int featureIndex, string direction)
    {
        // Phase-specific tips based on common issues
        return (phase, featureIndex, direction) switch
        {
            // Backswing phase tips
            (SwingPhase.Backswing, 0, "too low") =>
                "Take a fuller backswing to generate more racket head speed.",
            (SwingPhase.Backswing, 8, "too low") =>
                "Complete your shoulder turn before starting forward.",
            (SwingPhase.Backswing, 17, "too high") =>
                "Drop your racket head below the ball to create topspin.",

            // Contact phase tips
            (SwingPhase.Contact, 0, "too low") =>
                "Accelerate through the ball at contact, don't decelerate.",
            (SwingPhase.Contact, 1, "too low") =>
                "Snap your wrist through contact for more spin and power.",
            (SwingPhase.Contact, 13, "too low") =>
                "Strike the ball at a higher contact point for better margin.",
            (SwingPhase.Contact, 13, "too high") =>
                "Let the ball drop to your ideal contact height.",

            // Follow-through phase tips
            (SwingPhase.FollowThrough, 0, "too low") =>
                "Don't stop your swing short. Follow through completely.",
            (SwingPhase.FollowThrough, 16, "too low") =>
                "Extend your arm toward the target in the follow-through.",
            (SwingPhase.FollowThrough, 18, "too low") =>
                "Finish high over your shoulder for proper topspin technique.",

            _ => GetCoachingGuidance(featureIndex),
        };
    }

    /// <summary>
    /// Map feature deviations to a legacy feature importance dictionary for backward compatibility.
    /// This converts the new FeatureDeviation format to the old Dictionary&lt;string, double&gt; format.
    /// </summary>
    public static Dictionary<string, double> MapDeviationsToFeatureImportance(
        List<FeatureDeviation> deviations
    )
    {
        var importance = new Dictionary<string, double>();

        foreach (var deviation in deviations)
        {
            // Use the feature name as key and z-score magnitude as importance
            var key = deviation.FeatureName;
            if (!importance.ContainsKey(key))
            {
                // Convert z-score to 0-1 importance scale (higher z-score = more important)
                // Clamp to reasonable range
                var normalizedImportance = Math.Min(1.0, Math.Abs(deviation.ZScore) / 3.0);
                importance[key] = normalizedImportance;
            }
        }

        return importance;
    }
}

/// <summary>
/// Complete coaching information for a feature
/// </summary>
public class FeatureCoachingInfo
{
    public int FeatureIndex { get; set; }
    public string TechnicalName { get; set; } = "";
    public string CoachingTerm { get; set; } = "";
    public string CoachingGuidance { get; set; } = "";
}

/// <summary>
/// Coaching insight for a phase-specific deviation
/// </summary>
public class PhaseCoachingInsight
{
    public SwingPhase Phase { get; set; }
    public int FeatureIndex { get; set; }
    public string CoachingTerm { get; set; } = "";
    public string TechnicalName { get; set; } = "";
    public float ZScore { get; set; }
    public string Direction { get; set; } = "";
    public DeviationSeverity Severity { get; set; }
    public string Guidance { get; set; } = "";
    public string PhaseSpecificTip { get; set; } = "";
}

/// <summary>
/// Severity level for feature deviations
/// </summary>
public enum DeviationSeverity
{
    Minor, // |z| < 1.0
    Moderate, // 1.0 <= |z| < 2.0
    Major, // |z| >= 2.0
}
