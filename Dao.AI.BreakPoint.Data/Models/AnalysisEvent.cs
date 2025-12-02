using Dao.AI.BreakPoint.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.Data.Models;

public class AnalysisEvent : UpdatableModel
{
    public int PlayerId { get; set; }
    [ConcurrencyCheck]
    public AnaylsisStatus AnaylsisStatus { get; set; }
}
