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
    return this.http.get<PlayerDto>(`api/players/${playerId}`);
  }

  public getPlayerWithStatsById(
    playerId: number
  ): Observable<PlayerWithStatsDto> {
    return this.http.get<PlayerWithStatsDto>(`api/players/${playerId}/details`);
  }
}
