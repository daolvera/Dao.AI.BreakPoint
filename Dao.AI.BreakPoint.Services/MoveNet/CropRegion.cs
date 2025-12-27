namespace Dao.AI.BreakPoint.Services.MoveNet;

public class CropRegion
{
    public float YMin { get; set; }
    public float XMin { get; set; }
    public float YMax { get; set; }
    public float XMax { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }

    public static CropRegion InitCropRegion(int imageHeight, int imageWidth)
    {
        if (imageWidth > imageHeight)
        {
            float boxHeight = (float)imageWidth / imageHeight;
            float boxWidth = 1.0f;
            float yMin = ((imageHeight / 2.0f) - (imageWidth / 2.0f)) / imageHeight;
            float xMin = 0.0f;

            return new CropRegion
            {
                YMin = yMin,
                XMin = xMin,
                YMax = yMin + boxHeight,
                XMax = xMin + boxWidth,
                Height = boxHeight,
                Width = boxWidth
            };
        }
        else
        {
            float boxHeight = 1.0f;
            float boxWidth = (float)imageHeight / imageWidth;
            float yMin = 0.0f;
            float xMin = ((imageWidth / 2.0f) - (imageHeight / 2.0f)) / imageWidth;

            return new CropRegion
            {
                YMin = yMin,
                XMin = xMin,
                YMax = yMin + boxHeight,
                XMax = xMin + boxWidth,
                Height = boxHeight,
                Width = boxWidth
            };
        }
    }
}