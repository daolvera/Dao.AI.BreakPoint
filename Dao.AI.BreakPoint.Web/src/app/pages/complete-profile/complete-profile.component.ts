import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { Handedness } from '../../core/models/enums/handedness.enum';
import { CompleteProfileForm } from '../../core/models/forms/complete-profile.form';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';

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
    MatSelectModule,
  ],
  templateUrl: './complete-profile.component.html',
  styleUrl: './complete-profile.component.scss',
})
export class CompleteProfileComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly toastService = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly isSubmitting = signal(false);

  // Handedness options for template
  protected readonly Handedness = Handedness;
  protected readonly handednessOptions = Object.values(Handedness);

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
      handedness: this.fb.nonNullable.control<Handedness | null>(null, [
        Validators.required,
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
        handedness: formValue.handedness!,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.toastService.success('Profile completed successfully!');
          this.router.navigate(['/dashboard']);
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
