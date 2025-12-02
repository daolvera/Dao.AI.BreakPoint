namespace Dao.AI.BreakPoint.Services.MoveNet;

public partial class MoveNetVideoProcessor
{
    private struct BodyPosition
    {
        public float ShoulderRotation;  // How much shoulders are rotated (positive = open to court)
        public float HipRotation;       // How much hips are rotated (positive = open to court)
        public float RacketPosition;    // Where racket is relative to body center (negative = back, positive = forward)
        public bool ShouldersSquared;   // Are shoulders roughly parallel to baseline
        public bool HipsOpen;           // Are hips opened to the court
        public bool IsCoiled;           // Is body in coiled backswing position
    }
}