using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.Exceptions;
using Microsoft.Azure.Functions.Worker;

namespace Dao.AI.BreakPoint.AnalyzerFunction;

public class SwingAnalyzer(
    ISwingAnalyzerService SwingAnalyzerService,
    IAnalysisEventService AnalysisEventService
    //ILogger<SwingAnalyzer> Logger
    )
{

    [Function(nameof(SwingAnalyzer))]
    public async Task Run([BlobTrigger("swing-analysis/{analysisEventId}", Source = BlobTriggerSource.EventGrid, Connection = "")] Stream stream, string analysisEventId)
    {
        AnalysisEvent analysisEvent = await AnalysisEventService.GetAnalysisEventAsync(analysisEventId)
            ?? throw new MissingAnalysisEventException(analysisEventId);

        var keyFrames = await SwingAnalyzerService.AnalyzeSwingForKeyFrames(
            stream,
            analysisEvent);

        // foreach key frame, send it to the custom image analyzer
        // get the results for each type of key frame, package that up and send it to the custom coaching ai (llm)
        // save off the resulting coaching ai response to database as a swing analysis result
    }
}
