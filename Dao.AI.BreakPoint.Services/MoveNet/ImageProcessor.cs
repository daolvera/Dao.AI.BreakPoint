using OpenCvSharp;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public class ImageProcessor : IImageProcessor
{
    public NDArray PreprocessImageBytes(byte[] imageBytes, int targetSize)
    {
        // 1. Load image from bytes
        using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (mat.Empty())
        {
            throw new ArgumentException("Invalid image data provided.");
        }

        // 2. Convert BGR to RGB (OpenCV loads as BGR by default)
        using var rgbMat = new Mat();
        Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB);

        // 3. Resize to target size
        using var resizedMat = new Mat();
        Cv2.Resize(rgbMat, resizedMat, new OpenCvSharp.Size(targetSize, targetSize), interpolation: InterpolationFlags.Linear);

        // 4. Convert to NDArray format [1, height, width, 3]
        var imageData = new byte[targetSize * targetSize * 3];

        // Copy pixel data from Mat to array using safe indexer
        var indexer = resizedMat.GetGenericIndexer<Vec3b>();
        int idx = 0;
        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                var pixel = indexer[y, x];
                imageData[idx] = pixel.Item0;     // R
                imageData[idx + 1] = pixel.Item1; // G
                imageData[idx + 2] = pixel.Item2; // B
                idx += 3;
            }
        }

        // 5. Create NDArray with shape [1, height, width, 3] and uint8 values
        var ndArray = np.array(imageData).reshape(new int[] { 1, targetSize, targetSize, 3 });
        return ndArray.astype(np.uint8);
    }

    public NDArray CropAndResize(NDArray image, CropRegion cropRegion, int cropSize)
    {
        // Convert NDArray back to OpenCV Mat for processing
        var imageShape = image.shape;
        if (imageShape.Length != 4 || imageShape[0] != 1 || imageShape[3] != 3)
        {
            throw new ArgumentException("Expected image shape [1, height, width, 3]");
        }

        int height = (int)imageShape[1];
        int width = (int)imageShape[2];

        // Convert NDArray to byte array
        var imageBytes = image.ToByteArray();

        using var mat = Mat.FromPixelData(height, width, MatType.CV_8UC3, imageBytes);

        // 1. Calculate crop coordinates
        int cropX = Math.Max(0, (int)(cropRegion.XMin * width));
        int cropY = Math.Max(0, (int)(cropRegion.YMin * height));
        int cropWidth = Math.Min(width - cropX, (int)((cropRegion.XMax - cropRegion.XMin) * width));
        int cropHeight = Math.Min(height - cropY, (int)((cropRegion.YMax - cropRegion.YMin) * height));

        // Ensure valid crop dimensions
        if (cropWidth <= 0 || cropHeight <= 0)
        {
            throw new ArgumentException("Invalid crop region specified.");
        }

        var cropRect = new Rect(cropX, cropY, cropWidth, cropHeight);

        // 2. Extract the crop region
        using var croppedMat = new Mat(mat, cropRect);

        // 3. Resize the cropped area to [cropSize, cropSize]
        using var resizedMat = new Mat();
        Cv2.Resize(croppedMat, resizedMat, new OpenCvSharp.Size(cropSize, cropSize), interpolation: InterpolationFlags.Linear);

        // 4. Convert back to NDArray with shape [1, cropSize, cropSize, 3]
        var resultData = new byte[cropSize * cropSize * 3];

        var indexer = resizedMat.GetGenericIndexer<Vec3b>();
        int idx = 0;
        for (int y = 0; y < cropSize; y++)
        {
            for (int x = 0; x < cropSize; x++)
            {
                var pixel = indexer[y, x];
                resultData[idx] = pixel.Item0;     // R
                resultData[idx + 1] = pixel.Item1; // G
                resultData[idx + 2] = pixel.Item2; // B
                idx += 3;
            }
        }

        var resultNDArray = np.array(resultData).reshape(new int[] { 1, cropSize, cropSize, 3 });
        return resultNDArray.astype(np.uint8);
    }
}