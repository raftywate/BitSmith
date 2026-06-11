import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ProblemDetail } from '../models/problem-detail';

export interface AdminTestCase {
  input: string;
  expectedOutput: string;
  inputLabels: string[];
  isHidden: boolean;
}

export interface AdminCreateProblemPayload {
  title: string;
  description: string;
  difficulty: string;
  starterCode?: string;
  hints: string[];
  categoryIDs: string[];
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private problemApiUrl = `${environment.apiUrl}/problem`;

  createProblem(payload: AdminCreateProblemPayload): Observable<ProblemDetail> {
    return this.http.post<ProblemDetail>(this.problemApiUrl, payload);
  }

  updateProblem(problemId: string, payload: AdminCreateProblemPayload): Observable<ProblemDetail> {
    return this.http.put<ProblemDetail>(`${this.problemApiUrl}/${problemId}`, payload);
  }

  addTestCases(problemId: string, testCases: AdminTestCase[]): Observable<ProblemDetail> {
    return this.http.post<ProblemDetail>(`${this.problemApiUrl}/${problemId}/testcases`, testCases);
  }

  replaceTestCases(problemId: string, testCases: AdminTestCase[]): Observable<ProblemDetail> {
    return this.http.put<ProblemDetail>(`${this.problemApiUrl}/${problemId}/testcases`, testCases);
  }

  /**
   * Upload a CSV or JSON file of test cases.
   * File never has its content exposed in JS — sent as multipart/form-data.
   * CSV: header row (isHidden,input,expectedOutput), then data rows.
   * JSON: [{isHidden,input,expectedOutput}]
   */
  uploadTestCasesFile(problemId: string, file: File): Observable<ProblemDetail> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<ProblemDetail>(
      `${this.problemApiUrl}/${problemId}/testcases/upload`,
      formData
    );
  }
}
