/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        // --- YOUR NEW CUSTOM PALETTE ---

        // Base Backgrounds
        'bg-light': '#FFFFFF',       // White
        'bg-dark': '#0F172A',        // Deep Navy/Charcoal

        // Card / Surface Backgrounds
        'card-light': '#F7F8FA',     // Light Grayish
        'card-dark': '#1E293B',      // Dark Slate

        // Primary Brand Colors
        'primary-light': '#4F46E5',  // Indigo
        'primary-dark': '#818CF8',   // Soft Indigo
        
        'primary-hover-light': '#4338CA', // Darker Indigo
        'primary-hover-dark': '#6366F1',  // Brighter Indigo

        // Accent Colors
        'accent-light': '#06B6D4',   // Cyan
        'accent-dark': '#22D3EE',    // Bright Cyan

        // Text Colors
        'text-primary-light': '#1F2937', // Dark Gray
        'text-primary-dark': '#F1F5F9',  // Off-White

        'text-secondary-light': '#4B5563', // Medium Gray
        'text-secondary-dark': '#CBD5E1',  // Light Gray

        // Borders
        'border-light': '#E5E7EB',
        'border-dark': '#334155',

        // Status Colors (Light Mode)
        'success-light': '#16A34A',
        'warning-light': '#F59E0B',
        'error-light': '#DC2626',
        'info-light': '#2563EB',

        // Status Colors (Dark Mode)
        'success-dark': '#22C55E',
        'warning-dark': '#FBBF24',
        'error-dark': '#F87171',
        'info-dark': '#60A5FA',

        // Code Block Colors
        'code-bg-light': '#F3F4F6',
        'code-bg-dark': '#1E293B',
        'code-border-light': '#D1D5DB',
        'code-border-dark': '#334155'
      }
    },
  },
  plugins: [],
}