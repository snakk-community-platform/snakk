/**
 * Discussion Preview Component
 * Shared preview-popover feature used on frontpage, hub detail, community detail, and space detail.
 * Auto-initializes on DOM ready. Safe to load on any page — exits early if #discussions-container is absent.
 */

(function(): void {
    'use strict';

    function initDiscussionPreviews(): void {
        if (document.documentElement.classList.contains('no-discussion-previews')) return;

        const container = document.getElementById('discussions-container');
        if (!container) return;

        const previewCache = new Map<string, string>();

        function truncateText(text: string, maxLength: number): string {
            if (text.length <= maxLength) return text;

            let truncated = text.substring(0, maxLength);
            const lastSpace = truncated.lastIndexOf(' ');

            if (lastSpace > 0) {
                truncated = truncated.substring(0, lastSpace);
            }

            return truncated + '...';
        }

        async function fetchPreview(discussionId: string): Promise<string | null> {
            if (previewCache.has(discussionId)) {
                const cached = previewCache.get(discussionId);
                return cached !== undefined ? cached : null;
            }

            try {
                const response = await fetch(`/bff/discussions/${discussionId}/preview`);
                if (!response.ok) {
                    throw new Error('Failed to fetch preview');
                }

                const data = await response.json() as { content: string };
                previewCache.set(discussionId, data.content);
                return data.content;
            } catch (error) {
                console.error('Error fetching preview:', error);
                return null;
            }
        }

        function togglePreview(button: HTMLElement, previewDiv: HTMLElement, discussionId: string): void {
            const isCurrentlyVisible = !previewDiv.classList.contains('hidden');

            if (isCurrentlyVisible) {
                previewDiv.classList.add('hidden');
                button.classList.remove('active');
            } else {
                const previewContent = previewDiv.querySelector('.preview-content') as HTMLElement | null;
                if (!previewContent) return;

                if (previewContent.textContent) {
                    previewDiv.classList.remove('hidden');
                    button.classList.add('active');
                } else {
                    previewContent.innerHTML = '<div class="skeleton h-3 w-full rounded"></div><div class="skeleton h-3 w-3/4 rounded mt-2"></div>';
                    previewDiv.classList.remove('hidden');
                    button.classList.add('active');

                    fetchPreview(discussionId).then(content => {
                        if (content) {
                            previewContent.textContent = truncateText(content, 480);
                        } else {
                            previewContent.textContent = 'Failed to load preview';
                        }
                    });
                }
            }
        }

        // Event delegation: catches clicks on both initial and HTMX-loaded preview buttons
        container.addEventListener('click', (e: MouseEvent) => {
            const target = e.target as HTMLElement;
            const button = target.closest('.preview-btn') as HTMLElement | null;
            if (!button || !button.dataset.discussionId) return;

            e.preventDefault();
            const discussionId = button.dataset.discussionId;
            const wrapper = button.closest('.topic-item-wrapper');
            const previewDiv = wrapper?.querySelector('.discussion-preview') as HTMLElement | null;

            if (previewDiv) {
                togglePreview(button, previewDiv, discussionId);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initDiscussionPreviews);
    } else {
        initDiscussionPreviews();
    }
})();
