import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { UserDto } from '../models/dtos/user.dto';
import { CompleteProfileRequest } from '../models/requests/complete-profile.request';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  public userInfo = signal<UserDto | null>(null);
  public isAuthenticated = computed(
    () => this.getCookie('user_authenticated') === 'true',
  );
  private http = inject(HttpClient);
  private router = inject(Router);
  private config = inject(ConfigService);

  public loadUserInfo() {
    this.http
      .get<UserDto>(this.config.getApiUrl('Auth/me'))
      .subscribe((user) => {
        this.userInfo.set(user);
      });
  }

  public login() {
    let requestEndpoint: string = 'google';
    window.location.href = this.config.getApiUrl(`Auth/${requestEndpoint}`);
  }

  public completeProfile(completeProfileRequest: CompleteProfileRequest) {
    return this.http.post(
      this.config.getApiUrl('Auth/Complete'),
      completeProfileRequest,
    );
  }

  public updateUserInfoFromComplete() {
    this.userInfo.update((user) => {
      if (!user) {
        return null;
      }
      return { ...user, isProfileComplete: true };
    });
  }

  public logout() {
    this.http.delete(this.config.getApiUrl('Auth/logout')).subscribe(() => {
      this.userInfo.set(null);
      this.router.navigate(['/']);
    });
  }

  public refreshToken() {
    return this.http.get(this.config.getApiUrl('Auth/refresh'));
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
