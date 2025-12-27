using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Fallback coaching service when Azure OpenAI is not configured
/// Provides static tips based on stroke type and USTA rating
/// </summary>
public class StaticCoachingService : ICoachingService
{
    public Task<List<string>> GenerateCoachingTipsAsync(
        SwingType strokeType,
        double qualityScore,
        Dictionary<string, double> featureImportance,
        double ustaRating
    )
    {
        var tips = GetStaticTipsForStroke(strokeType, qualityScore, ustaRating);
        return Task.FromResult(tips);
    }

    private static List<string> GetStaticTipsForStroke(
        SwingType strokeType,
        double qualityScore,
        double ustaRating
    )
    {
        var tips = new List<string>();

        // General tips based on score and rating
        if (qualityScore < 50)
        {
            tips.Add(
                "Focus on your ready position - stay low with knees bent and weight on the balls of your feet."
            );
            tips.Add("Practice shadow swings to groove your muscle memory without a ball.");
        }
        else if (qualityScore < 70)
        {
            tips.Add(
                "Work on your follow-through - complete the motion fully through the hitting zone."
            );
        }

        // Rating-specific guidance
        if (ustaRating <= 3.0)
        {
            tips.Add(
                "Focus on consistency first - aim to keep the ball in play for rallies of 5+ shots."
            );
        }
        else if (ustaRating >= 4.0)
        {
            tips.Add("Work on adding variety to your shots - vary spin, pace, and placement.");
        }

        // Stroke-specific tips
        switch (strokeType)
        {
            case SwingType.ForehandGroundStroke:
                tips.Add("Turn your shoulders early during the backswing to generate more power.");
                tips.Add("Practice the windshield wiper finish for better topspin.");
                break;

            case SwingType.BackhandGroundStroke:
                tips.Add("For two-handed backhands, let your non-dominant hand lead the swing.");
                tips.Add("Keep your elbow close to your body during the swing for better control.");
                break;

            default:
                tips.Add(
                    "Stay balanced throughout the shot and recover quickly to ready position."
                );
                break;
        }

        tips.Add("Record yourself practicing these tips and compare to your analysis.");

        return tips.Take(5).ToList();
    }
}
