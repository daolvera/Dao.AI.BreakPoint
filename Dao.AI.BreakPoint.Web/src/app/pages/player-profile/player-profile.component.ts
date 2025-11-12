import { Component, inject, input, OnInit } from '@angular/core';
import { PlayerService } from '../../core/services/player.service';
import { PlayerWithStatsDto } from '../../core/models/dtos/player.dto';
import { MatCardModule } from '@angular/material/card';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-player-profile',
  standalone: true,
  imports: [MatCardModule],
  templateUrl: './player-profile.component.html',
  styleUrl: './player-profile.component.scss',
})
export class PlayerProfileComponent implements OnInit {
  protected playerWithStats: PlayerWithStatsDto | null = null;
  private playerService = inject(PlayerService);
  private authService = inject(AuthService);

  public ngOnInit(): void {
    this.playerService
      .getPlayerWithStatsById(this.authService.userInfo()!.playerId!)
      .subscribe((playerStats) => {
        this.playerWithStats = playerStats;
      });
  }
}
