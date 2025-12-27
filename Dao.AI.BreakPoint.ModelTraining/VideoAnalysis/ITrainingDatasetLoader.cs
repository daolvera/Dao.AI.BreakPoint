namespace Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;

internal interface ITrainingDatasetLoader
{
    Task<List<TrainingSwingVideo>> ProcessVideoDirectoryAsync(
        string videoDirectory,
        string moveNetModelPath,
        string phaseClassifierModelPath
    );
    void SaveVideoLabel(VideoLabel label, string labelPath);
    Task<VideoLabel> LoadVideoLabelAsync(string labelPath);
}
