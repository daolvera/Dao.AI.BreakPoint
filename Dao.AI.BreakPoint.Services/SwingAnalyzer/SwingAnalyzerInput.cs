using Microsoft.ML.Data;
using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingAnalyzerInput
{
    [VectorType(66)] // 24 speed/accel + 8 angles + 34 positions
    public required float[] Features { get; set; }
    [Range(1.0, 7.0)]
    public required float Label { get; set; } // Your target variable (e.g., swing quality score, classification)
}
