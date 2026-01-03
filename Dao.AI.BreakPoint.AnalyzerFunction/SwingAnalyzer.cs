using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.Azure.Functions.Worker;

namespace Dao.AI.BreakPoint.AnalyzerFunction;

public class SwingAnalyzer(
    ISwingAnalyzerService SwingAnalyzerService,
    IAnalysisProcessingService AnalysisEventService
)
{
    [Function(nameof(SwingAnalyzer))]
    public async Task Run(
        [BlobTrigger(
            "swing-analysis/{analysisRequestId}",
            Source = BlobTriggerSource.EventGrid,
            Connection = "BlobStorage"
        )]
            Stream videoStream,
        int analysisRequestId
    )
    {
        AnalysisRequest analysisRequest =
            await AnalysisEventService.GetRequestAsync(analysisRequestId)
            ?? throw new MissingAnalysisRequestException(analysisRequestId);
        await SwingAnalyzerService.AnalyzeSwingAsync(videoStream, analysisRequest);
    }
}
