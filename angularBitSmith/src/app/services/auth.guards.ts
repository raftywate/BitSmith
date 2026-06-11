import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth';

export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn$()) {
    return true;
  }

  return router.createUrlTree(['/login'], {
    queryParams: { redirect: state.url }
  });
};

export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.isLoggedIn$() ? router.createUrlTree(['/problems']) : true;
};

export const adminGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAdmin$()) {
    return true;
  }

  // If logged in but not admin, go home. If not logged in, redirect to login.
  if (authService.isLoggedIn$()) {
    return router.createUrlTree(['/problems']);
  }

  return router.createUrlTree(['/login'], {
    queryParams: { redirect: state.url }
  });
};
