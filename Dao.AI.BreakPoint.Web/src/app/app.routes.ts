import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { PlayerProfileComponent } from './pages/player-profile/player-profile.component';

export const routes: Routes = [
  // { path: '', component: HomeComponent },
  { path: '', component: PlayerProfileComponent },
  { path: 'home', redirectTo: '' },
  // TODO: put your page as the home for signed-in users
  { path: 'profile/:id', component: PlayerProfileComponent },
  // TODO: Add in anonymous home page
  // TODO: add in login handling
  // TODO: add in a dashboard page once logged in
  // TODO: add in a profile page
  // TODO: add in a settings page
  // TODO: add in an error handling page
];
