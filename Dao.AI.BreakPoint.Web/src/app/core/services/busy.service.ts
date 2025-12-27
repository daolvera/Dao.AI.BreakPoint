import { Injectable, signal, computed } from '@angular/core';

/**
 * Service for managing application-wide loading/busy state.
 * Tracks the number of active HTTP requests and exposes a loading signal.
 */
@Injectable({
  providedIn: 'root',
})
export class BusyService {
  private readonly activeRequests = signal(0);

  /** Whether there are any active HTTP requests */
  public readonly isLoading = computed(() => this.activeRequests() > 0);

  /**
   * Increment the active request count.
   * Call this when an HTTP request starts.
   */
  public startRequest(): void {
    this.activeRequests.update((count) => count + 1);
  }

  /**
   * Decrement the active request count.
   * Call this when an HTTP request completes (success or error).
   */
  public stopRequest(): void {
    this.activeRequests.update((count) => Math.max(0, count - 1));
  }
}
