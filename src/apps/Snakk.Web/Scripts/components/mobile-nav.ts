(function (): void {
    'use strict';

    const HTML_OPEN_CLASS = 'sn-nav-open';
    const DRAWER_OPEN_CLASS = 'is-open';
    const BACKDROP_VISIBLE_CLASS = 'is-visible';

    const html = document.documentElement;

    function getDrawer(): HTMLElement | null {
        return document.querySelector<HTMLElement>('.sn-left');
    }

    function getBackdrop(): HTMLElement {
        let backdrop = document.getElementById('sn-mobile-backdrop');
        if (!backdrop) {
            backdrop = document.createElement('div');
            backdrop.id = 'sn-mobile-backdrop';
            backdrop.className = 'sn-mobile-backdrop';
            backdrop.addEventListener('click', close);
            document.body.appendChild(backdrop);
        }
        return backdrop;
    }

    function isOpen(): boolean {
        return html.classList.contains(HTML_OPEN_CLASS);
    }

    function setTriggerState(expanded: boolean): void {
        document.querySelectorAll<HTMLElement>('[data-mobile-nav-toggle]')
            .forEach(t => t.setAttribute('aria-expanded', expanded ? 'true' : 'false'));
    }

    function open(): void {
        const drawer = getDrawer();
        if (!drawer) return;
        drawer.classList.add(DRAWER_OPEN_CLASS);
        getBackdrop().classList.add(BACKDROP_VISIBLE_CLASS);
        html.classList.add(HTML_OPEN_CLASS);
        setTriggerState(true);
    }

    function close(): void {
        const drawer = getDrawer();
        if (drawer) {
            drawer.classList.remove(DRAWER_OPEN_CLASS);
        }
        getBackdrop().classList.remove(BACKDROP_VISIBLE_CLASS);
        html.classList.remove(HTML_OPEN_CLASS);
        setTriggerState(false);
    }

    function toggle(): void {
        if (isOpen()) close();
        else open();
    }

    // Click delegation for the hamburger button. Delegation means the
    // button doesn't need to be re-bound after HTMX swaps.
    document.addEventListener('click', function (evt: MouseEvent): void {
        const target = evt.target as HTMLElement | null;
        if (!target) return;
        const trigger = target.closest<HTMLElement>('[data-mobile-nav-toggle]');
        if (trigger) {
            evt.preventDefault();
            toggle();
            return;
        }
        // Any nav link inside the drawer should close the drawer after
        // its click propagates into HTMX navigation.
        if (isOpen()) {
            const link = target.closest<HTMLAnchorElement>('.sn-left a');
            if (link) {
                close();
            }
        }
    });

    document.addEventListener('keydown', function (evt: KeyboardEvent): void {
        if (evt.key === 'Escape' && isOpen()) {
            close();
        }
    });

    // Close on HTMX swap (page navigation) and on breakpoint change
    // above lg (drawer becomes part of the desktop layout again).
    document.addEventListener('htmx:afterSwap', function (evt: Event): void {
        const detail = (evt as CustomEvent).detail;
        if (detail?.target?.id === 'main-content' && isOpen()) {
            close();
        }
    });

    const desktopQuery = window.matchMedia('(min-width: 1024px)');
    function onBreakpointChange(e: MediaQueryListEvent | MediaQueryList): void {
        if (e.matches && isOpen()) close();
    }
    if (desktopQuery.addEventListener) {
        desktopQuery.addEventListener('change', onBreakpointChange);
    } else if ((desktopQuery as any).addListener) {
        (desktopQuery as any).addListener(onBreakpointChange);
    }
})();
