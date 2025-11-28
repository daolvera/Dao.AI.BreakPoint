using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;

namespace Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;

public interface ITrainingDatasetLoader
{
    Task<List<ProcessedSwingVideo>> ProcessVideoDirectoryAsync(string videoDirectory, string moveNetModelPath);
    void SaveVideoLabel(VideoLabel label, string labelPath);
    Task<VideoLabel> LoadVideoLabelAsync(string labelPath);
}
