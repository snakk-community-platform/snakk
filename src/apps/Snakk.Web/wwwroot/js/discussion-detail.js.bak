// Discussion Detail Page JavaScript

// ===== Editor Functions =====

// Auto-grow textarea
function autoGrow(element) {
    element.style.height = 'auto';
    element.style.height = Math.max(96, element.scrollHeight) + 'px'; // min-h-24 = 96px
}

// Insert markup around selection or at cursor
function insertMarkup(before, after) {
    const textarea = document.getElementById('post-content-input');
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
function insertLinePrefix(prefix) {
    const textarea = document.getElementById('post-content-input');
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
function handleEditorKeydown(event) {
    // Ctrl+Enter to submit
    if (event.ctrlKey && event.key === 'Enter') {
        event.preventDefault();
        document.getElementById('reply-form').submit();
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
function togglePreview(show) {
    previewVisible = show;
    const textarea = document.getElementById('post-content-input');
    const previewPanel = document.getElementById('preview-panel');

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
let previewTimeout = null;
function updatePreviewDebounced() {
    if (!previewVisible) return;
    clearTimeout(previewTimeout);
    previewTimeout = setTimeout(updatePreview, 300);
}

function updatePreview() {
    if (!previewVisible) return;
    const content = document.getElementById('post-content-input').value;
    const previewContent = document.getElementById('preview-content');

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
function replyToPost(postId, authorName) {
    document.getElementById('reply-to-post-id').value = postId;
    document.getElementById('reply-context').classList.remove('hidden');
    document.getElementById('reply-context-author').textContent = authorName;
    const textarea = document.getElementById('post-content-input');
    textarea.focus();
    autoGrow(textarea);
    document.getElementById('reply-form-container').scrollIntoView({ behavior: 'smooth', block: 'center' });
}

// Quote a post's content (or selected text)
function quotePost(postId, content, authorName) {
    const textarea = document.getElementById('post-content-input');
    const quote = `> ${authorName} wrote:\n> ${content.split('\n').join('\n> ')}\n\n`;
    textarea.value = quote + textarea.value;
    replyToPost(postId, authorName);
    autoGrow(textarea);
    updatePreviewDebounced();
}

// ===== Smart Selection Quote =====

// Track current selection for smart quoting
let currentSelection = { postId: null, text: '', authorName: '' };

function hideSelectionQuoteButton() {
    const btn = document.getElementById('selection-quote-btn');
    if (btn) btn.remove();
}

function showSelectionQuoteButton() {
    hideSelectionQuoteButton(); // Remove any existing

    const selection = window.getSelection();
    if (!selection.rangeCount || !currentSelection.text) return;

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
        quotePost(currentSelection.postId, currentSelection.text, currentSelection.authorName);
        hideSelectionQuoteButton();
        window.getSelection().removeAllRanges();
    };

    document.body.appendChild(button);
}

// Clear reply context
function clearReplyContext() {
    document.getElementById('reply-to-post-id').value = '';
    document.getElementById('reply-context').classList.add('hidden');
}

// Highlight a referenced post when clicking quote
function highlightPost(postId) {
    const post = document.getElementById('post-' + postId);
    if (post) {
        post.classList.add('post-highlight');
        setTimeout(() => post.classList.remove('post-highlight'), 2000);
    }
}

// Edit post
function editPost(postId, userId) {
    const contentDiv = document.getElementById('post-content-' + postId);
    const rawContent = contentDiv.dataset.rawContent || '';
    const originalHtml = contentDiv.innerHTML;

    // Store original state for cancel
    contentDiv.dataset.originalHtml = originalHtml;

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

    const textarea = document.getElementById('edit-textarea-' + postId);
    autoGrow(textarea);
    textarea.focus();
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function submitEdit(postId, userId) {
    const textarea = document.getElementById('edit-textarea-' + postId);
    const content = textarea.value;

    fetch(`/api/posts/${postId}/edit?userId=${userId}&content=${encodeURIComponent(content)}`, {
        method: 'POST'
    })
    .then(response => response.text())
    .then(html => {
        document.getElementById('post-' + postId).outerHTML = html;
    })
    .catch(error => {
        alert('Error updating post: ' + error);
    });
}

function cancelEdit(postId) {
    const contentDiv = document.getElementById('post-content-' + postId);
    const originalHtml = contentDiv.dataset.originalHtml;
    if (originalHtml) {
        contentDiv.innerHTML = originalHtml;
        delete contentDiv.dataset.originalHtml;
    }
}

// Jump to unread functionality
let lastReadPostId = null;

function jumpToUnread() {
    if (lastReadPostId) {
        // Find the next post after lastReadPostId
        const posts = document.querySelectorAll('.post-item');
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
let markReadTimeout = null;
function markPostsAsRead() {
    clearTimeout(markReadTimeout);
    markReadTimeout = setTimeout(() => {
        const posts = document.querySelectorAll('.post-item');
        let lastVisiblePostId = null;

        for (const post of posts) {
            const rect = post.getBoundingClientRect();
            if (rect.bottom > 0 && rect.top < window.innerHeight) {
                lastVisiblePostId = post.dataset.postId;
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
function initDraftAutoSave(discussionId) {
    const textarea = document.getElementById('post-content-input');
    if (!textarea || !window.SnakkDraftManager) return;

    // Restore draft if exists
    const getReplyToPostId = () => {
        return document.getElementById('reply-to-post-id')?.value || null;
    };

    const restored = window.SnakkDraftManager.restoreDraft(discussionId, textarea, getReplyToPostId());

    // Start auto-save
    window.SnakkDraftManager.startAutoSave(discussionId, textarea, getReplyToPostId);

    // Clear draft on successful post
    const form = document.getElementById('reply-form');
    if (form) {
        form.addEventListener('submit', function(e) {
            // Clear draft after a short delay (to ensure post succeeded)
            setTimeout(() => {
                const replyToPostId = getReplyToPostId();
                window.SnakkDraftManager.clearDraftOnSuccess(discussionId, replyToPostId);
            }, 1000);
        });
    }
}

// ===== Reactions System =====
let currentReactionPostId = null;
const reactionEmojis = { ThumbsUp: '👍', Heart: '❤️', Eyes: '👀' };

function toggleReactionPicker(postId) {
    const picker = document.getElementById('reaction-picker');
    const btn = document.querySelector(`#reactions-${postId} .add-reaction`);

    if (currentReactionPostId === postId && !picker.classList.contains('hidden')) {
        picker.classList.add('hidden');
        currentReactionPostId = null;
        return;
    }

    currentReactionPostId = postId;
    if (btn) {
        const rect = btn.getBoundingClientRect();
        picker.style.left = `${rect.left}px`;
        // picker is position:fixed, so use viewport-relative coordinates (no scrollY)
        picker.style.top = `${rect.bottom + 5}px`;
    }
    picker.classList.remove('hidden');
}

async function toggleReaction(postId, reactionType) {
    const picker = document.getElementById('reaction-picker');
    picker.classList.add('hidden');
    currentReactionPostId = null;

    if (!postId) {
        console.error('toggleReaction called with no postId');
        return;
    }

    const reactionsBar = document.getElementById(`reactions-${postId}`);
    const originalHTML = reactionsBar?.innerHTML;

    // Optimistic UI update - toggle the reaction immediately
    const button = reactionsBar?.querySelector(`[data-type="${reactionType}"]`);
    if (button) {
        const isActive = button.classList.contains('active');
        const countSpan = button.querySelector('.count');
        const currentCount = parseInt(countSpan?.textContent || '0');

        if (isActive) {
            // Remove reaction
            button.classList.remove('active');
            const newCount = currentCount - 1;
            if (newCount === 0 && countSpan) {
                button.remove();
            } else if (countSpan) {
                countSpan.textContent = newCount;
            }
        } else {
            // Add reaction
            button.classList.add('active');
            if (countSpan) {
                countSpan.textContent = currentCount + 1;
            }
        }
    }

    const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';

    try {
        const response = await fetch(`/bff/posts/${postId}/reactions`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ emoji: reactionType }),
            credentials: 'include'
        });

        if (!response.ok) {
            // Revert optimistic update on error
            if (reactionsBar && originalHTML) {
                reactionsBar.innerHTML = originalHTML;
            }
            const errorText = await response.text();
            console.error('Failed to toggle reaction:', response.status, errorText);
            showToast('Failed to update reaction. Please try again.', 'error');
            return;
        }

        // Refresh reactions for this post to ensure accuracy
        await loadReactionsForPost(postId);
    } catch (err) {
        // Revert optimistic update on error
        if (reactionsBar && originalHTML) {
            reactionsBar.innerHTML = originalHTML;
        }
        console.error('Error toggling reaction:', err);
        showToast('Network error. Please check your connection.', 'error');
    }
}

async function loadReactionsForPost(postId) {
    const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';
    const reactionsBar = document.getElementById(`reactions-${postId}`);
    if (!reactionsBar) return;

    try {
        // Get counts
        const countsResponse = await fetch(`/bff/posts/${postId}/reactions`);
        const counts = await countsResponse.json();

        // Get user's reaction
        const myResponse = await fetch(`/bff/posts/${postId}/reactions/me`, { credentials: 'include' });
        const myReaction = await myResponse.json();

        // Rebuild reactions bar
        let html = '';

        // Add reactions with counts
        // API returns camelCase keys: thumbsUp, heart, eyes
        const keyMap = { ThumbsUp: 'thumbsUp', Heart: 'heart', Eyes: 'eyes' };
        for (const [type, emoji] of Object.entries(reactionEmojis)) {
            const count = counts[keyMap[type]] || 0;
            if (count > 0) {
                const isActive = myReaction.reaction === type ? 'active' : '';
                html += `<button type="button" class="reaction-pill ${isActive}" data-type="${type}" onclick="event.preventDefault(); event.stopPropagation(); toggleReaction('${postId}', '${type}'); return false;">${emoji} <span class="count">${count}</span></button>`;
            }
        }

        // Add the "+" button only if user hasn't reacted yet
        if (!myReaction.reaction) {
            html += `<button type="button" class="reaction-pill add-reaction" onclick="event.preventDefault(); event.stopPropagation(); toggleReactionPicker('${postId}'); return false;" title="Add reaction">+</button>`;
        }

        reactionsBar.innerHTML = html;
    } catch (err) {
        console.error('Error loading reactions:', err);
    }
}

// Load reactions for all posts on page load
function loadAllReactions() {
    document.querySelectorAll('[id^="reactions-"]').forEach(bar => {
        const postId = bar.id.replace('reactions-', '');
        loadReactionsForPost(postId);
    });
}

// ===== Follow Discussion =====
async function toggleFollowDiscussion(discussionId) {
    const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';
    const btn = document.getElementById('follow-btn');
    const text = document.getElementById('follow-text');

    // Optimistic UI update - toggle immediately
    const currentlyFollowing = btn.classList.contains('btn-primary');
    const newFollowingState = !currentlyFollowing;
    updateFollowButton(newFollowingState);

    // Update cache optimistically
    if (window.SnakkFollowCache) {
        window.SnakkFollowCache.setDiscussionFollowed(discussionId, newFollowingState);
    }

    try {
        const response = await fetch(`/bff/discussions/${discussionId}/follow`, {
            method: 'POST',
            credentials: 'include'
        });

        if (!response.ok) {
            // Revert optimistic update on error
            updateFollowButton(currentlyFollowing);
            if (window.SnakkFollowCache) {
                window.SnakkFollowCache.setDiscussionFollowed(discussionId, currentlyFollowing);
            }
            console.error('Failed to toggle follow');
            showToast('Failed to update follow status. Please try again.', 'error');
            return;
        }

        const result = await response.json();
        // Update to actual server state (should match optimistic update)
        updateFollowButton(result.isFollowing);
        if (window.SnakkFollowCache) {
            window.SnakkFollowCache.setDiscussionFollowed(discussionId, result.isFollowing);
        }
    } catch (err) {
        // Revert optimistic update on error
        updateFollowButton(currentlyFollowing);
        if (window.SnakkFollowCache) {
            window.SnakkFollowCache.setDiscussionFollowed(discussionId, currentlyFollowing);
        }
        console.error('Error toggling follow:', err);
        showToast('Network error. Please check your connection.', 'error');
    }
}

function updateFollowButton(isFollowing) {
    const btn = document.getElementById('follow-btn');
    const text = document.getElementById('follow-text');
    const icon = document.getElementById('follow-icon');

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

async function loadFollowStatus(discussionId) {
    // Check cache first
    if (window.SnakkFollowCache) {
        const cached = window.SnakkFollowCache.isDiscussionFollowed(discussionId);
        if (cached !== null) {
            updateFollowButton(cached);
            return; // Use cached value, skip API call
        }
    }

    // Cache miss or not available, fetch from API
    const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';
    try {
        const response = await fetch(`/bff/discussions/${discussionId}/follow-status`, { credentials: 'include' });
        const result = await response.json();
        updateFollowButton(result.isFollowing);

        // Update cache
        if (window.SnakkFollowCache) {
            window.SnakkFollowCache.setDiscussionFollowed(discussionId, result.isFollowing);
        }
    } catch (err) {
        // Not logged in or error - leave as default
    }
}

// ===== Mute Discussion =====
function toggleMuteDiscussion(discussionId) {
    const mutedDiscussions = JSON.parse(localStorage.getItem('mutedDiscussions') || '[]');
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

function updateMuteButton(isMuted) {
    const btn = document.getElementById('mute-discussion-btn');
    const text = document.getElementById('mute-text');

    if (isMuted) {
        text.textContent = 'Unmute discussion';
    } else {
        text.textContent = 'Mute discussion';
    }
}

function loadMuteStatus(discussionId) {
    const mutedDiscussions = JSON.parse(localStorage.getItem('mutedDiscussions') || '[]');
    const isMuted = mutedDiscussions.includes(discussionId);
    updateMuteButton(isMuted);
}

// ===== Hide Posts From User =====
function hidePostsFromUser(userId, userName) {
    const hiddenUsers = JSON.parse(localStorage.getItem('hiddenUsers') || '[]');

    if (!hiddenUsers.includes(userId)) {
        hiddenUsers.push(userId);
        localStorage.setItem('hiddenUsers', JSON.stringify(hiddenUsers));

        // Hide all posts from this user
        document.querySelectorAll(`[data-author-id="${userId}"]`).forEach(post => {
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

function unhideUser(userId) {
    const hiddenUsers = JSON.parse(localStorage.getItem('hiddenUsers') || '[]');
    const index = hiddenUsers.indexOf(userId);

    if (index > -1) {
        hiddenUsers.splice(index, 1);
        localStorage.setItem('hiddenUsers', JSON.stringify(hiddenUsers));

        // Show all posts from this user
        document.querySelectorAll(`[data-author-id="${userId}"]`).forEach(post => {
            post.style.display = '';
        });

        // Remove any notification banners
        document.querySelectorAll('.fixed.top-20').forEach(banner => banner.remove());
    }
}

function applyHiddenUsers() {
    const hiddenUsers = JSON.parse(localStorage.getItem('hiddenUsers') || '[]');

    hiddenUsers.forEach(userId => {
        document.querySelectorAll(`[data-author-id="${userId}"]`).forEach(post => {
            post.style.display = 'none';
        });
    });
}

// ===== Typing Indicator =====
let typingTimeout = null;
let isTyping = false;

function notifyTyping() {
    if (!isTyping) {
        isTyping = true;
        // TODO: Send typing start notification via SignalR
        // connection.invoke('StartTyping', discussionId);
    }

    clearTimeout(typingTimeout);
    typingTimeout = setTimeout(() => {
        isTyping = false;
        // TODO: Send typing stop notification via SignalR
        // connection.invoke('StopTyping', discussionId);
    }, 2000);
}

function showTypingIndicator(users) {
    const indicator = document.getElementById('typing-indicator');
    const usersSpan = document.getElementById('typing-users');

    if (users && users.length > 0) {
        if (users.length === 1) {
            usersSpan.textContent = `${users[0]} is typing...`;
        } else if (users.length === 2) {
            usersSpan.textContent = `${users[0]} and ${users[1]} are typing...`;
        } else {
            usersSpan.textContent = `${users.length} people are typing...`;
        }
        indicator.classList.remove('hidden');
    } else {
        indicator.classList.add('hidden');
    }
}

// ===== Keyboard Navigation =====
let currentPostIndex = -1;
const posts = [];

function initKeyboardNavigation() {
    // Build posts array for navigation
    document.querySelectorAll('.post-article').forEach(post => {
        posts.push(post);
    });

    document.addEventListener('keydown', (e) => {
        // Don't intercept if user is typing in an input/textarea
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') {
            return;
        }

        // Don't intercept if modals/pickers are open
        const picker = document.getElementById('reaction-picker');
        if (picker && !picker.classList.contains('hidden')) {
            if (e.key === 'Escape') {
                picker.classList.add('hidden');
                currentReactionPostId = null;
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
                const composer = document.getElementById('comment-input');
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

function navigateToPost(index) {
    // Clear previous selection
    if (currentPostIndex >= 0 && posts[currentPostIndex]) {
        posts[currentPostIndex].classList.remove('keyboard-selected');
    }

    // Clamp index
    if (index < 0) index = 0;
    if (index >= posts.length) index = posts.length - 1;

    currentPostIndex = index;

    if (posts[currentPostIndex]) {
        posts[currentPostIndex].classList.add('keyboard-selected');
        posts[currentPostIndex].scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

// ===== Toast Notifications =====
function showToast(message, type = 'error', duration = 4000) {
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
let postsScrollObserver = null;

function initPostsEndlessScroll() {
    const sentinel = document.getElementById('scroll-sentinel');
    if (!sentinel) return;

    // Disconnect previous observer if it exists
    if (postsScrollObserver) {
        postsScrollObserver.disconnect();
    }

    postsScrollObserver = new IntersectionObserver((entries) => {
        if (entries[0].isIntersecting && postsHasMoreItems && !postsIsLoading) {
            loadMorePosts();
        }
    }, { rootMargin: '100px' });

    postsScrollObserver.observe(sentinel);
}

async function loadMorePosts(discussionId, currentUserId, isAuthenticated, isLocked) {
    if (postsIsLoading || !postsHasMoreItems) return;
    postsIsLoading = true;

    const loadingIndicator = document.getElementById('loading-indicator');
    const endMessage = document.getElementById('end-message');
    loadingIndicator?.classList.remove('hidden');

    const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';

    try {
        const response = await fetch(
            `${apiBaseUrl}/api/discussions/${discussionId}/posts?offset=${postsCurrentOffset}&pageSize=${postsPageSize}`,
            { credentials: 'include' }
        );

        if (!response.ok) throw new Error('Failed to load posts');

        const data = await response.json();
        const container = document.getElementById('posts-container');
        const sentinel = document.getElementById('scroll-sentinel');

        if (data.items && data.items.length > 0) {
            // Track previous author for grouping
            const existingPosts = container.querySelectorAll('.post-item');
            let previousAuthorId = existingPosts.length > 0
                ? existingPosts[existingPosts.length - 1].dataset.authorId
                : null;

            const newPostIds = [];
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

function retryLoadPosts(discussionId, currentUserId, isAuthenticated, isLocked, preferEndlessScroll) {
    const errorMessage = document.getElementById('load-error-message');
    errorMessage?.classList.add('hidden');
    // Reinitialize endless scroll
    if (preferEndlessScroll) {
        initPostsEndlessScroll();
    }
    // Trigger load immediately
    loadMorePosts(discussionId, currentUserId, isAuthenticated, isLocked);
}

async function renderPostContent(postId) {
    const contentDiv = document.getElementById(`post-content-${postId}`);
    if (!contentDiv) return;

    const rawContent = contentDiv.dataset.rawContent;
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

function formatPostRelativeTime(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    const now = new Date();
    const diff = now - date;
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

function createPostElement(post, isSameAuthorAsPrevious, currentUserId, isAuthenticated, isLocked) {
    const article = document.createElement('article');
    article.id = `post-${post.publicId}`;
    article.className = `post-item post-article group ${post.isFirstPost ? 'first-post' : ''}`;
    article.dataset.authorId = post.author.publicId;
    article.dataset.postId = post.publicId;

    const isOP = post.isFirstPost;
    const hasReplyTo = post.replyTo != null;
    const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';

    let authorBlockHtml = '';
    if (!isSameAuthorAsPrevious) {
        let rolesBadge = '';
        if (post.author.role === 'admin') {
            rolesBadge = '<span class="badge badge-subtle badge-xs ml-1">Admin</span>';
        } else if (post.author.role === 'mod') {
            rolesBadge = '<span class="badge badge-subtle badge-xs ml-1">Mod</span>';
        }
        const opBadge = isOP ? '<span class="badge badge-primary-subtle badge-xs ml-1">OP</span>' : '';
        const editedTag = post.editedAt ? '<span class="ml-1">(edited)</span>' : '';

        if (post.author.isDeleted) {
            authorBlockHtml = `
                <div class="author-block">
                    <div class="author-avatar-simple">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                        </svg>
                    </div>
                    <div class="author-info">
                        <span class="author-name italic text-muted">${post.author.displayName}</span>
                        ${rolesBadge}${opBadge}
                        <div class="post-meta">${formatPostRelativeTime(post.createdAt)}${editedTag}</div>
                    </div>
                </div>`;
        } else {
            authorBlockHtml = `
                <div class="author-block">
                    <div class="author-avatar-simple">
                        <img src="${apiBaseUrl}/avatars/${post.author.publicId}" alt="${post.author.displayName}" loading="lazy" />
                    </div>
                    <div class="author-info">
                        <a href="/u/${post.author.publicId}" class="author-name hover:underline" data-popup-type="user" data-popup-id="${post.author.publicId}" data-popup-name="${post.author.displayName}">${post.author.displayName}</a>
                        ${rolesBadge}${opBadge}
                        <div class="post-meta">${formatPostRelativeTime(post.createdAt)}${editedTag}</div>
                    </div>
                </div>`;
        }
    } else {
        const editedTag = post.editedAt ? '<span class="ml-1">(edited)</span>' : '';
        authorBlockHtml = `<div class="post-meta mb-2">${formatPostRelativeTime(post.createdAt)}${editedTag}</div>`;
    }

    let replyToHtml = '';
    if (hasReplyTo) {
        replyToHtml = `
            <a href="#post-${post.replyTo.postId}" class="editorial-quote block mb-4 text-sm" onclick="highlightPost('${post.replyTo.postId}')">
                <span class="quote-author">${post.replyTo.authorName} wrote:</span>
                <p class="line-clamp-2 mt-1">${escapeHtml(post.replyTo.contentSnippet)}</p>
            </a>`;
    }

    // Render actions (simplified - full action rendering requires server-side markup)
    let actionsHtml = '';
    if (!isLocked && isAuthenticated) {
        actionsHtml = `
            <button onclick="replyToPost('${post.publicId}', '${escapeHtml(post.author.displayName)}')" class="subtle-btn">Reply</button>
            <button onclick="quotePost('${post.publicId}', \`${escapeHtml(post.content).replace(/`/g, '\\`')}\`, '${escapeHtml(post.author.displayName)}')" class="subtle-btn">Quote</button>`;
    }

    article.innerHTML = `
        ${authorBlockHtml}
        ${replyToHtml}
        <div id="post-content-${post.publicId}" class="prose prose-content" data-raw-content="${escapeHtml(post.content)}" data-author-name="${post.author.displayName}">
            ${post.renderedContent || escapeHtml(post.content)}
        </div>
        <div class="reactions-minimal" id="reactions-${post.publicId}">
            <button type="button" class="reaction-pill add-reaction" onclick="event.preventDefault(); event.stopPropagation(); toggleReactionPicker('${post.publicId}'); return false;" title="Add reaction">+</button>
        </div>
        <div class="subtle-actions">
            ${actionsHtml}
        </div>
    `;

    return article;
}

// ===== Report System =====
let reportReasons = [];

async function loadReportReasons(spaceId) {
    const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';

    try {
        let url = `${apiBaseUrl}/api/moderation/reports/reasons`;
        if (spaceId) {
            url += `?spaceId=${spaceId}`;
        }

        const response = await fetch(url, { credentials: 'include' });
        if (!response.ok) throw new Error('Failed to load reasons');

        const data = await response.json();
        reportReasons = data.items || [];

        // Populate the select dropdown
        const select = document.getElementById('report-reason');
        select.innerHTML = '<option value="">Select a reason...</option>';
        reportReasons.forEach(reason => {
            const option = document.createElement('option');
            option.value = reason.publicId;
            option.textContent = reason.name;
            option.dataset.description = reason.description || '';
            select.appendChild(option);
        });
    } catch (err) {
        console.error('Error loading report reasons:', err);
    }
}

function openReportModal(type, targetId, description, spaceId) {
    // Reset the form
    document.getElementById('report-form').reset();
    document.getElementById('report-error').classList.add('hidden');
    document.getElementById('report-success').classList.add('hidden');
    document.getElementById('report-submit-btn').disabled = false;
    document.getElementById('report-submit-text').classList.remove('hidden');
    document.getElementById('report-submit-loading').classList.add('hidden');
    document.getElementById('report-reason-description').classList.add('hidden');

    // Set the target info
    document.getElementById('report-type').value = type;
    document.getElementById('report-target-id').value = targetId;
    document.getElementById('report-target-description').textContent = description;

    // Load reasons if not already loaded
    if (reportReasons.length === 0) {
        loadReportReasons(spaceId);
    }

    // Show the modal
    document.getElementById('report_modal').showModal();
}

async function submitReport(event) {
    event.preventDefault();

    const type = document.getElementById('report-type').value;
    const targetId = document.getElementById('report-target-id').value;
    const reasonId = document.getElementById('report-reason').value;
    const details = document.getElementById('report-details').value;

    if (!reasonId) {
        showReportError('Please select a reason for your report.');
        return;
    }

    // Show loading state
    const submitBtn = document.getElementById('report-submit-btn');
    const submitText = document.getElementById('report-submit-text');
    const submitLoading = document.getElementById('report-submit-loading');

    submitBtn.disabled = true;
    submitText.classList.add('hidden');
    submitLoading.classList.remove('hidden');

    try {
        const requestBody = {
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
        document.getElementById('report-error').classList.add('hidden');
        document.getElementById('report-success').classList.remove('hidden');
        submitBtn.disabled = true;

        // Close the modal after a delay
        setTimeout(() => {
            document.getElementById('report_modal').close();
        }, 2000);

    } catch (err) {
        console.error('Error submitting report:', err);
        showReportError(err.message || 'An error occurred while submitting your report. Please try again.');

        submitBtn.disabled = false;
        submitText.classList.remove('hidden');
        submitLoading.classList.add('hidden');
    }
}

function showReportError(message) {
    const errorDiv = document.getElementById('report-error');
    const errorMessage = document.getElementById('report-error-message');
    errorMessage.textContent = message;
    errorDiv.classList.remove('hidden');
}

// ===== Initialize Discussion Page =====
function initDiscussionPage(config) {
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
}

function setupEventListeners() {
    // Check selection on mouseup anywhere in document
    document.addEventListener('mouseup', (e) => {
        // Small delay to let selection finalize
        setTimeout(() => {
            const selection = window.getSelection();
            const selectedText = selection.toString().trim();

            if (!selectedText || selectedText.length < 3) {
                currentSelection = { postId: null, text: '', authorName: '' };
                hideSelectionQuoteButton();
                return;
            }

            // Check if selection is within a post content div
            if (!selection.rangeCount) return;

            const range = selection.getRangeAt(0);
            const container = range.commonAncestorContainer;

            // Find the post content div
            const postContentDiv = container.nodeType === Node.TEXT_NODE
                ? container.parentElement?.closest('[id^="post-content-"]')
                : container.closest?.('[id^="post-content-"]');

            if (postContentDiv) {
                const postId = postContentDiv.id.replace('post-content-', '');
                const authorName = postContentDiv.dataset.authorName || 'Unknown';

                currentSelection = { postId, text: selectedText, authorName };
                showSelectionQuoteButton();
            } else {
                currentSelection = { postId: null, text: '', authorName: '' };
                hideSelectionQuoteButton();
            }
        }, 10);
    });

    // Hide quote button when clicking elsewhere (but not on the button itself)
    document.addEventListener('mousedown', (e) => {
        const btn = document.getElementById('selection-quote-btn');
        if (btn && !btn.contains(e.target)) {
            hideSelectionQuoteButton();
        }
    });

    // Hide on scroll
    document.addEventListener('scroll', hideSelectionQuoteButton, { passive: true });

    // Hide reaction picker when clicking outside
    document.addEventListener('click', (e) => {
        const picker = document.getElementById('reaction-picker');
        if (picker && !picker.contains(e.target) && !e.target.closest('.add-reaction')) {
            picker.classList.add('hidden');
            currentReactionPostId = null;
        }
    });

    // Show reason description when selected
    document.getElementById('report-reason')?.addEventListener('change', function() {
        const selectedOption = this.options[this.selectedIndex];
        const description = selectedOption?.dataset?.description;
        const descDiv = document.getElementById('report-reason-description');

        if (description) {
            descDiv.textContent = description;
            descDiv.classList.remove('hidden');
        } else {
            descDiv.classList.add('hidden');
        }
    });
}

// Expose necessary functions globally for onclick handlers
window.autoGrow = autoGrow;
window.insertMarkup = insertMarkup;
window.insertLinePrefix = insertLinePrefix;
window.handleEditorKeydown = handleEditorKeydown;
window.togglePreview = togglePreview;
window.replyToPost = replyToPost;
window.quotePost = quotePost;
window.clearReplyContext = clearReplyContext;
window.highlightPost = highlightPost;
window.editPost = editPost;
window.submitEdit = submitEdit;
window.cancelEdit = cancelEdit;
window.jumpToUnread = jumpToUnread;
window.toggleReactionPicker = toggleReactionPicker;
window.toggleReaction = toggleReaction;
window.toggleFollowDiscussion = toggleFollowDiscussion;
window.toggleMuteDiscussion = toggleMuteDiscussion;
window.hidePostsFromUser = hidePostsFromUser;
window.unhideUser = unhideUser;
window.retryLoadPosts = retryLoadPosts;
window.openReportModal = openReportModal;
window.submitReport = submitReport;
window.loadMorePosts = loadMorePosts;
window.initDiscussionPage = initDiscussionPage;
