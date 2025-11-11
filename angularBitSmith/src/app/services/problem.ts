import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Observable } from 'rxjs';
import { ProblemSummary } from '../models/problem-summary';

@Injectable({
  providedIn: 'root'
})
export class Problem {
  private http = inject(HttpClient);

  private apiUrl = `${environment.apiUrl}/problem`;

  getProblems() : Observable<ProblemSummary[]> {
    return this.http.get<ProblemSummary[]>(this.apiUrl);
  }
}