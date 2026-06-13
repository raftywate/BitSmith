import { inject, Injectable, signal, computed, Signal } from '@angular/core';
import { AuthServiceContract } from './auth.contract';
import { Observable, tap } from 'rxjs';
import { AuthResponse } from '../models/auth-response';
import { User } from '../models/user';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class AuthService extends AuthServiceContract {
  private readonly tokenStorageKey = 'compylr.token';
  private readonly userStorageKey = 'compylr.user';

  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/auth`;

  private userSignal = signal<User | null>(this.getInitialUser());

  public readonly currentUser$: Signal<User | null> = this.userSignal.asReadonly();

  public readonly isLoggedIn$: Signal<boolean> = computed(() => !!this.userSignal());

  public readonly isAdmin$: Signal<boolean> = computed(() => this.userSignal()?.role?.toLowerCase() === 'admin');

  private getInitialUser(): User | null {
    const token = localStorage.getItem(this.tokenStorageKey);
    const userJson = localStorage.getItem(this.userStorageKey);

    if (token && userJson) {
      if (this.isTokenExpired(token)) {
        this.clearStoredAuth();
        return null;
      }

      try {
        const userData = JSON.parse(userJson) as User;
        return {
          id: userData.id,
          username: userData.username,
          role: userData.role,
          profilePictureUrl: userData.profilePictureUrl
        } as User;
      } catch (e) {
        console.error('Failed to parse stored user data:', e);
        this.clearStoredAuth();
        return null;
      }
    }

    return null;
  }

  private saveAuthData(response: AuthResponse) {
    if (!response.token || !response.userId || !response.username || !response.role) {
      console.error('Invalid auth response data:', response);
      return;
    }

    localStorage.setItem(this.tokenStorageKey, response.token);

    const userData: User = {
      id: response.userId,
      username: response.username,
      role: response.role,
      profilePictureUrl: response.profilePictureUrl
    };

    localStorage.setItem(this.userStorageKey, JSON.stringify(userData));
    this.userSignal.set(userData);
  }

  public updateCurrentUserProfilePicture(url: string | null) {
    const current = this.userSignal();
    if (current) {
      const updated: User = {
        ...current,
        profilePictureUrl: url
      };
      localStorage.setItem(this.userStorageKey, JSON.stringify(updated));
      this.userSignal.set(updated);
    }
  }

  public updateCurrentUserDetails(username: string, profilePictureUrl: string | null) {
    const current = this.userSignal();
    if (current) {
      const updated: User = {
        ...current,
        username: username,
        profilePictureUrl: profilePictureUrl
      };
      localStorage.setItem(this.userStorageKey, JSON.stringify(updated));
      this.userSignal.set(updated);
    }
  }

  login(credentials: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, credentials).pipe(
      tap(response => this.saveAuthData(response))
    );
  }

  register(credentials: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, credentials).pipe(
      tap(response => {
        if (!response.requiresVerification) {
          this.saveAuthData(response);
        }
      })
    );
  }

  verifyOtp(email: string, otp: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/verify-otp`, { email, otp }).pipe(
      tap(response => this.saveAuthData(response))
    );
  }

  resendOtp(email: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/resend-otp`, { email });
  }

  logout(silent = false): void {
    this.clearStoredAuth();
    this.userSignal.set(null);

    if (!silent) {
      console.info('Signed out of Compylr session.');
    }
  }

  public getToken(): string | null {
    const token = localStorage.getItem(this.tokenStorageKey);

    if (!token || this.isTokenExpired(token)) {
      this.clearStoredAuth();
      this.userSignal.set(null);
      return null;
    }

    return token;
  }

  private clearStoredAuth() {
    localStorage.removeItem(this.tokenStorageKey);
    localStorage.removeItem(this.userStorageKey);
  }

  private isTokenExpired(token: string): boolean {
    try {
      const payload = token.split('.')[1];
      if (!payload) {
        return true;
      }

      const normalizedPayload = payload
        .replace(/-/g, '+')
        .replace(/_/g, '/')
        .padEnd(Math.ceil(payload.length / 4) * 4, '=');

      const decoded = JSON.parse(atob(normalizedPayload)) as {
        exp?: number;
      };

      if (!decoded.exp) {
        return false;
      }

      return decoded.exp * 1000 <= Date.now();
    } catch {
      return true;
    }
  }
}
