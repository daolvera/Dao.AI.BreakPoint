namespace Dao.AI.BreakPoint.ModelTraining;

public class TrainingConfiguration
{
    public string VideoDirectory { get; set; } = "training_videos";
    public string InputModelPath { get; set; } = "movenet/saved_model.pb";
    public string ModelOutputPath { get; set; } = "usta_swing_model.h5";
    public int Epochs { get; set; } = 5;

    public int BatchSize { get; set; } = 32;
    public float ValidationSplit { get; set; } = 0.2f;
    public float LearningRate { get; set; } = 0.001f;
    public int NumFeatures { get; set; } = 66;
}
