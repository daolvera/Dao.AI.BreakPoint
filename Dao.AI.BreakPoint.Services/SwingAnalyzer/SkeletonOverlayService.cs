using Dao.AI.BreakPoint.Services.MoveNet;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using CvPoint = OpenCvSharp.Point;
using CvSize = OpenCvSharp.Size;
using ISImage = SixLabors.ImageSharp.Image;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Service for generating skeleton overlay images and GIFs from swing analysis data.
/// Draws MoveNet skeleton on video frames with color-coded joint highlighting.
/// </summary>
public class SkeletonOverlayService : ISkeletonOverlayService
{
    // MoveNet skeleton connections (pairs of joint indices)
    private static readonly (int, int)[] SkeletonConnections =
    [
        // Face
        ((int)JointFeatures.LeftEar, (int)JointFeatures.LeftEye),
        ((int)JointFeatures.LeftEye, (int)JointFeatures.Nose),
        ((int)JointFeatures.Nose, (int)JointFeatures.RightEye),
        ((int)JointFeatures.RightEye, (int)JointFeatures.RightEar),
        // Torso
        ((int)JointFeatures.LeftShoulder, (int)JointFeatures.RightShoulder),
        ((int)JointFeatures.LeftShoulder, (int)JointFeatures.LeftHip),
        ((int)JointFeatures.RightShoulder, (int)JointFeatures.RightHip),
        ((int)JointFeatures.LeftHip, (int)JointFeatures.RightHip),
        // Left arm
        ((int)JointFeatures.LeftShoulder, (int)JointFeatures.LeftElbow),
        ((int)JointFeatures.LeftElbow, (int)JointFeatures.LeftWrist),
        // Right arm
        ((int)JointFeatures.RightShoulder, (int)JointFeatures.RightElbow),
        ((int)JointFeatures.RightElbow, (int)JointFeatures.RightWrist),
        // Left leg
        ((int)JointFeatures.LeftHip, (int)JointFeatures.LeftKnee),
        ((int)JointFeatures.LeftKnee, (int)JointFeatures.LeftAnkle),
        // Right leg
        ((int)JointFeatures.RightHip, (int)JointFeatures.RightKnee),
        ((int)JointFeatures.RightKnee, (int)JointFeatures.RightAnkle),
    ];

    // Colors (BGR format for OpenCV)
    private static readonly Scalar GoodColor = new(0, 255, 0); // Green
    private static readonly Scalar WarningColor = new(0, 165, 255); // Orange
    private static readonly Scalar BadColor = new(0, 0, 255); // Red
    private static readonly Scalar NeutralColor = new(255, 255, 0); // Cyan
    private static readonly Scalar BoneColor = new(255, 255, 255); // White

    private const float MinConfidence = 0.3f;
    private const int JointRadius = 6;
    private const int BoneThickness = 2;

    /// <summary>
    /// Generate a skeleton overlay PNG for a single frame
    /// </summary>
    public byte[] GenerateOverlayImage(
        byte[] frameImage,
        FrameData frameData,
        Dictionary<string, double> featureImportance,
        double qualityScore,
        int imageWidth,
        int imageHeight
    )
    {
        using var mat = Mat.FromImageData(frameImage, ImreadModes.Color);

        // Identify problem joints based on feature importance
        var problemJoints = GetProblemJoints(featureImportance);

        // Draw skeleton
        DrawSkeleton(mat, frameData.Joints, problemJoints, imageWidth, imageHeight);

        // Add text annotations
        AddAnnotations(mat, qualityScore, featureImportance, frameData);

        return mat.ToBytes(".png");
    }

    /// <summary>
    /// Generate a skeleton overlay GIF showing the full swing
    /// </summary>
    public byte[] GenerateOverlayGif(
        List<byte[]> frameImages,
        SwingData swingData,
        Dictionary<string, double> featureImportance,
        double qualityScore,
        int imageWidth,
        int imageHeight,
        int frameDelayMs = 50
    )
    {
        var problemJoints = GetProblemJoints(featureImportance);
        var processedFrames = new List<Mat>();

        try
        {
            // Find the worst frame (lowest confidence or specific criteria)
            int worstFrameIndex = FindWorstFrameIndex(swingData, featureImportance);

            for (int i = 0; i < Math.Min(frameImages.Count, swingData.Frames.Count); i++)
            {
                var frameImage = frameImages[i];
                var frameData = swingData.Frames[i];

                using var sourceMat = Mat.FromImageData(frameImage, ImreadModes.Color);

                // Resize for GIF efficiency (max 480p)
                var resizedMat = ResizeForGif(sourceMat);

                // Draw skeleton with highlighting
                bool isWorstFrame = i == worstFrameIndex;
                DrawSkeleton(
                    resizedMat,
                    frameData.Joints,
                    problemJoints,
                    imageWidth,
                    imageHeight,
                    isWorstFrame
                );

                // Add minimal annotations (just score, no detailed text for GIF)
                AddGifAnnotations(
                    resizedMat,
                    qualityScore,
                    frameData,
                    i,
                    swingData.Frames.Count,
                    isWorstFrame
                );

                processedFrames.Add(resizedMat);
            }

            // Encode as GIF
            return EncodeGif(processedFrames, frameDelayMs);
        }
        finally
        {
            // Dispose all frames
            foreach (var frame in processedFrames)
            {
                frame.Dispose();
            }
        }
    }

    /// <summary>
    /// Find the frame index with the worst technique (for highlighting)
    /// </summary>
    public int FindWorstFrameIndex(
        SwingData swingData,
        Dictionary<string, double> featureImportance
    )
    {
        // Find the frame with the lowest average confidence or highest deviation
        // Focus on the "swing" phase where contact happens
        int worstIndex = swingData.Frames.Count / 2; // Default to middle

        float lowestConfidence = float.MaxValue;

        for (int i = 0; i < swingData.Frames.Count; i++)
        {
            var frame = swingData.Frames[i];

            // Calculate average confidence for key joints (wrists, elbows, shoulders)
            var keyJoints = new[]
            {
                (int)JointFeatures.LeftWrist,
                (int)JointFeatures.RightWrist,
                (int)JointFeatures.LeftElbow,
                (int)JointFeatures.RightElbow,
                (int)JointFeatures.LeftShoulder,
                (int)JointFeatures.RightShoulder,
            };

            float avgConfidence = keyJoints
                .Where(j => j < frame.Joints.Length)
                .Average(j => frame.Joints[j].Confidence);

            if (avgConfidence < lowestConfidence)
            {
                lowestConfidence = avgConfidence;
                worstIndex = i;
            }
        }

        return worstIndex;
    }

    /// <summary>
    /// Draw skeleton on the frame with color-coded joints
    /// </summary>
    private void DrawSkeleton(
        Mat frame,
        JointData[] joints,
        HashSet<int> problemJoints,
        int originalWidth,
        int originalHeight,
        bool highlightFrame = false
    )
    {
        // Joint coordinates are normalized (0-1), so multiply directly by frame dimensions
        // The frame may be resized for GIF, so we use the actual frame dimensions
        int frameWidth = frame.Width;
        int frameHeight = frame.Height;

        // Draw bones first (so joints appear on top)
        foreach (var (joint1Idx, joint2Idx) in SkeletonConnections)
        {
            if (joint1Idx >= joints.Length || joint2Idx >= joints.Length)
                continue;

            var joint1 = joints[joint1Idx];
            var joint2 = joints[joint2Idx];

            if (joint1.Confidence < MinConfidence || joint2.Confidence < MinConfidence)
                continue;

            var pt1 = new CvPoint((int)(joint1.X * frameWidth), (int)(joint1.Y * frameHeight));
            var pt2 = new CvPoint((int)(joint2.X * frameWidth), (int)(joint2.Y * frameHeight));

            // Color bone based on whether either joint is problematic
            Scalar boneColor =
                (problemJoints.Contains(joint1Idx) || problemJoints.Contains(joint2Idx))
                    ? BadColor
                    : BoneColor;

            Cv2.Line(frame, pt1, pt2, boneColor, BoneThickness);
        }

        // Draw joints
        for (int i = 0; i < joints.Length; i++)
        {
            var joint = joints[i];
            if (joint.Confidence < MinConfidence)
                continue;

            var center = new CvPoint((int)(joint.X * frameWidth), (int)(joint.Y * frameHeight));

            // Determine joint color
            Scalar color;
            int radius = JointRadius;

            if (problemJoints.Contains(i))
            {
                color = BadColor;
                radius = JointRadius + 2; // Make problem joints larger
            }
            else if (joint.Confidence > 0.7f)
            {
                color = GoodColor;
            }
            else if (joint.Confidence > 0.5f)
            {
                color = WarningColor;
            }
            else
            {
                color = NeutralColor;
            }

            // Draw filled circle for joint
            Cv2.Circle(frame, center, radius, color, -1);

            // Draw outline for emphasis
            if (highlightFrame && problemJoints.Contains(i))
            {
                Cv2.Circle(frame, center, radius + 3, new Scalar(255, 255, 255), 2);
            }
        }

        // Add frame border if this is the worst frame
        if (highlightFrame)
        {
            Cv2.Rectangle(
                frame,
                new CvPoint(0, 0),
                new CvPoint(frame.Width - 1, frame.Height - 1),
                BadColor,
                3
            );
        }
    }

    /// <summary>
    /// Add text annotations to the frame
    /// </summary>
    private static void AddAnnotations(
        Mat frame,
        double qualityScore,
        Dictionary<string, double> featureImportance,
        FrameData frameData
    )
    {
        int yPos = 30;
        var fontFace = HersheyFonts.HersheySimplex;
        double fontScale = 0.7;
        int thickness = 2;

        // Quality score with color coding
        Scalar scoreColor =
            qualityScore >= 70 ? GoodColor
            : qualityScore >= 40 ? WarningColor
            : BadColor;

        Cv2.PutText(
            frame,
            $"Quality: {qualityScore:F0}/100",
            new CvPoint(10, yPos),
            fontFace,
            fontScale,
            scoreColor,
            thickness
        );
        yPos += 30;

        // Swing phase
        Cv2.PutText(
            frame,
            $"Phase: {frameData.SwingPhase}",
            new CvPoint(10, yPos),
            fontFace,
            fontScale * 0.8,
            NeutralColor,
            1
        );
        yPos += 25;

        // Top problem features (up to 3)
        var topProblems = featureImportance
            .Where(kvp => kvp.Value < 0.3) // Low importance = potential problem
            .OrderBy(kvp => kvp.Value)
            .Take(3)
            .ToList();

        if (topProblems.Count > 0)
        {
            Cv2.PutText(
                frame,
                "Focus Areas:",
                new CvPoint(10, yPos),
                fontFace,
                fontScale * 0.6,
                WarningColor,
                1
            );
            yPos += 20;

            foreach (var problem in topProblems)
            {
                string shortName = ShortenFeatureName(problem.Key);
                Cv2.PutText(
                    frame,
                    $"- {shortName}",
                    new CvPoint(15, yPos),
                    fontFace,
                    fontScale * 0.5,
                    BadColor,
                    1
                );
                yPos += 18;
            }
        }
    }

    /// <summary>
    /// Add minimal annotations for GIF frames
    /// </summary>
    private static void AddGifAnnotations(
        Mat frame,
        double qualityScore,
        FrameData frameData,
        int frameIndex,
        int totalFrames,
        bool isWorstFrame
    )
    {
        var fontFace = HersheyFonts.HersheySimplex;

        // Quality score
        Scalar scoreColor =
            qualityScore >= 70 ? GoodColor
            : qualityScore >= 40 ? WarningColor
            : BadColor;

        Cv2.PutText(frame, $"{qualityScore:F0}", new CvPoint(10, 25), fontFace, 0.6, scoreColor, 2);

        // Frame counter
        Cv2.PutText(
            frame,
            $"{frameIndex + 1}/{totalFrames}",
            new CvPoint(frame.Width - 60, 25),
            fontFace,
            0.4,
            NeutralColor,
            1
        );

        // Phase indicator
        string phaseShort = frameData.SwingPhase switch
        {
            Data.Enums.SwingPhase.Backswing => "BACK",
            Data.Enums.SwingPhase.Contact => "CONTACT",
            Data.Enums.SwingPhase.FollowThrough => "FOLLOW",
            _ => "",
        };

        if (!string.IsNullOrEmpty(phaseShort))
        {
            Cv2.PutText(
                frame,
                phaseShort,
                new CvPoint(10, frame.Height - 10),
                fontFace,
                0.4,
                NeutralColor,
                1
            );
        }

        // Worst frame indicator
        if (isWorstFrame)
        {
            Cv2.PutText(
                frame,
                "FOCUS",
                new CvPoint(frame.Width / 2 - 30, 25),
                fontFace,
                0.5,
                BadColor,
                2
            );
        }
    }

    /// <summary>
    /// Identify joints that need improvement based on feature importance
    /// </summary>
    private static HashSet<int> GetProblemJoints(Dictionary<string, double> featureImportance)
    {
        var problemJoints = new HashSet<int>();

        // Map feature names back to joint indices
        var featureToJoint = new Dictionary<string, int[]>
        {
            ["Left Shoulder"] = [(int)JointFeatures.LeftShoulder],
            ["Right Shoulder"] = [(int)JointFeatures.RightShoulder],
            ["Left Elbow"] = [(int)JointFeatures.LeftElbow],
            ["Right Elbow"] = [(int)JointFeatures.RightElbow],
            ["Left Wrist"] = [(int)JointFeatures.LeftWrist],
            ["Right Wrist"] = [(int)JointFeatures.RightWrist],
            ["Left Hip"] = [(int)JointFeatures.LeftHip],
            ["Right Hip"] = [(int)JointFeatures.RightHip],
            ["Left Knee"] = [(int)JointFeatures.LeftKnee],
            ["Right Knee"] = [(int)JointFeatures.RightKnee],
            ["Left Ankle"] = [(int)JointFeatures.LeftAnkle],
            ["Right Ankle"] = [(int)JointFeatures.RightAnkle],
        };

        // Find features with low importance (areas needing work)
        var lowImportanceFeatures = featureImportance
            .Where(kvp => kvp.Value < 0.3)
            .OrderBy(kvp => kvp.Value)
            .Take(5);

        foreach (var feature in lowImportanceFeatures)
        {
            // Extract joint name from feature name
            foreach (var mapping in featureToJoint)
            {
                if (feature.Key.Contains(mapping.Key))
                {
                    foreach (var jointIdx in mapping.Value)
                    {
                        problemJoints.Add(jointIdx);
                    }
                    break;
                }
            }
        }

        return problemJoints;
    }

    /// <summary>
    /// Shorten feature name for display
    /// </summary>
    private static string ShortenFeatureName(string featureName)
    {
        return featureName
            .Replace("Velocity", "Vel")
            .Replace("Acceleration", "Acc")
            .Replace("Position", "Pos")
            .Replace("Angle", "∠");
    }

    /// <summary>
    /// Resize frame for GIF efficiency
    /// </summary>
    private static Mat ResizeForGif(Mat source, int maxHeight = 360)
    {
        if (source.Height <= maxHeight)
        {
            return source.Clone();
        }

        double scale = maxHeight / (double)source.Height;
        int newWidth = (int)(source.Width * scale);

        var resized = new Mat();
        Cv2.Resize(source, resized, new OpenCvSharp.Size(newWidth, maxHeight));
        return resized;
    }

    /// <summary>
    /// Encode frames as an animated GIF using ImageSharp
    /// </summary>
    private static byte[] EncodeGif(List<Mat> frames, int frameDelayMs)
    {
        if (frames.Count == 0)
        {
            return [];
        }

        // Convert delay from milliseconds to centiseconds (GIF uses 1/100th second units)
        int frameDelay = Math.Max(1, frameDelayMs / 10);

        // Sample frames to keep GIF size reasonable (max ~30 frames for a swing)
        var sampledFrames = SampleFrames(frames, maxFrames: 30);

        // Get dimensions from first frame
        int width = sampledFrames[0].Width;
        int height = sampledFrames[0].Height;

        // Create the animated GIF
        using var gif = new Image<Rgba32>(width, height);
        var gifMetaData = gif.Metadata.GetGifMetadata();
        gifMetaData.RepeatCount = 0; // Loop forever

        bool isFirstFrame = true;
        foreach (var cvFrame in sampledFrames)
        {
            // Convert OpenCV Mat to ImageSharp Image
            using var frameImage = ConvertMatToImageSharp(cvFrame);

            // Get the root frame for the GIF
            var rootFrame = gif.Frames.RootFrame;

            if (isFirstFrame)
            {
                // Copy first frame data directly to root frame
                frameImage.Frames.RootFrame.ProcessPixelRows(
                    rootFrame,
                    (sourceAccessor, targetAccessor) =>
                    {
                        for (int y = 0; y < sourceAccessor.Height; y++)
                        {
                            var sourceRow = sourceAccessor.GetRowSpan(y);
                            var targetRow = targetAccessor.GetRowSpan(y);
                            sourceRow.CopyTo(targetRow);
                        }
                    }
                );

                // Set delay on root frame
                var rootMeta = rootFrame.Metadata.GetGifMetadata();
                rootMeta.FrameDelay = frameDelay;

                isFirstFrame = false;
            }
            else
            {
                // Add subsequent frames
                var addedFrame = gif.Frames.AddFrame(frameImage.Frames.RootFrame);
                var frameMeta = addedFrame.Metadata.GetGifMetadata();
                frameMeta.FrameDelay = frameDelay;
            }
        }

        // Encode to byte array
        using var ms = new MemoryStream();
        var encoder = new GifEncoder { ColorTableMode = GifColorTableMode.Local };
        gif.SaveAsGif(ms, encoder);
        return ms.ToArray();
    }

    /// <summary>
    /// Sample frames to reduce GIF size while maintaining smooth animation
    /// </summary>
    private static List<Mat> SampleFrames(List<Mat> frames, int maxFrames)
    {
        if (frames.Count <= maxFrames)
        {
            return frames;
        }

        var sampled = new List<Mat>();
        double step = (double)frames.Count / maxFrames;

        for (int i = 0; i < maxFrames; i++)
        {
            int frameIndex = (int)(i * step);
            if (frameIndex < frames.Count)
            {
                sampled.Add(frames[frameIndex]);
            }
        }

        // Always include last frame
        if (sampled.Count > 0 && sampled[^1] != frames[^1])
        {
            sampled[^1] = frames[^1];
        }

        return sampled;
    }

    /// <summary>
    /// Convert OpenCV Mat (BGR) to ImageSharp Image (RGBA)
    /// </summary>
    private static Image<Rgba32> ConvertMatToImageSharp(Mat mat)
    {
        // Convert BGR to RGB
        using var rgbMat = new Mat();
        Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB);

        // Create ImageSharp image
        var image = new Image<Rgba32>(mat.Width, mat.Height);

        // Copy pixel data
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    var pixel = rgbMat.At<Vec3b>(y, x);
                    pixelRow[x] = new Rgba32(pixel.Item0, pixel.Item1, pixel.Item2, 255);
                }
            }
        });

        return image;
    }
}

/// <summary>
/// Interface for skeleton overlay generation
/// </summary>
public interface ISkeletonOverlayService
{
    /// <summary>
    /// Generate a skeleton overlay PNG for a single frame
    /// </summary>
    byte[] GenerateOverlayImage(
        byte[] frameImage,
        FrameData frameData,
        Dictionary<string, double> featureImportance,
        double qualityScore,
        int imageWidth,
        int imageHeight
    );

    /// <summary>
    /// Generate a skeleton overlay GIF showing the full swing
    /// </summary>
    byte[] GenerateOverlayGif(
        List<byte[]> frameImages,
        SwingData swingData,
        Dictionary<string, double> featureImportance,
        double qualityScore,
        int imageWidth,
        int imageHeight,
        int frameDelayMs = 50
    );

    /// <summary>
    /// Find the frame index with the worst technique
    /// </summary>
    int FindWorstFrameIndex(SwingData swingData, Dictionary<string, double> featureImportance);
}
