# Modern JavaScript: When to Use Classes

## Overview

Classes are best for components with **state** and **behavior**. IIFEs are fine for simple utilities.

---

## Example 1: Sidebar Scrollbar (Current is Good!)

### Current (IIFE) - ✅ Fine as-is
```javascript
(function() {
    'use strict';
    function checkSidebarScrollbar() {
        const sidebar = document.getElementById('sidebar');
        if (!sidebar) return;

        if (sidebar.scrollHeight > sidebar.clientHeight) {
            sidebar.classList.add('has-scrollbar');
        } else {
            sidebar.classList.remove('has-scrollbar');
        }
    }

    document.addEventListener('DOMContentLoaded', checkSidebarScrollbar);
    window.addEventListener('resize', checkSidebarScrollbar);
    document.addEventListener('htmx:afterSwap', checkSidebarScrollbar);
})();
```

**Why IIFE is fine:** No state, simple utility, one instance, fire-and-forget.

---

## Example 2: Draft Manager (Should be a Class!)

### Before (Functional with Globals)
```javascript
(function() {
    let autoSaveTimer = null;
    let currentDraftKey = null;

    function saveDraft(discussionId, content) { /* ... */ }
    function startAutoSave(discussionId) { /* ... */ }

    window.SnakkDraftManager = {
        saveDraft,
        startAutoSave,
        // ... more
    };
})();
```

### After (ES6 Class) - ⭐ Better!
```javascript
class DraftManager {
    constructor(options = {}) {
        this.storageKey = options.storageKey || 'snakk_drafts';
        this.autoSaveInterval = options.autoSaveInterval || 5000;
        this.maxAge = options.maxAge || 7 * 24 * 60 * 60 * 1000; // 7 days

        this.autoSaveTimer = null;
        this.currentDraftKey = null;

        this.pruneOldDrafts();
    }

    getDraftKey(discussionId, replyToPostId = null) {
        return replyToPostId
            ? `${discussionId}:reply:${replyToPostId}`
            : `${discussionId}:post`;
    }

    getAllDrafts() {
        try {
            return JSON.parse(localStorage.getItem(this.storageKey) || '{}');
        } catch (e) {
            console.error('Failed to load drafts:', e);
            return {};
        }
    }

    saveDraft(discussionId, content, replyToPostId = null) {
        if (!content || content.trim().length === 0) {
            this.deleteDraft(discussionId, replyToPostId);
            return;
        }

        const drafts = this.getAllDrafts();
        const key = this.getDraftKey(discussionId, replyToPostId);

        drafts[key] = {
            content,
            discussionId,
            replyToPostId,
            savedAt: Date.now()
        };

        localStorage.setItem(this.storageKey, JSON.stringify(drafts));

        // Dispatch custom event
        this.dispatchEvent('draft:saved', { key, content });
    }

    startAutoSave(discussionId, textarea, getReplyToPostId) {
        this.stopAutoSave();
        this.currentDraftKey = discussionId;

        this.autoSaveTimer = setInterval(() => {
            const content = textarea.value;
            const replyToPostId = getReplyToPostId ? getReplyToPostId() : null;
            this.saveDraft(discussionId, content, replyToPostId);
        }, this.autoSaveInterval);

        // Save on blur
        textarea.addEventListener('blur', () => {
            const content = textarea.value;
            const replyToPostId = getReplyToPostId ? getReplyToPostId() : null;
            this.saveDraft(discussionId, content, replyToPostId);
        });
    }

    stopAutoSave() {
        if (this.autoSaveTimer) {
            clearInterval(this.autoSaveTimer);
            this.autoSaveTimer = null;
        }
        this.currentDraftKey = null;
    }

    restoreDraft(discussionId, textarea, replyToPostId = null) {
        const draft = this.getDraft(discussionId, replyToPostId);
        if (!draft || !draft.content) return false;

        textarea.value = draft.content;
        this.showRestoreIndicator();
        return true;
    }

    deleteDraft(discussionId, replyToPostId = null) {
        const drafts = this.getAllDrafts();
        const key = this.getDraftKey(discussionId, replyToPostId);
        delete drafts[key];
        localStorage.setItem(this.storageKey, JSON.stringify(drafts));

        this.dispatchEvent('draft:deleted', { key });
    }

    pruneOldDrafts() {
        const drafts = this.getAllDrafts();
        let changed = false;

        for (const [key, draft] of Object.entries(drafts)) {
            const age = Date.now() - draft.savedAt;
            if (age > this.maxAge) {
                delete drafts[key];
                changed = true;
            }
        }

        if (changed) {
            localStorage.setItem(this.storageKey, JSON.stringify(drafts));
        }
    }

    dispatchEvent(name, detail = {}) {
        document.dispatchEvent(new CustomEvent(`snakk:${name}`, { detail }));
    }

    destroy() {
        this.stopAutoSave();
    }
}

// Export as singleton (common pattern)
window.SnakkDraftManager = new DraftManager();

// Or export class for multiple instances
window.DraftManager = DraftManager;
```

**Benefits:**
- ✅ Clear constructor with options
- ✅ State encapsulated in instance
- ✅ Easy to test (create instance, call methods)
- ✅ Multiple instances possible
- ✅ Clear lifecycle (constructor, destroy)
- ✅ Private methods with #privateName (if needed)

---

## Example 3: Selection Quote Handler

### Before (Functional)
```javascript
let currentSelection = { postId: null, text: '', authorName: '' };

function showSelectionQuoteButton() { /* ... */ }
function hideSelectionQuoteButton() { /* ... */ }
```

### After (Class) - ⭐ Much Better!
```javascript
class SelectionQuoteHandler {
    constructor(options = {}) {
        this.buttonId = options.buttonId || 'selection-quote-btn';
        this.buttonClass = options.buttonClass || 'btn btn-xs btn-primary';
        this.minSelectionLength = options.minSelectionLength || 3;

        this.currentSelection = {
            postId: null,
            text: '',
            authorName: ''
        };

        this.button = null;

        this.init();
    }

    init() {
        document.addEventListener('mouseup', (e) => this.handleMouseUp(e));
        document.addEventListener('mousedown', (e) => this.handleMouseDown(e));
        document.addEventListener('scroll', () => this.hide(), { passive: true });
    }

    handleMouseUp(e) {
        setTimeout(() => {
            const selection = window.getSelection();
            const selectedText = selection.toString().trim();

            if (!selectedText || selectedText.length < this.minSelectionLength) {
                this.reset();
                return;
            }

            if (!selection.rangeCount) return;

            const range = selection.getRangeAt(0);
            const postContentDiv = this.findPostContentDiv(range.commonAncestorContainer);

            if (postContentDiv) {
                const postId = postContentDiv.id.replace('post-content-', '');
                const authorName = postContentDiv.dataset.authorName || 'Unknown';

                this.currentSelection = { postId, text: selectedText, authorName };
                this.show(range);
            } else {
                this.reset();
            }
        }, 10);
    }

    handleMouseDown(e) {
        if (this.button && !this.button.contains(e.target)) {
            this.hide();
        }
    }

    findPostContentDiv(node) {
        const element = node.nodeType === Node.TEXT_NODE
            ? node.parentElement
            : node;
        return element?.closest('[id^="post-content-"]');
    }

    show(range) {
        this.hide(); // Remove any existing button

        const rect = range.getBoundingClientRect();
        if (rect.width === 0 && rect.height === 0) return;

        this.button = document.createElement('button');
        this.button.id = this.buttonId;
        this.button.className = `fixed z-50 ${this.buttonClass}`;
        this.button.textContent = 'Quote selection';

        const left = Math.max(10, rect.left);
        const top = rect.bottom + window.scrollY + 5;

        this.button.style.left = `${left}px`;
        this.button.style.top = `${top}px`;

        this.button.onmousedown = (e) => e.preventDefault();
        this.button.onclick = (e) => {
            e.preventDefault();
            e.stopPropagation();
            this.handleQuote();
        };

        document.body.appendChild(this.button);
    }

    hide() {
        if (this.button) {
            this.button.remove();
            this.button = null;
        }
    }

    reset() {
        this.currentSelection = { postId: null, text: '', authorName: '' };
        this.hide();
    }

    handleQuote() {
        // Dispatch custom event with selection data
        document.dispatchEvent(new CustomEvent('snakk:quote-selection', {
            detail: this.currentSelection
        }));

        this.reset();
        window.getSelection().removeAllRanges();
    }

    destroy() {
        this.hide();
        // Remove event listeners if needed (would need to store bound references)
    }
}

// Usage
const selectionHandler = new SelectionQuoteHandler({
    minSelectionLength: 5,
    buttonClass: 'btn btn-primary btn-sm'
});
```

---

## Example 4: Toast Notification System

### Modern Class Pattern
```javascript
class Toast {
    constructor(message, options = {}) {
        this.message = message;
        this.type = options.type || 'info';
        this.duration = options.duration || 4000;
        this.position = options.position || 'bottom-right';
        this.dismissible = options.dismissible !== false;

        this.element = null;
        this.timeoutId = null;

        this.create();
    }

    create() {
        this.element = document.createElement('div');
        this.element.className = this.getClasses();
        this.element.innerHTML = this.getHTML();

        if (this.dismissible) {
            const closeBtn = this.element.querySelector('[data-dismiss]');
            closeBtn.addEventListener('click', () => this.dismiss());
        }

        document.body.appendChild(this.element);

        // Animate in
        requestAnimationFrame(() => {
            this.element.style.opacity = '1';
            this.element.style.transform = 'translateX(0)';
        });

        // Auto-dismiss
        if (this.duration > 0) {
            this.timeoutId = setTimeout(() => this.dismiss(), this.duration);
        }
    }

    getClasses() {
        const positions = {
            'top-right': 'top-6 right-6',
            'bottom-right': 'bottom-6 right-6',
            // ... more
        };

        const colors = {
            'success': 'bg-success',
            'error': 'bg-error',
            'warning': 'bg-warning',
            'info': 'bg-info'
        };

        return `fixed ${positions[this.position]} ${colors[this.type]} text-white px-4 py-3 rounded-lg shadow-lg z-50 flex items-center gap-3 transition-all duration-300`;
    }

    getHTML() {
        return `
            <span class="flex-1">${this.escapeHtml(this.message)}</span>
            ${this.dismissible ? '<button data-dismiss>×</button>' : ''}
        `;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    dismiss() {
        if (this.timeoutId) {
            clearTimeout(this.timeoutId);
        }

        this.element.style.opacity = '0';
        this.element.style.transform = this.position.includes('right')
            ? 'translateX(400px)'
            : 'translateX(-400px)';

        setTimeout(() => {
            this.element.remove();
        }, 300);
    }
}

// Toast Manager (Singleton)
class ToastManager {
    constructor() {
        this.toasts = [];
    }

    show(message, options) {
        const toast = new Toast(message, options);
        this.toasts.push(toast);
        return toast;
    }

    success(message, options = {}) {
        return this.show(message, { ...options, type: 'success' });
    }

    error(message, options = {}) {
        return this.show(message, { ...options, type: 'error' });
    }

    warning(message, options = {}) {
        return this.show(message, { ...options, type: 'warning' });
    }

    info(message, options = {}) {
        return this.show(message, { ...options, type: 'info' });
    }

    dismissAll() {
        this.toasts.forEach(toast => toast.dismiss());
        this.toasts = [];
    }
}

// Export singleton
window.toast = new ToastManager();

// Usage
toast.success('User created!');
toast.error('Failed to save', { duration: 6000 });
```

---

## Example 5: Reaction System

### Modern Class Pattern
```javascript
class ReactionSystem {
    constructor(options = {}) {
        this.apiBaseUrl = options.apiBaseUrl || window.apiBaseUrl;
        this.emojis = options.emojis || {
            ThumbsUp: '👍',
            Heart: '❤️',
            Eyes: '👀'
        };

        this.currentPickerPostId = null;
        this.picker = null;

        this.init();
    }

    init() {
        // Event delegation for reactions
        document.addEventListener('click', (e) => {
            const action = e.target.closest('[data-action^="reaction"]');
            if (!action) return;

            e.preventDefault();

            if (action.dataset.action === 'reaction-picker') {
                this.togglePicker(action.dataset.postId);
            } else if (action.dataset.action === 'reaction-toggle') {
                this.toggle(action.dataset.postId, action.dataset.type);
            }
        });

        // Close picker on outside click
        document.addEventListener('click', (e) => {
            if (this.picker && !this.picker.contains(e.target) && !e.target.closest('[data-action="reaction-picker"]')) {
                this.closePicker();
            }
        });
    }

    async toggle(postId, reactionType) {
        this.closePicker();

        const bar = document.getElementById(`reactions-${postId}`);
        const originalHTML = bar?.innerHTML;

        // Optimistic update
        this.optimisticUpdate(postId, reactionType);

        try {
            const response = await fetch(`/bff/posts/${postId}/reactions`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ emoji: reactionType }),
                credentials: 'include'
            });

            if (!response.ok) {
                // Revert on error
                if (bar && originalHTML) {
                    bar.innerHTML = originalHTML;
                }
                this.showError('Failed to update reaction');
                return;
            }

            // Refresh to get accurate counts
            await this.loadForPost(postId);
        } catch (err) {
            // Revert on error
            if (bar && originalHTML) {
                bar.innerHTML = originalHTML;
            }
            this.showError('Network error');
        }
    }

    optimisticUpdate(postId, reactionType) {
        const bar = document.getElementById(`reactions-${postId}`);
        const button = bar?.querySelector(`[data-type="${reactionType}"]`);

        if (button) {
            const isActive = button.classList.contains('active');
            const countSpan = button.querySelector('.count');
            const currentCount = parseInt(countSpan?.textContent || '0');

            if (isActive) {
                button.classList.remove('active');
                const newCount = currentCount - 1;
                if (newCount === 0) {
                    button.remove();
                } else if (countSpan) {
                    countSpan.textContent = newCount;
                }
            } else {
                button.classList.add('active');
                if (countSpan) {
                    countSpan.textContent = currentCount + 1;
                }
            }
        }
    }

    async loadForPost(postId) {
        const bar = document.getElementById(`reactions-${postId}`);
        if (!bar) return;

        try {
            const [countsRes, myRes] = await Promise.all([
                fetch(`/bff/posts/${postId}/reactions`),
                fetch(`/bff/posts/${postId}/reactions/me`, { credentials: 'include' })
            ]);

            const counts = await countsRes.json();
            const myReaction = await myRes.json();

            bar.innerHTML = this.renderBar(postId, counts, myReaction);
        } catch (err) {
            console.error('Failed to load reactions:', err);
        }
    }

    renderBar(postId, counts, myReaction) {
        let html = '';

        const keyMap = { ThumbsUp: 'thumbsUp', Heart: 'heart', Eyes: 'eyes' };

        for (const [type, emoji] of Object.entries(this.emojis)) {
            const count = counts[keyMap[type]] || 0;
            if (count > 0) {
                const isActive = myReaction.reaction === type ? 'active' : '';
                html += `<button type="button"
                               class="reaction-pill ${isActive}"
                               data-action="reaction-toggle"
                               data-post-id="${postId}"
                               data-type="${type}">
                            ${emoji} <span class="count">${count}</span>
                         </button>`;
            }
        }

        if (!myReaction.reaction) {
            html += `<button type="button"
                           class="reaction-pill add-reaction"
                           data-action="reaction-picker"
                           data-post-id="${postId}"
                           title="Add reaction">+</button>`;
        }

        return html;
    }

    togglePicker(postId) {
        if (this.currentPickerPostId === postId && this.picker) {
            this.closePicker();
        } else {
            this.showPicker(postId);
        }
    }

    showPicker(postId) {
        this.closePicker();

        this.currentPickerPostId = postId;
        this.picker = document.createElement('div');
        this.picker.id = 'reaction-picker';
        this.picker.className = 'reaction-picker';

        const button = document.querySelector(`[data-action="reaction-picker"][data-post-id="${postId}"]`);
        if (button) {
            const rect = button.getBoundingClientRect();
            this.picker.style.left = `${rect.left}px`;
            this.picker.style.top = `${rect.bottom + 5}px`;
        }

        this.picker.innerHTML = Object.entries(this.emojis)
            .map(([type, emoji]) => `
                <button type="button"
                        data-action="reaction-toggle"
                        data-post-id="${postId}"
                        data-type="${type}">${emoji}</button>
            `).join('');

        document.body.appendChild(this.picker);
    }

    closePicker() {
        if (this.picker) {
            this.picker.remove();
            this.picker = null;
        }
        this.currentPickerPostId = null;
    }

    showError(message) {
        // Use toast system or custom error display
        if (window.toast) {
            window.toast.error(message);
        }
    }

    destroy() {
        this.closePicker();
    }
}

// Export singleton
window.reactions = new ReactionSystem();
```

---

## Decision Matrix: Class vs IIFE

| Factor | Use IIFE | Use Class |
|--------|----------|-----------|
| Has state | ❌ No | ✅ Yes |
| Multiple instances | ❌ No | ✅ Possible |
| Lifecycle management | ❌ Simple | ✅ Complex |
| Reusability | ❌ Limited | ✅ High |
| Testability | ⚠️ OK | ✅ Easy |
| Configuration | ⚠️ Global vars | ✅ Constructor |

---

## Best Practices for ES6 Classes

### 1. Constructor Options Pattern
```javascript
class Component {
    constructor(options = {}) {
        // Destructure with defaults
        const {
            element = null,
            autoInit = true,
            onUpdate = null
        } = options;

        this.element = element;
        this.onUpdate = onUpdate;

        if (autoInit) {
            this.init();
        }
    }
}
```

### 2. Private Methods (ES2022)
```javascript
class Component {
    #privateMethod() {
        // Truly private
    }

    publicMethod() {
        this.#privateMethod();
    }
}
```

### 3. Static Methods
```javascript
class Validator {
    static isEmail(email) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
    }

    static isUrl(url) {
        try {
            new URL(url);
            return true;
        } catch {
            return false;
        }
    }
}

// Usage
if (Validator.isEmail('test@example.com')) { }
```

### 4. Getters and Setters
```javascript
class User {
    constructor(firstName, lastName) {
        this.firstName = firstName;
        this.lastName = lastName;
    }

    get fullName() {
        return `${this.firstName} ${this.lastName}`;
    }

    set fullName(name) {
        [this.firstName, this.lastName] = name.split(' ');
    }
}

const user = new User('John', 'Doe');
console.log(user.fullName); // "John Doe"
user.fullName = 'Jane Smith';
```

### 5. Singleton Pattern
```javascript
class Config {
    constructor() {
        if (Config.instance) {
            return Config.instance;
        }

        this.settings = {};
        Config.instance = this;
    }

    set(key, value) {
        this.settings[key] = value;
    }

    get(key) {
        return this.settings[key];
    }
}

// Always returns same instance
const config1 = new Config();
const config2 = new Config();
console.log(config1 === config2); // true
```

---

## Recommendation for Snakk

### Keep as IIFE:
- ✅ sidebar-scrollbar.js
- ✅ search-focus.js
- ✅ theme.js
- ✅ Simple utilities

### Convert to Classes:
- ⭐ draft-manager.js → DraftManager class
- ⭐ Selection quote handler → SelectionQuoteHandler class
- ⭐ Reaction system → ReactionSystem class
- ⭐ Notification badge → NotificationBadge class

### Mixed Approach (Current is fine):
- ✅ auth.js - Functions exported as object (fine)
- ✅ utils.js - Collection of utilities (fine)
- ✅ actions.js - Event delegation (fine)

---

## Summary

**IIFE Pattern:**
- ✅ Good for: Simple utilities, one-time init, stateless functions
- ✅ Used in: sidebar-scrollbar.js, theme.js

**Class Pattern:**
- ⭐ Better for: Stateful components, reusable instances, complex behavior
- ⭐ Use for: DraftManager, SelectionHandler, ReactionSystem, Toast

**Current Code Quality:** 8/10
- Modern patterns used
- Room for classes where appropriate
- But current approach works well!

Would you like me to convert any specific files to use classes?
