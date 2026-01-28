import { Injectable } from '@angular/core';

/**
 * Configuration service that provides runtime configuration values.
 * The API URL is injected at container startup via window.__env.
 */
@Injectable({
  providedIn: 'root',
})
export class ConfigService {
  private readonly config: RuntimeConfig;

  constructor() {
    // Runtime config is injected via env-config.js generated at container startup
    const windowConfig = (window as WindowWithEnv).__env;

    this.config = {
      // For local development (ng serve), use relative URLs which the proxy handles
      // For production, use the injected API URL
      apiUrl: windowConfig?.apiUrl || '',
    };
  }

  /**
   * Gets the API base URL.
   * Returns empty string for local development (relative URLs).
   * Returns the full API URL for production deployments.
   */
  public get apiUrl(): string {
    return this.config.apiUrl;
  }

  /**
   * Builds a full API URL from a relative path.
   * @param path The API path (e.g., 'Auth/me' or '/Auth/me')
   * @returns The full URL (e.g., 'https://api.example.com/Auth/me' or '/api/Auth/me')
   */
  public getApiUrl(path: string): string {
    const cleanPath = path.startsWith('/') ? path.slice(1) : path;

    if (this.config.apiUrl) {
      // Production: use absolute URL to API
      return `${this.config.apiUrl}/${cleanPath}`;
    }

    // Development: use relative URL (proxy handles it)
    return `/api/${cleanPath}`;
  }
}

interface RuntimeConfig {
  apiUrl: string;
}

interface WindowWithEnv extends Window {
  __env?: {
    apiUrl?: string;
  };
}
