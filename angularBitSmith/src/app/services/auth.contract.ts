import { Observable } from 'rxjs';
import { User } from '../models/user';
import { Injectable } from '@angular/core';
import { AuthResponse } from '../models/auth-response';

@Injectable({
  providedIn: 'root',
})
export abstract class AuthServiceContract {
  //State Management
  abstract readonly currentUser$ : Observable<User | null>;
  abstract readonly isLoggedIn$ : Observable<boolean>;
  abstract readonly isAdmin$ : Observable<boolean>;

  //API methods
  abstract login(credentials : any) : Observable<AuthResponse>;
  abstract register(credentials : any) : Observable<AuthResponse>;
  abstract logout() : void;
}
