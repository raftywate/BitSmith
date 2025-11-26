import { ProblemSummary } from './problem-summary';

export interface ProblemListResponse {
  problems: ProblemSummary[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}