import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
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
      this.loadProblems();
    });
  }

  ngOnInit() {
    this.loadProblems();
  }

  onSearchInput(query: string) {
    this.searchSubject.next(query);
  }

  loadProblems() {
    this.isLoading.set(true);
    this.problemService.getProblems(1, 20, this.searchQuery()).subscribe({
      next: res => {
        this.problems.set(res.problems);
        this.isLoading.set(false);
      },
      error: err => {
        this.toastService.error('Failed to load problems');
        this.isLoading.set(false);
      }
    });
  }

  selectProblem(id: string) {
    this.selectedProblemId.set(id);
  }

  onSubmit() {
    if (this.podForm.invalid || !this.selectedProblemId()) return;

    this.isSubmitting.set(true);
    this.error.set(null);

    const dateStr = this.podForm.value.dateStr;
    const problemId = this.selectedProblemId()!;

    this.problemService.setProblemOfTheDay(dateStr, problemId).subscribe({
      next: () => {
        this.toastService.success('Problem of the Day set successfully!');
        this.isSubmitting.set(false);
      },
      error: err => {
        this.error.set(getApiErrorMessage(err, 'Failed to set Problem of the Day'));
        this.isSubmitting.set(false);
      }
    });
  }
}
