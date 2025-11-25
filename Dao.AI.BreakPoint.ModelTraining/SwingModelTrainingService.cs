using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.ML;
using Microsoft.ML.Transforms;
using System.Numerics;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class SwingModelTrainingService(MLContext MlContext, IPoseFeatureExtractorService PoseFeatureExtractorService)
{
    /// <summary>
    /// Train a regression model from video sequences
    /// </summary>
    public async Task<ITransformer> TrainRegressionModelAsync(
        List<(List<FrameData> frames, float label)> trainingData,
        TrainingConfiguration config,
        int imageHeight,
        int imageWidth)
    {
        // Step 1: Process all sequences and extract features
        List<SwingAnalyzerInput> allFeatures = [];

        foreach (var (frames, label) in trainingData)
        {
            var rawFeatures = ProcessFrameSequence(frames, imageHeight, imageWidth);

            // Add each frame as a training example
            for (int i = 0; i < rawFeatures.GetLength(0); i++)
            {
                var frameFeatures = new float[rawFeatures.GetLength(1)];
                for (int j = 0; j < rawFeatures.GetLength(1); j++)
                {
                    frameFeatures[j] = rawFeatures[i, j];
                }

                allFeatures.Add(new SwingAnalyzerInput
                {
                    Features = frameFeatures,
                    Label = label
                });
            }
        }

        // Step 2: Create IDataView
        var dataView = MlContext.Data.LoadFromEnumerable(allFeatures);

        // Step 3: Split data
        var split = MlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

        // Step 4: Define pipeline
        var pipeline = MlContext
            // Handle missing values by replacing with mean
            .Transforms.ReplaceMissingValues(
                nameof(SwingAnalyzerInput.Features),
                replacementMode: MissingValueReplacingEstimator.ReplacementMode.Mean
            )
            // Normalize features
            .Append(MlContext.Transforms.NormalizeMeanVariance(
                nameof(SwingAnalyzerInput.Features)))
            // Add the regression trainer
            .Append(MlContext.Regression.Trainers.LbfgsPoissonRegression(
                labelColumnName: nameof(SwingAnalyzerInput.Label),
                featureColumnName: nameof(SwingAnalyzerInput.Features),
                l1Regularization: config.LearningRate,
                l2Regularization: config.LearningRate,
                optimizationTolerance: 1e-7f,
                historySize: config.HistorySize)
            );

        // Step 5: Train
        Console.WriteLine("Training model...");
        var model = pipeline.Fit(split.TrainSet);

        // Step 6: Evaluate
        var predictions = model.Transform(split.TestSet);
        var metrics = MlContext.Regression.Evaluate(predictions,
            labelColumnName: nameof(SwingAnalyzerInput.Label));

        Console.WriteLine($"R²: {metrics.RSquared:0.##}");
        Console.WriteLine($"RMSE: {metrics.RootMeanSquaredError:0.##}");
        Console.WriteLine($"MAE: {metrics.MeanAbsoluteError:0.##}");

        // Step 7: Save model
        MlContext.Model.Save(model, dataView.Schema, config.ModelOutputPath);
        Console.WriteLine($"Model saved to {config.ModelOutputPath}");

        return model;
    }

    private float[,] ProcessFrameSequence(
        List<FrameData> frames,
        int imageHeight,
        int imageWidth,
        float deltaTime = 1 / 30f)
    {
        var featuresList = new List<float[]>();
        Vector2[]? prev2Positions = null;
        Vector2[]? prevPositions = null;

        foreach (var frame in frames)
        {
            var (currentPositions, confidences) = MoveNetPoseFeatureExtractorService.KeypointsToPixels(frame, imageHeight, imageWidth);

            var frameFeatures = PoseFeatureExtractorService.BuildFrameFeatures(
                prev2Positions,
                prevPositions,
                currentPositions,
                confidences,
                deltaTime);

            featuresList.Add(frameFeatures);

            prev2Positions = prevPositions;
            prevPositions = currentPositions;
        }

        int numFrames = featuresList.Count;
        int numFeatures = featuresList[0].Length;
        var featuresArray = new float[numFrames, numFeatures];

        for (int i = 0; i < numFrames; i++)
        {
            for (int j = 0; j < numFeatures; j++)
            {
                featuresArray[i, j] = featuresList[i][j];
            }
        }

        return featuresArray;
    }
}
