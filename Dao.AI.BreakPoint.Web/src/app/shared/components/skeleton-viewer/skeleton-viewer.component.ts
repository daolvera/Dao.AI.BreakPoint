import { CommonModule } from '@angular/common';
import { Component, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-skeleton-viewer',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="skeleton-viewer">
      @if (imageUrl() || gifUrl()) {
      <div class="viewer-container relative">
        <!-- Static Image / Key Frame -->
        @if (!showGif() && imageUrl()) {
        <img
          [src]="imageUrl()"
          [alt]="altText()"
          class="skeleton-image rounded-lg shadow-md w-full h-auto"
        />
        }

        <!-- Animated GIF -->
        @if (showGif() && gifUrl()) {
        <img
          [src]="gifUrl()"
          [alt]="altText() + ' Animation'"
          class="skeleton-gif rounded-lg shadow-md w-full h-auto"
        />
        }

        <!-- Toggle button -->
        @if (imageUrl() && gifUrl()) {
        <button
          mat-mini-fab
          color="primary"
          class="toggle-btn absolute bottom-4 right-4"
          (click)="toggleView()"
          [matTooltip]="showGif() ? 'Show Key Frame' : 'Play Animation'"
        >
          <mat-icon>{{ showGif() ? 'photo' : 'play_arrow' }}</mat-icon>
        </button>
        }
      </div>

      <!-- Caption -->
      <p class="text-sm text-gray-500 mt-2 text-center">
        {{ showGif() ? 'Full Swing Animation' : 'Key Frame Analysis' }}
        @if (imageUrl() && gifUrl()) {
        <span class="text-xs">(Click button to toggle)</span>
        }
      </p>
      } @else {
      <div
        class="placeholder flex items-center justify-center bg-gray-100 rounded-lg p-8"
      >
        <div class="text-center text-gray-500">
          <mat-icon class="text-4xl mb-2">image</mat-icon>
          <p>{{ placeholderText() }}</p>
        </div>
      </div>
      }
    </div>
  `,
  styles: [
    `
      .skeleton-viewer {
        .viewer-container {
          position: relative;

          img {
            max-height: 400px;
            object-fit: contain;
          }

          .toggle-btn {
            opacity: 0.9;
            transition: opacity 0.2s;

            &:hover {
              opacity: 1;
            }
          }
        }

        .placeholder {
          min-height: 200px;
        }
      }
    `,
  ],
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
