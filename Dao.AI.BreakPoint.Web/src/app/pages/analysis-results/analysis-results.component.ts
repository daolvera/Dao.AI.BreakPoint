import { CommonModule } from '@angular/common';
import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';

import {
  AnalysisRequestDto,
  AnalysisResultDto,
} from '../../core/models/dtos/analysis.dto';
import { AnalysisStatus } from '../../core/models/enums/analysis-status.enum';
import { SwingType } from '../../core/models/enums/swing-type.enum';
import { AnalysisService } from '../../core/services/analysis.service';
import { SignalRService } from '../../core/services/signalr.service';

@Component({
  selector: 'app-analysis-results',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatProgressBarModule,
  ],
  templateUrl: './analysis-results.component.html',
  styleUrl: './analysis-results.component.scss',
})
export class AnalysisResultsComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  protected analysisService = inject(AnalysisService);
  protected signalRService = inject(SignalRService);

  private destroy$ = new Subject<void>();
  private analysisRequestId: number = 0;

  // Data - use new service signals
  protected request = this.analysisService.currentRequest;
  protected result = this.analysisService.currentResult;
  protected isLoading = signal(true);
  protected error = signal<string | null>(null);

  // Computed
  protected isProcessing = computed(() => {
    const req = this.request();
    return (
      req?.status === AnalysisStatus.Requested ||
      req?.status === AnalysisStatus.InProgress
    );
  });

  protected hasResult = computed(() => {
    const res = this.result();
    return res !== null && res !== undefined;
  });

  protected sortedFeatures = computed(() => {
    const res = this.result();
    if (!res?.featureImportance) return [];
    return Object.entries(res.featureImportance)
      .sort(([, a], [, b]) => b - a)
      .slice(0, 5); // Top 5 features
  });

  // Constants for template
  protected AnalysisStatus = AnalysisStatus;
  protected SwingTypes = SwingType;

  ngOnInit(): void {
    this.analysisRequestId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.analysisRequestId || isNaN(this.analysisRequestId)) {
      this.router.navigate(['/dashboard']);
      return;
    }

    this.loadAnalysis();
    this.setupSignalR();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.analysisRequestId) {
      this.signalRService.leaveAnalysisGroup(this.analysisRequestId);
    }
  }

  private loadAnalysis(): void {
    this.isLoading.set(true);

    // Load both request and result (result may not exist yet)
    this.analysisService.getRequest(this.analysisRequestId).subscribe({
      next: () => {
        // Try to get the result if the request is completed
        this.analysisService.getResult(this.analysisRequestId).subscribe({
          next: () => this.isLoading.set(false),
          error: () => this.isLoading.set(false), // Result may not exist yet
        });
      },
      error: (err) => {
        this.error.set('Failed to load analysis');
        this.isLoading.set(false);
        console.error(err);
      },
    });
  }

  private async setupSignalR(): Promise<void> {
    await this.signalRService.startConnection();
    await this.signalRService.joinAnalysisGroup(this.analysisRequestId);

    this.signalRService.analysisCompleted$
      .pipe(takeUntil(this.destroy$))
      .subscribe((result: AnalysisResultDto) => {
        if (result.analysisRequestId === this.analysisRequestId) {
          this.analysisService.updateResultFromNotification(result);
        }
      });

    this.signalRService.analysisStatusChanged$
      .pipe(takeUntil(this.destroy$))
      .subscribe((request: AnalysisRequestDto) => {
        if (request.id === this.analysisRequestId) {
          this.analysisService.updateRequestFromNotification(request);
        }
      });

    this.signalRService.analysisFailed$
      .pipe(takeUntil(this.destroy$))
      .subscribe(({ analysisRequestId, errorMessage }) => {
        if (analysisRequestId === this.analysisRequestId) {
          this.error.set(errorMessage);
        }
      });
  }

  protected getScoreGrade(score: number): string {
    if (score >= 90) return 'Excellent';
    if (score >= 80) return 'Great';
    if (score >= 70) return 'Good';
    if (score >= 60) return 'Fair';
    if (score >= 50) return 'Needs Work';
    return 'Keep Practicing';
  }

  protected getScoreColor(score: number): string {
    if (score >= 80) return 'text-green-500';
    if (score >= 60) return 'text-yellow-500';
    if (score >= 40) return 'text-orange-500';
    return 'text-red-500';
  }

  protected getScoreBgColor(score: number): string {
    if (score >= 80) return 'bg-green-100';
    if (score >= 60) return 'bg-yellow-100';
    if (score >= 40) return 'bg-orange-100';
    return 'bg-red-100';
  }

  protected formatFeatureName(key: string): string {
    // Convert camelCase or snake_case to readable format
    return key
      .replace(/([A-Z])/g, ' $1')
      .replace(/_/g, ' ')
      .replace(/^\s/, '')
      .toLowerCase()
      .replace(/^./, (str) => str.toUpperCase());
  }

  protected getFeatureBarWidth(value: number): number {
    // Assuming values are 0-1 or percentages
    return Math.min(Math.max(value * 100, 5), 100);
  }

  protected deleteAnalysis(): void {
    if (confirm('Are you sure you want to delete this analysis?')) {
      this.analysisService.deleteRequest(this.analysisRequestId).subscribe({
        next: () => {
          this.router.navigate(['/dashboard']);
        },
        error: (err) => {
          console.error('Delete failed:', err);
        },
      });
    }
  }
}
