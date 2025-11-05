using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services;

public interface ISwingAnalyzerService
{
    public Task<IEnumerable<string>> AnalyzeSwingForKeyFrames(
        Stream stream,
        AnalysisEvent analysisEvent,
        CancellationToken cancellationToken = default
        );
}
