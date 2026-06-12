(function() {
    'use strict';

    // Track initialized elements in module-scope WeakSets instead of DOM data
    // attributes. HTMX serializes the page DOM into its history cache on
    // navigate-away, which would bake `dataset.init="1"` into the cached HTML
    // and block re-init on restore. Module-scope WeakSets are cleared on
    // full page loads but not affected by history cache serialization, so
    // restored elements are correctly seen as uninitialized and re-run their
    // wiring (including critical state like spoiler reveal or compare clip
    // paths) against the freshly restored HTML.
    const initializedCarousels = new WeakSet<Element>();
    const initializedCompare = new WeakSet<Element>();
    const initializedSpoilers = new WeakSet<Element>();
    const initializedPreviewNav = new WeakSet<Element>();

    // Timestamp of the last time a compare slider drag ended. Used by
    // initPreviewNavigation to suppress the click-to-navigate that would
    // otherwise fire when the user releases the mouse after a slider drag.
    // A small debounce window (300ms) is enough — the synthesized click
    // that follows pointerup fires in the same tick.
    let compareDragEndTimestamp = 0;

    // Module-level active drag state for the compare widget.
    // A single pair of document-level pointermove/pointerup listeners delegate
    // to this so we never accumulate listeners across multiple initAll() calls.
    // rect is captured once on pointerdown to avoid getBoundingClientRect per move.
    let activeDrag: { setPos: (x: number, rect: DOMRect) => void; rect: DOMRect } | null = null;
    let docCompareListenersAttached = false;

    // Canonical implementation lives in core/utils.ts as
    // window.SnakkUtils.extractBlurUrl. Keep a local fallback in case
    // utils.ts hasn't loaded yet (load-order edge case when this script
    // runs before core scripts — happens on some HTMX swap paths).
    function extractBlurUrl(el: HTMLElement | null): string | null {
        const shared = (window as any).SnakkUtils?.extractBlurUrl;
        if (typeof shared === 'function') return shared(el);

        // Fallback — identical algorithm to SnakkUtils.extractBlurUrl.
        let cur: HTMLElement | null = el;
        for (let i = 0; i < 5 && cur; i++) {
            const bg = cur.style.backgroundImage;
            if (bg && bg !== 'none') {
                const m = bg.match(/^url\((?:["'])?(.+?)(?:["'])?\)$/);
                if (m) return m[1] || null;
            }
            const cv = cur.style.getPropertyValue('--blur-bg').trim();
            if (cv && cv !== 'none') {
                const m = cv.match(/^url\((?:["'])?(.+?)(?:["'])?\)$/);
                if (m) return m[1] || null;
            }
            cur = cur.parentElement;
        }
        return null;
    }

    function initImagesCarousels(root?: Element): void {
        const carousels = (root || document).querySelectorAll('.sn-images-preview');
        carousels.forEach(function(preview) {
            const el = preview as HTMLElement;
            if (initializedCarousels.has(el)) return;
            initializedCarousels.add(el);

            const track = el.querySelector('.sn-images-track') as HTMLElement | null;
            const slides = el.querySelectorAll<HTMLImageElement>('.sn-images-slide');
            if (!track || slides.length === 0) return;

            let current = 0;

            // Carousel nav (multi-image only)
            if (slides.length > 1) {
                const counter = el.querySelector('.sn-images-counter');

                function loadSrc(img: HTMLImageElement): void {
                    if (img.dataset.src && !img.dataset.loaded) {
                        img.src = img.dataset.src;
                        img.dataset.loaded = '1';
                    }
                }

                function showSlide(index: number): void {
                    current = Math.max(0, Math.min(slides.length - 1, index));
                    track!.style.transform = `translateX(-${current * 100}%)`;
                    loadSrc(slides[current]!);
                    if (counter) counter.textContent = (current + 1) + ' / ' + slides.length;
                    if (prev) (prev as HTMLElement).style.visibility = current === 0 ? 'hidden' : '';
                    if (next) (next as HTMLElement).style.visibility = current === slides.length - 1 ? 'hidden' : '';
                }

                function preloadSlide(index: number): void {
                    const idx = (index + slides.length) % slides.length;
                    const s = slides[idx];
                    if (s) loadSrc(s);
                }

                const prev = el.querySelector('.sn-images-prev');
                const next = el.querySelector('.sn-images-next');

                if (prev) prev.addEventListener('click', function(e) { e.preventDefault(); e.stopPropagation(); showSlide(current - 1); });
                if (next) {
                    next.addEventListener('click', function(e) { e.preventDefault(); e.stopPropagation(); showSlide(current + 1); });
                    next.addEventListener('mouseenter', function() { preloadSlide(current + 1); });
                }
                if (prev) prev.addEventListener('mouseenter', function() { preloadSlide(current - 1); });

                const utils = (window as any).SnakkUtils;
                if (utils?.addCarouselSwipe) utils.addCarouselSwipe(track, showSlide, () => current);
                showSlide(0);
            }

            // Expand button opens lightbox at current slide
            const expandBtn = el.querySelector('.sn-images-expand-btn');
            if (expandBtn) {
                expandBtn.addEventListener('click', function(e) {
                    e.preventDefault();
                    e.stopPropagation();

                    const lightbox = (window as any).SnakkLightbox;
                    if (!lightbox) return;

                    const fullUrls: string[] = [];
                    const blurUrls: (string | null)[] = [];
                    slides.forEach(function(s) {
                        fullUrls.push(s.dataset.full || s.dataset.src || s.src);
                        blurUrls.push(extractBlurUrl(s.parentElement));
                    });
                    lightbox.open(fullUrls, current, blurUrls);
                });
            }
        });
    }

    // ── Compare widget previews ──────────────────
    function initComparePreview(root?: Element): void {
        const widgets = (root || document).querySelectorAll('.sn-compare-widget');
        widgets.forEach(function(widget) {
            const el = widget as HTMLElement;
            if (initializedCompare.has(el)) return;
            initializedCompare.add(el);

            const beforeEl = el.querySelector('.gup-compare-before') as HTMLElement | null;
            const afterEl = el.querySelector('.gup-compare-after') as HTMLElement | null;
            const slider = el.querySelector('.sn-compare-slider') as HTMLElement | null;
            if (!beforeEl || !afterEl || !slider) return;

            // Wipe any stale inline slider state that may have been baked into
            // the HTMX history cache from a previous drag interaction. Without
            // this, computeInitialPosition below would read against whatever
            // clip-path was inlined at navigate-away time.
            beforeEl.style.clipPath = '';
            afterEl.style.clipPath = '';
            slider.style.right = '';

            function setPos(x: number, rect: DOMRect): void {
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
                const elRect = el.getBoundingClientRect();
                if (afterLabel) {
                    const labelRect = afterLabel.getBoundingClientRect();
                    setPos(labelRect.left - 12, elRect);
                } else {
                    setPos(elRect.left + elRect.width * 0.5, elRect);
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

            // Attach document-level pointermove/pointerup exactly once for all
            // compare widgets. They delegate to the module-level activeDrag so
            // no listeners accumulate across htmx:afterSettle re-runs.
            if (!docCompareListenersAttached) {
                docCompareListenersAttached = true;
                document.addEventListener('pointermove', (e) => {
                    if (activeDrag) activeDrag.setPos(e.clientX, activeDrag.rect);
                });
                document.addEventListener('pointerup', () => {
                    if (activeDrag) {
                        // Mark when the drag ended so initPreviewNavigation can
                        // suppress the synthesized click that follows. Without
                        // this, releasing the mouse after dragging the slider
                        // would fire a click on .sn-card-preview and navigate
                        // to the discussion — clearly wrong.
                        compareDragEndTimestamp = Date.now();
                        activeDrag = null;
                    }
                });
            }

            slider.addEventListener('pointerdown', (e) => { activeDrag = { setPos, rect: el.getBoundingClientRect() }; slider!.setPointerCapture(e.pointerId); e.preventDefault(); });
            el.addEventListener('pointerdown', (e) => {
                if ((e.target as HTMLElement).closest('.gup-compare-handle, .sn-images-expand-btn')) return;
                const rect = el.getBoundingClientRect();
                activeDrag = { setPos, rect };
                setPos(e.clientX, rect);
                e.preventDefault();
            });

            // Expand button opens lightbox with both images
            const expandBtn = el.querySelector('.sn-images-expand-btn');
            if (expandBtn) {
                expandBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const lightbox = (window as any).SnakkLightbox;
                    if (!lightbox) return;
                    const imgs = el.querySelectorAll<HTMLImageElement>('img');
                    const urls = Array.from(imgs).map(i => i.dataset.full || i.src);
                    const blurs = Array.from(imgs).map(i => extractBlurUrl(i.parentElement));
                    lightbox.open(urls, 0, blurs);
                });
            }
        });
    }

    // ── Spoiler reveal for previews ────────────
    function initSpoilerReveal(root?: Element): void {
        const spoilers = (root || document).querySelectorAll('.sn-images-spoiler');
        spoilers.forEach(function(container) {
            const el = container as HTMLElement;
            if (initializedSpoilers.has(el)) return;
            initializedSpoilers.add(el);

            const overlay = el.querySelector('.sn-images-spoiler-overlay') as HTMLElement | null;
            if (!overlay) return;

            const discussionId = el.dataset.discussionId || '';
            const storageKey = discussionId ? `snakk:spoiler-revealed:${discussionId}` : '';

            function reveal(): void {
                const isCompare = !!el.querySelector('.sn-compare-widget');
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

            // Auto-reveal if the user previously revealed this spoiler this
            // session. We check sessionStorage directly rather than relying on
            // spoiler-restore.ts having marked .revealed, because external
            // script execution on HTMX swaps is unreliable — on a fresh link
            // navigation to the frontpage, the server returns the card
            // unrevealed and spoiler-restore.ts may not have run before this
            // init pass. Checking storage here makes the behavior independent
            // of that timing.
            const alreadyRevealed = el.classList.contains('revealed')
                || (storageKey !== '' && sessionStorage.getItem(storageKey) === '1');
            if (alreadyRevealed) {
                el.classList.add('revealed');
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
        const previews = (root || document).querySelectorAll<HTMLElement>('.sn-card-preview[data-discussion-url]');
        previews.forEach(function(el) {
            if (initializedPreviewNav.has(el)) return;
            initializedPreviewNav.add(el);

            el.addEventListener('click', function(e) {
                const target = e.target as HTMLElement;
                // Don't navigate if clicking interactive elements
                if (target.closest('a, button, .gup-compare-slider, .gup-compare-handle')) return;
                // Don't navigate if the click was the release of a compare
                // slider drag — the synthesized click fires immediately
                // after pointerup, so anything within the last ~300ms is
                // part of the same drag gesture.
                if (Date.now() - compareDragEndTimestamp < 300) return;

                const url = el.dataset.discussionUrl;
                if (url) window.location.href = url;
            });
        });
    }

    const initializedLinkAdult = new WeakSet<Element>();

    // ── Adult-content blur reveal for link-card thumbnails ────────────
    function initLinkCardAdultReveal(root?: Element): void {
        const thumbs = (root || document).querySelectorAll('.sn-link-card-thumb-adult-blur');
        thumbs.forEach(function(container) {
            const el = container as HTMLElement;
            if (initializedLinkAdult.has(el)) return;
            initializedLinkAdult.add(el);

            const overlay = el.querySelector('.sn-link-card-adult-overlay') as HTMLElement | null;
            const img = el.querySelector('.sn-link-card-img-adult-blur') as HTMLImageElement | null;
            if (!overlay || !img) return;

            const discussionId = el.dataset.discussionId || '';
            const storageKey = discussionId ? `snakk:adult-revealed:${discussionId}` : '';

            function reveal(): void {
                el.classList.add('revealed');
                img!.classList.add('revealed');
                if (storageKey) sessionStorage.setItem(storageKey, '1');
            }

            if (storageKey && sessionStorage.getItem(storageKey) === '1') {
                reveal();
                return;
            }

            overlay.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                reveal();
            });
        });
    }

    function initAll(root?: Element): void {
        initImagesCarousels(root);
        initComparePreview(root);
        initSpoilerReveal(root);
        initLinkCardAdultReveal(root);
        initPreviewNavigation(root);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() { initAll(); });
    } else {
        initAll();
    }

    document.body.addEventListener('htmx:afterSettle', function(e) { initAll((e as CustomEvent).detail.elt); });
    // historyRestore fires when HTMX restores a page from its history cache
    // (clicking a nav link that points back to a previously-visited page).
    // Without this, cached compare widgets would keep stale inline styles
    // (clip-paths from a prior drag) and spoiler previews would keep their
    // blur src instead of the real image.
    document.body.addEventListener('htmx:historyRestore', function() { initAll(); });
})();
