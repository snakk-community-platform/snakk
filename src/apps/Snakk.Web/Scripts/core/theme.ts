/**
 * Theme Management
 * Manages light/dark/auto theme preferences with system theme detection.
 * Critical theme detection (data-theme + dark CSS) is inlined in _Layout.cshtml <head>.
 * This deferred script handles: toggle UI, system listener, dynamic CSS load/unload.
 */

// ============================================================================
// Type Definitions
// ============================================================================

type ThemePreference = 'light' | 'dark' | 'auto';
type DaisyUITheme = 'lofi' | 'dark';

interface SnakkTheme {
    getPreference(): ThemePreference;
    setPreference(preference: ThemePreference): void;
    getSystemTheme(): 'light' | 'dark';
    getEffectiveTheme(): DaisyUITheme;
    applyTheme(): void;
    toggleTheme(): void;
    updateToggleButton(): void;
    setupSystemThemeListener(): void;
    init(): void;
}


// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    const THEME_KEY = 'snakk_theme_preference';
    const LIGHT_THEME: DaisyUITheme = 'lofi';
    const DARK_THEME: DaisyUITheme = 'dark';
    const DARK_THEME_CSS_ID = 'dark-theme-css';

    // Theme preferences: 'light', 'dark', 'auto'
    const PREF_LIGHT: ThemePreference = 'light';
    const PREF_DARK: ThemePreference = 'dark';
    const PREF_AUTO: ThemePreference = 'auto';

    let systemThemeListener: ((e: MediaQueryListEvent) => void) | null = null;

    // Get dark theme CSS URL from meta tag (set by _Layout.cshtml with version hash)
    function getDarkThemeCssUrl(): string {
        const meta = document.querySelector('meta[name="dark-theme-css"]');
        return meta?.getAttribute('content') ?? '/css/vendor/dark-theme.css';
    }

    // Load dark theme CSS stylesheet into <head>
    function loadDarkThemeCSS(): void {
        // Already loaded (either by document.write in <head> or by a previous toggle)
        if (document.getElementById(DARK_THEME_CSS_ID)) return;

        const link = document.createElement('link');
        link.id = DARK_THEME_CSS_ID;
        link.rel = 'stylesheet';
        link.href = getDarkThemeCssUrl();
        document.head.appendChild(link);
    }

    // Remove dark theme CSS stylesheet from <head>
    function unloadDarkThemeCSS(): void {
        const link = document.getElementById(DARK_THEME_CSS_ID);
        if (link) link.remove();
    }

    const snakkTheme: SnakkTheme = {
        // Get user preference (light/dark/auto)
        getPreference(): ThemePreference {
            const stored = localStorage.getItem(THEME_KEY);
            if (stored === 'light' || stored === 'dark' || stored === 'auto') {
                return stored;
            }
            return PREF_AUTO;
        },

        // Set user preference
        setPreference(preference: ThemePreference): void {
            localStorage.setItem(THEME_KEY, preference);
            this.applyTheme();
        },

        // Get system theme preference
        getSystemTheme(): 'light' | 'dark' {
            return window.matchMedia('(prefers-color-scheme: dark)').matches ? PREF_DARK : PREF_LIGHT;
        },

        // Get effective theme to apply (resolves 'auto' to light/dark)
        getEffectiveTheme(): DaisyUITheme {
            const preference = this.getPreference();

            if (preference === PREF_AUTO) {
                return this.getSystemTheme() === PREF_DARK ? DARK_THEME : LIGHT_THEME;
            }

            return preference === PREF_DARK ? DARK_THEME : LIGHT_THEME;
        },

        // Apply theme to document and load/unload dark CSS as needed
        applyTheme(): void {
            const theme = this.getEffectiveTheme();
            document.documentElement.setAttribute('data-theme', theme);

            if (theme === DARK_THEME) {
                loadDarkThemeCSS();
            } else {
                unloadDarkThemeCSS();
            }

            this.updateToggleButton();
        },

        // Toggle through: light → dark → auto → light
        toggleTheme(): void {
            const current = this.getPreference();
            let next: ThemePreference;

            switch (current) {
                case PREF_LIGHT:
                    next = PREF_DARK;
                    break;
                case PREF_DARK:
                    next = PREF_AUTO;
                    break;
                case PREF_AUTO:
                default:
                    next = PREF_LIGHT;
                    break;
            }

            this.setPreference(next);
        },

        // Update toggle button icon and text
        updateToggleButton(): void {
            const button = document.getElementById('theme-toggle');
            if (!button) return;

            const preference = this.getPreference();
            let icon: string;
            let text: string;

            // Toggle switch indicator icon (shown on the right)
            const toggleIndicator = `<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 ml-auto opacity-60" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                       <rect x="3" y="8" width="18" height="8" rx="4" stroke-width="2"/>
                                       <circle cx="15" cy="12" r="3" fill="currentColor"/>
                                     </svg>`;

            switch (preference) {
                case PREF_LIGHT:
                    // Sun icon
                    icon = `<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" />
                            </svg>`;
                    text = 'Light mode';
                    break;
                case PREF_DARK:
                    // Moon icon
                    icon = `<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
                            </svg>`;
                    text = 'Dark mode';
                    break;
                case PREF_AUTO:
                default:
                    // Split sun/moon icon (using a simple circle with half filled)
                    icon = `<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <circle cx="12" cy="12" r="9" stroke-width="2" fill="none"/>
                              <path d="M12 3 A 9 9 0 0 1 12 21 Z" fill="currentColor" stroke="none"/>
                            </svg>`;
                    text = 'Auto mode';
                    break;
            }

            button.innerHTML = icon + text + toggleIndicator;
        },

        // Setup system theme listener for auto mode
        setupSystemThemeListener(): void {
            const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

            // Remove existing listener if any
            if (systemThemeListener) {
                mediaQuery.removeEventListener('change', systemThemeListener);
            }

            // Add new listener
            systemThemeListener = (_e: MediaQueryListEvent) => {
                if (this.getPreference() === PREF_AUTO) {
                    this.applyTheme();
                }
            };

            mediaQuery.addEventListener('change', systemThemeListener);
        },

        init(): void {
            // Theme already applied by inline script in <head> — just sync CSS state and UI
            const theme = this.getEffectiveTheme();
            if (theme === DARK_THEME) {
                loadDarkThemeCSS();
            }
            this.updateToggleButton();

            // Setup listener for system theme changes
            this.setupSystemThemeListener();
        }
    };

    // Export to window
    (window as any).snakkTheme = snakkTheme;

    // Initialize theme on load
    snakkTheme.init();
})();
