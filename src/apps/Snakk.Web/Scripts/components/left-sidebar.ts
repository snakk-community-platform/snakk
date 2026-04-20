(function(): void {
    'use strict';

    function updateActiveLink(): void {
        const path = location.pathname;
        document.querySelectorAll<HTMLAnchorElement>('.fp-nav .fp-nav-item').forEach(a => {
            const href = a.getAttribute('href') ?? '';
            let active: boolean;
            if (href.endsWith('/rules')) active = path.endsWith('/rules');
            else if (href.endsWith('/moderators')) active = path.endsWith('/moderators');
            else active = path === href;
            a.classList.toggle('active', active);
        });
    }

    document.addEventListener('htmx:afterSwap', function(evt: Event): void {
        const detail = (evt as CustomEvent).detail;
        if (detail?.target?.id === 'main-content') {
            updateActiveLink();
        }
    });
})();
