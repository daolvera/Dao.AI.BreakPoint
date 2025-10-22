import { Component, inject } from '@angular/core';
import { MatIcon, MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
  selector: 'breakpoint-logo',
  standalone: true,
  imports: [MatIcon],
  template: `
    <mat-icon
      svgIcon="breakpoint-logo"
      aria-hidden="false"
      aria-label="BreakPoint.AI"
    ></mat-icon>
  `,
})
// TODO: Make the logo bigger
export class BreakPointLogoComponent {
  private iconRegistry = inject(MatIconRegistry);
  private sanitizer = inject(DomSanitizer);

  constructor() {
    this.iconRegistry.addSvgIcon(
      'breakpoint-logo',
      this.sanitizer.bypassSecurityTrustResourceUrl('logo.svg')
    );
  }
}
