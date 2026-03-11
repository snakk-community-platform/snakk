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
    role?: 'admin' | 'mod' | 'user';
    isDeleted?: boolean;
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
    isAuthenticated: boolean;
    currentUserId: string;
    isLocked: boolean;
    preferEndlessScroll: boolean;
    postsCurrentOffset: number;
    postsHasMoreItems: boolean;
    hasCodeBlocks: boolean;
    postCount: number;
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

// ===== Editor Functions =====

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

// Initialize Toast UI editor for the reply form (deduped — safe to call multiple times)
let editorInitPromise: Promise<void> | null = null;
let activeDiscussionId: string | null = null;

function initReplyEditor(): Promise<void> {
    if (editorInitPromise) return editorInitPromise;

    editorInitPromise = (async () => {
        const container = document.getElementById('editor-container');
        const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
        if (!container || !textarea) return;
        if (!(window as any).SnakkEditor) return;

        const editor = await (window as any).SnakkEditor.init({
            container,
            textarea,
            placeholder: 'Share your thoughts...',
        });

        // Focus editor when clicking anywhere in the container (not toolbar/footer)
        if (editor) {
            container.addEventListener('click', (e) => {
                if (!(e.target as HTMLElement).closest('.milkdown-toolbar, .milkdown-footer')) {
                    editor.focus();
                }
            });

            // Move submit button into the editor footer
            const submitBtn = document.getElementById('reply-submit-btn');
            const footer = container.querySelector('.milkdown-footer');
            if (submitBtn && footer) {
                footer.appendChild(submitBtn);
                submitBtn.classList.remove('hidden');
            }
        }

        // Intercept form submit to upload deferred images and sync textarea
        const form = textarea.closest('form') as HTMLFormElement | null;
        if (form && editor) {
            console.log('[Editor] form submit handler attached, hx-boost=', form.getAttribute('hx-boost'));

            form.addEventListener('submit', async (e) => {
                console.log('[Submit] ========== SUBMIT START ==========');
                console.log('[Submit] handler fired, container=', container?.id);
                console.log('[Submit] event defaultPrevented=', e.defaultPrevented, 'type=', e.type);
                const pending: Map<string, File> = (window as any).SnakkEditor.getPendingUploads(container);
                const md = editor.getMarkdown();

                console.log('[Submit] pending.size=', pending.size, 'md length=', md.length);
                console.log('[Submit] md contains blob?', md.includes('blob:'));
                console.log('[Submit] md preview=', md.substring(0, 300));

                // Validate content
                if (!md.trim()) {
                    e.preventDefault();
                    return;
                }

                // Clear draft immediately before submission proceeds
                if (activeDiscussionId && (window as any).SnakkDraftManager) {
                    const replyToPostId = (document.getElementById('reply-to-post-id') as HTMLInputElement)?.value || null;
                    (window as any).SnakkDraftManager.clearDraftOnSuccess(activeDiscussionId, replyToPostId);
                }

                // If there are pending image uploads, handle everything via fetch
                // to avoid HTMX boost intercepting the re-submit and causing race conditions
                if (pending.size > 0) {
                    e.preventDefault();
                    e.stopPropagation();
                    console.log('[Submit] >>> UPLOAD BRANCH: uploading', pending.size, 'images...');
                    console.log('[Submit] pending keys:', [...pending.keys()]);

                    const submitBtn = form.querySelector('button[type="submit"]') as HTMLButtonElement | null;
                    if (submitBtn) {
                        submitBtn.disabled = true;
                        submitBtn.dataset.originalText = submitBtn.textContent || '';
                        submitBtn.textContent = 'Uploading images...';
                    }

                    try {
                        let updatedMd = md;
                        for (const [blobUrl, file] of pending) {
                            console.log('[Submit] uploading:', file.name, 'size=', file.size);
                            const uploadData = new FormData();
                            uploadData.append('file', file, file.name);

                            console.log('[Submit] fetching /bff/media/upload...');
                            const response = await fetch('/bff/media/upload', {
                                method: 'POST',
                                body: uploadData,
                            });

                            console.log('[Submit] upload response: status=', response.status, 'ok=', response.ok);
                            if (!response.ok) {
                                const errText = await response.text();
                                console.error('[Submit] Image upload failed: status=', response.status, 'body=', errText);
                                continue;
                            }

                            const resultText = await response.text();
                            console.log('[Submit] upload raw response:', resultText);
                            const result = JSON.parse(resultText);
                            console.log('[Submit] upload parsed result:', result, 'url=', result.url);
                            updatedMd = updatedMd.split(blobUrl).join(result.url);
                        }

                        console.log('[Submit] updatedMd preview=', updatedMd.substring(0, 200));
                        (window as any).SnakkEditor.clearPendingUploads(container);

                        // Submit the form via fetch to bypass HTMX boost entirely
                        const formData = new FormData(form);
                        formData.set('PostContent', updatedMd);

                        console.log('[Submit] posting form via fetch to:', form.action);
                        const response = await fetch(form.action, {
                            method: 'POST',
                            body: formData,
                        });

                        console.log('[Submit] form response: redirected=', response.redirected, 'url=', response.url, 'status=', response.status);
                        if (response.redirected) {
                            const url = new URL(response.url);
                            url.hash = 'reply-form';
                            window.location.href = url.toString();
                        } else {
                            window.location.reload();
                        }
                    } catch (err) {
                        console.error('[Submit] Error uploading images:', err);
                        if (submitBtn) {
                            submitBtn.disabled = false;
                            submitBtn.textContent = submitBtn.dataset.originalText || 'Post Reply';
                        }
                    }
                    return;
                }

                // No pending uploads — just sync textarea, let form submit naturally
                console.log('[Submit] >>> NO-UPLOAD BRANCH: syncing textarea, letting form submit');
                console.log('[Submit] textarea.name=', textarea.name, 'form.action=', form.action);
                textarea.value = md;
                console.log('[Submit] ========== SUBMIT END (native) ==========');
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
function replyToPost(postId: string, authorName: string): void {
    const replyToInput = document.getElementById('reply-to-post-id') as HTMLInputElement;
    const replyContext = document.getElementById('reply-context');
    const replyContextAuthor = document.getElementById('reply-context-author');

    if (replyToInput) replyToInput.value = postId;
    if (replyContext) replyContext.classList.remove('hidden');
    if (replyContextAuthor) replyContextAuthor.textContent = authorName;

    // Force-init editor if not loaded yet, then focus
    initReplyEditor().then(() => {
        focusReplyEditor();
        document.getElementById('reply-form-container')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
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
    button.className = 'fixed z-50 btn btn-xs btn-primary';
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
    if (replyContext) replyContext.classList.add('hidden');
}

// Highlight a referenced post when clicking quote
function highlightPost(postId: string): void {
    const post = document.querySelector<HTMLElement>(`[data-post-id="${postId}"]`);
    if (post) {
        post.classList.add('post-highlight');
        setTimeout(() => post.classList.remove('post-highlight'), 2000);
    }
}

// Edit post
function editPost(postId: string, userId: string): void {
    const contentDiv = document.getElementById('post-content-' + postId);
    if (!contentDiv) return;

    const rawContent = (contentDiv as HTMLElement).dataset.rawContent || '';
    const originalHtml = contentDiv.innerHTML;

    // Store original state for cancel
    (contentDiv as HTMLElement).dataset.originalHtml = originalHtml;

    contentDiv.innerHTML = `
        <form id="edit-form-${postId}" class="space-y-2">
            <textarea class="textarea w-full min-h-20 text-sm resize-none"
                      id="edit-textarea-${postId}"
                      oninput="autoGrow(this)">${escapeHtml(rawContent)}</textarea>
            <div class="flex items-center justify-between">
                <span class="text-xs text-base-content/50">Supports **bold**, *italic*, \`code\`, [links](url)</span>
                <div class="flex gap-2">
                    <button type="button" class="btn btn-ghost btn-xs" onclick="cancelEdit('${postId}')">Cancel</button>
                    <button type="button" class="btn btn-primary btn-xs" onclick="submitEdit('${postId}', '${userId}')">Save</button>
                </div>
            </div>
        </form>
    `;

    const textarea = document.getElementById('edit-textarea-' + postId) as HTMLTextAreaElement;
    if (textarea) {
        autoGrow(textarea);
        textarea.focus();
    }
}

function escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

const sanitizeHtml = (window as any).SnakkUtils?.sanitizeHtml || function(html: string): string {
    if (!html) return '';
    const parser = new DOMParser();
    const doc = parser.parseFromString(html, 'text/html');
    doc.querySelectorAll('script,iframe,object,embed,form,base,meta,link,style').forEach(el => el.remove());
    doc.body.querySelectorAll('*').forEach(el => {
        Array.from(el.attributes).forEach(attr => {
            if (attr.name.startsWith('on')) el.removeAttribute(attr.name);
        });
        ['href', 'src', 'action', 'formaction'].forEach(a => {
            const v = el.getAttribute(a);
            if (v && v.trim().toLowerCase().startsWith('javascript:')) el.removeAttribute(a);
        });
    });
    return doc.body.innerHTML;
};

function submitEdit(postId: string, userId: string): void {
    const textarea = document.getElementById('edit-textarea-' + postId) as HTMLTextAreaElement;
    if (!textarea) return;

    const content = textarea.value;

    fetch(`/bff/posts/${postId}/edit?userId=${userId}&content=${encodeURIComponent(content)}`, {
        method: 'POST'
    })
    .then(response => response.text())
    .then(html => {
        const postElement = document.getElementById('post-' + postId);
        if (postElement) {
            postElement.outerHTML = html;
        }
    })
    .catch(error => {
        alert('Error updating post: ' + error);
    });
}

function cancelEdit(postId: string): void {
    const contentDiv = document.getElementById('post-content-' + postId);
    if (!contentDiv) return;

    const originalHtml = (contentDiv as HTMLElement).dataset.originalHtml;
    if (originalHtml) {
        contentDiv.innerHTML = originalHtml;
        delete (contentDiv as HTMLElement).dataset.originalHtml;
    }
}

// Jump to unread functionality
let lastReadPostId: string | null = null;

function jumpToUnread(): void {
    if (lastReadPostId) {
        // Find the next post after lastReadPostId
        const posts = document.querySelectorAll<HTMLElement>('.post-item');
        let foundLast = false;
        for (const post of posts) {
            if (foundLast) {
                post.scrollIntoView({ behavior: 'smooth', block: 'center' });
                post.classList.add('post-highlight');
                setTimeout(() => post.classList.remove('post-highlight'), 2000);
                break;
            }
            if (post.dataset.postId === lastReadPostId) {
                foundLast = true;
            }
        }
    }
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
    document.querySelectorAll<HTMLElement>('.post-item').forEach(post => {
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
    document.querySelectorAll<HTMLElement>('.post-item').forEach(post => {
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

    // Restore draft — if the editor is active, sync the restored content into it
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

const smileyPlaceholderSvg = '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>';

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

// Render reaction spans from data-attributes
function renderReactionCounts(reactionsBar: HTMLElement): void {
    const counts = getReactionCounts(reactionsBar);
    const myReactions = getMyReactions(reactionsBar);
    let html = '';
    let hasAny = false;

    for (const [type, emoji] of Object.entries(reactionEmojis)) {
        const count = counts[type] || 0;
        if (count > 0) {
            const isActive = myReactions.includes(type);
            html += `<span data-type="${type}" class="${isActive ? 'active' : ''}">${emoji} ${count}</span>`;
            hasAny = true;
        }
    }

    if (!hasAny) {
        html = `<span class="hidden group-hover:inline" data-reaction-placeholder>${smileyPlaceholderSvg}</span>`;
    }

    reactionsBar.innerHTML = html;
}

function hideReactionPicker(): void {
    // Restore any elements that were forced visible
    document.querySelectorAll<HTMLElement>('[data-actions-forced]').forEach(el => {
        if (el.hasAttribute('data-reaction-placeholder')) {
            // Smiley placeholder: restore hidden + group-hover:inline
            el.classList.add('hidden');
            el.classList.remove('inline');
            // Tailwind class needs to be re-added since we removed it
            if (!el.classList.contains('group-hover:inline')) {
                el.classList.add('group-hover:inline');
            }
        } else {
            // Action button wrappers: restore hidden + remove flex
            el.classList.add('hidden');
            el.classList.remove('flex');
        }
        delete el.dataset.actionsForced;
    });

    const picker = document.getElementById('reaction-picker');
    if (picker) {
        picker.classList.add('hidden');
        picker.dataset.postId = '';
    }
    currentReactionPostId = null;
}

function setupReactionPickerHover(): void {
    const picker = document.getElementById('reaction-picker');
    if (!picker || picker.dataset.hoverBound) return;

    picker.addEventListener('mouseenter', () => {
        if (reactionPickerHideTimer) {
            clearTimeout(reactionPickerHideTimer);
            reactionPickerHideTimer = null;
        }
    });

    picker.addEventListener('mouseleave', () => {
        reactionPickerHideTimer = setTimeout(hideReactionPicker, 300);
    });

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

function toggleReactionPicker(postId: string): void {
    const picker = document.getElementById('reaction-picker');
    const reactionsBar = document.getElementById(`reactions-${postId}`);

    if (!picker || !reactionsBar) return;

    // Clear any pending hide timer
    if (reactionPickerHideTimer) {
        clearTimeout(reactionPickerHideTimer);
        reactionPickerHideTimer = null;
    }

    if (currentReactionPostId === postId && !picker.classList.contains('hidden')) {
        hideReactionPicker();
        return;
    }

    currentReactionPostId = postId;
    picker.dataset.postId = postId;

    const rect = reactionsBar.getBoundingClientRect();
    picker.style.left = `${rect.left}px`;
    // picker is position:fixed, so use viewport-relative coordinates (no scrollY)
    picker.style.top = `${rect.bottom + 5}px`;

    // Force the hover wrapper visible while picker is open
    const postArticle = reactionsBar.closest('.post-article');
    const hoverWrapper = postArticle?.querySelector('.hidden.group-hover\\:flex') as HTMLElement | null;
    if (hoverWrapper) {
        hoverWrapper.classList.remove('hidden');
        hoverWrapper.classList.add('flex');
        hoverWrapper.dataset.actionsForced = 'true';
    }

    // Force the smiley placeholder visible while picker is open
    const smileyPlaceholder = reactionsBar.querySelector('[data-reaction-placeholder]') as HTMLElement | null;
    if (smileyPlaceholder) {
        smileyPlaceholder.classList.remove('hidden', 'group-hover:inline');
        smileyPlaceholder.classList.add('inline');
        smileyPlaceholder.dataset.actionsForced = 'true';
    }

    // Start hide timer when mouse leaves the reactions area
    reactionsBar.onmouseleave = () => {
        reactionPickerHideTimer = setTimeout(hideReactionPicker, 300);
    };

    picker.classList.remove('hidden');
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

    // Snapshot for revert on error
    const snapshotCounts = getReactionCounts(reactionsBar);
    const snapshotMyReactions = getMyReactions(reactionsBar);

    // Compute optimistic state
    const newCounts = { ...snapshotCounts };
    const newMyReactions = [...snapshotMyReactions];
    const existingIndex = newMyReactions.indexOf(reactionType);

    if (existingIndex >= 0) {
        // Toggle off: user already has this reaction type
        newCounts[reactionType] = Math.max(0, (newCounts[reactionType] || 0) - 1);
        newMyReactions.splice(existingIndex, 1);
    } else {
        // Switch: remove any existing reaction first, then add new one
        if (newMyReactions.length > 0) {
            const oldType = newMyReactions[0] as string;
            newCounts[oldType] = Math.max(0, (newCounts[oldType] || 0) - 1);
            newMyReactions.splice(0, newMyReactions.length);
        }
        newCounts[reactionType] = (newCounts[reactionType] || 0) + 1;
        newMyReactions.push(reactionType);
    }

    // Write optimistic state and render immediately
    setReactionData(reactionsBar, newCounts, newMyReactions);
    renderReactionCounts(reactionsBar);

    try {
        const response = await fetch(`/bff/posts/${postId}/reactions`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ type: reactionTypeValues[reactionType] }),
            credentials: 'include'
        });

        if (!response.ok) {
            setReactionData(reactionsBar, snapshotCounts, snapshotMyReactions);
            renderReactionCounts(reactionsBar);
            const errorText = await response.text();
            console.error('Failed to toggle reaction:', response.status, errorText);
            showToast('Failed to update reaction. Please try again.', 'error');
            return;
        }

        // Refresh from server to get accurate counts
        await loadReactionsForPost(postId);
    } catch (err) {
        setReactionData(reactionsBar, snapshotCounts, snapshotMyReactions);
        renderReactionCounts(reactionsBar);
        console.error('Error toggling reaction:', err);
        showToast('Network error. Please check your connection.', 'error');
    }
}

async function loadReactionsForPost(postId: string): Promise<void> {
    const reactionsBar = document.getElementById(`reactions-${postId}`);
    if (!reactionsBar) return;

    try {
        const [countsResponse, myResponse] = await Promise.all([
            fetch(`/bff/posts/${postId}/reactions`),
            fetch(`/bff/posts/${postId}/reactions/me`)
        ]);

        const countsData: ReactionCountsResponse = await countsResponse.json();
        const myData: MyReactionsResponse = await myResponse.json();

        setReactionData(reactionsBar, countsData.counts || {}, myData.reactions || []);
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
    const btn = document.getElementById('follow-btn');

    if (!btn) return;

    // Optimistic UI update - toggle immediately
    const currentlyFollowing = btn.classList.contains('btn-primary');
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

    if (!btn || !text || !icon) return;

    if (isFollowing) {
        btn.classList.add('btn-primary');
        btn.classList.remove('btn-ghost');
        text.textContent = 'Following';
        icon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />';
    } else {
        btn.classList.remove('btn-primary');
        btn.classList.add('btn-ghost');
        text.textContent = 'Follow';
        icon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />';
    }
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
        banner.className = 'fixed top-20 left-1/2 transform -translate-x-1/2 bg-base-100 border border-subtle px-4 py-3 rounded-lg shadow-lg z-50';
        banner.innerHTML = `
            <div class="flex items-center gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-muted" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2" />
                </svg>
                <p class="text-sm">Discussion muted. You won't see it in your feed.</p>
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
        banner.className = 'fixed top-20 left-1/2 transform -translate-x-1/2 bg-base-100 border border-subtle px-4 py-3 rounded-lg shadow-lg z-50';
        banner.innerHTML = `
            <div class="flex items-center gap-3">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-muted" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                </svg>
                <div>
                    <p class="text-sm font-medium">Posts from ${userName} are now hidden</p>
                    <button onclick="unhideUser('${userId}')" class="text-xs text-primary underline">Undo</button>
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
        document.querySelectorAll('.fixed.top-20').forEach(banner => banner.remove());
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
// let typingTimeout: ReturnType<typeof setTimeout> | null = null;
// let isTyping = false;

// Commented out - not currently used but may be needed in the future
// function notifyTyping(): void {
//     if (!isTyping) {
//         isTyping = true;
//         // TODO: Send typing start notification via SignalR
//         // connection.invoke('StartTyping', discussionId);
//     }
//
//     if (typingTimeout) clearTimeout(typingTimeout);
//     typingTimeout = setTimeout(() => {
//         isTyping = false;
//         // TODO: Send typing stop notification via SignalR
//         // connection.invoke('StopTyping', discussionId);
//     }, 2000);
// }

// function showTypingIndicator(users: string[]): void {
//     const indicator = document.getElementById('typing-indicator');
//     const usersSpan = document.getElementById('typing-users');
//
//     if (!indicator || !usersSpan) return;
//
//     if (users && users.length > 0) {
//         if (users.length === 1) {
//             usersSpan.textContent = `${users[0]} is typing...`;
//         } else if (users.length === 2) {
//             usersSpan.textContent = `${users[0]} and ${users[1]} are typing...`;
//         } else {
//             usersSpan.textContent = `${users.length} people are typing...`;
//         }
//         indicator.classList.remove('hidden');
//     } else {
//         indicator.classList.add('hidden');
//     }
// }

// ===== Keyboard Navigation =====
let currentPostIndex = -1;
const posts: HTMLElement[] = [];

function initKeyboardNavigation(): void {
    // Build posts array for navigation
    document.querySelectorAll<HTMLElement>('.post-article').forEach(post => {
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
        if (picker && !picker.classList.contains('hidden')) {
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
                    posts[currentPostIndex]?.classList.remove('keyboard-selected');
                    currentPostIndex = -1;
                }
                break;
        }
    });
}

function navigateToPost(index: number): void {
    // Clear previous selection
    if (currentPostIndex >= 0) {
        posts[currentPostIndex]?.classList.remove('keyboard-selected');
    }

    // Clamp index
    if (index < 0) index = 0;
    if (index >= posts.length) index = posts.length - 1;

    currentPostIndex = index;

    const currentPost = posts[currentPostIndex];
    if (currentPost) {
        currentPost.classList.add('keyboard-selected');
        currentPost.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

// ===== Toast Notifications =====
function showToast(message: string, type: 'error' | 'success' | 'info' = 'error', duration: number = 4000): void {
    const toast = document.createElement('div');
    const bgColor = type === 'error' ? 'bg-error' : type === 'success' ? 'bg-success' : 'bg-info';
    toast.className = `fixed bottom-6 right-6 ${bgColor} text-white px-4 py-3 rounded-lg shadow-lg z-50 flex items-center gap-2 max-w-sm`;
    toast.style.transition = 'all 0.3s ease';
    toast.style.transform = 'translateX(400px)';

    const icon = type === 'error'
        ? '<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>'
        : type === 'success'
        ? '<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>'
        : '<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>';

    toast.innerHTML = `
        ${icon}
        <p class="text-sm">${message}</p>
    `;

    document.body.appendChild(toast);

    // Animate in
    setTimeout(() => {
        toast.style.transform = 'translateX(0)';
    }, 10);

    // Remove after duration
    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(400px)';
        setTimeout(() => toast.remove(), 300);
    }, duration);
}

// ===== Endless Scroll for Posts =====
let postsCurrentOffset = 0;
let postsHasMoreItems = false;
let postsIsLoading = false;
const postsPageSize = 20;
let postsScrollObserver: IntersectionObserver | null = null;

// Load-up (earlier posts) state
let postsStartOffset = 0;
let postsIsLoadingEarlier = false;
let loadUpObserver: IntersectionObserver | null = null;

// Fragment tracking state
let fragmentRafId: number | null = null;
let suppressFragmentUpdate = false;

// Thread nav state
let totalPostCount = 0;

function initPostsEndlessScroll(): void {
    const sentinel = document.getElementById('scroll-sentinel');
    if (!sentinel) return;

    // Disconnect previous observer if it exists
    if (postsScrollObserver) {
        postsScrollObserver.disconnect();
    }

    postsScrollObserver = new IntersectionObserver((entries) => {
        const entry = entries[0];
        if (entry && entry.isIntersecting && postsHasMoreItems && !postsIsLoading) {
            // This will be called from event delegation with proper params
            const discussionId = document.body.dataset.discussionId || '';
            const currentUserId = document.body.dataset.currentUserId || '';
            const isAuthenticated = document.body.dataset.isAuthenticated === 'true';
            const isLocked = document.body.dataset.isLocked === 'true';

            loadMorePosts(discussionId, currentUserId, isAuthenticated, isLocked);
        }
    }, { rootMargin: '100px' });

    postsScrollObserver.observe(sentinel);
}

async function loadMorePosts(discussionId: string, currentUserId: string, isAuthenticated: boolean, isLocked: boolean): Promise<void> {
    if (postsIsLoading || !postsHasMoreItems) return;
    postsIsLoading = true;

    const loadingIndicator = document.getElementById('loading-indicator');
    const endMessage = document.getElementById('end-message');
    loadingIndicator?.classList.remove('hidden');


    try {
        const response = await fetch(
            `/bff/discussions/${discussionId}/posts?offset=${postsCurrentOffset}&pageSize=${postsPageSize}`,
            { credentials: 'include' }
        );

        if (!response.ok) throw new Error('Failed to load posts');

        const data: { items?: Post[]; hasMoreItems: boolean; hasCodeBlocks?: boolean } = await response.json();
        const container = document.getElementById('posts-container');
        const sentinel = document.getElementById('scroll-sentinel');

        if (!container || !sentinel) return;

        if (data.items && data.items.length > 0) {
            // Track previous author for grouping
            const existingPosts = container.querySelectorAll<HTMLElement>('.post-item');
            let previousAuthorId: string | null = existingPosts.length > 0
                ? existingPosts[existingPosts.length - 1]?.dataset.authorId || null
                : null;

            data.items.forEach(post => {
                const isSameAuthor = previousAuthorId === post.author.publicId;
                const postElement = createPostElement(post, isSameAuthor, currentUserId, isAuthenticated, isLocked);
                container.insertBefore(postElement, sentinel);
                previousAuthorId = post.author.publicId;

                // Load reactions for this new post
                loadReactionsForPost(post.publicId);
            });
            postsCurrentOffset += data.items.length;

            // Highlight code blocks in new posts if present
            if (data.hasCodeBlocks && (window as any).SnakkSyntax) {
                (window as any).SnakkSyntax.highlightAll(container);
            }

            // Observe new posts for read tracking
            observeNewPosts();
        }

        postsHasMoreItems = data.hasMoreItems;

        if (!postsHasMoreItems) {
            endMessage?.classList.remove('hidden');
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
        errorMessage?.classList.remove('hidden');
        // Disconnect observer but don't set hasMoreItems to false (allow retry)
        if (postsScrollObserver) {
            postsScrollObserver.disconnect();
            postsScrollObserver = null;
        }
    } finally {
        loadingIndicator?.classList.add('hidden');
        postsIsLoading = false;
    }
}

function retryLoadPosts(discussionId: string, currentUserId: string, isAuthenticated: boolean, isLocked: boolean, preferEndlessScroll: boolean): void {
    const errorMessage = document.getElementById('load-error-message');
    errorMessage?.classList.add('hidden');
    // Reinitialize endless scroll
    if (preferEndlessScroll) {
        initPostsEndlessScroll();
    }
    // Trigger load immediately
    loadMorePosts(discussionId, currentUserId, isAuthenticated, isLocked);
}

function formatPostRelativeTime(dateString: string): string {
    if (!dateString) return '';
    const date = new Date(dateString);
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);

    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    if (hours < 24) return `${hours}h ago`;
    if (days < 7) return `${days}d ago`;
    if (days < 365) return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

function createPostElement(post: Post, isSameAuthorAsPrevious: boolean, currentUserId: string, isAuthenticated: boolean, isLocked: boolean): HTMLElement {
    const article = document.createElement('article');
    article.id = `post-${post.postNumber}`;
    const authorClass = isSameAuthorAsPrevious ? 'same-author' : 'new-author';
    article.className = `post-item post-article post-layout group ${post.isFirstPost ? 'first-post' : ''} ${authorClass}`;
    article.dataset.authorId = post.author.publicId;
    article.dataset.postId = post.publicId;
    article.dataset.postNumber = String(post.postNumber);

    const isOP = post.isFirstPost;
    const hasReplyTo = post.replyTo != null;
    const isOwner = isAuthenticated && currentUserId === post.author.publicId;

    // Build left pane (skip for first post — author is in header)
    let authorPaneHtml = '';
    if (!isOP) {
        authorPaneHtml = '<aside class="post-author-pane">';
        if (!isSameAuthorAsPrevious) {
            if (post.author.isDeleted) {
                authorPaneHtml += `
                    <div class="post-avatar post-avatar-deleted">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                        </svg>
                    </div>
                    <span class="post-author-name deleted">${escapeHtml(post.author.displayName)}</span>`;
            } else {
                authorPaneHtml += `
                    <img src="${post.author.avatarUrl || ''}" alt="${escapeHtml(post.author.displayName)}"
                         width="48" height="48" class="post-avatar" loading="lazy" />
                    <a href="/u/${post.author.publicId}" class="post-author-name"
                       data-popup-type="user" data-popup-id="${post.author.publicId}"
                       data-popup-name="${escapeHtml(post.author.displayName)}">${escapeHtml(post.author.displayName)}</a>`;
            }
            // Badges
            let badges = '';
            if (post.author.role === 'admin') {
                badges += '<span class="badge badge-error badge-xs">Admin</span>';
            } else if (post.author.role === 'mod') {
                badges += '<span class="badge badge-info badge-xs">Mod</span>';
            }
            authorPaneHtml += `<div class="post-author-badges">${badges}</div>`;
        }
        authorPaneHtml += '</aside>';
    }

    // Build action buttons
    let actionButtonsHtml = '';
    if (!isLocked && isAuthenticated) {
        actionButtonsHtml = `
            <div class="hidden group-hover:flex items-center gap-1">
            <button onclick="replyToPost('${post.publicId}', '${escapeHtml(post.author.displayName)}')"
                    class="subtle-btn"
                    aria-label="Reply to ${escapeHtml(post.author.displayName)}">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
                </svg>
                Reply
            </button>
            <button onclick="quotePost('${post.publicId}', \`${escapeHtml(post.content).replace(/`/g, '\\`')}\`, '${escapeHtml(post.author.displayName)}')"
                    class="subtle-btn"
                    aria-label="Quote post by ${escapeHtml(post.author.displayName)}">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z" />
                </svg>
                Quote
            </button>
            </div>`;
    }

    // Dropdown menu items
    let ownerItems = '';
    if (isOwner) {
        ownerItems = `
            <li><button onclick="editPost('${post.publicId}', '${currentUserId}')" class="text-sm">Edit</button></li>
            <li>
                <button hx-delete="/api/posts/${post.publicId}?userId=${currentUserId}"
                        hx-target="#post-${post.publicId}"
                        hx-swap="outerHTML"
                        hx-confirm="Are you sure you want to delete this post?"
                        class="text-sm text-error">
                    Delete
                </button>
            </li>`;
    }

    let nonOwnerItems = '';
    if (isAuthenticated && !isOwner) {
        nonOwnerItems = `
            <li>
                <button onclick="hidePostsFromUser('${post.author.publicId}', '${escapeHtml(post.author.displayName)}')" class="text-sm">
                    Hide posts from user
                </button>
            </li>
            <li>
                <button onclick="openReportModal('post', '${post.publicId}', 'this post')" class="text-sm text-error">
                    Report post
                </button>
            </li>`;
    }

    const dropdownHtml = `
        <div class="dropdown dropdown-end">
            <button tabindex="0" class="subtle-btn" aria-label="Post options">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 12h.01M12 12h.01M19 12h.01M6 12a1 1 0 11-2 0 1 1 0 012 0zm7 0a1 1 0 11-2 0 1 1 0 012 0zm7 0a1 1 0 11-2 0 1 1 0 012 0z" />
                </svg>
            </button>
            <ul tabindex="0" class="dropdown-content menu p-1 shadow-lg bg-base-100 border border-subtle rounded-lg w-48 z-20">
                <li>
                    <button hx-get="/api/posts/${post.publicId}/history"
                            hx-target="#history-modal-content"
                            hx-swap="innerHTML"
                            onclick="history_modal.showModal()"
                            class="text-sm">
                        History
                    </button>
                </li>
                ${ownerItems}
                ${nonOwnerItems}
            </ul>
        </div>`;

    const canReact = !isLocked && isAuthenticated;
    const smileyPlaceholderHtml = canReact
        ? `<span class="hidden group-hover:inline" data-reaction-placeholder>${smileyPlaceholderSvg}</span>`
        : '';
    const reactionsContainerHtml = `<div class="flex items-center gap-2 text-base text-muted${canReact ? ' cursor-pointer' : ''}" id="reactions-${post.publicId}" data-reaction-counts="{}" data-my-reactions="[]"${canReact ? ` onclick="event.preventDefault(); event.stopPropagation(); toggleReactionPicker('${post.publicId}'); return false;"` : ''} aria-label="${canReact ? 'Add reaction to post' : 'Reactions'}">${smileyPlaceholderHtml}</div>`;

    // Build toolbar
    const editedTag = post.editedAt ? '<span>(edited)</span>' : '';

    // Inline author (shown on mobile, hidden on desktop via CSS; skip for first post)
    let inlineAuthorHtml = '';
    if (!isOP) {
        if (isSameAuthorAsPrevious) {
            // Same-author: inline author is hidden by default, shown on mobile
            if (post.author.isDeleted) {
                inlineAuthorHtml = `<span class="post-author-inline hidden"><span class="deleted">${escapeHtml(post.author.displayName)}</span></span>`;
            } else {
                inlineAuthorHtml = `<span class="post-author-inline hidden"><a href="/u/${post.author.publicId}" data-popup-type="user" data-popup-id="${post.author.publicId}" data-popup-name="${escapeHtml(post.author.displayName)}">${escapeHtml(post.author.displayName)}</a></span>`;
            }
        } else {
            // New author: inline author shown on mobile (CSS controls visibility)
            if (post.author.isDeleted) {
                inlineAuthorHtml = `<span class="post-author-inline"><span class="deleted">${escapeHtml(post.author.displayName)}</span></span>`;
            } else {
                inlineAuthorHtml = `<span class="post-author-inline"><a href="/u/${post.author.publicId}" data-popup-type="user" data-popup-id="${post.author.publicId}" data-popup-name="${escapeHtml(post.author.displayName)}">${escapeHtml(post.author.displayName)}</a></span>`;
            }
        }
    }

    let replyToHtml = '';
    if (hasReplyTo && post.replyTo) {
        replyToHtml = `
            <a href="#post-${post.replyTo.postId}" class="editorial-quote block mb-4 text-sm" onclick="highlightPost('${post.replyTo.postId}')">
                <span class="quote-author">${escapeHtml(post.replyTo.authorName)} wrote:</span>
                <p class="line-clamp-2 mt-1">${escapeHtml(post.replyTo.contentSnippet)}</p>
            </a>`;
    }

    article.innerHTML = `
        ${authorPaneHtml}
        <div class="post-main">
            <div class="post-toolbar">
                <div class="post-toolbar-left">
                    ${inlineAuthorHtml}
                    <span class="post-time">${formatPostRelativeTime(post.createdAt)}${editedTag}</span>
                </div>
                <div class="post-toolbar-right">
                    ${actionButtonsHtml}
                    ${reactionsContainerHtml}
                    ${dropdownHtml}
                </div>
            </div>
            ${replyToHtml}
            <div id="post-content-${post.publicId}" class="prose prose-content" data-author-name="${escapeHtml(post.author.displayName)}">
                ${post.renderedContent ? sanitizeHtml(post.renderedContent) : escapeHtml(post.content)}
            </div>
        </div>
    `;

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
    document.getElementById('report-error')?.classList.add('hidden');
    document.getElementById('report-success')?.classList.add('hidden');
    const submitBtn = document.getElementById('report-submit-btn') as HTMLButtonElement | null;
    if (submitBtn) submitBtn.disabled = false;
    document.getElementById('report-submit-text')?.classList.remove('hidden');
    document.getElementById('report-submit-loading')?.classList.add('hidden');
    document.getElementById('report-reason-description')?.classList.add('hidden');

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
    submitText?.classList.add('hidden');
    submitLoading?.classList.remove('hidden');

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

        // Show success
        document.getElementById('report-error')?.classList.add('hidden');
        document.getElementById('report-success')?.classList.remove('hidden');
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
        submitText?.classList.remove('hidden');
        submitLoading?.classList.add('hidden');
    }
}

function showReportError(message: string): void {
    const errorDiv = document.getElementById('report-error');
    const errorMessage = document.getElementById('report-error-message');
    if (errorMessage) errorMessage.textContent = message;
    errorDiv?.classList.remove('hidden');
}

// ===== Fragment-Based Navigation =====

function initFragmentTracking(): void {
    window.addEventListener('scroll', onScrollUpdateFragment, { passive: true });
}

function onScrollUpdateFragment(): void {
    if (fragmentRafId !== null || suppressFragmentUpdate) return;
    fragmentRafId = requestAnimationFrame(() => {
        fragmentRafId = null;
        const posts = document.querySelectorAll<HTMLElement>('.post-item[data-post-number]');
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
    isLocked: boolean
): Promise<void> {
    const hash = window.location.hash;
    if (!hash || !hash.startsWith('#post-')) return;

    const postNumber = parseInt(hash.slice(6), 10); // '#post-'.length === 6
    if (isNaN(postNumber) || postNumber <= 1) return;

    // Post already in DOM (SSR rendered it) — just scroll
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

    // Block scroll-down observer during load
    postsIsLoading = true;

    // Clear server-rendered posts
    Array.from(container.querySelectorAll('.post-item')).forEach(p => p.remove());

    const loadingIndicator = document.getElementById('loading-indicator');
    loadingIndicator?.classList.remove('hidden');

    postsStartOffset = targetOffset;
    postsCurrentOffset = targetOffset;

    try {
        const response = await fetch(
            `/bff/discussions/${discussionId}/posts?offset=${targetOffset}&pageSize=${postsPageSize}`,
            { credentials: 'include' }
        );
        if (!response.ok) throw new Error('Failed to load posts for fragment');

        const data: { items?: Post[]; hasMoreItems: boolean; hasCodeBlocks?: boolean } = await response.json();

        if (data.items && data.items.length > 0) {
            let previousAuthorId: string | null = null;
            data.items.forEach(post => {
                const el = createPostElement(post, previousAuthorId === post.author.publicId, currentUserId, isAuthenticated, isLocked);
                container.insertBefore(el, scrollSentinel);
                previousAuthorId = post.author.publicId;
            });
            postsCurrentOffset = targetOffset + data.items.length;

            if (data.hasCodeBlocks && (window as any).SnakkSyntax) {
                (window as any).SnakkSyntax.highlightAll(container);
            }
            observeNewPosts();
            data.items.forEach(post => loadReactionsForPost(post.publicId));
        }

        postsHasMoreItems = data.hasMoreItems;
        if (!postsHasMoreItems) {
            document.getElementById('end-message')?.classList.remove('hidden');
        }

        // Show load-up sentinel and start watching for upward scroll
        if (postsStartOffset > 0) {
            document.getElementById('load-up-sentinel')?.classList.remove('hidden');
            initLoadUpObserver(discussionId, currentUserId, isAuthenticated, isLocked);
        }

        // Scroll to target post
        const targetEl = document.getElementById(`post-${postNumber}`);
        if (targetEl) {
            suppressFragmentUpdate = true;
            requestAnimationFrame(() => {
                targetEl.scrollIntoView({ behavior: 'instant', block: 'start' });
                setTimeout(() => { suppressFragmentUpdate = false; }, 200);
            });
        }
    } catch (err) {
        console.error('Failed to load posts for fragment:', err);
    } finally {
        loadingIndicator?.classList.add('hidden');
        postsIsLoading = false;
        // Re-check if scroll sentinel needs to trigger more loading
        if (postsHasMoreItems) initPostsEndlessScroll();
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
    indicator?.classList.remove('hidden');

    try {
        const response = await fetch(
            `/bff/discussions/${discussionId}/posts?offset=${loadOffset}&pageSize=${postsPageSize}`,
            { credentials: 'include' }
        );
        if (!response.ok) throw new Error('Failed to load earlier posts');

        const data: { items?: Post[]; hasMoreItems: boolean; hasCodeBlocks?: boolean } = await response.json();

        if (data.items && data.items.length > 0) {
            const container = document.getElementById('posts-container');
            if (!container) return;

            // Scroll anchor: record the position of the first currently-loaded post
            const firstCurrentPost = container.querySelector<HTMLElement>('.post-item');
            const anchorTop = firstCurrentPost?.getBoundingClientRect().top ?? 0;

            // Build and insert new elements just after the load-up sentinel
            const loadUpSentinel = document.getElementById('load-up-sentinel');
            const insertBefore = loadUpSentinel?.nextSibling ?? firstCurrentPost;
            let previousAuthorId: string | null = null;
            data.items.forEach(post => {
                const el = createPostElement(post, previousAuthorId === post.author.publicId, currentUserId, isAuthenticated, isLocked);
                if (insertBefore) container.insertBefore(el, insertBefore);
                previousAuthorId = post.author.publicId;
            });

            // Restore scroll position to cancel the layout shift from prepended content
            if (firstCurrentPost) {
                window.scrollBy(0, firstCurrentPost.getBoundingClientRect().top - anchorTop);
            }

            postsStartOffset = loadOffset;

            if (data.hasCodeBlocks && (window as any).SnakkSyntax) {
                (window as any).SnakkSyntax.highlightAll(container);
            }
            observeNewPosts();
            data.items.forEach(post => loadReactionsForPost(post.publicId));
        }

        // Hide sentinel once we've loaded all the way back to the beginning
        if (postsStartOffset <= 0) {
            document.getElementById('load-up-sentinel')?.classList.add('hidden');
            loadUpObserver?.disconnect();
            loadUpObserver = null;
        }
    } catch (err) {
        console.error('Failed to load earlier posts:', err);
    } finally {
        postsIsLoadingEarlier = false;
        indicator?.classList.add('hidden');
    }
}

// ===== Thread Navigation Bar =====

function initThreadNav(config: DiscussionConfig): void {
    const pane = document.getElementById('thread-nav');
    if (!pane) return;

    totalPostCount = config.postCount || 0;
    if (totalPostCount <= 1) {
        pane.classList.add('hidden');
        return;
    }

    pane.classList.remove('hidden');

    const progressBar = document.getElementById('thread-nav-progress') as HTMLElement | null;
    const input = document.getElementById('thread-nav-input') as HTMLInputElement | null;
    const totalEl = document.getElementById('thread-nav-total');

    if (totalEl) totalEl.textContent = String(totalPostCount);

    function getCurrentPostNumber(): number {
        const posts = document.querySelectorAll<HTMLElement>('.post-item[data-post-number]');
        let current = 1;
        for (const post of posts) {
            if (post.getBoundingClientRect().top <= 80) {
                current = parseInt(post.dataset.postNumber || '1', 10);
            } else {
                break;
            }
        }
        return current;
    }

    function updateNav(postNumber: number): void {
        const n = Math.max(1, Math.min(postNumber, totalPostCount));
        if (input && document.activeElement !== input) {
            input.value = String(n);
        }
        if (progressBar) {
            progressBar.style.width = `${(n / totalPostCount) * 100}%`;
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
        const current = getCurrentPostNumber();
        let target: number;
        if (action === 'first') target = 1;
        else if (action === 'prev') target = Math.max(1, current - postsPageSize);
        else if (action === 'next') target = Math.min(totalPostCount, current + postsPageSize);
        else if (action === 'last') target = totalPostCount;
        else return;

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
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        const newHash = n <= 1 ? '' : `#post-${n}`;
        history.replaceState(null, '', location.pathname + newHash);
        setTimeout(() => { suppressFragmentUpdate = false; }, 500);
        return;
    }

    // Need to load — use fragment entry mechanism
    history.replaceState(null, '', location.pathname + `#post-${n}`);
    handleFragmentEntry(config.discussionId, config.currentUserId || '', config.isAuthenticated, config.isLocked);
}

// ===== Initialize Discussion Page =====
function initDiscussionPage(config: DiscussionConfig): void {
    // Reset editor state for HTMX navigation (DOM was swapped, old editor is gone)
    editorInitPromise = null;

    // Set endless scroll state from config
    postsCurrentOffset = config.postsCurrentOffset;
    postsHasMoreItems = config.postsHasMoreItems;

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

    // Track post visibility for read state (IntersectionObserver — no scroll polling)
    initReadObserver();

    // Apply hidden users filter
    applyHiddenUsers();

    // Store discussionId for deferred editor init
    activeDiscussionId = config.discussionId || null;

    // Lazy-load markdown editor when scrolled into view
    const editorContainer = document.getElementById('editor-container');
    if (editorContainer) {
        const editorObserver = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    editorObserver.disconnect();
                    initReplyEditor();
                    break;
                }
            }
        }, { rootMargin: '200px' });
        editorObserver.observe(editorContainer);
    }

    // Highlight code blocks in initial page load
    if (config.hasCodeBlocks && (window as any).SnakkSyntax) {
        (window as any).SnakkSyntax.highlightAll();
    }

    // Load follow status
    if (config.discussionId) {
        loadFollowStatus(config.discussionId);
        loadMuteStatus(config.discussionId);
    }

    // Initialize endless scroll if enabled
    if (config.preferEndlessScroll) {
        initPostsEndlessScroll();
        // Fragment navigation: load correct page when entering via #post-N link
        handleFragmentEntry(config.discussionId, config.currentUserId || '', config.isAuthenticated, config.isLocked);
        initFragmentTracking();
        // Thread navigation bar (osu-style pagination)
        initThreadNav(config);
    }

    // Initialize keyboard navigation
    initKeyboardNavigation();

    // Setup event listeners
    setupEventListeners();

}

function setupEventListeners(): void {
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
        if (picker && !picker.classList.contains('hidden') && !picker.contains(target) && !target.closest('[aria-label="Add reaction to post"]')) {
            hideReactionPicker();
        }
    });

    // Toggle spoiler reveal on click
    document.addEventListener('click', (event) => {
        const spoiler = (event.target as HTMLElement).closest('.spoiler') as HTMLElement | null;
        if (spoiler) {
            spoiler.classList.toggle('revealed');
        }
    });

    // Show reason description when selected
    document.getElementById('report-reason')?.addEventListener('change', function(this: HTMLSelectElement) {
        const selectedOption = this.options[this.selectedIndex];
        const description = selectedOption?.dataset?.description;
        const descDiv = document.getElementById('report-reason-description');

        if (description) {
            if (descDiv) descDiv.textContent = description;
            descDiv?.classList.remove('hidden');
        } else {
            descDiv?.classList.add('hidden');
        }
    });
}

// ===== Event Delegation =====
// Handle all data-action clicks
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
        case 'edit-post':
            editPost(action.dataset.postId || '', action.dataset.userId || '');
            break;
        case 'submit-edit':
            submitEdit(action.dataset.postId || '', action.dataset.userId || '');
            break;
        case 'cancel-edit':
            cancelEdit(action.dataset.postId || '');
            break;
        case 'highlight-post':
            highlightPost(action.dataset.postId || '');
            break;

        // Reaction actions
        case 'toggle-reaction-picker':
            toggleReactionPicker(action.dataset.postId || '');
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
                // Clipboard API unavailable — silently ignore
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
        case 'jump-to-unread':
            jumpToUnread();
            break;

        // User actions
        case 'hide-posts-from-user':
            hidePostsFromUser(action.dataset.userId || '', action.dataset.userName || '');
            break;
        case 'unhide-user':
            unhideUser(action.dataset.userId || '');
            break;

        // Load actions
        case 'retry-load-posts':
            retryLoadPosts(
                action.dataset.discussionId || '',
                action.dataset.currentUserId || '',
                action.dataset.isAuthenticated === 'true',
                action.dataset.isLocked === 'true',
                action.dataset.preferEndlessScroll === 'true'
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
                action.dataset.type || '',
                action.dataset.targetId || '',
                action.dataset.description || '',
                action.dataset.spaceId
            );
            break;
    }
});

// Handle form submissions with data-action
document.addEventListener('submit', async (e) => {
    const form = e.target as HTMLFormElement;
    if (!form.dataset.action) return;

    e.preventDefault();

    switch (form.dataset.action) {
        case 'submit-report':
            await submitReport(e);
            break;
    }
});

// Handle textarea input for auto-grow (inline edit textareas)
document.addEventListener('input', (e) => {
    const target = e.target as HTMLElement;
    if (target.matches && target.matches('textarea[data-auto-grow]')) {
        autoGrow(target as HTMLTextAreaElement);
    }
});

// Export minimal API for programmatic access
(window as any).SnakkDiscussion = {
    init: initDiscussionPage,
    loadReactions: loadAllReactions,
    loadMorePosts: loadMorePosts
};

// Expose legacy functions for backwards compatibility (can be removed later)
// These are kept for any inline onclick handlers that haven't been migrated yet
(window as any).autoGrow = autoGrow;
(window as any).replyToPost = replyToPost;
(window as any).quotePost = quotePost;
(window as any).editPost = editPost;
(window as any).submitEdit = submitEdit;
(window as any).cancelEdit = cancelEdit;
(window as any).toggleReaction = toggleReaction;
(window as any).toggleReactionPicker = toggleReactionPicker;
(window as any).toggleFollowDiscussion = toggleFollowDiscussion;
(window as any).openReportModal = openReportModal;
(window as any).submitReport = submitReport;
(window as any).initDiscussionPage = initDiscussionPage;
(window as any).highlightPost = highlightPost;
(window as any).unhideUser = unhideUser;
(window as any).hidePostsFromUser = hidePostsFromUser;

})();
