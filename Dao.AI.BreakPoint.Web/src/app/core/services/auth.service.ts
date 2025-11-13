import { inject, Injectable, signal } from '@angular/core';
import { UserDto } from '../models/dtos/user.dto';
import { HttpClient } from '@angular/common/http';
import { RefreshTokenResponse } from '../models/responses/refresh-token.response';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  public userInfo = signal<UserDto | null>(null);
  public isAuthenticated = signal<boolean>(false);
  private readonly accessToken = signal<string | null>(null);
  private http = inject(HttpClient);

  public login() {
    let requestEndpoint: string = 'google';
    this.http.get(`api/Auth/${requestEndpoint}`).subscribe((response: any) => {
      console.log('Login response:', response);
    });
  }

  public logout() {
    // clear the cache and call it a day
  }

  public refreshToken(refreshToken: string) {
    return this.http.post<RefreshTokenResponse>('api/auth/refresh', {
      refreshToken,
    });
  }
}
