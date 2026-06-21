/**
 * New Discussion Page (all type-specific pages share this script)
 * Handles: Milkdown editor init, sessionStorage draft restore, form validation,
 * and beforeunload guard when the user has started filling out the form.
 * Deferred blob-URL images (paste/drop, image-group toolbar) are flushed on submit.
 */

(function() {
    'use strict';

    // ─── Submit-button blocker registry ─────────────────────────
    //
    // Multiple scripts (this one + gallery-new.ts) need to block the
    // shared submit button. Each owns named blockers; button is disabled
    // iff any blocker is set. Stored in data-blockers as a space-separated
    // set so state survives DOM re-renders and is debuggable in devtools.
    function getSubmitBtn(): HTMLButtonElement | null {
        return document.getElementById('new-discussion-submit') as HTMLButtonElement | null;
    }

    function currentBlockers(btn: HTMLButtonElement): Set<string> {
        const raw = (btn.dataset.blockers || '').trim();
        return new Set(raw ? raw.split(/\s+/) : []);
    }

    function applyBlockers(btn: HTMLButtonElement, blockers: Set<string>): void {
        btn.dataset.blockers = Array.from(blockers).join(' ');
        btn.disabled = blockers.size > 0;
    }

    function setBlocker(name: string, blocked: boolean): void {
        const btn = getSubmitBtn();
        if (!btn) return;
        const blockers = currentBlockers(btn);
        if (blocked) blockers.add(name); else blockers.delete(name);
        applyBlockers(btn, blockers);
    }

    function hasBlockers(): boolean {
        const btn = getSubmitBtn();
        if (!btn) return false;
        return currentBlockers(btn).size > 0;
    }

    // Returns true only when the markdown contains meaningful text beyond
    // block-level fences (charts, code blocks, image groups, callouts).
    function hasTextContent(md: string): boolean {
        if (/^```image-group/m.test(md)) return true;
        return md.replace(/```[\s\S]*?```/g, '').trim().length > 0;
    }

    window.SnakkNewDiscussion = { setBlocker, hasBlockers };

    // ─── Teardown registry ──────────────────────────────────────
    //
    // Populated by initEditor on each run; flushed at the start of the
    // next run (HTMX back-navigation causes initEditor to fire again on
    // the existing page state). Prevents duplicate listeners and double
    // editor mounts.
    const teardowns: Array<() => void> = [];

    function runTeardowns(): void {
        for (const fn of teardowns) { try { fn(); } catch {} }
        teardowns.length = 0;
    }

    // ─── beforeunload dirty guard ───────────────────────────────
    //
    // Snapshots every form field on load; isDirty() returns true if any field
    // differs, or the gallery has uploaded/uploading images. Modern browsers
    // ignore any custom message and show their own "Leave site?" dialog.
    // Returns a teardown function for re-init cleanup.
    function installUnloadGuard(form: HTMLFormElement, getEditorMarkdown: () => string): () => void {
        const snapshot = new Map<string, string>();
        const initialEditorMd = getEditorMarkdown();

        const snapshotFields = () => {
            form.querySelectorAll<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>(
                'input, textarea, select'
            ).forEach(el => {
                if (el.type === 'hidden' && el.name === 'ImagesImageUrls') return;
                if (el.type === 'checkbox' || el.type === 'radio') {
                    snapshot.set(fieldKey(el), (el as HTMLInputElement).checked ? '1' : '0');
                } else {
                    snapshot.set(fieldKey(el), el.value);
                }
            });
        };

        const fieldKey = (el: Element) => {
            const name = (el as HTMLInputElement).name || '';
            const id = (el as HTMLElement).id || '';
            // Disambiguate repeated names (e.g. DebatePositions[]) by index in the form.
            const siblings = Array.from(form.querySelectorAll(`[name="${CSS.escape(name)}"]`));
            const index = siblings.indexOf(el);
            return `${name}|${id}|${index}`;
        };

        const isDirty = (): boolean => {
            // Gallery uploads — any images in the picker count as dirty.
            // SnakkGalleryNew is set by gallery-new.ts (deferred, runs after this script,
            // but isDirty is only called lazily so it's available by then).
            const galleryNew = (window as any).SnakkGalleryNew;
            if (galleryNew && galleryNew.getImageCount() > 0) return true;

            // Editor content (Milkdown is not a form field, it syncs into the
            // textarea only on submit — so compare directly).
            if (getEditorMarkdown().trim() !== initialEditorMd.trim()) return true;

            // Form field snapshot comparison.
            for (const el of form.querySelectorAll<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>(
                'input, textarea, select'
            )) {
                if (el.type === 'hidden' && el.name === 'ImagesImageUrls') continue;
                const current = el.type === 'checkbox' || el.type === 'radio'
                    ? ((el as HTMLInputElement).checked ? '1' : '0')
                    : el.value;
                const prev = snapshot.get(fieldKey(el));
                if (prev === undefined) {
                    // New field appeared (e.g. debate position added) → dirty.
                    if (current.length > 0 && current !== '0') return true;
                } else if (prev !== current) {
                    return true;
                }
            }
            return false;
        };

        // Single disarm flag shared by all guard paths (submit, allow-unload, user confirmed).
        let guardDisarmed = false;
        const disarmGuard = () => { guardDisarmed = true; };

        const beforeUnloadHandler = (e: BeforeUnloadEvent) => {
            if (guardDisarmed || !isDirty()) return;
            e.preventDefault();
            e.returnValue = '';
        };

        // Intercept anchor clicks at capture phase so we run before HTMX.
        // HTMX-boosted navigation never triggers beforeunload, so this is the
        // only hook that catches soft navigation away from the page.
        const navClickHandler = (e: MouseEvent) => {
            if (guardDisarmed) return;

            // [data-allow-unload] on any element (e.g. a Cancel button) disarms silently.
            if ((e.target as HTMLElement).closest('[data-allow-unload]')) {
                disarmGuard();
                return;
            }

            const anchor = (e.target as HTMLElement).closest<HTMLAnchorElement>('a[href]');
            if (!anchor) return;
            if (!isDirty()) return;

            if (!window.confirm('You have unsaved content. Leave this page?')) {
                // Block the click entirely — HTMX runs in bubble phase and never sees it.
                e.preventDefault();
                e.stopPropagation();
            } else {
                // User confirmed leave. Disarm beforeunload too so real navigations
                // (non-boosted links) don't show a second browser dialog.
                disarmGuard();
            }
        };

        // Take the initial snapshot after any sessionStorage draft has been
        // restored so restored values don't count as "dirty".
        snapshotFields();

        window.addEventListener('beforeunload', beforeUnloadHandler);
        document.addEventListener('click', navClickHandler, true);

        // Disarm on legitimate form submit.
        const submitDisarm = () => disarmGuard();
        form.addEventListener('submit', submitDisarm);

        return () => {
            window.removeEventListener('beforeunload', beforeUnloadHandler);
            document.removeEventListener('click', navClickHandler, true);
            form.removeEventListener('submit', submitDisarm);
        };
    }

    // Restore draft from sessionStorage if navigating from type picker
    function restoreDraft(): void {
        const params = new URLSearchParams(window.location.search);
        const draftId = params.get('draft');
        if (!draftId) return;

        const key = `snakk-draft-${draftId}`;
        const raw = sessionStorage.getItem(key);
        if (!raw) return;

        try {
            const draft = JSON.parse(raw);
            const titleInput = document.getElementById('new-discussion-title') as HTMLInputElement;
            const textarea = document.getElementById('new-discussion-content') as HTMLTextAreaElement;

            if (titleInput && draft.title) titleInput.value = draft.title;
            if (textarea && draft.content) textarea.value = draft.content;
        } catch { /* ignore parse errors */ }

        sessionStorage.removeItem(key);
    }

    async function initEditor(): Promise<void> {
        // Tear down any previous session before re-initialising (HTMX back-navigation
        // restores the cached DOM and re-runs scripts, so this runs more than once).
        runTeardowns();

        restoreDraft();

        const container = document.getElementById('editor-container') as HTMLElement | null;
        const textarea = document.getElementById('new-discussion-content') as HTMLTextAreaElement | null;

        // Form may exist without a Milkdown editor (e.g. gallery/poll types).
        // Find the form via the title input as a fallback.
        const titleInput = document.getElementById('new-discussion-title') as HTMLInputElement | null;
        const form = (textarea?.closest('form') ?? titleInput?.closest('form')) as HTMLFormElement | null;

        let editor: any = null;

        // Seed blockers before the editor boots so the button stays disabled
        // from the moment it's unhidden (see submitBtn.classList.remove('hidden')
        // below). Title blocker tracks the input; body blocker is only relevant
        // when this page actually has an editor.
        setBlocker('title', !(titleInput?.value.trim()));
        if (container && textarea) {
            setBlocker('body', !hasTextContent(textarea.value));
        }

        if (titleInput) {
            const titleHandler = () => setBlocker('title', !titleInput.value.trim());
            titleInput.addEventListener('input', titleHandler);
            teardowns.push(() => titleInput.removeEventListener('input', titleHandler));
        }

        if (container && textarea && (window as any).SnakkEditor) {
            // Destroy any previous editor in this container before re-mounting.
            (window as any).SnakkEditor.destroy(container);

            const placeholder = container.dataset.placeholder || 'Write your content...';

            editor = await (window as any).SnakkEditor.init({
                container,
                textarea,
                placeholder,
                initialValue: textarea.value || '',
                onChange: (markdown: string) => {
                    setBlocker('body', !hasTextContent(markdown));
                },
                onUploadStateChange: (uploading: boolean) => {
                    setBlocker('uploading-inline', uploading);
                },
            });

            teardowns.push(() => (window as any).SnakkEditor?.destroy(container));

            if (editor) {
                const containerClickHandler = (e: MouseEvent) => {
                    if (!(e.target as HTMLElement).closest('.milkdown-toolbar, .milkdown-footer')) {
                        editor.focus();
                    }
                };
                container.addEventListener('click', containerClickHandler);
                teardowns.push(() => container.removeEventListener('click', containerClickHandler));

                const submitBtn = document.getElementById('new-discussion-submit');
                const footer = container.querySelector('.milkdown-footer');
                if (submitBtn && footer) {
                    footer.appendChild(submitBtn);
                    submitBtn.classList.remove('sn-hidden');
                }
            }
        }

        if (!form) return;

        let deferredSubmitReady = false;

        const submitHandler = async (e: Event) => {
            const titleEl = form.querySelector('input[name="NewTitle"]') as HTMLInputElement | null;

            if (editor) {
                // Second pass: uploads done — sync CDN-URL markdown and let submit proceed
                if (deferredSubmitReady) {
                    deferredSubmitReady = false;
                    if (textarea) textarea.value = editor.getMarkdown();
                    return;
                }

                const md = editor.getMarkdown();
                if (!titleEl?.value.trim() || !md.trim()) {
                    e.preventDefault();
                    return;
                }

                if (!editor.hasPendingUploads()) {
                    // Fast path: no deferred uploads, sync and submit normally
                    if (textarea) textarea.value = md;
                    return;
                }

                // Deferred path: flush blob-URL images before submitting
                e.preventDefault();
                setBlocker('flushing', true);
                try {
                    await editor.flushUploads();
                } catch (err) {
                    console.error('[Editor] Deferred upload failed:', err);
                    setBlocker('flushing', false);
                    return;
                }
                setBlocker('flushing', false);
                deferredSubmitReady = true;
                form.dataset.allowResubmit = 'true';
                form.requestSubmit();
            }
        };

        form.addEventListener('submit', submitHandler);
        teardowns.push(() => form.removeEventListener('submit', submitHandler));

        // Install the unload guard after draft restore + editor init, so restored
        // values form the "clean" baseline. Guard works on every discussion type.
        teardowns.push(installUnloadGuard(form, () => (editor ? editor.getMarkdown() : (textarea?.value ?? ''))));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initEditor);
    } else {
        initEditor();
    }
})();
