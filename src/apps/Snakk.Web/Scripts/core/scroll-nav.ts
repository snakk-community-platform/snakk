(function (): void {
    'use strict';

    const MOBILE_MAX_PX = 1023.98;
    const SNAP_DELAY_MS = 150;
    const SNAP_TRANSITION = 'transform 200ms ease';

    const header = document.querySelector<HTMLElement>('.sn-header');
    const tabbar = document.querySelector<HTMLElement>('.sn-tabbar');

    if (!header || !tabbar) return;

    let threadNav = document.getElementById('thread-nav');

    // headerOffset: 0 (visible) → -headerH (hidden above viewport)
    // tabbarOffset: 0 (visible) → +tabbarH (hidden below viewport)
    let headerOffset = 0;
    let tabbarOffset = 0;
    let lastScrollY = window.scrollY;
    let lastScrollHeight = document.documentElement.scrollHeight;
    let rafPending = false;
    let isMobile = window.innerWidth <= MOBILE_MAX_PX;
    let snapTimer: ReturnType<typeof setTimeout> | null = null;

    let headerH = 0;
    let tabbarH = 0;

    function measureDimensions(): void {
        headerH = header!.offsetHeight || 56;
        tabbarH = tabbar!.offsetHeight || 56;
    }

    measureDimensions();

    function clearSnap(): void {
        if (snapTimer !== null) {
            clearTimeout(snapTimer);
            snapTimer = null;
        }
    }

    function applyOffsets(transition: string): void {
        header!.style.transition = transition;
        tabbar!.style.transition = transition;
        header!.style.transform = headerOffset !== 0 ? `translateY(${headerOffset}px)` : '';
        tabbar!.style.transform = tabbarOffset !== 0 ? `translateY(${tabbarOffset}px)` : '';

        // Keep #thread-nav bottom edge flush with the top of the tabbar.
        if (threadNav) {
            if (isMobile) {
                threadNav.style.transition = transition === 'none' ? 'none' : 'bottom 200ms ease';
                threadNav.style.bottom = Math.max(0, tabbarH - tabbarOffset) + 'px';
            } else {
                threadNav.style.transition = '';
                threadNav.style.bottom = '';
            }
        }
    }

    function resetOffsets(): void {
        clearSnap();
        headerOffset = 0;
        tabbarOffset = 0;
        applyOffsets('none');
    }

    function scheduleSnap(): void {
        clearSnap();
        snapTimer = setTimeout(() => {
            const snappedHeader = headerOffset < -headerH / 2 ? -headerH : 0;
            const snappedTabbar = tabbarOffset > tabbarH / 2 ? tabbarH : 0;

            // Skip if already at an edge — no visual change needed.
            if (snappedHeader === headerOffset && snappedTabbar === tabbarOffset) return;

            headerOffset = snappedHeader;
            tabbarOffset = snappedTabbar;
            applyOffsets(SNAP_TRANSITION);
        }, SNAP_DELAY_MS);
    }

    function onFrame(): void {
        rafPending = false;

        const scrollY = window.scrollY;
        const scrollHeight = document.documentElement.scrollHeight;
        const delta = scrollY - lastScrollY;
        const heightGrew = scrollHeight > lastScrollHeight;
        lastScrollY = scrollY;
        lastScrollHeight = scrollHeight;

        if (delta === 0) return;

        // Ignore apparent scroll-up caused by content loading below (infinite scroll).
        // When the page gets taller and scrollY decreases in the same frame, the user
        // didn't actually scroll up — the layout shifted.
        if (delta < 0 && heightGrew) return;

        // At the very top: always fully visible.
        if (scrollY <= 0) {
            resetOffsets();
            return;
        }

        // Scroll down (delta > 0): both hide. Scroll up (delta < 0): both show.
        headerOffset = Math.min(0, Math.max(-headerH, headerOffset - delta));
        tabbarOffset = Math.min(tabbarH, Math.max(0, tabbarOffset + delta));

        applyOffsets('none');
        scheduleSnap();
    }

    function onScroll(): void {
        if (!isMobile || rafPending) return;
        rafPending = true;
        requestAnimationFrame(onFrame);
    }

    function onResize(): void {
        measureDimensions();
        isMobile = window.innerWidth <= MOBILE_MAX_PX;
        lastScrollY = window.scrollY;
        lastScrollHeight = document.documentElement.scrollHeight;
        if (!isMobile) resetOffsets();
    }

    function onHtmxSwap(): void {
        measureDimensions();
        threadNav = document.getElementById('thread-nav');
        lastScrollY = window.scrollY;
        lastScrollHeight = document.documentElement.scrollHeight;
        resetOffsets();
    }

    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onResize, { passive: true });
    document.addEventListener('htmx:afterSwap', onHtmxSwap);
})();
