/**
 * Frontpage (Index) JavaScript
 * Manages discussion previews
 */

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    /**
     * Discussion Preview Feature
     * Uses event delegation on #discussions-container so HTMX-loaded items work automatically.
     */
    function initDiscussionPreviews(): void {
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
                return previewCache.get(discussionId) || null;
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
                const previewContent = previewDiv.querySelector<HTMLElement>('.preview-content');
                if (!previewContent) return;

                if (previewContent.textContent) {
                    previewDiv.classList.remove('hidden');
                    button.classList.add('active');
                } else {
                    previewContent.innerHTML = '<span class="loading loading-spinner loading-sm"></span>';
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

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            initDiscussionPreviews();
        });
    } else {
        initDiscussionPreviews();
    }
})();
