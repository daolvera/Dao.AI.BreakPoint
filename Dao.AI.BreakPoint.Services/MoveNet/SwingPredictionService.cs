using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Dao.AI.BreakPoint.Services.MoveNet;
using Tensorflow.Keras.Engine;
using Tensorflow.NumPy;
using System.Numerics;
using static Tensorflow.KerasApi;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public class SwingPredictionService : IDisposable
{
    private readonly IModel _model;
    private readonly IPoseFeatureExtractorService _poseFeatureExtractor;
    private readonly int _sequenceLength;
    private readonly int _numFeatures;
    private const float MIN_CONFIDENCE = 0.2f;

    public SwingPredictionService(
        string modelPath, 
        IPoseFeatureExtractorService poseFeatureExtractor,
        int sequenceLength = 90,
        int numFeatures = 66)
    {
        _poseFeatureExtractor = poseFeatureExtractor;
        _sequenceLength = sequenceLength;
        _numFeatures = numFeatures;

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Trained model file not found: {modelPath}");
        }

        _model = keras.models.load_model(modelPath);
    }

    /// <summary>
    /// Predict swing technique scores from processed swing data
    /// </summary>
    public SwingPrediction PredictSwingTechnique(SwingData swing, int imageHeight, int imageWidth, int frameRate = 30)
    {
        var features = ExtractSwingFeatures(swing, imageHeight, imageWidth, frameRate);
        var prediction = _model.predict(features);

        // Extract the 6 output values: [overall, shoulder, contact, prep, balance, follow]
        var scores = prediction.numpy().ToArray<float>();

        // Map to existing SwingPrediction constructor
        var issueCategories = new string[] { "shoulder_rotation", "contact_point", "preparation_timing", "balance", "follow_through" };
        
        return new SwingPrediction(scores, issueCategories);
    }

    /// <summary>
    /// Extract normalized features from swing data for model prediction
    /// </summary>
    private NDArray ExtractSwingFeatures(SwingData swing, int imageHeight, int imageWidth, int frameRate)
    {
        var frameFeatures = new List<float[]>();
        Vector2[]? prev2Positions = null;
        Vector2[]? prevPositions = null;

        foreach (var frame in swing.Frames)
        {
            var (currentPositions, confidences) = MoveNetPoseFeatureExtractorService.KeypointsToPixels(
                frame, imageHeight, imageWidth);

            // Build frame features using the pose feature extractor
            var features = _poseFeatureExtractor.BuildFrameFeatures(
                prev2Positions,
                prevPositions,
                currentPositions,
                confidences,
                1.0f / frameRate);

            frameFeatures.Add(features);

            // Update position history
            prev2Positions = prevPositions;
            prevPositions = currentPositions;
        }

        // Normalize and pad the sequence
        var normalizedSequence = NormalizeAndPadSequence(frameFeatures);
        
        // Convert to NDArray with batch dimension
        var inputArray = np.zeros((1, _sequenceLength, _numFeatures));
        
        for (int i = 0; i < _sequenceLength; i++)
        {
            for (int j = 0; j < _numFeatures; j++)
            {
                inputArray[0, i, j] = normalizedSequence[i, j];
            }
        }

        return inputArray;
    }

    /// <summary>
    /// Normalize and pad sequence to match training data format
    /// </summary>
    private float[,] NormalizeAndPadSequence(List<float[]> frameFeatures)
    {
        var normalizedSequence = new float[_sequenceLength, _numFeatures];

        // Pad or truncate to target sequence length
        var actualLength = Math.Min(frameFeatures.Count, _sequenceLength);
        
        for (int frameIdx = 0; frameIdx < actualLength; frameIdx++)
        {
            var features = frameFeatures[frameIdx];
            for (int featIdx = 0; featIdx < Math.Min(features.Length, _numFeatures); featIdx++)
            {
                var value = features[featIdx];
                // Handle NaN values by setting them to 0
                normalizedSequence[frameIdx, featIdx] = float.IsNaN(value) ? 0.0f : value;
            }
        }

        return normalizedSequence;
    }

    /// <summary>
    /// Generate technique issues based on prediction scores
    /// </summary>
    private TechniqueIssues GenerateTechniqueIssues(float[] scores)
    {
        const float issueThreshold = 4.0f; // Below this score indicates an issue

        var issues = new TechniqueIssues
        {
            ShoulderRotationScore = scores.Length > 1 ? scores[1] : 0,
            ContactPointScore = scores.Length > 2 ? scores[2] : 0,
            PreparationTimingScore = scores.Length > 3 ? scores[3] : 0,
            BalanceScore = scores.Length > 4 ? scores[4] : 0,
            FollowThroughScore = scores.Length > 5 ? scores[5] : 0,
            DetectedIssues = Array.Empty<string>()
        };

        var detectedIssues = new List<string>();

        if (scores.Length > 1 && scores[1] < issueThreshold)
        {
            detectedIssues.Add("shoulder_rotation_timing");
            detectedIssues.Add("shoulder_alignment");
        }

        if (scores.Length > 2 && scores[2] < issueThreshold)
        {
            detectedIssues.Add("contact_point_positioning");
            detectedIssues.Add("contact_height_consistency");
        }

        if (scores.Length > 3 && scores[3] < issueThreshold)
        {
            detectedIssues.Add("preparation_timing");
            detectedIssues.Add("racket_takeback");
        }

        if (scores.Length > 4 && scores[4] < issueThreshold)
        {
            detectedIssues.Add("balance_throughout_swing");
            detectedIssues.Add("weight_transfer");
        }

        if (scores.Length > 5 && scores[5] < issueThreshold)
        {
            detectedIssues.Add("follow_through_extension");
            detectedIssues.Add("finish_position");
        }

        issues.DetectedIssues = detectedIssues.ToArray();
        return issues;
    }

    /// <summary>
    /// Calculate prediction confidence based on pose detection quality
    /// </summary>
    private double CalculateConfidence(SwingData swing)
    {
        var totalFrames = swing.Frames.Count;
        var highConfidenceFrames = 0;

        foreach (var frame in swing.Frames)
        {
            var highConfidenceKeypoints = frame.SwingPoseFeatures.Count(kp => kp.Confidence > MIN_CONFIDENCE);
            if (highConfidenceKeypoints >= 12) // At least 12 out of 17 keypoints have good confidence
            {
                highConfidenceFrames++;
            }
        }

        return (double)highConfidenceFrames / totalFrames;
    }

    /// <summary>
    /// Generate actionable coaching recommendations based on technique analysis
    /// </summary>
    public List<string> GenerateCoachingRecommendations(SwingPrediction prediction)
    {
        var recommendations = new List<string>();

        // Add recommendations based on the weakest technique areas
        var techniqueScores = new Dictionary<string, double>
        {
            ["Shoulder"] = prediction.ShoulderRotationScore,
            ["Contact"] = prediction.ContactPointScore,
            ["Preparation"] = prediction.PreparationTimingScore,
            ["Balance"] = prediction.BalanceScore,
            ["Follow-through"] = prediction.FollowThroughScore
        };

        var sortedTechniques = techniqueScores.OrderBy(x => x.Value).ToList();

        // Focus on the 3 weakest areas
        for (int i = 0; i < Math.Min(3, sortedTechniques.Count); i++)
        {
            var technique = sortedTechniques[i];
            recommendations.AddRange(GetTechniqueSpecificRecommendations(technique.Key, technique.Value));
        }

        // Add overall recommendations based on rating
        if (prediction.OverallScore < 3.0)
        {
            recommendations.Add("Focus on fundamental stroke mechanics and consistency");
            recommendations.Add("Practice basic swing patterns with slow, controlled movements");
        }
        else if (prediction.OverallScore < 4.5)
        {
            recommendations.Add("Work on shot placement and directional control");
            recommendations.Add("Develop more consistent timing patterns");
        }
        else if (prediction.OverallScore < 6.0)
        {
            recommendations.Add("Refine advanced techniques and shot variety");
            recommendations.Add("Focus on tactical shot selection and court positioning");
        }

        return recommendations.Take(5).ToList(); // Return top 5 recommendations
    }

    private List<string> GetTechniqueSpecificRecommendations(string technique, double score)
    {
        var recommendations = new List<string>();

        switch (technique.ToLower())
        {
            case "shoulder":
                recommendations.Add("Practice shoulder rotation drills with medicine ball");
                recommendations.Add("Focus on keeping shoulders level during preparation");
                break;
            case "contact":
                recommendations.Add("Work on contact point consistency drills");
                recommendations.Add("Practice hitting balls at different heights");
                break;
            case "preparation":
                recommendations.Add("Improve early racket preparation timing");
                recommendations.Add("Practice split-step and quick shoulder turn");
                break;
            case "balance":
                recommendations.Add("Include balance training exercises in practice");
                recommendations.Add("Focus on maintaining athletic stance throughout swing");
                break;
            case "follow-through":
                recommendations.Add("Extend follow-through across the body");
                recommendations.Add("Practice finishing with racket high and over opposite shoulder");
                break;
        }

        return recommendations;
    }

    public void Dispose()
    {
        // TensorFlow.NET models typically don't need explicit disposal
        // If using session-based models, dispose them here
    }
}