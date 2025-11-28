using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public class MoveNetInferenceService : IDisposable
{
    private readonly IImageProcessor _imageProcessor;
    private InferenceSession InferenceSession { get; set; }

    public MoveNetInferenceService(string modelPath, IImageProcessor? imageProcessor = null)
    {
        _imageProcessor = imageProcessor ?? new ImageProcessor();

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"MoveNet model file not found: {modelPath}");
        }
        InferenceSession = new InferenceSession(modelPath);
    }

    public SwingPoseFeatures[] InferPoseFromImage(NDArray imageArray, int targetSize = 256)
    {
        // Convert NDArray to float tensor (assuming imageArray is [H,W,C] and normalized)
        var inputTensor = ToOnnxTensor(imageArray, targetSize);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        };

        using var results = InferenceSession.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();

        // MoveNet output shape: [1, 1, 17, 3] (batch, person, keypoints, [y,x,confidence])
        // Parse output to SwingPoseFeatures[]
        var features = new List<SwingPoseFeatures>();
        int keypoints = 17;
        for (int i = 0; i < keypoints; i++)
        {
            int baseIdx = i * 3;
            features.Add(new SwingPoseFeatures
            {
                Y = output[baseIdx],
                X = output[baseIdx + 1],
                Confidence = output[baseIdx + 2]
            });
        }
        return features.ToArray();
    }

    private static DenseTensor<float> ToOnnxTensor(NDArray imageArray, int targetSize)
    {
        // Assumes imageArray is float32, shape [targetSize, targetSize, 3], normalized 0-1
        var shape = new[] { 1, targetSize, targetSize, 3 };
        var tensor = new DenseTensor<float>(shape);

        var flat = imageArray.ravel().astype(np.float32).ToArray<float>();

        for (int i = 0; i < flat.Length; i++)
        {
            tensor.Buffer.Span[i] = flat[i];
        }
        return tensor;
    }

    public SwingPoseFeatures[] InferPoseFromImageBytes(byte[] imageBytes, int targetSize = 256)
    {
        // Load image and preprocess
        var imageArray = _imageProcessor.PreprocessImageBytes(imageBytes, targetSize);
        return InferPoseFromImage(imageArray, targetSize);
    }

    public SwingPoseFeatures[] RunInference(NDArray image, CropRegion cropRegion, int cropSize = 256)
    {
        // Crop and resize the image according to the crop region
        var croppedImage = _imageProcessor.CropAndResize(image, cropRegion, cropSize);

        // Run inference on the cropped image
        return InferPoseFromImage(croppedImage, cropSize);
    }

    public SwingPoseFeatures[] RunInference(byte[] imageBytes, CropRegion cropRegion, int cropSize = 256)
    {
        // Convert bytes to NDArray, then crop and resize
        var imageArray = _imageProcessor.PreprocessImageBytes(imageBytes, cropSize);
        var croppedImage = _imageProcessor.CropAndResize(imageArray, cropRegion, cropSize);

        // Run inference on the cropped image
        return InferPoseFromImage(croppedImage, cropSize);
    }

    public void Dispose()
    {
        InferenceSession.Dispose();
        GC.SuppressFinalize(this);
    }
}
