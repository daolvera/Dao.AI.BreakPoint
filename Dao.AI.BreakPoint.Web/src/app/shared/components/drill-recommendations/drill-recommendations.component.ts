import { CommonModule } from '@angular/common';
import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';

import { DrillRecommendationDto } from '../../../core/models/dtos/analysis.dto';
import {
  SwingPhase,
  SwingPhaseLabels,
} from '../../../core/models/enums/swing-phase.enum';
import { DrillService } from '../../../core/services/drill.service';

@Component({
  selector: 'app-drill-recommendations',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule,
  ],
  templateUrl: './drill-recommendations.component.html',
  styleUrl: './drill-recommendations.component.scss',
})
export class DrillRecommendationsComponent {
  private drillService = inject(DrillService);

  /** List of drill recommendations to display */
  drills = input.required<DrillRecommendationDto[]>();

  /** Emitted when a drill is updated (completed, feedback, dismissed) */
  drillUpdated = output<DrillRecommendationDto>();

  isLoading = signal(false);
  feedbackDrill = signal<DrillRecommendationDto | null>(null);
  feedbackText = '';

  getPhaseLabel(phase: SwingPhase): string {
    return SwingPhaseLabels[phase] || 'Unknown';
  }

  markComplete(drill: DrillRecommendationDto): void {
    this.isLoading.set(true);
    this.drillService.completeDrill(drill.id).subscribe({
      next: (updated) => {
        this.drillUpdated.emit(updated);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  submitFeedback(drill: DrillRecommendationDto, thumbsUp: boolean): void {
    this.isLoading.set(true);
    this.drillService
      .submitFeedback(drill.id, { thumbsUp, feedbackText: undefined })
      .subscribe({
        next: (updated) => {
          this.drillUpdated.emit(updated);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  openFeedbackDialog(drill: DrillRecommendationDto): void {
    this.feedbackDrill.set(drill);
    this.feedbackText = '';
  }

  cancelFeedback(): void {
    this.feedbackDrill.set(null);
    this.feedbackText = '';
  }

  submitNegativeFeedback(): void {
    const drill = this.feedbackDrill();
    if (!drill) return;

    this.isLoading.set(true);
    this.drillService
      .submitFeedback(drill.id, {
        thumbsUp: false,
        feedbackText: this.feedbackText || undefined,
      })
      .subscribe({
        next: (updated) => {
          this.drillUpdated.emit(updated);
          this.feedbackDrill.set(null);
          this.feedbackText = '';
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  dismissDrill(drill: DrillRecommendationDto): void {
    if (!confirm('Dismiss this drill recommendation?')) return;

    this.isLoading.set(true);
    this.drillService.dismissDrill(drill.id).subscribe({
      next: () => {
        // Emit with isActive = false to signal removal
        this.drillUpdated.emit({ ...drill, isActive: false });
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }
}
