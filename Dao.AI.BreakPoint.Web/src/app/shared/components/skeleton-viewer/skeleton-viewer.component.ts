import { CommonModule } from '@angular/common';
import { Component, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-skeleton-viewer',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './skeleton-viewer.component.html',
  styleUrl: './skeleton-viewer.component.scss',
})
export class SkeletonViewerComponent {
  /** URL of the static skeleton overlay image (key frame) */
  imageUrl = input<string | undefined>();

  /** URL of the animated skeleton overlay GIF */
  gifUrl = input<string | undefined>();

  /** Alt text for accessibility */
  altText = input('Skeleton overlay showing pose analysis');

  /** Text to show when no image is available */
  placeholderText = input('Skeleton overlay not available');

  /** Whether to show the GIF animation */
  showGif = signal(false);

  toggleView(): void {
    this.showGif.update((v) => !v);
  }
}
