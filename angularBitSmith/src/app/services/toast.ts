import { Injectable, signal } from '@angular/core';

export interface Toast {
    id: number;
    message: string;
    type: 'success' | 'error' | 'info' | 'warning';
    duration: number;
}

@Injectable({
    providedIn: 'root'
})
export class ToastService {
    toasts = signal<Toast[]>([]);
    private counter = 0;

    show(
        message: string,
        type: 'success' | 'error' | 'info' | 'warning' = 'info',
        duration = 4200
    ) {
        const id = this.counter++;
        const toast: Toast = { id, message, type, duration };

        this.toasts.update(current => [...current, toast]);

        setTimeout(() => {
            this.remove(id);
        }, duration);
    }

    success(message: string) {
        this.show(message, 'success');
    }

    error(message: string) {
        this.show(message, 'error');
    }

    info(message: string) {
        this.show(message, 'info');
    }

    warning(message: string) {
        this.show(message, 'warning');
    }

    remove(id: number) {
        this.toasts.update(current => current.filter(t => t.id !== id));
    }
}
