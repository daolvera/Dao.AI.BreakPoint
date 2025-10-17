import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface Player {
  id?: number;
  name: string;
  email: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface Match {
  id?: number;
  player1Id: number;
  player2Id?: number;
  matchDate: string;
  location: string;
  result: string;
  notes?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface WeatherForecast {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  // Weather endpoints
  getWeatherForecast(): Observable<WeatherForecast[]> {
    return this.http.get<WeatherForecast[]>(`${this.baseUrl}/weatherforecast`);
  }

  // Player endpoints
  getPlayers(): Observable<Player[]> {
    return this.http.get<Player[]>(`${this.baseUrl}/players`);
  }

  createPlayer(player: Player): Observable<Player> {
    return this.http.post<Player>(`${this.baseUrl}/players`, player);
  }

  // Match endpoints
  getMatches(): Observable<Match[]> {
    return this.http.get<Match[]>(`${this.baseUrl}/matches`);
  }

  createMatch(match: Match): Observable<Match> {
    return this.http.post<Match>(`${this.baseUrl}/matches`, match);
  }
}
