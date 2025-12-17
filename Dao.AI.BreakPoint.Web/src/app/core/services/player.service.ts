import { inject, Injectable } from '@angular/core';
import { PlayerDto, PlayerWithStatsDto } from '../models/dtos/player.dto';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class PlayerService {
  private http = inject(HttpClient);

  public getPlayerById(playerId: number): Observable<PlayerDto> {
    return this.http.get<PlayerDto>(`api/player/${playerId}`);
  }

  public getPlayerWithStatsById(
    playerId: number
  ): Observable<PlayerWithStatsDto> {
    return this.http.get<PlayerWithStatsDto>(`api/player/${playerId}/details`);
  }

  public deletePlayerVideo(
    playerId: number,
    videoId: string
  ): Observable<void> {
    return this.http.delete<void>(`api/player/${playerId}/videos/${videoId}`);
  }
}
