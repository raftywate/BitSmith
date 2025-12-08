import { Component, inject } from '@angular/core';
import { AsyncPipe, NgIf } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ProblemService } from '../../services/problem';
import { switchMap } from 'rxjs';

@Component({
  standalone: true,
  selector: 'app-problem-detail',
  templateUrl: './problem-detail.html',
  imports: [
    AsyncPipe,        
    RouterModule
  ]
})
export class ProblemDetail {
  private route = inject(ActivatedRoute);
  private problemService = inject(ProblemService);

  problem$ = this.route.paramMap.pipe(
    switchMap(params => this.problemService.getProblemById(params.get('id')!))
  );
}
