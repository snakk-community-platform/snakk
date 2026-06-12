/**
 * Snakk Syntax Highlighting Component
 * Lazy-loads Prism.js on demand when posts contain code blocks.
 */

(function(): void {
    'use strict';

    if ((window as any).SnakkSyntax) return;

    const CSS_ID = 'prism-css';
    const LINE_NUMBERS_CSS_ID = 'prism-line-numbers-css';
    const PRISM_VERSION = '1.30.0';
    let loadPromise: Promise<any> | null = null;

    const EXPAND_SVG = '<span class="icon icon-code-expand" style="width:12px;height:12px" aria-hidden="true"></span>';
    const COLLAPSE_SVG = '<span class="icon icon-code-collapse" style="width:12px;height:12px" aria-hidden="true"></span>';

    let expandedWrapper: HTMLElement | null = null;
    let expandedBtn: HTMLElement | null = null;
    let expandedBackdrop: HTMLElement | null = null;
    let expandedPlaceholder: HTMLElement | null = null;
    let expandedEscapeHandler: ((e: KeyboardEvent) => void) | null = null;

    function collapseExpanded(): void {
        if (!expandedWrapper || !expandedBtn) return;
        expandedWrapper.classList.remove('code-block-expanded');
        document.documentElement.style.overflowY = '';
        expandedBtn.innerHTML = EXPAND_SVG;
        expandedBtn.title = 'Expand code';
        expandedBackdrop?.remove();
        expandedPlaceholder?.remove();
        if (expandedEscapeHandler) {
            document.removeEventListener('keydown', expandedEscapeHandler);
        }
        expandedWrapper = null;
        expandedBtn = null;
        expandedBackdrop = null;
        expandedPlaceholder = null;
        expandedEscapeHandler = null;
    }

    function toggleCodeBlockExpand(wrapper: HTMLElement, btn: HTMLElement): void {
        // If this wrapper is already expanded, collapse it.
        if (wrapper.classList.contains('code-block-expanded')) {
            collapseExpanded();
            return;
        }
        // If a different wrapper is expanded, collapse it first (single-overlay invariant).
        if (expandedWrapper) collapseExpanded();

        const rect = wrapper.getBoundingClientRect();
        const placeholder = document.createElement('div');
        placeholder.style.height = `${rect.height}px`;
        wrapper.parentElement?.insertBefore(placeholder, wrapper);

        wrapper.classList.add('code-block-expanded');
        document.documentElement.style.overflowY = 'hidden';
        btn.innerHTML = COLLAPSE_SVG;
        btn.title = 'Collapse code';

        const backdrop = document.createElement('div');
        backdrop.className = 'code-block-backdrop';
        backdrop.addEventListener('click', () => collapseExpanded());
        document.body.appendChild(backdrop);

        const escHandler = (e: KeyboardEvent) => {
            if (e.key === 'Escape') collapseExpanded();
        };
        document.addEventListener('keydown', escHandler);

        expandedWrapper = wrapper;
        expandedBtn = btn;
        expandedBackdrop = backdrop;
        expandedPlaceholder = placeholder;
        expandedEscapeHandler = escHandler;
    }

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

    function getLangIconsSrc(): string | null {
        return document.querySelector<HTMLMetaElement>('meta[name="lang-icons-src"]')?.content || null;
    }

    let langIconsPromise: Promise<void> | null = null;

    function loadLangIcons(): Promise<void> {
        if (langIconsPromise) return langIconsPromise;
        const src = getLangIconsSrc();
        if (!src) {
            langIconsPromise = Promise.resolve();
            return langIconsPromise;
        }
        langIconsPromise = loadScript(src).catch(() => { /* icons are optional */ });
        return langIconsPromise;
    }

    async function loadPrism(): Promise<any> {
        if (!loadPromise) {
            loadPromise = (async () => {
                try {
                    loadCSS(CSS_ID, `/css/vendor/prism.css?v=${PRISM_VERSION}`);
                    loadCSS(LINE_NUMBERS_CSS_ID, `/css/vendor/prism-line-numbers.css?v=${PRISM_VERSION}`);
                    // Inject lang-icons in parallel with Prism so both are ready for labeling.
                    await Promise.all([
                        loadScript(`/js/vendor/prism.js?v=${PRISM_VERSION}`),
                        loadLangIcons(),
                    ]);
                    const Prism = (window as any).Prism;
                    if (!Prism) {
                        console.error('[SnakkSyntax] Prism not found on window after load');
                        return null;
                    }
                    // Disable Prism's automatic highlighting on page load
                    Prism.manual = true;
                    return Prism;
                } catch (e) {
                    console.error('[SnakkSyntax] Failed to load Prism', e);
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
        // Re-ensure stylesheets are present — head-support (merge mode) removes
        // dynamically-added <link> tags that aren't in the new page's <head>.
        loadCSS(CSS_ID, `/css/vendor/prism.css?v=${PRISM_VERSION}`);
        loadCSS(LINE_NUMBERS_CSS_ID, `/css/vendor/prism-line-numbers.css?v=${PRISM_VERSION}`);

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

                const header = document.createElement('div');
                header.className = 'code-block-header';

                const label = document.createElement('span');
                label.className = 'code-language-label';
                label.textContent = (lang && lang !== 'none') ? (LANG_LABELS[lang] || lang) : 'Plain text';
                header.appendChild(label);

                const actions = document.createElement('div');
                actions.className = 'code-block-header-actions';

                // Expand button
                const expandBtn = document.createElement('button');
                expandBtn.type = 'button';
                expandBtn.className = 'code-expand-btn';
                expandBtn.title = 'Expand code';
                expandBtn.setAttribute('aria-label', 'Expand code');
                expandBtn.innerHTML = EXPAND_SVG;
                expandBtn.addEventListener('click', () => toggleCodeBlockExpand(wrapper, expandBtn));
                actions.appendChild(expandBtn);

                // Copy button
                const copyBtn = document.createElement('button');
                copyBtn.type = 'button';
                copyBtn.className = 'code-copy-btn';
                copyBtn.title = 'Copy code';
                copyBtn.setAttribute('aria-label', 'Copy code');
                copyBtn.innerHTML = '<span class="icon icon-copy" style="width:14px;height:14px" aria-hidden="true"></span>';
                copyBtn.addEventListener('click', async () => {
                    const code = block.textContent || '';
                    try {
                        await navigator.clipboard.writeText(code);
                        copyBtn.classList.add('code-copy-btn-copied');
                        copyBtn.innerHTML = '<span class="icon icon-check" style="width:14px;height:14px" aria-hidden="true"></span>';
                        setTimeout(() => {
                            copyBtn.classList.remove('code-copy-btn-copied');
                            copyBtn.innerHTML = '<span class="icon icon-copy" style="width:14px;height:14px" aria-hidden="true"></span>';
                        }, 1500);
                    } catch { /* ignore */ }
                });
                actions.appendChild(copyBtn);

                header.appendChild(actions);
                wrapper.insertBefore(header, pre);
            }
        });
    }

    function resetHighlighting(container?: HTMLElement): void {
        const root = container || document;
        root.querySelectorAll<HTMLElement>('code.prism-highlighted').forEach(el => {
            el.classList.remove('prism-highlighted');
        });
        root.querySelectorAll<HTMLElement>('.code-block-wrapper').forEach(wrapper => {
            const pre = wrapper.querySelector('pre');
            if (pre && wrapper.parentNode) {
                wrapper.parentNode.insertBefore(pre, wrapper);
                wrapper.remove();
            }
        });
    }

    (window as any).SnakkSyntax = { highlightAll, loadPrism, resetHighlighting };

    // Run once on load, then after every HTMX settle. Scoped to document so we
    // catch blocks wherever they land. `:not(.prism-highlighted)` makes repeats free.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => highlightAll());
    } else {
        highlightAll();
    }
    document.addEventListener('htmx:afterSettle', () => highlightAll());
    document.addEventListener('htmx:historyRestore', () => {
        resetHighlighting();
        highlightAll();
    });
})();
