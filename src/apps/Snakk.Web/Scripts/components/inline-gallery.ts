/**
 * Inline Gallery — wires lightbox clicks for image-group galleries embedded in post prose,
 * and upgrades standalone prose images to use srcset + LQIP blur + lightbox.
 */

(function(): void {
    'use strict';

    if ((window as any).SnakkInlineGallery) return;

    const GALLERY_SELECTORS = '.gup-grid, .gup-masonry, .gup-justified, .gup-single, .ig-carousel';

    // Layouts where the blur-behind effect is active
    const BLUR_LAYOUTS = new Set(['gup-grid', 'gup-justified', 'ig-carousel']);

    // Matches full-resolution Snakk CDN post image URLs (no _thumb/_med suffix)
    const SNAKK_MEDIA_RE = /\/media\/posts\/\d{4}\/\d{2}\/\d{2}\/[^/._]+\.webp$/;

    const SIZES = '(min-width: 1540px) 864px, (min-width: 1024px) calc(100vw - 16rem), 100vw';

    function isSnakkMediaUrl(url: string): boolean {
        return SNAKK_MEDIA_RE.test(url);
    }

    function deriveVariants(fullUrl: string): { thumb: string; med: string } {
        const stem = fullUrl.slice(0, -5); // strip .webp
        return { thumb: stem + '_thumb.webp', med: stem + '_med.webp' };
    }

    // Upgrade an <img> that was rendered before the server-side change (no srcset yet).
    function upgradeImg(img: HTMLImageElement, fullUrl: string): void {
        if (img.srcset) return;
        const { thumb, med } = deriveVariants(fullUrl);
        img.srcset = `${thumb} 300w, ${med} 900w, ${fullUrl} 2048w`;
        img.sizes = SIZES;
        img.src = med;
        img.addEventListener('error', () => {
            img.removeAttribute('srcset');
            img.removeAttribute('sizes');
            img.src = fullUrl;
        }, { once: true });
    }

    function applyBlurBackgrounds(container: HTMLElement): void {
        const hasBlur = [...container.classList].some(c => BLUR_LAYOUTS.has(c));
        if (!hasBlur) return;
        container.querySelectorAll<HTMLElement>('.images-upload-item[data-full]').forEach(item => {
            const full = item.dataset.full;
            if (!full || item.style.backgroundImage) return;
            // Prefer the actual blur data URI from the DB; fall back to _thumb URL
            const bgSrc = item.dataset.blur ?? (isSnakkMediaUrl(full) ? deriveVariants(full).thumb : full);
            item.style.backgroundImage = `url(${bgSrc})`;
            item.style.backgroundSize = 'cover';
            item.style.backgroundPosition = 'center';
            const img = item.querySelector('img');
            if (img) {
                if (img.complete) {
                    item.classList.add('images-item-loaded');
                } else {
                    img.addEventListener('load', () => item.classList.add('images-item-loaded'), { once: true });
                }
            }
        });
    }

    function wireGallery(container: Element): void {
        const items = Array.from(container.querySelectorAll<HTMLElement>('.images-upload-item[data-full]'));
        if (!items.length) return;

        items.forEach(item => {
            const img = item.querySelector<HTMLImageElement>('img');
            const fullUrl = item.dataset.full ?? '';
            if (img && isSnakkMediaUrl(fullUrl)) upgradeImg(img, fullUrl);
        });

        applyBlurBackgrounds(container as HTMLElement);

        const urls = items.map(el => el.dataset.full ?? '').filter(Boolean);
        // Use data-blur (actual DB blur URI) when available, else derive _thumb as LQIP
        const lqipUrls = urls.map((u, i) =>
            items[i]?.dataset.blur ?? (isSnakkMediaUrl(u) ? deriveVariants(u).thumb : null));

        items.forEach((item, idx) => {
            item.addEventListener('click', () => {
                const lb = (window as any).SnakkLightbox;
                if (lb?.open) lb.open(urls, idx, lqipUrls);
            });
            item.style.cursor = 'pointer';
        });
    }

    function wireCarousel(container: HTMLElement): void {
        const track = container.querySelector<HTMLElement>('.gup-carousel-track');
        const slides = Array.from(container.querySelectorAll<HTMLElement>('.gup-carousel-track .images-upload-item'));
        const prevBtn = container.querySelector<HTMLElement>('.gup-carousel-prev');
        const nextBtn = container.querySelector<HTMLElement>('.gup-carousel-next');
        const counter = container.querySelector<HTMLElement>('.gup-carousel-counter');

        if (!track || !slides.length) return;

        slides.forEach(slide => {
            const img = slide.querySelector<HTMLImageElement>('img');
            const fullUrl = slide.dataset.full ?? '';
            if (img && isSnakkMediaUrl(fullUrl)) upgradeImg(img, fullUrl);
        });

        const urls = slides.map(s => s.dataset.full ?? '').filter(Boolean);
        const lqipUrls = urls.map((u, i) =>
            slides[i]?.dataset.blur ?? (isSnakkMediaUrl(u) ? deriveVariants(u).thumb : null));
        let current = 0;

        function goTo(idx: number): void {
            current = Math.max(0, Math.min(slides.length - 1, idx));
            track!.style.transform = `translateX(-${current * 100}%)`;
            if (counter) counter.textContent = `${current + 1} / ${slides.length}`;
            if (prevBtn) prevBtn.style.visibility = current === 0 ? 'hidden' : '';
            if (nextBtn) nextBtn.style.visibility = current === slides.length - 1 ? 'hidden' : '';
        }

        applyBlurBackgrounds(container);

        if (prevBtn) prevBtn.addEventListener('click', e => { e.stopPropagation(); goTo(current - 1); });
        if (nextBtn) nextBtn.addEventListener('click', e => { e.stopPropagation(); goTo(current + 1); });
        goTo(0);

        slides.forEach((slide, idx) => {
            slide.style.cursor = 'zoom-in';
            slide.addEventListener('click', () => {
                const lb = (window as any).SnakkLightbox;
                if (lb?.open) lb.open(urls, idx, lqipUrls);
            });
        });
    }

    function processProseImages(prose: HTMLElement): void {
        prose.querySelectorAll<HTMLImageElement>('p > img').forEach(img => {
            // For server-rendered new posts img.src is _med.webp; data-full holds the full URL.
            // For old posts img.src is the full URL and data-full is not yet set.
            const fullUrl = img.dataset.full ?? img.src;
            if (!isSnakkMediaUrl(fullUrl)) return;

            const { thumb, med } = deriveVariants(fullUrl);
            // Enrich srcset if not already set by server (handles any post rendered without imageData)
            if (!img.srcset) {
                img.dataset.full = fullUrl;
                img.srcset = `${thumb} 300w, ${med} 900w, ${fullUrl} 2048w`;
                img.sizes = SIZES;
                img.src = med;
                img.loading = 'lazy';
                img.addEventListener('error', () => {
                    img.removeAttribute('srcset');
                    img.removeAttribute('sizes');
                    img.src = fullUrl;
                }, { once: true });
            }

            // Wrap in a blur-placeholder container
            const wrapper = document.createElement('span');
            wrapper.className = 'prose-img-wrap';
            const bgSrc = img.dataset.blur ?? thumb;
            wrapper.style.backgroundImage = `url(${bgSrc})`;
            const w = img.getAttribute('width');
            const h = img.getAttribute('height');
            if (w && h) wrapper.style.aspectRatio = `${w} / ${h}`;
            img.parentNode!.insertBefore(wrapper, img);
            wrapper.appendChild(img);

            if (img.complete && img.naturalWidth > 0) {
                wrapper.classList.add('images-item-loaded');
            } else {
                img.addEventListener('load', () => wrapper.classList.add('images-item-loaded'), { once: true });
            }

            const lightboxUrl = img.dataset.full ?? fullUrl;
            const lqip = img.dataset.blur ?? thumb;
            wrapper.addEventListener('click', () => {
                const lb = (window as any).SnakkLightbox;
                if (lb?.open) lb.open([lightboxUrl], 0, [lqip]);
            });
            wrapper.style.cursor = 'pointer';
        });
    }

    function wireInlineSingle(el: HTMLElement): void {
        const fullUrl = el.dataset.full ?? '';
        if (!fullUrl) return;
        const img = el.querySelector<HTMLImageElement>('img');
        if (img && isSnakkMediaUrl(fullUrl)) upgradeImg(img, fullUrl);
        const lqip = el.dataset.blur ?? (isSnakkMediaUrl(fullUrl) ? deriveVariants(fullUrl).thumb : null);
        el.style.cursor = 'pointer';
        el.addEventListener('click', () => {
            const lb = (window as any).SnakkLightbox;
            if (lb?.open) lb.open([fullUrl], 0, [lqip]);
        });
    }

    function processGalleries(root: Document | HTMLElement): void {
        const proseEls = root.querySelectorAll<HTMLElement>('.prose-content:not([data-ig-init])');
        proseEls.forEach(prose => {
            prose.dataset.igInit = '1';
            processProseImages(prose);
            prose.querySelectorAll<HTMLElement>('.ig-single[data-full]').forEach(wireInlineSingle);
            const galleries = prose.querySelectorAll<HTMLElement>(GALLERY_SELECTORS);
            galleries.forEach(g => {
                if (g.classList.contains('ig-carousel')) {
                    wireCarousel(g as HTMLElement);
                } else {
                    wireGallery(g);
                }
            });
        });
    }

    function init(): void {
        processGalleries(document);
    }

    (window as any).SnakkInlineGallery = { init };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
    document.body.addEventListener('htmx:load', init);
    document.body.addEventListener('htmx:historyRestore', init);
})();
