import { Component, inject, input, OnInit, signal } from '@angular/core';
import { PlayerService } from '../../core/services/player.service';
import { PlayerWithStatsDto, VideoUploadResult } from '../../core/models/dtos';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
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
    VideoUploadComponent,
  ],
  templateUrl: './player-profile.component.html',
  styleUrl: './player-profile.component.scss',
})
export class PlayerProfileComponent implements OnInit {
  protected playerWithStats = signal<PlayerWithStatsDto | null>(null);
  protected showVideoUpload = signal(false);

  private playerService = inject(PlayerService);
  protected authService = inject(AuthService);
  private toastService = inject(ToastService);

  public ngOnInit(): void {
    const playerId = this.authService.userInfo()!.playerId!;

    this.playerService
      .getPlayerWithStatsById(playerId)
      .subscribe((playerStats) => {
        this.playerWithStats.set(playerStats);
      });
  }

  protected toggleVideoUpload(): void {
    this.showVideoUpload.set(!this.showVideoUpload());
  }

  protected onVideoUploaded(result: VideoUploadResult): void {
    this.showVideoUpload.set(false);
  }

  protected deleteVideo(videoId: string): void {
    const playerId = this.authService.userInfo()!.playerId!;

    this.playerService.deletePlayerVideo(playerId, videoId).subscribe({
      next: () => {
        this.toastService.success('Video deleted successfully');
      },
      error: (error) => {
        console.error('Failed to delete video:', error);
      },
    });
  }

  protected formatDate(date: Date): string {
    return new Date(date).toLocaleDateString();
  }
}
