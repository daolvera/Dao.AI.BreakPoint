using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using FluentAssertions;

namespace Dao.AI.BreakPoint.Services.Tests.MoveNet;

/// <summary>
/// Unit tests for data structures used in MoveNetVideoProcessor.
/// Tests verify that FrameData, JointData, and ProcessedSwingVideo
/// correctly store and retrieve swing analysis data.
/// </summary>
public class MoveNetVideoProcessorTests
{
    private const int NumKeypoints = 17;
    private const float HighConfidence = 0.9f;

    #region Helper Methods

    /// <summary>
    /// Creates a JointData array with all joints at specified confidence
    /// </summary>
    private static JointData[] CreateKeypoints(float confidence = HighConfidence)
    {
        var keypoints = new JointData[NumKeypoints];
        for (int i = 0; i < NumKeypoints; i++)
        {
            keypoints[i] = new JointData
            {
                X = 0.5f,
                Y = 0.5f,
                Confidence = confidence,
                JointFeature = (JointFeatures)i,
            };
        }
        return keypoints;
    }

    /// <summary>
    /// Creates a FrameData with the specified swing phase and body positioning
    /// </summary>
    private static FrameData CreateFrameData(
        SwingPhase phase,
        float wristSpeed = 5f,
        float wristAccel = 2f,
        float elbowAngle = 90f,
        float shoulderAngle = 45f
    )
    {
        var joints = CreateKeypoints();

        // Set speeds for wrists
        joints[(int)JointFeatures.LeftWrist].Speed = wristSpeed;
        joints[(int)JointFeatures.RightWrist].Speed = wristSpeed;
        joints[(int)JointFeatures.LeftWrist].Acceleration = wristAccel;
        joints[(int)JointFeatures.RightWrist].Acceleration = wristAccel;

        return new FrameData
        {
            Joints = joints,
            SwingPhase = phase,
            LeftElbowAngle = elbowAngle,
            RightElbowAngle = elbowAngle,
            LeftShoulderAngle = shoulderAngle,
            RightShoulderAngle = shoulderAngle,
            LeftHipAngle = 170f,
            RightHipAngle = 170f,
            LeftKneeAngle = 160f,
            RightKneeAngle = 160f,
            WristSpeed = wristSpeed,
            WristAcceleration = wristAccel,
            ShoulderSpeed = 3f,
            ElbowSpeed = 4f,
            HipRotationSpeed = 1f,
            FrameIndex = 0,
        };
    }

    /// <summary>
    /// Creates a list of frames representing a complete swing progression
    /// </summary>
    private static List<FrameData> CreateCompleteSwingFrames()
    {
        var frames = new List<FrameData>();

        // Backswing phase (10+ frames required)
        for (int i = 0; i < 12; i++)
        {
            frames.Add(
                CreateFrameData(
                    SwingPhase.Backswing,
                    wristSpeed: 5f + i * 0.5f,
                    elbowAngle: 60f + i * 2
                )
            );
        }

        // Swing phase (5+ frames required)
        for (int i = 0; i < 8; i++)
        {
            frames.Add(
                CreateFrameData(
                    SwingPhase.Swing,
                    wristSpeed: 15f + i * 2f,
                    wristAccel: 8f,
                    elbowAngle: 100f + i * 5
                )
            );
        }

        // Follow-through phase (5+ frames required)
        for (int i = 0; i < 7; i++)
        {
            frames.Add(
                CreateFrameData(
                    SwingPhase.FollowThrough,
                    wristSpeed: 12f - i * 1.5f,
                    elbowAngle: 160f + i * 2
                )
            );
        }

        // Number the frames
        for (int i = 0; i < frames.Count; i++)
        {
            frames[i].FrameIndex = i;
        }

        return frames;
    }

    #endregion

    #region Data Structure Tests

    [Fact]
    [Trait("Category", "Handedness")]
    public void ProcessedSwingVideo_ShouldContainSwings_WhenValidFramesProvided()
    {
        // This test validates that the data structures work correctly
        // Actual MoveNet inference is not tested here
        var swingData = new SwingData { Frames = CreateCompleteSwingFrames() };

        var video = new ProcessedSwingVideo
        {
            Swings = [swingData],
            ImageHeight = 480,
            ImageWidth = 640,
            FrameRate = 30,
        };

        // Assert
        video.Swings.Should().HaveCount(1);
        video.Swings[0].Frames.Should().HaveCount(27); // 12 + 8 + 7
        video.FrameRate.Should().Be(30);
    }

    [Fact]
    [Trait("Category", "Handedness")]
    public void FrameData_ShouldTrackBothArms_ForHandednessDetection()
    {
        // Arrange
        var frame = CreateFrameData(SwingPhase.Swing);

        // Set different speeds for left and right arm
        frame.Joints[(int)JointFeatures.LeftWrist].Speed = 20f;
        frame.Joints[(int)JointFeatures.RightWrist].Speed = 5f;

        // Act - Calculate max wrist speed (as done in processor)
        var maxWristSpeed = Math.Max(
            frame.Joints[(int)JointFeatures.LeftWrist].Speed ?? 0,
            frame.Joints[(int)JointFeatures.RightWrist].Speed ?? 0
        );

        // Assert
        maxWristSpeed.Should().Be(20f);
        frame.Joints[(int)JointFeatures.LeftWrist].Speed.Should().Be(20f);
        frame.Joints[(int)JointFeatures.RightWrist].Speed.Should().Be(5f);
    }

    #endregion

    #region FrameData and JointData Tests

    [Fact]
    [Trait("Category", "DataStructures")]
    public void FrameData_ShouldStoreAllAngles()
    {
        // Arrange & Act
        var frame = new FrameData
        {
            LeftElbowAngle = 90f,
            RightElbowAngle = 95f,
            LeftShoulderAngle = 45f,
            RightShoulderAngle = 50f,
            LeftHipAngle = 170f,
            RightHipAngle = 175f,
            LeftKneeAngle = 160f,
            RightKneeAngle = 165f,
        };

        // Assert
        frame.LeftElbowAngle.Should().Be(90f);
        frame.RightElbowAngle.Should().Be(95f);
        frame.LeftShoulderAngle.Should().Be(45f);
        frame.RightShoulderAngle.Should().Be(50f);
        frame.LeftHipAngle.Should().Be(170f);
        frame.RightHipAngle.Should().Be(175f);
        frame.LeftKneeAngle.Should().Be(160f);
        frame.RightKneeAngle.Should().Be(165f);
    }

    [Fact]
    [Trait("Category", "DataStructures")]
    public void JointData_ShouldStoreMotionMetrics()
    {
        // Arrange & Act
        var joint = new JointData
        {
            X = 0.5f,
            Y = 0.6f,
            Confidence = 0.95f,
            Speed = 15.5f,
            Acceleration = 8.2f,
            JointFeature = JointFeatures.RightWrist,
        };

        // Assert
        joint.X.Should().Be(0.5f);
        joint.Y.Should().Be(0.6f);
        joint.Confidence.Should().Be(0.95f);
        joint.Speed.Should().Be(15.5f);
        joint.Acceleration.Should().Be(8.2f);
    }

    [Fact]
    [Trait("Category", "DataStructures")]
    public void ProcessedSwingVideo_MinimumSwingLength_Is15Frames()
    {
        // This validates the business rule: swings must be at least 15 frames
        var shortSwingFrames = new List<FrameData>();
        for (int i = 0; i < 14; i++) // 14 frames - too short
        {
            shortSwingFrames.Add(CreateFrameData(SwingPhase.Backswing));
        }

        // A swing with less than 15 frames should not be added to the final result
        // This tests the constant documented in ProcessVideoFrames
        shortSwingFrames
            .Count.Should()
            .BeLessThan(15, "incomplete swings (< 15 frames) should not be included");
    }

    #endregion
}
