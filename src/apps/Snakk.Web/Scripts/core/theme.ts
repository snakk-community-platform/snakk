/**
 * Theme Management
 * Manages light/dark/auto theme preferences with system theme detection.
 * Dark theme is CSS-variable-based ([data-theme="dark"] in _variables.scss) — no separate stylesheet.
 * Critical FOUC prevention (sets data-theme="dark" for auto/dark users) is inlined in _Layout.cshtml <head>.
 * This deferred script handles: toggle UI, system theme listener, preference persistence.
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

    // Theme preferences: 'light', 'dark', 'auto'
    const PREF_LIGHT: ThemePreference = 'light';
    const PREF_DARK: ThemePreference = 'dark';
    const PREF_AUTO: ThemePreference = 'auto';

    const THEME_COOKIE = '.Snakk.Pref.Theme';

    let systemThemeListener: ((e: MediaQueryListEvent) => void) | null = null;

    function isLoggedIn(): boolean {
        return document.body.dataset.auth === '1';
    }

    function readThemeCookie(): ThemePreference | null {
        const entry = document.cookie.split('; ').find(r => r.startsWith(THEME_COOKIE + '='));
        if (!entry) return null;
        const val = entry.split('=')[1];
        return (val === 'light' || val === 'dark' || val === 'auto') ? val : null;
    }

    function persistTheme(pref: ThemePreference): void {
        localStorage.setItem(THEME_KEY, pref);
        if (isLoggedIn()) {
            const maxAge = 365 * 24 * 60 * 60;
            document.cookie = `${THEME_COOKIE}=${pref}; path=/; max-age=${maxAge}; samesite=lax; secure`;
        }
    }

    const snakkTheme: SnakkTheme = {
        // Get user preference (light/dark/auto)
        getPreference(): ThemePreference {
            if (isLoggedIn()) {
                const cookie = readThemeCookie();
                if (cookie) return cookie;
            }
            const stored = localStorage.getItem(THEME_KEY);
            if (stored === 'light' || stored === 'dark' || stored === 'auto') {
                return stored;
            }
            return PREF_LIGHT;
        },

        // Set user preference
        setPreference(preference: ThemePreference): void {
            persistTheme(preference);
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

        // Apply theme to document — dark is CSS-variable-based, just flip data-theme
        applyTheme(): void {
            const theme = this.getEffectiveTheme();
            document.documentElement.setAttribute('data-theme', theme);
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

            const toggleIndicator = '<span class="sn-icon icon-theme-toggle sn-h-4 sn-w-4 sn-ml-auto sn-opacity-60" aria-hidden="true"></span>';

            switch (preference) {
                case PREF_LIGHT:
                    icon = '<span class="sn-icon icon-sun sn-h-4 sn-w-4" aria-hidden="true"></span>';
                    text = 'Light mode';
                    break;
                case PREF_DARK:
                    icon = '<span class="sn-icon icon-moon sn-h-4 sn-w-4" aria-hidden="true"></span>';
                    text = 'Dark mode';
                    break;
                case PREF_AUTO:
                default:
                    icon = '<span class="sn-icon icon-theme-auto sn-h-4 sn-w-4" aria-hidden="true"></span>';
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
            // Sync cookie from localStorage for authenticated users whose cookie may be
            // missing (e.g. cleared cookies, or preference set before login).
            // This ensures the server sees the correct preference on the next F5.
            if (isLoggedIn() && !readThemeCookie()) {
                const stored = localStorage.getItem(THEME_KEY);
                if (stored === 'light' || stored === 'dark' || stored === 'auto') {
                    persistTheme(stored as ThemePreference);
                }
            }

            // The inline pre-paint script in _Layout.cshtml already set data-theme for
            // the "auto" case. Re-running applyTheme() is an idempotent sync that also
            // covers CSP-blocked inline scripts and localStorage changed across tabs.
            this.applyTheme();

            // Setup listener for system theme changes
            this.setupSystemThemeListener();
        }
    };

    // Export to window
    (window as any).snakkTheme = snakkTheme;

    // Initialize theme on load
    snakkTheme.init();
})();
