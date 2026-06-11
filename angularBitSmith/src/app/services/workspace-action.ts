import { Injectable, signal } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class WorkspaceActionService {
  public readonly runSamplesRequest$ = new Subject<void>();
  public readonly submitRequest$ = new Subject<void>();

  public readonly isRunningSamples = signal(false);
  public readonly isSubmitting = signal(false);
  public readonly hasWorkspaceActive = signal(false);

  public requestRunSamples() {
    this.runSamplesRequest$.next();
  }

  public requestSubmit() {
    this.submitRequest$.next();
  }
}
