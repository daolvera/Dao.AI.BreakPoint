namespace Dao.AI.BreakPoint.Data.Models;

public abstract class UpdatableModel : BaseModel
{
    public DateTime UpdatedAt { get; set; }
    /// <summary>
    /// If null, it was updated by the system
    /// otherwise conencts to the user that made the last update
    /// </summary>
    public string? UpdatedByAppUserId { get; set; }
}
