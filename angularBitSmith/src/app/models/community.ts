export type VoteChoice = 'up' | 'down' | null;
export type VotableEntityType = 'Solution' | 'Comment';

export interface SolutionSummary {
  id: string;
  problemId: string;
  title: string;
  authorName: string;
  authorUsername: string;
  excerpt: string;
  voteCount: number;
  commentCount: number;
  createdAt: string;
  problemTitle?: string;
  problemNumber?: number;
  problemSlug?: string;
}

export interface SolutionDetail extends SolutionSummary {
  content: string;
}

export interface SolutionCreateRequest {
  problemId: string;
  title: string;
  content: string;
}

export interface CommentViewModel {
  id: string;
  solutionId: string;
  parentCommentId: string | null;
  content: string;
  authorUsername: string;
  createdAt: string;
  voteCount: number;
  replies: CommentViewModel[];
  userVote?: VoteChoice;
}

export interface CommentCreateRequest {
  solutionId: string;
  parentCommentId?: string | null;
  content: string;
}

export interface VoteRequest {
  entityId: string;
  entityType: VotableEntityType;
  isUpvote: boolean;
}

export interface SolutionUpdateRequest {
  title: string;
  content: string;
}

export interface CommentUpdateRequest {
  content: string;
}
