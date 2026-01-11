import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { AnalysisResultsComponent } from './pages/analysis-results/analysis-results.component';
import { CompleteProfileComponent } from './pages/complete-profile/complete-profile.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { HomeComponent } from './pages/home/home.component';
import { SettingsComponent } from './pages/settings/settings.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'home', redirectTo: '' },
  {
    path: 'auth/complete',
    component: CompleteProfileComponent,
    canActivate: [authGuard],
  },
  {
    path: 'profile',
    redirectTo: 'dashboard',
  },
  {
    path: 'dashboard',
    component: DashboardComponent,
    canActivate: [authGuard],
  },
  {
    path: 'settings',
    component: SettingsComponent,
    canActivate: [authGuard],
  },
  {
    path: 'analysis/:id',
    component: AnalysisResultsComponent,
    canActivate: [authGuard],
  },
  // TODO: Add in anonymous home page
  // TODO: add in login handling
  // TODO: add in an error handling page
];
