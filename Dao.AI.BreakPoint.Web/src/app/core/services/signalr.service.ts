import { Injectable, inject, signal, NgZone } from '@angular/core';
import { Subject } from 'rxjs';
import {
  AnalysisRequestDto,
  AnalysisResultDto,
} from '../models/dtos/analysis.dto';
import { TokenService } from './token.service';
import { ConfigService } from './config.service';

export interface SignalRConnectionState {
  connected: boolean;
  error?: string;
}

@Injectable({
  providedIn: 'root',
})
export class SignalRService {
  private connection: any = null;
  private tokenService = inject(TokenService);
  private configService = inject(ConfigService);
  private ngZone = inject(NgZone);

  public connectionState = signal<SignalRConnectionState>({ connected: false });

  // Subjects for analysis events
  public analysisStatusChanged$ = new Subject<AnalysisRequestDto>();
  public analysisCompleted$ = new Subject<AnalysisResultDto>();
  public analysisFailed$ = new Subject<{
    analysisRequestId: number;
    errorMessage: string;
  }>();

  private subscribedPlayers = new Set<number>();
  private subscribedAnalyses = new Set<number>();

  async startConnection(): Promise<void> {
    if (this.connection) {
      return;
    }

    try {
      const signalR = await import('@microsoft/signalr');

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(this.configService.getApiUrl('hubs/analysis'), {
          // Use arrow function to get fresh token on each request
          accessTokenFactory: () => this.tokenService.getAccessToken() ?? '',
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

      // Register event handlers - wrap in NgZone to ensure Angular change detection
      // and use void return to prevent any async response expectations
      this.connection.on(
        'AnalysisStatusChanged',
        (request: AnalysisRequestDto): void => {
          this.ngZone.run(() => {
            this.analysisStatusChanged$.next(request);
          });
        },
      );

      this.connection.on(
        'AnalysisCompleted',
        (result: AnalysisResultDto): void => {
          this.ngZone.run(() => {
            this.analysisCompleted$.next(result);
          });
        },
      );

      this.connection.on(
        'AnalysisFailed',
        (analysisRequestId: number, errorMessage: string): void => {
          this.ngZone.run(() => {
            this.analysisFailed$.next({ analysisRequestId, errorMessage });
          });
        },
      );

      this.connection.onclose((): void => {
        this.ngZone.run(() => {
          this.connectionState.set({ connected: false });
        });
      });

      this.connection.onreconnected((): void => {
        this.ngZone.run(() => {
          this.connectionState.set({ connected: true });
          this.resubscribeToGroups();
        });
      });

      await this.connection.start();
      this.connectionState.set({ connected: true });
    } catch (error: any) {
      console.error('SignalR connection failed:', error);
      this.connectionState.set({ connected: false, error: error.message });
    }
  }

  async stopConnection(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.connectionState.set({ connected: false });
      this.subscribedPlayers.clear();
      this.subscribedAnalyses.clear();
    }
  }

  async joinPlayerGroup(playerId: number): Promise<void> {
    if (this.connection && this.connectionState().connected) {
      await this.connection.invoke('JoinPlayerGroup', playerId);
      this.subscribedPlayers.add(playerId);
    }
  }

  async leavePlayerGroup(playerId: number): Promise<void> {
    if (this.connection && this.connectionState().connected) {
      await this.connection.invoke('LeavePlayerGroup', playerId);
      this.subscribedPlayers.delete(playerId);
    }
  }

  async joinAnalysisGroup(analysisId: number): Promise<void> {
    if (this.connection && this.connectionState().connected) {
      await this.connection.invoke('JoinAnalysisGroup', analysisId);
      this.subscribedAnalyses.add(analysisId);
    }
  }

  async leaveAnalysisGroup(analysisId: number): Promise<void> {
    if (this.connection && this.connectionState().connected) {
      await this.connection.invoke('LeaveAnalysisGroup', analysisId);
      this.subscribedAnalyses.delete(analysisId);
    }
  }

  private async resubscribeToGroups(): Promise<void> {
    for (const playerId of this.subscribedPlayers) {
      await this.connection.invoke('JoinPlayerGroup', playerId);
    }
    for (const analysisId of this.subscribedAnalyses) {
      await this.connection.invoke('JoinAnalysisGroup', analysisId);
    }
  }
}
