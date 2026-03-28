/**
 * Lazy-loads the markdown editor script when the reply form enters the viewport
 * or when the user clicks reply/quote/edit buttons.
 * Reads the editor script URL from a JSON config element (#editor-loader-config).
 */
(function () {
    'use strict';

    let loaded = false;

    function loadEditor(): void {
        if (loaded) return;
        loaded = true;
        const cfgEl = document.getElementById('editor-loader-config');
        if (!cfgEl) return;
        const cfg = JSON.parse(cfgEl.textContent || '{}');
        if (!cfg.editorSrc) return;
        const s = document.createElement('script');
        s.src = cfg.editorSrc;
        document.body.appendChild(s);
    }

    function init(): void {
        const container = document.getElementById('reply-form-container') || document.getElementById('editor-container');
        if (container) {
            if ('IntersectionObserver' in window) {
                const obs = new IntersectionObserver((entries) => {
                    if (entries[0]?.isIntersecting) { loadEditor(); obs.disconnect(); }
                }, { rootMargin: '200px' });
                obs.observe(container);
            } else {
                loadEditor();
            }
        }
        document.addEventListener('click', (e) => {
            const btn = (e.target as HTMLElement).closest('[data-action="reply"], [data-action="quote-reply"], [data-action="edit-post"]');
            if (btn) loadEditor();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Re-init on HTMX swap (navigating between discussions)
    document.body.addEventListener('htmx:load', init);
})();
