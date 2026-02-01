using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.MoveNet;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Service for generating skeleton overlay images and GIFs from swing analysis data using ImageSharp.
/// Draws MoveNet skeleton on video frames with color-coded joint highlighting.
/// </summary>
public class ImageSharpSkeletonOverlayService : ISkeletonOverlayService
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

    // Colors (RGBA format for ImageSharp)
    private static readonly Color GoodColor = Color.FromRgba(0, 255, 0, 255); // Green
    private static readonly Color WarningColor = Color.FromRgba(255, 165, 0, 255); // Orange
    private static readonly Color BadColor = Color.FromRgba(255, 0, 0, 255); // Red
    private static readonly Color NeutralColor = Color.FromRgba(0, 255, 255, 255); // Cyan
    private static readonly Color BoneColor = Color.FromRgba(255, 255, 255, 255); // White

    private const float MinConfidence = 0.3f;
    private const int JointRadius = 6;
    private const int BoneThickness = 2;

    private readonly Font _font;
    private readonly Font _smallFont;
    private readonly Font _tinyFont;

    public ImageSharpSkeletonOverlayService()
    {
        // Use system fonts - fallback to a default if not found
        var fontFamily = SystemFonts.Families.FirstOrDefault(f =>
            f.Name.Contains("Arial", StringComparison.OrdinalIgnoreCase) ||
            f.Name.Contains("Segoe", StringComparison.OrdinalIgnoreCase) ||
            f.Name.Contains("DejaVu", StringComparison.OrdinalIgnoreCase));

        fontFamily = fontFamily.Name is null
            ? SystemFonts.Families.First()
            : fontFamily;

        _font = fontFamily.CreateFont(16, FontStyle.Bold);
        _smallFont = fontFamily.CreateFont(12, FontStyle.Regular);
        _tinyFont = fontFamily.CreateFont(10, FontStyle.Regular);
    }

    /// <summary>
    /// Generate a skeleton overlay PNG for a single frame
    /// </summary>
    public byte[] GenerateOverlayImage(
        byte[] frameImage,
        FrameData frameData,
        Dictionary<string, double> featureImportance,
        double qualityScore,
        int imageWidth,
        int imageHeight)
    {
        using var image = Image.Load<Rgba32>(frameImage);

        // Identify problem joints based on feature importance
        var problemJoints = GetProblemJoints(featureImportance);

        // Draw skeleton
        DrawSkeleton(image, frameData.Joints, problemJoints, imageWidth, imageHeight);

        // Add text annotations
        AddAnnotations(image, qualityScore, featureImportance, frameData);

        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
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
        int frameDelayMs = 50)
    {
        var problemJoints = GetProblemJoints(featureImportance);
        var processedFrames = new List<Image<Rgba32>>();

        try
        {
            // Find the worst frame (lowest confidence or specific criteria)
            int worstFrameIndex = FindWorstFrameIndex(swingData, featureImportance);

            for (int i = 0; i < Math.Min(frameImages.Count, swingData.Frames.Count); i++)
            {
                var frameImage = frameImages[i];
                var frameData = swingData.Frames[i];

                using var sourceImage = Image.Load<Rgba32>(frameImage);

                // Resize for GIF efficiency (max 480p)
                var resizedImage = ResizeForGif(sourceImage);

                // Draw skeleton with highlighting
                bool isWorstFrame = i == worstFrameIndex;
                DrawSkeleton(
                    resizedImage,
                    frameData.Joints,
                    problemJoints,
                    imageWidth,
                    imageHeight,
                    isWorstFrame);

                // Add minimal annotations (just score, no detailed text for GIF)
                AddGifAnnotations(
                    resizedImage,
                    qualityScore,
                    frameData,
                    i,
                    swingData.Frames.Count,
                    isWorstFrame);

                processedFrames.Add(resizedImage);
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
        Dictionary<string, double> featureImportance)
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
        Image<Rgba32> image,
        JointData[] joints,
        HashSet<int> problemJoints,
        int originalWidth,
        int originalHeight,
        bool highlightFrame = false)
    {
        // Joint coordinates are normalized (0-1), so multiply directly by frame dimensions
        // The frame may be resized for GIF, so we use the actual frame dimensions
        int frameWidth = image.Width;
        int frameHeight = image.Height;

        image.Mutate(ctx =>
        {
            // Draw bones first (so joints appear on top)
            foreach (var (joint1Idx, joint2Idx) in SkeletonConnections)
            {
                if (joint1Idx >= joints.Length || joint2Idx >= joints.Length)
                    continue;

                var joint1 = joints[joint1Idx];
                var joint2 = joints[joint2Idx];

                if (joint1.Confidence < MinConfidence || joint2.Confidence < MinConfidence)
                    continue;

                var pt1 = new PointF(joint1.X * frameWidth, joint1.Y * frameHeight);
                var pt2 = new PointF(joint2.X * frameWidth, joint2.Y * frameHeight);

                // Color bone based on whether either joint is problematic
                Color boneColor =
                    (problemJoints.Contains(joint1Idx) || problemJoints.Contains(joint2Idx))
                        ? BadColor
                        : BoneColor;

                ctx.DrawLine(boneColor, BoneThickness, pt1, pt2);
            }

            // Draw joints
            for (int i = 0; i < joints.Length; i++)
            {
                var joint = joints[i];
                if (joint.Confidence < MinConfidence)
                    continue;

                var center = new PointF(joint.X * frameWidth, joint.Y * frameHeight);

                // Determine joint color
                Color color;
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
                var ellipse = new EllipsePolygon(center, radius);
                ctx.Fill(color, ellipse);

                // Draw outline for emphasis
                if (highlightFrame && problemJoints.Contains(i))
                {
                    var outlineEllipse = new EllipsePolygon(center, radius + 3);
                    ctx.Draw(Color.White, 2, outlineEllipse);
                }
            }

            // Add frame border if this is the worst frame
            if (highlightFrame)
            {
                var rectangle = new RectangularPolygon(0, 0, image.Width, image.Height);
                ctx.Draw(BadColor, 3, rectangle);
            }
        });
    }

    /// <summary>
    /// Add text annotations to the frame
    /// </summary>
    private void AddAnnotations(
        Image<Rgba32> image,
        double qualityScore,
        Dictionary<string, double> featureImportance,
        FrameData frameData)
    {
        int yPos = 10;

        // Quality score with color coding
        Color scoreColor =
            qualityScore >= 70 ? GoodColor
            : qualityScore >= 40 ? WarningColor
            : BadColor;

        image.Mutate(ctx =>
        {
            ctx.DrawText($"Quality: {qualityScore:F0}/100", _font, scoreColor, new PointF(10, yPos));
            yPos += 25;

            // Swing phase
            ctx.DrawText($"Phase: {frameData.SwingPhase}", _smallFont, NeutralColor, new PointF(10, yPos));
            yPos += 20;

            // Top problem features (up to 3)
            var topProblems = featureImportance
                .Where(kvp => kvp.Value < 0.3) // Low importance = potential problem
                .OrderBy(kvp => kvp.Value)
                .Take(3)
                .ToList();

            if (topProblems.Count > 0)
            {
                ctx.DrawText("Focus Areas:", _tinyFont, WarningColor, new PointF(10, yPos));
                yPos += 15;

                foreach (var problem in topProblems)
                {
                    string shortName = ShortenFeatureName(problem.Key);
                    ctx.DrawText($"- {shortName}", _tinyFont, BadColor, new PointF(15, yPos));
                    yPos += 14;
                }
            }
        });
    }

    /// <summary>
    /// Add minimal annotations for GIF frames
    /// </summary>
    private void AddGifAnnotations(
        Image<Rgba32> image,
        double qualityScore,
        FrameData frameData,
        int frameIndex,
        int totalFrames,
        bool isWorstFrame)
    {
        // Quality score
        Color scoreColor =
            qualityScore >= 70 ? GoodColor
            : qualityScore >= 40 ? WarningColor
            : BadColor;

        image.Mutate(ctx =>
        {
            ctx.DrawText($"{qualityScore:F0}", _smallFont, scoreColor, new PointF(10, 10));

            // Frame counter
            ctx.DrawText($"{frameIndex + 1}/{totalFrames}", _tinyFont, NeutralColor, new PointF(image.Width - 50, 10));

            // Phase indicator
            string phaseShort = frameData.SwingPhase switch
            {
                SwingPhase.Backswing => "BACK",
                SwingPhase.Contact => "CONTACT",
                SwingPhase.FollowThrough => "FOLLOW",
                _ => "",
            };

            if (!string.IsNullOrEmpty(phaseShort))
            {
                ctx.DrawText(phaseShort, _tinyFont, NeutralColor, new PointF(10, image.Height - 20));
            }

            // Worst frame indicator
            if (isWorstFrame)
            {
                ctx.DrawText("FOCUS", _smallFont, BadColor, new PointF((image.Width / 2) - 25, 10));
            }
        });
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
            .Replace("Position", "Pos");
    }

    /// <summary>
    /// Resize frame for GIF efficiency
    /// </summary>
    private static Image<Rgba32> ResizeForGif(Image<Rgba32> source, int maxHeight = 360)
    {
        if (source.Height <= maxHeight)
        {
            return source.Clone();
        }

        double scale = maxHeight / (double)source.Height;
        int newWidth = (int)(source.Width * scale);

        var resized = source.Clone();
        resized.Mutate(ctx => ctx.Resize(newWidth, maxHeight));
        return resized;
    }

    /// <summary>
    /// Encode frames as an animated GIF
    /// </summary>
    private static byte[] EncodeGif(List<Image<Rgba32>> frames, int frameDelayMs)
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
        foreach (var frameImage in sampledFrames)
        {
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
                    });

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
    private static List<Image<Rgba32>> SampleFrames(List<Image<Rgba32>> frames, int maxFrames)
    {
        if (frames.Count <= maxFrames)
        {
            return frames;
        }

        var sampled = new List<Image<Rgba32>>();
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
}
