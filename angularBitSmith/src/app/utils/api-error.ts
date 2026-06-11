import { HttpErrorResponse } from '@angular/common/http';

export function getApiErrorMessage(
  error: unknown,
  fallback = 'Something went wrong. Please try again.'
): string {
  if (error instanceof HttpErrorResponse) {
    if (typeof error.error === 'string' && error.error.trim()) {
      return error.error;
    }

    if (typeof error.error?.error === 'string' && error.error.error.trim()) {
      return error.error.error;
    }
  }

  return fallback;
}
