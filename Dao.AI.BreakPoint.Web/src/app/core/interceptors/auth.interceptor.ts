import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);
  const authReq = req.clone({
    withCredentials: true,
  });
  return next(authReq).pipe(
    catchError((err) => {
      if (err.status === 401) {
        return authService.refreshToken().pipe(
          switchMap(() => {
            return next(authReq);
          }),
          catchError(() => {
            // navigate to home
            router.navigate(['/']);
            return throwError(() => err);
          })
        );
      }

      return throwError(() => err);
    })
  );
};
