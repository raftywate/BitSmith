import { computed, effect, Injectable, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'black' | 'system';
export type ResolvedTheme = 'light' | 'dark' | 'black';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly storageKey = 'compylr.theme';
  private readonly mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

  readonly currentTheme = signal<ThemePreference>(this.loadThemePreference());
  readonly resolvedTheme = signal<ResolvedTheme>(this.resolveTheme(this.currentTheme()));
  readonly isDark = computed(() => this.resolvedTheme() === 'dark' || this.resolvedTheme() === 'black');

  constructor() {
    effect(() => {
      const preference = this.currentTheme();
      const resolved = this.resolveTheme(preference);

      this.resolvedTheme.set(resolved);
      localStorage.setItem(this.storageKey, preference);
      document.documentElement.dataset['theme'] = resolved;
      document.documentElement.style.colorScheme = resolved === 'light' ? 'light' : 'dark';
    });

    this.mediaQuery.addEventListener('change', () => {
      if (this.currentTheme() === 'system') {
        this.resolvedTheme.set(this.resolveTheme('system'));
        document.documentElement.dataset['theme'] = this.resolvedTheme();
        document.documentElement.style.colorScheme = this.resolvedTheme() === 'light' ? 'light' : 'dark';
      }
    });
  }

  setTheme(theme: ThemePreference) {
    this.currentTheme.set(theme);
  }

  toggleTheme() {
    const current = this.resolvedTheme();
    if (current === 'light') {
      this.setTheme('dark');
    } else if (current === 'dark') {
      this.setTheme('black');
    } else {
      this.setTheme('light');
    }
  }

  setLightTheme() {
    this.setTheme('light');
  }

  setDarkTheme() {
    this.setTheme('dark');
  }

  setBlackTheme() {
    this.setTheme('black');
  }

  setSystemTheme() {
    this.setTheme('system');
  }

  private loadThemePreference(): ThemePreference {
    const storedTheme = localStorage.getItem(this.storageKey) as ThemePreference | null;
    return storedTheme ?? 'system';
  }

  private resolveTheme(preference: ThemePreference): ResolvedTheme {
    if (preference === 'system') {
      return this.mediaQuery.matches ? 'dark' : 'light';
    }

    return preference;
  }
}
