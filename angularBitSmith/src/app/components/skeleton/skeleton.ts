import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-skeleton',
    imports: [CommonModule],
    template: `
    <div
      class="skeleton"
      [style.width]="width"
      [style.height]="height"
      [ngClass]="classes"
    ></div>
  `,
    styles: [`
    .skeleton {
      display: block;
      border-radius: 999px;
      background: var(--surface-2);
      animation: skeleton-pulse 1.4s ease-in-out infinite;
    }

    @keyframes skeleton-pulse {
      50% {
        opacity: 0.55;
      }
    }
  `]
})
export class SkeletonComponent {
    @Input() width = '100%';
    @Input() height = '1rem';
    @Input() classes = '';
}
