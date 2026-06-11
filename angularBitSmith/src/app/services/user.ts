import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { UserProfile, UserProfileUpdateRequest, UserPreferencesUpdateRequest, UserSettingsUpdateRequest } from '../models/user-profile';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/user`;

  getMyProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.apiUrl}/me`);
  }

  updateMyProfile(payload: UserProfileUpdateRequest): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${this.apiUrl}/me`, payload);
  }

  uploadMyAvatar(file: File): Observable<UserProfile> {
    const formData = new FormData();
    formData.append('avatar', file);

    return this.http.post<UserProfile>(`${this.apiUrl}/me/avatar`, formData);
  }

  removeMyAvatar(): Observable<UserProfile> {
    return this.http.delete<UserProfile>(`${this.apiUrl}/me/avatar`);
  }

  updateMyPreferences(payload: UserPreferencesUpdateRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/me/preferences`, payload);
  }

  updateMySettings(payload: UserSettingsUpdateRequest): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${this.apiUrl}/me/settings`, payload);
  }
}
