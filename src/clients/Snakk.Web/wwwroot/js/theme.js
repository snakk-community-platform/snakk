// Theme Management
(function() {
    'use strict';

    const THEME_KEY = 'snakk_theme';
    const LIGHT_THEME = 'lofi';
    const DARK_THEME = 'dark';

    window.snakkTheme = {
        getTheme: function() {
            return localStorage.getItem(THEME_KEY) || LIGHT_THEME;
        },

        setTheme: function(theme) {
            localStorage.setItem(THEME_KEY, theme);
            document.documentElement.setAttribute('data-theme', theme);
        },

        toggleTheme: function() {
            const currentTheme = this.getTheme();
            const newTheme = currentTheme === LIGHT_THEME ? DARK_THEME : LIGHT_THEME;
            this.setTheme(newTheme);
            this.updateToggleButton();
        },

        updateToggleButton: function() {
            const button = document.getElementById('theme-toggle');
            if (!button) return;

            const currentTheme = this.getTheme();
            const isDark = currentTheme === DARK_THEME;

            button.innerHTML = isDark
                ? `<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                     <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" />
                   </svg>`
                : `<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                     <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
                   </svg>`;
        },

        init: function() {
            // Apply saved theme immediately (before page renders)
            const savedTheme = this.getTheme();
            document.documentElement.setAttribute('data-theme', savedTheme);
        }
    };

    // Initialize theme on load
    window.snakkTheme.init();
})();
