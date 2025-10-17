import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ApiService, WeatherForecast, Player, Match } from './api.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'Dao.AI.BreakPoint.Web';
  weatherData: WeatherForecast[] = [];
  players: Player[] = [];
  matches: Match[] = [];
  loading = true;
  error: string | null = null;

  constructor(private apiService: ApiService) {}

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading = true;
    this.error = null;

    // Load weather data
    this.apiService.getWeatherForecast().subscribe({
      next: (data) => {
        this.weatherData = data;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load weather data: ' + err.message;
        this.loading = false;
      }
    });

    // Load players
    this.apiService.getPlayers().subscribe({
      next: (data) => {
        this.players = data;
      },
      error: (err) => {
        console.error('Failed to load players:', err);
      }
    });

    // Load matches
    this.apiService.getMatches().subscribe({
      next: (data) => {
        this.matches = data;
      },
      error: (err) => {
        console.error('Failed to load matches:', err);
      }
    });
  }
}
