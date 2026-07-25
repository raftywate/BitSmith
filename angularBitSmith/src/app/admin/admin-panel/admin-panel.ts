import { CommonModule } from '@angular/common';
import { Component, inject, signal, OnInit, ElementRef, ViewChild, DestroyRef } from '@angular/core';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { AdminService } from '../../services/admin';
import { ProblemService } from '../../services/problem';
import { ToastService } from '../../services/toast';
import { Category } from '../../models/category';
import { getApiErrorMessage } from '../../utils/api-error';
import { MarkdownRenderPipe } from '../../pipes/markdown-render.pipe';
import { ProblemSummary } from '../../models/problem-summary';
import { ProblemDetail, SampleTestCase } from '../../models/problem-detail';

export type AdminStep = 'problem' | 'testcases' | 'review' | 'done';

@Component({
  selector: 'app-admin-panel',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, MarkdownRenderPipe],
  templateUrl: './admin-panel.html',
  styleUrl: './admin-panel.scss'
})
export class AdminPanelComponent implements OnInit {
  @ViewChild('fileInput') fileInputRef!: ElementRef<HTMLInputElement>;
  @ViewChild('descriptionInput') descriptionInputRef!: ElementRef<HTMLTextAreaElement>;

  private fb = inject(FormBuilder);
  private adminService = inject(AdminService);
  private problemService = inject(ProblemService);
  private toastService = inject(ToastService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);

  private searchSubject = new Subject<string>();

  constructor() {
    this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(query => {
      this.problemSearch.set(query);
      if (query.trim()) {
        this.searchExistingProblems();
      } else {
        this.existingProblems.set([]);
      }
    });

    this.problemForm.get('hasHints')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(has => {
      if (has && this.hintsArray.length === 0) {
        this.addHint();
      }
    });
  }

  categories = signal<Category[]>([]);
  existingProblems = signal<ProblemSummary[]>([]);
  isEditRoute = signal(false);
  isSubmitting = signal(false);
  isLoadingExistingProblems = signal(false);
  isLoadingEditProblem = signal(false);
  createdProblemId = signal<string | null>(null);
  editingProblemId = signal<string | null>(null);
  problemSearch = signal('');
  isAddingTestCases = signal(false);
  isUploadingFile = signal(false);
  activeStep = signal<AdminStep>('problem');

  difficulties = ['Easy', 'Medium', 'Hard'];
  selectedCategories = signal<string[]>([]);

  // Collapsible state for categories section
  categoriesOpen = signal(false);
  descriptionPreviewOpen = signal(true);

  // File upload state
  selectedFile = signal<File | null>(null);
  uploadMode = signal<'manual' | 'file'>('manual');

  activeStarterLang = signal('csharp');
  starterLanguages = [
    { value: 'csharp', label: 'C#' },
    { value: 'python', label: 'Python' },
    { value: 'java', label: 'Java' },
    { value: 'cpp', label: 'C++' },
    { value: 'c', label: 'C' }
  ];

  reviewTestCaseIndex = signal(0);

  problemForm: FormGroup = this.fb.group({
    title: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(100)]],
    description: ['', [Validators.required, Validators.minLength(10)]],
    difficulty: ['Easy', Validators.required],
    metaDataJson: [''],
    hasHints: [false],
    hints: this.fb.array([]),
    starterCode: this.fb.group({
      csharp: [''],
      python: [''],
      java: [''],
      cpp: [''],
      c: ['']
    })
  });

  testCasesForm: FormGroup = this.fb.group({
    sharedInputLabels: [''],
    testCases: this.fb.array([this.createTestCaseRow(false), this.createTestCaseRow(true)])
  });

  get testCasesArray(): FormArray {
    return this.testCasesForm.get('testCases') as FormArray;
  }

  get hintsArray(): FormArray {
    return this.problemForm.get('hints') as FormArray;
  }

  get isEditMode(): boolean {
    return !!this.editingProblemId();
  }

  get sampleCases() {
    return this.testCasesArray.controls.filter(c => !c.get('isHidden')?.value);
  }

  get hiddenCases() {
    return this.testCasesArray.controls.filter(c => c.get('isHidden')?.value);
  }

  get sampleCaseIndices() {
    return this.testCasesArray.controls
      .map((c, i) => ({ control: c, index: i }))
      .filter(x => !x.control.get('isHidden')?.value);
  }

  get hiddenCaseIndices() {
    return this.testCasesArray.controls
      .map((c, i) => ({ control: c, index: i }))
      .filter(x => x.control.get('isHidden')?.value);
  }

  get selectedCategoryNames(): string[] {
    const cats = this.categories();
    return this.selectedCategories().map(id => cats.find(c => c.id === id)?.name ?? '').filter(Boolean);
  }

  ngOnInit() {
    this.isEditRoute.set(this.router.url.includes('/edit'));
    this.problemService.getCategories().subscribe({
      next: cats => this.categories.set(cats),
      error: err => console.error('Failed to load categories:', err)
    });

    const problemId = this.route.snapshot.queryParamMap.get('problemId');
    if (problemId) {
      this.loadProblemForEdit(problemId);
    }
  }

  createHintControl(value = '') {
    return this.fb.control(value);
  }

  createTestCaseRow(isHidden = true): FormGroup {
    return this.fb.group({
      input: ['', Validators.required],
      inputLabelsText: [''],
      expectedOutput: ['', Validators.required],
      isHidden: [isHidden]
    });
  }

  createTestCaseRowFromModel(testCase: SampleTestCase): FormGroup {
    return this.fb.group({
      input: [testCase.input, Validators.required],
      inputLabelsText: [(testCase.inputLabels ?? []).join('\n')],
      expectedOutput: [testCase.expectedOutput, Validators.required],
      isHidden: [!!testCase.isHidden]
    });
  }

  addSampleCase() {
    this.testCasesArray.push(this.createTestCaseRow(false));
  }

  addHiddenCase() {
    this.testCasesArray.push(this.createTestCaseRow(true));
  }

  removeTestCase(i: number) {
    this.testCasesArray.removeAt(i);
  }

  addHint() {
    this.hintsArray.push(this.createHintControl());
  }

  removeHint(index: number) {
    this.hintsArray.removeAt(index);
    if (!this.hintsArray.length && this.problemForm.get('hasHints')?.value) {
      this.addHint();
    }
  }

  onSearchInput(query: string) {
    this.problemSearch.set(query);
    this.searchSubject.next(query);
  }

  searchExistingProblems() {
    this.isLoadingExistingProblems.set(true);
    this.problemService.getProblems(1, 25, this.problemSearch()).subscribe({
      next: result => {
        this.existingProblems.set(result.problems);
        this.isLoadingExistingProblems.set(false);
      },
      error: err => {
        this.isLoadingExistingProblems.set(false);
        this.toastService.error(getApiErrorMessage(err, 'Unable to load existing problems.'));
      }
    });
  }

  loadProblemForEdit(problemId: string) {
    this.isLoadingEditProblem.set(true);
    this.problemService.getProblemById(problemId).subscribe({
      next: problem => {
        this.populateProblemForm(problem);
        this.editingProblemId.set(problem.id);
        this.createdProblemId.set(problem.id);
        this.activeStep.set('problem');
        this.isLoadingEditProblem.set(false);
        this.toastService.success(`Editing "${problem.title}".`);
      },
      error: err => {
        this.isLoadingEditProblem.set(false);
        this.toastService.error(getApiErrorMessage(err, 'Unable to load problem for editing.'));
      }
    });
  }

  cancelEdit() {
    this.editingProblemId.set(null);
    this.createdProblemId.set(null);
    this.createAnother();
  }

  toggleCategory(id: string) {
    const current = this.selectedCategories();
    if (current.includes(id)) {
      this.selectedCategories.set(current.filter(c => c !== id));
    } else {
      this.selectedCategories.set([...current, id]);
    }
  }

  isCategorySelected(id: string): boolean {
    return this.selectedCategories().includes(id);
  }

  insertDescriptionSnippet(snippet: string, selectedTextFallback = 'text') {
    const control = this.problemForm.get('description');
    const textarea = this.descriptionInputRef?.nativeElement;
    const current = control?.value ?? '';

    if (!control || !textarea) {
      control?.setValue(`${current}${snippet}`);
      return;
    }

    const start = textarea.selectionStart ?? current.length;
    const end = textarea.selectionEnd ?? current.length;
    const selectedText = current.slice(start, end) || selectedTextFallback;
    const textToInsert = snippet.replace('{{text}}', selectedText);
    const nextValue = `${current.slice(0, start)}${textToInsert}${current.slice(end)}`;

    control.setValue(nextValue);
    control.markAsDirty();
    control.markAsTouched();

    queueMicrotask(() => {
      textarea.focus();
      const selectionStart = start + textToInsert.length;
      textarea.setSelectionRange(selectionStart, selectionStart);
    });
  }

  onCodeKeyDown(event: KeyboardEvent) {
    const target = event.target as HTMLTextAreaElement;
    const start = target.selectionStart;
    const end = target.selectionEnd;
    const value = target.value;

    // 1. TAB & SHIFT+TAB
    if (event.key === 'Tab') {
      event.preventDefault();
      const indent = '    ';
      if (event.shiftKey) {
        if (start === end && value.substring(start - 4, start) === indent) {
          target.value = value.substring(0, start - 4) + value.substring(end);
          target.selectionStart = target.selectionEnd = start - 4;
        }
      } else {
        target.value = value.substring(0, start) + indent + value.substring(end);
        target.selectionStart = target.selectionEnd = start + indent.length;
      }
      target.dispatchEvent(new Event('input', { bubbles: true }));
      return;
    }

    // 2. AUTO-INDENT ON ENTER KEY
    if (event.key === 'Enter') {
      event.preventDefault();
      const lineStart = value.lastIndexOf('\n', start - 1) + 1;
      const currentLine = value.substring(lineStart, start);
      const leadingMatch = currentLine.match(/^[ \t]*/);
      let indent = leadingMatch ? leadingMatch[0] : '';
      
      const trimmedLine = currentLine.trimEnd();
      const lastChar = trimmedLine.slice(-1);
      if (['{', '[', '(', ':'].includes(lastChar)) {
        indent += '    ';
      }

      const insertText = '\n' + indent;
      target.value = value.substring(0, start) + insertText + value.substring(end);
      target.selectionStart = target.selectionEnd = start + insertText.length;
      target.dispatchEvent(new Event('input', { bubbles: true }));
      return;
    }

    // 3. AUTO-CLOSING PAIRS ({}, [], (), "", '')
    const pairs: Record<string, string> = {
      '{': '}',
      '[': ']',
      '(': ')',
      '"': '"',
      "'": "'"
    };

    if (pairs[event.key] && start === end) {
      const closeChar = pairs[event.key];
      if ((event.key === '"' || event.key === "'") && value[start] === event.key) {
        event.preventDefault();
        target.selectionStart = target.selectionEnd = start + 1;
        return;
      }
      event.preventDefault();
      target.value = value.substring(0, start) + event.key + closeChar + value.substring(end);
      target.selectionStart = target.selectionEnd = start + 1;
      target.dispatchEvent(new Event('input', { bubbles: true }));
      return;
    }

    // 4. BACKSPACE PAIR DELETE
    if (event.key === 'Backspace' && start === end && start > 0) {
      const prevChar = value[start - 1];
      const nextChar = value[start];
      if (
        (prevChar === '{' && nextChar === '}') ||
        (prevChar === '[' && nextChar === ']') ||
        (prevChar === '(' && nextChar === ')') ||
        (prevChar === '"' && nextChar === '"') ||
        (prevChar === "'" && nextChar === "'")
      ) {
        event.preventDefault();
        target.value = value.substring(0, start - 1) + value.substring(start + 1);
        target.selectionStart = target.selectionEnd = start - 1;
        target.dispatchEvent(new Event('input', { bubbles: true }));
        return;
      }
    }
  }

  onDescriptionKeyDown(event: KeyboardEvent) {
    if (event.key === 'Tab') {
      this.onCodeKeyDown(event);
      return;
    }
    const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
    const modifier = isMac ? event.metaKey : event.ctrlKey;
    if (modifier) {
      const key = event.key.toLowerCase();
      if (key === 'b') {
        event.preventDefault();
        this.insertDescriptionSnippet('**{{text}}**');
      } else if (key === 'i') {
        event.preventDefault();
        this.insertDescriptionSnippet('*{{text}}*');
      } else if (key === 'u') {
        event.preventDefault();
        this.insertDescriptionSnippet('<u>{{text}}</u>');
      } else if (key === 'x' && event.shiftKey) {
        event.preventDefault();
        this.insertDescriptionSnippet('~~{{text}}~~');
      }
    }
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedFile.set(file);
  }

  clearFile() {
    this.selectedFile.set(null);
    if (this.fileInputRef?.nativeElement) {
      this.fileInputRef.nativeElement.value = '';
    }
  }

  hasStarterCode(lang: string): boolean {
    const starterCodeGroup = this.problemForm.get('starterCode') as FormGroup;
    return !!starterCodeGroup?.get(lang)?.value?.trim();
  }

  onSubmitProblem() {
    if (this.problemForm.invalid) {
      this.problemForm.markAllAsTouched();
      this.toastService.error('Please fix the errors in the problem details form before continuing.');
      return;
    }

    const val = this.problemForm.getRawValue();
    let metaJson = val.metaDataJson?.trim();
    if (!metaJson) {
      const rawStarterCode = val.starterCode || {};
      const code = rawStarterCode.python || rawStarterCode.csharp || rawStarterCode.java || rawStarterCode.cpp || rawStarterCode.c || '';
      const labels = (val.sharedInputLabels || '')
        .split('\n')
        .map((l: string) => l.trim())
        .filter((l: string) => l.length > 0);
      const generated = this.inferMetadataFromCode(code, labels);
      metaJson = JSON.stringify(generated);
    } else {
      try {
        JSON.parse(metaJson);
      } catch (e) {
        this.toastService.error('Method Signature Metadata must be a valid JSON object.');
        return;
      }
    }

    this.isSubmitting.set(true);

    // Build starter code dictionary
    const rawStarterCode = val.starterCode || {};
    const nonEmptyCodes: Record<string, string> = {};
    for (const key of Object.keys(rawStarterCode)) {
      const code = rawStarterCode[key]?.trim();
      if (code) {
        nonEmptyCodes[key] = code;
      }
    }
    const starterCodeJson = Object.keys(nonEmptyCodes).length > 0
      ? JSON.stringify(nonEmptyCodes)
      : undefined;

    const payload = {
      title: val.title,
      description: val.description,
      difficulty: val.difficulty,
      starterCode: starterCodeJson,
      metaDataJson: metaJson,
      hints: this.getCleanHints(),
      categoryIDs: this.selectedCategories()
    };

    const request = this.isEditMode
      ? this.adminService.updateProblem(this.editingProblemId()!, payload)
      : this.adminService.createProblem(payload);

    request.subscribe({
      next: (problem) => {
        this.createdProblemId.set(problem.id);
        // By setting editingProblemId, going "Previous" from Step 2 will treat it as an edit 
        this.editingProblemId.set(problem.id);
        this.isSubmitting.set(false);
        this.toastService.success(`Problem "${problem.title}" ${this.isEditMode ? 'updated' : 'created'}!`);
        this.activeStep.set('testcases');
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toastService.error(getApiErrorMessage(err, 'Failed to save problem details.'));
      }
    });
  }

  goToReview() {
    if (this.uploadMode() === 'manual') {
      const hasManualCases = this.testCasesArray.length > 0;
      if (!hasManualCases) {
        this.toastService.error('Please add at least one test case.');
        return;
      }
      if (this.testCasesForm.invalid) {
        this.testCasesForm.markAllAsTouched();
        this.toastService.error('Please fill in all required fields for the test cases.');
        return;
      }
    } else {
      // file mode
      if (!this.selectedFile()) {
        this.toastService.error('Please select a file to upload before proceeding.');
        return;
      }
    }
    this.activeStep.set('review');
  }

  goBackToProblem() {
    this.activeStep.set('problem');
  }

  goBackToTestCases() {
    this.activeStep.set('testcases');
  }

  onSubmitTestCases() {
    const problemId = this.createdProblemId();
    if (!problemId) return;

    if (this.uploadMode() === 'file') {
      const file = this.selectedFile();
      if (!file) {
        this.toastService.error('Please select a file to upload.');
        return;
      }
      this.isUploadingFile.set(true);
      this.adminService.uploadTestCasesFile(problemId, file).subscribe({
        next: () => {
          this.isUploadingFile.set(false);
          this.toastService.success(`Test cases uploaded from ${file.name}!`);
          this.activeStep.set('done');
        },
        error: (err) => {
          this.isUploadingFile.set(false);
          this.toastService.error(getApiErrorMessage(err, 'Failed to upload test cases.'));
        }
      });
      return;
    }

    // Manual mode — validate manual form
    const hasManualCases = this.testCasesArray.length > 0;
    if (!hasManualCases) {
      this.toastService.error('Please add at least one test case.');
      return;
    }
    if (this.testCasesForm.invalid) {
      this.testCasesForm.markAllAsTouched();
      this.toastService.error('Please fix the errors in the test cases before submitting.');
      return;
    }

    const testCases = this.getTestCasePayload();
    if (!testCases.length) {
      this.activeStep.set('done');
      return;
    }

    this.isAddingTestCases.set(true);
    const request = this.isEditMode
      ? this.adminService.replaceTestCases(problemId, testCases)
      : this.adminService.addTestCases(problemId, testCases);

    request.subscribe({
      next: () => {
        this.isAddingTestCases.set(false);
        this.toastService.success(`Test cases ${this.isEditMode ? 'updated' : 'added'} successfully!`);
        this.activeStep.set('done');
      },
      error: (err) => {
        this.isAddingTestCases.set(false);
        this.toastService.error(getApiErrorMessage(err, 'Failed to add test cases.'));
      }
    });
  }

  skipTestCases() {
    this.activeStep.set('done');
  }

  goToProblem() {
    const id = this.createdProblemId();
    if (id) void this.router.navigate(['/problems', id]);
  }

  autoGenerateMetadata() {
    const starterGroup = this.problemForm.get('starterCode') as FormGroup;
    const codes = starterGroup?.value || {};
    const code = codes.python || codes.csharp || codes.java || codes.cpp || codes.c || '';
    const labels = (this.problemForm.get('sharedInputLabels')?.value || '')
      .split('\n')
      .map((l: string) => l.trim())
      .filter((l: string) => l.length > 0);

    const generated = this.inferMetadataFromCode(code, labels);
    this.problemForm.patchValue({
      metaDataJson: JSON.stringify(generated, null, 2)
    });
    this.toastService.success('Metadata JSON auto-generated from starter code!');
  }

  private inferMetadataFromCode(code: string, labels: string[]) {
    let methodName = 'solve';
    let returnType = 'integer[]';
    let params: { name: string; type: string }[] = [];

    const pyMatch = code.match(/def\s+([a-zA-Z0-9_]+)\s*\(([^)]*)\)\s*(?:->\s*([^:]+))?:/);
    if (pyMatch) {
      methodName = pyMatch[1];
      const rawParams = pyMatch[2].replace(/\bself\b,?\s*/, '').trim();
      const rawReturn = pyMatch[3] ? pyMatch[3].trim() : '';

      if (rawReturn) {
        returnType = this.mapTypeToMeta(rawReturn);
      }

      if (rawParams) {
        const paramPairs = rawParams.split(',');
        params = paramPairs.map((p, idx) => {
          const parts = p.split(':');
          const pName = parts[0].trim() || labels[idx] || `param${idx + 1}`;
          const pType = parts[1] ? this.mapTypeToMeta(parts[1].trim()) : 'integer';
          return { name: pName, type: pType };
        });
      }
    } else {
      const cLikeMatch = code.match(/(?:public|private|protected|static|\s)*([\w<>[\]]+)\s+([a-zA-Z0-9_]+)\s*\(([^)]*)\)/);
      if (cLikeMatch && cLikeMatch[2] !== 'class' && cLikeMatch[2] !== 'Solution') {
        returnType = this.mapTypeToMeta(cLikeMatch[1]);
        methodName = cLikeMatch[2];
        const rawParams = cLikeMatch[3].trim();
        if (rawParams) {
          const paramPairs = rawParams.split(',');
          params = paramPairs.map((p, idx) => {
            const tokens = p.trim().split(/\s+/);
            let pType = 'integer';
            let pName = labels[idx] || `param${idx + 1}`;
            if (tokens.length >= 2) {
              pType = this.mapTypeToMeta(tokens[0]);
              pName = tokens[1].replace(/[&*]/g, '');
            } else if (tokens.length === 1) {
              pType = this.mapTypeToMeta(tokens[0]);
            }
            return { name: pName, type: pType };
          });
        }
      }
    }

    if (params.length === 0 && labels.length > 0) {
      params = labels.map(label => ({ name: label, type: 'integer[]' }));
    } else if (params.length === 0) {
      params = [{ name: 'nums', type: 'integer[]' }];
    }

    return {
      name: methodName,
      params: params,
      return: { type: returnType }
    };
  }

  private mapTypeToMeta(typeStr: string): string {
    const typeLower = typeStr.toLowerCase();
    const isDoubleArray = typeLower.includes('[][]') || typeLower.includes('list<list') || typeLower.includes('vector<vector');
    const isArray = isDoubleArray || typeStr.includes('[') || typeLower.includes('vector') || typeLower.includes('list');

    const clean = typeStr.replace(/vector<|List<|list<|>|&|\*|\[|\]/g, '').trim().toLowerCase();
    
    let base = 'integer';
    if (clean.includes('string') || clean.includes('str')) base = 'string';
    else if (clean.includes('double') || clean.includes('float')) base = 'double';
    else if (clean.includes('long')) base = 'long';
    else if (clean.includes('bool')) base = 'boolean';
    else if (clean.includes('char')) base = 'character';
    else if (clean.includes('listnode')) base = 'ListNode';
    else if (clean.includes('treenode')) base = 'TreeNode';

    if (isDoubleArray) return `${base}[][]`;
    if (isArray) return `${base}[]`;
    return base;
  }

  createAnother() {
    this.problemForm.reset({ difficulty: 'Easy', hasHints: false });
    this.problemForm.setControl('hints', this.fb.array([]));
    this.selectedCategories.set([]);
    this.testCasesForm.reset({ sharedInputLabels: '' });
    this.testCasesForm.setControl('testCases', this.fb.array([this.createTestCaseRow(false), this.createTestCaseRow(true)]));
    this.createdProblemId.set(null);
    this.editingProblemId.set(null);
    this.selectedFile.set(null);
    this.uploadMode.set('manual');
    this.activeStarterLang.set('csharp');
    this.activeStep.set('problem');
  }

  private populateProblemForm(problem: ProblemDetail) {
    const starterCode = this.parseStarterCode(problem.starterCode);
    const hasHints = !!(problem.hints?.length);
    this.problemForm.patchValue({
      title: problem.title,
      description: problem.description,
      difficulty: problem.difficulty,
      starterCode,
      metaDataJson: problem.metaDataJson || '',
      hasHints
    });

    const hints = problem.hints?.length ? problem.hints : [];
    this.problemForm.setControl('hints', this.fb.array(hints.map(hint => this.createHintControl(hint))));
    this.selectedCategories.set(problem.categories.map(category => category.id));

    const editableTestCases = problem.testCases?.length ? problem.testCases : problem.sampleTestCases;
    
    const firstLabels = editableTestCases?.[0]?.inputLabels ?? [];
    this.testCasesForm.patchValue({
      sharedInputLabels: firstLabels.join('\n')
    });

    this.testCasesForm.setControl(
      'testCases',
      this.fb.array(editableTestCases.length
        ? editableTestCases.map(testCase => this.createTestCaseRowFromModel(testCase))
        : [this.createTestCaseRow(false), this.createTestCaseRow(true)])
    );
  }

  private parseStarterCode(starterCode: string | null) {
    const fallback = { csharp: '', python: '', java: '', cpp: '', c: '' };
    if (!starterCode?.trim()) {
      return fallback;
    }

    try {
      return { ...fallback, ...JSON.parse(starterCode) };
    } catch {
      return { ...fallback, csharp: starterCode };
    }
  }

  getCleanHints(): string[] {
    if (!this.problemForm.get('hasHints')?.value) {
      return [];
    }
    return this.hintsArray.getRawValue()
      .map((hint: string) => hint.trim())
      .filter((hint: string) => !!hint);
  }

  private getTestCasePayload() {
    const sharedLabels = this.parseInputLabels(this.testCasesForm.get('sharedInputLabels')?.value);
    return this.testCasesArray.getRawValue().map(testCase => ({
      input: testCase.input,
      expectedOutput: testCase.expectedOutput,
      inputLabels: sharedLabels,
      isHidden: testCase.isHidden
    }));
  }

  parseInputLabels(value: string | null | undefined): string[] {
    return (value ?? '')
      .split(/\r?\n|,/)
      .map(label => label.trim())
      .filter(label => !!label);
  }

  getReviewTestCaseInputParts(control: any) {
    const inputVal = control.get('input')?.value ?? '';
    const values = inputVal.split(/\r?\n/);
    const sharedLabelsText = this.testCasesForm.get('sharedInputLabels')?.value;
    const labels = this.parseInputLabels(sharedLabelsText);

    return values.map((value: string, index: number) => ({
      label: labels[index]?.trim() || `Input ${index + 1}`,
      value
    }));
  }
}
