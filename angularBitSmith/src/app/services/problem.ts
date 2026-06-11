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
    categoryIds?: string[]
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

    return this.http.get<ProblemListResponse>(this.apiUrl, { params });
  }

  getProblemById(id: string): Observable<ProblemDetail> {
    return this.http.get<ProblemDetail>(`${this.apiUrl}/${id}`);
  }

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.apiUrl}/categories`);
  }
}
