// Theme Management
(function() {
    'use strict';

    const THEME_KEY = 'snakk_theme_preference';
    const LIGHT_THEME = 'lofi';
    const DARK_THEME = 'dark';

    // Theme preferences: 'light', 'dark', 'auto'
    const PREF_LIGHT = 'light';
    const PREF_DARK = 'dark';
    const PREF_AUTO = 'auto';

    let systemThemeListener = null;

    window.snakkTheme = {
        // Get user preference (light/dark/auto)
        getPreference: function() {
            return localStorage.getItem(THEME_KEY) || PREF_AUTO;
        },

        // Set user preference
        setPreference: function(preference) {
            localStorage.setItem(THEME_KEY, preference);
            this.applyTheme();
        },

        // Get system theme preference
        getSystemTheme: function() {
            return window.matchMedia('(prefers-color-scheme: dark)').matches ? PREF_DARK : PREF_LIGHT;
        },

        // Get effective theme to apply (resolves 'auto' to light/dark)
        getEffectiveTheme: function() {
            const preference = this.getPreference();

            if (preference === PREF_AUTO) {
                return this.getSystemTheme() === PREF_DARK ? DARK_THEME : LIGHT_THEME;
            }

            return preference === PREF_DARK ? DARK_THEME : LIGHT_THEME;
        },

        // Apply theme to document
        applyTheme: function() {
            const theme = this.getEffectiveTheme();
            document.documentElement.setAttribute('data-theme', theme);
            this.updateToggleButton();
        },

        // Toggle through: light → dark → auto → light
        toggleTheme: function() {
            const current = this.getPreference();
            let next;

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
        updateToggleButton: function() {
            const button = document.getElementById('theme-toggle');
            if (!button) return;

            const preference = this.getPreference();
            let icon, text;

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
        setupSystemThemeListener: function() {
            // Remove existing listener if any
            if (systemThemeListener) {
                window.matchMedia('(prefers-color-scheme: dark)').removeEventListener('change', systemThemeListener);
            }

            // Add new listener
            systemThemeListener = (e) => {
                if (this.getPreference() === PREF_AUTO) {
                    this.applyTheme();
                }
            };

            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', systemThemeListener);
        },

        init: function() {
            // Apply saved theme immediately (before page renders)
            this.applyTheme();

            // Setup listener for system theme changes
            this.setupSystemThemeListener();
        }
    };

    // Initialize theme on load
    window.snakkTheme.init();
})();
