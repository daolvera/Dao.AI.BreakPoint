import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { PlayerProfileComponent } from './pages/player-profile/player-profile.component';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'home', redirectTo: '' },
  {
    path: 'profile',
    component: PlayerProfileComponent,
    canActivate: [authGuard],
  },
  // TODO: Add in anonymous home page
  // TODO: add in login handling
  // TODO: add in a dashboard page once logged in
  // TODO: add in a profile page
  // TODO: add in a settings page
  // TODO: add in an error handling page
];
