using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Numerics;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public class MoveNetInferenceService : IDisposable, IPoseInferenceService
{
    private readonly IImageProcessor _imageProcessor;
    private InferenceSession InferenceSession { get; set; }
    private const float MinConfidence = 0.3f;

    public MoveNetInferenceService(string modelPath, IImageProcessor? imageProcessor = null)
    {
        _imageProcessor = imageProcessor ?? new ImageProcessor();

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"MoveNet model file not found: {modelPath}");
        }
        InferenceSession = new InferenceSession(modelPath);
    }

    private JointData[] InferPoseFromImage(
        NDArray imageArray,
        int imageHeight,
        int imageWidth,
        int targetSize = 256,
        CropRegion? cropRegion = null,
        FrameData? prevFrame = null,
        FrameData? prev2Frame = null,
        float deltaTime = 1 / 30f)
    {
        // Convert NDArray to int tensor (model expects Int32 input)
        var inputTensor = ToOnnxTensor(imageArray, targetSize);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        };

        using var results = InferenceSession.Run(inputs);
        var output = results[0].AsEnumerable<float>().ToArray();

        // MoveNet output shape: [1, 1, 17, 3] (batch, person, keypoints, [y,x,confidence])
        var keypoints = new JointData[MoveNetVideoProcessor.NumKeyPoints];

        // Process each joint and calculate all properties as we go
        for (int i = 0; i < MoveNetVideoProcessor.NumKeyPoints; i++)
        {
            int baseIdx = i * 3;

            // Get normalized coordinates from model output
            var y = cropRegion is null ?
                output[baseIdx] :
                cropRegion.YMin + (cropRegion.Height * output[baseIdx]);
            var x = cropRegion is null ?
                output[baseIdx + 1] :
                cropRegion.XMin + (cropRegion.Width * output[baseIdx + 1]);
            var confidence = output[baseIdx + 2];

            // Convert to pixel coordinates
            var currentPixelPos = JointData.ToPixelCoordinates(x, y, imageHeight, imageWidth);

            // Calculate velocity if previous frame exists
            float? speed = null;
            if (prevFrame != null && confidence >= MinConfidence)
            {
                var prevPixelPos = prevFrame.Joints[i].ToPixelCoordinates(imageHeight, imageWidth);
                var velocity = (currentPixelPos - prevPixelPos) / deltaTime;
                speed = velocity.Length();
            }

            // Calculate acceleration if two previous frames exist
            float? acceleration = null;
            if (prev2Frame != null && prevFrame != null && confidence >= MinConfidence)
            {
                var prevPixelPos = prevFrame.Joints[i].ToPixelCoordinates(imageHeight, imageWidth);
                var prev2PixelPos = prev2Frame.Joints[i].ToPixelCoordinates(imageHeight, imageWidth);
                var accel = (currentPixelPos - (2 * prevPixelPos) + prev2PixelPos) / (deltaTime * deltaTime);
                acceleration = accel.Length();
            }

            keypoints[i] = new()
            {
                Y = y,
                X = x,
                Confidence = confidence,
                JointFeature = (JointFeatures)i,
                Speed = speed,
                Acceleration = acceleration
            };
        }

        return keypoints;
    }

    public float[] ComputeJointAngles(JointData[] keypoints, int imageHeight, int imageWidth)
    {
        var positions = new Vector2[MoveNetVideoProcessor.NumKeyPoints];
        var confidences = new float[MoveNetVideoProcessor.NumKeyPoints];

        for (int i = 0; i < MoveNetVideoProcessor.NumKeyPoints; i++)
        {
            positions[i] = keypoints[i].ToPixelCoordinates(imageHeight, imageWidth);
            confidences[i] = keypoints[i].Confidence;
        }

        return positions.ComputeJointAngles(confidences, MinConfidence);
    }

    private static DenseTensor<int> ToOnnxTensor(NDArray imageArray, int targetSize)
    {
        // Model expects Int32 input, shape [1, targetSize, targetSize, 3], values 0-255
        var shape = new[] { 1, targetSize, targetSize, 3 };
        var tensor = new DenseTensor<int>(shape);
        var byteData = imageArray.ToByteArray();
        for (int i = 0; i < byteData.Length; i++)
        {
            tensor.Buffer.Span[i] = (int)byteData[i];
        }

        return tensor;
    }

    public JointData[] RunInference(
        byte[] imageBytes,
        CropRegion cropRegion,
        int imageHeight,
        int imageWidth,
        FrameData? prevFrame = null,
        FrameData? prev2Frame = null,
        float deltaTime = 1 / 30f,
        int cropSize = 256)
    {
        // Convert bytes to NDArray, then crop and resize
        var imageArray = _imageProcessor.PreprocessImageBytes(imageBytes, cropSize);
        try
        {
            var croppedImage = _imageProcessor.CropAndResize(imageArray, cropRegion, cropSize);

            // Run inference on the cropped image
            return InferPoseFromImage(croppedImage, imageHeight, imageWidth, cropSize, cropRegion, prevFrame, prev2Frame, deltaTime);
        }
        catch
        {
            // On error, run inference on the original image
            return InferPoseFromImage(imageArray, imageHeight, imageWidth, cropSize, null, prevFrame, prev2Frame, deltaTime);
        }
    }

    public void Dispose()
    {
        InferenceSession.Dispose();
        GC.SuppressFinalize(this);
    }
}
