using System.ComponentModel.DataAnnotations;
using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Data.Models;

public class Player : UpdatableModel
{
    /// <summary>
    /// The user will always be populated
    /// unless the Player is created as an opponent in a match not as an application user
    /// </summary>
    public string? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>
    /// Rating as defined by the USTA
    /// NTRP system
    /// https://activenetwork.my.salesforce-sites.com/usta/articles/en_US/Article/League-NTRP-Rating-Information
    /// </summary>
    [Range(1.0, 7.0)]
    public double UstaRating { get; set; }

    /// <summary>
    /// Whether the player is right-handed or left-handed
    /// </summary>
    public Handedness Handedness { get; set; }
    #region Player Type
    public PlayerType EstimatedPlayerType { get; set; }

    [Range(0.0, 1.0)]
    public double BigServerScore { get; set; }

    [Range(0.0, 1.0)]
    public double ServeAndVolleyerScore { get; set; }

    [Range(0.0, 1.0)]
    public double AllCourtPlayerScore { get; set; }

    [Range(0.0, 1.0)]
    public double AttackingBaselinerScore { get; set; }

    [Range(0.0, 1.0)]
    public double SolidBaselinerScore { get; set; }

    [Range(0.0, 1.0)]
    public double CounterPuncherScore { get; set; }
    #endregion
    public virtual ICollection<Match> MyReportedMatches { get; set; } = [];
    public virtual ICollection<Match> MyParticipatedMatches { get; set; } = [];

    /// <summary>
    /// In-progress analysis requests for this player
    /// </summary>
    public virtual ICollection<AnalysisRequest> AnalysisRequests { get; set; } = [];

    /// <summary>
    /// Completed analysis results for this player (historical record)
    /// </summary>
    public virtual ICollection<AnalysisResult> AnalysisResults { get; set; } = [];
}
