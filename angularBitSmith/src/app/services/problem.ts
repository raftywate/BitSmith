import { Observable } from 'rxjs';
import { inject, Injectable } from '@angular/core';
import { ProblemDetail } from '../models/problem-detail';
import { environment } from '../../environments/environment';
import { HttpClient, HttpParams } from '@angular/common/http';
import { ProblemListResponse } from '../models/problem-list-response';
import { Category } from '../models/category';

@Injectable({
  providedIn: 'root'
})
export class ProblemService {
  private http = inject(HttpClient);

  private apiUrl = `${environment.apiUrl}/problem`;

  getProblems(
    page: number = 1,
    pageSize: number = 50,
    search?: string,
    categoryIds?: string[],
    statusFilter?: string
  ): Observable<ProblemListResponse> {
    let params = new HttpParams()
      .set('PageNumber', String(page))
      .set('PageSize', String(pageSize));

    if (search?.trim()) {
      params = params.set('Search', search.trim());
    }

    if (categoryIds && categoryIds.length > 0) {
      categoryIds.forEach(id => {
        params = params.append('CategoryIds', id);
      });
    }

    if (statusFilter) {
      params = params.set('StatusFilter', statusFilter);
    }

    return this.http.get<ProblemListResponse>(this.apiUrl, { params });
  }

  getProblemById(id: string): Observable<ProblemDetail> {
    return this.http.get<ProblemDetail>(`${this.apiUrl}/${id}`);
  }

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.apiUrl}/categories`);
  }

  getProblemOfTheDay(dateStr?: string): Observable<any> {
    let params = new HttpParams();
    if (dateStr) params = params.set('dateStr', dateStr);
    params = params.set('_t', new Date().getTime().toString()); // Cache buster
    return this.http.get<any>(`${this.apiUrl}/pod`, { params });
  }

  setProblemOfTheDay(dateStr: string, problemId: string): Observable<any> {
    const params = new HttpParams()
      .set('dateStr', dateStr)
      .set('problemId', problemId);
    return this.http.post<any>(`${this.apiUrl}/pod`, null, { params });
  }

  getPoDActivity(dateStr?: string, tzOffset?: number): Observable<{ solvedDates: string[], currentStreak: number }> {
    let params = new HttpParams();
    if (dateStr) params = params.set('dateStr', dateStr);
    if (tzOffset !== undefined) params = params.set('tzOffset', tzOffset.toString());
    params = params.set('_t', new Date().getTime().toString());
    return this.http.get<{ solvedDates: string[], currentStreak: number }>(`${this.apiUrl}/pod/activity`, { params });
  }
}
