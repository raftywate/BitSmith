import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Observable } from 'rxjs';
import { ProblemSummary } from '../models/problem-summary';
import { ProblemListResponse } from '../models/problem-list-response';
import { ProblemDetail } from '../models/problem-detail';

@Injectable({
  providedIn: 'root'
})
export class ProblemService {
  private http = inject(HttpClient);

  private apiUrl = `${environment.apiUrl}/problem`;

  /**
   * Fetches problems with pagination.
   * Defaults to Page 1, Size 10 if not specified.
   */
  getProblems(page: number = 1, pageSize: number = 10): Observable<ProblemListResponse> {
    // Use HttpParams to safely build the query string
    // Result: /api/problem?PageNumber=1&PageSize=10
    let params = new HttpParams()
      .set('PageNumber', page)
      .set('PageSize', pageSize);

    return this.http.get<ProblemListResponse>(this.apiUrl, { params });
  }

  getProblemById(id : string): Observable<ProblemDetail> {
    return this.http.get<ProblemDetail>(`${this.apiUrl}/${id}`);
  }
}