namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Interface for skeleton overlay generation
/// </summary>
public interface ISkeletonOverlayService
{
    /// <summary>
    /// Generate a skeleton overlay PNG for a single frame
    /// </summary>
    byte[] GenerateOverlayImage(
        byte[] frameImage,
        FrameData frameData,
        Dictionary<string, double> featureImportance,
        double qualityScore,
        int imageWidth,
        int imageHeight
    );

    /// <summary>
    /// Generate a skeleton overlay GIF showing the full swing
    /// </summary>
    byte[] GenerateOverlayGif(
        List<byte[]> frameImages,
        SwingData swingData,
        Dictionary<string, double> featureImportance,
        double qualityScore,
        int imageWidth,
        int imageHeight,
        int frameDelayMs = 50
    );

    /// <summary>
    /// Find the frame index with the worst technique
    /// </summary>
    int FindWorstFrameIndex(SwingData swingData, Dictionary<string, double> featureImportance);
}
