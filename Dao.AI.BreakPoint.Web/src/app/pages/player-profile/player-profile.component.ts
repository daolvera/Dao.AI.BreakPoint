import { Component, inject, input, OnInit, signal } from '@angular/core';
import {
  PlayerService,
  PlayerVideoDto,
} from '../../core/services/player.service';
import { PlayerWithStatsDto, VideoUploadResult } from '../../core/models/dtos';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../core/services/auth.service';
import { VideoUploadComponent } from '../../shared/components/video-upload/video-upload.component';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-player-profile',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    VideoUploadComponent,
  ],
  templateUrl: './player-profile.component.html',
  styleUrl: './player-profile.component.scss',
})
export class PlayerProfileComponent implements OnInit {
  protected playerWithStats = signal<PlayerWithStatsDto | null>(null);
  protected playerVideos = signal<PlayerVideoDto[]>([]);
  protected showVideoUpload = signal(false);

  private playerService = inject(PlayerService);
  protected authService = inject(AuthService);
  private snackBar = inject(MatSnackBar);

  public ngOnInit(): void {
    const playerId = this.authService.userInfo()!.playerId!;

    this.playerService
      .getPlayerWithStatsById(playerId)
      .subscribe((playerStats) => {
        this.playerWithStats.set(playerStats);
      });

    this.loadPlayerVideos();
  }

  private loadPlayerVideos(): void {
    const playerId = this.authService.userInfo()!.playerId!;
    this.playerService.getPlayerVideos(playerId).subscribe({
      next: (videos) => {
        this.playerVideos.set(videos);
      },
      error: (error) => {
        console.error('Failed to load player videos:', error);
        // Don't show error message as videos might not be implemented on backend yet
      },
    });
  }

  protected toggleVideoUpload(): void {
    this.showVideoUpload.set(!this.showVideoUpload());
  }

  protected onVideoUploaded(result: VideoUploadResult): void {
    this.showVideoUpload.set(false);
    this.loadPlayerVideos(); // Refresh the video list
  }

  protected deleteVideo(videoId: string): void {
    const playerId = this.authService.userInfo()!.playerId!;

    this.playerService.deletePlayerVideo(playerId, videoId).subscribe({
      next: () => {
        this.loadPlayerVideos(); // Refresh the video list
        this.snackBar.open('Video deleted successfully', 'Close', {
          duration: 3000,
        });
      },
      error: (error) => {
        console.error('Failed to delete video:', error);
        this.snackBar.open('Failed to delete video', 'Close', {
          duration: 5000,
        });
      },
    });
  }

  protected formatDate(date: Date): string {
    return new Date(date).toLocaleDateString();
  }
}
