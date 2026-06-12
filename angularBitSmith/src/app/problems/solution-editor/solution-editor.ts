import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CommunityService } from '../../services/community';
import { ProblemService } from '../../services/problem';
import { ToastService } from '../../services/toast';
import { MarkdownRenderPipe } from '../../pipes/markdown-render.pipe';
import { AngularSplitModule } from 'angular-split';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';

@Component({
  selector: 'app-solution-editor',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MarkdownRenderPipe,
    AngularSplitModule,
    MonacoEditorModule
  ],
  templateUrl: './solution-editor.html',
  styleUrl: './solution-editor.scss'
})
export class SolutionEditorComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private location = inject(Location);
  private fb = inject(FormBuilder);
  private communityService = inject(CommunityService);
  private problemService = inject(ProblemService);
  private toastService = inject(ToastService);

  private destroy$ = new Subject<void>();

  problemId = signal<string>('');
  resolvedProblemId = signal<string>('');
  solutionId = signal<string | null>(null);
  isEditMode = computed(() => !!this.solutionId());
  
  isSubmitting = signal(false);
  isLoading = signal(true);

  solutionForm = this.fb.group({
    title: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(100)]],
    content: ['', [Validators.required, Validators.minLength(50)]]
  });

  editorOptions = {
    language: 'markdown',
    theme: 'vs-dark',
    minimap: { enabled: false },
    wordWrap: 'on',
    fontSize: 14,
    lineNumbers: 'on',
    padding: { top: 16, bottom: 16 },
    scrollBeyondLastLine: false,
    automaticLayout: true
  };

  previewContent = signal<string>('');

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const problemId = params.get('id');
      const solutionId = params.get('solutionId'); // Will be present if in edit route

      if (!problemId) {
        this.router.navigate(['/problems']);
        return;
      }

      this.problemId.set(problemId);
      
      this.problemService.getProblemById(problemId).subscribe({
        next: (problem) => {
          this.resolvedProblemId.set(problem.id);
          
          if (solutionId) {
            this.solutionId.set(solutionId);
            this.loadSolution(solutionId);
          } else {
            this.isLoading.set(false);
          }
        },
        error: () => {
          this.toastService.error('Failed to load problem details.');
          this.router.navigate(['/problems']);
        }
      });
    });

    this.solutionForm.controls.content.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(value => {
        this.previewContent.set(value || '');
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadSolution(id: string) {
    this.communityService.getSolutionById(id).subscribe({
      next: (solution) => {
        this.solutionForm.patchValue({
          title: solution.title,
          content: solution.content
        });
        this.previewContent.set(solution.content);
        this.isLoading.set(false);
      },
      error: () => {
        this.toastService.error('Failed to load solution');
        this.router.navigate(['/problems', this.problemId()]);
      }
    });
  }

  goBack() {
    this.location.back();
  }

  publishSolution() {
    if (this.solutionForm.invalid) {
      this.solutionForm.markAllAsTouched();
      
      const titleCtrl = this.solutionForm.controls.title;
      const contentCtrl = this.solutionForm.controls.content;
      
      if (titleCtrl.invalid) {
        this.toastService.warning('Please enter a valid title (10-100 characters).');
      } else if (contentCtrl.invalid) {
        this.toastService.warning('Please enter a valid explanation (minimum 50 characters).');
      }
      
      return;
    }

    this.isSubmitting.set(true);
    const payload = this.solutionForm.getRawValue() as { title: string; content: string };

    if (this.isEditMode()) {
      this.communityService.updateSolution(this.solutionId()!, payload).subscribe({
        next: () => {
          this.toastService.success('Solution updated successfully');
          this.router.navigate(['/problems', this.problemId()]);
        },
        error: () => {
          this.toastService.error('Failed to update solution');
          this.isSubmitting.set(false);
        }
      });
    } else {
      this.communityService.createSolution({
        problemId: this.resolvedProblemId() || this.problemId(),
        ...payload
      }).subscribe({
        next: () => {
          this.toastService.success('Solution published successfully');
          this.router.navigate(['/problems', this.problemId()]);
        },
        error: () => {
          this.toastService.error('Failed to publish solution');
          this.isSubmitting.set(false);
        }
      });
    }
  }
}
