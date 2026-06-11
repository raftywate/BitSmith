import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  SampleRunRequest,
  SampleRunResult,
  SubmissionCreateRequest,
  SubmissionDetail,
  SubmissionResult
} from '../models/submission';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SubmissionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/submission`;

  createSubmission(payload: SubmissionCreateRequest): Observable<SubmissionResult> {
    return this.http.post<SubmissionResult>(this.apiUrl, payload);
  }

  getMySubmissionsForProblem(problemId: string): Observable<SubmissionDetail[]> {
    return this.http.get<SubmissionDetail[]>(`${this.apiUrl}/problem/${problemId}`);
  }

  runSampleTests(payload: SampleRunRequest): Observable<SampleRunResult[]> {
    return this.http.post<SampleRunResult[]>(`${this.apiUrl}/run-samples`, payload);
  }
}
