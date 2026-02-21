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
    publicId: string;
    content: string;
    renderedContent?: string;
    author: PostAuthor;
    createdAt: string;
    editedAt?: string;
    isFirstPost: boolean;
    replyTo?: PostReplyTo;
}

interface ReactionCounts {
    thumbsUp?: number;
    heart?: number;
    eyes?: number;
    crazy?: number;
}

interface MyReaction {
    reaction: string | null;
}

interface FollowStatus {
    isFollowing: boolean;
}

interface DiscussionConfig {
    discussionId: string;
    isAuthenticated: boolean;
    isLocked: boolean;
    preferEndlessScroll: boolean;
    postsCurrentOffset: number;
    postsHasMoreItems: boolean;
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

// Auto-grow textarea
function autoGrow(element: HTMLTextAreaElement): void {
    element.style.height = 'auto';
    element.style.height = Math.max(96, element.scrollHeight) + 'px'; // min-h-24 = 96px
}

// Insert markup around selection or at cursor
function insertMarkup(before: string, after: string): void {
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const text = textarea.value;
    const selected = text.substring(start, end);

    textarea.value = text.substring(0, start) + before + selected + after + text.substring(end);

    // Position cursor appropriately
    if (selected) {
        textarea.selectionStart = start;
        textarea.selectionEnd = start + before.length + selected.length + after.length;
    } else {
        textarea.selectionStart = textarea.selectionEnd = start + before.length;
    }
    textarea.focus();
    autoGrow(textarea);
    updatePreviewDebounced();
}

// Insert prefix at start of current line
function insertLinePrefix(prefix: string): void {
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const text = textarea.value;

    // Find start of current line
    let lineStart = start;
    while (lineStart > 0 && text[lineStart - 1] !== '\n') {
        lineStart--;
    }

    textarea.value = text.substring(0, lineStart) + prefix + text.substring(lineStart);
    textarea.selectionStart = textarea.selectionEnd = start + prefix.length;
    textarea.focus();
    autoGrow(textarea);
    updatePreviewDebounced();
}

// Handle keyboard shortcuts
function handleEditorKeydown(event: KeyboardEvent): void {
    const form = document.getElementById('reply-form') as HTMLFormElement | null;

    // Ctrl+Enter to submit
    if (event.ctrlKey && event.key === 'Enter') {
        event.preventDefault();
        form?.submit();
        return;
    }
    // Ctrl+B for bold
    if (event.ctrlKey && event.key === 'b') {
        event.preventDefault();
        insertMarkup('**', '**');
        return;
    }
    // Ctrl+I for italic
    if (event.ctrlKey && event.key === 'i') {
        event.preventDefault();
        insertMarkup('*', '*');
        return;
    }
    // Ctrl+K for link
    if (event.ctrlKey && event.key === 'k') {
        event.preventDefault();
        insertMarkup('[', '](url)');
        return;
    }
}

// Preview toggle
let previewVisible = false;
function togglePreview(show: boolean): void {
    previewVisible = show;
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
    const previewPanel = document.getElementById('preview-panel');

    if (!textarea || !previewPanel) return;

    if (show) {
        previewPanel.classList.remove('hidden');
        textarea.style.display = 'none';
        updatePreview();
    } else {
        previewPanel.classList.add('hidden');
        textarea.style.display = '';
        textarea.focus();
    }
}

// Update preview via htmx
let previewTimeout: ReturnType<typeof setTimeout> | null = null;
function updatePreviewDebounced(): void {
    if (!previewVisible) return;
    if (previewTimeout) clearTimeout(previewTimeout);
    previewTimeout = setTimeout(updatePreview, 300);
}

function updatePreview(): void {
    if (!previewVisible) return;
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
    const previewContent = document.getElementById('preview-content');
    if (!textarea || !previewContent) return;

    const content = textarea.value;

    if (!content.trim()) {
        previewContent.innerHTML = '<p class="text-base-content/50 italic">Nothing to preview</p>';
        return;
    }

    fetch(`/bff/markup/preview`, {
        method: 'POST',
        body: content,
        headers: { 'Content-Type': 'text/plain' },
        credentials: 'include'
    })
    .then(response => response.text())
    .then(html => {
        previewContent.innerHTML = html;
    })
    .catch(() => {
        previewContent.innerHTML = '<p class="text-error">Preview failed</p>';
    });
}

// ===== Reply/Quote Functions =====

// Reply to a specific post
function replyToPost(postId: string, authorName: string): void {
    const replyToInput = document.getElementById('reply-to-post-id') as HTMLInputElement;
    const replyContext = document.getElementById('reply-context');
    const replyContextAuthor = document.getElementById('reply-context-author');
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;

    if (replyToInput) replyToInput.value = postId;
    if (replyContext) replyContext.classList.remove('hidden');
    if (replyContextAuthor) replyContextAuthor.textContent = authorName;
    if (textarea) {
        textarea.focus();
        autoGrow(textarea);
    }
    document.getElementById('reply-form-container')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

// Quote a post's content (or selected text)
function quotePost(postId: string, content: string, authorName: string): void {
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
    if (!textarea) return;

    const quote = `> ${authorName} wrote:\n> ${content.split('\n').join('\n> ')}\n\n`;
    textarea.value = quote + textarea.value;
    replyToPost(postId, authorName);
    autoGrow(textarea);
    updatePreviewDebounced();
}

// ===== Smart Selection Quote =====

// Track current selection for smart quoting
let currentSelection: CurrentSelection = { postId: null, text: '', authorName: '' };

function hideSelectionQuoteButton(): void {
    const btn = document.getElementById('selection-quote-btn');
    if (btn) btn.remove();
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
    const post = document.getElementById('post-' + postId);
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
            <textarea class="textarea textarea-bordered w-full min-h-20 text-sm resize-none"
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

// Mark posts as read on scroll (debounced)
let markReadTimeout: ReturnType<typeof setTimeout> | null = null;
function markPostsAsRead(): void {
    if (markReadTimeout) clearTimeout(markReadTimeout);
    markReadTimeout = setTimeout(() => {
        const posts = document.querySelectorAll<HTMLElement>('.post-item');
        let lastVisiblePostId: string | null = null;

        for (const post of posts) {
            const rect = post.getBoundingClientRect();
            if (rect.bottom > 0 && rect.top < window.innerHeight) {
                lastVisiblePostId = post.dataset.postId || null;
            }
        }

        if (lastVisiblePostId && lastVisiblePostId !== lastReadPostId) {
            // Batch update via read state batcher (reduces API calls)
            const discussionId = document.body.dataset.discussionId;
            if (discussionId && window.SnakkReadStateBatcher) {
                window.SnakkReadStateBatcher.updateReadState(discussionId, lastVisiblePostId);
            }
            lastReadPostId = lastVisiblePostId;
        }
    }, 1000);
}

// Initialize draft auto-save
function initDraftAutoSave(discussionId: string): void {
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement;
    if (!textarea || !(window as any).SnakkDraftManager) return;

    // Restore draft if exists
    const getReplyToPostId = (): string | null => {
        const input = document.getElementById('reply-to-post-id') as HTMLInputElement;
        return input?.value || null;
    };

    (window as any).SnakkDraftManager?.restoreDraft(discussionId, textarea, getReplyToPostId());

    // Start auto-save
    (window as any).SnakkDraftManager?.startAutoSave(discussionId, textarea, getReplyToPostId);

    // Clear draft on successful post
    const form = document.getElementById('reply-form') as HTMLFormElement;
    if (form) {
        form.addEventListener('submit', function() {
            // Clear draft after a short delay (to ensure post succeeded)
            setTimeout(() => {
                const replyToPostId = getReplyToPostId();
                (window as any).SnakkDraftManager?.clearDraftOnSuccess(discussionId, replyToPostId);
            }, 1000);
        });
    }
}

// ===== Reactions System =====
let currentReactionPostId: string | null = null;
const reactionEmojis: Record<string, string> = { ThumbsUp: '👍', Heart: '❤️', Eyes: '👀', Crazy: '🤯' };
const reactionTypeValues: Record<string, number> = { ThumbsUp: 1, Heart: 2, Eyes: 3, Crazy: 4 };
// Maps PascalCase type names to data-count-* attribute suffixes
const reactionDataKeys: Record<string, string> = { ThumbsUp: 'thumbsup', Heart: 'heart', Eyes: 'eyes', Crazy: 'crazy' };
let reactionPickerHideTimer: ReturnType<typeof setTimeout> | null = null;

const smileyPlaceholderSvg = '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>';

// Read all reaction counts from data-attributes
function getReactionCountsFromAttrs(el: HTMLElement): Record<string, number> {
    const counts: Record<string, number> = {};
    for (const [type, dataKey] of Object.entries(reactionDataKeys)) {
        counts[type] = parseInt(el.dataset[`count${dataKey.charAt(0).toUpperCase()}${dataKey.slice(1)}`] || el.getAttribute(`data-count-${dataKey}`) || '0', 10) || 0;
    }
    return counts;
}

// Write reaction counts to data-attributes
function setReactionCountAttrs(el: HTMLElement, counts: Record<string, number>): void {
    for (const [type, dataKey] of Object.entries(reactionDataKeys)) {
        el.setAttribute(`data-count-${dataKey}`, String(counts[type] || 0));
    }
}

// Render reaction spans from data-attributes
function renderReactionCounts(reactionsBar: HTMLElement): void {
    const counts = getReactionCountsFromAttrs(reactionsBar);
    let html = '';
    let hasAny = false;

    for (const [type, emoji] of Object.entries(reactionEmojis)) {
        const count = counts[type] || 0;
        if (count > 0) {
            html += `<span data-type="${type}">${emoji} ${count}</span>`;
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

    // Snapshot data-attrs for revert on error
    const snapshotCounts = getReactionCountsFromAttrs(reactionsBar);
    const snapshotMyReaction = reactionsBar.dataset.myReaction || '';

    // Compute optimistic counts
    const newCounts = { ...snapshotCounts };
    const myReaction = snapshotMyReaction;

    if (myReaction === reactionType) {
        // Toggle off: user clicks the same reaction they already gave
        newCounts[reactionType] = Math.max(0, (newCounts[reactionType] || 0) - 1);
        reactionsBar.dataset.myReaction = '';
    } else {
        if (myReaction) {
            // Change: decrement old reaction, increment new
            newCounts[myReaction] = Math.max(0, (newCounts[myReaction] || 0) - 1);
        }
        // Add new reaction
        newCounts[reactionType] = (newCounts[reactionType] || 0) + 1;
        reactionsBar.dataset.myReaction = reactionType;
    }

    // Write optimistic counts and render immediately
    setReactionCountAttrs(reactionsBar, newCounts);
    renderReactionCounts(reactionsBar);

    try {
        const response = await fetch(`/bff/posts/${postId}/reactions`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ type: reactionTypeValues[reactionType] }),
            credentials: 'include'
        });

        if (!response.ok) {
            // Revert to snapshot on error
            setReactionCountAttrs(reactionsBar, snapshotCounts);
            reactionsBar.dataset.myReaction = snapshotMyReaction;
            renderReactionCounts(reactionsBar);
            const errorText = await response.text();
            console.error('Failed to toggle reaction:', response.status, errorText);
            showToast('Failed to update reaction. Please try again.', 'error');
            return;
        }

        // Refresh from server to get accurate counts
        await loadReactionsForPost(postId);
    } catch (err) {
        // Revert to snapshot on error
        setReactionCountAttrs(reactionsBar, snapshotCounts);
        reactionsBar.dataset.myReaction = snapshotMyReaction;
        renderReactionCounts(reactionsBar);
        console.error('Error toggling reaction:', err);
        showToast('Network error. Please check your connection.', 'error');
    }
}

async function loadReactionsForPost(postId: string): Promise<void> {
    const reactionsBar = document.getElementById(`reactions-${postId}`);
    if (!reactionsBar) return;

    try {
        const countsResponse = await fetch(`/bff/posts/${postId}/reactions`);
        const counts: ReactionCounts = await countsResponse.json();

        // API returns camelCase keys — map to PascalCase for data-attrs
        const keyMap: Record<string, keyof ReactionCounts> = { ThumbsUp: 'thumbsUp', Heart: 'heart', Eyes: 'eyes', Crazy: 'crazy' };
        const serverCounts: Record<string, number> = {};
        for (const [type] of Object.entries(reactionEmojis)) {
            const key = keyMap[type];
            serverCounts[type] = key ? (counts[key] || 0) : 0;
        }

        // Update data-attrs with server truth (preserves data-my-reaction)
        setReactionCountAttrs(reactionsBar, serverCounts);
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
        // Don't intercept if user is typing in an input/textarea
        const target = e.target as HTMLElement;
        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
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

        const data: { items?: Post[]; hasMoreItems: boolean } = await response.json();
        const container = document.getElementById('posts-container');
        const sentinel = document.getElementById('scroll-sentinel');

        if (!container || !sentinel) return;

        if (data.items && data.items.length > 0) {
            // Track previous author for grouping
            const existingPosts = container.querySelectorAll<HTMLElement>('.post-item');
            let previousAuthorId: string | null = existingPosts.length > 0
                ? existingPosts[existingPosts.length - 1]?.dataset.authorId || null
                : null;

            const newPostIds: string[] = [];
            data.items.forEach(post => {
                const isSameAuthor = previousAuthorId === post.author.publicId;
                const postElement = createPostElement(post, isSameAuthor, currentUserId, isAuthenticated, isLocked);
                container.insertBefore(postElement, sentinel);
                previousAuthorId = post.author.publicId;
                newPostIds.push(post.publicId);

                // Load reactions for this new post
                loadReactionsForPost(post.publicId);
            });
            postsCurrentOffset += data.items.length;

            // Render markdown for new posts
            newPostIds.forEach(postId => renderPostContent(postId));
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

async function renderPostContent(postId: string): Promise<void> {
    const contentDiv = document.getElementById(`post-content-${postId}`);
    if (!contentDiv) return;

    const rawContent = (contentDiv as HTMLElement).dataset.rawContent;
    if (!rawContent) return;

    try {
        const response = await fetch(`/bff/markup/preview`, {
            method: 'POST',
            body: rawContent,
            headers: { 'Content-Type': 'text/plain' },
            credentials: 'include'
        });

        if (response.ok) {
            const html = await response.text();
            contentDiv.innerHTML = html;
        }
    } catch (err) {
        console.error('Failed to render post content:', err);
    }
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
    article.id = `post-${post.publicId}`;
    article.className = `post-item post-article group ${post.isFirstPost ? 'first-post' : ''}`;
    article.dataset.authorId = post.author.publicId;
    article.dataset.postId = post.publicId;

    const isOP = post.isFirstPost;
    const hasReplyTo = post.replyTo != null;
    const isOwner = isAuthenticated && currentUserId === post.author.publicId;

    // Build action buttons + dropdown (used in header)
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
    const reactionsContainerHtml = `<div class="flex items-center gap-2 text-xs text-muted${canReact ? ' cursor-pointer' : ''}" id="reactions-${post.publicId}" data-count-thumbsup="0" data-count-heart="0" data-count-eyes="0" data-count-crazy="0" data-my-reaction=""${canReact ? ` onclick="event.preventDefault(); event.stopPropagation(); toggleReactionPicker('${post.publicId}'); return false;"` : ''} aria-label="${canReact ? 'Add reaction to post' : 'Reactions'}">${smileyPlaceholderHtml}</div>`;

    const actionsContainerHtml = isSameAuthorAsPrevious
        ? `<div class="subtle-actions flex-1 flex justify-end gap-1">${actionButtonsHtml}${reactionsContainerHtml}${dropdownHtml}</div>`
        : `<div class="subtle-actions flex items-center gap-1">${actionButtonsHtml}${reactionsContainerHtml}${dropdownHtml}</div>`;

    let headerHtml = '';
    if (!isSameAuthorAsPrevious) {
        let badges = '';
        if (post.author.role === 'admin') {
            badges += '<span class="badge badge-error badge-xs">Admin</span>';
        } else if (post.author.role === 'mod') {
            badges += '<span class="badge badge-info badge-xs">Mod</span>';
        }
        if (isOP) {
            badges += '<span class="badge badge-primary badge-xs">OP</span>';
        }
        const editedTag = post.editedAt ? '<span class="ml-1">(edited)</span>' : '';

        if (post.author.isDeleted) {
            headerHtml = `
                <div class="flex items-start gap-3">
                    <div class="w-8 h-8 rounded-full bg-base-200 flex items-center justify-center flex-shrink-0">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-base-content/50" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                        </svg>
                    </div>
                    <div class="flex-1 min-w-0">
                        <div class="flex items-center gap-2">
                            <span class="text-sm font-semibold italic text-muted">${escapeHtml(post.author.displayName)}</span>
                            ${badges}
                        </div>
                        <div class="text-xs text-muted">${formatPostRelativeTime(post.createdAt)}${editedTag}</div>
                    </div>
                    ${actionsContainerHtml}
                </div>`;
        } else {
            headerHtml = `
                <div class="flex items-start gap-3">
                    <img src="${post.author.avatarUrl || ''}" alt="${escapeHtml(post.author.displayName)}" class="w-8 h-8 rounded-full flex-shrink-0" loading="lazy" />
                    <div class="flex-1 min-w-0">
                        <div class="flex items-center gap-2">
                            <a href="/u/${post.author.publicId}" class="text-sm font-semibold hover:underline" data-popup-type="user" data-popup-id="${post.author.publicId}" data-popup-name="${escapeHtml(post.author.displayName)}">${escapeHtml(post.author.displayName)}</a>
                            ${badges}
                        </div>
                        <div class="text-xs text-muted">${formatPostRelativeTime(post.createdAt)}${editedTag}</div>
                    </div>
                    ${actionsContainerHtml}
                </div>`;
        }
    } else {
        const editedTag = post.editedAt ? '<span>(edited)</span>' : '';
        headerHtml = `
            <div class="flex items-center gap-2 pl-11">
                <span class="text-xs text-muted opacity-0 group-hover:opacity-100 transition-opacity">${formatPostRelativeTime(post.createdAt)}${editedTag}</span>
                ${actionsContainerHtml}
            </div>`;
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
        ${headerHtml}
        <div class="pl-11 mt-1">
            ${replyToHtml}
            <div id="post-content-${post.publicId}" class="prose prose-content" data-raw-content="${escapeHtml(post.content)}" data-author-name="${escapeHtml(post.author.displayName)}">
                ${post.renderedContent || escapeHtml(post.content)}
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

// ===== Sticky Sidebar =====
function initStickySidebar(): void {
    // Only run on desktop (lg breakpoint)
    if (window.innerWidth < 1024) return;

    const sidebar = document.getElementById('sidebar');
    const nav = document.querySelector<HTMLElement>('nav');

    if (!sidebar || !nav) return;

    let sidebarOriginalTop: number | null = null;
    let navHeight = 0;
    let isSticky = false;

    function updateMeasurements(): void {
        if (!sidebar || !nav) return;
        navHeight = nav.offsetHeight;
        const sidebarRect = sidebar.getBoundingClientRect();
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

        if (sidebarOriginalTop === null) {
            sidebarOriginalTop = sidebarRect.top + scrollTop;
        }

        // Set max-height to viewport minus nav height
        sidebar.style.maxHeight = `calc(100vh - ${navHeight}px)`;
    }

    function handleScroll(): void {
        if (!sidebar || sidebarOriginalTop === null) return;
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const triggerPoint = sidebarOriginalTop - navHeight;

        if (scrollTop >= triggerPoint && !isSticky) {
            sidebar.classList.add('sidebar-sticky');
            sidebar.style.top = `calc(${navHeight}px + 1rem)`;
            isSticky = true;
        } else if (scrollTop < triggerPoint && isSticky) {
            sidebar.classList.remove('sidebar-sticky');
            sidebar.style.top = '';
            isSticky = false;
        }
    }

    // Initialize
    updateMeasurements();
    handleScroll();

    // Listen to scroll events (throttled)
    let scrollTimeout: number;
    window.addEventListener('scroll', function() {
        if (scrollTimeout) {
            window.cancelAnimationFrame(scrollTimeout);
        }
        scrollTimeout = window.requestAnimationFrame(function() {
            handleScroll();
        });
    }, { passive: true });

    // Update measurements on resize
    let resizeTimeout: ReturnType<typeof setTimeout>;
    window.addEventListener('resize', function() {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(function() {
            if (!sidebar) return;
            if (window.innerWidth >= 1024) {
                sidebarOriginalTop = null; // Reset to recalculate
                updateMeasurements();
                handleScroll();
            } else {
                // Remove sticky on mobile
                sidebar.classList.remove('sidebar-sticky');
                sidebar.style.top = '';
                sidebar.style.maxHeight = '';
                isSticky = false;
            }
        }, 100);
    });
}

// ===== Initialize Discussion Page =====
function initDiscussionPage(config: DiscussionConfig): void {
    // Set endless scroll state from config
    postsCurrentOffset = config.postsCurrentOffset;
    postsHasMoreItems = config.postsHasMoreItems;

    // Initialize read state batcher
    if (window.SnakkReadStateBatcher) {
        window.SnakkReadStateBatcher.init(config.isAuthenticated);
    }

    // Track scroll for read state
    window.addEventListener('scroll', markPostsAsRead, { passive: true });

    // Initial mark as read
    markPostsAsRead();

    // Apply hidden users filter
    applyHiddenUsers();

    // Load follow status
    if (config.discussionId) {
        loadFollowStatus(config.discussionId);
        loadMuteStatus(config.discussionId);

        // Initialize draft auto-save for reply form
        initDraftAutoSave(config.discussionId);
    }

    // Initialize endless scroll if enabled
    if (config.preferEndlessScroll) {
        initPostsEndlessScroll();
    }

    // Initialize keyboard navigation
    initKeyboardNavigation();

    // Setup event listeners
    setupEventListeners();

    // Initialize sticky sidebar
    initStickySidebar();
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
        // Editor actions
        case 'toggle-preview':
            togglePreview(action.dataset.show === 'true');
            break;
        case 'insert-bold':
            insertMarkup('**', '**');
            break;
        case 'insert-italic':
            insertMarkup('*', '*');
            break;
        case 'insert-link':
            insertMarkup('[', '](url)');
            break;
        case 'insert-code':
            insertMarkup('`', '`');
            break;
        case 'insert-list':
            insertLinePrefix('- ');
            break;

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

// Handle textarea input for auto-grow
document.addEventListener('input', (e) => {
    const target = e.target as HTMLElement;
    if (target.matches && target.matches('textarea[data-auto-grow]')) {
        autoGrow(target as HTMLTextAreaElement);
    }
});

// Handle keyboard shortcuts
document.addEventListener('keydown', (e) => {
    const textarea = document.getElementById('post-content-input') as HTMLTextAreaElement | null;
    if (textarea && e.target === textarea) {
        handleEditorKeydown(e);
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
(window as any).togglePreview = togglePreview;
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
