using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.VideoProcessing;
using Microsoft.Extensions.Options;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingAnalyzerService(
    IVideoProcessingService VideoProcessingService,
    IOptions<MoveNetOptions> MoveNetOptions
    ) : ISwingAnalyzerService
{
    public async Task AnalyzeSwingAsync(Stream videoStream, AnalysisEvent analysisEvent)
    {
        // save the stream to a temporary file
        string tempFilePath = Path.GetTempFileName();
        using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
        {
            await videoStream.CopyToAsync(fileStream);
        }
        var frameImages = VideoProcessingService.ExtractFrames(tempFilePath);
        if (frameImages.Count == 0)
        {
            throw new InvalidOperationException("No frames extracted from the provided video stream.");
        }
        var metadata = VideoProcessingService.GetVideoMetadata(tempFilePath);
        using var processor = new MoveNetVideoProcessor(MoveNetOptions.Value.ModelPath);
        var swingVideo = processor.ProcessVideoFrames(frameImages, metadata);
        // take the prepared swings and analyze them
        // save the result to the db as a swinganalysis
        // update the swing analysis status
        analysisEvent.AnaylsisStatus = AnaylsisStatus.Completed;
    }
}
