using Dao.AI.BreakPoint.Services.SwingAnalyzer;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class TrainingSwingVideo
{
    public required ProcessedSwingVideo SwingVideo { get; set; }
    public required VideoLabel TrainingLabel { get; set; }
}
