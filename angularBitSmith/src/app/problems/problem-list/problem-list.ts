import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, NgClass } from "@angular/common";
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Problem } from '../../services/problem';
import { ProblemSummary } from '../../models/problem-summary';
import { ProblemDifficulty } from '../../models/problem-difficulty';
import { ProblemListResponse } from '../../models/problem-list-response';

import { ToastService } from '../../services/toast';

import { SkeletonComponent } from '../../components/skeleton/skeleton';

@Component({
    selector: 'app-problem-list',
    standalone: true,
    imports: [CommonModule, NgClass, FormsModule, RouterModule, SkeletonComponent],
    templateUrl: './problem-list.html',
    styleUrl: './problem-list.scss'
})
export class ProblemList implements OnInit {
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
    private toastService = inject(ToastService);

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
                this.toastService.error('Failed to fetch problems');
                this.isLoading.set(false);
                console.error(err);
            }
        });
    }

    //Pagination Actions

    onNextPage() {
        if (this.hasNextPage()) {
            this.currentPage.update(p => p + 1);
            this.loadProblems();
        }
    }

    onPrevPage() {
        if (this.hasPrevPage()) {
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
