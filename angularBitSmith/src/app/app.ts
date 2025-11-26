import { Observable } from 'rxjs';
import { Problem } from './services/problem';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './services/theme'; 
import { HttpClient } from '@angular/common/http';
import { CommonModule, NgClass } from "@angular/common";
import { ProblemSummary } from './models/problem-summary';
import { ProblemDifficulty } from './models/problem-difficulty';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ProblemListResponse } from './models/problem-list-response';

@Component({
  selector: 'app-root',
  imports: [CommonModule, NgClass, FormsModule],
  templateUrl: './app.html',
  standalone : true,
  styleUrl: './app.scss'
})
export class App implements OnInit {
  //State signals
  protected readonly title = signal('angularBitSmith');
  protected readonly ProblemDifficulty = ProblemDifficulty;
  
  // Data
  problems = signal<ProblemSummary[]>([]);
  isLoading = signal(true);
  error = signal<string | null>(null);

  // Pagination State
  currentPage = signal(1);
  pageSize = signal(10);
  totalCount = signal(0);
  totalPages = signal(0);
  hasNextPage = signal(false);
  hasPrevPage = signal(false);

  //Services
  private problemService = inject(Problem);
  public themeService = inject(ThemeService);

  ngOnInit() {
    this.loadProblems();
  }

  loadProblems() {
    this.isLoading.set(true);
    this.error.set(null);

    //Calling service with current page and size
    this.problemService.getProblems(this.currentPage(), this.pageSize()).subscribe({
      next: (response: ProblemListResponse) => {

        //1.Update the data list
        this.problems.set(response.problems);

        //2.Update pagination metadata
        this.totalCount.set(response.totalCount);
        this.totalPages.set(response.totalPages);
        this.hasNextPage.set(response.hasNextPage);
        this.hasPrevPage.set(response.hasPreviousPage);

        this.isLoading.set(false);
      },
      error: (err) => {
        // Handle any errors (e.g., API is not running)
        this.error.set('Failed to fetch problems. Is the .NET API running?');
        this.isLoading.set(false);
        console.error(err);
      }
    });
  }

  //Pagination Actions

  onNextPage() {
    if(this.hasNextPage()) {
      this.currentPage.update(p => p + 1);
      this.loadProblems();
    }
  }

  onPrevPage() {
    if(this.hasPrevPage()) {
      this.currentPage.update(p => p - 1);
      this.loadProblems();
    }
  }

  onPageSizeChange() {
    // When size changes, reset to page 1 to avoid "Page 5 of 2" errors
    this.currentPage.set(1);
    this.loadProblems();
  }
}
