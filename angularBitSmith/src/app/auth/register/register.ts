import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth';
import { ToastService } from '../../services/toast';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-register',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './register.html',
  styleUrl: './register.scss',
})
export class Register implements OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastService = inject(ToastService);
  private readonly route = inject(ActivatedRoute);

  constructor() {
    this.route.queryParams.subscribe(params => {
      const verifyEmail = params['verifyEmail'];
      if (verifyEmail) {
        this.verificationEmail.set(verifyEmail);
        this.requiresVerification.set(true);
        this.startCooldown();
      }
    });
  }

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  redirectUrl = this.router.parseUrl(this.router.url).queryParams['redirect'] ?? '/problems';
  showInviteField = signal(false);

  requiresVerification = signal(false);
  otpCode = signal('');
  verificationEmail = signal('');
  isVerifying = signal(false);
  resendCooldown = signal(0);
  private cooldownInterval: any = null;

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
      next: (res) => {
        if (res.requiresVerification) {
          this.verificationEmail.set(payload.email);
          this.requiresVerification.set(true);
          this.toastService.info('Verification OTP sent to your email.');
          this.startCooldown();
        } else {
          this.toastService.success('Your Compylr account is ready.');
          void this.router.navigateByUrl(this.redirectUrl);
        }
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

  startCooldown() {
    this.resendCooldown.set(60);
    if (this.cooldownInterval) {
      clearInterval(this.cooldownInterval);
    }
    this.cooldownInterval = setInterval(() => {
      this.resendCooldown.update(c => {
        if (c <= 1) {
          clearInterval(this.cooldownInterval);
          return 0;
        }
        return c - 1;
      });
    }, 1000);
  }

  onVerifyOtp() {
    const email = this.verificationEmail();
    const code = this.otpCode().trim();
    if (!code || code.length < 6) {
      this.toastService.warning('Please enter a valid 6-digit OTP code.');
      return;
    }

    this.isVerifying.set(true);
    this.errorMessage.set(null);

    this.authService.verifyOtp(email, code).subscribe({
      next: () => {
        this.toastService.success('Email verified! Welcome to Compylr.');
        if (this.cooldownInterval) clearInterval(this.cooldownInterval);
        void this.router.navigateByUrl(this.redirectUrl);
      },
      error: (error: HttpErrorResponse) => {
        this.isVerifying.set(false);
        const message = getApiErrorMessage(error, 'Incorrect or expired OTP verification code.');
        this.errorMessage.set(message);
        this.toastService.error(message);
      },
      complete: () => this.isVerifying.set(false)
    });
  }

  onResendOtp() {
    if (this.resendCooldown() > 0) return;

    this.isLoading.set(true);
    this.authService.resendOtp(this.verificationEmail()).subscribe({
      next: () => {
        this.toastService.success('A new verification OTP code has been sent.');
        this.startCooldown();
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);
        this.toastService.error(getApiErrorMessage(error, 'Failed to resend OTP.'));
      },
      complete: () => this.isLoading.set(false)
    });
  }

  ngOnDestroy() {
    if (this.cooldownInterval) {
      clearInterval(this.cooldownInterval);
    }
  }

}
