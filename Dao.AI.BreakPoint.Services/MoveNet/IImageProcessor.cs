using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public interface IImageProcessor
{
    NDArray PreprocessImageBytes(byte[] imageBytes, int targetSize);
    NDArray CropAndResize(NDArray image, CropRegion cropRegion, int cropSize);
}
