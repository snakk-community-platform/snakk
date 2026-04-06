(function () {
    'use strict';

    const COOKIE_NAME = 'snakk_skip_external_warn';
    const MODAL_ID = 'external-link-modal';

    function isSkipEnabled(): boolean {
        return document.cookie.split(';').some(c => c.trim().startsWith(`${COOKIE_NAME}=1`));
    }

    function setSkipCookie(enabled: boolean): void {
        const maxAge = enabled ? 60 * 60 * 24 * 365 : 0; // 1 year or expire
        document.cookie = `${COOKIE_NAME}=${enabled ? '1' : '0'};path=/;max-age=${maxAge};SameSite=Lax`;
    }

    function isExternalLink(a: HTMLAnchorElement): boolean {
        if (!a.href || a.href.startsWith('javascript:')) return false;
        if (a.hostname === location.hostname) return false;
        if (a.target !== '_blank') return false;
        // Skip embed buttons and links inside the modal itself
        if (a.closest('.link-embed-btn, .link-embed-container, #external-link-modal')) return false;
        return true;
    }

    function getModal(): HTMLDialogElement | null {
        return document.getElementById(MODAL_ID) as HTMLDialogElement | null;
    }

    function showModal(url: string, domain: string): void {
        const modal = getModal();
        if (!modal) return;

        const domainEl = modal.querySelector('[data-domain]');
        const urlEl = modal.querySelector('[data-url]') as HTMLAnchorElement | null;
        const checkbox = modal.querySelector('input[type="checkbox"]') as HTMLInputElement | null;

        if (domainEl) domainEl.textContent = domain;
        if (urlEl) {
            urlEl.href = url;
            urlEl.textContent = `Continue to ${domain}`;
        }
        if (checkbox) checkbox.checked = false;

        modal.showModal();
    }

    function init(): void {
        // Prevent HTMX preload from prefetching external links (CSP blocks connect-src to other origins)
        document.querySelectorAll<HTMLAnchorElement>('a[target="_blank"]').forEach(a => {
            if (a.hostname !== location.hostname) a.setAttribute('preload', 'false');
        });

        // Also handle dynamically added links (HTMX endless scroll, etc.)
        new MutationObserver(mutations => {
            for (const m of mutations) {
                for (const node of m.addedNodes) {
                    if (node instanceof HTMLElement) {
                        node.querySelectorAll<HTMLAnchorElement>('a[target="_blank"]').forEach(a => {
                            if (a.hostname !== location.hostname) a.setAttribute('preload', 'false');
                        });
                    }
                }
            }
        }).observe(document.body, { childList: true, subtree: true });

        // Event delegation — intercept all external link clicks
        document.addEventListener('click', (e: MouseEvent) => {
            const a = (e.target as Element)?.closest('a') as HTMLAnchorElement | null;
            if (!a || !isExternalLink(a)) return;
            if (isSkipEnabled()) return; // user opted out of warnings

            e.preventDefault();
            e.stopPropagation();

            const domain = a.hostname.replace(/^www\./, '');
            showModal(a.href, domain);
        }, true);

        // Modal button handlers
        const modal = getModal();
        if (!modal) return;

        const continueBtn = modal.querySelector('[data-url]') as HTMLAnchorElement | null;
        const cancelBtn = modal.querySelector('[data-cancel]') as HTMLButtonElement | null;
        const checkbox = modal.querySelector('input[type="checkbox"]') as HTMLInputElement | null;

        if (continueBtn) {
            continueBtn.addEventListener('click', (e) => {
                e.preventDefault();
                const url = continueBtn.href;
                if (checkbox?.checked) setSkipCookie(true);
                modal.close();
                window.open(url, '_blank', 'noopener,noreferrer');
            });
        }

        if (cancelBtn) {
            cancelBtn.addEventListener('click', () => {
                modal.close();
            });
        }

        // Close on backdrop click
        modal.addEventListener('click', (e) => {
            if (e.target === modal) modal.close();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
