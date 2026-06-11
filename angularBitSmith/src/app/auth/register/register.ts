import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth';
import { ToastService } from '../../services/toast';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-register',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './register.html',
  styleUrl: './register.scss',
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastService = inject(ToastService);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  redirectUrl = this.router.parseUrl(this.router.url).queryParams['redirect'] ?? '/problems';
  showInviteField = signal(false);

  registerForm: FormGroup = this.fb.nonNullable.group(
    {
      username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(100)]],
      confirmPassword: ['', Validators.required],
      inviteCode: ['']
    },
    {
      validators: control => {
        const password = control.get('password')?.value;
        const confirmPassword = control.get('confirmPassword')?.value;
        return password === confirmPassword ? null : { mismatch: true };
      }
    }
  );

  get username() {
    return this.registerForm.get('username');
  }

  get email() {
    return this.registerForm.get('email');
  }

  get password() {
    return this.registerForm.get('password');
  }

  get confirmPassword() {
    return this.registerForm.get('confirmPassword');
  }

  onSubmit() {
    this.errorMessage.set(null);

    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    const payload = this.registerForm.getRawValue();

    this.authService.register(payload).subscribe({
      next: () => {
        this.toastService.success('Your Compylr account is ready.');
        void this.router.navigateByUrl(this.redirectUrl);
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);

        if (error.status === 429) {
          this.errorMessage.set('Too many registration attempts. Please try again shortly.');
          this.toastService.warning('Registration rate limit reached.');
          return;
        }

        const message = getApiErrorMessage(error, 'Unable to create your account right now.');
        this.errorMessage.set(message);
        this.toastService.error(message);
        console.error('Registration error:', error);
      },
      complete: () => this.isLoading.set(false)
    });
  }

}
