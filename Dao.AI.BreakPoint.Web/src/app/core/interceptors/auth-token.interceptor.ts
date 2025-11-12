import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { switchMap, catchError, of } from 'rxjs';
import { TokenService } from '../services/token.service';
import { AuthService } from '../services/auth.service';

export const authTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenService = inject(TokenService);
  const authService = inject(AuthService);

  // Skip auth header for login requests
  if (req.url.includes('/auth/google') || req.url.includes('/auth/refresh')) {
    return next(req);
  }

  const accessToken = tokenService.getAccessToken();

  if (!accessToken) {
    return next(req);
  }

  // If token is expired, try to refresh
  if (tokenService.isTokenExpired()) {
    const refreshToken = tokenService.getRefreshToken();

    if (!refreshToken) {
      return next(req);
    }

    return authService.refreshToken(refreshToken).pipe(
      switchMap((response) => {
        tokenService.setTokens(
          response.accessToken,
          response.refreshToken,
          response.expiresAt
        );

        // Retry original request with new token
        const authReq = req.clone({
          headers: req.headers.set(
            'Authorization',
            `Bearer ${response.accessToken}`
          ),
        });

        return next(authReq);
      }),
      catchError(() => {
        // Refresh failed, clear tokens and proceed without auth
        tokenService.clearTokens();
        return next(req);
      })
    );
  }

  // Add current token to request
  const authReq = req.clone({
    headers: req.headers.set('Authorization', `Bearer ${accessToken}`),
  });

  return next(authReq);
};
