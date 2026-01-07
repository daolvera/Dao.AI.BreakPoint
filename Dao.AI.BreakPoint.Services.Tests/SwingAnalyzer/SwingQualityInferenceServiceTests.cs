using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using FluentAssertions;

namespace Dao.AI.BreakPoint.Services.Tests.SwingAnalyzer;

/// <summary>
/// Tests for SwingQualityInferenceService
/// </summary>
public class SwingQualityInferenceServiceTests
{
    #region Feature Importance Tests

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void RunInference_WithoutModel_ReturnsHeuristicResult()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null);
        var swing = CreateTestSwingData();

        // Act
        var result = service.RunInference(swing);

        // Assert
        result.Should().NotBeNull();
        result.IsFromModel.Should().BeFalse("no model was loaded");
        result.QualityScore.Should().BeInRange(0, 100);
        result.FeatureImportance.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void RunInference_FeatureImportance_ContainsExpectedFeatures()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null);
        var swing = CreateTestSwingData();

        // Act
        var result = service.RunInference(swing);

        // Assert - Should contain key tennis-related features
        result.FeatureImportance.Keys.Should().Contain(k => k.Contains("Wrist"));
        result.FeatureImportance.Keys.Should().Contain(k => k.Contains("Shoulder"));
        result.FeatureImportance.Keys.Should().Contain(k => k.Contains("Elbow"));
    }

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void GetTopFeatures_ReturnsOrderedByImportance()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null);
        var swing = CreateVariedSwingData();

        // Act
        var result = service.RunInference(swing);
        var topFeatures = result.GetTopFeatures(5);

        // Assert
        topFeatures.Should().HaveCount(5);
        for (int i = 0; i < topFeatures.Count - 1; i++)
        {
            topFeatures[i]
                .Importance.Should()
                .BeGreaterThanOrEqualTo(topFeatures[i + 1].Importance);
        }
    }

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void GetWeakFeatures_ReturnsLowestImportance()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null);
        var swing = CreateVariedSwingData();

        // Act
        var result = service.RunInference(swing);
        var weakFeatures = result.GetWeakFeatures(3);

        // Assert
        weakFeatures.Should().HaveCount(3);
        for (int i = 0; i < weakFeatures.Count - 1; i++)
        {
            weakFeatures[i].Importance.Should().BeLessThanOrEqualTo(weakFeatures[i + 1].Importance);
        }
    }

    #endregion

    #region Quality Score Tests

    [Fact]
    [Trait("Category", "QualityScore")]
    public void QualityScore_WithGoodMotion_ShouldBeHigher()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null);

        var goodSwing = CreateGoodMotionSwingData();
        var poorSwing = CreatePoorMotionSwingData();

        // Act
        var goodResult = service.RunInference(goodSwing);
        var poorResult = service.RunInference(poorSwing);

        // Assert
        goodResult
            .QualityScore.Should()
            .BeGreaterThanOrEqualTo(50, "good motion swing should score at least average");
        poorResult
            .QualityScore.Should()
            .BeLessThanOrEqualTo(100, "all scores should be in valid range");
    }

    [Fact]
    [Trait("Category", "QualityScore")]
    public void QualityScore_AlwaysInValidRange()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null);

        var testCases = new[]
        {
            CreateTestSwingData(),
            CreateVariedSwingData(),
            CreateGoodMotionSwingData(),
            CreatePoorMotionSwingData(),
            new SwingData { Frames = [] }, // Empty swing
        };

        // Act & Assert
        foreach (var swing in testCases)
        {
            var result = service.RunInference(swing);
            result.QualityScore.Should().BeInRange(0, 100);
        }
    }

    #endregion

    #region Service Lifecycle Tests

    [Fact]
    [Trait("Category", "Lifecycle")]
    public void IsModelLoaded_WithNullPath_ReturnsFalse()
    {
        using var service = new SwingQualityInferenceService(null);
        service.IsModelLoaded.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Lifecycle")]
    public void IsModelLoaded_WithInvalidPath_ReturnsFalse()
    {
        using var service = new SwingQualityInferenceService("/nonexistent/path/model.onnx");
        service.IsModelLoaded.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Lifecycle")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var service = new SwingQualityInferenceService(null);
        service.Dispose();
        service.Dispose(); // Should not throw
    }

    [Fact]
    [Trait("Category", "Constants")]
    public void AggregatedFeatureCount_IsCorrect()
    {
        // 16 features × 3 stats = 48
        SwingQualityInferenceService.AggregatedFeatureCount.Should().Be(48);
    }

    [Fact]
    [Trait("Category", "Constants")]
    public void FeaturesPerFrame_IsCorrect()
    {
        // 6 joints × 2 (velocity + acceleration) + 4 angles = 16
        SwingQualityInferenceService.FeaturesPerFrame.Should().Be(16);
    }

    #endregion

    #region Helper Methods

    private static SwingData CreateTestSwingData()
    {
        var frames = new List<FrameData>();
        var random = new Random(42);

        for (int t = 0; t < 60; t++)
        {
            var frame = CreateFrameWithRandomData(random);
            frames.Add(frame);
        }

        return new SwingData { Frames = frames };
    }

    private static SwingData CreateVariedSwingData()
    {
        var frames = new List<FrameData>();

        for (int t = 0; t < 60; t++)
        {
            var frame = new FrameData
            {
                Joints = CreateJointsWithVariedMotion(t),
                LeftElbowAngle = 90 + (float)Math.Sin(t * 0.1) * 20,
                RightElbowAngle = 100 + (float)Math.Sin(t * 0.15) * 30,
                LeftShoulderAngle = 45 + (float)Math.Sin(t * 0.2) * 15,
                RightShoulderAngle = 50 + (float)Math.Sin(t * 0.25) * 25,
            };
            frames.Add(frame);
        }

        return new SwingData { Frames = frames };
    }

    private static SwingData CreateGoodMotionSwingData()
    {
        var frames = new List<FrameData>();

        for (int t = 0; t < 60; t++)
        {
            float progress = t / 60f;

            var joints = new JointData[MoveNetVideoProcessor.NumKeyPoints];
            for (int j = 0; j < joints.Length; j++)
            {
                joints[j] = new JointData
                {
                    JointFeature = (JointFeatures)j,
                    Confidence = 0.9f,
                    Speed = 0.5f,
                    Acceleration = 0.1f,
                };
            }

            // Good wrist motion
            joints[(int)JointFeatures.RightWrist].Speed = (float)Math.Sin(progress * Math.PI) * 15f;
            joints[(int)JointFeatures.RightWrist].Acceleration =
                (float)Math.Cos(progress * Math.PI) * 5f;
            joints[(int)JointFeatures.LeftWrist].Speed = (float)Math.Sin(progress * Math.PI) * 10f;

            var frame = new FrameData
            {
                Joints = joints,
                LeftElbowAngle = 90 + (float)Math.Sin(progress * Math.PI) * 20,
                RightElbowAngle = 90 + (float)Math.Sin(progress * Math.PI) * 25,
                LeftShoulderAngle = 45 + (float)Math.Sin(progress * Math.PI) * 30,
                RightShoulderAngle = 45 + (float)Math.Sin(progress * Math.PI) * 35,
            };
            frames.Add(frame);
        }

        return new SwingData { Frames = frames };
    }

    private static SwingData CreatePoorMotionSwingData()
    {
        var frames = new List<FrameData>();

        for (int t = 0; t < 60; t++)
        {
            var joints = new JointData[MoveNetVideoProcessor.NumKeyPoints];
            for (int j = 0; j < joints.Length; j++)
            {
                joints[j] = new JointData
                {
                    JointFeature = (JointFeatures)j,
                    Confidence = 0.9f,
                    Speed = 0.1f,
                    Acceleration = 0.01f,
                };
            }

            var frame = new FrameData
            {
                Joints = joints,
                LeftElbowAngle = 90f,
                RightElbowAngle = 90f,
                LeftShoulderAngle = 45f,
                RightShoulderAngle = 45f,
            };
            frames.Add(frame);
        }

        return new SwingData { Frames = frames };
    }

    private static FrameData CreateFrameWithRandomData(Random random)
    {
        var joints = new JointData[MoveNetVideoProcessor.NumKeyPoints];
        for (int j = 0; j < joints.Length; j++)
        {
            joints[j] = new JointData
            {
                JointFeature = (JointFeatures)j,
                Confidence = 0.8f + (float)random.NextDouble() * 0.2f,
                Speed = (float)random.NextDouble() * 5f,
                Acceleration = (float)random.NextDouble() * 2f,
            };
        }

        return new FrameData
        {
            Joints = joints,
            LeftElbowAngle = 80f + (float)random.NextDouble() * 40f,
            RightElbowAngle = 80f + (float)random.NextDouble() * 40f,
            LeftShoulderAngle = 30f + (float)random.NextDouble() * 60f,
            RightShoulderAngle = 30f + (float)random.NextDouble() * 60f,
        };
    }

    private static JointData[] CreateJointsWithVariedMotion(int timeStep)
    {
        var joints = new JointData[MoveNetVideoProcessor.NumKeyPoints];
        for (int j = 0; j < joints.Length; j++)
        {
            float variation = (j % 10 + 1) / 10f;
            joints[j] = new JointData
            {
                JointFeature = (JointFeatures)j,
                Confidence = 0.9f,
                Speed = (float)Math.Sin(timeStep * 0.1 * (j + 1)) * variation * 5f,
                Acceleration = (float)Math.Cos(timeStep * 0.1 * (j + 1)) * variation * 2f,
            };
        }
        return joints;
    }

    #endregion
}
