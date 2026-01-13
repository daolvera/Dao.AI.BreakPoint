import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-phase-score-card',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  templateUrl: './phase-score-card.component.html',
  styleUrl: './phase-score-card.component.scss',
})
export class PhaseScoreCardComponent {
  /** Phase label (e.g., "Preparation", "Backswing") */
  label = input.required<string>();

  /** Score value (0-100) */
  score = input.required<number>();

  /** Whether this is the worst phase (highlights for attention) */
  isWorst = input(false);

  getCardClasses(): string {
    const score = this.score();
    let gradeClass = 'poor';

    if (score >= 85) gradeClass = 'excellent';
    else if (score >= 70) gradeClass = 'good';
    else if (score >= 55) gradeClass = 'fair';
    else if (score >= 40) gradeClass = 'needs-work';

    return `${gradeClass} ${this.isWorst() ? 'is-worst' : ''}`;
  }

  getScoreColor(): string {
    const score = this.score();
    if (score >= 85) return 'text-green-600';
    if (score >= 70) return 'text-lime-600';
    if (score >= 55) return 'text-yellow-600';
    if (score >= 40) return 'text-orange-600';
    return 'text-red-600';
  }

  getIconColor(): string {
    const score = this.score();
    if (score >= 85) return 'text-green-500';
    if (score >= 70) return 'text-lime-500';
    if (score >= 55) return 'text-yellow-500';
    if (score >= 40) return 'text-orange-500';
    return 'text-red-500';
  }

  getIcon(): string {
    const score = this.score();
    if (score >= 85) return 'check_circle';
    if (score >= 70) return 'thumb_up';
    if (score >= 55) return 'info';
    if (score >= 40) return 'warning';
    return 'error';
  }

  getTooltip(): string {
    if (this.isWorst()) {
      return 'This phase needs the most attention';
    }
    return `${this.label()}: ${this.getGrade()}`;
  }

  getGrade(): string {
    const score = this.score();
    if (score >= 90) return 'Excellent';
    if (score >= 80) return 'Great';
    if (score >= 70) return 'Good';
    if (score >= 60) return 'Fair';
    if (score >= 50) return 'Needs Work';
    return 'Focus Here';
  }

  getProgressColor(): 'primary' | 'accent' | 'warn' {
    const score = this.score();
    if (score >= 70) return 'primary';
    if (score >= 50) return 'accent';
    return 'warn';
  }
}
