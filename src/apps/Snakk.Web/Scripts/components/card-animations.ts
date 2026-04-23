(function () {
    'use strict';

    function initCardAnimations(): void {
        const observer = new IntersectionObserver((entries, obs) => {
            for (const entry of entries) {
                if (!entry.isIntersecting) continue;
                const el = entry.target as HTMLElement;
                if (el.hasAttribute('data-animated')) continue;
                el.setAttribute('data-animated', '');
                (el.querySelectorAll<HTMLElement>('.sn-poll-fill[data-poll-width]')).forEach((fill, i) => {
                    setTimeout(() => { fill.style.width = fill.dataset.pollWidth!; }, i * 50);
                });
                (el.querySelectorAll<HTMLElement>('.sn-debate-segment[data-debate-width]')).forEach(seg => {
                    seg.style.width = seg.dataset.debateWidth!;
                });
                obs.unobserve(el);
            }
        }, { threshold: 0.3 });
        document.querySelectorAll<HTMLElement>('.sn-poll-preview:not([data-animated]),.sn-debate-preview:not([data-animated])').forEach(el => observer.observe(el));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCardAnimations);
    } else {
        initCardAnimations();
    }
    document.addEventListener('htmx:afterSwap', initCardAnimations);
})();
