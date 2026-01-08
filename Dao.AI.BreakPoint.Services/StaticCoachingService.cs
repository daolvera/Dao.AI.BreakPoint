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

    public Task<List<GeneratedDrill>> GenerateDrillRecommendationsAsync(
        DrillRecommendationInput input
    )
    {
        var drills = GetStaticDrillRecommendations(input);
        return Task.FromResult(drills);
    }

    private static List<GeneratedDrill> GetStaticDrillRecommendations(
        DrillRecommendationInput input
    )
    {
        var drills = new List<GeneratedDrill>();
        var priority = 1;

        // Find the phase with lowest score
        var lowestPhase = input.PhaseAnalyses.OrderBy(p => p.Score).FirstOrDefault();

        if (lowestPhase != null)
        {
            var drill = lowestPhase.Phase switch
            {
                SwingPhase.Preparation => new GeneratedDrill(
                    SwingPhase.Preparation,
                    "ready position",
                    "Split Step Drill",
                    "Practice your split step timing by hopping as your opponent hits the ball. Land with your weight balanced on both feet, knees bent, ready to explode in either direction.",
                    "3 sets of 10 reps",
                    priority++
                ),
                SwingPhase.Backswing => new GeneratedDrill(
                    SwingPhase.Backswing,
                    "shoulder rotation",
                    "Turn and Freeze Drill",
                    "Practice your unit turn by rotating your shoulders while keeping your hips stable. Freeze at the end of your backswing and check that your racket is pointing to the back fence.",
                    "3 sets of 15 reps",
                    priority++
                ),
                SwingPhase.Contact => new GeneratedDrill(
                    SwingPhase.Contact,
                    "racket head speed",
                    "Contact Point Shadow Swings",
                    "Practice swinging through the contact zone without a ball. Focus on accelerating through contact and hitting the ball in front of your body.",
                    "5 minutes daily",
                    priority++
                ),
                SwingPhase.FollowThrough => new GeneratedDrill(
                    SwingPhase.FollowThrough,
                    "follow through completion",
                    "Windshield Wiper Drill",
                    "Practice the full follow-through motion, finishing with your racket over your opposite shoulder. Exaggerate the motion until it becomes natural.",
                    "3 sets of 20 reps",
                    priority++
                ),
                _ => new GeneratedDrill(
                    SwingPhase.Contact,
                    "overall technique",
                    "Mirror Practice",
                    "Practice your swing in front of a mirror to check your form at each phase of the stroke.",
                    "10 minutes daily",
                    priority++
                ),
            };

            drills.Add(drill);
        }

        // Add general improvement drill based on rating
        if (input.UstaRating <= 3.0)
        {
            drills.Add(
                new GeneratedDrill(
                    SwingPhase.Contact,
                    "consistency",
                    "Rally Counter Challenge",
                    "Try to hit 10 consecutive balls in play against a wall or practice partner. Focus on smooth, controlled strokes rather than power.",
                    "15 minutes, 3x weekly",
                    priority++
                )
            );
        }
        else
        {
            drills.Add(
                new GeneratedDrill(
                    SwingPhase.Contact,
                    "placement",
                    "Target Practice",
                    "Set up targets in the corners of the court. Practice hitting to specific zones, varying your spin and pace with each shot.",
                    "20 minutes, 2x weekly",
                    priority++
                )
            );
        }

        // Always add a footwork drill
        drills.Add(
            new GeneratedDrill(
                SwingPhase.Preparation,
                "footwork",
                "Lateral Movement Drill",
                "Set up cones 10 feet apart and practice shuffling between them while shadow swinging. Focus on staying low and maintaining balance throughout.",
                "3 sets of 30 seconds",
                priority
            )
        );

        return drills.Take(input.MaxDrills).ToList();
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
