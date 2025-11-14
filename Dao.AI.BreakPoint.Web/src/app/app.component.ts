import { Component, HostListener, inject, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterModule } from '@angular/router';
import { BreakPointLogoComponent } from './break-point-logo/break-point-logo.component';
import { AuthService } from './core/services/auth.service';

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
export class AppComponent implements OnInit {
  protected isSidenavExpanded: boolean = false;
  protected isMobile: boolean = false;
  protected readonly authService = inject(AuthService);

  public ngOnInit(): void {
    this.CheckScreenSize();
    if (this.authService.isAuthenticated()) {
      this.authService.loadUserInfo();
    }
  }

  @HostListener('window:resize', ['$event'])
  public onWindowResize(event: Event) {
    this.CheckScreenSize();
  }

  private CheckScreenSize() {
    this.isMobile = window.innerWidth < 768;
  }
}
