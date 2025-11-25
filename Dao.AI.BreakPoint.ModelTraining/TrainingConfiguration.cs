namespace Dao.AI.BreakPoint.ModelTraining;

internal class TrainingConfiguration
{
    public string ModelOutputPath { get; set; } = "pose_model.zip";
    public string NormalizationParamsPath { get; set; } = "normalization.json";
    public float LearningRate { get; set; } = 0.1f;
    public int HistorySize { get; set; } = 50;
}
