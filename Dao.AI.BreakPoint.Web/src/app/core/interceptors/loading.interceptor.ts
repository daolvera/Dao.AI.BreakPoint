import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { BusyService } from '../services/busy.service';

/**
 * HTTP interceptor that tracks active requests and updates the BusyService.
 * Shows a loading spinner when one or more HTTP requests are in progress.
 */
export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const busyService = inject(BusyService);

  busyService.startRequest();

  return next(req).pipe(
    finalize(() => {
      busyService.stopRequest();
    })
  );
};
