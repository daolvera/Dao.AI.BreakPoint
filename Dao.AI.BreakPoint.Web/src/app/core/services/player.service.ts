import { inject, Injectable } from '@angular/core';
import { PlayerDto, PlayerWithStatsDto } from '../models/dtos/player.dto';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface PlayerVideoDto {
  id: string;
  fileName: string;
  uploadDate: Date;
  url: string;
  thumbnailUrl?: string;
  duration: number;
}

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

  public getPlayerVideos(playerId: number): Observable<PlayerVideoDto[]> {
    return this.http.get<PlayerVideoDto[]>(`api/players/${playerId}/videos`);
  }

  public deletePlayerVideo(
    playerId: number,
    videoId: string
  ): Observable<void> {
    return this.http.delete<void>(`api/players/${playerId}/videos/${videoId}`);
  }
}
