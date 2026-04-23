/**
 * New Discussion Page (all type-specific pages share this script)
 * Handles: Milkdown editor init, sessionStorage draft restore, form validation,
 * and beforeunload guard when the user has started filling out the form.
 * Images are uploaded immediately by the editor — no submit-time upload needed.
 */

(function() {
    'use strict';

    // ─── Submit-button blocker registry ─────────────────────────
    //
    // Multiple scripts (this one + gallery-upload.ts) need to block the
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

    window.SnakkNewDiscussion = { setBlocker, hasBlockers };

    // ─── beforeunload dirty guard ───────────────────────────────
    //
    // Snapshots every form field on load; isDirty() returns true if any field
    // differs, or the gallery has uploaded/uploading images. Modern browsers
    // ignore any custom message and show their own "Leave site?" dialog.
    function installUnloadGuard(form: HTMLFormElement, getEditorMarkdown: () => string): void {
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
            // Gallery uploads — treat any preview children as dirty, regardless of
            // whether the upload has finished or a hidden input exists yet.
            const gallery = document.getElementById('images-preview');
            if (gallery && gallery.querySelector('.images-upload-item')) return true;

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

        const handler = (e: BeforeUnloadEvent) => {
            if (!isDirty()) return;
            e.preventDefault();
            e.returnValue = '';
        };

        // Take the initial snapshot after any sessionStorage draft has been
        // restored so restored values don't count as "dirty".
        snapshotFields();

        window.addEventListener('beforeunload', handler);

        // Disarm on legitimate form submit.
        form.addEventListener('submit', () => {
            window.removeEventListener('beforeunload', handler);
        });

        // Escape hatch: any element with [data-allow-unload] (e.g. a Cancel button).
        document.addEventListener('click', (e) => {
            if ((e.target as HTMLElement).closest('[data-allow-unload]')) {
                window.removeEventListener('beforeunload', handler);
            }
        }, true);
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
        restoreDraft();

        const container = document.getElementById('editor-container');
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
            setBlocker('body', !(textarea.value.trim()));
        }

        titleInput?.addEventListener('input', () => {
            setBlocker('title', !titleInput.value.trim());
        });

        if (container && textarea && (window as any).SnakkEditor) {
            const placeholder = container.dataset.placeholder || 'Write your content...';
            const hideImageButton = container.dataset.hideImage === 'true';

            editor = await (window as any).SnakkEditor.init({
                container,
                textarea,
                placeholder,
                initialValue: textarea.value || '',
                hideImageButton,
                onChange: (markdown: string) => {
                    setBlocker('body', !markdown.trim());
                },
            });

            if (editor) {
                container.addEventListener('click', (e) => {
                    if (!(e.target as HTMLElement).closest('.milkdown-toolbar, .milkdown-footer')) {
                        editor.focus();
                    }
                });

                const submitBtn = document.getElementById('new-discussion-submit');
                const footer = container.querySelector('.milkdown-footer');
                if (submitBtn && footer) {
                    footer.appendChild(submitBtn);
                    submitBtn.classList.remove('hidden');
                }
            }
        }

        if (!form) return;

        form.addEventListener('submit', (e) => {
            const titleEl = form.querySelector('input[name="NewTitle"]') as HTMLInputElement | null;

            if (editor) {
                const md = editor.getMarkdown();
                if (!titleEl?.value.trim() || !md.trim()) {
                    e.preventDefault();
                    return;
                }
                // Sync editor content to hidden textarea for form submission
                if (textarea) textarea.value = md;
            }
        });

        // Install the unload guard after draft restore + editor init, so restored
        // values form the "clean" baseline. Guard works on every discussion type.
        installUnloadGuard(form, () => (editor ? editor.getMarkdown() : (textarea?.value ?? '')));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initEditor);
    } else {
        initEditor();
    }
})();
