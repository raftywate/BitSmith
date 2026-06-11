export type SubmissionStatus =
  | 'Pending'
  | 'Running'
  | 'Accepted'
  | 'WrongAnswer'
  | 'RuntimeError'
  | 'InternalError'
  | 'CompilationError'
  | 'TimeLimitExceeded'
  | 'MemoryLimitExceeded';

export type EditorLanguage = 'csharp' | 'python' | 'java' | 'cpp' | 'c';

export interface SubmissionCreateRequest {
  problemId: string;
  language: EditorLanguage;
  code: string;
}

export interface SampleRunRequest {
  problemId: string;
  language: EditorLanguage;
  code: string;
}

export interface SampleRunResult {
  testCaseId: string;
  input: string;
  expectedOutput: string;
  actualOutput: string;
  status: string;
  error: string | null;
  executionTimeMs: number | null;
  executionMemoryKb: number | null;
  passed: boolean;
}

export interface SubmissionResult {
  id: string;
  problemId: string;
  status: SubmissionStatus;
  createdAt: string;
  errorMessage?: string | null;
  passedCount?: number;
  totalCount?: number;
  failedTestCaseInput?: string | null;
  failedTestCaseExpected?: string | null;
  failedTestCaseActual?: string | null;
}

export interface SubmissionDetail extends SubmissionResult {
  code: string;
  language: string;
  executionTimeMs: number | null;
  executionMemoryKb: number | null;
  errorMessage: string | null;
}

export interface LanguageOption {
  value: EditorLanguage;
  label: string;
  monacoLanguage: string;
}

export const EDITOR_LANGUAGE_OPTIONS: LanguageOption[] = [
  { value: 'csharp', label: 'C#', monacoLanguage: 'csharp' },
  { value: 'python', label: 'Python', monacoLanguage: 'python' },
  { value: 'java', label: 'Java', monacoLanguage: 'java' },
  { value: 'cpp', label: 'C++', monacoLanguage: 'cpp' },
  { value: 'c', label: 'C', monacoLanguage: 'c' }
];
