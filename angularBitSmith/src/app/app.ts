import { Observable } from 'rxjs';
import { Problem } from './services/problem';
import { RouterOutlet } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule, NgClass } from "@angular/common";
import { ProblemSummary } from './models/problem-summary';
import { ProblemDifficulty } from './models/problem-difficulty';
import { Component, OnInit, inject, signal } from '@angular/core';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, CommonModule, NgClass],
  templateUrl: './app.html',
  standalone : true,
  styleUrl: './app.scss'
})
export class App implements OnInit {
  protected readonly title = signal('angularBitSmith');


  problems = signal<ProblemSummary[]>([]);
  isLoading = signal(true);
  error = signal<string | null>(null);

  protected readonly ProblemDifficulty = ProblemDifficulty;
  private problemService = inject(Problem);

  ngOnInit() {
    this.loadProblems();
  }

  loadProblems() {
    this.isLoading.set(true);
    this.error.set(null);
    this.problemService.getProblems().subscribe({
      next: (data) => {
        //Success! Save the data to the signal.
        this.problems.set(data);
        this.isLoading.set(false);
        console.log('Fetched problem: ', data);
      },
      error: (err) => {
        // Handle any errors (e.g., API is not running)
        this.error.set('Failed to fetch problems. Is the .NET API running?');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }
}
