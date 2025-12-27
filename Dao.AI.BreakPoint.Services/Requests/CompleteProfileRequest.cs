using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Services.Requests;

public class CompleteProfileRequest
{
    public string Name { get; set; } = null!;
    public double UstaRating { get; set; }
    public Handedness Handedness { get; set; }
}
