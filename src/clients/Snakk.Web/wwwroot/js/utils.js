// Snakk Utility Functions

/**
 * Format a date as relative time (e.g., "2m ago", "5h ago", "3d ago")
 */
function formatRelativeTime(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'just now';
    if (diffMins < 60) return diffMins + 'm ago';
    if (diffHours < 24) return diffHours + 'h ago';
    if (diffDays < 7) return diffDays + 'd ago';
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}

/**
 * Format a number with k/M suffix (e.g., 1.2k, 2.5M)
 */
function formatCount(count) {
    if (count == null) return '0';
    if (count >= 1000000) {
        return (count / 1000000).toFixed(1).replace(/\.0$/, '') + 'M';
    }
    if (count >= 1000) {
        return (count / 1000).toFixed(1).replace(/\.0$/, '') + 'k';
    }
    return count.toString();
}

/**
 * Escape HTML to prevent XSS
 */
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/**
 * Format badges HTML for a discussion (pinned, locked, tags)
 */
function formatDiscussionBadges(discussion) {
    let badges = '';

    if (discussion.isPinned) {
        badges += '<span class="badge badge-primary-subtle badge-xs ml-2">Pinned</span>';
    }
    if (discussion.isLocked) {
        badges += '<span class="badge badge-warning-subtle badge-xs ml-2">Locked</span>';
    }
    if (discussion.tags && Array.isArray(discussion.tags) && discussion.tags.length > 0) {
        discussion.tags.slice(0, 3).forEach(tag => {
            badges += `<span class="badge badge-ghost badge-xs ml-2">${escapeHtml(tag)}</span>`;
        });
        if (discussion.tags.length > 3) {
            badges += '<span class="text-muted ml-1">...</span>';
        }
    }

    return badges;
}
