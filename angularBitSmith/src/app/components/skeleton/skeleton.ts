import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-skeleton',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div 
      class="animate-pulse bg-gray-200 dark:bg-gray-700 rounded"
      [style.width]="width"
      [style.height]="height"
      [ngClass]="classes"
    ></div>
  `
})
export class SkeletonComponent {
    @Input() width = '100%';
    @Input() height = '1rem';
    @Input() classes = '';
}
