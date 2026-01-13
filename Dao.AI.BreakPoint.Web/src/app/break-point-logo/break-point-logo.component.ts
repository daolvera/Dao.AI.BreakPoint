import { Component, Input } from '@angular/core';

@Component({
  selector: 'breakpoint-logo',
  standalone: true,
  templateUrl: './break-point-logo.component.html',
  styleUrl: './break-point-logo.component.scss',
})
export class BreakPointLogoComponent {
  @Input() iconOnly = false;
  protected title = 'BreakPoint.AI';
}
