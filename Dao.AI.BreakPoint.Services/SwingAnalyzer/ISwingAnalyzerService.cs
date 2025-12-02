using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public interface ISwingAnalyzerService
{
    Task AnalyzeSwingAsync(Stream videoStream, AnalysisEvent analysisEvent);
}