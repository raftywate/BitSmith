import { ProblemSummary } from './problem-summary';

export interface ProblemDetail extends ProblemSummary {
    description: string;
    examples: string; // HTML or Markdown string
    constraints: string; // HTML or Markdown string
    starterCode: string;
}
