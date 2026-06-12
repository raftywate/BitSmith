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
    statusFilter = signal<string>('');

    // UX States
    showRowTopics = signal(false);
    totalSolved = signal<number | null>(null);
    currentStreak = signal<number>(0);
    heatmapDays = signal<{date: string, count: number}[]>([]);
    revealedRows = signal<Set<string>>(new Set<string>());
    
    // PoD State
    pod = signal<ProblemSummary | null>(null);
    podSolvedDates = signal<string[]>([]);

    // Calendar State
    currentMonth = signal<Date>(new Date());
    
    calendarDays = computed(() => {
        const year = this.currentMonth().getFullYear();
        const month = this.currentMonth().getMonth();
        const firstDay = new Date(year, month, 1);
        const lastDay = new Date(year, month + 1, 0);
        
        const days = [];
        for (let i = 0; i < firstDay.getDay(); i++) {
            days.push(null);
        }
        
        const todayStr = new Date().toLocaleDateString('en-CA'); // YYYY-MM-DD format
        
        for (let i = 1; i <= lastDay.getDate(); i++) {
            const dateObj = new Date(year, month, i);
            const dateStr = dateObj.toLocaleDateString('en-CA');
            const isFuture = dateObj > new Date();

            days.push({
                date: i,
                dateStr: dateStr,
                isSolved: this.podSolvedDates().includes(dateStr),
                isToday: dateStr === todayStr,
                isFuture: isFuture
            });
        }
        return days;
    });

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
        this.searchQuery().trim().length > 0 || this.selectedCategoryIds().length > 0 || this.statusFilter().trim().length > 0
    );

    filtersOpen = signal(false);

    constructor() {
        this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
            const page = Number.parseInt(params.get('page') ?? '1', 10);
            const size = Number.parseInt(params.get('size') ?? '50', 10);
            const search = params.get('search') ?? '';
            const cats = params.getAll('cat');
            const status = params.get('status') ?? '';

            this.currentPage.set(Number.isFinite(page) && page > 0 ? page : 1);
            this.pageSize.set(this.allowedPageSizes.includes(size) ? size : 50);
            this.searchQuery.set(search);
            this.selectedCategoryIds.set(cats);
            this.statusFilter.set(status);
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
            this.navigateWithQueryParams(1, this.pageSize(), query, this.selectedCategoryIds(), this.statusFilter());
        });
    }

    ngOnInit() {
        this.problemService.getCategories().subscribe({
            next: cats => this.categories.set(cats),
            error: err => console.error('Failed to load categories:', err)
        });

        if (this.authService.isLoggedIn$()) {
            this.userService.getMyProfile().subscribe({
                next: profile => {
                    this.totalSolved.set(profile.stats.totalSolved);
                    // Legacy heatmap data removed as we use PoD calendar
                }
            });

            const todayStr = new Date().toLocaleDateString('en-CA');
            const tzOffset = new Date().getTimezoneOffset();

            this.problemService.getPoDActivity(todayStr, tzOffset).subscribe({
                next: activity => {
                    this.currentStreak.set(activity.currentStreak);
                    this.podSolvedDates.set(activity.solvedDates);
                }
            });
        }

        const todayStr = new Date().toLocaleDateString('en-CA');
        this.problemService.getProblemOfTheDay(todayStr).subscribe({
            next: pod => {
                this.pod.set(pod);
            }
        });
    }

    heatmapLevel(count: number) {
        if (count >= 5) return 4;
        if (count >= 3) return 3;
        if (count >= 1) return 2;
        return 1;
    }

    private buildHeatmapDays(profile: any): { date: string; count: number }[] {
        const activity = new Map<string, number>(
            (profile?.stats?.activity ?? []).map((day: any) => [day.date.slice(0, 10), Number(day.count)])
        );
        const today = new Date();
        const days: { date: string; count: number }[] = [];
        for (let offset = 363; offset >= 0; offset--) {
            const date = new Date(today);
            date.setDate(today.getDate() - offset);
            const key = date.toISOString().slice(0, 10);
            days.push({
                date: key,
                count: activity.get(key) ?? 0
            });
        }
        return days;
    }

    prevMonth() {
        this.currentMonth.update(d => new Date(d.getFullYear(), d.getMonth() - 1, 1));
    }

    nextMonth() {
        this.currentMonth.update(d => new Date(d.getFullYear(), d.getMonth() + 1, 1));
    }
    
    getMonthName() {
        return this.currentMonth().toLocaleString('default', { month: 'long', year: 'numeric' });
    }

    openPoDForDate(dateStr: string, isFuture: boolean) {
        if (isFuture) return;
        this.problemService.getProblemOfTheDay(dateStr).subscribe({
            next: pod => {
                if (pod && (pod.slug || pod.id)) {
                    void this.router.navigate(['/problems', pod.slug || pod.id], { queryParams: { podDate: dateStr } });
                } else {
                    this.toastService.error('No Problem of the Day found for this date.');
                }
            },
            error: () => this.toastService.error('Failed to load Problem of the Day.')
        });
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

    getTodayStr(): string {
        return new Date().toLocaleDateString('en-CA');
    }

    open(id: string, podDate?: string) {
        if (podDate) {
            void this.router.navigate(['/problems', id], { queryParams: { podDate } });
        } else {
            void this.router.navigate(['/problems', id]);
        }
    }

    openInNewTab(id: string, podDate?: string) {
        const url = this.router.serializeUrl(
            this.router.createUrlTree(['/problems', id], podDate ? { queryParams: { podDate } } : undefined)
        );
        window.open(url, '_blank');
    }

    loadProblems() {
        this.isLoading.set(true);
        this.error.set(null);

        this.problemService.getProblems(
            this.currentPage(),
            this.pageSize(),
            this.searchQuery() || undefined,
            this.selectedCategoryIds().length ? this.selectedCategoryIds() : undefined,
            this.statusFilter() || undefined
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
        this.navigateWithQueryParams(1, this.pageSize(), this.searchQuery(), updated, this.statusFilter());
    }

    setStatusFilter(status: string) {
        this.navigateWithQueryParams(1, this.pageSize(), this.searchQuery(), this.selectedCategoryIds(), status);
    }

    clearFilters() {
        this.navigateWithQueryParams(1, this.pageSize(), '', [], '');
    }

    onNextPage() {
        if (this.hasNextPage()) {
            this.navigateWithQueryParams(this.currentPage() + 1, this.pageSize(), this.searchQuery(), this.selectedCategoryIds(), this.statusFilter());
        }
    }

    onPrevPage() {
        if (this.hasPrevPage()) {
            this.navigateWithQueryParams(this.currentPage() - 1, this.pageSize(), this.searchQuery(), this.selectedCategoryIds(), this.statusFilter());
        }
    }

    onPageSizeChange(size: number) {
        this.navigateWithQueryParams(1, size, this.searchQuery(), this.selectedCategoryIds(), this.statusFilter());
    }

    goToPage(page: number) {
        if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
        this.navigateWithQueryParams(page, this.pageSize(), this.searchQuery(), this.selectedCategoryIds(), this.statusFilter());
    }

    private navigateWithQueryParams(page: number, size: number, search: string, cats: string[], status: string) {
        const queryParams: Record<string, any> = { page, size };
        if (search.trim()) queryParams['search'] = search.trim();
        if (cats.length) queryParams['cat'] = cats;
        if (status.trim()) queryParams['status'] = status.trim();
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams,
            queryParamsHandling: 'replace'
        });
    }
}
