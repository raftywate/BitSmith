import { inject, Injectable } from '@angular/core';
import { AuthServiceContract } from './auth.contract';
import { BehaviorSubject, Observable, map, tap } from 'rxjs';
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

private userSubject = new BehaviorSubject<User | null>(this.getInitialUser());

public readonly currentUser$: Observable<User | null> = this.userSubject.asObservable();

public readonly isLoggedIn$: Observable<boolean> = this.userSubject.pipe(
  map(user => !!user)
);

public readonly isAdmin$: Observable<boolean> = this.userSubject.pipe(
    map(user => user?.role === 'Admin')
  );

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
    this.userSubject.next(userData);
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
    this.userSubject.next(null);
  }

  public getToken(): string | null {
    return localStorage.getItem('jwt_token');
  }
  
}
