import { Category } from "./category";
import { ProblemDifficulty } from "./problem-difficulty.enum";

export interface ProblemSummary {
    id: string;
    title: string;
    problemNumber: number;
    difficulty: ProblemDifficulty;
    categories: Category[];
}
