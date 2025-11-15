import { Injectable, signal } from '@angular/core';

// Define the possible theme states
type Theme = 'light' | 'dark' | 'system';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {

  // Create a signal to hold the current theme state
  // We initialize it by loading the user's saved preference
  currentTheme = signal<Theme>(this.loadTheme());

  constructor() {
    // When the service is created, apply the theme immediately
    this.applyTheme(this.currentTheme());

    // Also, listen for *changes* to the system's theme
    // (e.g., if their OS changes from light to dark at 8 PM)
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if (this.currentTheme() === 'system') {
        this.applySystemTheme();
      }
    });
  }

  /**
   * Loads the saved theme from localStorage, defaulting to 'system'
   */
  private loadTheme(): Theme {
    const savedTheme = localStorage.getItem('theme') as Theme | null;
    return savedTheme || 'system';
  }

  /**
   * Saves the user's choice to localStorage
   */
  private saveTheme(theme: Theme) {
    localStorage.setItem('theme', theme);
  }

  /**
   * The main logic: applies the correct class to the <html> tag
   */
  private applyTheme(theme: Theme) {
    if (theme === 'system') {
      this.applySystemTheme();
    } else {
      this.setDarkClass(theme === 'dark');
    }
  }

  /**
   * Checks the browser's (prefers-color-scheme) media query
   */
  private applySystemTheme() {
    const isSystemDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    this.setDarkClass(isSystemDark);
  }

  /**
   * The "workhorse" function that actually adds/removes the 'dark' class
   */
  private setDarkClass(isDark: boolean) {
    if (isDark) {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }

  // --- PUBLIC METHODS FOR COMPONENTS ---

  /**
   * Sets the theme to 'dark', saves it, and applies it.
   */
  setDarkTheme() {
    this.currentTheme.set('dark');
    this.saveTheme('dark');
    this.applyTheme('dark');
  }

  /**
   * Sets the theme to 'light', saves it, and applies it.
   */
  setLightTheme() {
    this.currentTheme.set('light');
    this.saveTheme('light');
    this.applyTheme('light');
  }

  /**
   * Sets the theme to 'system', saves it, and applies it.
   */
  setSystemTheme() {
    this.currentTheme.set('system');
    this.saveTheme('system');
    this.applyTheme('system');
  }
}