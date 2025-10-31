using System.ComponentModel;

namespace Dao.AI.BreakPoint.Data.Enums;

/// <summary>
/// Based on the classic tennis categories of player
/// ref: https://www.atptour.com/en/news/insights-playing-styles
/// </summary>
public enum PlayerType
{
    [Description("A player with a fast first serve, who will often win points within their first two shots (e.g. aces, unreturned serves, serve + one winners).")]
    BigServer,
    [Description("A player who uses serve and volley as their primary tactic.")]
    ServeAndVolleyer,
    [Description("A player who is comfortable in all areas of the court, and often utilises their ability at the net to their advantage.")]
    AllCourtPlayer,
    [Description("A player who looks to dictate play from the baseline.")]
    AttackingBaseliner,
    [Description("A player who balances attacking and defending from the baseline.")]
    SolidBaseliner,
    [Description("A player who is comfortable playing in defence. They use this ability to frustrate their opponent or choose their moment to turn defence into attack.")]
    CounterPuncher
}
