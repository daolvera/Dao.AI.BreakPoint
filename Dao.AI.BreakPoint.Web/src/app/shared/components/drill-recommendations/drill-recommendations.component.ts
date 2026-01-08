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
  template: `
    <div class="drill-recommendations">
      <h3
        class="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2"
      >
        <mat-icon class="text-primary-500">fitness_center</mat-icon>
        Recommended Drills
      </h3>

      @if (drills().length === 0) {
      <p class="text-gray-500 text-center py-4">
        No drill recommendations available yet.
      </p>
      } @else {
      <div class="space-y-4">
        @for (drill of drills(); track drill.id) {
        <mat-card class="drill-card" [class.completed]="drill.completedAt">
          <mat-card-content class="p-4">
            <!-- Header -->
            <div class="flex items-start justify-between mb-2">
              <div class="flex items-center gap-2">
                <span
                  class="priority-badge"
                  [class]="'priority-' + drill.priority"
                >
                  {{ drill.priority }}
                </span>
                <h4 class="font-semibold text-gray-900">
                  {{ drill.drillName }}
                </h4>
              </div>
              <mat-chip-set>
                <mat-chip [highlighted]="true" class="phase-chip">
                  {{ getPhaseLabel(drill.targetPhase) }}
                </mat-chip>
              </mat-chip-set>
            </div>

            <!-- Description -->
            <p class="text-gray-700 mb-3">{{ drill.description }}</p>

            <!-- Target and Duration -->
            <div class="flex flex-wrap gap-4 text-sm text-gray-500 mb-3">
              <span class="flex items-center gap-1">
                <mat-icon class="text-sm">track_changes</mat-icon>
                {{ drill.targetFeature }}
              </span>
              @if (drill.suggestedDuration) {
              <span class="flex items-center gap-1">
                <mat-icon class="text-sm">schedule</mat-icon>
                {{ drill.suggestedDuration }}
              </span>
              }
            </div>

            <!-- Actions -->
            <div class="flex items-center gap-2 flex-wrap">
              @if (!drill.completedAt) {
              <button
                mat-stroked-button
                color="primary"
                (click)="markComplete(drill)"
                [disabled]="isLoading()"
              >
                <mat-icon>check</mat-icon>
                Mark Complete
              </button>
              } @else {
              <span class="text-green-600 flex items-center gap-1 text-sm">
                <mat-icon class="text-sm">check_circle</mat-icon>
                Completed {{ drill.completedAt | date : 'shortDate' }}
              </span>
              }

              <!-- Feedback buttons -->
              @if (drill.completedAt && drill.thumbsUp === undefined) {
              <div class="flex items-center gap-1 ml-auto">
                <span class="text-sm text-gray-500">Was this helpful?</span>
                <button
                  mat-icon-button
                  color="primary"
                  (click)="submitFeedback(drill, true)"
                  [disabled]="isLoading()"
                  matTooltip="Yes, helpful"
                >
                  <mat-icon>thumb_up</mat-icon>
                </button>
                <button
                  mat-icon-button
                  color="warn"
                  (click)="openFeedbackDialog(drill)"
                  [disabled]="isLoading()"
                  matTooltip="Not helpful"
                >
                  <mat-icon>thumb_down</mat-icon>
                </button>
              </div>
              } @if (drill.thumbsUp !== undefined) {
              <span
                class="ml-auto text-sm flex items-center gap-1"
                [class]="drill.thumbsUp ? 'text-green-600' : 'text-orange-600'"
              >
                <mat-icon class="text-sm">
                  {{ drill.thumbsUp ? 'thumb_up' : 'thumb_down' }}
                </mat-icon>
                {{ drill.thumbsUp ? 'Helpful' : 'Not helpful' }}
              </span>
              }

              <button
                mat-icon-button
                color="warn"
                (click)="dismissDrill(drill)"
                [disabled]="isLoading()"
                matTooltip="Dismiss"
                class="ml-auto"
              >
                <mat-icon>close</mat-icon>
              </button>
            </div>

            <!-- Feedback input (shown when giving negative feedback) -->
            @if (feedbackDrill()?.id === drill.id) {
            <div class="feedback-input mt-3 p-3 bg-gray-50 rounded">
              <mat-form-field appearance="outline" class="w-full">
                <mat-label>What could be improved?</mat-label>
                <input
                  matInput
                  [(ngModel)]="feedbackText"
                  placeholder="Optional feedback..."
                />
              </mat-form-field>
              <div class="flex gap-2 justify-end">
                <button mat-button (click)="cancelFeedback()">Cancel</button>
                <button
                  mat-raised-button
                  color="primary"
                  (click)="submitNegativeFeedback()"
                >
                  Submit
                </button>
              </div>
            </div>
            }
          </mat-card-content>
        </mat-card>
        }
      </div>
      }
    </div>
  `,
  styles: [
    `
      .drill-recommendations {
        .drill-card {
          transition: all 0.2s;

          &.completed {
            opacity: 0.8;
            background-color: #f9fafb;
          }

          &:hover {
            box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1);
          }
        }

        .priority-badge {
          display: flex;
          align-items: center;
          justify-content: center;
          width: 24px;
          height: 24px;
          border-radius: 50%;
          font-size: 12px;
          font-weight: bold;
          color: white;

          &.priority-1 {
            background-color: #ef4444;
          }
          &.priority-2 {
            background-color: #f97316;
          }
          &.priority-3 {
            background-color: #eab308;
          }
        }

        .phase-chip {
          font-size: 11px;
        }

        mat-icon.text-sm {
          font-size: 16px;
          width: 16px;
          height: 16px;
        }
      }
    `,
  ],
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
