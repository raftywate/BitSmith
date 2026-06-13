import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth';
import { ToastService } from '../../services/toast';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-login',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private toastService = inject(ToastService);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  redirectUrl = this.router.parseUrl(this.router.url).queryParams['redirect'] ?? '/problems';

  loginForm: FormGroup = this.fb.nonNullable.group({
    emailOrUsername: ['', [Validators.required]],
    password: ['', Validators.required]
  });

  get emailOrUsername() {
    return this.loginForm.get('emailOrUsername');
  }

  get password() {
    return this.loginForm.get('password');
  }

  onSubmit(): void {
    this.errorMessage.set(null);

    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    const { emailOrUsername, password } = this.loginForm.getRawValue();

    this.authService.login({ emailOrUsername, password }).subscribe({
      next: () => {
        this.toastService.success('Welcome back to Compylr.');
        void this.router.navigateByUrl(this.redirectUrl);
      },
      error: (err: HttpErrorResponse) => {
        this.isLoading.set(false);
        if (err.status === 429) {
          this.errorMessage.set('Too many login attempts. Please wait a moment and try again.');
          this.toastService.warning('Login rate limit reached.');
        } else if (err.status === 403 && err.error?.error?.startsWith('verification_required:')) {
          const email = err.error.error.split(':')[1];
          this.toastService.info('Please verify your email address. We sent a new code.');
          void this.router.navigate(['/register'], { queryParams: { verifyEmail: email, redirect: this.redirectUrl } });
        } else {
          const message = getApiErrorMessage(err, 'Unable to sign in right now.');
          this.errorMessage.set(message);
          this.toastService.error(message);
        }
        console.error('Login error:', err);
      },
      complete: () => this.isLoading.set(false)
    });
  }
}
