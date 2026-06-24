/**
 * Discussion Detail Page JavaScript
 * Handles all discussion page interactions: editor, reactions, replies, etc.
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface PostAuthor {
    publicId: string;
    displayName: string;
    avatarUrl?: string;
    avatarThumbnailUrl?: string;
    role?: 'admin' | 'mod' | 'user';
    isDeleted?: boolean;
    joinedAt?: string;
    discussionCount?: number;
    replyCount?: number;
}

interface PostReplyTo {
    postId: string;
    authorName: string;
    contentSnippet: string;
}

interface Post {
    postNumber: number;
    publicId: string;
    content: string;
    renderedContent?: string;
    author: PostAuthor;
    createdAt: string;
    editedAt?: string;
    isFirstPost: boolean;
    isNecro?: boolean;
    isOp?: boolean;
    isMilestone?: boolean;
    isUsersFirstPostInDiscussion?: boolean;
    isUsersFirstPostInSpace?: boolean;
    replyTo?: PostReplyTo;
}

interface ReactionCountsResponse {
    counts: Record<string, number>;
}

interface MyReactionsResponse {
    reactions: string[];
}

interface FollowStatus {
    isFollowing: boolean;
}

interface DiscussionConfig {
    discussionId: string;
    discussionType?: string;
    isAuthenticated: boolean;
    currentUserId: string;
    isLocked: boolean;
    spaceSlug?: string;
    hubSlug?: string;
    spaceId?: string;
    hubId?: string;
    communityId?: string;
    postsCurrentOffset: number;
    postsHasMoreItems: boolean;
    hasCodeBlocks: boolean;
    postCount: number;
    displayName: string;
    officialAnswers?: Record<string, string>;
    sort?: string;
    lastReadPostId?: string | null;
    isModerator?: boolean;
    unreadLabel?: string;
}

interface ReportReason {
    publicId: string;
    name: string;
    description?: string;
}

interface CurrentSelection {
    postId: string | null;
    text: string;
    authorName: string;
}

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

let discussionConfig: DiscussionConfig | null = null;
let iamaOfficialAnswers: Record<string, string> = {};
let iamaAnswerToQuestion: Record<string, string> = {};

// ===== Editor Functions =====

function loadCSS(id: string, href: string): void {
    if (document.getElementById(id)) return;
    const link = document.createElement('link');
    link.id = id;
    link.rel = 'stylesheet';
    link.href = href;
    document.head.appendChild(link);
}

// Auto-grow textarea (used for inline edit textareas)
function autoGrow(element: HTMLTextAreaElement): void {
    element.style.height = 'auto';
    element.style.height = Math.max(96, element.scrollHeight) + 'px';
}

// Get the active editor instance (Toast UI) for the reply form
function getReplyEditor(): any {
    const container = document.getElementById('editor-container');
    return container ? (window as any).SnakkEditor?.getInstance(container) : null;
}

// Get the current reply content from either Toast UI editor or fallback textarea
function getReplyContent(): string {
    const editor = getReplyEditor();
    if (editor) return editor.getMarkdown();
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
    return textarea?.value || '';
}

// Set reply content in either Toast UI editor or fallback textarea
function setReplyContent(content: string): void {
    const editor = getReplyEditor();
    if (editor) {
        editor.setMarkdown(content);
    } else {
        const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
        if (textarea) textarea.value = content;
    }
}

// Focus the reply editor
function focusReplyEditor(): void {
    const editor = getReplyEditor();
    if (editor) {
        editor.focus();
    } else {
        const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
        textarea?.focus();
    }
}

// Initialize Toast UI editor for the reply form (deduped â€” safe to call multiple times)
let editorInitPromise: Promise<void> | null = null;
let activeDiscussionId: string | null = null;

function initReplyEditor(): Promise<void> {
    if (editorInitPromise) return editorInitPromise;

    editorInitPromise = (async () => {
        const container = document.getElementById('editor-container');
        const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
        if (!container || !textarea) return;

        // Wait for the lazy-loaded markdown-editor script
        for (let i = 0; i < 50 && !(window as any).SnakkEditor; i++) {
            await new Promise(r => setTimeout(r, 100));
        }
        if (!(window as any).SnakkEditor) return;

        let replyUploading = false;
        let replyMd = '';

        function hasTextContent(md: string): boolean {
            return md.replace(/```[\s\S]*?```/g, '').trim().length > 0;
        }

        const MAX_POST_LENGTH = 50000;

        function updateCharCount(): void {
            const countEl = document.getElementById('reply-char-count');
            if (!countEl) return;
            const len = replyMd.length;
            if (len >= MAX_POST_LENGTH * 0.8) {
                countEl.classList.remove('sn-hidden');
                countEl.textContent = `${len.toLocaleString()} / ${MAX_POST_LENGTH.toLocaleString()}`;
                countEl.className = len > MAX_POST_LENGTH
                    ? 'sn-text-xs sn-text-error sn-font-medium'
                    : 'sn-text-xs sn-text-warning';
            } else {
                countEl.className = 'sn-text-xs sn-hidden';
            }
        }

        function updateReplyBtn(): void {
            const btn = document.getElementById('reply-submit-btn') as HTMLButtonElement | null;
            if (!btn) return;
            const form = btn.closest('form') ?? document.getElementById('reply-form');
            const hasPicker = !!form?.querySelector('.debate-position-picker');
            const hasPosition = !!form?.querySelector('input[name="DebatePositionId"]');
            const positionOk = !hasPicker || hasPosition;
            const tooLong = replyMd.length > MAX_POST_LENGTH;
            btn.disabled = !hasTextContent(replyMd) || replyUploading || !positionOk || tooLong;
            btn.title = hasPicker && !hasPosition ? 'Pick a position before replying' : '';
            updateCharCount();
        }

        const editor = await (window as any).SnakkEditor.init({
            container,
            textarea,
            placeholder: 'Share your thoughts...',
            onChange: (md: string) => {
                replyMd = md;
                updateReplyBtn();
            },
            onUploadStateChange: (uploading: boolean) => {
                replyUploading = uploading;
                updateReplyBtn();
            },
        });

        // Focus editor when clicking anywhere in the container (not toolbar/footer)
        if (editor) {
            container.addEventListener('click', (e) => {
                if (!(e.target as HTMLElement).closest('.milkdown-toolbar, .milkdown-footer')) {
                    editor.focus();
                }
            });

            // Move submit button into the editor footer; starts disabled until content is typed
            const submitBtn = document.getElementById('reply-submit-btn') as HTMLButtonElement | null;
            const footer = container.querySelector('.milkdown-footer');
            if (submitBtn && footer) {
                submitBtn.disabled = true;
                footer.appendChild(submitBtn);
                submitBtn.classList.remove('sn-hidden');

                const charCountEl = document.createElement('span');
                charCountEl.id = 'reply-char-count';
                charCountEl.className = 'sn-text-xs sn-hidden';
                footer.appendChild(charCountEl);
            }

            // Typing indicator: fire when content actually changes (not on arrow/modifier keys)
            textarea.addEventListener('input', onReplyContentChanged);

            // Re-evaluate submit button when a debate position is selected.
            const replyForm = textarea.closest('form') ?? document.getElementById('reply-form');
            replyForm?.addEventListener('snakk:debate:position-changed', () => updateReplyBtn());

            // If the debate picker was inserted before the editor finished initializing
            // (fetch resolved before SnakkEditor.init), move it into the message area now.
            const messageArea = container.querySelector('.milkdown-message-area');
            const earlyPicker = replyForm?.querySelector('.debate-position-picker');
            if (messageArea && earlyPicker && !messageArea.contains(earlyPicker)) {
                messageArea.appendChild(earlyPicker);
            }
        }

        // Intercept form submit: upload any deferred blob-URL images, then re-submit
        const form = textarea.closest('form') as HTMLFormElement | null;
        if (form && editor) {
            console.log('[Editor] form submit handler attached, hx-boost=', form.getAttribute('hx-boost'));

            let deferredSubmitReady = false;

            form.addEventListener('submit', async (e) => {
                clearComposingState();
                // Second pass: uploads done â€” let the event proceed
                if (deferredSubmitReady) {
                    deferredSubmitReady = false;
                    textarea.value = editor.getMarkdown();
                    return;
                }

                const md = editor.getMarkdown();
                if (!md.trim()) {
                    e.preventDefault();
                    return;
                }

                if (md.length > MAX_POST_LENGTH) {
                    e.preventDefault();
                    return;
                }

                // Clear draft immediately before submission proceeds
                if (activeDiscussionId && (window as any).SnakkDraftManager) {
                    const replyToPostId = (document.getElementById('reply-to-post-id') as HTMLInputElement)?.value || null;
                    (window as any).SnakkDraftManager.clearDraftOnSuccess(activeDiscussionId, replyToPostId);
                }

                if (!editor.hasPendingUploads()) {
                    textarea.value = md;
                    return;
                }

                // Deferred path: upload images before submitting
                e.preventDefault();

                const btn = document.getElementById('reply-submit-btn') as HTMLButtonElement | null;
                const btnSpinner = document.getElementById('reply-submit-spinner');
                const progressRow = document.getElementById('upload-progress-row');
                const chipsEl = document.getElementById('upload-image-chips');
                const statusEl = document.getElementById('upload-status-text');

                if (btn) btn.disabled = true;
                btnSpinner?.classList.remove('sn-hidden');

                const showProgress = (done: number, total: number): void => {
                    if (done === 0) {
                        // Build chips â€” all start as "waiting"
                        if (chipsEl) {
                            chipsEl.innerHTML = '';
                            for (let i = 0; i < total; i++) {
                                const chip = document.createElement('span');
                                chip.className = 'sn-upload-chip sn-upload-chip-waiting';
                                chip.id = `upload-chip-${i}`;
                                chipsEl.appendChild(chip);
                            }
                            // Mark first chip as uploading
                            const first = document.getElementById('upload-chip-0');
                            if (first) first.className = 'sn-upload-chip sn-upload-chip-uploading';
                        }
                        if (statusEl) statusEl.textContent = `Uploading 1 of ${total}â€¦`;
                        progressRow?.classList.remove('sn-hidden');
                    } else {
                        // Mark completed chip as done
                        const doneChip = document.getElementById(`upload-chip-${done - 1}`);
                        if (doneChip) doneChip.className = 'sn-upload-chip sn-upload-chip-done';
                        // Mark next chip as uploading
                        const nextChip = document.getElementById(`upload-chip-${done}`);
                        if (nextChip) nextChip.className = 'sn-upload-chip sn-upload-chip-uploading';
                        if (statusEl) statusEl.textContent = done < total ? `Uploading ${done + 1} of ${total}â€¦` : `Uploading ${total} of ${total}â€¦`;
                    }
                };

                try {
                    await editor.flushUploads(showProgress);
                } catch (err) {
                    console.error('[Editor] Deferred upload failed:', err);
                    if (btn) btn.disabled = false;
                    btnSpinner?.classList.add('sn-hidden');
                    progressRow?.classList.add('sn-hidden');
                    return;
                }

                btnSpinner?.classList.add('sn-hidden');

                deferredSubmitReady = true;
                // Bypass the global actions.ts submit handler (which would set "Submitting...")
                // for this programmatic re-submit. Progress row stays visible until page navigates.
                form.dataset.allowResubmit = 'true';
                form.requestSubmit();
            });
        }

        // Restore drafts once editor is ready
        if (activeDiscussionId) {
            initDraftAutoSave(activeDiscussionId);
        }
    })();

    return editorInitPromise;
}

// ===== Reply/Quote Functions =====

// Reply to a specific post
function openComposer(): void {
    const drawer = document.getElementById('compose-drawer');
    const btn = document.getElementById('compose-btn-wrap');
    if (!drawer) return;
    drawer.classList.add('sn-compose-drawer--open');
    btn?.classList.add('sn-hidden');
    // First open: lazy-load the editor CSS + init
    loadCSS('snakk-editor-css', '/css/features/editor.css');
    initReplyEditor().then(() => focusReplyEditor());
}

function closeComposer(): void {
    const drawer = document.getElementById('compose-drawer');
    const btn = document.getElementById('compose-btn-wrap');
    drawer?.classList.remove('sn-compose-drawer--open');
    btn?.classList.remove('sn-hidden');
    clearReplyContext();
}

function replyToPost(postId: string, authorName: string): void {
    const replyToInput = document.getElementById('reply-to-post-id') as HTMLInputElement;
    const replyContext = document.getElementById('reply-context');
    const replyContextAuthor = document.getElementById('reply-context-author');

    if (replyToInput) replyToInput.value = postId;
    if (replyContext) replyContext.classList.remove('sn-hidden');
    if (replyContextAuthor) replyContextAuthor.textContent = authorName;

    openComposer();
}

// Quote a post's content (or selected text)
function quotePost(postId: string, content: string, authorName: string): void {
    const quote = `> ${authorName} wrote:\n> ${content.split('\n').join('\n> ')}\n\n`;
    const current = getReplyContent();
    setReplyContent(quote + current);
    replyToPost(postId, authorName);
}

// ===== Smart Selection Quote =====

// Track current selection for smart quoting
let currentSelection: CurrentSelection = { postId: null, text: '', authorName: '' };

let quoteButtonActive = false;
function hideSelectionQuoteButton(): void {
    if (!quoteButtonActive) return;
    const btn = document.getElementById('selection-quote-btn');
    if (btn) btn.remove();
    quoteButtonActive = false;
}

function showSelectionQuoteButton(): void {
    hideSelectionQuoteButton(); // Remove any existing

    const selection = window.getSelection();
    if (!selection || !selection.rangeCount || !currentSelection.text) return;

    const range = selection.getRangeAt(0);
    const rect = range.getBoundingClientRect();

    // Don't show if rect is invalid (collapsed selection)
    if (rect.width === 0 && rect.height === 0) return;

    const button = document.createElement('button');
    button.id = 'selection-quote-btn';
    button.className = 'sn-fixed sn-z-50 sn-btn sn-btn-xs sn-btn-primary';
    button.textContent = 'Quote selection';

    // Position BELOW the selection (to avoid Edge's mini menu above)
    const left = Math.max(10, rect.left);
    const top = rect.bottom + window.scrollY + 5;

    button.style.left = `${left}px`;
    button.style.top = `${top}px`;

    button.onmousedown = (e) => {
        e.preventDefault(); // Prevent losing selection
    };

    button.onclick = (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (currentSelection.postId) {
            quotePost(currentSelection.postId, currentSelection.text, currentSelection.authorName);
        }
        hideSelectionQuoteButton();
        window.getSelection()?.removeAllRanges();
    };

    document.body.appendChild(button);
    quoteButtonActive = true;
}

// Clear reply context
function clearReplyContext(): void {
    const replyToInput = document.getElementById('reply-to-post-id') as HTMLInputElement;
    const replyContext = document.getElementById('reply-context');

    if (replyToInput) replyToInput.value = '';
    if (replyContext) replyContext.classList.add('sn-hidden');
}

// Highlight a referenced post when clicking quote
function highlightPost(postId: string): void {
    const post = document.querySelector<HTMLElement>(`[data-post-id="${postId}"]`);
    if (post) {
        post.classList.add('sn-post-highlight');
        setTimeout(() => post.classList.remove('sn-post-highlight'), 2000);
    }
}

// Edit post â€” WYSIWYG via Milkdown
const activeEditEditors = new Map<string, any>();

async function ensureEditorLoaded(): Promise<void> {
    if ((window as any).SnakkEditor) return;
    const configEl = document.getElementById('editor-loader-config');
    if (!configEl) return;
    let editorSrc: string;
    try { editorSrc = JSON.parse(configEl.textContent || '{}').editorSrc; }
    catch { return; }
    if (!editorSrc) return;
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = editorSrc;
        script.onload = () => resolve();
        script.onerror = reject;
        document.head.appendChild(script);
    });
}

async function editPost(postId: string, _userId: string): Promise<void> {
    const contentDiv = document.getElementById('post-content-' + postId);
    if (!contentDiv) return;

    const rawContent = (contentDiv as HTMLElement).dataset.rawContent || '';
    (contentDiv as HTMLElement).dataset.originalHtml = contentDiv.innerHTML;
    (contentDiv as HTMLElement).dataset.originalRawContent = rawContent;

    const editorContainerId = `edit-editor-${postId}`;
    const textareaId = `edit-textarea-${postId}`;
    contentDiv.innerHTML = `
        <div id="${editorContainerId}" class="sn-min-h-50"></div>
        <textarea id="${textareaId}" style="display:none"></textarea>
        <div class="sn-flex sn-gap-2 sn-mt-3">
            <button type="button" class="sn-btn sn-btn-primary sn-btn-sm" data-action="submit-edit" data-post-id="${postId}">Save</button>
            <button type="button" class="sn-btn sn-btn-ghost sn-btn-sm" data-action="cancel-edit" data-post-id="${postId}">Cancel</button>
        </div>
    `;

    await ensureEditorLoaded();

    const instance = await (window as any).SnakkEditor.init({
        container: document.getElementById(editorContainerId),
        textarea: document.getElementById(textareaId) as HTMLTextAreaElement,
        initialValue: rawContent,
    });
    activeEditEditors.set(postId, instance);
    instance.focus();
}

const escapeHtml = (text: string): string => (window as any).SnakkUtils.escapeHtml(text);

const encodeUlid = (window as any).SnakkUtils?.encodeUlid || function(s: string): string { return s; };

const sanitizeHtml = (html: string): string => (window as any).SnakkUtils.sanitizeHtml(html);

async function submitEdit(postId: string): Promise<void> {
    const editor = activeEditEditors.get(postId);
    const contentDiv = document.getElementById('post-content-' + postId);
    if (!editor || !contentDiv) return;

    const saveBtn = contentDiv.querySelector('[data-action="submit-edit"]') as HTMLButtonElement | null;
    if (saveBtn) { saveBtn.disabled = true; saveBtn.textContent = 'Savingâ€¦'; }

    try {
        if (editor.hasPendingUploads()) await editor.flushUploads();
        const content = editor.getMarkdown() as string;

        const response = await fetch(`/bff/posts/${postId}/edit`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify({ content })
        });

        if (!response.ok) {
            if (saveBtn) { saveBtn.disabled = false; saveBtn.textContent = 'Save'; }
            showToast('Failed to save. Please try again.', 'error');
            return;
        }

        const renderedHtml = await response.text();
        editor.destroy();
        activeEditEditors.delete(postId);
        contentDiv.innerHTML = sanitizeHtml(renderedHtml);
        (contentDiv as HTMLElement).dataset.rawContent = content;
    } catch {
        if (saveBtn) { saveBtn.disabled = false; saveBtn.textContent = 'Save'; }
    }
}

function cancelEdit(postId: string): void {
    const editor = activeEditEditors.get(postId);
    editor?.destroy();
    activeEditEditors.delete(postId);

    const contentDiv = document.getElementById('post-content-' + postId);
    if (!contentDiv) return;

    const originalHtml = (contentDiv as HTMLElement).dataset.originalHtml;
    const originalRaw = (contentDiv as HTMLElement).dataset.originalRawContent;
    if (originalHtml) {
        contentDiv.innerHTML = originalHtml;
        delete (contentDiv as HTMLElement).dataset.originalHtml;
        if (originalRaw !== undefined) (contentDiv as HTMLElement).dataset.rawContent = originalRaw;
        delete (contentDiv as HTMLElement).dataset.originalRawContent;
    }
}

// Discussion title edit
function editDiscussionTitle(): void {
    const container = document.getElementById('discussion-title-container');
    if (!container) return;

    const currentTitle = container.dataset.currentTitle ?? '';
    container.dataset.originalTitleHtml = container.innerHTML;

    container.innerHTML = `
        <div class="sn-flex sn-items-center sn-gap-2 sn-w-full">
            <input type="text"
                   id="discussion-title-input"
                   class="sn-input sn-input sn-flex-1 sn-text-xl sn-font-bold"
                   value="${escapeHtml(currentTitle)}"
                   maxlength="300" />
            <button type="button" class="sn-btn sn-btn-primary sn-btn-sm sn-shrink-0" data-action="submit-discussion-title">Save</button>
            <button type="button" class="sn-btn sn-btn-ghost sn-btn-sm sn-shrink-0" data-action="cancel-discussion-title">Cancel</button>
        </div>
    `;

    const input = document.getElementById('discussion-title-input') as HTMLInputElement | null;
    if (input) {
        input.focus();
        input.select();
        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') submitDiscussionTitle();
            if (e.key === 'Escape') cancelDiscussionTitle();
        });
    }
}

async function submitDiscussionTitle(): Promise<void> {
    const container = document.getElementById('discussion-title-container');
    if (!container) return;

    const discussionId = container.dataset.discussionId ?? '';
    const input = document.getElementById('discussion-title-input') as HTMLInputElement | null;
    const newTitle = input?.value.trim() ?? '';
    if (!newTitle) return;

    const saveBtn = container.querySelector('[data-action="submit-discussion-title"]') as HTMLButtonElement | null;
    if (saveBtn) { saveBtn.disabled = true; saveBtn.textContent = 'Savingâ€¦'; }

    try {
        const response = await fetch(`/bff/discussions/${discussionId}/title`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify({ title: newTitle })
        });

        if (!response.ok) {
            if (saveBtn) { saveBtn.disabled = false; saveBtn.textContent = 'Save'; }
            showToast('Failed to save. Please try again.', 'error');
            return;
        }

        container.dataset.currentTitle = newTitle;
        container.innerHTML = container.dataset.originalTitleHtml ?? '';
        delete container.dataset.originalTitleHtml;

        const h1 = container.querySelector('h1');
        if (h1) {
            const lastTextNode = Array.from(h1.childNodes)
                .filter(n => n.nodeType === Node.TEXT_NODE)
                .pop();
            if (lastTextNode) lastTextNode.textContent = newTitle;
        }

        document.title = newTitle;
    } catch {
        if (saveBtn) { saveBtn.disabled = false; saveBtn.textContent = 'Save'; }
    }
}

function cancelDiscussionTitle(): void {
    const container = document.getElementById('discussion-title-container');
    if (!container) return;

    const original = container.dataset.originalTitleHtml;
    if (original) {
        container.innerHTML = original;
        delete container.dataset.originalTitleHtml;
    }
}

let lastReadPostId: string | null = null;
let unreadLabel: string = '';

function insertUnreadSeparator(): void {
    document.getElementById('unread-separator')?.remove();
    if (!lastReadPostId) return;
    const lastRead = document.querySelector<HTMLElement>(`article[data-post-id="${lastReadPostId}"]`);
    if (!lastRead) return;
    const sep = document.createElement('div');
    sep.id = 'unread-separator';
    sep.className = 'sn-unread-separator';
    sep.setAttribute('role', 'separator');
    sep.textContent = unreadLabel || 'Unread';
    lastRead.after(sep);
}

// Mark posts as read via IntersectionObserver (no scroll polling or querySelectorAll)
let readObserver: IntersectionObserver | null = null;
let readFlushTimeout: ReturnType<typeof setTimeout> | null = null;
let latestVisiblePostId: string | null = null;

function initReadObserver(): void {
    if (readObserver) readObserver.disconnect();

    readObserver = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                const postId = (entry.target as HTMLElement).dataset.postId;
                if (postId) latestVisiblePostId = postId;
            }
        }
        flushReadState();
    }, { threshold: 0.1 });

    // Observe existing posts
    document.querySelectorAll<HTMLElement>('.sn-post-item').forEach(post => {
        readObserver!.observe(post);
    });
}

function flushReadState(): void {
    if (readFlushTimeout) clearTimeout(readFlushTimeout);
    readFlushTimeout = setTimeout(() => {
        if (latestVisiblePostId && latestVisiblePostId !== lastReadPostId) {
            const discussionId = document.body.dataset.discussionId;
            if (discussionId && window.SnakkReadStateBatcher) {
                window.SnakkReadStateBatcher.updateReadState(discussionId, latestVisiblePostId);
            }
            lastReadPostId = latestVisiblePostId;
        }
    }, 1000);
}

function observeNewPosts(): void {
    if (!readObserver) return;
    document.querySelectorAll<HTMLElement>('.sn-post-item').forEach(post => {
        readObserver!.observe(post); // Already-observed elements are ignored
    });
}

// Initialize draft auto-save
function initDraftAutoSave(discussionId: string): void {
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
    if (!textarea || !(window as any).SnakkDraftManager) return;

    const getReplyToPostId = (): string | null => {
        const input = document.getElementById('reply-to-post-id') as HTMLInputElement;
        return input?.value || null;
    };

    // Restore draft â€” if the editor is active, sync the restored content into it
    (window as any).SnakkDraftManager?.restoreDraft(discussionId, textarea, getReplyToPostId());
    const editor = getReplyEditor();
    if (editor && textarea.value) {
        editor.setMarkdown(textarea.value);
    }

    // Start auto-save (DraftManager reads from the textarea, which the editor keeps synced)
    (window as any).SnakkDraftManager?.startAutoSave(discussionId, textarea, getReplyToPostId);

    // Draft clearing is handled in the main submit handler (initReplyEditor)
    // to ensure it runs before HTMX swaps the page content
}

// ===== Reactions System =====
let currentReactionPostId: string | null = null;
const reactionEmojis: Record<string, string> = {
    Agree: '👍', Love: '❤️', Funny: '😂',
    Thinking: '🤔', Watching: '👀', Fire: '🔥',
    Thanks: '🙏', MindBlown: '🤯', ShipIt: '🚀'
};
const reactionTypeValues: Record<string, number> = {
    Agree: 1, Love: 2, Funny: 3, Thinking: 4, Watching: 5,
    Fire: 6, Thanks: 7, MindBlown: 8, ShipIt: 9
};
let reactionPickerHideTimer: ReturnType<typeof setTimeout> | null = null;

// Touch devices: the picker opens on tap and closes via outside-tap
// (see document click listener below). Skip hover-based auto-hide timers
// so the picker doesn't vanish from synthesized mouse events on mobile.
const reactionPickerCoarseQuery = window.matchMedia('(hover: none)');

const smileyPlaceholderSvg = '<span class="sn-icon icon-badge-check sn-h-4 sn-w-4" aria-hidden="true"></span>';

// ===== Pending Reaction Persistence =====
// Survives page refresh until the Valkey-buffered count is flushed to the DB.
// Key: sn:rx:{postId}  Value: { typeDelta, snapshotTotal, expiresAt }

const RX_LS_PREFIX = 'sn:rx:';
const RX_TTL_MS = 10 * 60 * 1000; // 10 minutes

interface PendingReaction {
    typeDelta: Record<string, number>;
    snapshotTotal: number;
    expiresAt: number;
}

function rxGetPending(postId: string): PendingReaction | null {
    try {
        const raw = localStorage.getItem(RX_LS_PREFIX + postId);
        if (!raw) return null;
        const entry = JSON.parse(raw) as PendingReaction;
        if (Date.now() > entry.expiresAt) {
            localStorage.removeItem(RX_LS_PREFIX + postId);
            return null;
        }
        return entry;
    } catch {
        return null;
    }
}

function rxSetPending(postId: string, typeDelta: Record<string, number>, rawCounts: Record<string, number>): void {
    const snapshotTotal = Object.values(rawCounts).reduce((s, v) => s + v, 0);
    const entry: PendingReaction = { typeDelta, snapshotTotal, expiresAt: Date.now() + RX_TTL_MS };
    try { localStorage.setItem(RX_LS_PREFIX + postId, JSON.stringify(entry)); } catch { /* storage full */ }
}

function rxClearPending(postId: string): void {
    localStorage.removeItem(RX_LS_PREFIX + postId);
}

// Apply any pending delta on top of raw server counts.
// If the DB total has moved since we reacted, the pending entry is stale — clear it and trust the DB.
function rxApplyPending(postId: string, rawCounts: Record<string, number>): Record<string, number> {
    const pending = rxGetPending(postId);
    if (!pending) return rawCounts;
    const currentTotal = Object.values(rawCounts).reduce((s, v) => s + v, 0);
    if (currentTotal !== pending.snapshotTotal) {
        rxClearPending(postId);
        return rawCounts;
    }
    const result: Record<string, number> = { ...rawCounts };
    for (const [type, delta] of Object.entries(pending.typeDelta)) {
        const v = (result[type] ?? 0) + delta;
        if (v <= 0) delete result[type];
        else result[type] = v;
    }
    return result;
}

function rxCleanupExpired(): void {
    const toRemove: string[] = [];
    for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (!key?.startsWith(RX_LS_PREFIX)) continue;
        try {
            const entry = JSON.parse(localStorage.getItem(key)!) as PendingReaction;
            if (Date.now() > entry.expiresAt) toRemove.push(key);
        } catch { toRemove.push(key!); }
    }
    toRemove.forEach(k => localStorage.removeItem(k));
}

// Read reaction counts from JSON data-attribute
function getReactionCounts(el: HTMLElement): Record<string, number> {
    try {
        return JSON.parse(el.dataset.reactionCounts || '{}');
    } catch {
        return {};
    }
}

// Read user's own reactions from JSON data-attribute
function getMyReactions(el: HTMLElement): string[] {
    try {
        return JSON.parse(el.dataset.myReactions || '[]');
    } catch {
        return [];
    }
}

// Write reaction counts and user reactions to data-attributes
function setReactionData(el: HTMLElement, counts: Record<string, number>, myReactions: string[]): void {
    el.dataset.reactionCounts = JSON.stringify(counts);
    el.dataset.myReactions = JSON.stringify(myReactions);
}

// Update the icon(s) in a .sn-reaction-badge wrapper based on current reaction state
function updateReactionBadgeIcon(badge: HTMLElement, hasAny: boolean, myReactions: string[]): void {
    if (!badge.hasAttribute('data-action')) return;

    badge.querySelectorAll<HTMLElement>(':scope > .icon').forEach(el => el.remove());

    const countsDiv = badge.querySelector<HTMLElement>('[id^="reactions-"]');
    if (!countsDiv) return;

    const makeIcon = (name: string): HTMLSpanElement => {
        const span = document.createElement('span');
        span.className = `icon ${name} h-4 w-4`;
        span.setAttribute('aria-hidden', 'true');
        return span;
    };

    if (!hasAny) {
        badge.insertBefore(makeIcon('icon-plus-circle'), countsDiv);
        badge.insertBefore(makeIcon('icon-badge-check'), countsDiv);
    } else if (myReactions.length > 0) {
        badge.insertBefore(makeIcon('icon-refresh'), countsDiv);
    } else {
        badge.insertBefore(makeIcon('icon-plus-circle'), countsDiv);
    }
}

// Render reaction spans from data-attributes, applying any locally-pending delta on top
function renderReactionCounts(reactionsBar: HTMLElement): void {
    const postId = reactionsBar.id.replace('reactions-', '');
    const counts = rxApplyPending(postId, getReactionCounts(reactionsBar));
    const myReactions = getMyReactions(reactionsBar);
    let html = '';
    let hasAny = false;

    for (const [type, emoji] of Object.entries(reactionEmojis)) {
        const count = counts[type] || 0;
        if (count > 0) {
            const isActive = myReactions.includes(type);
            html += `<span data-type="${type}" class="${isActive ? 'sn-active' : ''}"><span class="sn-reaction-icon">${emoji}</span> ${count}</span>`;
            hasAny = true;
        }
    }

    const badge = reactionsBar.closest<HTMLElement>('.sn-reaction-badge');
    if (badge) {
        reactionsBar.innerHTML = html;
        updateReactionBadgeIcon(badge, hasAny, myReactions);
    } else {
        if (!hasAny) {
            html = `<span class="sn-reaction-placeholder" data-reaction-placeholder>${smileyPlaceholderSvg}</span>`;
        }
        reactionsBar.innerHTML = html;
    }
}

function hideReactionPicker(): void {
    // Restore any elements that were forced visible
    document.querySelectorAll<HTMLElement>('[data-actions-forced]').forEach(el => {
        el.classList.remove('sn-actions-forced');
        delete el.dataset.actionsForced;
    });

    const picker = document.getElementById('reaction-picker');
    if (picker) {
        picker.querySelectorAll<HTMLElement>('.sn-is-selected').forEach(btn => btn.classList.remove('sn-is-selected'));
        picker.classList.add('sn-hidden');
        picker.dataset.postId = '';
    }
    currentReactionPostId = null;
}

function setupReactionPickerHover(): void {
    const picker = document.getElementById('reaction-picker');
    if (!picker || picker.dataset.hoverBound) return;

    if (!reactionPickerCoarseQuery.matches) {
        picker.addEventListener('mouseenter', () => {
            if (reactionPickerHideTimer) {
                clearTimeout(reactionPickerHideTimer);
                reactionPickerHideTimer = null;
            }
        });

        picker.addEventListener('mouseleave', () => {
            reactionPickerHideTimer = setTimeout(hideReactionPicker, 300);
        });
    }

    // Handle clicks on reaction buttons inside the picker
    picker.addEventListener('click', (e) => {
        const btn = (e.target as HTMLElement).closest('[data-reaction-type]') as HTMLElement | null;
        if (!btn) return;
        e.preventDefault();
        e.stopPropagation();
        const postId = picker.dataset.postId || '';
        const reactionType = btn.dataset.reactionType || '';
        if (postId && reactionType) {
            toggleReaction(postId, reactionType);
        }
    });

    picker.dataset.hoverBound = 'true';
}

function toggleReactionPicker(postId: string, sourceEl?: HTMLElement): void {
    const picker = document.getElementById('reaction-picker');
    const reactionsBar = document.getElementById(`reactions-${postId}`);

    if (!picker || !reactionsBar) return;

    // Clear any pending hide timer
    if (reactionPickerHideTimer) {
        clearTimeout(reactionPickerHideTimer);
        reactionPickerHideTimer = null;
    }

    if (currentReactionPostId === postId && !picker.classList.contains('sn-hidden')) {
        hideReactionPicker();
        return;
    }

    currentReactionPostId = postId;
    picker.dataset.postId = postId;

    // Position relative to the clicked element (may be breadcrumb copy or original)
    const positionEl = sourceEl ?? reactionsBar;
    const rect = positionEl.getBoundingClientRect();
    picker.style.left = `${rect.left}px`;
    // picker is position:fixed, so use viewport-relative coordinates (no scrollY)
    picker.style.top = `${rect.bottom + 5}px`;

    // Force the smiley placeholder visible while picker is open
    const smileyPlaceholder = reactionsBar.querySelector('[data-reaction-placeholder]') as HTMLElement | null;
    if (smileyPlaceholder) {
        smileyPlaceholder.classList.add('sn-actions-forced');
        smileyPlaceholder.dataset.actionsForced = 'true';
    }

    // Start hide timer when mouse leaves the reactions area (mouse only)
    if (!reactionPickerCoarseQuery.matches) {
        reactionsBar.onmouseleave = () => {
            reactionPickerHideTimer = setTimeout(hideReactionPicker, 300);
        };
    }

    // Stamp the currently-selected reaction button
    const myReactions = getMyReactions(reactionsBar);
    picker.querySelectorAll<HTMLElement>('.sn-reaction-picker-btn').forEach(btn => {
        btn.classList.toggle('sn-is-selected', myReactions.includes(btn.dataset.reactionType || ''));
    });

    picker.classList.remove('sn-hidden');
    setupReactionPickerHover();
}

async function toggleReaction(postId: string, reactionType: string): Promise<void> {
    hideReactionPicker();

    if (!postId) {
        console.error('toggleReaction called with no postId');
        return;
    }

    const reactionsBar = document.getElementById(`reactions-${postId}`);
    if (!reactionsBar) return;

    // Raw server counts (kept in data-attribute; never includes our pending delta)
    const rawCounts = getReactionCounts(reactionsBar);
    const snapshotMyReactions = getMyReactions(reactionsBar);

    // Displayed counts = server counts + any existing pending delta
    const pendingCounts = rxApplyPending(postId, rawCounts);

    // Compute optimistic state from what the user currently sees
    const newCounts = { ...pendingCounts };
    const newMyReactions = [...snapshotMyReactions];
    const existingIndex = newMyReactions.indexOf(reactionType);

    if (existingIndex >= 0) {
        newCounts[reactionType] = Math.max(0, (newCounts[reactionType] || 0) - 1);
        newMyReactions.splice(existingIndex, 1);
    } else {
        if (newMyReactions.length > 0) {
            const oldType = newMyReactions[0] as string;
            newCounts[oldType] = Math.max(0, (newCounts[oldType] || 0) - 1);
            newMyReactions.splice(0, newMyReactions.length);
        }
        newCounts[reactionType] = (newCounts[reactionType] || 0) + 1;
        newMyReactions.push(reactionType);
    }

    // Persist: net delta vs the raw server baseline (what the DB will eventually have)
    const allTypes = new Set([...Object.keys(rawCounts), ...Object.keys(newCounts)]);
    const typeDelta: Record<string, number> = {};
    for (const type of allTypes) {
        const d = (newCounts[type] ?? 0) - (rawCounts[type] ?? 0);
        if (d !== 0) typeDelta[type] = d;
    }
    if (Object.keys(typeDelta).length > 0) {
        rxSetPending(postId, typeDelta, rawCounts);
    } else {
        rxClearPending(postId);
    }

    // Keep raw server data in the attribute; update myReactions optimistically.
    // renderReactionCounts re-applies the localStorage delta, so displayed counts stay correct.
    setReactionData(reactionsBar, rawCounts, newMyReactions);
    renderReactionCounts(reactionsBar);

    try {
        const response = await fetch(`/bff/posts/${postId}/reactions`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ type: reactionTypeValues[reactionType] }),
            credentials: 'include'
        });

        if (!response.ok) {
            rxClearPending(postId);
            setReactionData(reactionsBar, rawCounts, snapshotMyReactions);
            renderReactionCounts(reactionsBar);
            const errorText = await response.text();
            console.error('Failed to toggle reaction:', response.status, errorText);
            showToast('Failed to update reaction. Please try again.', 'error');
            return;
        }

        // Refresh myReactions from server (counts still stale; localStorage covers them)
        await loadReactionsForPost(postId);
    } catch (err) {
        rxClearPending(postId);
        setReactionData(reactionsBar, rawCounts, snapshotMyReactions);
        renderReactionCounts(reactionsBar);
        console.error('Error toggling reaction:', err);
        showToast('Network error. Please check your connection.', 'error');
    }
}

async function loadReactionsForPost(postId: string): Promise<void> {
    const reactionsBar = document.getElementById(`reactions-${postId}`);
    if (!reactionsBar) return;

    const isAuthenticated = document.body.dataset.isAuthenticated === 'true';

    try {
        const [countsResponse, myResponse] = await Promise.all([
            fetch(`/bff/posts/${postId}/reactions`),
            isAuthenticated ? fetch(`/bff/posts/${postId}/reactions/me`) : Promise.resolve(null)
        ]);

        if (!countsResponse.ok) throw new Error(`HTTP ${countsResponse.status}`);
        const countsData: ReactionCountsResponse = await countsResponse.json();

        let myReactions: string[] = [];
        if (myResponse !== null) {
            if (!myResponse.ok) throw new Error(`HTTP ${myResponse.status}`);
            const myData: MyReactionsResponse = await myResponse.json();
            myReactions = myData.reactions || [];
        }

        setReactionData(reactionsBar, countsData.counts || {}, myReactions);
        renderReactionCounts(reactionsBar);
    } catch (err) {
        console.error('Error loading reactions:', err);
    }
}

// Load reactions for all posts on page load
function loadAllReactions(): void {
    document.querySelectorAll('[id^="reactions-"]').forEach(bar => {
        const postId = bar.id.replace('reactions-', '');
        loadReactionsForPost(postId);
    });
}

// ===== Follow Discussion =====
async function toggleFollowDiscussion(discussionId: string): Promise<void> {
    const btn = document.getElementById('follow-btn') ?? document.querySelector<HTMLElement>('.rp-subscribe-btn');

    if (!btn) return;

    // Optimistic UI update - toggle immediately
    const currentlyFollowing = btn.classList.contains('sn-btn-primary');
    const newFollowingState = !currentlyFollowing;
    updateFollowButton(newFollowingState);

    // Update cache optimistically
    const followCache = (window as any).SnakkFollowCache;
    if (followCache) {
        followCache.setDiscussionFollowed(discussionId, newFollowingState);
    }

    try {
        const response = await fetch(`/bff/discussions/${discussionId}/follow`, {
            method: 'POST',
            credentials: 'include'
        });

        if (!response.ok) {
            // Revert optimistic update on error
            updateFollowButton(currentlyFollowing);
            const cache = (window as any).SnakkFollowCache;
            if (cache) {
                cache.setDiscussionFollowed(discussionId, currentlyFollowing);
            }
            console.error('Failed to toggle follow');
            showToast('Failed to update follow status. Please try again.', 'error');
            return;
        }

        const result: FollowStatus = await response.json();
        // Update to actual server state (should match optimistic update)
        updateFollowButton(result.isFollowing);
        const cache = (window as any).SnakkFollowCache;
        if (cache) {
            cache.setDiscussionFollowed(discussionId, result.isFollowing);
        }
    } catch (err) {
        // Revert optimistic update on error
        updateFollowButton(currentlyFollowing);
        const cache = (window as any).SnakkFollowCache;
        if (cache) {
            cache.setDiscussionFollowed(discussionId, currentlyFollowing);
        }
        console.error('Error toggling follow:', err);
        showToast('Network error. Please check your connection.', 'error');
    }
}

function updateFollowButton(isFollowing: boolean): void {
    const btn = document.getElementById('follow-btn');
    const text = document.getElementById('follow-text');
    const icon = document.getElementById('follow-icon');

    if (btn && text && icon) {
        if (isFollowing) {
            btn.classList.add('sn-btn-primary');
            btn.classList.remove('sn-btn-ghost');
            text.textContent = 'Followed';
            icon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />';
        } else {
            btn.classList.remove('sn-btn-primary');
            btn.classList.add('sn-btn-ghost');
            text.textContent = 'Follow';
            icon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />';
        }
    }

    (window as any).SnakkUtils.updateSubscribeButton('.rp-subscribe-btn', isFollowing);
}

async function loadFollowStatus(discussionId: string): Promise<void> {
    // Check cache first
    const cache = (window as any).SnakkFollowCache;
    if (cache) {
        const cached = cache.isDiscussionFollowed(discussionId);
        if (cached !== null) {
            updateFollowButton(cached);
            return; // Use cached value, skip API call
        }
    }

    // Cache miss or not available, fetch from API
    try {
        const response = await fetch(`/bff/discussions/${discussionId}/follow-status`, { credentials: 'include' });
        if (!response.ok) return;
        const result: FollowStatus = await response.json();
        updateFollowButton(result.isFollowing);

        // Update cache
        if (cache) {
            cache.setDiscussionFollowed(discussionId, result.isFollowing);
        }
    } catch (err) {
        // Not logged in or error - leave as default
    }
}

// ===== Mute Discussion =====
function toggleMuteDiscussion(discussionId: string): void {
    const mutedDiscussions = JSON.parse(localStorage.getItem('mutedDiscussions') || '[]') as string[];
    const isMuted = mutedDiscussions.includes(discussionId);

    if (isMuted) {
        // Unmute
        const index = mutedDiscussions.indexOf(discussionId);
        mutedDiscussions.splice(index, 1);
        localStorage.setItem('mutedDiscussions', JSON.stringify(mutedDiscussions));
        updateMuteButton(false);
    } else {
        // Mute
        mutedDiscussions.push(discussionId);
        localStorage.setItem('mutedDiscussions', JSON.stringify(mutedDiscussions));
        updateMuteButton(true);

        // Show confirmation
        const banner = document.createElement('div');
        banner.className = 'sn-fixed sn-top-20 sn-left-1/2 sn-transform sn--translate-x-1/2 sn-bg-base-100 sn-border sn-border-subtle sn-px-4 sn-py-3 sn-rounded-lg sn-shadow-lg sn-z-50';
        banner.innerHTML = `
            <div class="sn-flex sn-items-center sn-gap-2">
                <span class="sn-icon icon-volume-off sn-h-5 sn-w-5 sn-text-muted" aria-hidden="true"></span>
                <p class="sn-text-sm">Discussion muted. You won't see it in your feed.</p>
            </div>
        `;
        document.body.appendChild(banner);
        setTimeout(() => banner.remove(), 3000);
    }
}

function updateMuteButton(isMuted: boolean): void {
    const text = document.getElementById('mute-text');

    if (text) {
        text.textContent = isMuted ? 'Unmute discussion' : 'Mute discussion';
    }
}

function loadMuteStatus(discussionId: string): void {
    const mutedDiscussions = JSON.parse(localStorage.getItem('mutedDiscussions') || '[]') as string[];
    const isMuted = mutedDiscussions.includes(discussionId);
    updateMuteButton(isMuted);
}

// ===== Expand Post Modal =====
function expandPost(postId: string): void {
    document.querySelector('.sn-post-expand-modal')?.remove();
    document.querySelector('.sn-post-expand-backdrop')?.remove();
    document.body.style.overflow = '';

    const contentEl = document.getElementById(`post-content-${postId}`);
    if (!contentEl) return;

    const article = contentEl.closest('article') as HTMLElement | null;
    const authorName = contentEl.dataset.authorName || '';
    const timeEl = article?.querySelector<HTMLTimeElement>('time');
    const timeText = timeEl?.textContent?.trim() || '';
    const header = authorName
        ? escapeHtml(authorName) + (timeText ? ` Â· ${escapeHtml(timeText)}` : '')
        : escapeHtml(timeText);

    const contentClone = contentEl.cloneNode(true) as HTMLElement;
    contentClone.removeAttribute('id');

    const backdrop = document.createElement('div');
    backdrop.className = 'sn-post-expand-backdrop';

    const modal = document.createElement('div');
    modal.className = 'sn-post-expand-modal';
    modal.setAttribute('role', 'dialog');
    modal.setAttribute('aria-modal', 'true');
    modal.innerHTML = `
        <div class="sn-post-expand-header">
            <span class="sn-post-expand-author">${header}</span>
            <button type="button" class="sn-subtle-btn" aria-label="Close">
                <span class="sn-icon icon-x sn-h-4 sn-w-4" aria-hidden="true"></span>
            </button>
        </div>
        <div class="sn-post-expand-body"></div>
    `;
    modal.querySelector('.sn-post-expand-body')!.appendChild(contentClone);

    function close(): void {
        modal.remove();
        backdrop.remove();
        document.body.style.overflow = '';
        document.removeEventListener('keydown', escHandler);
    }

    const escHandler = (e: KeyboardEvent) => { if (e.key === 'Escape') close(); };
    backdrop.addEventListener('click', close);
    modal.querySelector('button[aria-label="Close"]')!.addEventListener('click', close);
    document.addEventListener('keydown', escHandler);

    document.body.style.overflow = 'hidden';
    document.body.appendChild(backdrop);
    document.body.appendChild(modal);
}

// ===== Follow User =====
async function followUser(userId: string, userName: string): Promise<void> {
    if (!userId) return;
    try {
        const response = await fetch(`/bff/users/${userId}/follow`, {
            method: 'POST',
            credentials: 'include'
        });
        if (!response.ok) {
            showToast('Failed to follow user. Please try again.', 'error');
            return;
        }
        const data: { isFollowing: boolean } = await response.json();
        const label = data.isFollowing ? `Now following ${escapeHtml(userName)}` : `Unfollowed ${escapeHtml(userName)}`;
        showToast(label, 'success');
    } catch {
        showToast('Network error. Please try again.', 'error');
    }
}

// ===== Hide Posts From User =====
function hidePostsFromUser(userId: string, userName: string): void {
    const hiddenUsers = JSON.parse(localStorage.getItem('hiddenUsers') || '[]') as string[];

    if (!hiddenUsers.includes(userId)) {
        hiddenUsers.push(userId);
        localStorage.setItem('hiddenUsers', JSON.stringify(hiddenUsers));

        // Hide all posts from this user
        document.querySelectorAll<HTMLElement>(`[data-author-id="${userId}"]`).forEach(post => {
            post.style.display = 'none';
        });

        // Show confirmation
        const banner = document.createElement('div');
        banner.className = 'sn-fixed sn-top-20 sn-left-1/2 sn-transform sn--translate-x-1/2 sn-bg-base-100 sn-border sn-border-subtle sn-px-4 sn-py-3 sn-rounded-lg sn-shadow-lg sn-z-50';
        banner.innerHTML = `
            <div class="sn-flex sn-items-center sn-gap-3">
                <span class="sn-icon icon-eye-slash sn-h-5 sn-w-5 sn-text-muted" aria-hidden="true"></span>
                <div>
                    <p class="sn-text-sm sn-font-medium">Posts from ${escapeHtml(userName)} are now hidden</p>
                    <button data-action="unhide-user" data-author-id="${userId}" class="sn-text-xs sn-text-primary sn-underline">Undo</button>
                </div>
            </div>
        `;
        document.body.appendChild(banner);
        setTimeout(() => banner.remove(), 5000);
    }
}

function unhideUser(userId: string): void {
    const hiddenUsers = JSON.parse(localStorage.getItem('hiddenUsers') || '[]') as string[];
    const index = hiddenUsers.indexOf(userId);

    if (index > -1) {
        hiddenUsers.splice(index, 1);
        localStorage.setItem('hiddenUsers', JSON.stringify(hiddenUsers));

        // Show all posts from this user
        document.querySelectorAll<HTMLElement>(`[data-author-id="${userId}"]`).forEach(post => {
            post.style.display = '';
        });

        // Remove any notification banners
        document.querySelectorAll('.sn-fixed.sn-top-20').forEach(banner => banner.remove());
    }
}

function applyHiddenUsers(): void {
    const hiddenUsers = JSON.parse(localStorage.getItem('hiddenUsers') || '[]') as string[];

    hiddenUsers.forEach(userId => {
        document.querySelectorAll<HTMLElement>(`[data-author-id="${userId}"]`).forEach(post => {
            post.style.display = 'none';
        });
    });
}

// ===== Typing Indicator =====
// State-based: indicator is shown while the reply field has content.
// 3-minute inactivity guard clears it if the user walks away mid-composition.
let composingReply = false;
let composingInactivityTimeout: ReturnType<typeof setTimeout> | null = null;
const COMPOSING_INACTIVITY_MS = 3 * 60 * 1000;

function onReplyContentChanged(): void {
    const realtime = (window as any).SnakkRealtime;
    if (!realtime || !discussionConfig?.discussionId) return;
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement | null;
    const hasContent = (textarea?.value ?? '').trim().length > 0;

    if (hasContent) {
        if (!composingReply) {
            composingReply = true;
            realtime.startTyping(discussionConfig.discussionId);
        }
        if (composingInactivityTimeout) clearTimeout(composingInactivityTimeout);
        composingInactivityTimeout = setTimeout(() => {
            composingReply = false;
            composingInactivityTimeout = null;
            realtime.stopTyping(discussionConfig!.discussionId);
        }, COMPOSING_INACTIVITY_MS);
    } else {
        clearComposingState();
    }
}

function clearComposingState(): void {
    if (composingInactivityTimeout) { clearTimeout(composingInactivityTimeout); composingInactivityTimeout = null; }
    if (!composingReply) return;
    composingReply = false;
    const realtime = (window as any).SnakkRealtime;
    if (realtime && discussionConfig?.discussionId)
        realtime.stopTyping(discussionConfig.discussionId);
}

// ===== Keyboard Navigation =====
let currentPostIndex = -1;
const posts: HTMLElement[] = [];

function initKeyboardNavigation(): void {
    // Build posts array for navigation
    document.querySelectorAll<HTMLElement>('.sn-post-article').forEach(post => {
        posts.push(post);
    });

    document.addEventListener('keydown', (e) => {
        // Don't intercept if user is typing in an input/textarea/contenteditable
        const target = e.target as HTMLElement;
        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable) {
            return;
        }

        // Don't intercept browser shortcuts (Ctrl+R, Ctrl+J, etc.)
        if (e.ctrlKey || e.altKey || e.metaKey) {
            return;
        }

        // Don't intercept if modals/pickers are open
        const picker = document.getElementById('reaction-picker');
        if (picker && !picker.classList.contains('sn-hidden')) {
            if (e.key === 'Escape') {
                hideReactionPicker();
            }
            return;
        }

        switch(e.key) {
            case 'j': // Next post
            case 'ArrowDown':
                e.preventDefault();
                navigateToPost(currentPostIndex + 1);
                break;
            case 'k': // Previous post
            case 'ArrowUp':
                e.preventDefault();
                navigateToPost(currentPostIndex - 1);
                break;
            case 'r': // Reply to current post or focus composer
                e.preventDefault();
                const composer = document.getElementById('comment-input') as HTMLElement | null;
                if (composer) {
                    composer.focus();
                    composer.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
                break;
            case 'Escape': // Clear selection/close things
                if (currentPostIndex >= 0) {
                    posts[currentPostIndex]?.classList.remove('sn-keyboard-selected');
                    currentPostIndex = -1;
                }
                break;
        }
    });
}

function navigateToPost(index: number): void {
    // Clear previous selection
    if (currentPostIndex >= 0) {
        posts[currentPostIndex]?.classList.remove('sn-keyboard-selected');
    }

    // Clamp index
    if (index < 0) index = 0;
    if (index >= posts.length) index = posts.length - 1;

    currentPostIndex = index;

    const currentPost = posts[currentPostIndex];
    if (currentPost) {
        currentPost.classList.add('sn-keyboard-selected');
        currentPost.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

// ===== Toast Notifications =====
// showToast is available globally as window.SnakkUtils.showToast
const showToast = (message: string, type: 'error' | 'success' | 'info' = 'error', duration: number = 4000): void =>
    (window as any).SnakkUtils.showToast(message, type, duration);

// ===== IAmA Q&A post insertion =====
// For IAmA discussions, insert a post adjacent to its paired partner (if known)
// rather than simply appending before the sentinel.
function insertIamaPost(article: HTMLElement, container: HTMLElement, sentinel: HTMLElement): void {
    const publicId = article.dataset.publicId ?? '';
    const questionId = iamaAnswerToQuestion[publicId];
    if (questionId) {
        const questionEl = container.querySelector<HTMLElement>(`article[data-public-id="${questionId}"]`);
        if (questionEl) {
            questionEl.insertAdjacentElement('afterend', article);
            return;
        }
    }
    sentinel.before(article);
    const answerId = iamaOfficialAnswers[publicId];
    if (answerId) {
        const answerEl = container.querySelector<HTMLElement>(`article[data-public-id="${answerId}"]`);
        if (answerEl) {
            article.insertAdjacentElement('afterend', answerEl);
        }
    }
}

// ===== Endless Scroll for Posts =====
let postsCurrentOffset = 0;
let postsHasMoreItems = false;
let postsIsLoading = false;
const postsPageSize = 20;
let postsScrollObserver: IntersectionObserver | null = null;
// Tracks the publicId of the last rendered post author across chunk boundaries.
// Stored here rather than read from the DOM so it survives DOM windowing prunes.
let lastRenderedAuthorId: string | null = null;

// Load-up (earlier posts) state
let postsStartOffset = 0;
let postsIsLoadingEarlier = false;
let loadUpObserver: IntersectionObserver | null = null;

// Fragment tracking state
let fragmentRafId: number | null = null;
let suppressFragmentUpdate = false;

// Thread nav state
let totalPostCount = 0;

// ===== DOM Windowing =====
interface ChunkRecord { offset: number; el: HTMLDivElement; height: number; }
const MAX_DOM_CHUNKS = 5;
const CHUNK_CACHE_MAX = 10;
let chunksInDom: ChunkRecord[] = [];
const chunkCache = new Map<number, Post[]>();
let topSpacerObserver: IntersectionObserver | null = null;
let bottomSpacerObserver: IntersectionObserver | null = null;

const scrollDbg = {
    get on(): boolean { return !!(window as any).__snakkScrollDebug || localStorage.getItem('snakk:scroll:debug') === '1'; },
    log(msg: string, data?: Record<string, unknown>) {
        if (!this.on) return;
        const s = 'color:#6ee7b7;font-weight:bold';
        if (data) console.log(`%c[Scroll] ${msg}`, s, data);
        else console.log(`%c[Scroll] ${msg}`, s);
    },
    state() {
        if (!this.on) return;
        console.log('%c[Scroll] state', 'color:#6ee7b7;font-weight:bold', {
            chunks: chunksInDom.map(c => `off=${c.offset} h=${Math.round(c.height)}px`),
            cached: [...chunkCache.keys()],
            postsCurrentOffset,
            postsStartOffset,
            postsHasMoreItems,
            topSpacer: topSpacerEl() ? { h: topSpacerEl()!.style.height, endOff: topSpacerEl()!.dataset.endOffset } : null,
            bottomSpacer: bottomSpacerEl() ? { h: bottomSpacerEl()!.style.height, startOff: bottomSpacerEl()!.dataset.startOffset } : null,
        });
    },
};

// Exposed on window so devs can run snakkScrollDbg.enable() in console
(window as any).snakkScrollDbg = {
    enable: () => { (window as any).__snakkScrollDebug = true; console.log('%c[Scroll] debug ON', 'color:#6ee7b7;font-weight:bold'); },
    disable: () => { (window as any).__snakkScrollDebug = false; console.log('%c[Scroll] debug OFF', 'color:#6ee7b7'); },
    state: () => scrollDbg.state(),
    cache: () => console.table([...chunkCache.entries()].map(([k, v]) => ({ offset: k, posts: v.length }))),
};

function topSpacerEl(): HTMLDivElement | null { return document.getElementById('top-spacer') as HTMLDivElement | null; }
function bottomSpacerEl(): HTMLDivElement | null { return document.getElementById('bottom-spacer') as HTMLDivElement | null; }
function spacerPx(el: HTMLElement | null): number { return parseFloat(el?.style.height ?? '0') || 0; }

function storeInCache(offset: number, posts: Post[]): void {
    chunkCache.set(offset, posts);
    while (chunkCache.size > CHUNK_CACHE_MAX) {
        chunkCache.delete(chunkCache.keys().next().value!);
    }
    scrollDbg.log('cached chunk', { offset, posts: posts.length, cacheSize: chunkCache.size });
}

function pruneTopChunk(): void {
    const chunk = chunksInDom.shift();
    if (!chunk) return;
    // Cache was pre-populated at fetch time; SSR chunk (offset 0) falls back to server on recovery
    const spacer = topSpacerEl();
    if (spacer) {
        spacer.style.height = (spacerPx(spacer) + chunk.height) + 'px';
        spacer.dataset.endOffset = String(chunk.offset + postsPageSize);
    }
    scrollDbg.log('prune top', { offset: chunk.offset, height: chunk.height, topSpacerH: spacer?.style.height });
    chunk.el.remove();
    // overflow-anchor:auto on #posts-container compensates scroll automatically
}

function pruneBottomChunk(): void {
    const chunk = chunksInDom.pop();
    if (!chunk) return;
    const spacer = bottomSpacerEl();
    if (spacer) {
        spacer.style.height = (spacerPx(spacer) + chunk.height) + 'px';
        spacer.dataset.startOffset = String(chunk.offset);
    }
    scrollDbg.log('prune bottom', { offset: chunk.offset, height: chunk.height, bottomSpacerH: spacer?.style.height });
    chunk.el.remove();
    // Content was below viewport — no scroll compensation needed
}

function scheduleAppendChunkRecord(offset: number, el: HTMLDivElement): void {
    requestAnimationFrame(() => {
        const height = el.getBoundingClientRect().height;
        chunksInDom.push({ offset, el, height });
        scrollDbg.log('append chunk', { offset, height, total: chunksInDom.length });
        while (chunksInDom.length > MAX_DOM_CHUNKS) pruneTopChunk();
        scrollDbg.state();
    });
}

// Returns the last rendered post article in `el` (or container if not specified)
function lastPostIn(el: HTMLElement): HTMLElement | null {
    const posts = el.querySelectorAll<HTMLElement>('.sn-post-article[data-author-id]');
    return posts.length > 0 ? (posts[posts.length - 1] ?? null) : null;
}

// Returns the first rendered post article in `el`
function firstPostIn(el: HTMLElement): HTMLElement | null {
    return el.querySelector<HTMLElement>('.sn-post-article[data-author-id]');
}

// Re-evaluates sn-same-author / sn-new-author on `next` given what came before it.
// Called after any prepend or append that creates a new chunk boundary.
function fixAuthorBoundary(prev: HTMLElement | null, next: HTMLElement | null): void {
    if (!prev || !next) return;
    const same = prev.dataset.authorId === next.dataset.authorId;
    next.classList.toggle('sn-same-author', same);
    next.classList.toggle('sn-new-author', !same);
    scrollDbg.log('fix author boundary', { prev: prev.dataset.authorId, next: next.dataset.authorId, same });
}

function resetChunkState(): void {
    chunksInDom = [];
    chunkCache.clear();
    topSpacerObserver?.disconnect(); topSpacerObserver = null;
    bottomSpacerObserver?.disconnect(); bottomSpacerObserver = null;
    const ts = topSpacerEl();
    if (ts) { ts.style.height = '0'; ts.dataset.endOffset = '0'; }
    const bs = bottomSpacerEl();
    if (bs) { bs.style.height = '0'; bs.dataset.startOffset = '0'; }
    scrollDbg.log('chunk state reset');
}

function initPostsEndlessScroll(): void {
    const sentinel = document.getElementById('scroll-sentinel');
    if (!sentinel) return;

    // Disconnect previous observer if it exists
    if (postsScrollObserver) {
        postsScrollObserver.disconnect();
    }

    postsScrollObserver = new IntersectionObserver((entries) => {
        const entry = entries[0];
        if (!entry?.isIntersecting) return;
        if (postsHasMoreItems && !postsIsLoading) {
            const discussionId = document.body.dataset.discussionId || '';
            const currentUserId = document.body.dataset.currentUserId || '';
            const isAuthenticated = document.body.dataset.isAuthenticated === 'true';
            const isLocked = document.body.dataset.isLocked === 'true';
            loadMorePosts(discussionId, currentUserId, isAuthenticated, isLocked);
        } else if (!postsHasMoreItems) {
            document.getElementById('end-message')?.classList.remove('sn-hidden');
        }
    }, { rootMargin: '100px' });

    postsScrollObserver.observe(sentinel);
}

async function loadMorePosts(discussionId: string, currentUserId: string, isAuthenticated: boolean, isLocked: boolean): Promise<void> {
    if (postsIsLoading || !postsHasMoreItems) return;
    postsIsLoading = true;

    const loadingIndicator = document.getElementById('loading-indicator');
    const endMessage = document.getElementById('end-message');
    loadingIndicator?.classList.remove('sn-hidden');


    try {
        const response = await fetch(
            `/bff/discussions/${discussionId}/posts?offset=${postsCurrentOffset}&pageSize=${postsPageSize}&discussionType=${encodeURIComponent(discussionConfig?.discussionType ?? '')}`,
            { credentials: 'include' }
        );

        if (!response.ok) throw new Error('Failed to load posts');

        const data: { items?: Post[]; hasMoreItems: boolean; hasCodeBlocks?: boolean } = await response.json();
        const container = document.getElementById('posts-container');
        const sentinel = document.getElementById('scroll-sentinel');

        if (!container || !sentinel) return;

        if (data.items && data.items.length > 0) {
            // Use the module-level variable (not a DOM query) so the boundary is correct
            // even after DOM windowing has pruned earlier chunks from the container.
            let previousAuthorId: string | null = lastRenderedAuthorId;
            let previousCreatedAt: string | null = lastPostIn(container)?.dataset.createdAt ?? null;

            const isIama = discussionConfig?.discussionType === 'Iama';
            scrollDbg.log('fetch down', { offset: postsCurrentOffset, count: data.items.length, hasMore: data.hasMoreItems, isIama });

            if (isIama) {
                // IAmA reorders posts across the full container — incompatible with chunk windowing.
                data.items.forEach((post, idx) => {
                    if (post.isNecro && previousCreatedAt) {
                        container.insertBefore(createNecroSeparator(previousCreatedAt, post.createdAt), sentinel);
                    }
                    const isSameAuthor = previousAuthorId === post.author.publicId;
                    const isLast = !data.hasMoreItems && idx === data.items!.length - 1;
                    const postElement = createPostElement(post, post.isNecro ? false : isSameAuthor, currentUserId, isAuthenticated, isLocked, isLast, discussionConfig?.isModerator ?? false);
                    insertIamaPost(postElement, container, sentinel);
                    previousAuthorId = post.author.publicId;
                    lastRenderedAuthorId = post.author.publicId;
                    previousCreatedAt = post.createdAt;
                    loadReactionsForPost(post.publicId);
                });
            } else {
                storeInCache(postsCurrentOffset, data.items);
                const chunkEl = document.createElement('div') as HTMLDivElement;
                chunkEl.dataset.chunkOffset = String(postsCurrentOffset);
                data.items.forEach((post, idx) => {
                    if (post.isNecro && previousCreatedAt) {
                        chunkEl.appendChild(createNecroSeparator(previousCreatedAt, post.createdAt));
                    }
                    const isSameAuthor = previousAuthorId === post.author.publicId;
                    const isLast = !data.hasMoreItems && idx === data.items!.length - 1;
                    const postElement = createPostElement(post, post.isNecro ? false : isSameAuthor, currentUserId, isAuthenticated, isLocked, isLast, discussionConfig?.isModerator ?? false);
                    chunkEl.appendChild(postElement);
                    previousAuthorId = post.author.publicId;
                    lastRenderedAuthorId = post.author.publicId;
                    previousCreatedAt = post.createdAt;
                    loadReactionsForPost(post.publicId);
                });
                // Insert before bottom spacer (or sentinel if spacer absent) so spacer stays last
                (bottomSpacerEl() ?? sentinel).before(chunkEl);
                scheduleAppendChunkRecord(postsCurrentOffset, chunkEl);
            }

            postsCurrentOffset += data.items.length;

            // Highlight code blocks in new posts if present
            console.log('[DiscussionDetail] loadMorePosts (down) â€” hasCodeBlocks:', data.hasCodeBlocks);
            if (data.hasCodeBlocks && (window as any).SnakkSyntax) {
                (window as any).SnakkSyntax.highlightAll(container, 'loadMorePosts:down');
            }

            // Observe new posts for read tracking
            observeNewPosts();
            // Re-position separator in case the last-read post just loaded
            insertUnreadSeparator();
        }

        postsHasMoreItems = data.hasMoreItems;

        if (!postsHasMoreItems) {
            endMessage?.classList.remove('sn-hidden');
            // Disconnect the observer - no more posts to load
            if (postsScrollObserver) {
                postsScrollObserver.disconnect();
                postsScrollObserver = null;
            }
        }
    } catch (err) {
        console.error('Failed to load more posts:', err);
        // Show error message with retry button
        const errorMessage = document.getElementById('load-error-message');
        errorMessage?.classList.remove('sn-hidden');
        // Disconnect observer but don't set hasMoreItems to false (allow retry)
        if (postsScrollObserver) {
            postsScrollObserver.disconnect();
            postsScrollObserver = null;
        }
    } finally {
        loadingIndicator?.classList.add('sn-hidden');
        postsIsLoading = false;
    }
}

function retryLoadPosts(discussionId: string, currentUserId: string, isAuthenticated: boolean, isLocked: boolean): void {
    const errorMessage = document.getElementById('load-error-message');
    errorMessage?.classList.add('sn-hidden');
    initPostsEndlessScroll();
    loadMorePosts(discussionId, currentUserId, isAuthenticated, isLocked);
}

const formatPostRelativeTime = (dateString: string): string => (window as any).SnakkUtils.formatRelativeTime(dateString);

function formatTimeBetween(dateA: string, dateB: string): string {
    const a = new Date(dateA).getTime();
    const b = new Date(dateB).getTime();
    const diffMs = Math.abs(b - a);
    const days = Math.floor(diffMs / 86400000);

    if (days < 1) return 'less than a day later';
    if (days === 1) return '1 day later';
    if (days < 7) return `${days} days later`;

    const weeks = Math.floor(days / 7);
    if (days < 30) return weeks === 1 ? '1 week later' : `${weeks} weeks later`;

    const months = Math.floor(days / 30);
    if (days < 365) return months === 1 ? '1 month later' : `${months} months later`;

    const years = Math.floor(days / 365);
    const remainingMonths = Math.floor((days % 365) / 30);
    if (remainingMonths === 0) return years === 1 ? '1 year later' : `${years} years later`;
    return years === 1
        ? `1 year, ${remainingMonths} month${remainingMonths > 1 ? 's' : ''} later`
        : `${years} years, ${remainingMonths} month${remainingMonths > 1 ? 's' : ''} later`;
}

function createNecroSeparator(previousCreatedAt: string, necroCreatedAt: string): HTMLElement {
    const label = formatTimeBetween(previousCreatedAt, necroCreatedAt);
    const el = document.createElement('ul');
    el.className = 'sn-timeline sn-timeline-vertical sn-my-4';
    el.innerHTML = `
        <li>
            <hr class="bg-base-content/20" />
            <div class="sn-timeline-middle">
                <span class="sn-icon icon-clock-solid sn-w-4 sn-h-4 text-base-content/40" aria-hidden="true"></span>
            </div>
            <div class="sn-timeline-end sn-text-xs sn-font-semibold text-base-content/50 sn-uppercase sn-tracking-wide">${escapeHtml(label)}</div>
            <hr class="bg-base-content/20" />
        </li>`;
    return el;
}

function createPostElement(post: Post, isSameAuthorAsPrevious: boolean, currentUserId: string, isAuthenticated: boolean, isLocked: boolean, _isLastPost: boolean = false, isModerator: boolean = false): HTMLElement {
    const article = document.createElement('article');
    article.id = `post-${post.postNumber}`;
    article.dataset.createdAt = post.createdAt;
    const authorClass = isSameAuthorAsPrevious ? 'sn-same-author' : 'sn-new-author';
    article.className = `sn-post-item sn-post-article sn-post-layout group ${post.isFirstPost ? 'sn-first-post' : ''} ${authorClass}`;
    article.dataset.authorId = post.author.publicId;
    article.dataset.postId = post.publicId;
    article.dataset.publicId = post.publicId;
    article.dataset.postNumber = String(post.postNumber);

    const isOP = post.isFirstPost;
    const hasReplyTo = post.replyTo != null;
    const isOwner = isAuthenticated && currentUserId === post.author.publicId;

    // Build left pane (skip for first post â€” author is in header)
    let authorPaneHtml = '';
    if (!isOP) {
        authorPaneHtml = '<aside class="sn-post-author-pane">';
        if (!isSameAuthorAsPrevious) {
            if (post.author.isDeleted) {
                authorPaneHtml += `
                    <div class="sn-post-avatar sn-post-avatar-deleted">
                        <span class="sn-icon icon-user sn-h-5 sn-w-5" aria-hidden="true"></span>
                    </div>
                    <span class="sn-post-author-name sn-deleted">${escapeHtml(post.author.displayName)}</span>`;
            } else {
                authorPaneHtml += `
                    <img src="${post.author.avatarThumbnailUrl || post.author.avatarUrl || ''}" alt="${escapeHtml(post.author.displayName)}"
                         width="48" height="48" class="sn-post-avatar" loading="lazy" />
                    <a href="/u/${encodeUlid(post.author.publicId)}" class="sn-post-author-name"
                       data-popup-type="user" data-popup-id="${post.author.publicId}"
                       data-popup-name="${escapeHtml(post.author.displayName)}">${escapeHtml(post.author.displayName)}</a>`;
            }
            // Badges
            let badges = '';
            if (post.author.role === 'admin') {
                badges += '<span class="sn-badge sn-badge-error sn-badge-xs">Admin</span>';
            } else if (post.author.role === 'mod') {
                badges += '<span class="sn-badge sn-badge-info sn-badge-xs">Mod</span>';
            }
            if (post.isOp) {
                badges += '<span class="sn-badge sn-badge-primary sn-badge-xs sn-post-badge-op sn-shadow-layered">OP</span>';
            }
            if (post.isUsersFirstPostInSpace) {
                badges += '<span class="sn-badge sn-badge-success sn-badge-xs sn-post-badge-new sn-shadow-layered">New</span>';
            }
            if (post.isMilestone) {
                badges += '<span class="sn-badge sn-badge-warning sn-badge-xs sn-post-badge-milestone sn-shadow-layered" title="Milestone post">\u2605</span>';
            }
            authorPaneHtml += `<div class="sn-post-author-badges">${badges}</div>`;
            if (isAuthenticated && !post.author.isDeleted && !isOwner) {
                const authorOptsId = `sn-author-opts-${post.publicId}`;
                const banBtn = isModerator
                    ? `<li><button type="button" data-action="mod-ban-user" data-author-id="${post.author.publicId}" data-author-name="${escapeHtml(post.author.displayName)}" class="sn-text-sm sn-text-error"><span class="icon icon-ban sn-h-4 sn-w-4" aria-hidden="true"></span> Ban user</button></li>`
                    : '';
                authorPaneHtml += `
                    <button type="button"
                            class="sn-post-author-opts sn-subtle-btn"
                            popovertarget="${authorOptsId}"
                            aria-label="Author options">
                        <span class="icon icon-dots-horizontal sn-h-4 sn-w-4" aria-hidden="true"></span>
                    </button>
                    <ul id="${authorOptsId}" popover class="sn-dropdown-panel sn-menu sn-w-48">
                        <li><button type="button" data-action="follow-user" data-author-id="${post.author.publicId}" data-author-name="${escapeHtml(post.author.displayName)}" class="sn-text-sm"><span class="icon icon-user-follow sn-h-4 sn-w-4" aria-hidden="true"></span> Follow user</button></li>
                        <li><button type="button" data-action="hide-posts-from-user" data-author-id="${post.author.publicId}" data-author-name="${escapeHtml(post.author.displayName)}" class="sn-text-sm"><span class="icon icon-eye-slash sn-h-4 sn-w-4" aria-hidden="true"></span> Hide posts from user</button></li>
                        ${banBtn}
                    </ul>`;
            }
        }
        authorPaneHtml += '</aside>';
    }

    // Inline post action icons (revealed when ··· toggle is clicked)
    let inlineActionsHtml = '';
    if (isAuthenticated) {
        let historyBtn = '';
        if (isOwner || isModerator) {
            historyBtn = `
                <button hx-get="/bff/posts/${post.publicId}/history"
                        hx-target="#history-modal-content"
                        hx-swap="innerHTML"
                        data-modal-open="history_modal"
                        class="sn-btn sn-btn-outline sn-btn-sm sn-post-action-btn"
                        title="History"
                        aria-label="History">
                    <span class="icon icon-clock sn-h-4 sn-w-4" aria-hidden="true"></span>
                    History
                </button>`;
        }
        let reportBtn = '';
        if (!isOwner && !post.author.isDeleted) {
            reportBtn = `
                <button type="button"
                        data-action="open-report-modal"
                        data-report-type="post"
                        data-report-id="${post.publicId}"
                        data-report-label="this post"
                        class="sn-btn sn-btn-outline sn-btn-sm sn-post-action-btn"
                        title="Report post"
                        aria-label="Report post">
                    <span class="icon icon-exclamation-triangle sn-h-4 sn-w-4" aria-hidden="true"></span>
                    Report post
                </button>`;
        }
        let removeBtn = '';
        if (isModerator && !post.author.isDeleted) {
            removeBtn = `
                <button type="button"
                        data-action="mod-delete-post"
                        data-post-id="${post.publicId}"
                        data-post-number="${post.postNumber}"
                        class="sn-btn sn-btn-outline sn-btn-sm sn-post-action-btn sn-text-error"
                        title="Remove post"
                        aria-label="Remove post">
                    <span class="icon icon-trash sn-h-4 sn-w-4" aria-hidden="true"></span>
                    Remove post
                </button>`;
        }
        inlineActionsHtml = `
            <div class="sn-post-inline-actions" hidden>
                ${historyBtn}
                <button type="button"
                        data-action="toggle-save-post"
                        data-post-id="${post.publicId}"
                        data-saved="false"
                        aria-pressed="false"
                        class="sn-btn sn-btn-outline sn-btn-sm sn-post-action-btn sn-post-save-btn"
                        title="Save post"
                        aria-label="Save post">
                    <span class="icon icon-bookmark-alt sn-h-4 sn-w-4" aria-hidden="true"></span>
                    Save post
                </button>
                ${reportBtn}
                ${removeBtn}
            </div>
            <button type="button"
                    class="sn-subtle-btn sn-post-opts-toggle"
                    data-action="toggle-post-inline-actions"
                    aria-label="Post options">
                <span class="icon icon-dots-horizontal sn-h-4 sn-w-4" aria-hidden="true"></span>
            </button>`;
    }

    const canReact = !isLocked && isAuthenticated;
    const smileyPlaceholderHtml = canReact
        ? `<span class="sn-reaction-placeholder" data-reaction-placeholder>${smileyPlaceholderSvg}</span>`
        : '';
    const rxData = (post as any).reactions ?? {};
    const rxCountsAttr = JSON.stringify(rxData.counts ?? {}).replace(/"/g, '&quot;');
    const rxMyAttr = JSON.stringify(rxData.userReactions ?? []).replace(/"/g, '&quot;');
    const reactionsContainerHtml = `<div class="sn-flex sn-items-center sn-gap-2 sn-text-base sn-text-muted${canReact ? ' sn-cursor-pointer' : ''}" id="reactions-${post.publicId}" data-reaction-counts="${rxCountsAttr}" data-my-reactions="${rxMyAttr}"${canReact ? ` data-action="toggle-reaction-picker" data-post-id="${post.publicId}"` : ''} aria-label="${canReact ? 'Add reaction to post' : 'Reactions'}">${smileyPlaceholderHtml}</div>`;

    // Build toolbar
    const editedTag = post.editedAt ? '<span>(edited)</span>' : '';

    // Inline author (shown on mobile, hidden on desktop via CSS; skip for first post)
    let inlineAuthorHtml = '';
    if (!isOP) {
        if (isSameAuthorAsPrevious) {
            // Same-author: inline author is hidden by default, shown on mobile
            if (post.author.isDeleted) {
                inlineAuthorHtml = `<span class="sn-post-author-inline sn-hidden"><span class="sn-deleted">${escapeHtml(post.author.displayName)}</span></span>`;
            } else {
                inlineAuthorHtml = `<span class="sn-post-author-inline sn-hidden"><a href="/u/${encodeUlid(post.author.publicId)}" data-popup-type="user" data-popup-id="${post.author.publicId}" data-popup-name="${escapeHtml(post.author.displayName)}">${escapeHtml(post.author.displayName)}</a></span>`;
            }
        } else {
            // New author: inline author shown on mobile (CSS controls visibility)
            if (post.author.isDeleted) {
                inlineAuthorHtml = `<span class="sn-post-author-inline"><span class="sn-deleted">${escapeHtml(post.author.displayName)}</span></span>`;
            } else {
                inlineAuthorHtml = `<span class="sn-post-author-inline"><a href="/u/${encodeUlid(post.author.publicId)}" data-popup-type="user" data-popup-id="${post.author.publicId}" data-popup-name="${escapeHtml(post.author.displayName)}">${escapeHtml(post.author.displayName)}</a></span>`;
            }
        }
    }

    let replyToHtml = '';
    if (hasReplyTo && post.replyTo) {
        replyToHtml = `
            <a href="#post-${post.replyTo.postId}" class="sn-editorial-quote sn-block sn-mb-4 sn-text-sm" data-action="highlight-post" data-post-id="${post.replyTo.postId}">
                <span class="sn-quote-author">${escapeHtml(post.replyTo.authorName)} wrote:</span>
                <p class="sn-line-clamp-2 sn-mt-1">${escapeHtml(post.replyTo.contentSnippet)}</p>
            </a>`;
    }

    article.innerHTML = `
        ${authorPaneHtml}
        <div class="sn-post-main">
            <div class="sn-post-toolbar">
                <div class="sn-post-toolbar-left">
                    ${inlineAuthorHtml}
                    <span class="sn-post-time"><time data-timestamp="${post.createdAt}">${formatPostRelativeTime(post.createdAt)}</time>${editedTag}</span>
                    <span aria-hidden="true">·</span>
                    <span class="sn-post-number">#${post.postNumber}</span>
                </div>
                <div class="sn-post-toolbar-right">
                    ${inlineActionsHtml}
                    ${reactionsContainerHtml}
                </div>
            </div>
            ${replyToHtml}
            <div id="post-content-${post.publicId}" class="sn-prose sn-prose-content" data-author-name="${escapeHtml(post.author.displayName)}" data-raw-content="${escapeHtml(post.content)}">
                ${post.renderedContent ? sanitizeHtml(post.renderedContent) : escapeHtml(post.content)}
            </div>
        </div>
    `;

    // Wire up Popover API positioning for the author options panel
    const authorOptsPanel = article.querySelector<HTMLElement>(`[id^="sn-author-opts-"]`);
    if (authorOptsPanel) {
        authorOptsPanel.addEventListener('beforetoggle', (e: Event) => {
            if ((e as ToggleEvent).newState === 'open') {
                (window as any).snakkDropdown?.position(authorOptsPanel);
            }
        });
    }

    // Process htmx attributes on dynamically added elements
    if (typeof (window as any).htmx !== 'undefined') {
        (window as any).htmx.process(article);
    }

    return article;
}

// ===== Report System =====
let reportReasons: ReportReason[] = [];

async function loadReportReasons(spaceId?: string): Promise<void> {
    try {
        let url = `/bff/moderation/reports/reasons`;
        if (spaceId) {
            url += `?spaceId=${spaceId}`;
        }

        const response = await fetch(url, { credentials: 'include' });
        if (!response.ok) throw new Error('Failed to load reasons');

        const data: { items?: ReportReason[] } = await response.json();
        reportReasons = data.items || [];

        // Populate the select dropdown
        const select = document.getElementById('report-reason') as HTMLSelectElement | null;
        if (select) {
            select.innerHTML = '<option value="">Select a reason...</option>';
            reportReasons.forEach(reason => {
                const option = document.createElement('option');
                option.value = reason.publicId;
                option.textContent = reason.name;
                option.dataset.description = reason.description || '';
                select.appendChild(option);
            });
        }
    } catch (err) {
        console.error('Error loading report reasons:', err);
    }
}

function openReportModal(type: string, targetId: string, description: string, spaceId?: string): void {
    // Reset the form
    const form = document.getElementById('report-form') as HTMLFormElement | null;
    form?.reset();
    document.getElementById('report-error')?.classList.add('sn-hidden');
    const submitBtn = document.getElementById('report-submit-btn') as HTMLButtonElement | null;
    if (submitBtn) submitBtn.disabled = false;
    document.getElementById('report-submit-text')?.classList.remove('sn-hidden');
    document.getElementById('report-submit-loading')?.classList.add('sn-hidden');
    document.getElementById('report-reason-description')?.classList.add('sn-hidden');

    // Set the target info
    const typeInput = document.getElementById('report-type') as HTMLInputElement | null;
    const targetIdInput = document.getElementById('report-target-id') as HTMLInputElement | null;
    const descEl = document.getElementById('report-target-description');

    if (typeInput) typeInput.value = type;
    if (targetIdInput) targetIdInput.value = targetId;
    if (descEl) descEl.textContent = description;

    // Load reasons if not already loaded
    if (reportReasons.length === 0) {
        loadReportReasons(spaceId);
    }

    // Show the modal
    const modal = document.getElementById('report_modal') as any;
    modal?.showModal?.();
}

async function submitReport(event: Event): Promise<void> {
    event.preventDefault();

    const typeInput = document.getElementById('report-type') as HTMLInputElement | null;
    const targetIdInput = document.getElementById('report-target-id') as HTMLInputElement | null;
    const reasonSelect = document.getElementById('report-reason') as HTMLSelectElement | null;
    const detailsTextarea = document.getElementById('report-details') as HTMLTextAreaElement | null;

    const type = typeInput?.value;
    const targetId = targetIdInput?.value;
    const reasonId = reasonSelect?.value;
    const details = detailsTextarea?.value;

    if (!reasonId) {
        showReportError('Please select a reason for your report.');
        return;
    }

    // Show loading state
    const submitBtn = document.getElementById('report-submit-btn') as HTMLButtonElement | null;
    const submitText = document.getElementById('report-submit-text');
    const submitLoading = document.getElementById('report-submit-loading');

    if (submitBtn) submitBtn.disabled = true;
    submitText?.classList.add('sn-hidden');
    submitLoading?.classList.remove('sn-hidden');

    try {
        const requestBody: any = {
            reasonId: reasonId,
            details: details || null
        };

        // Set the appropriate ID based on type
        if (type === 'post') {
            requestBody.postId = targetId;
        } else if (type === 'discussion') {
            requestBody.discussionId = targetId;
        } else if (type === 'user') {
            requestBody.userId = targetId;
        }

        const response = await fetch(`/bff/moderation/reports`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestBody),
            credentials: 'include'
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({}));
            throw new Error(errorData.message || 'Failed to submit report');
        }

        document.getElementById('report-error')?.classList.add('sn-hidden');
        if (submitBtn) submitBtn.disabled = true;

        // Close the modal after a delay
        setTimeout(() => {
            const modal = document.getElementById('report_modal') as any;
            modal?.close?.();
        }, 2000);

    } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'An error occurred while submitting your report. Please try again.';
        console.error('Error submitting report:', err);
        showReportError(errorMessage);

        if (submitBtn) submitBtn.disabled = false;
        submitText?.classList.remove('sn-hidden');
        submitLoading?.classList.add('sn-hidden');
    }
}

function showReportError(message: string): void {
    const errorDiv = document.getElementById('report-error');
    const errorMessage = document.getElementById('report-error-message');
    if (errorMessage) errorMessage.textContent = message;
    errorDiv?.classList.remove('sn-hidden');
}

// ===== Fragment-Based Navigation =====

function initFragmentTracking(): void {
    window.addEventListener('scroll', onScrollUpdateFragment, { passive: true });
}

function onScrollUpdateFragment(): void {
    if (fragmentRafId !== null || suppressFragmentUpdate) return;
    fragmentRafId = requestAnimationFrame(() => {
        fragmentRafId = null;
        const posts = document.querySelectorAll<HTMLElement>('.sn-post-item[data-post-number]');
        if (!posts.length) return;

        // Find the last post whose top is at or above the sticky-header threshold
        let topPost: HTMLElement | null = null;
        for (const post of posts) {
            if (post.getBoundingClientRect().top <= 80) {
                topPost = post;
            } else {
                break;
            }
        }
        if (!topPost) topPost = posts[0] ?? null;
        if (!topPost) return;

        const postNumber = parseInt(topPost.dataset.postNumber || '0', 10);
        const newHash = postNumber <= 1 ? '' : `#post-${postNumber}`;
        if ((window.location.hash || '') !== newHash) {
            history.replaceState(null, '', location.pathname + newHash);
        }
    });
}

async function handleFragmentEntry(
    discussionId: string,
    currentUserId: string,
    isAuthenticated: boolean,
    isLocked: boolean,
    targetPost?: number
): Promise<void> {
    let postNumber: number;
    if (targetPost !== undefined) {
        postNumber = targetPost;
    } else {
        const hash = window.location.hash;
        if (!hash || !hash.startsWith('#post-')) return;
        postNumber = parseInt(hash.slice(6), 10); // '#post-'.length === 6
        if (isNaN(postNumber) || postNumber <= 1) return;
    }

    // Post already in DOM (SSR rendered it) â€” just scroll
    const existingEl = document.getElementById(`post-${postNumber}`);
    if (existingEl) {
        suppressFragmentUpdate = true;
        existingEl.scrollIntoView({ behavior: 'instant', block: 'start' });
        setTimeout(() => { suppressFragmentUpdate = false; }, 200);
        return;
    }

    // Need to load the page containing this post
    const targetOffset = Math.floor((postNumber - 1) / postsPageSize) * postsPageSize;
    const container = document.getElementById('posts-container');
    const scrollSentinel = document.getElementById('scroll-sentinel');
    if (!container || !scrollSentinel) return;

    // Block scroll-down observer and fragment-update listener for the entire load.
    // Must be set before the DOM clear: removing posts collapses scrollHeight to ~innerHeight,
    // which triggers the bottom-of-page shortcut in getCurrentPostNumber() and would corrupt
    // displayedPostNumber if the flag were set any later.
    postsIsLoading = true;
    suppressFragmentUpdate = true;

    // Reset load-up state from any previous navigation so a stale observer/sentinel
    // doesn't fire with an incorrect postsStartOffset.
    loadUpObserver?.disconnect();
    loadUpObserver = null;
    document.getElementById('load-up-sentinel')?.classList.add('sn-hidden');

    // Clear server-rendered posts and chunk wrappers, reset DOM windowing state
    Array.from(container.querySelectorAll('[data-chunk-offset]')).forEach(c => c.remove());
    Array.from(container.querySelectorAll('.sn-post-item')).forEach(p => p.remove());
    resetChunkState();

    const loadingIndicator = document.getElementById('loading-indicator');
    loadingIndicator?.classList.remove('sn-hidden');

    postsStartOffset = targetOffset;
    postsCurrentOffset = targetOffset;

    try {
        const response = await fetch(
            `/bff/discussions/${discussionId}/posts?offset=${targetOffset}&pageSize=${postsPageSize}&discussionType=${encodeURIComponent(discussionConfig?.discussionType ?? '')}`,
            { credentials: 'include' }
        );
        if (!response.ok) throw new Error('Failed to load posts for fragment');

        const data: { items?: Post[]; hasMoreItems: boolean; hasCodeBlocks?: boolean } = await response.json();

        if (data.items && data.items.length > 0) {
            scrollDbg.log('fetch fragment', { targetOffset, count: data.items.length, hasMore: data.hasMoreItems });
            storeInCache(targetOffset, data.items);
            const isIama = discussionConfig?.discussionType === 'Iama';
            let previousAuthorId: string | null = null;
            let previousCreatedAt: string | null = null;

            if (isIama) {
                data.items.forEach((post, idx) => {
                    if (post.isNecro && previousCreatedAt) {
                        container.insertBefore(createNecroSeparator(previousCreatedAt, post.createdAt), scrollSentinel);
                    }
                    const isSameAuthor = previousAuthorId === post.author.publicId;
                    const isLast = !data.hasMoreItems && idx === data.items!.length - 1;
                    const el = createPostElement(post, post.isNecro ? false : isSameAuthor, currentUserId, isAuthenticated, isLocked, isLast, discussionConfig?.isModerator ?? false);
                    insertIamaPost(el, container, scrollSentinel);
                    previousAuthorId = post.author.publicId;
                    previousCreatedAt = post.createdAt;
                });
            } else {
                const chunkEl = document.createElement('div') as HTMLDivElement;
                chunkEl.dataset.chunkOffset = String(targetOffset);
                data.items.forEach((post, idx) => {
                    if (post.isNecro && previousCreatedAt) {
                        chunkEl.appendChild(createNecroSeparator(previousCreatedAt, post.createdAt));
                    }
                    const isSameAuthor = previousAuthorId === post.author.publicId;
                    const isLast = !data.hasMoreItems && idx === data.items!.length - 1;
                    const el = createPostElement(post, post.isNecro ? false : isSameAuthor, currentUserId, isAuthenticated, isLocked, isLast, discussionConfig?.isModerator ?? false);
                    chunkEl.appendChild(el);
                    previousAuthorId = post.author.publicId;
                    previousCreatedAt = post.createdAt;
                });
                (bottomSpacerEl() ?? scrollSentinel).before(chunkEl);
                scheduleAppendChunkRecord(targetOffset, chunkEl);
            }

            postsCurrentOffset = targetOffset + data.items.length;

            console.log('[DiscussionDetail] loadMorePosts (up) â€” hasCodeBlocks:', data.hasCodeBlocks);
            if (data.hasCodeBlocks && (window as any).SnakkSyntax) {
                (window as any).SnakkSyntax.highlightAll(container, 'loadMorePosts:up');
            }
            observeNewPosts();
            data.items.forEach(post => loadReactionsForPost(post.publicId));
        }

        postsHasMoreItems = data.hasMoreItems;
        if (!postsHasMoreItems) {
            document.getElementById('end-message')?.classList.remove('sn-hidden');
        }

        // Show load-up sentinel and start watching for upward scroll
        if (postsStartOffset > 0) {
            document.getElementById('load-up-sentinel')?.classList.remove('sn-hidden');
            initLoadUpObserver(discussionId, currentUserId, isAuthenticated, isLocked);
        }

        // Scroll to target post, then re-enable the endless-scroll observer only after
        // the animation settles â€” prevents the sentinel from firing mid-flight and
        // racing the chunk navigation.
        const targetEl = document.getElementById(`post-${postNumber}`);
        if (targetEl) {
            requestAnimationFrame(() => {
                targetEl.scrollIntoView({ behavior: 'smooth', block: 'start' });
                const afterScroll = () => {
                    suppressFragmentUpdate = false;
                    if (postsHasMoreItems) initPostsEndlessScroll();
                };
                if ('onscrollend' in window) {
                    window.addEventListener('scrollend' as keyof WindowEventMap, afterScroll, { once: true });
                } else {
                    setTimeout(afterScroll, 600);
                }
            });
        } else {
            suppressFragmentUpdate = false;
            if (postsHasMoreItems) initPostsEndlessScroll();
        }
    } catch (err) {
        console.error('Failed to load posts for fragment:', err);
        suppressFragmentUpdate = false;
        if (postsHasMoreItems) initPostsEndlessScroll();
    } finally {
        loadingIndicator?.classList.add('sn-hidden');
        postsIsLoading = false;
    }
}

function initLoadUpObserver(
    discussionId: string,
    currentUserId: string,
    isAuthenticated: boolean,
    isLocked: boolean
): void {
    const sentinel = document.getElementById('load-up-sentinel');
    if (!sentinel) return;

    loadUpObserver?.disconnect();
    loadUpObserver = new IntersectionObserver((entries) => {
        if (entries[0]?.isIntersecting && postsStartOffset > 0 && !postsIsLoadingEarlier) {
            loadEarlierPosts(discussionId, currentUserId, isAuthenticated, isLocked);
        }
    }, { rootMargin: '200px' });
    loadUpObserver.observe(sentinel);
}

async function loadEarlierPosts(
    discussionId: string,
    currentUserId: string,
    isAuthenticated: boolean,
    isLocked: boolean
): Promise<void> {
    if (postsIsLoadingEarlier || postsStartOffset <= 0) return;
    postsIsLoadingEarlier = true;

    const loadOffset = Math.max(0, postsStartOffset - postsPageSize);
    const indicator = document.getElementById('load-up-indicator');
    indicator?.classList.remove('sn-hidden');

    try {
        const response = await fetch(
            `/bff/discussions/${discussionId}/posts?offset=${loadOffset}&pageSize=${postsPageSize}&discussionType=${encodeURIComponent(discussionConfig?.discussionType ?? '')}`,
            { credentials: 'include' }
        );
        if (!response.ok) throw new Error('Failed to load earlier posts');

        const data: { items?: Post[]; hasMoreItems: boolean; hasCodeBlocks?: boolean } = await response.json();

        if (data.items && data.items.length > 0) {
            const container = document.getElementById('posts-container');
            if (!container) return;

            // Scroll anchor + boundary reference: first post currently in DOM becomes the second chunk's first post after prepend
            const firstCurrentPost = firstPostIn(container);
            const anchorTop = firstCurrentPost?.getBoundingClientRect().top ?? 0;

            scrollDbg.log('fetch up (fragment)', { offset: loadOffset, count: data.items.length });
            storeInCache(loadOffset, data.items);

            const chunkEl = document.createElement('div') as HTMLDivElement;
            chunkEl.dataset.chunkOffset = String(loadOffset);
            let previousAuthorId: string | null = null;
            let previousCreatedAt: string | null = null;
            data.items.forEach(post => {
                if (post.isNecro && previousCreatedAt) {
                    chunkEl.appendChild(createNecroSeparator(previousCreatedAt, post.createdAt));
                }
                const isSameAuthor = previousAuthorId === post.author.publicId;
                const el = createPostElement(post, post.isNecro ? false : isSameAuthor, currentUserId, isAuthenticated, isLocked, false, discussionConfig?.isModerator ?? false);
                chunkEl.appendChild(el);
                previousAuthorId = post.author.publicId;
                previousCreatedAt = post.createdAt;
            });

            // Insert after top spacer (which sits between load-up-sentinel and first chunk)
            const ts = topSpacerEl();
            if (ts) {
                ts.after(chunkEl);
            } else {
                const loadUpSentinel = document.getElementById('load-up-sentinel');
                (loadUpSentinel?.nextSibling ? container.insertBefore(chunkEl, loadUpSentinel.nextSibling as ChildNode) : container.prepend(chunkEl));
            }

            const height = chunkEl.getBoundingClientRect().height;
            chunksInDom.unshift({ offset: loadOffset, el: chunkEl, height });
            scrollDbg.log('prepend chunk (fragment)', { offset: loadOffset, height, total: chunksInDom.length });
            while (chunksInDom.length > MAX_DOM_CHUNKS) pruneBottomChunk();

            // Restore scroll position to cancel the layout shift from prepended content
            if (firstCurrentPost) {
                window.scrollBy(0, firstCurrentPost.getBoundingClientRect().top - anchorTop);
            }

            // Fix author-grouping at the boundary: new chunk's last post → existing first post
            fixAuthorBoundary(lastPostIn(chunkEl), firstCurrentPost);

            postsStartOffset = loadOffset;
            scrollDbg.state();

            console.log('[DiscussionDetail] loadMorePosts (fragment) â€” hasCodeBlocks:', data.hasCodeBlocks);
            if (data.hasCodeBlocks && (window as any).SnakkSyntax) {
                (window as any).SnakkSyntax.highlightAll(container, 'loadMorePosts:fragment');
            }
            observeNewPosts();
            data.items.forEach(post => loadReactionsForPost(post.publicId));
        }

        // Hide sentinel once we've loaded all the way back to the beginning
        if (postsStartOffset <= 0) {
            document.getElementById('load-up-sentinel')?.classList.add('sn-hidden');
            loadUpObserver?.disconnect();
            loadUpObserver = null;
        }
    } catch (err) {
        console.error('Failed to load earlier posts:', err);
    } finally {
        postsIsLoadingEarlier = false;
        indicator?.classList.add('sn-hidden');
    }
}

// ===== Spacer Observer (DOM-window recovery) =====

function initSpacerObservers(): void {
    topSpacerObserver?.disconnect();
    bottomSpacerObserver?.disconnect();

    const tSpacer = topSpacerEl();
    if (tSpacer) {
        topSpacerObserver = new IntersectionObserver((entries) => {
            if (!entries[0]?.isIntersecting) return;
            if (postsIsLoadingEarlier) return;
            const endOffset = parseInt(tSpacer.dataset.endOffset ?? '0');
            if (endOffset <= 0 || spacerPx(tSpacer) <= 0) return;
            recoverTopChunk(endOffset);
        }, { rootMargin: '200px' });
        topSpacerObserver.observe(tSpacer);
    }

    const bSpacer = bottomSpacerEl();
    if (bSpacer) {
        bottomSpacerObserver = new IntersectionObserver((entries) => {
            if (!entries[0]?.isIntersecting) return;
            if (postsIsLoading) return;
            const startOffset = parseInt(bSpacer.dataset.startOffset ?? '0');
            if (spacerPx(bSpacer) <= 0) return;
            recoverBottomChunk(startOffset);
        }, { rootMargin: '100px' });
        bottomSpacerObserver.observe(bSpacer);
    }
}

async function recoverTopChunk(endOffset: number): Promise<void> {
    postsIsLoadingEarlier = true;
    const targetOffset = Math.max(0, endOffset - postsPageSize);
    const fromCache = chunkCache.has(targetOffset);
    scrollDbg.log('recover top', { targetOffset, fromCache });

    const discussionId = document.body.dataset.discussionId || '';
    const currentUserId = document.body.dataset.currentUserId || '';
    const isAuthenticated = document.body.dataset.isAuthenticated === 'true';
    const isLocked = document.body.dataset.isLocked === 'true';

    let posts: Post[];
    const cached = chunkCache.get(targetOffset);
    if (cached) {
        posts = cached;
    } else {
        try {
            const r = await fetch(
                `/bff/discussions/${discussionId}/posts?offset=${targetOffset}&pageSize=${postsPageSize}&discussionType=${encodeURIComponent(discussionConfig?.discussionType ?? '')}`,
                { credentials: 'include' }
            );
            if (!r.ok) throw new Error('Failed to recover top chunk');
            const d: { items?: Post[] } = await r.json();
            posts = d.items ?? [];
        } catch (err) {
            console.error('[Scroll] Failed to recover top chunk:', err);
            postsIsLoadingEarlier = false;
            return;
        }
    }

    storeInCache(targetOffset, posts);

    const container = document.getElementById('posts-container');
    const tSpacer = topSpacerEl();
    if (!container || !tSpacer) { postsIsLoadingEarlier = false; return; }

    // Capture pre-insert anchor + first post of what will become the second chunk
    const existingFirstPost = firstPostIn(container);
    const anchorTop = existingFirstPost?.getBoundingClientRect().top ?? 0;

    const chunkEl = document.createElement('div') as HTMLDivElement;
    chunkEl.dataset.chunkOffset = String(targetOffset);
    let prevAuthorId: string | null = null;
    let prevCreatedAt: string | null = null;
    posts.forEach(post => {
        if (post.isNecro && prevCreatedAt) chunkEl.appendChild(createNecroSeparator(prevCreatedAt, post.createdAt));
        const el = createPostElement(post, post.isNecro ? false : prevAuthorId === post.author.publicId, currentUserId, isAuthenticated, isLocked, false, discussionConfig?.isModerator ?? false);
        chunkEl.appendChild(el);
        prevAuthorId = post.author.publicId;
        prevCreatedAt = post.createdAt;
    });

    tSpacer.after(chunkEl);

    requestAnimationFrame(() => {
        const height = chunkEl.getBoundingClientRect().height;
        const newSpacerH = Math.max(0, spacerPx(tSpacer) - height);
        tSpacer.style.height = newSpacerH + 'px';
        tSpacer.dataset.endOffset = String(targetOffset);
        chunksInDom.unshift({ offset: targetOffset, el: chunkEl, height });
        scrollDbg.log('recover top done', { targetOffset, height, newSpacerH, total: chunksInDom.length });

        if (existingFirstPost) window.scrollBy(0, existingFirstPost.getBoundingClientRect().top - anchorTop);

        // Fix author-grouping at the new chunk boundary
        fixAuthorBoundary(lastPostIn(chunkEl), existingFirstPost);

        while (chunksInDom.length > MAX_DOM_CHUNKS) pruneBottomChunk();

        postsIsLoadingEarlier = false;
        observeNewPosts();
        posts.forEach(p => loadReactionsForPost(p.publicId));
        if ((window as any).SnakkSyntax) (window as any).SnakkSyntax.highlightAll(chunkEl, 'recover-top');
        scrollDbg.state();
    });
}

async function recoverBottomChunk(startOffset: number): Promise<void> {
    postsIsLoading = true;
    const fromCache = chunkCache.has(startOffset);
    scrollDbg.log('recover bottom', { startOffset, fromCache });

    const discussionId = document.body.dataset.discussionId || '';
    const currentUserId = document.body.dataset.currentUserId || '';
    const isAuthenticated = document.body.dataset.isAuthenticated === 'true';
    const isLocked = document.body.dataset.isLocked === 'true';

    let posts: Post[];
    const cached = chunkCache.get(startOffset);
    if (cached) {
        posts = cached;
    } else {
        try {
            const r = await fetch(
                `/bff/discussions/${discussionId}/posts?offset=${startOffset}&pageSize=${postsPageSize}&discussionType=${encodeURIComponent(discussionConfig?.discussionType ?? '')}`,
                { credentials: 'include' }
            );
            if (!r.ok) throw new Error('Failed to recover bottom chunk');
            const d: { items?: Post[] } = await r.json();
            posts = d.items ?? [];
        } catch (err) {
            console.error('[Scroll] Failed to recover bottom chunk:', err);
            postsIsLoading = false;
            return;
        }
    }

    storeInCache(startOffset, posts);

    const bSpacer = bottomSpacerEl();
    if (!bSpacer) { postsIsLoading = false; return; }

    const container = document.getElementById('posts-container');
    // Seed author context from last in-DOM post so the boundary between the existing
    // last chunk and this recovered chunk gets the correct sn-same-author class.
    const lastExisting = container ? lastPostIn(container) : null;

    const chunkEl = document.createElement('div') as HTMLDivElement;
    chunkEl.dataset.chunkOffset = String(startOffset);
    let prevAuthorId: string | null = lastExisting?.dataset.authorId ?? null;
    let prevCreatedAt: string | null = lastExisting?.dataset.createdAt ?? null;
    posts.forEach(post => {
        if (post.isNecro && prevCreatedAt) chunkEl.appendChild(createNecroSeparator(prevCreatedAt, post.createdAt));
        const el = createPostElement(post, post.isNecro ? false : prevAuthorId === post.author.publicId, currentUserId, isAuthenticated, isLocked, false, discussionConfig?.isModerator ?? false);
        chunkEl.appendChild(el);
        prevAuthorId = post.author.publicId;
        prevCreatedAt = post.createdAt;
    });

    bSpacer.before(chunkEl);

    requestAnimationFrame(() => {
        const height = chunkEl.getBoundingClientRect().height;
        const newSpacerH = Math.max(0, spacerPx(bSpacer) - height);
        const newStartOffset = startOffset + postsPageSize;
        bSpacer.style.height = newSpacerH + 'px';
        bSpacer.dataset.startOffset = String(newStartOffset);
        chunksInDom.push({ offset: startOffset, el: chunkEl, height });
        scrollDbg.log('recover bottom done', { startOffset, height, newSpacerH, newStartOffset, total: chunksInDom.length });

        while (chunksInDom.length > MAX_DOM_CHUNKS) pruneTopChunk();

        postsIsLoading = false;
        observeNewPosts();
        posts.forEach(p => loadReactionsForPost(p.publicId));
        if ((window as any).SnakkSyntax) (window as any).SnakkSyntax.highlightAll(chunkEl, 'recover-bottom');
        scrollDbg.state();
    });
}

// ===== Thread Navigation Bar =====

function initThreadNav(config: DiscussionConfig): void {
    const pane = document.getElementById('thread-nav');
    if (!pane) return;

    totalPostCount = config.postCount || 0;
    if (totalPostCount <= 1 || !config.postsHasMoreItems) {
        pane.classList.add('sn-hidden');
        return;
    }

    pane.classList.remove('sn-hidden');

    const input = document.getElementById('thread-nav-input') as HTMLInputElement | null;
    const totalEl = document.getElementById('thread-nav-total');
    const centerCol = document.querySelector('.sn-center') as HTMLElement | null;
    let progressFill: HTMLElement | null = null;
    if (centerCol) {
        // Remove previous progress bar if any (HTMX navigation)
        document.querySelector('.sn-thread-progress-bar')?.remove();

        const bar = document.createElement('div');
        bar.className = 'sn-thread-progress-bar';
        const fill = document.createElement('div');
        fill.className = 'sn-thread-progress-fill';
        bar.appendChild(fill);
        document.body.appendChild(bar);
        progressFill = fill;

        function updateProgressPosition(): void {
            const rect = centerCol!.getBoundingClientRect();
            bar.style.left = `${rect.left}px`;
            bar.style.width = `${rect.width}px`;
        }
        updateProgressPosition();
        window.addEventListener('resize', updateProgressPosition, { passive: true });
    }

    if (totalEl) totalEl.textContent = String(totalPostCount);

    function getCurrentPostNumber(): number {
        // At the bottom of the page the browser can't scroll the last post to block:start,
        // so report the total count rather than the topmost-visible post.
        if ((window.innerHeight + window.scrollY) >= document.documentElement.scrollHeight - 10) {
            return totalPostCount;
        }
        // .sn-post-article is present on both SSR posts (class: post-item) and
        // dynamically-loaded posts (class: sn-post-item) — use it so the counter
        // works from post 1, not just from the first endless-scroll chunk.
        const posts = document.querySelectorAll<HTMLElement>('.sn-post-article[data-post-number]');
        if (!posts.length) return 1;
        // Default to the first loaded post's number — avoids returning 1 when no post
        // has scrolled above the sticky header threshold (e.g. after a fragment load).
        let current = parseInt(posts[0]?.dataset.postNumber || '1', 10);
        for (const post of posts) {
            if (post.getBoundingClientRect().top <= 80) {
                current = parseInt(post.dataset.postNumber || '1', 10);
            } else {
                break;
            }
        }
        return current;
    }

    let displayedPostNumber = 1;

    function updateNav(postNumber: number): void {
        const n = Math.max(1, Math.min(postNumber, totalPostCount));
        displayedPostNumber = n;
        if (input && document.activeElement !== input) {
            input.value = String(n);
        }
        if (progressFill) {
            progressFill.style.width = `${(n / totalPostCount) * 100}%`;
        }
    }

    // Sync nav with scroll
    window.addEventListener('scroll', () => {
        if (suppressFragmentUpdate) return;
        requestAnimationFrame(() => {
            updateNav(getCurrentPostNumber());
        });
    }, { passive: true });

    // Initial state
    const hash = window.location.hash;
    const initialPost = hash?.startsWith('#post-') ? parseInt(hash.slice(6), 10) : 1;
    updateNav(isNaN(initialPost) ? 1 : initialPost);

    // Navigation buttons
    pane.addEventListener('click', (e) => {
        const btn = (e.target as HTMLElement).closest<HTMLElement>('[data-nav]');
        if (!btn) return;
        const action = btn.dataset.nav!;
        let target: number;
        if (action === 'first') target = 1;
        else if (action === 'prev') target = Math.max(1, displayedPostNumber - postsPageSize);
        else if (action === 'next') target = Math.min(totalPostCount, displayedPostNumber + postsPageSize);
        else if (action === 'last') target = totalPostCount;
        else return;

        updateNav(target);
        navigateToPostNumber(target, config);
    });

    // Input: jump to post on Enter or blur
    if (input) {
        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                const n = parseInt(input.value, 10);
                if (!isNaN(n)) navigateToPostNumber(n, config);
                input.blur();
            }
        });
        input.addEventListener('blur', () => {
            const n = parseInt(input.value, 10);
            if (!isNaN(n)) navigateToPostNumber(n, config);
        });
        input.addEventListener('focus', () => input.select());
    }
}

function navigateToPostNumber(postNumber: number, config: DiscussionConfig): void {
    const n = Math.max(1, Math.min(postNumber, totalPostCount));

    // Check if already in DOM
    const el = document.getElementById(`post-${n}`);
    if (el) {
        suppressFragmentUpdate = true;
        // For post-1, scroll to the very top so the sticky header doesn't obscure it.
        if (n === 1) {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        } else {
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
        const newHash = n <= 1 ? '' : `#post-${n}`;
        history.replaceState(null, '', location.pathname + newHash);
        setTimeout(() => { suppressFragmentUpdate = false; }, 500);
        return;
    }

    // Need to load â€” use fragment entry mechanism
    if (n === 1) {
        // handleFragmentEntry skips postNumber <= 1 from hash; pass it explicitly instead
        history.replaceState(null, '', location.pathname);
        handleFragmentEntry(config.discussionId, config.currentUserId || '', config.isAuthenticated, config.isLocked, 1);
    } else {
        history.replaceState(null, '', location.pathname + `#post-${n}`);
        handleFragmentEntry(config.discussionId, config.currentUserId || '', config.isAuthenticated, config.isLocked);
    }
}

// ===== Relative Timestamp Ticker =====

let timestampTickerInterval: ReturnType<typeof setInterval> | null = null;

function updateAllTimestamps(): void {
    document.querySelectorAll<HTMLElement>('time[data-timestamp]').forEach(el => {
        const formatted = formatPostRelativeTime(el.dataset.timestamp!);
        if (formatted) el.textContent = formatted;
    });
}

function initTimestampTicker(): void {
    updateAllTimestamps();
    if (timestampTickerInterval) clearInterval(timestampTickerInterval);
    timestampTickerInterval = setInterval(updateAllTimestamps, 30_000);
}

// ===== Initialize Discussion Page =====
function initDiscussionPage(config: DiscussionConfig): void {
    discussionConfig = config;

    rxCleanupExpired();

    // Record view — authenticated users go through SharedWorker (batched), anonymous get a direct fetch
    if (config.discussionId) {
        const realtime = (window as any).SnakkRealtime;
        if (realtime?.recordView) {
            realtime.recordView(config.discussionId);
        } else {
            fetch('/bff/views/discussions', {
                method: 'POST',
                keepalive: true,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify([config.discussionId]),
            }).catch(() => {});
        }
    }

    // Reset editor state for HTMX navigation (DOM was swapped, old editor is gone)
    editorInitPromise = null;

    // Reset DOM windowing state for this page (handles HTMX navigation re-init)
    resetChunkState();

    // Seed last-rendered author from the SSR posts so the first JS chunk boundary is correct.
    lastRenderedAuthorId = lastPostIn(document.getElementById('posts-container') ?? document.body)?.dataset.authorId ?? null;

    // Set endless scroll state from config
    postsCurrentOffset = config.postsCurrentOffset;
    postsHasMoreItems = config.postsHasMoreItems;

    // Seed the SSR chunk record so pruning knows about it from the start
    const ssrChunkEl = document.querySelector<HTMLDivElement>('#posts-container [data-chunk-offset="0"]');
    if (ssrChunkEl) {
        requestAnimationFrame(() => {
            const h = ssrChunkEl.getBoundingClientRect().height;
            if (h > 0) {
                chunksInDom.push({ offset: 0, el: ssrChunkEl, height: h });
                scrollDbg.log('seeded SSR chunk', { height: h });
            }
        });
    }

    // Seed last-read post id for the separator and jump-to-unread button
    lastReadPostId = config.lastReadPostId ?? null;
    unreadLabel = config.unreadLabel ?? '';
    insertUnreadSeparator();

    // Reset fragment/load-up state
    postsStartOffset = 0;
    postsIsLoadingEarlier = false;
    suppressFragmentUpdate = false;
    fragmentRafId = null;
    loadUpObserver?.disconnect();
    loadUpObserver = null;
    totalPostCount = 0;

    // Expose config values on body dataset so IntersectionObserver closures can read them
    document.body.dataset.currentUserId = config.currentUserId || '';
    document.body.dataset.isAuthenticated = String(config.isAuthenticated);
    document.body.dataset.isLocked = String(config.isLocked);

    // Initialize read state batcher
    if (window.SnakkReadStateBatcher) {
        window.SnakkReadStateBatcher.init(config.isAuthenticated);
    }

    // Track post visibility for read state (IntersectionObserver â€” no scroll polling)
    initReadObserver();

    // Apply hidden users filter
    applyHiddenUsers();

    // Store discussionId for deferred editor init
    activeDiscussionId = config.discussionId || null;

    // IAmA: build official-answer lookup tables
    iamaOfficialAnswers = config.officialAnswers ?? {};
    iamaAnswerToQuestion = {};
    for (const [q, a] of Object.entries(iamaOfficialAnswers)) {
        iamaAnswerToQuestion[a] = q;
    }

    // Escape key closes the drawer when it is open
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && document.getElementById('compose-drawer')?.classList.contains('sn-compose-drawer--open')) {
            closeComposer();
        }
    });

    // Highlight code blocks in initial page load
    console.log('[DiscussionDetail] initDiscussionPage â€” hasCodeBlocks:', (config as any).hasCodeBlocks, 'SnakkSyntax present:', !!(window as any).SnakkSyntax);
    if ((window as any).SnakkSyntax) {
        (window as any).SnakkSyntax.highlightAll(undefined, 'discussion-detail:init');
    }

    // Load follow/mute status (authenticated users only)
    if (config.discussionId && config.isAuthenticated) {
        loadFollowStatus(config.discussionId);
        loadMuteStatus(config.discussionId);
    }

    // Initialize endless scroll and DOM-window spacer observers
    initPostsEndlessScroll();
    initSpacerObservers();
    // Fragment navigation: load correct page when entering via #post-N link
    handleFragmentEntry(config.discussionId, config.currentUserId || '', config.isAuthenticated, config.isLocked);
    initFragmentTracking();
    // Thread navigation bar (osu-style pagination)
    initThreadNav(config);

    // Initialize keyboard navigation
    initKeyboardNavigation();

    // Start relative-time ticker (updates all data-timestamp elements every 30s)
    initTimestampTicker();

    // Setup event listeners
    setupEventListeners();

}

function setupEventListeners(): void {
    if ((window as any).__discussionDetailListenersRegistered) return;
    (window as any).__discussionDetailListenersRegistered = true;

    window.addEventListener('beforeunload', clearComposingState);

    // Check selection on mouseup anywhere in document
    document.addEventListener('mouseup', () => {
        // Small delay to let selection finalize
        setTimeout(() => {
            const selection = window.getSelection();
            const selectedText = selection?.toString().trim() || '';

            if (!selectedText || selectedText.length < 3) {
                currentSelection = { postId: null, text: '', authorName: '' };
                hideSelectionQuoteButton();
                return;
            }

            // Check if selection is within a post content div
            if (!selection || !selection.rangeCount) return;

            const range = selection.getRangeAt(0);
            const container = range.commonAncestorContainer;

            // Find the post content div
            const postContentDiv = container.nodeType === Node.TEXT_NODE
                ? (container.parentElement as HTMLElement | null)?.closest('[id^="post-content-"]')
                : (container as HTMLElement).closest?.('[id^="post-content-"]');

            if (postContentDiv) {
                const postId = postContentDiv.id.replace('post-content-', '');
                const authorName = (postContentDiv as HTMLElement).dataset.authorName || 'Unknown';

                currentSelection = { postId, text: selectedText, authorName };
                showSelectionQuoteButton();
            } else {
                currentSelection = { postId: null, text: '', authorName: '' };
                hideSelectionQuoteButton();
            }
        }, 10);
    });

    // Hide quote button when clicking elsewhere (but not on the button itself)
    document.addEventListener('mousedown', (event) => {
        const btn = document.getElementById('selection-quote-btn');
        if (btn && !btn.contains(event.target as Node)) {
            hideSelectionQuoteButton();
        }
    });

    // Hide on scroll
    document.addEventListener('scroll', hideSelectionQuoteButton, { passive: true });

    // Hide reaction picker when clicking outside
    document.addEventListener('click', (event) => {
        const picker = document.getElementById('reaction-picker');
        const target = event.target as HTMLElement;
        if (picker && !picker.classList.contains('sn-hidden') && !picker.contains(target) && !target.closest('[aria-label="Add reaction to post"]')) {
            hideReactionPicker();
        }
    });

    // Toggle spoiler reveal on click
    document.addEventListener('click', (event) => {
        const spoiler = (event.target as HTMLElement).closest('.spoiler') as HTMLElement | null;
        if (spoiler) {
            spoiler.classList.toggle('sn-revealed');
        }
    });

    // Show reason description when selected
    document.getElementById('report-reason')?.addEventListener('change', function(this: HTMLSelectElement) {
        const selectedOption = this.options[this.selectedIndex];
        const description = selectedOption?.dataset?.description;
        const descDiv = document.getElementById('report-reason-description');

        if (description) {
            if (descDiv) descDiv.textContent = description;
            descDiv?.classList.remove('sn-hidden');
        } else {
            descDiv?.classList.add('sn-hidden');
        }
    });
}

// ===== Moderator Actions =====

async function modDeletePost(postId: string): Promise<void> {
    if (!postId || !confirm('Remove this post? This action cannot be undone.')) return;
    try {
        const res = await fetch(`/bff/posts/${postId}/mod`, { method: 'DELETE' });
        if (!res.ok) throw new Error('Server error');
        const el = document.getElementById(`post-${postId}`);
        if (el) {
            el.style.transition = 'opacity 0.3s';
            el.style.opacity = '0';
            setTimeout(() => {
                el.innerHTML = '<div class=”sn-post-deleted-tombstone sn-text-muted sn-text-sm sn-italic sn-py-4 sn-px-6”>[Removed by moderator]</div>';
                el.style.opacity = '1';
            }, 300);
        }
    } catch {
        alert('Failed to remove post. Please try again.');
    }
}

async function modToggleDiscussionLock(discussionId: string, isLocked: boolean): Promise<void> {
    try {
        const method = isLocked ? 'DELETE' : 'POST';
        const res = await fetch(`/bff/discussions/${discussionId}/lock`, { method });
        if (!res.ok) throw new Error('Server error');
        const nowLocked = !isLocked;
        // Update button state
        const btn = document.querySelector<HTMLElement>('[data-action=”mod-lock-discussion”]');
        if (btn) {
            btn.dataset.isLocked = String(nowLocked);
            const label = btn.querySelector('span:not(.sn-icon)');
            if (label) label.textContent = nowLocked ? 'Unlock' : 'Lock';
            }
        // Update reply box
        const replyForm = document.getElementById('reply-form-area');
        if (replyForm) {
            if (nowLocked) replyForm.setAttribute('hidden', '');
            else replyForm.removeAttribute('hidden');
        }
    } catch {
        alert('Failed to update lock state. Please try again.');
    }
}

async function modDeleteDiscussion(discussionId: string): Promise<void> {
    if (!discussionId || !confirm('Remove this discussion? This action cannot be undone.')) return;
    try {
        const res = await fetch(`/bff/discussions/${discussionId}`, { method: 'DELETE' });
        if (!res.ok) throw new Error('Server error');
        window.location.href = '/';
    } catch {
        alert('Failed to remove discussion. Please try again.');
    }
}

let _modBanTargetUserId = '';

function openModUserOptions(authorId: string, authorName: string): void {
    _modBanTargetUserId = authorId;
    openModBanModal(authorId, authorName);
}

function openModBanModal(authorId: string, authorName: string): void {
    _modBanTargetUserId = authorId;
    const nameEl = document.getElementById('mod-ban-author-name');
    if (nameEl) nameEl.textContent = authorName;
    const errEl = document.getElementById('mod-ban-error');
    if (errEl) { errEl.textContent = ''; errEl.classList.add('sn-hidden'); }
    const modal = document.getElementById('mod_ban_modal') as HTMLDialogElement | null;
    modal?.showModal();
}

async function submitModBan(): Promise<void> {
    const userId = _modBanTargetUserId;
    if (!userId) return;

    const scope = (document.getElementById('mod-ban-scope') as HTMLSelectElement)?.value;
    const banType = (document.getElementById('mod-ban-type') as HTMLSelectElement)?.value;
    const durationHours = banType === 'Temporary'
        ? parseInt((document.getElementById('mod-ban-duration-hours') as HTMLInputElement)?.value || '24', 10)
        : null;
    const reason = (document.getElementById('mod-ban-reason') as HTMLTextAreaElement)?.value.trim() || null;

    const config = discussionConfig;
    const body: Record<string, unknown> = { banType, reason };
    if (durationHours) body.durationHours = durationHours;
    if (scope === 'space' && config?.spaceId) body.spaceId = config.spaceId;
    else if (scope === 'hub' && config?.hubId) body.hubId = config.hubId;
    else if (scope === 'community' && config?.communityId) body.communityId = config.communityId;

    const submitBtn = document.getElementById('mod-ban-submit') as HTMLButtonElement | null;
    if (submitBtn) { submitBtn.disabled = true; submitBtn.textContent = 'Banning…'; }

    try {
        const res = await fetch(`/bff/users/${userId}/ban`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        if (!res.ok) throw new Error('Server error');
        const modal = document.getElementById('mod_ban_modal') as HTMLDialogElement | null;
        modal?.close();
    } catch {
        const errEl = document.getElementById('mod-ban-error');
        if (errEl) { errEl.textContent = 'Failed to ban user. Please try again.'; errEl.classList.remove('sn-hidden'); }
    } finally {
        if (submitBtn) { submitBtn.disabled = false; submitBtn.textContent = 'Ban'; }
    }
}

// ===== Event Delegation =====
// Registered once â€” hx-boost re-executes this script on every discussion page navigation,
// which would stack duplicate listeners and fire actions N times per click.
if (!(window as any).__discussionDetailActionsRegistered) {
    (window as any).__discussionDetailActionsRegistered = true;

document.addEventListener('click', async (e) => {
    const target = e.target as HTMLElement;
    const action = target.closest('[data-action]') as HTMLElement | null;
    if (!action) return;

    const actionName = action.dataset.action;

    // Prevent default for most actions
    if (actionName !== 'submit-form') {
        e.preventDefault();
    }

    switch (actionName) {
        // Reply actions
        case 'open-compose':
            openComposer();
            break;
        case 'close-compose':
            closeComposer();
            break;
        case 'reply-to-post':
            replyToPost(action.dataset.postId || '', action.dataset.authorName || '');
            break;
        case 'quote-post':
            quotePost(action.dataset.postId || '', action.dataset.content || '', action.dataset.authorName || '');
            break;
        case 'clear-reply-context':
            clearReplyContext();
            break;

        // Post actions
        case 'expand-post':
            expandPost(action.dataset.postId || '');
            break;
        case 'edit-post':
            editPost(action.dataset.postId || '', action.dataset.userId || '');
            break;
        case 'submit-edit':
            submitEdit(action.dataset.postId || '');
            break;
        case 'cancel-edit':
            cancelEdit(action.dataset.postId || '');
            break;
        case 'edit-discussion-title':
            editDiscussionTitle();
            break;
        case 'submit-discussion-title':
            submitDiscussionTitle();
            break;
        case 'cancel-discussion-title':
            cancelDiscussionTitle();
            break;
        case 'highlight-post':
            highlightPost(action.dataset.postId || '');
            break;

        case 'toggle-post-inline-actions': {
            const postArticle = action.closest<HTMLElement>('.sn-post-article');
            if (!postArticle) break;
            const inlineActions = postArticle.querySelector<HTMLElement>('.sn-post-inline-actions');
            const toggleBtn = postArticle.querySelector<HTMLElement>('.sn-post-opts-toggle');
            const reactionPlaceholder = postArticle.querySelector<HTMLElement>('.sn-reaction-placeholder');
            if (inlineActions) inlineActions.removeAttribute('hidden');
            if (toggleBtn) toggleBtn.style.display = 'none';
            if (reactionPlaceholder) reactionPlaceholder.classList.add('sn-actions-forced');
            break;
        }

        // Reaction actions
        case 'toggle-reaction-picker':
            toggleReactionPicker(action.dataset.postId || '', action);
            break;
        case 'toggle-reaction':
            await toggleReaction(action.dataset.postId || '', action.dataset.reactionType || '');
            break;

        // Share actions
        case 'copy-discussion-link': {
            const discussionUrl = action.dataset.discussionUrl || '';
            if (!discussionUrl) break;
            try {
                await navigator.clipboard.writeText(discussionUrl);
                const originalHtml = action.innerHTML;
                action.textContent = 'Copied!';
                setTimeout(() => { action.innerHTML = originalHtml; }, 1500);
            } catch {
                // Clipboard API unavailable â€” silently ignore
            }
            break;
        }

        // Discussion actions
        case 'toggle-follow-discussion':
            await toggleFollowDiscussion(action.dataset.discussionId || '');
            break;
        case 'toggle-mute-discussion':
            toggleMuteDiscussion(action.dataset.discussionId || '');
            break;

        // User actions
        case 'follow-user':
            await followUser(action.dataset.authorId || '', action.dataset.authorName || '');
            break;
        case 'hide-posts-from-user':
            hidePostsFromUser(action.dataset.authorId || '', action.dataset.authorName || '');
            break;
        case 'unhide-user':
            unhideUser(action.dataset.authorId || '');
            break;

        // Load actions
        case 'retry-load-posts':
            retryLoadPosts(
                action.dataset.discussionId || '',
                action.dataset.currentUserId || '',
                action.dataset.isAuthenticated === 'true',
                action.dataset.isLocked === 'true'
            );
            break;
        case 'load-more-posts':
            await loadMorePosts(
                action.dataset.discussionId || '',
                action.dataset.currentUserId || '',
                action.dataset.isAuthenticated === 'true',
                action.dataset.isLocked === 'true'
            );
            break;

        // Report actions
        case 'open-report-modal':
            openReportModal(
                action.dataset.reportType || '',
                action.dataset.reportId || '',
                action.dataset.reportLabel || '',
                action.dataset.spaceId
            );
            break;

        // Moderator actions
        case 'mod-delete-post':
            await modDeletePost(action.dataset.postId || '');
            break;
        case 'mod-user-options':
            openModUserOptions(action.dataset.authorId || '', action.dataset.authorName || '');
            break;
        case 'mod-ban-user':
            openModBanModal(action.dataset.authorId || '', action.dataset.authorName || '');
            break;
        case 'mod-lock-discussion':
            await modToggleDiscussionLock(
                action.dataset.discussionId || '',
                action.dataset.isLocked === 'true'
            );
            break;
        case 'mod-delete-discussion':
            await modDeleteDiscussion(action.dataset.discussionId || '');
            break;
    }
});

// Mod ban modal submit
document.getElementById('mod-ban-submit')?.addEventListener('click', submitModBan);

// Mod ban type toggle (hide/show duration row)
document.getElementById('mod-ban-type')?.addEventListener('change', (e) => {
    const type = (e.target as HTMLSelectElement).value;
    const row = document.getElementById('mod-ban-duration-row');
    if (row) row.style.display = type === 'Permanent' ? 'none' : '';
});

// Register handlers with global delegation system (for data-submit-action and data-input-action)
if (window.SnakkActions) {
    window.SnakkActions.on('auto-grow', (el) => autoGrow(el as HTMLTextAreaElement));
    window.SnakkActions.on('submit-report', (_el, e) => submitReport(e));
}

} // end __discussionDetailActionsRegistered guard

// Export minimal API for programmatic access
(window as any).SnakkDiscussion = {
    init: initDiscussionPage,
    loadReactions: loadAllReactions,
    loadMorePosts: loadMorePosts
};

// Keep initDiscussionPage on window for backwards compatibility with SPA navigation
(window as any).initDiscussionPage = initDiscussionPage;

// ===== Self-initializing bootstrap (reads JSON config from Razor) =====
function bootstrapFromPageConfig(): void {
    const configEl = document.getElementById('discussion-page-config');
    console.log('[DiscussionDetail] bootstrapFromPageConfig â€” configEl found:', !!configEl, 'readyState:', document.readyState);
    if (!configEl) return;

    let config: DiscussionConfig;
    try {
        config = JSON.parse(configEl.textContent || '{}');
    } catch {
        return;
    }

    // Set body dataset properties (used by IntersectionObserver closures and other scripts)
    if (config.discussionId) {
        document.body.dataset.discussionId = config.discussionId;
        document.body.dataset.discussionType = config.discussionType || '';
        document.body.dataset.isAuthenticated = String(config.isAuthenticated);
        document.body.dataset.currentUserId = config.currentUserId || '';
        document.body.dataset.spaceSlug = config.spaceSlug || '';
        document.body.dataset.hubSlug = config.hubSlug || '';

        // Track in read history. read-history.js and discussion-detail.js are both
        // injected as async dynamic scripts by HTMX — execution order is not guaranteed.
        // If SnakkReadHistory isn't ready yet, queue the entry; read-history.ts drains it.
        if ((config as any).readHistory) {
            const entry = (config as any).readHistory;
            if (window.SnakkReadHistory) {
                window.SnakkReadHistory.addToHistory(entry);
            } else {
                ((window as any)._snakkReadHistoryQueue = (window as any)._snakkReadHistoryQueue || []).push(entry);
            }
        }
    }

    initDiscussionPage(config);
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bootstrapFromPageConfig);
} else {
    bootstrapFromPageConfig();
}

})();

