namespace Dao.AI.BreakPoint.ModelTraining;

internal class TrainingConfiguration
{
    public int SequenceLength { get; set; } = 60;  // Fixed sequence length (2 seconds at 30fps)
    public int BatchSize { get; set; } = 32;
    public int Epochs { get; set; } = 100;
    public float ValidationSplit { get; set; } = 0.2f;
    public string VideoPath { get; set; } = "data";
    public string InputModelPath { get; set; } = "movenet/saved_model.pb";
    public string DatasetPath { get; set; } = "data/labeled_video_dataset.json";
    public string TensorFlowModelPath { get; set; } = "swing_cnn_model.h5";
}
