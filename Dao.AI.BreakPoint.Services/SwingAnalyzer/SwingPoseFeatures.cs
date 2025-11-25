using System.Numerics;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingPoseFeatures
{
    public float Y { get; set; }  // Normalized 0-1
    public float X { get; set; }  // Normalized 0-1
    public float Confidence { get; set; }

    public Vector2 ToPixelCoordinates(int height, int width)
    {
        return new Vector2(X * width, Y * height);
    }
}