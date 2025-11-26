using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class SwingModelTrainingService(IPoseFeatureExtractorService PoseFeatureExtractorService)
{
    public async Task<string> TrainTensorFlowModelAsync(
        List<SwingData> trainingData,
        TensorFlowTrainingConfiguration config,
        int imageHeight,
        int imageWidth
    )
    {
        var labeledData = trainingData
            .Select(swing => new
            {
                swing.Frames,
                swing.OverallScore,
                TechniqueAnalysis = TechniqueAnalyzer.AnalyzeSwing(
                    swing.Frames,
                    swing.ContactFrame
                ),
            })
            .ToList();

        var batchSequences = ProcessFrameSequence(
            [.. labeledData.Select(x => (x.Frames, x.OverallScore))],
            config.SequenceLength,
            imageHeight,
            imageWidth
        );

        // Step 3: Prepare training targets
        var overallScores = labeledData.Select(x => x.OverallScore).ToArray();
        var techniqueScores = labeledData
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

        NDArray inputArray = batchSequences.ConvertToNumpyArray();
        var targetArray = concatenatedTargets.ConvertTargetsToNumpyArray();

        var model = SwingCnnModel.BuildSingleOutputModel(
            config.SequenceLength,
            config.NumFeatures,
            config.IssueCategories.Length
        );
        SwingCnnModel.CompileModel(model, config.LearningRate);

        Console.WriteLine("Training CNN model...");

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
        List<(List<FrameData> frames, float label)> trainingData,
        int sequenceLength,
        int imageHeight,
        int imageWidth,
        float deltaTime = 1 / 30f
    )
    {
        int batchSize = trainingData.Count;
        int numFeatures = 66;
        var batchSequences = new float[batchSize, sequenceLength, numFeatures];

        for (int batchIdx = 0; batchIdx < batchSize; batchIdx++)
        {
            var frames = trainingData[batchIdx].frames;
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
            for (int timeStep = 0; timeStep < sequenceLength; timeStep++)
            {
                if (timeStep < featuresList.Count)
                {
                    // Use actual frame data
                    for (int featIdx = 0; featIdx < numFeatures; featIdx++)
                    {
                        batchSequences[batchIdx, timeStep, featIdx] = featuresList[timeStep][
                            featIdx
                        ];
                    }
                }
                else
                {
                    // Pad with last frame for shorter sequences
                    int lastFrameIdx = featuresList.Count - 1;
                    for (int featIdx = 0; featIdx < numFeatures; featIdx++)
                    {
                        batchSequences[batchIdx, timeStep, featIdx] = featuresList[lastFrameIdx][
                            featIdx
                        ];
                    }
                }
            }
        }

        return batchSequences;
    }
}
