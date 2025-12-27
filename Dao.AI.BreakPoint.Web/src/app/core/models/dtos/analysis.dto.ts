import { AnalysisStatus } from '../enums/analysis-status.enum';
import { SwingType } from '../enums/swing-type.enum';

/**
 * DTO for an in-progress analysis request
 */
export interface AnalysisRequestDto {
  id: number;
  playerId: number;
  status: AnalysisStatus;
  strokeType: SwingType;
  videoBlobUrl?: string;
  errorMessage?: string;
  createdAt: string;
  updatedAt: string;
  resultId?: number;
}

/**
 * DTO for a completed analysis result
 */
export interface AnalysisResultDto {
  id: number;
  analysisRequestId: number;
  playerId: number;
  strokeType: SwingType;
  qualityScore: number;
  featureImportance: Record<string, number>;
  coachingTips: string[];
  skeletonOverlayUrl?: string;
  videoBlobUrl?: string;
  createdAt: string;
}

/**
 * Summary view for dashboard lists
 */
export interface AnalysisResultSummaryDto {
  id: number;
  analysisRequestId: number;
  strokeType: SwingType;
  qualityScore: number;
  createdAt: string;
}

export interface CreateAnalysisRequest {
  playerId: number;
  strokeType: SwingType;
}
