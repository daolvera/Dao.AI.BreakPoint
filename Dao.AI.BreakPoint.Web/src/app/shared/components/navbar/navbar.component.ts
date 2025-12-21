import { CommonModule } from '@angular/common';
import { Component, HostListener, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterModule } from '@angular/router';
import { BreakPointLogoComponent } from '../../../break-point-logo/break-point-logo.component';
import { AuthService } from '../../../core/services/auth.service';

interface NavItem {
  icon: string;
  label: string;
  route: string;
  requiresAuth: boolean;
}

@Component({
  selector: 'breakpoint-navbar',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatMenuModule,
    MatSidenavModule,
    BreakPointLogoComponent,
  ],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.scss',
})
export class NavbarComponent {
  protected readonly authService = inject(AuthService);
  protected readonly isExpanded = signal(false);
  protected readonly isMobile = signal(false);
  protected readonly isMobileDrawerOpen = signal(false);

  protected readonly navItems: NavItem[] = [
    {
      icon: 'dashboard',
      label: 'Dashboard',
      route: '/dashboard',
      requiresAuth: true,
    },
  ];

  constructor() {
    this.checkScreenSize();
  }

  @HostListener('window:resize')
  public onResize(): void {
    this.checkScreenSize();
  }

  protected toggleSidebar(): void {
    this.isExpanded.update((expanded) => !expanded);
  }

  protected toggleMobileDrawer(): void {
    this.isMobileDrawerOpen.update((open) => !open);
  }

  protected closeMobileDrawer(): void {
    this.isMobileDrawerOpen.set(false);
  }

  protected get visibleNavItems(): NavItem[] {
    return this.navItems.filter(
      (item) => !item.requiresAuth || this.authService.userInfo()
    );
  }

  private checkScreenSize(): void {
    this.isMobile.set(window.innerWidth < 768);
    if (this.isMobile()) {
      this.isExpanded.set(false);
    }
    if (!this.isMobile()) {
      this.isMobileDrawerOpen.set(false);
    }
  }
}
