import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterModule } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';
import { PlayerService } from '../../core/services/player.service';
import { ToastService } from '../../core/services/toast.service';

interface UstaRatingForm {
  ustaRating: FormControl<number | null>;
}

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly playerService = inject(PlayerService);
  private readonly toastService = inject(ToastService);

  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly currentRating = signal<number | null>(null);

  protected readonly ustaRatingInfoUrl =
    'https://www.usta.com/en/home/coach-organize/tennis-tool-center/run-usta-programs/national/understanding-ntrp-ratings.html';

  protected readonly ratingForm = this.fb.group<UstaRatingForm>({
    ustaRating: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(1),
      Validators.max(7),
    ]),
  });

  protected get playerId(): number | undefined {
    return this.authService.userInfo()?.playerId ?? undefined;
  }

  protected get playerName(): string {
    return this.authService.userInfo()?.displayName || 'Player';
  }

  ngOnInit(): void {
    this.loadCurrentRating();
  }

  private loadCurrentRating(): void {
    const pid = this.playerId;
    if (!pid) {
      this.isLoading.set(false);
      return;
    }

    this.playerService.getPlayerWithStatsById(pid).subscribe({
      next: (player) => {
        this.currentRating.set(player.estimatedRating);
        if (player.estimatedRating) {
          this.ratingForm.patchValue({ ustaRating: player.estimatedRating });
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load player data:', err);
        this.isLoading.set(false);
      },
    });
  }

  protected onSubmit(): void {
    if (this.ratingForm.invalid) {
      this.ratingForm.markAllAsTouched();
      return;
    }

    const pid = this.playerId;
    if (!pid) {
      this.toastService.error('Unable to identify player. Please try again.');
      return;
    }

    this.isSaving.set(true);
    const newRating = this.ratingForm.getRawValue().ustaRating;
    if (newRating === null) {
      this.toastService.error('Please enter a valid rating.');
      this.isSaving.set(false);
      return;
    }

    this.playerService.updateUstaRating(pid, newRating).subscribe({
      next: () => {
        this.currentRating.set(newRating);
        this.toastService.success('USTA rating updated successfully!');
        this.isSaving.set(false);
      },
      error: (err) => {
        console.error('Failed to update rating:', err);
        this.toastService.error('Failed to update rating. Please try again.');
        this.isSaving.set(false);
      },
    });
  }
}
