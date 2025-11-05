import { Component, HostListener, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
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
export class AppComponent implements OnInit {
  protected isSidenavExpanded: boolean = false;
  protected isMobile: boolean = false;
  protected isSignedIn: boolean = false;
  protected playerId: number | null = null;

  public ngOnInit(): void {
    this.CheckScreenSize();
  }

  @HostListener('window:resize', ['$event'])
  public onWindowResize(event: Event) {
    this.CheckScreenSize();
  }

  protected SignIn() {
    this.isSignedIn = true;
    this.playerId = 1;
  }

  private CheckScreenSize() {
    this.isMobile = window.innerWidth < 768;
  }
}
