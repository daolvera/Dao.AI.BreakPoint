import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { authTokenInterceptor } from './core/interceptors/auth-token.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authTokenInterceptor])),
    provideAnimationsAsync(),
    // TODO: add in a busy spinner on http requests
    // TODO: add in a toast service
    // TODO: add in a default error handler using a red toast with an error occurred
    // TODO: add in a logging service like AppInsights
    // TODO: add in modal service from angular material
  ],
};
