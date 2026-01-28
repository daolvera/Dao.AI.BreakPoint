import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DrillFeedbackRequest,
  DrillRecommendationDto,
} from '../models/dtos/analysis.dto';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root',
})
export class DrillService {
  private http = inject(HttpClient);
  private config = inject(ConfigService);

  /**
   * Get all drill recommendations for a player
   */
  getPlayerDrills(
    playerId: number,
    activeOnly = true,
  ): Observable<DrillRecommendationDto[]> {
    return this.http.get<DrillRecommendationDto[]>(
      this.config.getApiUrl(`drills/player/${playerId}`),
      {
        params: { activeOnly: activeOnly.toString() },
      },
    );
  }

  /**
   * Get drill recommendations for a specific analysis result
   */
  getAnalysisDrills(
    analysisResultId: number,
  ): Observable<DrillRecommendationDto[]> {
    return this.http.get<DrillRecommendationDto[]>(
      this.config.getApiUrl(`drills/analysis/${analysisResultId}`),
    );
  }

  /**
   * Get a specific drill recommendation
   */
  getDrill(id: number): Observable<DrillRecommendationDto> {
    return this.http.get<DrillRecommendationDto>(
      this.config.getApiUrl(`drills/${id}`),
    );
  }

  /**
   * Mark a drill as completed
   */
  completeDrill(id: number): Observable<DrillRecommendationDto> {
    return this.http.post<DrillRecommendationDto>(
      this.config.getApiUrl(`drills/${id}/complete`),
      {},
    );
  }

  /**
   * Submit feedback for a drill
   */
  submitFeedback(
    id: number,
    feedback: DrillFeedbackRequest,
  ): Observable<DrillRecommendationDto> {
    return this.http.post<DrillRecommendationDto>(
      this.config.getApiUrl(`drills/${id}/feedback`),
      feedback,
    );
  }

  /**
   * Dismiss/deactivate a drill recommendation
   */
  dismissDrill(id: number): Observable<void> {
    return this.http.post<void>(
      this.config.getApiUrl(`drills/${id}/dismiss`),
      {},
    );
  }

  /**
   * Get drill history with feedback for a player
   */
  getDrillHistory(
    playerId: number,
    limit = 20,
  ): Observable<DrillRecommendationDto[]> {
    return this.http.get<DrillRecommendationDto[]>(
      this.config.getApiUrl(`drills/player/${playerId}/history`),
      {
        params: { limit: limit.toString() },
      },
    );
  }
}
