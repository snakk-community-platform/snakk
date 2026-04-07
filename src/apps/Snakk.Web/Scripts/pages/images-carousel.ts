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
            const counter = el.querySelector('.fp-images-counter');
            if (!track || slides.length < 2) return;

            let current = 0;

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

            // Open lightbox on click (not on buttons)
            el.addEventListener('click', function(e) {
                const target = e.target as HTMLElement;
                if (target.closest('.fp-images-btn')) return;

                e.preventDefault();
                e.stopPropagation();

                const lightbox = (window as any).SnakkLightbox;
                if (!lightbox) return;

                const fullUrls: string[] = [];
                slides.forEach(function(s) { fullUrls.push(s.dataset.full || s.dataset.src || s.src); });
                lightbox.open(fullUrls, current);
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() { initImagesCarousels(); });
    } else {
        initImagesCarousels();
    }

    document.body.addEventListener('htmx:afterSettle', function(e) { initImagesCarousels((e as CustomEvent).detail.elt); });
})();
