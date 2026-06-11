import { CommonModule } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { UserProfile } from '../models/user-profile';
import { UserService } from '../services/user';
import { ToastService } from '../services/toast';
import { getApiErrorMessage } from '../utils/api-error';
import { AuthService } from '../services/auth';

@Component({
  selector: 'app-profile',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss'
})
export class ProfileComponent {
  private readonly userService = inject(UserService);
  private readonly toastService = inject(ToastService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly authService = inject(AuthService);

  profile = signal<UserProfile | null>(null);
  isLoading = signal(true);
  isSaving = signal(false);
  isEditing = signal(false);
  isUploadingAvatar = signal(false);
  avatarMenuOpen = signal(false);
  avatarEditorOpen = signal(false);
  selectedAvatarDataUrl = signal<string | null>(null);
  avatarZoom = signal(1);
  avatarRotation = signal(0);
  avatarOffsetX = signal(0);
  avatarOffsetY = signal(0);
  avatarError = signal<string | null>(null);

  profileForm: FormGroup = this.fb.nonNullable.group({
    displayName: ['', Validators.maxLength(100)],
    bio: ['', Validators.maxLength(500)]
  });

  heatmapDays = computed(() => this.buildHeatmapDays());

  constructor() {
    this.userService
      .getMyProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: profile => {
          this.profile.set(profile);
          this.authService.updateCurrentUserProfilePicture(profile.profilePictureUrl);
          this.syncProfileForm(profile);
          this.isLoading.set(false);
        },
        error: error => {
          this.toastService.error(getApiErrorMessage(error, 'Unable to load your profile.'));
          this.isLoading.set(false);
        }
      });
  }

  saveProfile() {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    const payload = this.profileForm.getRawValue();

    this.userService
      .updateMyProfile({
        displayName: payload.displayName || null,
        bio: payload.bio || null
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: profile => {
          this.profile.set(profile);
          this.isEditing.set(false);
          this.toastService.success('Profile updated.');
          this.isSaving.set(false);
        },
        error: error => {
          this.toastService.error(getApiErrorMessage(error, 'Unable to update your profile.'));
          this.isSaving.set(false);
        }
      });
  }

  initials() {
    const source = this.profile()?.displayName || this.profile()?.username || 'B';
    return source.slice(0, 2).toUpperCase();
  }

  startEditing() {
    const profile = this.profile();
    if (!profile) {
      return;
    }

    this.syncProfileForm(profile);
    this.isEditing.set(true);
  }

  cancelEditing() {
    this.startEditing();
    this.isEditing.set(false);
  }

  toggleAvatarMenu() {
    this.avatarMenuOpen.update(open => !open);
  }

  uploadAvatar(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      return;
    }

    this.avatarError.set(null);

    if (!file.type.startsWith('image/')) {
      this.avatarError.set('Choose an image file.');
      input.value = '';
      return;
    }

    if (file.size > 2 * 1024 * 1024) {
      this.avatarError.set('Avatar must be 2 MB or smaller.');
      input.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.selectedAvatarDataUrl.set(String(reader.result));
      this.avatarZoom.set(1);
      this.avatarRotation.set(0);
      this.avatarOffsetX.set(0);
      this.avatarOffsetY.set(0);
      this.avatarEditorOpen.set(true);
      this.avatarMenuOpen.set(false);
      input.value = '';
    };
    reader.onerror = () => {
      this.avatarError.set('Unable to preview this image.');
      input.value = '';
    };
    reader.readAsDataURL(file);
  }

  cancelAvatarEdit() {
    this.avatarEditorOpen.set(false);
    this.selectedAvatarDataUrl.set(null);
    this.avatarError.set(null);
  }

  async saveAvatarEdit() {
    const dataUrl = this.selectedAvatarDataUrl();
    if (!dataUrl) {
      return;
    }

    this.isUploadingAvatar.set(true);

    try {
      const blob = await this.createAvatarBlob(dataUrl);
      const file = new File([blob], 'avatar.png', { type: 'image/png' });

      this.userService
        .uploadMyAvatar(file)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: profile => {
            this.profile.set(profile);
            this.authService.updateCurrentUserProfilePicture(profile.profilePictureUrl);
            this.avatarEditorOpen.set(false);
            this.selectedAvatarDataUrl.set(null);
            this.toastService.success('Avatar updated.');
            this.isUploadingAvatar.set(false);
          },
          error: error => {
            this.avatarError.set(getApiErrorMessage(error, 'Unable to upload avatar.'));
            this.toastService.error(this.avatarError() ?? 'Unable to upload avatar.');
            this.isUploadingAvatar.set(false);
          }
        });
    } catch {
      this.avatarError.set('Unable to prepare this avatar.');
      this.isUploadingAvatar.set(false);
    }
  }

  removeAvatar() {
    this.avatarError.set(null);
    this.isUploadingAvatar.set(true);

    this.userService
      .removeMyAvatar()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: profile => {
          this.profile.set(profile);
          this.authService.updateCurrentUserProfilePicture(profile.profilePictureUrl);
          this.avatarMenuOpen.set(false);
          this.toastService.success('Avatar removed.');
          this.isUploadingAvatar.set(false);
        },
        error: error => {
          this.avatarError.set(getApiErrorMessage(error, 'Unable to remove avatar.'));
          this.toastService.error(this.avatarError() ?? 'Unable to remove avatar.');
          this.isUploadingAvatar.set(false);
        }
      });
  }

  heatmapLevel(count: number) {
    if (count >= 5) {
      return 4;
    }

    if (count >= 3) {
      return 3;
    }

    if (count >= 1) {
      return 2;
    }

    return 1;
  }

  private buildHeatmapDays() {
    const profile = this.profile();
    const activity = new Map((profile?.stats.activity ?? []).map(day => [day.date.slice(0, 10), day.count]));
    const today = new Date();
    const days = [];

    for (let offset = 363; offset >= 0; offset--) {
      const date = new Date(today);
      date.setDate(today.getDate() - offset);
      const key = date.toISOString().slice(0, 10);
      days.push({
        date: key,
        count: activity.get(key) ?? 0
      });
    }

    return days;
  }

  private syncProfileForm(profile: UserProfile) {
    this.profileForm.reset({
      displayName: profile.displayName ?? '',
      bio: profile.bio ?? ''
    });
  }

  private createAvatarBlob(dataUrl: string) {
    return new Promise<Blob>((resolve, reject) => {
      const image = new Image();
      image.onload = () => {
        const canvas = document.createElement('canvas');
        const size = 512;
        canvas.width = size;
        canvas.height = size;

        const context = canvas.getContext('2d');
        if (!context) {
          reject();
          return;
        }

        context.fillStyle = '#000000';
        context.fillRect(0, 0, size, size);
        context.translate(size / 2 + this.avatarOffsetX(), size / 2 + this.avatarOffsetY());
        context.rotate((this.avatarRotation() * Math.PI) / 180);

        const baseScale = Math.max(size / image.width, size / image.height);
        const scale = baseScale * this.avatarZoom();
        const width = image.width * scale;
        const height = image.height * scale;
        context.drawImage(image, -width / 2, -height / 2, width, height);

        canvas.toBlob(blob => (blob ? resolve(blob) : reject()), 'image/png', 0.92);
      };
      image.onerror = reject;
      image.src = dataUrl;
    });
  }
}
