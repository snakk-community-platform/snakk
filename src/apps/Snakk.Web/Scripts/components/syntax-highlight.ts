/**
 * Snakk Syntax Highlighting Component
 * Lazy-loads Prism.js on demand when posts contain code blocks.
 */

(function(): void {
    'use strict';

    if ((window as any).SnakkSyntax) return;

    const CSS_ID = 'prism-css';
    const LINE_NUMBERS_CSS_ID = 'prism-line-numbers-css';
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
                    loadCSS(LINE_NUMBERS_CSS_ID, '/css/vendor/prism-line-numbers.css');
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

        const LANG_LABELS: Record<string, string> = {
            javascript: 'JavaScript', typescript: 'TypeScript', csharp: 'C#',
            html: 'HTML', css: 'CSS', sql: 'SQL', json: 'JSON',
            bash: 'Bash', python: 'Python', markdown: 'Markdown',
            yaml: 'YAML', xml: 'XML', markup: 'HTML',
        };

        codeBlocks.forEach((block: Element) => {
            const pre = block.parentElement;
            if (pre) pre.classList.add('line-numbers');
            Prism.highlightElement(block);
            block.classList.add('prism-highlighted');

            // Add language label — wrapped outside the scrollable <pre>
            if (pre && !pre.parentElement?.classList.contains('code-block-wrapper')) {
                const langClass = Array.from(block.classList).find(c => c.startsWith('language-'));
                const lang = langClass?.replace('language-', '') || '';

                const wrapper = document.createElement('div');
                wrapper.className = 'code-block-wrapper';
                pre.parentNode!.insertBefore(wrapper, pre);
                wrapper.appendChild(pre);

                if (lang && lang !== 'none') {
                    const label = document.createElement('span');
                    label.className = 'code-language-label';
                    label.textContent = LANG_LABELS[lang] || lang;
                    wrapper.appendChild(label);
                }

                // Copy button
                const copyBtn = document.createElement('button');
                copyBtn.type = 'button';
                copyBtn.className = 'code-copy-btn';
                copyBtn.title = 'Copy code';
                copyBtn.setAttribute('aria-label', 'Copy code');
                copyBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>';
                copyBtn.addEventListener('click', async () => {
                    const code = block.textContent || '';
                    try {
                        await navigator.clipboard.writeText(code);
                        copyBtn.classList.add('code-copy-btn-copied');
                        copyBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>';
                        setTimeout(() => {
                            copyBtn.classList.remove('code-copy-btn-copied');
                            copyBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>';
                        }, 1500);
                    } catch { /* ignore */ }
                });
                wrapper.appendChild(copyBtn);
            }
        });
    }

    (window as any).SnakkSyntax = { highlightAll, loadPrism };

    // Run once on load, then after every HTMX swap. Scoped to document so we
    // catch blocks wherever they land. `:not(.prism-highlighted)` makes repeats free.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => highlightAll());
    } else {
        highlightAll();
    }
    document.body.addEventListener('htmx:afterSwap', () => highlightAll());
    document.body.addEventListener('htmx:afterSettle', () => highlightAll());
})();
