import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, HostListener, ElementRef } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { ThemeService } from '../../services/theme';
import { AuthService } from '../../services/auth';
import { ProblemService } from '../../services/problem';
import { WorkspaceActionService } from '../../services/workspace-action';
import { ProblemSummary } from '../../models/problem-summary';
import { filter } from 'rxjs';

@Component({
    selector: 'app-header',
    imports: [CommonModule, RouterLink],
    templateUrl: './header.html',
    styleUrl: './header.scss'
})
export class Header implements OnInit {
    public themeService = inject(ThemeService);
    public authService = inject(AuthService);
    public workspaceActionService = inject(WorkspaceActionService);
    private readonly router = inject(Router);
    private readonly problemService = inject(ProblemService);
    private readonly elementRef = inject(ElementRef);

    public isMenuOpen = false;
    private problems: ProblemSummary[] = [];
    public prevProblemId: string | null = null;
    public nextProblemId: string | null = null;
    public prevProblemQueryParams: Record<string, string> | null = null;
    public nextProblemQueryParams: Record<string, string> | null = null;

    ngOnInit() {
        this.loadProblems();

        // Listen to navigation events to update prev/next links
        this.router.events
            .pipe(
                filter((event): event is NavigationEnd => event instanceof NavigationEnd)
            )
            .subscribe(() => {
                this.updatePrevNextProblem();
            });
    }

    private loadProblems() {
        this.problemService.getProblems(1, 100).subscribe({
            next: (res) => {
                this.problems = res.problems;
                this.updatePrevNextProblem();
            },
            error: (err) => {
                console.error('Failed to load problems for navigation:', err);
            }
        });
    }

    private getPath(): string {
        return this.router.url.split('?')[0].split('#')[0];
    }

    public isHomePage(): boolean {
        return this.getPath() === '/problems';
    }

    public isProblemDetailPage(): boolean {
        const path = this.getPath();
        const parts = path.split('/');
        return parts.length === 3 && parts[1] === 'problems' && parts[2] !== '';
    }

    private getPodDate(): string | null {
        try {
            const urlObj = new URL(this.router.url, 'http://localhost');
            return urlObj.searchParams.get('podDate');
        } catch {
            return null;
        }
    }

    private updatePrevNextProblem() {
        const path = this.getPath();
        const parts = path.split('/');
        if (parts.length === 3 && parts[1] === 'problems') {
            const currentId = parts[2];
            const podDate = this.getPodDate();

            if (podDate) {
                this.nextProblemId = null;
                this.nextProblemQueryParams = null;
                this.prevProblemId = null;
                this.prevProblemQueryParams = null;

                const todayStr = new Date().toLocaleDateString('en-CA');

                try {
                    const dateParts = podDate.split('-');
                    const year = parseInt(dateParts[0], 10);
                    const month = parseInt(dateParts[1], 10) - 1; // 0-indexed month
                    const day = parseInt(dateParts[2], 10);

                    // Load Prev PoD
                    const prevDate = new Date(year, month, day);
                    prevDate.setDate(prevDate.getDate() - 1);
                    const prevDateStr = prevDate.toLocaleDateString('en-CA');
                    this.problemService.getProblemOfTheDay(prevDateStr).subscribe({
                        next: (prevPod) => {
                            if (prevPod && (prevPod.slug || prevPod.id)) {
                                this.prevProblemId = prevPod.slug || prevPod.id;
                                this.prevProblemQueryParams = { podDate: prevDateStr };
                            }
                        }
                    });

                    // Load Next PoD (only if podDate is before today)
                    if (podDate < todayStr) {
                        const nextDate = new Date(year, month, day);
                        nextDate.setDate(nextDate.getDate() + 1);
                        const nextDateStr = nextDate.toLocaleDateString('en-CA');
                        this.problemService.getProblemOfTheDay(nextDateStr).subscribe({
                            next: (nextPod) => {
                                if (nextPod && (nextPod.slug || nextPod.id)) {
                                    this.nextProblemId = nextPod.slug || nextPod.id;
                                    this.nextProblemQueryParams = { podDate: nextDateStr };
                                }
                            }
                        });
                    }
                } catch {
                    this.prevProblemId = null;
                    this.nextProblemId = null;
                }
                return;
            }

            const index = this.problems.findIndex(p => p.id === currentId || p.slug === currentId);
            if (index !== -1) {
                const prev = index > 0 ? this.problems[index - 1] : null;
                const next = index < this.problems.length - 1 ? this.problems[index + 1] : null;
                this.prevProblemId = prev ? (prev.slug || prev.id) : null;
                this.nextProblemId = next ? (next.slug || next.id) : null;
                this.prevProblemQueryParams = null;
                this.nextProblemQueryParams = null;
                return;
            }
        }
        this.prevProblemId = null;
        this.nextProblemId = null;
        this.prevProblemQueryParams = null;
        this.nextProblemQueryParams = null;
    }

    public toggleMenu(event: Event) {
        event.stopPropagation();
        this.isMenuOpen = !this.isMenuOpen;
    }

    @HostListener('document:click', ['$event'])
    public onDocumentClick(event: Event) {
        if (!this.elementRef.nativeElement.contains(event.target)) {
            this.isMenuOpen = false;
        }
    }

    logout() {
        this.authService.logout();
        this.router.navigate(['/problems']);
    }
}
