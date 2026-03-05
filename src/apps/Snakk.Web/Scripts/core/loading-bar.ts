/**
 * Navigation Loading Bar
 * Manages the top loading bar for HTMX SPA navigation with adaptive duration.
 * Also updates page title after content swap.
 */

(function(): void {
    'use strict';

    let bar: HTMLElement | null = null;
    let startTime: number | null = null;
    let generation = 0; // Invalidates pending rAF callbacks on cancel/restore
    const durations: number[] = [];

    function getBar(): HTMLElement | null {
        return bar || (bar = document.getElementById('loading-bar'));
    }

    function p95(): number {
        if (durations.length < 3) return 2000;
        const sorted = durations.slice().sort((a, b) => a - b);
        return sorted[Math.floor(sorted.length * 0.95)] ?? sorted[sorted.length - 1] ?? 2000;
    }

    function resetBar(b: HTMLElement): void {
        generation++;
        b.classList.remove('active');
        b.classList.add('complete');
        startTime = null;
    }

    document.addEventListener('htmx:beforeRequest', (evt: Event) => {
        const detail = (evt as CustomEvent).detail;
        if (detail.target && detail.target.id === 'main-content') {
            const b = getBar();
            if (!b) return;
            const gen = ++generation;
            startTime = performance.now();
            b.style.setProperty('--loading-duration', p95() + 'ms');
            b.classList.remove('complete');
            // Double rAF ensures browser paints the reset before starting the animation
            // Generation check prevents stale callbacks after historyRestore/abort
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    if (gen === generation) {
                        b.classList.add('active');
                    }
                });
            });
        }
    });

    document.addEventListener('htmx:afterSettle', (evt: Event) => {
        const detail = (evt as CustomEvent).detail;
        if (detail.target && detail.target.id === 'main-content') {
            const b = getBar();
            if (!b) return;
            if (startTime) {
                durations.push(performance.now() - startTime);
                if (durations.length > 20) durations.shift();
            }
            resetBar(b);
        }
    });

    document.addEventListener('htmx:historyRestore', () => {
        const b = getBar();
        if (!b) return;
        resetBar(b);
    });

    document.addEventListener('htmx:abort', () => {
        const b = getBar();
        if (!b) return;
        generation++;
        b.classList.remove('active', 'complete');
        startTime = null;
    });

    document.addEventListener('htmx:afterSwap', (evt: Event) => {
        const detail = (evt as CustomEvent).detail;
        if (detail.target.id === 'main-content') {
            const titleEl = document.getElementById('page-title');
            if (titleEl && titleEl.dataset.title) {
                document.title = titleEl.dataset.title;
            }
        }
    });
})();
