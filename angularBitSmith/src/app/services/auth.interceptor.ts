import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth';
import { ToastService } from './toast';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const toastService = inject(ToastService);
  const token = authService.getToken();

  const authenticatedRequest = token
    ? request.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      })
    : request;

  return next(authenticatedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      const isAuthRoute =
        request.url.includes('/auth/login') || request.url.includes('/auth/register');

      if (error.status === 401 && token && !isAuthRoute) {
        authService.logout(true);
        toastService.warning('Your session expired. Sign in again to keep working.');
      }

      return throwError(() => error);
    })
  );
};
