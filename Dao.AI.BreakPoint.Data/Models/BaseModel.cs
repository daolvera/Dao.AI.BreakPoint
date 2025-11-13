using System.ComponentModel.DataAnnotations.Schema;

namespace Dao.AI.BreakPoint.Data.Models;

public abstract class BaseModel
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// If null, it was created by the system
    /// otherwise connects to the user created the model
    /// </summary>
    public string? CreatedByAppUserId { get; set; }
}
