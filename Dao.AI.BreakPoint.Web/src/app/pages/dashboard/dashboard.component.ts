import { CommonModule } from '@angular/common';
import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';

import { MatProgressBar } from '@angular/material/progress-bar';
import {
  AnalysisRequestDto,
  AnalysisResultDto,
  PlayerWithStatsDto,
} from '../../core/models/dtos';
import { AnalysisStatus } from '../../core/models/enums/analysis-status.enum';
import { Handedness } from '../../core/models/enums/handedness.enum';
import { SwingType } from '../../core/models/enums/swing-type.enum';
import { AnalysisService } from '../../core/services/analysis.service';
import { AuthService } from '../../core/services/auth.service';
import { PlayerService } from '../../core/services/player.service';
import { SignalRService } from '../../core/services/signalr.service';
import { VideoUploadComponent } from '../../shared/components/video-upload/video-upload.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatSelectModule,
    FormsModule,
    VideoUploadComponent,
    MatProgressBar,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit, OnDestroy {
  protected authService = inject(AuthService);
  protected analysisService = inject(AnalysisService);
  protected signalRService = inject(SignalRService);
  private playerService = inject(PlayerService);

  private destroy$ = new Subject<void>();

  // UI State
  protected showUploadDialog = signal(false);
  protected selectedStrokeType = signal<SwingType>(
    SwingType.ForehandGroundStroke
  );
  protected isConnecting = signal(false);

  // Player data
  protected playerWithStats = signal<PlayerWithStatsDto | null>(null);

  // Data - use the new service signals
  protected pendingRequests = this.analysisService.pendingRequests;
  protected resultHistory = this.analysisService.resultHistory;
  protected isLoading = this.analysisService.isLoading;

  // Enums for template
  protected AnalysisStatus = AnalysisStatus;
  protected Handedness = Handedness;

  // Stroke type options for dropdown
  protected strokeTypeOptions: { value: SwingType; label: string }[] = [
    { value: SwingType.ForehandGroundStroke, label: 'Forehand Ground Stroke' },
    { value: SwingType.BackhandGroundStroke, label: 'Backhand Ground Stroke' },
  ];

  // Computed values
  protected playerId = computed(() => this.authService.userInfo()?.playerId);
  protected playerName = computed(
    () =>
      this.playerWithStats()?.name ||
      this.authService.userInfo()?.displayName ||
      'Player'
  );

  // Computed statistics for the template (avoid arrow functions in template)
  protected totalAnalyses = computed(
    () => this.pendingRequests().length + this.resultHistory().length
  );
  protected completedCount = computed(() => this.resultHistory().length);
  protected inProgressCount = computed(() => this.pendingRequests().length);
  protected hasActiveAnalysis = computed(
    () => this.pendingRequests().length > 0
  );

  ngOnInit(): void {
    this.loadPlayerData();
    this.loadAnalysisData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.disconnectSignalR();
  }

  private loadPlayerData(): void {
    const pid = this.playerId();
    if (pid) {
      this.playerService
        .getPlayerWithStatsById(pid)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (player) => this.playerWithStats.set(player),
          error: (err) => console.error('Failed to load player data:', err),
        });
    }
  }

  /**
   * Load analysis data and connect SignalR if there are pending analyses
   */
  private loadAnalysisData(): void {
    const pid = this.playerId();
    if (!pid) return;

    // Load history
    this.analysisService.getResultHistory(pid).subscribe();

    // Load pending and connect SignalR if needed
    this.analysisService.getPendingRequests(pid).subscribe({
      next: (requests) => {
        if (requests.length > 0) {
          this.connectSignalR();
        }
      },
    });
  }

  /**
   * Connect to SignalR and subscribe to events
   */
  private async connectSignalR(): Promise<void> {
    if (this.signalRService.connectionState().connected) {
      return;
    }

    const pid = this.playerId();
    if (!pid) return;

    try {
      this.isConnecting.set(true);
      await this.signalRService.startConnection();
      await this.signalRService.joinPlayerGroup(pid);

      // Subscribe to events after connection is established
      this.signalRService.analysisCompleted$
        .pipe(takeUntil(this.destroy$))
        .subscribe((result: AnalysisResultDto) => {
          console.log('SignalR: AnalysisCompleted received', result);
          this.analysisService.updateResultFromNotification(result);
        });

      this.signalRService.analysisStatusChanged$
        .pipe(takeUntil(this.destroy$))
        .subscribe((request: AnalysisRequestDto) => {
          console.log('SignalR: AnalysisStatusChanged received', request);
          this.analysisService.updateRequestFromNotification(request);
        });

      console.log('SignalR connected and subscribed for player:', pid);
    } catch (error) {
      console.error('Failed to connect SignalR:', error);
    } finally {
      this.isConnecting.set(false);
    }
  }

  /**
   * Disconnect from SignalR
   */
  private async disconnectSignalR(): Promise<void> {
    if (!this.signalRService.connectionState().connected) return;

    const pid = this.playerId();
    if (pid) {
      await this.signalRService.leavePlayerGroup(pid);
    }
    await this.signalRService.stopConnection();
  }

  protected openUploadDialog(): void {
    this.showUploadDialog.set(true);
  }

  protected closeUploadDialog(): void {
    this.showUploadDialog.set(false);
  }

  protected async onVideoUploaded(result: any): Promise<void> {
    const pid = this.playerId();
    if (!pid || !result.file) return;

    // Connect to SignalR for live updates before uploading
    await this.connectSignalR();

    this.analysisService
      .uploadVideo(result.file, pid, this.selectedStrokeType())
      .subscribe({
        next: () => {
          this.showUploadDialog.set(false);
          console.log('Video uploaded, waiting for SignalR updates...');
        },
        error: (err) => {
          console.error('Upload failed:', err);
        },
      });
  }

  protected getStatusIcon(status: AnalysisStatus): string {
    switch (status) {
      case AnalysisStatus.Requested:
        return 'schedule';
      case AnalysisStatus.InProgress:
        return 'hourglass_empty';
      case AnalysisStatus.Completed:
        return 'check_circle';
      case AnalysisStatus.Failed:
        return 'error';
      default:
        return 'help';
    }
  }

  protected getStatusLabel(status: AnalysisStatus): string {
    switch (status) {
      case AnalysisStatus.Requested:
        return 'Queued';
      case AnalysisStatus.InProgress:
        return 'Analyzing';
      case AnalysisStatus.Completed:
        return 'Completed';
      case AnalysisStatus.Failed:
        return 'Failed';
      default:
        return 'Unknown';
    }
  }

  protected getStatusColor(status: AnalysisStatus): string {
    switch (status) {
      case AnalysisStatus.Requested:
        return 'text-blue-500';
      case AnalysisStatus.InProgress:
        return 'text-yellow-500';
      case AnalysisStatus.Completed:
        return 'text-green-500';
      case AnalysisStatus.Failed:
        return 'text-red-500';
      default:
        return 'text-gray-500';
    }
  }

  protected getScoreColor(score: number | undefined): string {
    if (!score) return 'text-gray-400';
    if (score >= 80) return 'text-green-500';
    if (score >= 60) return 'text-yellow-500';
    if (score >= 40) return 'text-orange-500';
    return 'text-red-500';
  }

  protected getStrokeTypeLabel(strokeType: SwingType): string {
    return (
      this.strokeTypeOptions.find((o) => o.value === strokeType)?.label ??
      'Unknown'
    );
  }

  protected formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  protected deleteRequest(requestId: number): void {
    this.analysisService.deleteRequest(requestId).subscribe({
      error: (err) => console.error('Failed to delete request:', err),
    });
  }
}
