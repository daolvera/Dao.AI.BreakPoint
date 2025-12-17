import { Injectable, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

/**
 * Toast service for displaying snackbar notifications with different severity levels.
 * Uses Angular Material's MatSnackBar under the hood.
 */
@Injectable({
  providedIn: 'root',
})
export class ToastService {
  private readonly snackBar = inject(MatSnackBar);

  private readonly defaultConfig: MatSnackBarConfig = {
    duration: 5000,
    horizontalPosition: 'center',
    verticalPosition: 'bottom',
  };

  /**
   * Display a success toast notification
   * @param message The message to display
   * @param duration Optional duration in milliseconds (default: 5000)
   */
  success(message: string, duration?: number): void {
    this.show(message, 'success', duration);
  }

  /**
   * Display an error toast notification
   * @param message The message to display
   * @param duration Optional duration in milliseconds (default: 5000)
   */
  error(message: string, duration?: number): void {
    this.show(message, 'error', duration);
  }

  /**
   * Display an info toast notification
   * @param message The message to display
   * @param duration Optional duration in milliseconds (default: 5000)
   */
  info(message: string, duration?: number): void {
    this.show(message, 'info', duration);
  }

  /**
   * Display a warning toast notification
   * @param message The message to display
   * @param duration Optional duration in milliseconds (default: 5000)
   */
  warning(message: string, duration?: number): void {
    this.show(message, 'warning', duration);
  }

  /**
   * Display a toast notification
   * @param message The message to display
   * @param type The type of toast (success, error, info, warning)
   * @param duration Optional duration in milliseconds
   */
  private show(message: string, type: ToastType, duration?: number): void {
    const config: MatSnackBarConfig = {
      ...this.defaultConfig,
      panelClass: [`toast-${type}`],
      ...(duration && { duration }),
    };

    this.snackBar.open(message, 'Close', config);
  }

  /**
   * Dismiss the currently displayed toast
   */
  dismiss(): void {
    this.snackBar.dismiss();
  }
}
