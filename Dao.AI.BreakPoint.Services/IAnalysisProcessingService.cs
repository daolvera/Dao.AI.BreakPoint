using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Service for processing analysis requests (used by Azure Function)
/// </summary>
public interface IAnalysisProcessingService
{
    /// <summary>
    /// Gets an analysis request by its ID (used by Azure Function)
    /// </summary>
    Task<AnalysisRequest?> GetRequestAsync(int analysisRequestId);

    /// <summary>
    /// Updates the status of an analysis request
    /// </summary>
    Task UpdateStatusAsync(
        int analysisRequestId,
        AnalysisStatus status,
        string? errorMessage = null
    );

    /// <summary>
    /// Creates the analysis result from completed processing
    /// </summary>
    Task<AnalysisResult> CreateResultAsync(
        int analysisRequestId,
        double qualityScore,
        Dictionary<string, double> featureImportance,
        string? skeletonOverlayUrl
    );
}
