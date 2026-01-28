import { inject, Injectable } from '@angular/core';
import { PlayerDto, PlayerWithStatsDto } from '../models/dtos/player.dto';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root',
})
export class PlayerService {
  private http = inject(HttpClient);
  private config = inject(ConfigService);

  public getPlayerById(playerId: number): Observable<PlayerDto> {
    return this.http.get<PlayerDto>(
      this.config.getApiUrl(`player/${playerId}`),
    );
  }

  public getPlayerWithStatsById(
    playerId: number,
  ): Observable<PlayerWithStatsDto> {
    return this.http.get<PlayerWithStatsDto>(
      this.config.getApiUrl(`player/${playerId}/details`),
    );
  }

  public updateUstaRating(
    playerId: number,
    ustaRating: number,
  ): Observable<boolean> {
    return this.http.patch<boolean>(
      this.config.getApiUrl(`player/${playerId}/rating`),
      {
        ustaRating,
      },
    );
  }

  public deletePlayerVideo(
    playerId: number,
    videoId: string,
  ): Observable<void> {
    return this.http.delete<void>(
      this.config.getApiUrl(`player/${playerId}/videos/${videoId}`),
    );
  }
}
