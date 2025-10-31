import { Component, inject, OnInit } from '@angular/core';
import { PlayerService } from '../../core/services/player.service';
import { PlayerWithStatsDto } from '../../core/models/dtos/player.dto';

@Component({
  selector: 'app-player-profile',
  standalone: true,
  imports: [],
  templateUrl: './player-profile.component.html',
  styleUrl: './player-profile.component.scss',
})
export class PlayerProfileComponent implements OnInit {
  protected playerId: string | null = null;
  protected playerWithStats: PlayerWithStatsDto | null = null;
  private playerService = inject(PlayerService);

  public ngOnInit(): void {
    this.playerService.getPlayerWithStatsById(1).subscribe((playerStats) => {
      this.playerWithStats = playerStats;
    });
  }
}
