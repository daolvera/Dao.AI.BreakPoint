namespace Dao.AI.BreakPoint.Services.MoveNet;

public partial class MoveNetVideoProcessor
{
    private struct BodyPosition
    {
        public float ShoulderRotation { get; set; }
        public float HipRotation { get; set; }
        public float RacketPosition { get; set; }
        public bool ShouldersSquared { get; set; }
        public bool HipsOpen { get; set; }
        public bool IsCoiled { get; set; }
        public float RacketSpeed { get; set; }
        public float RacketAcceleration { get; set; }
        public float ShoulderAngularVelocity { get; set; }
        public float ElbowAngle { get; set; }
        public float ElbowAngularVelocity { get; set; }
    }
}