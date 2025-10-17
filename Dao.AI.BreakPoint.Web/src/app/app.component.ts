import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent{
  title = 'Dao.AI.BreakPoint.Web';

  constructor(private http: HttpClient) {
    http.get<HealthCheck>('api/health').subscribe({
      next: result => console.log('Health Check:', result),
      error: console.error
    });
  }
}


export interface HealthCheck{
  status: string;
  timestamp: string;
}
