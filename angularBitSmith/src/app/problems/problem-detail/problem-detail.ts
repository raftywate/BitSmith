import { Component, inject } from '@angular/core';
import { CommonModule } from "@angular/common";
import { ActivatedRoute, RouterModule } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { switchMap, map } from 'rxjs/operators';
import { Problem } from '../../services/problem';
import { AuthService } from '../../services/auth';
import { SkeletonComponent } from '../../components/skeleton/skeleton';
import { of } from 'rxjs';

@Component({
    selector: 'app-problem-detail',
    standalone: true,
    imports: [CommonModule, RouterModule, SkeletonComponent],
    templateUrl: './problem-detail.html',
    styleUrl: './problem-detail.scss'
})
export class ProblemDetail {
    private route = inject(ActivatedRoute);
    private problemService = inject(Problem);
    private authService = inject(AuthService);

    public problem = toSignal(
        this.route.paramMap.pipe(
            map(params => params.get('id')),
            switchMap(id => id ? this.problemService.getProblemById(id) : of(null))
        )
    );

    public isLoggedIn = this.authService.isLoggedIn$;
}
