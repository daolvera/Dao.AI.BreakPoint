using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class SwingModelTrainingService(IPoseFeatureExtractorService PoseFeatureExtractorService)
{
    public async Task<string> TrainTensorFlowModelAsync(
        List<CleanedSwingData> trainingData,
        TrainingConfiguration config
    )
    {
        // Extract frame sequences with consistent image dimensions
        var batchSequences = ProcessFrameSequence(trainingData, config);

        var overallScores = trainingData.Select(x => x.OverallScore).ToArray();
        var techniqueScores = trainingData
            .Select(x =>
                new float[]
                {
                    x.TechniqueAnalysis.ShoulderRotationScore,
                    x.TechniqueAnalysis.ContactPointScore,
                    x.TechniqueAnalysis.PreparationTimingScore,
                    x.TechniqueAnalysis.BalanceScore,
                    x.TechniqueAnalysis.FollowThroughScore,
                }
            )
            .ToArray();

        var concatenatedTargets = labeledData
            .Select(x =>
                ModelTrainingUtilities.CreateConcatenatedTarget(
                    x.OverallScore,
                    x.TechniqueAnalysis,
                    config.IssueCategories
                )
            )
            .ToArray();
        float[] ustaRatings = [.. trainingData.Select(x => (float)x.OverallScore)];

        NDArray inputArray = batchSequences.ConvertToNumpyArray();
        var targetArray = ustaRatings.ConvertTargetsToNumpyArray();

        var model = SwingCnnModel.BuildSingleOutputModel(
            config.SequenceLength,
            config.NumFeatures,
            outputSize: 1 // Single output for USTA rating prediction
        );
        SwingCnnModel.CompileModel(model, config.LearningRate);

        Console.WriteLine("Training CNN model for USTA rating prediction...");

        var history = model.fit(
            inputArray,
            targetArray,
            batch_size: config.BatchSize,
            epochs: config.Epochs,
            validation_split: config.ValidationSplit,
            verbose: 1
        );

        model.save(config.ModelOutputPath);
        Console.WriteLine($"Model saved to {config.ModelOutputPath}");

        return config.ModelOutputPath;
    }

    private float[,,] ProcessFrameSequence(
        List<CleanedSwingData> trainingData,
        TrainingConfiguration config,
        float deltaTime = 1f / 30f
    )
    {
        int numFeatures = 66;
        var batchSequences = new float[config.BatchSize, trainingData.First().RawSwingData.FrameRate, numFeatures];

        for (int batchIdx = 0; batchIdx < config.BatchSize; batchIdx++)
        {
            var swingData = trainingData[batchIdx];
            var frames = swingData.RawSwingData.Frames;
            var imageHeight = swingData.RawSwingData.ImageHeight;
            var imageWidth = swingData.RawSwingData.ImageWidth;

            var featuresList = new List<float[]>();
            Vector2[]? prev2Positions = null;
            Vector2[]? prevPositions = null;

            foreach (var frame in frames)
            {
                var (currentPositions, confidences) =
                    MoveNetPoseFeatureExtractorService.KeypointsToPixels(
                        frame,
                        imageHeight,
                        imageWidth
                    );

                var frameFeatures = PoseFeatureExtractorService.BuildFrameFeatures(
                    prev2Positions,
                    prevPositions,
                    currentPositions,
                    confidences,
                    deltaTime
                );

                featuresList.Add(frameFeatures);

                prev2Positions = prevPositions;
                prevPositions = currentPositions;
            }

            // Pad or truncate to fixed sequence length
            for (int timeStep = 0; timeStep < swingData.RawSwingData.FrameRate; timeStep++)
            {
                if (timeStep < featuresList.Count)
                {
                    // Use actual frame data
                    for (int featIdx = 0; featIdx < numFeatures; featIdx++)
                    {
                        batchSequences[batchIdx, timeStep, featIdx] = featuresList[timeStep][featIdx];
                    }
                }
                else
                {
                    // Pad with last frame for shorter sequences
                    int lastFrameIdx = featuresList.Count - 1;
                    for (int featIdx = 0; featIdx < numFeatures; featIdx++)
                    {
                        batchSequences[batchIdx, timeStep, featIdx] = featuresList[lastFrameIdx][featIdx];
                    }
                }
            }
        }

        return batchSequences;
    }
}
