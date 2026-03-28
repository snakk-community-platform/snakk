/**
 * Lazy Script Loader
 *
 * Loads non-critical scripts on first interaction or browser idle.
 * Reads script URLs from <meta> tags to avoid inline Razor interpolation.
 */
(function () {
    'use strict';

    function loadScript(src: string): void {
        const s = document.createElement('script');
        s.src = src;
        document.body.appendChild(s);
    }

    // Hover popups: load on first pointer interaction
    const siteJsMeta = document.querySelector<HTMLMetaElement>('meta[name="lazy-site-js"]');
    if (siteJsMeta?.content) {
        let loaded = false;
        document.addEventListener('pointerover', function handler() {
            if (loaded) return;
            loaded = true;
            document.removeEventListener('pointerover', handler);
            loadScript(siteJsMeta.content);
        }, { passive: true, capture: true });
    }

    // Sidebar scrollbar + breadcrumb title: load when browser is idle
    const sidebarMeta = document.querySelector<HTMLMetaElement>('meta[name="lazy-sidebar-js"]');
    const breadcrumbMeta = document.querySelector<HTMLMetaElement>('meta[name="lazy-breadcrumb-js"]');

    function loadIdleScripts(): void {
        if (sidebarMeta?.content) loadScript(sidebarMeta.content);
        if (breadcrumbMeta?.content) loadScript(breadcrumbMeta.content);
    }

    if ('requestIdleCallback' in window) {
        (window as any).requestIdleCallback(loadIdleScripts);
    } else {
        setTimeout(loadIdleScripts, 200);
    }
})();
