import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { UserService } from '../services/user';
import { ToastService } from '../services/toast';
import { AuthService } from '../services/auth';
import { getApiErrorMessage } from '../utils/api-error';

const passwordMatchValidator = (group: AbstractControl): ValidationErrors | null => {
  const currentPassword = group.get('currentPassword');
  const newPassword = group.get('newPassword');
  const confirmNewPassword = group.get('confirmNewPassword');

  const errors: ValidationErrors = {};

  if (newPassword?.value) {
    if (!currentPassword?.value) {
      errors['currentPasswordRequired'] = true;
    }
    if (newPassword.value !== confirmNewPassword?.value) {
      errors['passwordsMismatch'] = true;
    }
  }

  return Object.keys(errors).length > 0 ? errors : null;
};

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './settings.html',
  styleUrl: './settings.scss'
})
export class SettingsComponent {
  private readonly userService = inject(UserService);
  private readonly toastService = inject(ToastService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  isLoading = signal(true);
  isSaving = signal(false);
  errorMessage = signal<string | null>(null);

  settingsForm: FormGroup = this.fb.group({
    username: ['', [Validators.required, Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    currentPassword: [''],
    newPassword: ['', [Validators.minLength(6)]],
    confirmNewPassword: ['']
  }, { validators: passwordMatchValidator });

  constructor() {
    this.loadSettings();
  }

  loadSettings() {
    this.userService
      .getMyProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: profile => {
          this.settingsForm.patchValue({
            username: profile.username,
            email: profile.email
          });
          this.isLoading.set(false);
        },
        error: error => {
          this.toastService.error(getApiErrorMessage(error, 'Unable to load your settings.'));
          this.isLoading.set(false);
        }
      });
  }

  saveSettings() {
    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);

    const formValues = this.settingsForm.value;
    const payload: any = {
      username: formValues.username,
      email: formValues.email
    };

    if (formValues.newPassword) {
      payload.currentPassword = formValues.currentPassword;
      payload.newPassword = formValues.newPassword;
    }

    this.userService
      .updateMySettings(payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: updatedProfile => {
          this.toastService.success('Account settings updated successfully.');
          
          // Sync with AuthService
          this.authService.updateCurrentUserDetails(
            updatedProfile.username,
            updatedProfile.profilePictureUrl
          );

          // Clear password fields
          this.settingsForm.patchValue({
            currentPassword: '',
            newPassword: '',
            confirmNewPassword: ''
          });
          // Reset form validation state for password fields specifically
          this.settingsForm.get('currentPassword')?.markAsPristine();
          this.settingsForm.get('currentPassword')?.markAsUntouched();
          this.settingsForm.get('newPassword')?.markAsPristine();
          this.settingsForm.get('newPassword')?.markAsUntouched();
          this.settingsForm.get('confirmNewPassword')?.markAsPristine();
          this.settingsForm.get('confirmNewPassword')?.markAsUntouched();

          this.isSaving.set(false);
        },
        error: error => {
          const msg = getApiErrorMessage(error, 'Failed to update account settings.');
          this.errorMessage.set(msg);
          this.toastService.error(msg);
          this.isSaving.set(false);
        }
      });
  }
}
