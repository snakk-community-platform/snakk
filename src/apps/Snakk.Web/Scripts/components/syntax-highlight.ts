/**
 * Snakk Syntax Highlighting Component
 * Lazy-loads Prism.js on demand when posts contain code blocks.
 */

(function(): void {
    'use strict';

    if ((window as any).SnakkSyntax) return;

    const CSS_ID = 'prism-css';
    let loadPromise: Promise<any> | null = null;

    function loadCSS(id: string, href: string): void {
        if (document.getElementById(id)) return;
        const link = document.createElement('link');
        link.id = id;
        link.rel = 'stylesheet';
        link.href = href;
        document.head.appendChild(link);
    }

    function loadScript(src: string): Promise<void> {
        return new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${src}"]`);
            if (existing) { resolve(); return; }

            const script = document.createElement('script');
            script.src = src;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error(`Failed to load ${src}`));
            document.head.appendChild(script);
        });
    }

    async function loadPrism(): Promise<any> {
        if (!loadPromise) {
            loadPromise = (async () => {
                try {
                    loadCSS(CSS_ID, '/css/vendor/prism.css');
                    await loadScript('/js/vendor/prism.js');
                    const Prism = (window as any).Prism;
                    if (!Prism) {
                        console.error('Prism not found on window.Prism');
                        return null;
                    }
                    // Disable Prism's automatic highlighting on page load
                    Prism.manual = true;
                    return Prism;
                } catch (e) {
                    console.error('Failed to load Prism:', e);
                    loadPromise = null;
                    return null;
                }
            })();
        }
        return loadPromise;
    }

    /**
     * Highlight all code blocks within the given container (or document if omitted).
     * Only processes elements that haven't been highlighted yet.
     */
    async function highlightAll(container?: HTMLElement): Promise<void> {
        const Prism = await loadPrism();
        if (!Prism) return;

        const root = container || document;
        const codeBlocks = root.querySelectorAll('pre > code:not(.prism-highlighted)');

        codeBlocks.forEach((block: Element) => {
            Prism.highlightElement(block);
            block.classList.add('prism-highlighted');
        });
    }

    (window as any).SnakkSyntax = { highlightAll, loadPrism };
})();
