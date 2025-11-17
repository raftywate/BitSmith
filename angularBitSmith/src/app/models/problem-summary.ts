import { Category } from "./category";
import { ProblemDifficulty } from "./problem-difficulty";

export interface ProblemSummary {
    id : string;
    title : string;
    problemNumber: number;
    difficulty : ProblemDifficulty;
    categories : Category[];
}
