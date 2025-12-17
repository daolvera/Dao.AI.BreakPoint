import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { CompleteProfileForm } from '../../core/models/forms/complete-profile.form';

@Component({
  selector: 'app-complete-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatCardModule,
  ],
  templateUrl: './complete-profile.component.html',
  styleUrl: './complete-profile.component.scss',
})
export class CompleteProfileComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly toastService = inject(ToastService);

  protected readonly isSubmitting = signal(false);

  protected readonly profileForm: FormGroup<CompleteProfileForm> =
    this.fb.group({
      name: this.fb.nonNullable.control('', [
        Validators.required,
        Validators.minLength(2),
        Validators.maxLength(100),
      ]),
      ustaRating: this.fb.nonNullable.control<number | null>(null, [
        Validators.required,
        Validators.min(1),
        Validators.max(7),
      ]),
    });

  protected readonly ustaRatingInfoUrl =
    'https://www.usta.com/en/home/coach-organize/tennis-tool-center/run-usta-programs/national/understanding-ntrp-ratings.html';

  protected onSubmit(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);

    const formValue = this.profileForm.getRawValue();
    this.authService
      .completeProfile({
        name: formValue.name,
        ustaRating: formValue.ustaRating!,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.toastService.success('Profile completed successfully!');
        },
        error: (err) => {
          console.error('Error completing profile:', err);
          this.toastService.error(
            'Failed to complete profile. Please try again.'
          );
          this.isSubmitting.set(false);
        },
      });
  }
}
