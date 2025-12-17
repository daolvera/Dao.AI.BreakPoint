import { Component, inject, input, signal, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { VideoUploadService } from '../../../core/services/video-upload.service';
import { ToastService } from '../../../core/services/toast.service';
import { VideoUploadResult } from '../../../core/models/dtos';

@Component({
  selector: 'app-video-upload',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="video-upload-container">
      <div
        class="upload-area"
        [class.drag-over]="isDragOver()"
        (dragover)="onDragOver($event)"
        (dragleave)="onDragLeave($event)"
        (drop)="onDrop($event)"
      >
        @if (isUploading()) {
        <div class="upload-progress">
          <mat-progress-bar mode="indeterminate"></mat-progress-bar>
          <p>Uploading video...</p>
        </div>
        } @else if (uploadedVideo()) {
        <div class="upload-success">
          <mat-icon>check_circle</mat-icon>
          <p>Video uploaded successfully!</p>
          <button mat-button color="primary" (click)="resetUpload()">
            Upload Another
          </button>
        </div>
        } @else {
        <div class="upload-prompt">
          <mat-icon>cloud_upload</mat-icon>
          <h3>Upload Tennis Swing Video</h3>
          <p>Drag and drop your video here or click to select</p>
          <p class="upload-requirements">
            • Supported formats: MP4, WebM, OGG, MOV, AVI<br />
            • Maximum duration: 30 seconds<br />
            • Maximum size: 50MB
          </p>

          <input
            #fileInput
            type="file"
            accept="video/*"
            (change)="onFileSelected($event)"
            style="display: none;"
          />

          <button mat-raised-button color="primary" (click)="fileInput.click()">
            <mat-icon>add</mat-icon>
            Select Video
          </button>
        </div>
        }
      </div>

      @if (selectedFile() && !isUploading()) {
      <div class="file-info">
        <h4>Selected File:</h4>
        <p>{{ selectedFile()?.name }}</p>
        <p>Size: {{ formatFileSize(selectedFile()?.size || 0) }}</p>

        <div class="upload-actions">
          <button
            mat-raised-button
            color="primary"
            (click)="uploadVideo()"
            [disabled]="!selectedFile()"
          >
            <mat-icon>upload</mat-icon>
            Upload Video
          </button>

          <button mat-button (click)="clearSelection()">Cancel</button>
        </div>
      </div>
      }
    </div>
  `,
  styleUrl: './video-upload.component.scss',
})
export class VideoUploadComponent {
  // Inputs
  playerId = input.required<number>();

  // Outputs
  videoUploaded = output<VideoUploadResult>();

  // Services
  private videoUploadService = inject(VideoUploadService);
  private toastService = inject(ToastService);

  // Signals
  protected selectedFile = signal<File | null>(null);
  protected isUploading = signal(false);
  protected isDragOver = signal(false);
  protected uploadedVideo = signal<VideoUploadResult | null>(null);

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);

    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.handleFileSelection(files[0]);
    }
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.handleFileSelection(input.files[0]);
    }
  }

  private async handleFileSelection(file: File): Promise<void> {
    const validation = await this.videoUploadService.validateVideo(file);

    if (validation.isValid) {
      this.selectedFile.set(file);
    } else {
      this.toastService.error(validation.error || 'Invalid video file');
    }
  }

  protected async uploadVideo(): Promise<void> {
    const file = this.selectedFile();
    if (!file) return;

    this.isUploading.set(true);

    try {
      this.videoUploadService
        .uploadPlayerVideo(this.playerId(), file)
        .subscribe({
          next: (result: VideoUploadResult) => {
            if (result.success) {
              this.uploadedVideo.set(result);
              this.videoUploaded.emit(result);
              this.toastService.success('Video uploaded successfully!');
            }
          },
          error: (error: any) => {
            console.error('Upload error:', error);
          },
          complete: () => {
            this.isUploading.set(false);
          },
        });
    } catch (error) {
      this.isUploading.set(false);
      this.toastService.error('An error occurred during upload');
    }
  }

  protected clearSelection(): void {
    this.selectedFile.set(null);
  }

  protected resetUpload(): void {
    this.selectedFile.set(null);
    this.uploadedVideo.set(null);
  }

  protected formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
