using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.Services.MoveNet;

/// <summary>
/// ImageSharp-based implementation of <see cref="IImageProcessor"/> for MoveNet preprocessing.
/// </summary>
public class ImageSharpImageProcessor : IImageProcessor
{
    public NDArray PreprocessImageBytes(byte[] imageBytes, int targetSize)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        using var image = Image.Load<Rgb24>(imageBytes);

        // Resize to target size using bilinear interpolation
        image.Mutate(ctx => ctx.Resize(targetSize, targetSize, KnownResamplers.Bicubic));

        // Convert to NDArray format [1, height, width, 3]
        var imageData = new byte[targetSize * targetSize * 3];

        int idx = 0;
        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                var pixel = image[x, y];
                imageData[idx] = pixel.R;
                imageData[idx + 1] = pixel.G;
                imageData[idx + 2] = pixel.B;
                idx += 3;
            }
        }

        // Create NDArray with shape [1, height, width, 3] and uint8 values
        var ndArray = np.array(imageData).reshape(new int[] { 1, targetSize, targetSize, 3 });
        return ndArray.astype(np.uint8);
    }

    public NDArray CropAndResize(NDArray image, CropRegion cropRegion, int cropSize)
    {
        ArgumentNullException.ThrowIfNull(cropRegion);

        var imageShape = image.shape;
        if (imageShape.Length != 4 || imageShape[0] != 1 || imageShape[3] != 3)
        {
            throw new ArgumentException("Expected image shape [1, height, width, 3]");
        }

        int height = (int)imageShape[1];
        int width = (int)imageShape[2];

        // Convert NDArray to ImageSharp Image
        var imageBytes = image.ToByteArray();
        using var img = Image.LoadPixelData<Rgb24>(imageBytes, width, height);

        // Calculate crop coordinates
        int cropX = Math.Max(0, (int)(cropRegion.XMin * width));
        int cropY = Math.Max(0, (int)(cropRegion.YMin * height));
        int cropWidth = Math.Min(width - cropX, (int)((cropRegion.XMax - cropRegion.XMin) * width));
        int cropHeight = Math.Min(height - cropY, (int)((cropRegion.YMax - cropRegion.YMin) * height));

        // Ensure valid crop dimensions
        if (cropWidth <= 0 || cropHeight <= 0)
        {
            throw new ArgumentException("Invalid crop region specified.");
        }

        var cropRect = new Rectangle(cropX, cropY, cropWidth, cropHeight);

        // Crop and resize in one mutation
        img.Mutate(ctx => ctx
            .Crop(cropRect)
            .Resize(cropSize, cropSize, KnownResamplers.Bicubic));

        // Convert back to NDArray with shape [1, cropSize, cropSize, 3]
        var resultData = new byte[cropSize * cropSize * 3];

        int idx = 0;
        for (int y = 0; y < cropSize; y++)
        {
            for (int x = 0; x < cropSize; x++)
            {
                var pixel = img[x, y];
                resultData[idx] = pixel.R;
                resultData[idx + 1] = pixel.G;
                resultData[idx + 2] = pixel.B;
                idx += 3;
            }
        }

        var resultNDArray = np.array(resultData).reshape(new int[] { 1, cropSize, cropSize, 3 });
        return resultNDArray.astype(np.uint8);
    }
}
