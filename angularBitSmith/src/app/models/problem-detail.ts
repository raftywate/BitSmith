import { Category } from "./category";
import { ProblemDifficulty } from "./problem-difficulty.enum";

export interface SampleTestCase {
  id: string;
  input: string;
  expectedOutput: string;
  inputLabels: string[];
  isHidden?: boolean;
}

export interface ProblemDetail {
  id: string;
  problemNumber: number;
  title: string;
  description: string;
  difficulty: ProblemDifficulty;
  starterCode: string | null;
  metaDataJson?: string | null;
  hints: string[];
  categories: Category[];
  sampleTestCases: SampleTestCase[];
  testCases?: SampleTestCase[];
  authorName: string;
  status?: 'Solved' | 'Attempted' | 'Unattempted';
}
