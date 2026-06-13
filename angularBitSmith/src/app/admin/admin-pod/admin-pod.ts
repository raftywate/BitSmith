import { Component, DestroyRef, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ProblemService } from '../../services/problem';
import { ToastService } from '../../services/toast';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ProblemSummary } from '../../models/problem-summary';
import { getApiErrorMessage } from '../../utils/api-error';
import { Category } from '../../models/category';

@Component({
  selector: 'app-admin-pod',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterModule],
  templateUrl: './admin-pod.html'
})
export class AdminPodComponent implements OnInit {
  private fb = inject(FormBuilder);
  private problemService = inject(ProblemService);
  private toastService = inject(ToastService);
  private destroyRef = inject(DestroyRef);

  podForm: FormGroup;
  problems = signal<ProblemSummary[]>([]);
  isLoading = signal(false);
  isSubmitting = signal(false);
  searchQuery = signal('');
  selectedProblemId = signal<string | null>(null);
  error = signal<string | null>(null);

  categories = signal<Category[]>([]);
  selectedCategoryIds = signal<string[]>([]);
  todayStr = signal(new Date().toLocaleDateString('en-CA'));

  // Pagination signals
  currentPage = signal(1);
  pageSize = signal(50);
  totalPages = signal(1);
  totalCount = signal(0);
  hasNextPage = signal(false);
  hasPrevPage = signal(false);

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

  private searchSubject = new Subject<string>();

  constructor() {
    this.podForm = this.fb.group({
      dateStr: [new Date().toLocaleDateString('en-CA'), [Validators.required]]
    });

    this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(query => {
      this.searchQuery.set(query);
      this.currentPage.set(1);
      this.loadProblems();
    });
  }

  ngOnInit() {
    this.problemService.getCategories().subscribe({
      next: cats => this.categories.set(cats),
      error: err => console.error('Failed to load categories:', err)
    });
    this.loadProblems();
  }

  onSearchInput(query: string) {
    this.searchSubject.next(query);
  }

  loadProblems() {
    this.isLoading.set(true);
    this.problemService.getProblems(
      this.currentPage(),
      this.pageSize(),
      this.searchQuery(),
      this.selectedCategoryIds()
    ).subscribe({
      next: res => {
        this.problems.set(res.problems);
        this.totalPages.set(res.totalPages);
        this.totalCount.set(res.totalCount);
        this.hasNextPage.set(res.hasNextPage);
        this.hasPrevPage.set(res.hasPreviousPage);
        this.isLoading.set(false);
      },
      error: err => {
        this.toastService.error('Failed to load problems');
        this.isLoading.set(false);
      }
    });
  }

  isDateInPast(dateStr: string): boolean {
    return dateStr < this.todayStr();
  }

  selectProblemAndSet(problem: ProblemSummary) {
    if (this.podForm.invalid) {
      this.toastService.error('Please enter a valid date first.');
      return;
    }

    const dateStr = this.podForm.value.dateStr;
    if (this.isDateInPast(dateStr)) {
      this.toastService.error('Cannot set the Problem of the Day in the past.');
      return;
    }

    this.isSubmitting.set(true);
    this.selectedProblemId.set(problem.id);
    this.error.set(null);

    this.problemService.setProblemOfTheDay(dateStr, problem.id).subscribe({
      next: () => {
        this.toastService.success(`Problem of the Day for ${dateStr} successfully set to "${problem.title}"!`);
        this.isSubmitting.set(false);
      },
      error: err => {
        this.error.set(getApiErrorMessage(err, 'Failed to set Problem of the Day'));
        this.isSubmitting.set(false);
      }
    });
  }

  toggleCategory(id: string) {
    const current = this.selectedCategoryIds();
    if (current.includes(id)) {
      this.selectedCategoryIds.set(current.filter(c => c !== id));
    } else {
      this.selectedCategoryIds.set([...current, id]);
    }
    this.currentPage.set(1);
    this.loadProblems();
  }

  onNextPage() {
    if (this.hasNextPage()) {
      this.currentPage.set(this.currentPage() + 1);
      this.loadProblems();
    }
  }

  onPrevPage() {
    if (this.hasPrevPage()) {
      this.currentPage.set(this.currentPage() - 1);
      this.loadProblems();
    }
  }

  goToPage(page: number) {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadProblems();
  }
}
