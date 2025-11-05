import { Component, inject } from '@angular/core';
import { MatIcon, MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
  selector: 'breakpoint-logo',
  standalone: true,
  imports: [MatIcon],
  template: `
    <h1 class="ml-2 flex items-center">
      <!-- todo: make icon bigger -->
      <mat-icon
        svgIcon="breakpoint-logo"
        aria-hidden="false"
        aria-label="BreakPoint.AI"
        class="mr-2"
      ></mat-icon>
      {{ title }}
    </h1>
  `,
})
export class BreakPointLogoComponent {
  protected title = 'BreakPoint.AI';
  private iconRegistry = inject(MatIconRegistry);
  private sanitizer = inject(DomSanitizer);

  constructor() {
    this.iconRegistry.addSvgIcon(
      'breakpoint-logo',
      this.sanitizer.bypassSecurityTrustResourceUrl('logo.svg')
    );
  }
}
