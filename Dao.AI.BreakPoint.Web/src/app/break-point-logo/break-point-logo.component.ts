import { Component, inject, Input } from '@angular/core';
import { MatIcon, MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
  selector: 'breakpoint-logo',
  standalone: true,
  template: `
    <h2 class="flex items-center" [class.ml-1]="iconOnly">
      <img src="/breakpointlogo.png" alt="BreakPoint.AI Logo" class="h-6" />
      @if (!iconOnly) {
      {{ title }}
      }
    </h2>
  `,
})
export class BreakPointLogoComponent {
  @Input() iconOnly = false;
  protected title = 'BreakPoint.AI';
}
