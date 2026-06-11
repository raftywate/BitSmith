import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CommentCreateRequest,
  CommentViewModel,
  SolutionCreateRequest,
  SolutionDetail,
  SolutionSummary,
  VoteRequest
} from '../models/community';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class CommunityService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl;

  getSolutionsForProblem(problemId: string): Observable<SolutionSummary[]> {
    return this.http.get<SolutionSummary[]>(`${this.apiUrl}/solution/problem/${problemId}`);
  }

  getSolutionById(solutionId: string): Observable<SolutionDetail> {
    return this.http.get<SolutionDetail>(`${this.apiUrl}/solution/${solutionId}`);
  }

  createSolution(payload: SolutionCreateRequest): Observable<SolutionSummary> {
    return this.http.post<SolutionSummary>(`${this.apiUrl}/solution`, payload);
  }

  getCommentsForSolution(
    solutionId: string,
    pageNumber = 1,
    pageSize = 5
  ): Observable<CommentViewModel[]> {
    const params = new HttpParams()
      .set('PageNumber', String(pageNumber))
      .set('PageSize', String(pageSize));

    return this.http.get<CommentViewModel[]>(`${this.apiUrl}/comment/solution/${solutionId}`, {
      params
    });
  }

  createComment(payload: CommentCreateRequest): Observable<CommentViewModel> {
    return this.http.post<CommentViewModel>(`${this.apiUrl}/comment`, payload);
  }

  castVote(payload: VoteRequest): Observable<number> {
    return this.http.post<number>(`${this.apiUrl}/vote`, payload);
  }
}
