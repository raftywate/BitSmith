import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, HostListener, inject, signal, OnDestroy, effect, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators, FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AngularSplitModule, SplitGutterInteractionEvent } from 'angular-split';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { SkeletonComponent } from '../../components/skeleton/skeleton';
import {
  CommentViewModel,
  SolutionDetail,
  SolutionSummary,
  VoteChoice
} from '../../models/community';
import { ProblemDetail, SampleTestCase } from '../../models/problem-detail';
import { ProblemDifficulty } from '../../models/problem-difficulty.enum';
import {
  EDITOR_LANGUAGE_OPTIONS,
  EditorLanguage,
  SampleRunResult,
  SubmissionDetail,
  SubmissionResult,
  SubmissionStatus
} from '../../models/submission';
import { AuthService } from '../../services/auth';
import { CommunityService } from '../../services/community';
import { ProblemService } from '../../services/problem';
import { SubmissionService } from '../../services/submission';
import { ThemeService } from '../../services/theme';
import { ToastService } from '../../services/toast';
import { UserService } from '../../services/user';
import { WorkspaceActionService } from '../../services/workspace-action';
import { UserPreferencesUpdateRequest } from '../../models/user-profile';
import { getApiErrorMessage } from '../../utils/api-error';
import { MarkdownRenderPipe } from '../../pipes/markdown-render.pipe';
import { finalize, switchMap, takeWhile, catchError } from 'rxjs/operators';
import { interval, of, combineLatest } from 'rxjs';

type InfoPanelId = 'description' | 'solutions' | 'submissions';
type WorkPanelId = 'editor' | 'result' | 'tests' | 'history';
type DockPanelId = InfoPanelId | WorkPanelId;
type DockZoneId = 'leftTop' | 'leftBottom' | 'rightTop' | 'rightBottom';
type DockColumnId = 'left' | 'right';
type EditorThemeChoice = 'system' | 'light' | 'dark' | 'contrast';

interface SolutionUiModel extends SolutionSummary {
  detail: SolutionDetail | null;
  isExpanded: boolean;
  isDetailLoading: boolean;
  comments: CommentViewModel[];
  commentsPage: number;
  hasMoreComments: boolean;
  isCommentsLoading: boolean;
  userVote: VoteChoice;
}

interface DockPanelDefinition {
  id: DockPanelId;
  label: string;
  description: string;
}

interface DockZoneState {
  tabs: DockPanelId[];
  activeTab: DockPanelId | null;
}

interface DockDropState {
  mode: 'zone' | 'below';
  targetId: DockZoneId | DockColumnId;
}

@Component({
  selector: 'app-problem-detail',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    AngularSplitModule,
    MonacoEditorModule,
    SkeletonComponent,
    MarkdownRenderPipe
  ],
  templateUrl: './problem-detail.html',
  styleUrl: './problem-detail.scss'
})
export class ProblemDetailComponent implements OnDestroy {
  protected readonly ProblemDifficulty = ProblemDifficulty;
  protected readonly editorLanguages = EDITOR_LANGUAGE_OPTIONS;

  private readonly commentsPageSize = 5;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  private readonly problemService = inject(ProblemService);
  private readonly communityService = inject(CommunityService);
  private readonly submissionService = inject(SubmissionService);
  protected readonly authService = inject(AuthService);
  private readonly themeService = inject(ThemeService);
  private readonly toastService = inject(ToastService);
  private readonly userService = inject(UserService);
  private readonly workspaceActionService = inject(WorkspaceActionService);

  readonly editorFontSize = signal<number>(this.getInitialEditorFontSize());
  readonly editorTabSize = signal<number>(this.getInitialEditorTabSize());
  readonly editorFontFamily = signal<string>(this.getInitialEditorFontFamily());
  readonly editorSettingsOpen = signal(false);
  private editorInstance: any = null;

  readonly mainSplitSizesHorizontal = signal<number[]>(this.getInitialMainSplitSizesHorizontal());
  readonly mainSplitSizesVertical = signal<number[]>(this.getInitialMainSplitSizesVertical());
  readonly leftSplitSizes = signal<number[]>(this.getInitialLeftSplitSizes());
  readonly rightSplitSizes = signal<number[]>(this.getInitialRightSplitSizes());
  readonly selectedSubmission = signal<SubmissionDetail | null>(null);

  readonly problem = signal<ProblemDetail | null>(null);
  readonly problemId = signal<string | null>(null);
  readonly problemError = signal<string | null>(null);
  readonly isLoadingProblem = signal(true);
  readonly topicsOpen = signal(false);

  toggleTopics() {
    this.topicsOpen.update(v => !v);
  }

  scrollToHints() {
    const el = document.getElementById('hints-section');
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  readonly dockPanelDefinitions: ReadonlyArray<DockPanelDefinition> = [
    {
      id: 'description',
      label: 'Description',
      description: 'Read the prompt, examples, and constraints.'
    },
    {
      id: 'solutions',
      label: 'Solutions',
      description: 'Browse community write-ups, votes, and threaded discussion.'
    },
    {
      id: 'submissions',
      label: 'Submissions',
      description: 'Review your personal attempts for this problem.'
    },
    {
      id: 'editor',
      label: 'Code',
      description: 'Write and edit your solution in Monaco.'
    },
    {
      id: 'tests',
      label: 'Test Cases',
      description: 'Inspect sample cases and compare expected and actual output.'
    },
    {
      id: 'result',
      label: 'Test Result',
      description: 'See the latest judge result for your submission.'
    }
  ];

  readonly dockZones = signal<Record<DockZoneId, DockZoneState>>(this.getInitialDockZones());
  readonly draggedDockPanel = signal<DockPanelId | null>(null);
  readonly dockDropTarget = signal<DockDropState | null>(null);
  readonly fullscreenZoneId = signal<DockZoneId | null>(null);
  readonly isCompactWorkspace = signal(this.readIsCompactWorkspace());
  readonly mainSplitDirection = computed(() => (this.isCompactWorkspace() ? 'vertical' : 'horizontal'));

  readonly editorLanguage = signal<EditorLanguage>(this.getInitialLanguage());
  readonly editorTheme = signal<EditorThemeChoice>(this.getInitialEditorTheme());
  readonly isEditorFullscreen = signal(false);
  readonly code = signal('');
  readonly isSubmitting = signal(false);
  readonly isRunningSamples = signal(false);
  readonly editorThemeOptions: Array<{ value: EditorThemeChoice; label: string }> = [
    { value: 'system', label: 'System' },
    { value: 'light', label: 'Light' },
    { value: 'dark', label: 'Dark' },
    { value: 'contrast', label: 'High contrast' }
  ];

  readonly solutions = signal<SolutionUiModel[]>([]);
  readonly isLoadingSolutions = signal(false);

  readonly submissions = signal<SubmissionDetail[]>([]);
  readonly isLoadingSubmissions = signal(false);
  readonly lastSubmission = signal<SubmissionResult | SubmissionDetail | null>(null);
  readonly sampleRunResults = signal<SampleRunResult[]>([]);
  readonly selectedSampleIndex = signal(0);

  readonly expandedSolution = computed(() => this.solutions().find(s => s.isExpanded));

  readonly rootCommentDrafts = signal<Record<string, string>>({});
  readonly replyDrafts = signal<Record<string, string>>({});
  readonly openReplyComposers = signal<Record<string, boolean>>({});
  readonly editDrafts = signal<Record<string, string>>({});
  readonly editingComments = signal<Record<string, boolean>>({});
  readonly pendingCommentTargets = signal<Record<string, boolean>>({});
  readonly pendingVoteTargets = signal<Record<string, boolean>>({});

  readonly editorOptions = computed(() => {
    const languageOption =
      this.editorLanguages.find(option => option.value === this.editorLanguage()) ??
      this.editorLanguages[0];

    const fontMap: Record<string, string> = {
      'IBM Plex Mono': '"IBM Plex Mono", Consolas, Monaco, monospace',
      'Cascadia': '"Cascadia Code", Consolas, Monaco, monospace',
      'Fira Code': '"Fira Code", Consolas, Monaco, monospace'
    };

    return {
      theme: this.getMonacoTheme(),
      language: languageOption.monacoLanguage,
      automaticLayout: true,
      minimap: { enabled: false },
      fontFamily: fontMap[this.editorFontFamily()] ?? '"IBM Plex Mono", Consolas, Monaco, monospace',
      fontSize: this.editorFontSize(),
      tabSize: this.editorTabSize(),
      insertSpaces: true,
      scrollBeyondLastLine: false,
      lineNumbersMinChars: 3,
      roundedSelection: false,
      padding: { top: 18, bottom: 18 }
    };
  });

  readonly latestSubmission = computed(() => this.lastSubmission() ?? this.submissions()[0] ?? null);

  private lastLoadedUserId: string | null = null;
  private lastLoadedProblemId: string | null = null;
  private lastLoadedPodDate: string | null = null;

  private getStorageKey(type: 'code' | 'customTestCases', problemId: string, lang?: string): string {
    const user = this.authService.currentUser$();
    const prefix = user ? user.id : 'anonymous';
    if (type === 'code') {
      return `compylr.code.${prefix}.${problemId}.${lang}`;
    } else {
      return `compylr.customTestCases.${prefix}.${problemId}`;
    }
  }

  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(([params, queryParams]) => {
        const id = params.get('id');
        const podDate = queryParams.get('podDate');

        if (!id) {
          return;
        }

        if (id !== this.lastLoadedProblemId || podDate !== this.lastLoadedPodDate) {
          this.lastLoadedProblemId = id;
          this.lastLoadedPodDate = podDate;
          this.loadProblem(id);
        }
      });

    this.loadUserPreferences();

    // Sync active workspace state
    this.workspaceActionService.hasWorkspaceActive.set(true);

    // Sync isRunningSamples & isSubmitting signals from component to service
    effect(() => {
      this.workspaceActionService.isRunningSamples.set(this.isRunningSamples());
    });
    effect(() => {
      this.workspaceActionService.isSubmitting.set(this.isSubmitting());
    });

    // Reload problem details, submissions, custom test cases, and editor code when the logged-in user changes.
    effect(() => {
      const user = this.authService.currentUser$();
      const currentUserId = user ? user.id : 'anonymous';
      const id = this.problemId();
      if (id && currentUserId !== this.lastLoadedUserId) {
        untracked(() => {
          this.loadProblem(id);
        });
      }
    });

    // Listen to header events
    this.workspaceActionService.runSamplesRequest$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.runSampleTests();
      });

    this.workspaceActionService.submitRequest$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.submitCode();
      });
  }

  @HostListener('window:resize')
  onWindowResize() {
    this.isCompactWorkspace.set(this.readIsCompactWorkspace());
  }

  ngOnDestroy() {
    this.workspaceActionService.hasWorkspaceActive.set(false);
  }

  onEditorInit(editor: any) {
    this.editorInstance = editor;

    const monaco = (window as any).monaco;
    if (monaco) {
      // 1. Register custom document formatting providers once
      if (!(window as any).compylrFormattersRegistered) {
        (window as any).compylrFormattersRegistered = true;

        const languages = ['csharp', 'python', 'java', 'cpp', 'c'];
        for (const lang of languages) {
          monaco.languages.registerDocumentFormattingEditProvider(lang, {
            provideDocumentFormattingEdits(model: any, options: any) {
              const text = model.getValue();
              const formatted = formatCodeSimple(text, lang, options.tabSize, options.insertSpaces);
              return [
                {
                  range: model.getFullModelRange(),
                  text: formatted,
                },
              ];
            },
          });
        }
      }

      // 2. Bind keyboard shortcuts
      // Ctrl + ' to RUN
      editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Quote, () => {
        this.runSampleTests();
      });

      // Ctrl + Enter to SUBMIT
      editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, () => {
        this.submitCode();
      });
    }
  }

  formatCode() {
    if (this.editorInstance) {
      this.editorInstance.trigger('editor', 'editor.action.formatDocument', null);
    }
  }

  toggleEditorSettings(event: Event) {
    event.stopPropagation();
    this.editorSettingsOpen.update(open => !open);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event) {
    const clickedInside = (event.target as HTMLElement).closest('.editor-settings-container');
    if (!clickedInside) {
      this.editorSettingsOpen.set(false);
    }
  }

  dockTabLabel(panelId: DockPanelId) {
    return this.dockPanelDefinitions.find(panel => panel.id === panelId)?.label ?? panelId;
  }

  zoneTabs(zoneId: DockZoneId) {
    return this.dockZones()[zoneId].tabs;
  }

  zoneActiveTab(zoneId: DockZoneId) {
    return this.dockZones()[zoneId].activeTab;
  }

  setZoneActiveTab(zoneId: DockZoneId, panelId: DockPanelId) {
    this.dockZones.update(current => {
      const updated = {
        ...current,
        [zoneId]: {
          ...current[zoneId],
          activeTab: panelId
        }
      };
      this.saveLayoutState(updated);
      return updated;
    });
    this.activateDockPanel(panelId);
  }

  startDockDrag(event: DragEvent, panelId: DockPanelId) {
    if (!event.dataTransfer) {
      return;
    }

    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', panelId);
    this.draggedDockPanel.set(panelId);
    this.dockDropTarget.set(null);
  }

  handleDockDragEnd() {
    this.draggedDockPanel.set(null);
    this.dockDropTarget.set(null);
  }

  allowZoneDrop(event: DragEvent, zoneId: DockZoneId) {
    if (!this.draggedDockPanel()) {
      return;
    }

    event.preventDefault();
    this.dockDropTarget.set({
      mode: 'zone',
      targetId: zoneId
    });
  }

  allowBelowDrop(event: DragEvent, columnId: DockColumnId) {
    if (!this.draggedDockPanel()) {
      return;
    }

    event.preventDefault();
    this.dockDropTarget.set({
      mode: 'below',
      targetId: columnId
    });
  }

  handleZoneDrop(event: DragEvent, zoneId: DockZoneId) {
    event.preventDefault();

    const panelId = this.draggedDockPanel();
    if (!panelId) {
      return;
    }

    this.movePanelToZone(panelId, zoneId);
    this.handleDockDragEnd();
  }

  handleBelowDrop(event: DragEvent, columnId: DockColumnId) {
    event.preventDefault();

    const panelId = this.draggedDockPanel();
    if (!panelId) {
      return;
    }

    this.movePanelToZone(panelId, this.bottomZoneId(columnId));
    this.handleDockDragEnd();
  }

  isZoneDropTarget(zoneId: DockZoneId) {
    const target = this.dockDropTarget();
    return !!target && target.mode === 'zone' && target.targetId === zoneId;
  }

  isBelowDropTarget(columnId: DockColumnId) {
    const target = this.dockDropTarget();
    return !!target && target.mode === 'below' && target.targetId === columnId;
  }

  openDockPanel(panelId: DockPanelId) {
    const zoneId = this.findPanelZone(panelId);
    if (!zoneId) {
      return;
    }

    this.setZoneActiveTab(zoneId, panelId);
  }

  resetDockLayout() {
    const defaultZones = this.createDefaultDockZones();
    this.dockZones.set(defaultZones);
    this.saveLayoutState(defaultZones);
    this.mainSplitSizesHorizontal.set([46, 54]);
    this.mainSplitSizesVertical.set([50, 50]);
    this.leftSplitSizes.set([62, 38]);
    this.rightSplitSizes.set([60, 40]);
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem('compylr.mainSplitSizesHorizontal');
      localStorage.removeItem('compylr.mainSplitSizesVertical');
      localStorage.removeItem('compylr.leftSplitSizes');
      localStorage.removeItem('compylr.rightSplitSizes');
    }
    this.fullscreenZoneId.set(null);
    this.handleDockDragEnd();
  }

  columnHasBottomZone(columnId: DockColumnId) {
    return this.zoneTabs(this.bottomZoneId(columnId)).length > 0;
  }

  zoneHasTabs(zoneId: DockZoneId) {
    return this.zoneTabs(zoneId).length > 0;
  }

  topZoneId(columnId: DockColumnId): DockZoneId {
    return columnId === 'left' ? 'leftTop' : 'rightTop';
  }

  bottomZoneId(columnId: DockColumnId): DockZoneId {
    return columnId === 'left' ? 'leftBottom' : 'rightBottom';
  }

  isZoneFullscreen(zoneId: DockZoneId) {
    return this.fullscreenZoneId() === zoneId;
  }

  toggleZoneFullscreen(zoneId: DockZoneId) {
    this.fullscreenZoneId.update(current => (current === zoneId ? null : zoneId));
  }

  setSampleIndex(index: number) {
    this.selectedSampleIndex.set(index);
  }

  inputParts(testCase: Pick<SampleTestCase, 'input' | 'inputLabels'>) {
    const values = (testCase.input ?? '').split(/\r?\n/);
    const labels = testCase.inputLabels ?? [];

    return values.map((value, index) => ({
      label: labels[index]?.trim() || `Input ${index + 1}`,
      value
    }));
  }

  setEditorLanguage(language: EditorLanguage) {
    if (this.editorLanguage() === language) {
      return;
    }

    this.editorLanguage.set(language);
    this.code.set(this.getStarterCode());

    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('compylr.preferredLanguage', language);
    }
    if (this.authService.isLoggedIn$()) {
      this.userService.updateMyPreferences({ preferredLanguage: language }).subscribe({
        error: err => console.error('Failed to update preferred language on backend:', err)
      });
    }
  }

  setEditorTheme(theme: EditorThemeChoice) {
    this.editorTheme.set(theme);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('compylr.editorTheme', theme);
    }
  }

  setEditorFontSize(size: number) {
    this.editorFontSize.set(size);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('compylr.editorFontSize', String(size));
    }
  }

  setEditorFontFamily(family: string) {
    this.editorFontFamily.set(family);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('compylr.editorFontFamily', family);
    }
  }

  setEditorTabSize(size: number) {
    this.editorTabSize.set(size);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('compylr.editorTabSize', String(size));
    }
  }

  toggleEditorFullscreen() {
    this.isEditorFullscreen.update(value => !value);
  }

  loadProblem(problemId: string) {
    this.problemId.set(problemId);
    this.problem.set(null);
    this.problemError.set(null);
    this.isLoadingProblem.set(true);
    this.isLoadingSolutions.set(false);
    this.isLoadingSubmissions.set(false);
    this.solutions.set([]);
    this.submissions.set([]);
    this.lastSubmission.set(null);
    this.selectedSubmission.set(null);
    this.sampleRunResults.set([]);
    this.selectedSampleIndex.set(0);
    this.rootCommentDrafts.set({});
    this.replyDrafts.set({});
    this.openReplyComposers.set({});

    const currentUser = this.authService.currentUser$();
    this.lastLoadedUserId = currentUser ? currentUser.id : 'anonymous';

    this.problemService.getProblemById(problemId).subscribe({
      next: problem => {
        if (typeof localStorage !== 'undefined') {
          const storedCustom = localStorage.getItem(this.getStorageKey('customTestCases', problemId));
          if (storedCustom) {
            try {
              const customCases: SampleTestCase[] = JSON.parse(storedCustom);
              problem.sampleTestCases = [...problem.sampleTestCases, ...customCases];
            } catch (e) {
              console.error('Failed to parse custom test cases', e);
            }
          }
        }
        this.problem.set(problem);
        this.code.set(this.getStarterCode());
        this.isLoadingProblem.set(false);
        this.checkInitialActiveTabs();
      },
      error: error => {
        const message = getApiErrorMessage(error, 'Unable to load this problem right now.');
        this.problemError.set(message);
        this.toastService.error(message);
        this.isLoadingProblem.set(false);
      }
    });
  }

  copyToClipboard(text: string, event?: MouseEvent) {
    if (!text) return;
    const target = event?.currentTarget as HTMLElement;

    const performCopy = () => {
      this.toastService.success('Copied to clipboard!');
      if (target) {
        target.classList.add('copied');
        setTimeout(() => {
          target.classList.remove('copied');
        }, 2000);
      }
    };

    if (navigator.clipboard) {
      navigator.clipboard.writeText(text).then(() => {
        performCopy();
      }).catch(err => {
        console.error('Failed to copy: ', err);
      });
    } else {
      const textArea = document.createElement('textarea');
      textArea.value = text;
      document.body.appendChild(textArea);
      textArea.select();
      try {
        document.execCommand('copy');
        performCopy();
      } catch (err) {
        console.error('Fallback: Unable to copy', err);
      }
      document.body.removeChild(textArea);
    }
  }

  checkInitialActiveTabs() {
    const zones = this.dockZones();
    let shouldLoadSolutions = false;
    let shouldLoadSubmissions = false;
    
    for (const key of Object.keys(zones) as DockZoneId[]) {
      if (zones[key].activeTab === 'solutions') shouldLoadSolutions = true;
      if (zones[key].activeTab === 'submissions') shouldLoadSubmissions = true;
    }

    if (shouldLoadSolutions) {
      this.loadSolutions(true);
    }
    if (shouldLoadSubmissions) {
      this.loadSubmissions(true);
    }
  }

  runSampleTests() {
    const problem = this.problem();
    if (!problem) {
      return;
    }

    if (!this.ensureAuthenticated('Sign in to run code against sample test cases.')) {
      return;
    }

    if (!problem.sampleTestCases?.length) {
      this.toastService.info('This problem does not include sample test cases yet.');
      this.openDockPanel('tests');
      return;
    }

    if (!this.code().trim()) {
      this.toastService.warning('Add some code before running sample tests.');
      return;
    }

    this.isRunningSamples.set(true);
    this.openDockPanel('result');

    this.submissionService
      .runSampleTests({
        problemId: problem.id,
        language: this.editorLanguage(),
        code: this.code()
      })
      .subscribe({
        next: results => {
          this.sampleRunResults.set(results);
          this.selectedSampleIndex.set(0);
          const passed = results.filter(result => result.passed).length;
          this.toastService.success(`Sample run finished: ${passed}/${results.length} passed.`);
        },
        error: error => {
          this.toastService.error(getApiErrorMessage(error, 'Unable to run sample tests right now.'));
          this.isRunningSamples.set(false);
        },
        complete: () => this.isRunningSamples.set(false)
      });
  }

  resetCode() {
    this.code.set(this.getStarterCode());
  }

  addFailedTestCaseToRunner(input: string, expected: string) {
    const currentProblem = this.problem();
    if (!currentProblem) return;

    const exists = currentProblem.sampleTestCases.some(tc => tc.input === input);
    if (exists) {
      this.toastService.info('This test case is already in your Test Cases tab.');
      return;
    }

    const newTestCase: SampleTestCase = {
      id: `custom_${Date.now()}`,
      input,
      inputLabels: [],
      isHidden: false,
      expectedOutput: expected
    };

    if (typeof localStorage !== 'undefined') {
      const key = this.getStorageKey('customTestCases', currentProblem.id);
      const stored = localStorage.getItem(key);
      let customCases: SampleTestCase[] = [];
      if (stored) {
        try { customCases = JSON.parse(stored); } catch (e) {}
      }
      customCases.push(newTestCase);
      localStorage.setItem(key, JSON.stringify(customCases));
    }

    this.problem.update(current => {
      if (!current) return current;
      const updatedTestCases = [...current.sampleTestCases, newTestCase];
      
      this.toastService.success('Failed test case added to Test Cases tab!');
      
      setTimeout(() => {
        this.openDockPanel('tests');
        this.selectedSampleIndex.set(updatedTestCases.length - 1);
      }, 50);

      return {
        ...current,
        sampleTestCases: updatedTestCases
      };
    });
  }

  updateCode(value: string) {
    this.code.set(value || '');
    if (typeof localStorage !== 'undefined') {
      const problemId = this.problem()?.id || this.problemId();
      const lang = this.editorLanguage();
      if (problemId && lang) {
        localStorage.setItem(this.getStorageKey('code', problemId, lang), value || '');
      }
    }
  }

  submitCode() {
    const problem = this.problem();
    if (!problem) {
      return;
    }

    if (!this.ensureAuthenticated('Sign in to submit code and keep your history.')) {
      return;
    }

    if (!this.code().trim()) {
      this.toastService.warning('Add some code before you submit.');
      return;
    }

    this.isSubmitting.set(true);

    this.submissionService
      .createSubmission({
        problemId: problem.id,
        language: this.editorLanguage(),
        code: this.code()
      })
      .subscribe({
        next: result => {
          this.lastSubmission.set(result);
          this.loadSubmissionsAndSelectFirst();
          
          if (result.status === 'Pending' || result.status === 'Running') {
            this.pollSubmissionStatus(result.id);
          } else {
            this.isSubmitting.set(false);
            this.toastService.success(`Submission finished with status: ${this.formatStatus(result.status)}.`);
          }
        },
        error: error => {
          this.toastService.error(getApiErrorMessage(error, 'Unable to submit code right now.'));
          this.isSubmitting.set(false);
        }
      });
  }

  private pollSubmissionStatus(submissionId: string) {
    let attempts = 0;
    const maxAttempts = 30;

    interval(1000)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        switchMap(() => this.submissionService.getSubmission(submissionId)),
        catchError(err => {
          console.error('Error during polling:', err);
          return of(null);
        }),
        takeWhile((sub): sub is SubmissionDetail => {
          attempts++;
          if (!sub) {
            return false;
          }
          const isPendingOrRunning = sub.status === 'Pending' || sub.status === 'Running';
          const withinTimeLimit = attempts < maxAttempts;
          return isPendingOrRunning && withinTimeLimit;
        }, true)
      )
      .subscribe({
        next: sub => {
          if (sub) {
            this.lastSubmission.set(sub);
            
            const currentSelected = this.selectedSubmission();
            if (currentSelected && currentSelected.id === sub.id) {
              this.selectedSubmission.set(sub);
            }

            // Refresh submissions list to show updated status/details in submissions tab
            this.loadSubmissions(true);
            if (sub.status !== 'Pending' && sub.status !== 'Running') {
              this.isSubmitting.set(false);
              this.toastService.success(`Submission finished with status: ${this.formatStatus(sub.status)}.`);
            }
          }
        },
        error: () => {
          this.isSubmitting.set(false);
          this.toastService.error('Failed checking submission status.');
        },
        complete: () => {
          this.isSubmitting.set(false);
          const currentSub = this.lastSubmission();
          if (currentSub && (currentSub.status === 'Pending' || currentSub.status === 'Running')) {
            this.toastService.warning('Submission taking too long. Check back in your Submissions history tab.');
          }
        }
      });
  }

  loadSubmissions(force = false) {
    const problemId = this.problem()?.id || this.problemId();
    if (!problemId || !this.authService.isLoggedIn$()) {
      return;
    }

    if (this.isLoadingSubmissions() || (this.submissions().length && !force)) {
      return;
    }

    this.isLoadingSubmissions.set(true);
    this.submissionService.getMySubmissionsForProblem(problemId).subscribe({
      next: submissions => {
        this.submissions.set(submissions);

        if (!this.lastSubmission() && submissions.length > 0) {
          this.lastSubmission.set(submissions[0]);
        }

        this.isLoadingSubmissions.set(false);
      },
      error: error => {
        this.toastService.error(getApiErrorMessage(error, 'Unable to load your submissions.'));
        this.isLoadingSubmissions.set(false);
      }
    });
  }

  viewSubmissionDetail(submission: SubmissionDetail) {
    this.selectedSubmission.set(submission);
  }

  clearSelectedSubmission() {
    this.selectedSubmission.set(null);
  }

  copyCodeToEditor(code: string, language: string) {
    this.code.set(code);
    this.openDockPanel('editor');
    
    const normalized = language.trim().toLowerCase().replace('sharp', 'sharp');
    let langChoice: EditorLanguage = 'csharp';
    if (normalized === 'python' || normalized === 'py') langChoice = 'python';
    else if (normalized === 'java') langChoice = 'java';
    else if (normalized === 'cpp' || normalized === 'c++') langChoice = 'cpp';
    else if (normalized === 'c') langChoice = 'c';
    
    this.setEditorLanguage(langChoice);
    this.toastService.success('Code loaded into editor!');
  }

  loadSubmissionsAndSelectFirst() {
    const problemId = this.problem()?.id || this.problemId();
    if (!problemId) return;
    
    this.isLoadingSubmissions.set(true);
    this.submissionService.getMySubmissionsForProblem(problemId).subscribe({
      next: submissions => {
        this.submissions.set(submissions);
        if (submissions.length > 0) {
          this.selectedSubmission.set(submissions[0]);
          this.openDockPanel('submissions');
        }
        this.isLoadingSubmissions.set(false);
      },
      error: error => {
        this.toastService.error(getApiErrorMessage(error, 'Unable to load your submissions.'));
        this.isLoadingSubmissions.set(false);
      }
    });
  }

  loadSolutions(force = false) {
    const problemId = this.problem()?.id || this.problemId();
    if (!problemId) {
      return;
    }

    if (this.isLoadingSolutions() || (this.solutions().length && !force)) {
      return;
    }

    this.isLoadingSolutions.set(true);
    this.communityService.getSolutionsForProblem(problemId).subscribe({
      next: solutions => {
        this.solutions.set(solutions.map(solution => this.toSolutionUiModel(solution)));
        this.isLoadingSolutions.set(false);
      },
      error: error => {
        this.toastService.error(getApiErrorMessage(error, 'Unable to load community solutions.'));
        this.isLoadingSolutions.set(false);
      }
    });
  }





  toggleSolution(solutionId: string) {
    const solution = this.solutions().find(item => item.id === solutionId);
    if (!solution) {
      return;
    }

    const willExpand = !solution.isExpanded;
    this.patchSolution(solutionId, current => ({ ...current, isExpanded: willExpand }));

    if (willExpand) {
      if (!solution.detail) {
        this.loadSolutionDetail(solutionId);
      }

      if (!solution.comments.length) {
        this.loadComments(solutionId, 1, false);
      }
    }
  }

  getPreviousSolution(solutionId: string): SolutionUiModel | null {
    const solutions = this.solutions();
    const index = solutions.findIndex(s => s.id === solutionId);
    if (index > 0) {
      return solutions[index - 1];
    }
    return null;
  }

  getNextSolution(solutionId: string): SolutionUiModel | null {
    const solutions = this.solutions();
    const index = solutions.findIndex(s => s.id === solutionId);
    if (index >= 0 && index < solutions.length - 1) {
      return solutions[index + 1];
    }
    return null;
  }

  goToSolution(currentId: string, targetId: string) {
    this.patchSolution(currentId, current => ({ ...current, isExpanded: false }));
    this.toggleSolution(targetId);
    
    setTimeout(() => {
      const element = document.getElementById(`solution-${targetId}`);
      if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    }, 50);
  }

  loadSolutionDetail(solutionId: string) {
    this.patchSolution(solutionId, current => ({ ...current, isDetailLoading: true }));

    this.communityService.getSolutionById(solutionId).subscribe({
      next: detail => {
        this.patchSolution(solutionId, current => ({
          ...current,
          detail,
          title: detail.title,
          excerpt: detail.excerpt,
          voteCount: detail.voteCount,
          commentCount: detail.commentCount,
          isDetailLoading: false
        }));
      },
      error: error => {
        this.patchSolution(solutionId, current => ({ ...current, isDetailLoading: false }));
        this.toastService.error(getApiErrorMessage(error, 'Unable to open this solution.'));
      }
    });
  }

  loadComments(solutionId: string, pageNumber: number, append: boolean) {
    this.patchSolution(solutionId, current => ({ ...current, isCommentsLoading: true }));

    this.communityService.getCommentsForSolution(solutionId, pageNumber, this.commentsPageSize).subscribe({
      next: comments => {
        this.patchSolution(solutionId, current => ({
          ...current,
          comments: append ? [...current.comments, ...comments] : comments,
          commentsPage: pageNumber,
          hasMoreComments: comments.length === this.commentsPageSize,
          isCommentsLoading: false
        }));
      },
      error: error => {
        this.patchSolution(solutionId, current => ({ ...current, isCommentsLoading: false }));
        this.toastService.error(getApiErrorMessage(error, 'Unable to load comments right now.'));
      }
    });
  }

  loadMoreComments(solutionId: string) {
    const solution = this.solutions().find(item => item.id === solutionId);
    if (!solution || !solution.hasMoreComments) {
      return;
    }

    this.loadComments(solutionId, solution.commentsPage + 1, true);
  }

  rootCommentDraft(solutionId: string) {
    return this.rootCommentDrafts()[solutionId] ?? '';
  }

  setRootCommentDraft(solutionId: string, value: string) {
    this.rootCommentDrafts.update(current => ({ ...current, [solutionId]: value }));
  }

  replyDraft(commentId: string) {
    return this.replyDrafts()[commentId] ?? '';
  }

  setReplyDraft(commentId: string, value: string) {
    this.replyDrafts.update(current => ({ ...current, [commentId]: value }));
  }

  isReplyComposerOpen(commentId: string) {
    return !!this.openReplyComposers()[commentId];
  }

  toggleReplyComposer(commentId: string) {
    if (!this.ensureAuthenticated('Sign in to reply to the discussion.')) {
      return;
    }

    this.openReplyComposers.update(current => ({ ...current, [commentId]: !current[commentId] }));
  }

  toggleCommentEdit(commentId: string, currentContent: string) {
    if (!this.ensureAuthenticated('Sign in to edit your comment.')) return;
    this.editingComments.update(s => ({ ...s, [commentId]: !s[commentId] }));
    if (this.editingComments()[commentId]) {
      this.editDrafts.update(s => ({ ...s, [commentId]: currentContent }));
    }
  }

  setEditDraft(commentId: string, value: string) {
    this.editDrafts.update(s => ({ ...s, [commentId]: value }));
  }

  editComment(solutionId: string, commentId: string) {
    const draft = this.editDrafts()[commentId];
    if (!draft || draft.trim().length === 0) return;

    this.setPendingState(this.pendingCommentTargets, `edit:${commentId}`, true);

    this.communityService.updateComment(commentId, { content: draft }).subscribe({
      next: updated => {
        this.patchSolution(solutionId, solution => {
          const newComments = this.replaceComment(solution.comments, updated);
          return { ...solution, comments: newComments };
        });
        this.editingComments.update(s => ({ ...s, [commentId]: false }));
        this.setPendingState(this.pendingCommentTargets, `edit:${commentId}`, false);
        this.toastService.success('Comment updated successfully.');
      },
      error: err => {
        this.toastService.error(getApiErrorMessage(err, 'Failed to update comment.'));
        this.setPendingState(this.pendingCommentTargets, `edit:${commentId}`, false);
      }
    });
  }

  private replaceComment(comments: CommentViewModel[], updated: CommentViewModel): CommentViewModel[] {
    return comments.map(c => {
      if (c.id === updated.id) {
        return { ...c, content: updated.content };
      }
      if (c.replies && c.replies.length > 0) {
        return { ...c, replies: this.replaceComment(c.replies, updated) };
      }
      return c;
    });
  }

  postRootComment(solutionId: string) {
    this.publishComment(solutionId, null, this.rootCommentDraft(solutionId));
  }

  postReply(solutionId: string, parentCommentId: string) {
    this.publishComment(solutionId, parentCommentId, this.replyDraft(parentCommentId));
  }

  voteOnSolution(solutionId: string, isUpvote: boolean) {
    if (!this.ensureAuthenticated('Sign in to vote on solutions and comments.')) {
      return;
    }

    const pendingKey = this.voteKey('solution', solutionId);
    if (this.pendingVoteTargets()[pendingKey]) {
      return;
    }

    const currentVote = this.solutions().find(solution => solution.id === solutionId)?.userVote ?? null;
    this.setPendingState(this.pendingVoteTargets, pendingKey, true);

    this.communityService
      .castVote({
        entityId: solutionId,
        entityType: 'Solution',
        isUpvote
      })
      .pipe(finalize(() => this.setPendingState(this.pendingVoteTargets, pendingKey, false)))
      .subscribe({
        next: voteCount => {
          const nextVote = currentVote === (isUpvote ? 'up' : 'down') ? null : isUpvote ? 'up' : 'down';
          this.patchSolution(solutionId, solution => ({
            ...solution,
            voteCount,
            userVote: nextVote,
            detail: solution.detail ? { ...solution.detail, voteCount } : null
          }));
        },
        error: error => {
          this.toastService.error(getApiErrorMessage(error, 'Unable to record your vote.'));
        }
      });
  }

  voteOnComment(solutionId: string, commentId: string, isUpvote: boolean) {
    if (!this.ensureAuthenticated('Sign in to vote on solutions and comments.')) {
      return;
    }

    const pendingKey = this.voteKey('comment', commentId);
    if (this.pendingVoteTargets()[pendingKey]) {
      return;
    }

    const currentVote = this.findCommentVote(solutionId, commentId);
    this.setPendingState(this.pendingVoteTargets, pendingKey, true);

    this.communityService
      .castVote({
        entityId: commentId,
        entityType: 'Comment',
        isUpvote
      })
      .pipe(finalize(() => this.setPendingState(this.pendingVoteTargets, pendingKey, false)))
      .subscribe({
        next: voteCount => {
          const nextVote = currentVote === (isUpvote ? 'up' : 'down') ? null : isUpvote ? 'up' : 'down';

          this.patchSolution(solutionId, solution => ({
            ...solution,
            comments: this.updateCommentTree(solution.comments, commentId, comment => ({
              ...comment,
              voteCount,
              userVote: nextVote
            }))
          }));
        },
        error: error => {
          this.toastService.error(getApiErrorMessage(error, 'Unable to record your vote.'));
        }
      });
  }

  voteKey(scope: 'solution' | 'comment', id: string) {
    return `${scope}:${id}`;
  }

  formatStatus(status: SubmissionStatus | string) {
    return status.replace(/([A-Z])/g, ' $1').trim();
  }

  formatRelativeTime(value: string) {
    let dateStr = value;
    if (!dateStr.endsWith('Z') && !dateStr.match(/[+-]\d{2}:?\d{2}$/)) {
      dateStr += 'Z';
    }
    const timestamp = new Date(dateStr).getTime();
    const now = Date.now();
    const diff = Math.round((timestamp - now) / 1000);
    const formatter = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

    const units: Array<[Intl.RelativeTimeFormatUnit, number]> = [
      ['day', 60 * 60 * 24],
      ['hour', 60 * 60],
      ['minute', 60],
      ['second', 1]
    ];

    for (const [unit, seconds] of units) {
      if (Math.abs(diff) >= seconds || unit === 'second') {
        return formatter.format(Math.round(diff / seconds), unit);
      }
    }

    return formatter.format(0, 'second');
  }

  goToLogin() {
    void this.router.navigate(['/login'], {
      queryParams: { redirect: this.router.url }
    });
  }

  submissionSummary(submission: SubmissionResult | SubmissionDetail | null) {
    if (!submission) {
      return 'Submit your code to see the result here.';
    }

    if ('executionTimeMs' in submission && submission.executionTimeMs != null) {
      return `${this.formatStatus(submission.status)} in ${submission.executionTimeMs}ms`;
    }

    return this.formatStatus(submission.status);
  }

  private publishComment(solutionId: string, parentCommentId: string | null, content: string) {
    if (!this.ensureAuthenticated('Sign in to join the discussion.')) {
      return;
    }

    const trimmedContent = content.trim();
    if (!trimmedContent) {
      this.toastService.warning('Write a comment before posting it.');
      return;
    }

    const pendingKey = parentCommentId ? `comment:${parentCommentId}` : `solution:${solutionId}`;
    if (this.pendingCommentTargets()[pendingKey]) {
      return;
    }

    this.setPendingState(this.pendingCommentTargets, pendingKey, true);

    this.communityService
      .createComment({
        solutionId,
        parentCommentId,
        content: trimmedContent
      })
      .pipe(finalize(() => this.setPendingState(this.pendingCommentTargets, pendingKey, false)))
      .subscribe({
        next: comment => {
          this.patchSolution(solutionId, solution => {
            const nextComments = parentCommentId
              ? this.appendReply(solution.comments, parentCommentId, comment)
              : [comment, ...solution.comments];

            return {
              ...solution,
              comments: nextComments,
              commentCount: solution.commentCount + 1,
              detail: solution.detail
                ? {
                    ...solution.detail,
                    commentCount: solution.detail.commentCount + 1
                  }
                : null
            };
          });

          if (parentCommentId) {
            this.replyDrafts.update(current => ({ ...current, [parentCommentId]: '' }));
            this.openReplyComposers.update(current => ({ ...current, [parentCommentId]: false }));
          } else {
            this.rootCommentDrafts.update(current => ({ ...current, [solutionId]: '' }));
          }
        },
        error: error => {
          console.error('Comment error:', error);
          this.toastService.error('Unable to publish your comment.');
        }
      });
  }

  deleteSolution(solutionId: string, event: Event) {
    event.stopPropagation();
    if (!confirm('Are you sure you want to delete this solution? This cannot be undone.')) {
      return;
    }
    this.communityService.deleteSolution(solutionId).subscribe({
      next: () => {
        this.solutions.update(current => current.filter(s => s.id !== solutionId));
        this.toastService.success('Solution deleted');
      },
      error: () => this.toastService.error('Failed to delete solution')
    });
  }

  deleteComment(commentId: string) {
    if (!confirm('Are you sure you want to delete this comment?')) {
      return;
    }
    this.communityService.deleteComment(commentId).subscribe({
      next: () => {
        // Soft delete locally to avoid full reload
        this.solutions.update(current => 
          current.map(solution => {
            const newComments = this.softDeleteCommentFromTree(solution.comments, commentId);
            return { ...solution, comments: newComments };
          })
        );
        this.toastService.success('Comment deleted');
      },
      error: () => this.toastService.error('Failed to delete comment')
    });
  }

  private softDeleteCommentFromTree(comments: CommentViewModel[], commentId: string): CommentViewModel[] {
    return comments.map(c => {
      if (c.id === commentId) {
        return { ...c, content: '[Deleted by user]', authorUsername: '[Deleted]' };
      }
      if (c.replies && c.replies.length > 0) {
        return { ...c, replies: this.softDeleteCommentFromTree(c.replies, commentId) };
      }
      return c;
    });
  }

  private findCommentVote(solutionId: string, commentId: string): VoteChoice {
    const solution = this.solutions().find(item => item.id === solutionId);
    if (!solution) {
      return null;
    }

    const search = (comments: CommentViewModel[]): VoteChoice => {
      for (const comment of comments) {
        if (comment.id === commentId) {
          return comment.userVote ?? null;
        }

        const nested = search(comment.replies);
        if (nested !== null) {
          return nested;
        }
      }

      return null;
    };

    return search(solution.comments);
  }

  private ensureAuthenticated(message: string) {
    if (this.authService.isLoggedIn$()) {
      return true;
    }

    this.toastService.info(message);
    this.goToLogin();
    return false;
  }

  private getStarterCode() {
    const problemId = this.problem()?.id || this.problemId();
    const lang = this.editorLanguage();

    if (typeof localStorage !== 'undefined' && problemId && lang) {
      const savedCode = localStorage.getItem(this.getStorageKey('code', problemId, lang));
      if (savedCode !== null) {
        return savedCode;
      }
    }

    const starterCode = this.problem()?.starterCode?.trim();
    if (starterCode) {
      try {
        const codes = JSON.parse(starterCode);
        const lang = this.editorLanguage();
        if (codes && codes[lang]) {
          return codes[lang];
        }
      } catch (e) {
        // Fallback for non-JSON content (legacy C# only starter codes)
        if (this.editorLanguage() === 'csharp') {
          return starterCode;
        }
      }
    }

    switch (this.editorLanguage()) {
      case 'python':
        return 'class Solution:\n    def solve(self):\n        pass\n';
      case 'java':
        return 'class Solution {\n    public void solve() {\n        \n    }\n}\n';
      case 'cpp':
        return '#include <bits/stdc++.h>\nusing namespace std;\n\nclass Solution {\npublic:\n    void solve() {\n        \n    }\n};\n';
      case 'c':
        return '#include <stdio.h>\n\nvoid solve(void) {\n    \n}\n';
      case 'csharp':
      default:
        return 'public class Solution\n{\n    public void Solve()\n    {\n        \n    }\n}\n';
    }
  }

  private getMonacoTheme() {
    switch (this.editorTheme()) {
      case 'light':
        return 'vs';
      case 'dark':
        return 'vs-dark';
      case 'contrast':
        return 'hc-black';
      case 'system':
      default:
        return this.themeService.isDark() ? 'vs-dark' : 'vs';
    }
  }

  private readIsCompactWorkspace() {
    return typeof window !== 'undefined' ? window.innerWidth <= 960 : false;
  }

  private createDefaultDockZones(): Record<DockZoneId, DockZoneState> {
    return {
      leftTop: {
        tabs: ['description', 'solutions', 'submissions'],
        activeTab: 'description'
      },
      leftBottom: {
        tabs: [],
        activeTab: null
      },
      rightTop: {
        tabs: ['editor', 'tests', 'result'],
        activeTab: 'editor'
      },
      rightBottom: {
        tabs: [],
        activeTab: null
      }
    };
  }

  private activateDockPanel(panelId: DockPanelId) {
    if (panelId === 'solutions') {
      this.loadSolutions();
      return;
    }

    if (panelId === 'submissions' && this.authService.isLoggedIn$()) {
      this.loadSubmissions();
    }
  }

  private findPanelZone(panelId: DockPanelId): DockZoneId | null {
    const currentZones = this.dockZones();
    const zoneIds: DockZoneId[] = ['leftTop', 'leftBottom', 'rightTop', 'rightBottom'];

    for (const zoneId of zoneIds) {
      if (currentZones[zoneId].tabs.includes(panelId)) {
        return zoneId;
      }
    }

    return null;
  }

  private movePanelToZone(panelId: DockPanelId, targetZoneId: DockZoneId) {
    this.dockZones.update(current => {
      const zoneIds: DockZoneId[] = ['leftTop', 'leftBottom', 'rightTop', 'rightBottom'];
      const next: Record<DockZoneId, DockZoneState> = {
        leftTop: { ...current.leftTop, tabs: [...current.leftTop.tabs] },
        leftBottom: { ...current.leftBottom, tabs: [...current.leftBottom.tabs] },
        rightTop: { ...current.rightTop, tabs: [...current.rightTop.tabs] },
        rightBottom: { ...current.rightBottom, tabs: [...current.rightBottom.tabs] }
      };

      for (const zoneId of zoneIds) {
        if (!next[zoneId].tabs.includes(panelId)) {
          continue;
        }

        next[zoneId].tabs = next[zoneId].tabs.filter(existingPanelId => existingPanelId !== panelId);
        next[zoneId].activeTab =
          next[zoneId].activeTab === panelId ? next[zoneId].tabs[0] ?? null : next[zoneId].activeTab;
      }

      if (!next[targetZoneId].tabs.includes(panelId)) {
        next[targetZoneId].tabs.push(panelId);
      }
      next[targetZoneId].activeTab = panelId;

      this.saveLayoutState(next);
      return next;
    });

    this.activateDockPanel(panelId);
  }

  private getInitialLanguage(): EditorLanguage {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.preferredLanguage');
      if (stored) {
        const normalized = stored.toLowerCase().replace('#', 'sharp');
        const isValid = this.editorLanguages.some(opt => opt.value === normalized);
        return isValid ? (normalized as EditorLanguage) : 'csharp';
      }
    }
    return 'csharp';
  }

  private getInitialDockZones(): Record<DockZoneId, DockZoneState> {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.layoutState');
      if (stored) {
        try {
          const parsed: Record<DockZoneId, DockZoneState> = JSON.parse(stored);
          // Reset active tabs to 'description' and 'editor' when opening a problem
          for (const zoneKey of Object.keys(parsed) as DockZoneId[]) {
            const zone = parsed[zoneKey];
            if (zone.tabs.includes('description')) {
              zone.activeTab = 'description';
            } else if (zone.tabs.includes('editor')) {
              zone.activeTab = 'editor';
            }
          }
          return parsed;
        } catch (e) {
          console.error('Failed to parse stored layoutState', e);
        }
      }
    }
    return this.createDefaultDockZones();
  }

  private loadUserPreferences() {
    if (this.authService.isLoggedIn$()) {
      this.userService.getMyProfile().subscribe({
        next: profile => {
          let needsUpdate = false;
          const updatePayload: UserPreferencesUpdateRequest = {};

          if (profile.preferredLanguage) {
            const normalized = profile.preferredLanguage.toLowerCase().replace('#', 'sharp');
            const isValid = this.editorLanguages.some(opt => opt.value === normalized);
            const finalLang = isValid ? (normalized as EditorLanguage) : 'csharp';

            if (typeof localStorage !== 'undefined') {
              localStorage.setItem('compylr.preferredLanguage', finalLang);
            }
            this.editorLanguage.set(finalLang);
            if (this.problem()) {
              this.code.set(this.getStarterCode());
            }
          } else {
            if (typeof localStorage !== 'undefined') {
              const localLang = localStorage.getItem('compylr.preferredLanguage');
              if (localLang) {
                updatePayload.preferredLanguage = localLang;
                needsUpdate = true;
              }
            }
          }

          if (profile.layoutState) {
            if (typeof localStorage !== 'undefined') {
              localStorage.setItem('compylr.layoutState', profile.layoutState);
            }
            try {
              const layout = JSON.parse(profile.layoutState);
              this.dockZones.set(layout);
            } catch (e) {
              console.error('Failed to parse layout state from profile:', e);
            }
          } else {
            if (typeof localStorage !== 'undefined') {
              const localLayout = localStorage.getItem('compylr.layoutState');
              if (localLayout) {
                updatePayload.layoutState = localLayout;
                needsUpdate = true;
              }
            }
          }

          if (needsUpdate) {
            this.userService.updateMyPreferences(updatePayload).subscribe({
              error: err => console.error('Failed to sync initial preferences to backend:', err)
            });
          }
        },
        error: err => {
          console.error('Failed to load user profile preferences:', err);
        }
      });
    }
  }

  private saveLayoutState(layout: Record<DockZoneId, DockZoneState>) {
    const layoutStr = JSON.stringify(layout);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('compylr.layoutState', layoutStr);
    }
    if (this.authService.isLoggedIn$()) {
      this.userService.updateMyPreferences({ layoutState: layoutStr }).subscribe({
        error: err => console.error('Failed to update layout preference on backend:', err)
      });
    }
  }

  private toSolutionUiModel(solution: SolutionSummary): SolutionUiModel {
    return {
      ...solution,
      detail: null,
      isExpanded: false,
      isDetailLoading: false,
      comments: [],
      commentsPage: 0,
      hasMoreComments: false,
      isCommentsLoading: false,
      userVote: null
    };
  }

  private patchSolution(solutionId: string, updater: (solution: SolutionUiModel) => SolutionUiModel) {
    this.solutions.update(current => current.map(solution => (solution.id === solutionId ? updater(solution) : solution)));
  }

  private appendReply(
    comments: CommentViewModel[],
    parentCommentId: string,
    comment: CommentViewModel
  ): CommentViewModel[] {
    return comments.map(existingComment => {
      if (existingComment.id === parentCommentId) {
        return {
          ...existingComment,
          replies: [...existingComment.replies, comment]
        };
      }

      return {
        ...existingComment,
        replies: this.appendReply(existingComment.replies, parentCommentId, comment)
      };
    });
  }

  private updateCommentTree(
    comments: CommentViewModel[],
    commentId: string,
    updater: (comment: CommentViewModel) => CommentViewModel
  ): CommentViewModel[] {
    return comments.map(comment => {
      if (comment.id === commentId) {
        return updater(comment);
      }

      return {
        ...comment,
        replies: this.updateCommentTree(comment.replies, commentId, updater)
      };
    });
  }

  private setPendingState(
    stateSignal: {
      update: (updater: (current: Record<string, boolean>) => Record<string, boolean>) => void;
    },
    key: string,
    value: boolean
  ) {
    stateSignal.update(current => ({ ...current, [key]: value }));
  }

  private getInitialEditorTheme(): EditorThemeChoice {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.editorTheme');
      if (stored) return stored as EditorThemeChoice;
    }
    return 'system';
  }

  private getInitialEditorFontSize(): number {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.editorFontSize');
      if (stored) return parseInt(stored, 10) || 14;
    }
    return 14;
  }

  private getInitialEditorFontFamily(): string {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.editorFontFamily');
      if (stored) return stored;
    }
    return 'IBM Plex Mono';
  }

  private getInitialEditorTabSize(): number {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.editorTabSize');
      if (stored) return parseInt(stored, 10) || 4;
    }
    return 4;
  }

  onMainSplitDragEnd(event: SplitGutterInteractionEvent) {
    const sizes = event.sizes.map(s => typeof s === 'number' ? s : parseFloat(s as any) || 0);
    if (this.mainSplitDirection() === 'horizontal') {
      this.mainSplitSizesHorizontal.set(sizes);
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem('compylr.mainSplitSizesHorizontal', JSON.stringify(sizes));
      }
    } else {
      this.mainSplitSizesVertical.set(sizes);
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem('compylr.mainSplitSizesVertical', JSON.stringify(sizes));
      }
    }
  }

  onLeftSplitDragEnd(event: SplitGutterInteractionEvent) {
    const sizes = event.sizes.map(s => typeof s === 'number' ? s : parseFloat(s as any) || 0);
    this.leftSplitSizes.set(sizes);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('compylr.leftSplitSizes', JSON.stringify(sizes));
    }
  }

  onRightSplitDragEnd(event: SplitGutterInteractionEvent) {
    const sizes = event.sizes.map(s => typeof s === 'number' ? s : parseFloat(s as any) || 0);
    this.rightSplitSizes.set(sizes);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('compylr.rightSplitSizes', JSON.stringify(sizes));
    }
  }

  private getInitialMainSplitSizesHorizontal(): number[] {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.mainSplitSizesHorizontal');
      if (stored) {
        try {
          return JSON.parse(stored);
        } catch (e) {}
      }
    }
    return [46, 54];
  }

  private getInitialMainSplitSizesVertical(): number[] {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.mainSplitSizesVertical');
      if (stored) {
        try {
          return JSON.parse(stored);
        } catch (e) {}
      }
    }
    return [50, 50];
  }

  private getInitialLeftSplitSizes(): number[] {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.leftSplitSizes');
      if (stored) {
        try {
          return JSON.parse(stored);
        } catch (e) {}
      }
    }
    return [62, 38];
  }

  private getInitialRightSplitSizes(): number[] {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem('compylr.rightSplitSizes');
      if (stored) {
        try {
          return JSON.parse(stored);
        } catch (e) {}
      }
    }
    return [60, 40];
  }

  stripHtml(html: string): string {
    if (!html) return '';
    return html.replace(/<[^>]*>?/gm, '');
  }

  canEdit(authorUsername: string): boolean {
    if (!authorUsername || authorUsername === '[Deleted]') return false;
    const user = this.authService.currentUser$();
    return !!(user && user.username?.trim().toLowerCase() === authorUsername.trim().toLowerCase());
  }

  canDelete(authorUsername: string): boolean {
    if (!authorUsername || authorUsername === '[Deleted]') return false;
    return this.authService.isAdmin$() || this.canEdit(authorUsername);
  }
}

function formatCodeSimple(text: string, language: string, tabSize: number, insertSpaces: boolean): string {
  const indentChar = insertSpaces ? ' '.repeat(tabSize) : '\t';
  const lines = text.split(/\r?\n/);
  let indentLevel = 0;
  const formattedLines: string[] = [];
  const isPython = language === 'python';

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const trimmed = line.trim();

    if (trimmed === '') {
      formattedLines.push('');
      continue;
    }

    if (isPython) {
      const startsWithDedent = /^(elif|else|except|finally)\b/.test(trimmed);
      if (startsWithDedent) {
        indentLevel = Math.max(0, indentLevel - 1);
      }

      const indent = indentChar.repeat(indentLevel);
      formattedLines.push(indent + trimmed);

      if (startsWithDedent) {
        indentLevel++;
      }

      if (trimmed.endsWith(':')) {
        indentLevel++;
      }
    } else {
      let openingCount = 0;
      let closingCount = 0;

      for (let j = 0; j < trimmed.length; j++) {
        const char = trimmed[j];
        if (char === '{') openingCount++;
        else if (char === '}') closingCount++;
      }

      const startsWithClosing = trimmed.startsWith('}');
      if (startsWithClosing) {
        indentLevel = Math.max(0, indentLevel - 1);
      }

      const indent = indentChar.repeat(indentLevel);
      formattedLines.push(indent + trimmed);

      if (startsWithClosing) {
        indentLevel = Math.max(0, indentLevel + (openingCount - (closingCount - 1)));
      } else {
        indentLevel = Math.max(0, indentLevel + (openingCount - closingCount));
      }
    }
  }

  return formattedLines.join('\n');
}
