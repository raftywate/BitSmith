import { Signal, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { User } from '../models/user';
import { AuthResponse } from '../models/auth-response';

@Injectable({
  providedIn: 'root',
})
export abstract class AuthServiceContract {
  //State Management
  abstract readonly currentUser$: Signal<User | null>;
  abstract readonly isLoggedIn$: Signal<boolean>;
  abstract readonly isAdmin$: Signal<boolean>;

  //API methods
  abstract login(credentials: any): Observable<AuthResponse>;
  abstract register(credentials: any): Observable<AuthResponse>;
  abstract logout(): void;
}
