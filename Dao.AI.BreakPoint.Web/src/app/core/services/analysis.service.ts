import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import {
  AnalysisRequestDto,
  AnalysisResultDto,
  AnalysisResultSummaryDto,
} from '../models/dtos/analysis.dto';
import { SwingType } from '../models/enums/swing-type.enum';

@Injectable({
  providedIn: 'root',
})
export class AnalysisService {
  private http = inject(HttpClient);

  // Signal-based state for reactive UI
  public currentRequest = signal<AnalysisRequestDto | null>(null);
  public currentResult = signal<AnalysisResultDto | null>(null);
  public pendingRequests = signal<AnalysisRequestDto[]>([]);
  public resultHistory = signal<AnalysisResultSummaryDto[]>([]);
  public isLoading = signal(false);

  /**
   * Upload a video for analysis
   */
  uploadVideo(
    video: File,
    playerId: number,
    strokeType: SwingType
  ): Observable<AnalysisRequestDto> {
    const formData = new FormData();
    formData.append('video', video);

    this.isLoading.set(true);

    return this.http
      .post<AnalysisRequestDto>(
        `api/Analysis/upload?playerId=${playerId}&strokeType=${strokeType}`,
        formData
      )
      .pipe(
        tap({
          next: (result) => {
            this.currentRequest.set(result);
            // Add to pending requests at the beginning
            this.pendingRequests.update((requests) => [result, ...requests]);
          },
          finalize: () => this.isLoading.set(false),
        })
      );
  }

  /**
   * Get analysis request by ID (for checking status)
   */
  getRequest(id: number): Observable<AnalysisRequestDto> {
    return this.http
      .get<AnalysisRequestDto>(`api/Analysis/request/${id}`)
      .pipe(tap((result) => this.currentRequest.set(result)));
  }

  /**
   * Get analysis result by ID (completed analysis)
   */
  getResult(id: number): Observable<AnalysisResultDto> {
    return this.http
      .get<AnalysisResultDto>(`api/Analysis/result/${id}`)
      .pipe(tap((result) => this.currentResult.set(result)));
  }

  /**
   * Get pending requests for a player
   */
  getPendingRequests(playerId: number): Observable<AnalysisRequestDto[]> {
    return this.http
      .get<AnalysisRequestDto[]>(`api/Analysis/player/${playerId}/pending`)
      .pipe(tap((results) => this.pendingRequests.set(results)));
  }

  /**
   * Get result history for a player
   */
  getResultHistory(
    playerId: number,
    page = 1,
    pageSize = 10
  ): Observable<AnalysisResultSummaryDto[]> {
    return this.http
      .get<AnalysisResultSummaryDto[]>(
        `api/Analysis/player/${playerId}/history`,
        {
          params: { page: page.toString(), pageSize: pageSize.toString() },
        }
      )
      .pipe(
        tap((results) => {
          if (page === 1) {
            this.resultHistory.set(results);
          } else {
            this.resultHistory.update((history) => [...history, ...results]);
          }
        })
      );
  }

  /**
   * Delete an analysis request
   */
  deleteRequest(id: number): Observable<void> {
    return this.http.delete<void>(`api/Analysis/request/${id}`).pipe(
      tap(() => {
        this.pendingRequests.update((requests) =>
          requests.filter((r) => r.id !== id)
        );
        if (this.currentRequest()?.id === id) {
          this.currentRequest.set(null);
        }
      })
    );
  }

  /**
   * Update from SignalR status change notification
   */
  updateRequestFromNotification(request: AnalysisRequestDto): void {
    this.currentRequest.set(request);

    // Update in pending requests if exists
    this.pendingRequests.update((requests) =>
      requests.map((r) => (r.id === request.id ? request : r))
    );
  }

  /**
   * Update from SignalR completion notification
   */
  updateResultFromNotification(result: AnalysisResultDto): void {
    this.currentResult.set(result);

    // Remove from pending requests
    this.pendingRequests.update((requests) =>
      requests.filter((r) => r.id !== result.analysisRequestId)
    );

    // Add to result history
    this.resultHistory.update((history) => [
      {
        id: result.id,
        analysisRequestId: result.analysisRequestId,
        strokeType: result.strokeType,
        qualityScore: result.qualityScore,
        createdAt: result.createdAt,
      },
      ...history,
    ]);
  }
}
