namespace Dao.AI.BreakPoint.Services.Exceptions;

/// <summary>
/// Exception to be thrown in async data processing when the analysis event cannot be found
/// </summary>
/// <param name="analysisEventId">The analysis event id that cannot be found</param>
public class MissingAnalysisRequestException(int analysisEventId)
    : InvalidOperationException($"Analysis Event with ID {analysisEventId} not found.")
{
    // TODO DAO: better messaging
}
