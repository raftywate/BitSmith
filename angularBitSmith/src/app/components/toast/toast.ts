import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../services/toast';

@Component({
    selector: 'app-toast',
    imports: [CommonModule],
    templateUrl: './toast.html',
    styleUrl: './toast.scss'
})
export class ToastComponent {
    toastService = inject(ToastService);

    iconFor(type: string) {
        switch (type) {
            case 'success':
                return '✓';
            case 'error':
                return '!';
            case 'warning':
                return '•';
            default:
                return 'i';
        }
    }
}
