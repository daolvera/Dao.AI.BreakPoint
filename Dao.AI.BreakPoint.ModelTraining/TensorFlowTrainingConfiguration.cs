namespace Dao.AI.BreakPoint.ModelTraining;

public class TensorFlowTrainingConfiguration
{
    public string ModelOutputPath { get; set; } = "swing_cnn_model.h5";
    public int SequenceLength { get; set; } = 60;
    public int BatchSize { get; set; } = 32;
    public int Epochs { get; set; } = 100;
    public float ValidationSplit { get; set; } = 0.2f;
    public float LearningRate { get; set; } = 0.001f;
    public int NumFeatures { get; set; } = 66;
    public string[] IssueCategories { get; set; } =
    [
        "Insufficient shoulder rotation",
        "Excessive shoulder rotation",
        "Contact point too far back",
        "Contact point too far forward",
        "Rushed preparation",
        "Late preparation",
        "Narrow stance",
        "Wide stance",
        "Poor follow through extension",
    ];
}
