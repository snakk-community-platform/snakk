(function() {
    'use strict';

    function initImagesCarousels(root?: Element): void {
        const carousels = (root || document).querySelectorAll('.fp-images-preview');
        carousels.forEach(function(preview) {
            const el = preview as HTMLElement;
            if (el.dataset.init) return;
            el.dataset.init = '1';

            const track = el.querySelector('.fp-images-track') as HTMLElement | null;
            const slides = el.querySelectorAll<HTMLImageElement>('.fp-images-slide');
            if (!track || slides.length === 0) return;

            let current = 0;

            // Carousel nav (multi-image only)
            if (slides.length > 1) {
                const counter = el.querySelector('.fp-images-counter');

                function loadSrc(img: HTMLImageElement): void {
                    if (img.dataset.src && !img.dataset.loaded) {
                        img.src = img.dataset.src;
                        img.dataset.loaded = '1';
                    }
                }

                function showSlide(index: number): void {
                    current = (index + slides.length) % slides.length;
                    track!.style.transform = `translateX(-${current * 100}%)`;
                    loadSrc(slides[current]!);
                    if (counter) counter.textContent = (current + 1) + ' / ' + slides.length;
                }

                function preloadSlide(index: number): void {
                    const idx = (index + slides.length) % slides.length;
                    const s = slides[idx];
                    if (s) loadSrc(s);
                }

                const prev = el.querySelector('.fp-images-prev');
                const next = el.querySelector('.fp-images-next');

                if (prev) prev.addEventListener('click', function(e) { e.preventDefault(); e.stopPropagation(); showSlide(current - 1); });
                if (next) {
                    next.addEventListener('click', function(e) { e.preventDefault(); e.stopPropagation(); showSlide(current + 1); });
                    next.addEventListener('mouseenter', function() { preloadSlide(current + 1); });
                }
                if (prev) prev.addEventListener('mouseenter', function() { preloadSlide(current - 1); });
            }

            // Expand button opens lightbox at current slide
            const expandBtn = el.querySelector('.fp-images-expand-btn');
            if (expandBtn) {
                expandBtn.addEventListener('click', function(e) {
                    e.preventDefault();
                    e.stopPropagation();

                    const lightbox = (window as any).SnakkLightbox;
                    if (!lightbox) return;

                    const fullUrls: string[] = [];
                    slides.forEach(function(s) { fullUrls.push(s.dataset.full || s.dataset.src || s.src); });
                    lightbox.open(fullUrls, current);
                });
            }
        });
    }

    // ── Compare widget previews ──────────────────
    function initComparePreview(root?: Element): void {
        const widgets = (root || document).querySelectorAll('.fp-compare-widget');
        widgets.forEach(function(widget) {
            const el = widget as HTMLElement;
            if (el.dataset.init) return;
            el.dataset.init = '1';

            const beforeEl = el.querySelector('.gup-compare-before') as HTMLElement | null;
            const afterEl = el.querySelector('.gup-compare-after') as HTMLElement | null;
            const slider = el.querySelector('.fp-compare-slider') as HTMLElement | null;
            if (!beforeEl || !afterEl || !slider) return;

            function setPos(x: number): void {
                const rect = el.getBoundingClientRect();
                const pct = Math.max(0, Math.min(1, (x - rect.left) / rect.width));
                const rightPct = (1 - pct) * 100;
                const leftPct = pct * 100;
                beforeEl!.style.clipPath = `inset(0 ${rightPct}% 0 0)`;
                afterEl!.style.clipPath = `inset(0 0 0 ${leftPct}%)`;
                slider!.style.right = `${rightPct}%`;
            }

            // Set initial position from "After" label.
            // Discussion lists often init while the card is still laying out, so we need
            // to wait until the widget has a real width before measuring — otherwise
            // the label's client rect falls outside the widget and pct clamps to 0
            // (slider ends up all the way left instead of beside the "After" label).
            const afterLabel = afterEl.querySelector('.gup-compare-label-after') as HTMLElement | null;

            function computeInitialPosition(): void {
                if (afterLabel) {
                    const labelRect = afterLabel.getBoundingClientRect();
                    setPos(labelRect.left - 12);
                } else {
                    const rect = el.getBoundingClientRect();
                    setPos(rect.left + rect.width * 0.5);
                }
            }

            if (el.clientWidth > 0) {
                requestAnimationFrame(computeInitialPosition);
            } else {
                const ro = new ResizeObserver(() => {
                    if (el.clientWidth > 0) {
                        computeInitialPosition();
                        ro.disconnect();
                    }
                });
                ro.observe(el);
            }

            let dragging = false;
            slider.addEventListener('pointerdown', (e) => { dragging = true; slider!.setPointerCapture(e.pointerId); e.preventDefault(); });
            el.addEventListener('pointerdown', (e) => {
                if ((e.target as HTMLElement).closest('.gup-compare-handle, .fp-images-expand-btn')) return;
                dragging = true; setPos(e.clientX); e.preventDefault();
            });

            // Expand button opens lightbox with both images
            const expandBtn = el.querySelector('.fp-images-expand-btn');
            if (expandBtn) {
                expandBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const lightbox = (window as any).SnakkLightbox;
                    if (!lightbox) return;
                    const imgs = el.querySelectorAll<HTMLImageElement>('img');
                    const urls = Array.from(imgs).map(i => i.dataset.full || i.src);
                    lightbox.open(urls, 0);
                });
            }
            document.addEventListener('pointermove', (e) => { if (dragging) setPos(e.clientX); });
            document.addEventListener('pointerup', () => { dragging = false; });
        });
    }

    // ── Spoiler reveal for previews ────────────
    function initSpoilerReveal(root?: Element): void {
        const spoilers = (root || document).querySelectorAll('.fp-images-spoiler');
        spoilers.forEach(function(container) {
            const el = container as HTMLElement;
            if (el.dataset.spoilerInit) return;
            el.dataset.spoilerInit = '1';

            const overlay = el.querySelector('.fp-images-spoiler-overlay') as HTMLElement | null;
            if (!overlay) return;

            const discussionId = el.dataset.discussionId || '';
            const storageKey = discussionId ? `snakk:spoiler-revealed:${discussionId}` : '';

            function reveal(): void {
                const isCompare = !!el.querySelector('.fp-compare-widget');
                const deferred = el.querySelectorAll<HTMLImageElement>('img[data-deferred-src]');
                deferred.forEach((img, i) => {
                    const realSrc = img.dataset.deferredSrc!;
                    img.dataset.src = realSrc;
                    img.removeAttribute('data-deferred-src');

                    // Compare: load both images immediately. Carousel: only first, rest lazy-loaded.
                    if (i === 0 || isCompare) {
                        img.src = realSrc;
                        img.dataset.loaded = '1';
                    }
                });
                el.classList.add('revealed');
                if (storageKey) sessionStorage.setItem(storageKey, '1');
            }

            // If spoiler-restore.ts already marked us .revealed synchronously
            // (pre-paint), load the deferred images and skip wiring the reveal
            // overlay. No class toggle here — the class is already set, so
            // there's no flash.
            if (el.classList.contains('revealed')) {
                reveal();
                return;
            }

            // Preload first image on hover
            overlay.addEventListener('mouseenter', () => {
                const first = el.querySelector<HTMLImageElement>('img[data-deferred-src]');
                if (first?.dataset.deferredSrc) {
                    const p = new window.Image();
                    p.src = first.dataset.deferredSrc;
                }
            }, { once: true });

            overlay.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                reveal();
            });
        });
    }

    // ── Preview click-to-navigate ────────────────
    function initPreviewNavigation(root?: Element): void {
        const previews = (root || document).querySelectorAll<HTMLElement>('.fp-card-preview[data-discussion-url]');
        previews.forEach(function(el) {
            if (el.dataset.navInit) return;
            el.dataset.navInit = '1';

            el.addEventListener('click', function(e) {
                const target = e.target as HTMLElement;
                // Don't navigate if clicking interactive elements
                if (target.closest('a, button, .gup-compare-slider, .gup-compare-handle')) return;

                const url = el.dataset.discussionUrl;
                if (url) window.location.href = url;
            });
        });
    }

    function initAll(root?: Element): void {
        initImagesCarousels(root);
        initComparePreview(root);
        initSpoilerReveal(root);
        initPreviewNavigation(root);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() { initAll(); });
    } else {
        initAll();
    }

    document.body.addEventListener('htmx:afterSettle', function(e) { initAll((e as CustomEvent).detail.elt); });
})();
