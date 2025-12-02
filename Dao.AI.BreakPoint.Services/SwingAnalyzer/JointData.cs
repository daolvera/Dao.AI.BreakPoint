using Dao.AI.BreakPoint.Services.MoveNet;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class JointData
{
    public float Y { get; set; }  // Normalized 0-1
    public float X { get; set; }  // Normalized 0-1
    public float Confidence { get; set; }
    public required JointFeatures JointFeature { get; set; }
    public float? Speed { get; set; }
    public float? Acceleration { get; set; }

    public static Vector2 ToPixelCoordinates(float x, float y, int height, int width)
    {
        return new Vector2(x * width, y * height);
    }

    public Vector2 ToPixelCoordinates(int height, int width)
    {
        return ToPixelCoordinates(X, Y, height, width);
    }
}