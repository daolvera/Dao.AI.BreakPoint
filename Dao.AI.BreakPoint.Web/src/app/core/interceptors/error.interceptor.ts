import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../services/toast.service';

/**
 * HTTP interceptor that catches unhandled server errors and displays
 * a user-friendly error toast notification.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastService = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Skip 401 errors as they are handled by the auth interceptor
      if (error.status === 401) {
        return throwError(() => error);
      }

      // Display user-friendly error message based on status code
      const message = getErrorMessage(error);
      toastService.error(message);

      return throwError(() => error);
    })
  );
};

/**
 * Get a user-friendly error message based on the HTTP error response.
 * @param error The HTTP error response
 * @returns A user-friendly error message
 */
function getErrorMessage(error: HttpErrorResponse): string {
  // Check if the server sent a custom error message
  if (error.error?.message && typeof error.error.message === 'string') {
    return error.error.message;
  }

  // Check for .NET ProblemDetails format
  if (error.error?.title && typeof error.error.title === 'string') {
    return error.error.title;
  }

  // Check for .NET exception detail (only in development)
  if (error.error?.detail && typeof error.error.detail === 'string') {
    // Return generic message for security, don't expose exception details to users
    return 'An unexpected error occurred. Please try again later.';
  }

  // Return message based on status code
  switch (error.status) {
    case 0:
      return 'Unable to connect to the server. Please check your internet connection.';
    case 400:
      return 'Invalid request. Please check your input and try again.';
    case 403:
      return 'You do not have permission to perform this action.';
    case 404:
      return 'The requested resource was not found.';
    case 408:
      return 'The request timed out. Please try again.';
    case 409:
      return 'A conflict occurred. Please refresh and try again.';
    case 422:
      return 'The request could not be processed. Please check your input.';
    case 429:
      return 'Too many requests. Please wait a moment and try again.';
    case 500:
      return 'An unexpected error occurred. Please try again later.';
    case 502:
      return 'The server is temporarily unavailable. Please try again later.';
    case 503:
      return 'The service is temporarily unavailable. Please try again later.';
    case 504:
      return 'The server took too long to respond. Please try again.';
    default:
      return 'Something went wrong. Please try again later.';
  }
}
