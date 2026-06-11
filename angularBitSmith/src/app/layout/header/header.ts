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
    imports: [CommonModule, RouterLink, RouterLinkActive],
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

    private updatePrevNextProblem() {
        const path = this.getPath();
        const parts = path.split('/');
        if (parts.length === 3 && parts[1] === 'problems') {
            const currentId = parts[2];
            const index = this.problems.findIndex(p => p.id === currentId);
            if (index !== -1) {
                this.prevProblemId = index > 0 ? this.problems[index - 1].id : null;
                this.nextProblemId = index < this.problems.length - 1 ? this.problems[index + 1].id : null;
                return;
            }
        }
        this.prevProblemId = null;
        this.nextProblemId = null;
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
