import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ProblemService } from '../../services/problem';
import { ProblemSummary } from '../../models/problem-summary';
import { ProblemDifficulty } from '../../models/problem-difficulty.enum';
import { ProblemListResponse } from '../../models/problem-list-response';
import { Category } from '../../models/category';
import { ToastService } from '../../services/toast';
import { SkeletonComponent } from '../../components/skeleton/skeleton';
import { getApiErrorMessage } from '../../utils/api-error';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { AuthService } from '../../services/auth';
import { UserService } from '../../services/user';

@Component({
    selector: 'app-problem-list',
    standalone: true,
    imports: [CommonModule, RouterModule, SkeletonComponent, FormsModule],
    templateUrl: './problem-list.html',
    styleUrl: './problem-list.scss'
})
export class ProblemList implements OnInit {
    protected readonly ProblemDifficulty = ProblemDifficulty;
    private readonly allowedPageSizes = [10, 20, 50, 100];

    // Data
    problems = signal<ProblemSummary[]>([]);
    isLoading = signal(true);
    error = signal<string | null>(null);
    categories = signal<Category[]>([]);

    // Pagination State
    currentPage = signal(1);
    pageSize = signal(50);
    totalCount = signal(0);
    totalPages = signal(1);
    hasNextPage = signal(false);
    hasPrevPage = signal(false);

    // Filter State
    searchQuery = signal('');
    selectedCategoryIds = signal<string[]>([]);

    // UX States
    showRowTopics = signal(false);
    totalSolved = signal<number | null>(null);
    revealedRows = signal<Set<string>>(new Set<string>());

    private searchSubject = new Subject<string>();
    private problemService = inject(ProblemService);
    private toastService = inject(ToastService);
    private router = inject(Router);
    private route = inject(ActivatedRoute);
    private destroyRef = inject(DestroyRef);
    protected readonly authService = inject(AuthService);
    private readonly userService = inject(UserService);

    visiblePages = computed(() => {
        const total = this.totalPages();
        const current = this.currentPage();
        const start = Math.max(1, current - 2);
        const end = Math.min(total, start + 4);
        const adjustedStart = Math.max(1, end - 4);
        return Array.from({ length: end - adjustedStart + 1 }, (_, index) => adjustedStart + index);
    });

    resultRange = computed(() => {
        if (!this.problems().length) return '0';
        const start = (this.currentPage() - 1) * this.pageSize() + 1;
        const end = start + this.problems().length - 1;
        return `${start}-${end}`;
    });

    hasActiveFilters = computed(() =>
        this.searchQuery().trim().length > 0 || this.selectedCategoryIds().length > 0
    );

    filtersOpen = signal(false);

    constructor() {
        this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
            const page = Number.parseInt(params.get('page') ?? '1', 10);
            const size = Number.parseInt(params.get('size') ?? '50', 10);
            const search = params.get('search') ?? '';
            const cats = params.getAll('cat');

            this.currentPage.set(Number.isFinite(page) && page > 0 ? page : 1);
            this.pageSize.set(this.allowedPageSizes.includes(size) ? size : 50);
            this.searchQuery.set(search);
            this.selectedCategoryIds.set(cats);
            if (cats.length > 0) {
                this.filtersOpen.set(true);
            }
            this.loadProblems();
        });

        // Debounce search input
        this.searchSubject.pipe(
            debounceTime(350),
            distinctUntilChanged(),
            takeUntilDestroyed(this.destroyRef)
        ).subscribe(query => {
            this.navigateWithQueryParams(1, this.pageSize(), query, this.selectedCategoryIds());
        });
    }

    ngOnInit() {
        this.problemService.getCategories().subscribe({
            next: cats => this.categories.set(cats),
            error: err => console.error('Failed to load categories:', err)
        });

        if (this.authService.isLoggedIn$()) {
            this.userService.getMyProfile().subscribe({
                next: profile => this.totalSolved.set(profile.stats.totalSolved),
                error: err => console.error('Failed to load user profile stats:', err)
            });
        }
    }

    toggleRowTopics() {
        this.showRowTopics.update(v => !v);
    }

    toggleRowReveal(event: Event, problemId: string) {
        event.stopPropagation();
        this.revealedRows.update(set => {
            const newSet = new Set(set);
            if (newSet.has(problemId)) {
                newSet.delete(problemId);
            } else {
                newSet.add(problemId);
            }
            return newSet;
        });
    }

    open(id: string) {
        void this.router.navigate(['/problems', id]);
    }

    loadProblems() {
        this.isLoading.set(true);
        this.error.set(null);

        this.problemService.getProblems(
            this.currentPage(),
            this.pageSize(),
            this.searchQuery() || undefined,
            this.selectedCategoryIds().length ? this.selectedCategoryIds() : undefined
        ).subscribe({
            next: (response: ProblemListResponse) => {
                this.problems.set(response.problems);
                this.totalCount.set(response.totalCount);
                this.totalPages.set(response.totalPages);
                this.hasNextPage.set(response.hasNextPage);
                this.hasPrevPage.set(response.hasPreviousPage);
                this.isLoading.set(false);
            },
            error: err => {
                const message = getApiErrorMessage(err, 'Failed to load problems right now.');
                this.error.set(message);
                this.toastService.error(message);
                this.isLoading.set(false);
                console.error(err);
            }
        });
    }

    onSearchInput(query: string) {
        this.searchSubject.next(query);
    }

    toggleCategory(id: string) {
        const current = this.selectedCategoryIds();
        const updated = current.includes(id)
            ? current.filter(c => c !== id)
            : [...current, id];
        this.navigateWithQueryParams(1, this.pageSize(), this.searchQuery(), updated);
    }

    clearFilters() {
        this.navigateWithQueryParams(1, this.pageSize(), '', []);
    }

    onNextPage() {
        if (this.hasNextPage()) {
            this.navigateWithQueryParams(this.currentPage() + 1, this.pageSize(), this.searchQuery(), this.selectedCategoryIds());
        }
    }

    onPrevPage() {
        if (this.hasPrevPage()) {
            this.navigateWithQueryParams(this.currentPage() - 1, this.pageSize(), this.searchQuery(), this.selectedCategoryIds());
        }
    }

    onPageSizeChange(size: number) {
        this.navigateWithQueryParams(1, size, this.searchQuery(), this.selectedCategoryIds());
    }

    goToPage(page: number) {
        if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
        this.navigateWithQueryParams(page, this.pageSize(), this.searchQuery(), this.selectedCategoryIds());
    }

    private navigateWithQueryParams(page: number, size: number, search: string, cats: string[]) {
        const queryParams: Record<string, any> = { page, size };
        if (search.trim()) queryParams['search'] = search.trim();
        if (cats.length) queryParams['cat'] = cats;
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams,
            queryParamsHandling: 'replace'
        });
    }
}
