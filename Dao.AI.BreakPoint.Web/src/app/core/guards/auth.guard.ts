import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, map, take } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    // Redirect to home page if not authenticated
    router.navigate(['/']);
    return false;
  }

  // If user info is not loaded yet, load it and wait
  if (authService.userInfo() === null) {
    authService.loadUserInfo();

    // Wait for userInfo to be loaded (non-null)
    return toObservable(authService.userInfo).pipe(
      filter((user) => user !== null),
      take(1),
      map((user) => {
        if (
          !user.isProfileComplete &&
          route.routeConfig?.path !== 'auth/complete'
        ) {
          router.navigate(['/auth/complete']);
        }
        return true;
      })
    );
  }

  // User info is already loaded, check profile completion
  const user = authService.userInfo();
  if (!user?.isProfileComplete && route.routeConfig?.path !== 'auth/complete') {
    router.navigate(['/auth/complete']);
  }

  return true;
};
