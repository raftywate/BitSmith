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

  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/auth`;

  // State using Signals
  private userSignal = signal<User | null>(this.getInitialUser());

  public readonly currentUser$: Signal<User | null> = this.userSignal.asReadonly();

  public readonly isLoggedIn$: Signal<boolean> = computed(() => !!this.userSignal());

  public readonly isAdmin$: Signal<boolean> = computed(() => this.userSignal()?.role === 'Admin');

  private getInitialUser(): User | null {
    const token = localStorage.getItem('jwt_token');
    const userJson = localStorage.getItem('current_user');

    if (token && userJson) {
      try {
        const userData = JSON.parse(userJson);
        return {
          id: userData.userId,
          username: userData.username,
          role: userData.role
        } as User;
      } catch (e) {
        console.error("Failed to parse stored user data:", e);
        return null;
      }
    }
    return null;
  }

  private saveAuthData(response: AuthResponse) {
    localStorage.setItem('jwt_token', response.token);
    const userData: User = {
      id: response.userId,
      username: response.username,
      role: response.role
    }
    localStorage.setItem('current_user', JSON.stringify(userData));
    this.userSignal.set(userData);
  }

  // --- Abstract Methods Implementation ---

  login(credentials: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, credentials).pipe(
      tap(response => this.saveAuthData(response))
    );
  }

  register(credentials: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, credentials).pipe(
      tap(response => this.saveAuthData(response))
    );
  }

  logout(): void {
    localStorage.removeItem('jwt_token');
    localStorage.removeItem('current_user');
    this.userSignal.set(null);
  }

  public getToken(): string | null {
    return localStorage.getItem('jwt_token');
  }

}
