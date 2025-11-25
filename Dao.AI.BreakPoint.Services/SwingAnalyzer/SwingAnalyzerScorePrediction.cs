using Microsoft.ML.Data;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingAnalyzerScorePrediction
{
    [ColumnName("Score")]
    public float PredictedLabel { get; set; }
}
