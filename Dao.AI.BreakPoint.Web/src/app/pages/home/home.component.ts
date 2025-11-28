import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'breakpoint-home',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatCardModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  protected authService = inject(AuthService);

  protected features = [
    {
      icon: 'video_call',
      title: 'Upload Swing Videos',
      description:
        'Record and upload your tennis swing videos up to 30 seconds for detailed analysis.',
    },
    {
      icon: 'psychology',
      title: 'AI-Powered Analysis',
      description:
        'Get instant AI-driven feedback on your technique, form, and areas for improvement.',
    },
    {
      icon: 'trending_up',
      title: 'Track Progress',
      description:
        'Monitor your improvement over time with detailed statistics and match tracking.',
    },
    {
      icon: 'groups',
      title: 'Connect with Players',
      description:
        'Find other players, schedule matches, and build your tennis network.',
    },
    {
      icon: 'sports_tennis',
      title: 'Match Statistics',
      description:
        'Keep track of your wins, losses, and performance metrics across all matches.',
    },
    {
      icon: 'school',
      title: 'Personalized Coaching',
      description:
        'Receive tailored coaching tips based on your playing style and skill level.',
    },
  ];

  protected steps = [
    {
      step: '1',
      title: 'Sign Up',
      description:
        'Create your free BreakPoint account in seconds with Google authentication.',
    },
    {
      step: '2',
      title: 'Upload Video',
      description:
        'Record your tennis swing and upload it directly from your device.',
    },
    {
      step: '3',
      title: 'Get Analysis',
      description:
        'Our AI analyzes your technique and provides instant feedback and tips.',
    },
    {
      step: '4',
      title: 'Improve & Track',
      description:
        'Apply the feedback, upload new videos, and watch your game improve over time.',
    },
  ];

  protected login(): void {
    this.authService.login();
  }
}
