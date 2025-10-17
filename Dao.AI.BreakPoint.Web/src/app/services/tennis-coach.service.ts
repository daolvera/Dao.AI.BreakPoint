import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface CoachingSession {
  id: string;
  playerName: string;
  date: Date;
  drillsCompleted: number;
  performanceScore: number;
}

@Injectable({
  providedIn: 'root'
})
export class TennisCoachService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  // Example API calls for your tennis AI coach
  getCoachingSessions(): Observable<CoachingSession[]> {
    return this.http.get<CoachingSession[]>(`${this.apiUrl}/coaching-sessions`);
  }

  createCoachingSession(session: Partial<CoachingSession>): Observable<CoachingSession> {
    return this.http.post<CoachingSession>(`${this.apiUrl}/coaching-sessions`, session);
  }

  getPlayerStats(playerId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/players/${playerId}/stats`);
  }

  analyzePerformance(sessionId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/coaching-sessions/${sessionId}/analyze`, {});
  }
}
