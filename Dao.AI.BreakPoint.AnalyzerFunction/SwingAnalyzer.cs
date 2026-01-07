using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.Azure.Functions.Worker;

namespace Dao.AI.BreakPoint.AnalyzerFunction;

public class SwingAnalyzer(
    ISwingAnalyzerService SwingAnalyzerService,
    IAnalysisProcessingService AnalysisEventService,
    IBlobStorageService BlobStorageService
)
{
    /// <summary>
    /// Triggered when a video is uploaded to the swing-analysis container.
    /// Uses polling-based trigger (default) which works with both Azurite and Azure Blob Storage.
    /// Note: For production with high volume, consider using EventGrid source with proper Azure Event Grid setup.
    /// </summary>
    [Function(nameof(SwingAnalyzer))]
    public async Task Run(
        [BlobTrigger(
            "swing-analysis/{analysisRequestFileName}"
        )]
        Stream videoStream,
        string analysisRequestFileName
    )
    {
        // check if there is an extension first
        if (Path.HasExtension(analysisRequestFileName))
        {
            analysisRequestFileName = Path.GetFileNameWithoutExtension(analysisRequestFileName);
        }
        int analysisRequestId = int.Parse(analysisRequestFileName);
        AnalysisRequest analysisRequest =
            await AnalysisEventService.GetRequestAsync(analysisRequestId)
            ?? throw new MissingAnalysisRequestException(analysisRequestId);
        await SwingAnalyzerService.AnalyzeSwingAsync(videoStream, analysisRequest);

        // Delete the video blob after successful analysis
        if (!string.IsNullOrEmpty(analysisRequest.VideoBlobUrl))
        {
            await BlobStorageService.DeleteAsync(analysisRequest.VideoBlobUrl);
        }
    }
}
