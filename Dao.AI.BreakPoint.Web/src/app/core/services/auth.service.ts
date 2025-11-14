import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { UserDto } from '../models/dtos/user.dto';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  public userInfo = signal<UserDto | null>(null);
  public isAuthenticated = computed(
    () => this.getCookie('user_authenticated') === 'true'
  );
  private http = inject(HttpClient);
  private router = inject(Router);

  public loadUserInfo() {
    this.http.get<UserDto>('api/Auth/me').subscribe((user) => {
      this.userInfo.set(user);
    });
  }

  public login() {
    let requestEndpoint: string = 'google';
    window.location.href = `api/Auth/${requestEndpoint}`;
  }

  public logout() {
    this.http.delete('api/Auth/logout').subscribe(() => {
      this.userInfo.set(null);
      this.router.navigate(['/']);
    });
  }

  public refreshToken() {
    return this.http.get('api/Auth/refresh');
  }

  private getCookie(name: string): string | null {
    const nameEQ = name + '=';
    const cookies = document.cookie.split(';');

    for (let cookie of cookies) {
      cookie = cookie.trim();
      if (cookie.indexOf(nameEQ) === 0) {
        return cookie.substring(nameEQ.length);
      }
    }
    return null;
  }
}
