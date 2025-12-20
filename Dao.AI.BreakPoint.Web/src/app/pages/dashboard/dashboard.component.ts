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
} from '../../core/models/dtos/analysis.dto';
import { AnalysisStatus } from '../../core/models/enums/analysis-status.enum';
import {
  SwingType,
  SwingTypeLabels,
} from '../../core/models/enums/swing-type.enum';
import { AnalysisService } from '../../core/services/analysis.service';
import { AuthService } from '../../core/services/auth.service';
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

  private destroy$ = new Subject<void>();

  // UI State
  protected showUploadDialog = signal(false);
  protected selectedStrokeType = signal<SwingType>(
    SwingType.ForehandGroundStroke
  );
  protected isConnecting = signal(true);

  // Data - use the new service signals
  protected pendingRequests = this.analysisService.pendingRequests;
  protected resultHistory = this.analysisService.resultHistory;
  protected isLoading = this.analysisService.isLoading;

  // Enums for template
  protected AnalysisStatus = AnalysisStatus;
  protected SwingType = SwingType;
  protected SwingTypeLabels = SwingTypeLabels;
  protected strokeTypes = Object.values(SwingType);

  // Computed values
  protected playerId = computed(() => this.authService.userInfo()?.playerId);

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
    this.initializeSignalR();
    this.loadAnalysisHistory();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    const pid = this.playerId();
    if (pid) {
      this.signalRService.leavePlayerGroup(pid);
    }
  }

  private async initializeSignalR(): Promise<void> {
    try {
      await this.signalRService.startConnection();

      const pid = this.playerId();
      if (pid) {
        await this.signalRService.joinPlayerGroup(pid);
      }

      // Subscribe to SignalR events
      this.signalRService.analysisCompleted$
        .pipe(takeUntil(this.destroy$))
        .subscribe((result: AnalysisResultDto) => {
          this.analysisService.updateResultFromNotification(result);
        });

      this.signalRService.analysisStatusChanged$
        .pipe(takeUntil(this.destroy$))
        .subscribe((request: AnalysisRequestDto) => {
          this.analysisService.updateRequestFromNotification(request);
        });
    } catch (error) {
      console.error('Failed to initialize SignalR:', error);
    } finally {
      this.isConnecting.set(false);
    }
  }

  private loadAnalysisHistory(): void {
    const pid = this.playerId();
    if (pid) {
      this.analysisService.getPendingRequests(pid).subscribe();
      this.analysisService.getResultHistory(pid).subscribe();
    }
  }

  protected openUploadDialog(): void {
    this.showUploadDialog.set(true);
  }

  protected closeUploadDialog(): void {
    this.showUploadDialog.set(false);
  }

  protected onVideoUploaded(result: any): void {
    // The video-upload component emits the result
    // Now we need to create the analysis
    const pid = this.playerId();
    if (pid && result.file) {
      this.analysisService
        .uploadVideo(result.file, pid, this.selectedStrokeType())
        .subscribe({
          next: () => {
            this.showUploadDialog.set(false);
          },
          error: (err) => {
            console.error('Upload failed:', err);
          },
        });
    }
  }

  protected getStatusIcon(status: AnalysisStatus): string {
    switch (status) {
      case AnalysisStatus.Requested:
        return 'schedule';
      case AnalysisStatus.InProgress:
        return 'pending';
      case AnalysisStatus.Completed:
        return 'check_circle';
      case AnalysisStatus.Failed:
        return 'error';
      default:
        return 'help';
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

  protected formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }
}
