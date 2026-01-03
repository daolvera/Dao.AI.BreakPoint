using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Text.Json;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Helper service to extract frames from videos and prepare them for labeling.
/// This creates JSON files with features that just need a phase label added.
/// </summary>
public class FrameLabelingHelper
{
    private readonly string _moveNetModelPath;
    private readonly string _outputDirectory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public FrameLabelingHelper(string moveNetModelPath, string outputDirectory)
    {
        _moveNetModelPath = moveNetModelPath;
        _outputDirectory = outputDirectory;

        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
    }

    /// <summary>
    /// Extract frames from a video and save feature files for labeling.
    /// Each frame gets a JSON file with features pre-computed.
    /// User just needs to add the "phase" field (0-4).
    /// </summary>
    public async Task ExtractFramesForLabelingAsync(
        string videoPath,
        List<byte[]> frameImages,
        VideoMetadata metadata,
        bool isRightHanded,
        int sampleEveryNFrames = 500
    )
    {
        var videoName = Path.GetFileNameWithoutExtension(videoPath);
        var cropRegion = CropRegion.InitCropRegion(metadata.Height, metadata.Width);
        float deltaTime = 1.0f / metadata.FrameRate;

        using var inferenceService = new MoveNetInferenceService(_moveNetModelPath);

        FrameData? prevFrame = null;
        FrameData? prev2Frame = null;

        for (int frameIndex = 1; frameIndex < frameImages.Count; frameIndex++)
        {
            bool isFrameToSample = frameIndex % sampleEveryNFrames == 0;
            bool isFrameBeforeSample = (frameIndex + 1) % sampleEveryNFrames == 0;
            bool isFrameTwoBeforeSample = (frameIndex + 2) % sampleEveryNFrames == 0;
            // only do the work when needed
            if (isFrameToSample || isFrameBeforeSample || isFrameTwoBeforeSample)
            {
                var frame = frameImages[frameIndex];

                var keypoints = inferenceService.RunInference(
                    frame,
                    cropRegion,
                    metadata.Height,
                    metadata.Width,
                    prevFrame,
                    prev2Frame,
                    deltaTime
                );

                var angles = inferenceService.ComputeJointAngles(
                    keypoints,
                    metadata.Height,
                    metadata.Width
                );

                if (isFrameTwoBeforeSample)
                {
                    prev2Frame = CreateFrameData(keypoints, angles, frameIndex);
                }
                if (isFrameBeforeSample)
                {
                    prevFrame = CreateFrameData(keypoints, angles, frameIndex);
                }

                // Only sample every N frames to reduce labeling work
                if (frameIndex % sampleEveryNFrames == 0)
                {
                    // Extract features for the classifier
                    var features = SwingPhaseClassifierTrainingService.ExtractFrameFeatures(
                        keypoints,
                        angles,
                        isRightHanded,
                        prevFrame,
                        prev2Frame
                    );

                    // Create unlabeled frame data
                    var frameData = new UnlabeledFrameJson
                    {
                        VideoName = videoName,
                        FrameIndex = frameIndex,
                        Timestamp = frameIndex / metadata.FrameRate,
                        IsRightHanded = isRightHanded,
                        Features = features,
                        Phase = -1, // -1 indicates unlabeled, user needs to set 0-4
                    };

                    // Save to JSON file
                    var outputPath = Path.Combine(
                        _outputDirectory,
                        $"{videoName}_frame_{frameIndex:D5}.json"
                    );

                    var json = JsonSerializer.Serialize(
                        frameData,
                        _jsonOptions
                    );

                    await File.WriteAllTextAsync(outputPath, json);
                }

                // Update crop region
                cropRegion = GetCropRegion(keypoints, metadata);
            }

        }

        Console.WriteLine(
            $"Extracted {frameImages.Count / sampleEveryNFrames} frames from {videoName}"
        );
    }

    private static FrameData CreateFrameData(JointData[] keypoints, float[] angles, int frameIndex)
    {
        return new FrameData
        {
            Joints = keypoints,
            SwingPhase = Data.Enums.SwingPhase.Preparation, // Placeholder
            LeftElbowAngle = angles[0],
            RightElbowAngle = angles[1],
            LeftShoulderAngle = angles[2],
            RightShoulderAngle = angles[3],
            LeftHipAngle = angles[4],
            RightHipAngle = angles[5],
            LeftKneeAngle = angles[6],
            RightKneeAngle = angles[7],
            FrameIndex = frameIndex,
        };
    }

    private static CropRegion GetCropRegion(JointData[] keypoints, VideoMetadata metadata)
    {
        // Simplified crop region - use full frame if torso not visible
        var leftHip = keypoints[(int)JointFeatures.LeftHip];
        var rightHip = keypoints[(int)JointFeatures.RightHip];

        if (leftHip.Confidence < 0.2f && rightHip.Confidence < 0.2f)
        {
            return CropRegion.InitCropRegion(metadata.Height, metadata.Width);
        }

        return CropRegion.InitCropRegion(metadata.Height, metadata.Width);
    }

    /// <summary>
    /// Print labeling instructions
    /// </summary>
    public static void PrintLabelingInstructions()
    {
        Console.WriteLine("=== Frame Labeling Instructions ===");
        Console.WriteLine();
        Console.WriteLine("Edit each JSON file and set the 'phase' field to:");
        Console.WriteLine("  0 = None (no person visible, or person not in tennis stance)");
        Console.WriteLine("  1 = Preparation (ready position, waiting for ball)");
        Console.WriteLine("  2 = Backswing (racket going back, body coiling)");
        Console.WriteLine("  3 = Swing (forward motion, racket accelerating through contact)");
        Console.WriteLine("  4 = FollowThrough (after contact, racket decelerating)");
        Console.WriteLine();
        Console.WriteLine("Tips:");
        Console.WriteLine("  - Open the corresponding video frame image to see the pose");
        Console.WriteLine("  - Look at wrist position relative to body for phase cues");
        Console.WriteLine("  - Backswing: racket behind body, shoulders turned");
        Console.WriteLine("  - Swing: racket moving forward, high velocity");
        Console.WriteLine("  - FollowThrough: racket in front, shoulders squared");
        Console.WriteLine();
    }
}

/// <summary>
/// JSON structure for unlabeled frames awaiting human labeling
/// </summary>
internal class UnlabeledFrameJson
{
    public required string VideoName { get; set; }
    public int FrameIndex { get; set; }
    public double Timestamp { get; set; }
    public bool IsRightHanded { get; set; }
    public required float[] Features { get; set; }

    /// <summary>
    /// Phase label: -1 = unlabeled, 0-4 = labeled phase
    /// </summary>
    public int Phase { get; set; }
}
