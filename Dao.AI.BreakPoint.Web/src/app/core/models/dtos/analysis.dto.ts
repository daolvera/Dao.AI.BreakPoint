import { AnalysisStatus } from '../enums/analysis-status.enum';
import { SwingType } from '../enums/swing-type.enum';
import { SwingPhase } from '../enums/swing-phase.enum';

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
 * Phase-specific quality scores
 */
export interface PhaseScoresDto {
  preparation: number;
  backswing: number;
  contact: number;
  followThrough: number;
}

/**
 * Individual feature deviation from reference
 */
export interface FeatureDeviationDto {
  featureIndex: number;
  featureName: string;
  zScore: number;
  actualValue: number;
  referenceMean: number;
  referenceStd: number;
  severity: 'significant' | 'moderate' | 'slight' | 'normal';
  direction: 'above' | 'below';
}

/**
 * Phase-specific deviations
 */
export interface PhaseDeviationDto {
  phase: SwingPhase;
  featureDeviations: FeatureDeviationDto[];
}

/**
 * Drill recommendation
 */
export interface DrillRecommendationDto {
  id: number;
  analysisResultId: number;
  playerId: number;
  targetPhase: SwingPhase;
  targetFeature: string;
  drillName: string;
  description: string;
  suggestedDuration?: string;
  priority: number;
  completedAt?: string;
  thumbsUp?: boolean;
  feedbackText?: string;
  isActive: boolean;
  createdAt: string;
}

/**
 * Request to submit drill feedback
 */
export interface DrillFeedbackRequest {
  thumbsUp: boolean;
  feedbackText?: string;
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
  phaseScores: PhaseScoresDto;
  phaseDeviations: PhaseDeviationDto[];
  drillRecommendations: DrillRecommendationDto[];
  coachingTips: string[];
  skeletonOverlayUrl?: string;
  skeletonOverlayGifUrl?: string;
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
  phaseScores: PhaseScoresDto;
  createdAt: string;
}

export interface CreateAnalysisRequest {
  playerId: number;
  strokeType: SwingType;
}
