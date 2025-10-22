import { Component, OnInit } from '@angular/core';
import { RouterModule, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { BreakPointLogoComponent } from './break-point-logo/break-point-logo.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    MatToolbarModule,
    MatButtonModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    RouterModule,
    BreakPointLogoComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  protected title = 'BreakPoint.AI';
  protected isSidenavExpanded: boolean = false;

  constructor(private http: HttpClient) {
    http.get<HealthCheck>('api/health').subscribe({
      next: (result) => console.log('Health Check:', result),
      error: console.error,
    });
  }
}

export interface HealthCheck {
  status: string;
  timestamp: string;
}
