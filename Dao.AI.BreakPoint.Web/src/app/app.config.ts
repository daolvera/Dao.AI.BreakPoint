import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient()
    // add in a busy spinner on http requests
    // add in a toast service
    // add in a default error handler using a red toast with an error occurred
    // add in a logging service like AppInsights
    // add in modal service from angular material
    // add in auth stuff
  ]
};
