using Dao.AI.BreakPoint.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.Data.Models;

public class Player : UpdatableModel
{
    /// <summary>
    /// The user will always be populated
    /// unless the Player is created as an opponent in a match not as an application user
    /// </summary>
    public string? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Rating as defined by the USTA
    /// NTRP system
    /// https://activenetwork.my.salesforce-sites.com/usta/articles/en_US/Article/League-NTRP-Rating-Information
    /// </summary>
    [Range(1.0, 7.0)]
    public double UstaRating { get; set; }
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
}
