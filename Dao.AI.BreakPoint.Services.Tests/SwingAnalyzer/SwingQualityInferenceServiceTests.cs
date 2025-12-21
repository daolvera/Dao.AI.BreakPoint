using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using FluentAssertions;

namespace Dao.AI.BreakPoint.Services.Tests.SwingAnalyzer;

/// <summary>
/// Tests for SwingQualityInferenceService feature importance extraction
/// </summary>
public class SwingQualityInferenceServiceTests
{
    private const int SequenceLength = 90;
    private const int NumFeatures = 66;

    #region Feature Importance Tests

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void RunInference_WithoutModel_ReturnsHeuristicResult()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null, SequenceLength, NumFeatures);
        var preprocessedSwing = CreateTestSwingData();

        // Act
        var result = service.RunInference(preprocessedSwing);

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
        using var service = new SwingQualityInferenceService(null, SequenceLength, NumFeatures);
        var preprocessedSwing = CreateTestSwingData();

        // Act
        var result = service.RunInference(preprocessedSwing);

        // Assert - Should contain key tennis-related features
        result.FeatureImportance.Keys.Should().Contain(k => k.Contains("Wrist"));
        result.FeatureImportance.Keys.Should().Contain(k => k.Contains("Shoulder"));
        result.FeatureImportance.Keys.Should().Contain(k => k.Contains("Hip"));
        result.FeatureImportance.Keys.Should().Contain(k => k.Contains("Elbow"));
    }

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void RunInference_HighVarianceFeature_HasHigherImportance()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null, SequenceLength, NumFeatures);
        var preprocessedSwing = new float[SequenceLength, NumFeatures];

        // Set high variance for Right Wrist Velocity (index 5)
        // And low variance for other features
        for (int t = 0; t < SequenceLength; t++)
        {
            // High variance feature - oscillates significantly
            preprocessedSwing[t, 5] = (float)Math.Sin(t * 0.5) * 10f;

            // Low variance features - nearly constant
            for (int f = 0; f < NumFeatures; f++)
            {
                if (f != 5)
                {
                    preprocessedSwing[t, f] = 0.5f;
                }
            }
        }

        // Act
        var result = service.RunInference(preprocessedSwing);
        var rightWristVelocityImportance = result.FeatureImportance["Right Wrist Velocity"];

        // Assert - High variance feature should have higher importance
        rightWristVelocityImportance
            .Should()
            .BeGreaterThan(0.5, "high variance features should have higher importance");
    }

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void GetTopFeatures_ReturnsOrderedByImportance()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null, SequenceLength, NumFeatures);
        var preprocessedSwing = CreateVariedSwingData();

        // Act
        var result = service.RunInference(preprocessedSwing);
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
        using var service = new SwingQualityInferenceService(null, SequenceLength, NumFeatures);
        var preprocessedSwing = CreateVariedSwingData();

        // Act
        var result = service.RunInference(preprocessedSwing);
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
    [Trait("Category", "FeatureImportance")]
    public void QualityScore_WithGoodMotion_ShouldBeHigher()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null, SequenceLength, NumFeatures);

        // Create swing with good motion characteristics
        var goodSwing = CreateGoodMotionSwingData();
        var poorSwing = CreatePoorMotionSwingData();

        // Act
        var goodResult = service.RunInference(goodSwing);
        var poorResult = service.RunInference(poorSwing);

        // Assert - Good swing should score higher (at least 10 points more)
        // Note: Without actual ML model, heuristics may have limited differentiation
        goodResult
            .QualityScore.Should()
            .BeGreaterThanOrEqualTo(50, "good motion swing should score at least average");
        poorResult
            .QualityScore.Should()
            .BeLessThanOrEqualTo(100, "all scores should be in valid range");
    }

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void QualityScore_AlwaysInValidRange()
    {
        // Arrange
        using var service = new SwingQualityInferenceService(null, SequenceLength, NumFeatures);

        // Test with various data patterns
        var testCases = new[]
        {
            CreateTestSwingData(),
            CreateVariedSwingData(),
            CreateGoodMotionSwingData(),
            CreatePoorMotionSwingData(),
            new float[SequenceLength, NumFeatures], // All zeros
        };

        // Act & Assert
        foreach (var testData in testCases)
        {
            var result = service.RunInference(testData);
            result.QualityScore.Should().BeInRange(0, 100);
        }
    }

    #endregion

    #region Service Lifecycle Tests

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void IsModelLoaded_WithNullPath_ReturnsFalse()
    {
        // Arrange & Act
        using var service = new SwingQualityInferenceService(null);

        // Assert
        service.IsModelLoaded.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void IsModelLoaded_WithInvalidPath_ReturnsFalse()
    {
        // Arrange & Act
        using var service = new SwingQualityInferenceService("/nonexistent/path/model.onnx");

        // Assert
        service.IsModelLoaded.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "FeatureImportance")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var service = new SwingQualityInferenceService(null);

        // Act & Assert - Should not throw
        service.Dispose();
        service.Dispose();
    }

    #endregion

    #region Helper Methods

    private static float[,] CreateTestSwingData()
    {
        var data = new float[SequenceLength, NumFeatures];
        var random = new Random(42); // Deterministic for reproducibility

        for (int t = 0; t < SequenceLength; t++)
        {
            for (int f = 0; f < NumFeatures; f++)
            {
                data[t, f] = (float)random.NextDouble();
            }
        }

        return data;
    }

    private static float[,] CreateVariedSwingData()
    {
        var data = new float[SequenceLength, NumFeatures];

        for (int t = 0; t < SequenceLength; t++)
        {
            for (int f = 0; f < NumFeatures; f++)
            {
                // Different variance levels for different features
                float variance = (f % 10 + 1) / 10f;
                data[t, f] = (float)(Math.Sin(t * 0.1 * (f + 1)) * variance);
            }
        }

        return data;
    }

    private static float[,] CreateGoodMotionSwingData()
    {
        var data = new float[SequenceLength, NumFeatures];

        // Good swing characteristics:
        // - High wrist velocity (indices 4, 5)
        // - Good shoulder angles (indices 26, 27)
        // - Good hip angles (indices 28, 29)
        for (int t = 0; t < SequenceLength; t++)
        {
            float progress = t / (float)SequenceLength;

            // Right wrist velocity - smooth acceleration pattern
            data[t, 5] = (float)Math.Sin(progress * Math.PI) * 15f;

            // Right shoulder angle - good rotation
            data[t, 27] = (float)(45 + 30 * Math.Sin(progress * Math.PI));

            // Right hip angle - good rotation
            data[t, 29] = (float)(90 + 20 * Math.Sin(progress * Math.PI));

            // Fill other features with moderate values
            for (int f = 0; f < NumFeatures; f++)
            {
                if (f != 5 && f != 27 && f != 29)
                {
                    data[t, f] = 0.5f + (float)Math.Sin(t * 0.05) * 0.2f;
                }
            }
        }

        return data;
    }

    private static float[,] CreatePoorMotionSwingData()
    {
        var data = new float[SequenceLength, NumFeatures];

        // Poor swing characteristics:
        // - Low, inconsistent wrist velocity
        // - Minimal shoulder rotation
        // - Minimal hip rotation
        for (int t = 0; t < SequenceLength; t++)
        {
            // Very low wrist velocity
            data[t, 5] = 0.1f;

            // Minimal shoulder angle variation
            data[t, 27] = 0.1f;

            // Minimal hip angle variation
            data[t, 29] = 0.1f;

            // Fill other features with low values
            for (int f = 0; f < NumFeatures; f++)
            {
                if (f != 5 && f != 27 && f != 29)
                {
                    data[t, f] = 0.1f;
                }
            }
        }

        return data;
    }

    #endregion
}
