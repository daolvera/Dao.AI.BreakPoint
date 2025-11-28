import { Injectable, inject, signal } from '@angular/core';
import {
  HttpClient,
  HttpEventType,
  HttpProgressEvent,
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { VideoUploadResult, VideoValidationResult } from '../models/dtos';

@Injectable({
  providedIn: 'root',
})
export class VideoUploadService {
  private http = inject(HttpClient);

  // Allowed video types
  private readonly allowedVideoTypes = [
    'video/mp4',
    'video/webm',
    'video/ogg',
    'video/quicktime',
    'video/x-msvideo', // .avi
  ];

  // Maximum file size (50MB)
  private readonly maxFileSize = 50 * 1024 * 1024;

  // Maximum duration (30 seconds)
  private readonly maxDuration = 30;

  /**
   * Validates a video file for type, size, and duration
   */
  public validateVideo(file: File): Promise<VideoValidationResult> {
    return new Promise((resolve) => {
      // Check file type
      if (!this.allowedVideoTypes.includes(file.type)) {
        resolve({
          isValid: false,
          error: `Invalid video type. Allowed types: ${this.allowedVideoTypes.join(
            ', '
          )}`,
        });
        return;
      }

      // Check file size
      if (file.size > this.maxFileSize) {
        resolve({
          isValid: false,
          error: `File size too large. Maximum size is ${Math.round(
            this.maxFileSize / (1024 * 1024)
          )}MB`,
        });
        return;
      }

      // Check video duration
      const video = document.createElement('video');
      video.preload = 'metadata';

      video.onloadedmetadata = () => {
        URL.revokeObjectURL(video.src);

        if (video.duration > this.maxDuration) {
          resolve({
            isValid: false,
            error: `Video duration too long. Maximum duration is ${this.maxDuration} seconds`,
          });
        } else {
          resolve({ isValid: true });
        }
      };

      video.onerror = () => {
        URL.revokeObjectURL(video.src);
        resolve({
          isValid: false,
          error:
            'Unable to read video metadata. Please ensure the file is a valid video.',
        });
      };

      video.src = URL.createObjectURL(file);
    });
  }

  /**
   * Uploads a video file for a player
   */
  public uploadPlayerVideo(
    playerId: number,
    file: File
  ): Observable<VideoUploadResult> {
    const formData = new FormData();
    formData.append('video', file);
    formData.append('playerId', playerId.toString());

    return this.http
      .post<VideoUploadResult>(`api/players/${playerId}/videos`, formData, {
        reportProgress: true,
        observe: 'events',
      })
      .pipe(
        map((event) => {
          if (event.type === HttpEventType.Response) {
            return event.body as VideoUploadResult;
          }
          // For progress events, we can extend this later
          return { success: false, message: 'Uploading...' };
        }),
        catchError((error) => {
          console.error('Video upload error:', error);
          return throwError(() => ({
            success: false,
            message: 'Failed to upload video. Please try again.',
          }));
        })
      );
  }

  /**
   * Gets upload progress (can be extended for real-time progress tracking)
   */
  public getUploadProgress(): Observable<number> {
    // Placeholder for upload progress implementation
    return new Observable((observer) => {
      observer.next(0);
      observer.complete();
    });
  }
}
