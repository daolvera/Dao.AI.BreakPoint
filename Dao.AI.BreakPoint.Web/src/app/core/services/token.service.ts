import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TokenService {
  private readonly ACCESS_TOKEN_KEY = 'bp_access_token';
  private readonly REFRESH_TOKEN_KEY = 'bp_refresh_token';
  private readonly TOKEN_EXPIRY_KEY = 'bp_token_expiry';

  setTokens(
    accessToken: string,
    refreshToken: string,
    expiresAt: string
  ): void {
    localStorage.setItem(this.ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(this.REFRESH_TOKEN_KEY, refreshToken);
    localStorage.setItem(this.TOKEN_EXPIRY_KEY, expiresAt);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  getTokenExpiry(): Date | null {
    const expiry = localStorage.getItem(this.TOKEN_EXPIRY_KEY);
    return expiry ? new Date(expiry) : null;
  }

  isTokenExpired(): boolean {
    const expiry = this.getTokenExpiry();
    if (!expiry) return true;

    // Add 5-minute buffer for clock skew
    const bufferTime = 5 * 60 * 1000;
    return Date.now() >= expiry.getTime() - bufferTime;
  }

  clearTokens(): void {
    localStorage.removeItem(this.ACCESS_TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.TOKEN_EXPIRY_KEY);
  }

  hasValidToken(): boolean {
    return !!this.getAccessToken() && !this.isTokenExpired();
  }
}
