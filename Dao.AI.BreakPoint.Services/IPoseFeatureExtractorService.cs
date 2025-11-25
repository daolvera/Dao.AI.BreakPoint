using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services;

public interface IPoseFeatureExtractorService
{
    static abstract (Vector2[] positions, float[] confidences) KeypointsToPixels(FrameData frame, int height, int width);
    float[] BuildFrameFeatures(Vector2[]? prev2Positions, Vector2[]? prevPositions, Vector2[] currentPositions, float[] confidences, float deltaTime = 0.0333333351F);
}