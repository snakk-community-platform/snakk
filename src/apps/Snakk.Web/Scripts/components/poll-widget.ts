/**
 * Poll Widget — renders poll options with voting and results.
 * Loaded conditionally on Poll-type discussion pages.
 */

interface PollOption {
    id: number;
    text: string;
    voteCount: number;
    displayOrder: number;
}

interface PollData {
    options: PollOption[];
    allowMultiple: boolean;
    allowChangeVote: boolean;
    closesAt: string | null;
    isClosed: boolean;
    totalVotes: number;
    userVotedOptionIds: number[];
}

(function() {
    'use strict';

    const container = document.getElementById('poll-container');
    if (!container) return;

    const discussionId = container.dataset.discussionId;
    const isAuthenticated = container.dataset.authenticated === 'true';
    if (!discussionId) return;

    const escapeHtml = (window as any).SnakkUtils?.escapeHtml || function(text: string): string {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    };

    let pollData: PollData | null = null;
    let selectedOptionIds: Set<number> = new Set();

    async function loadPoll(): Promise<void> {
        try {
            const response = await fetch(`/bff/discussions/${discussionId}/poll`);
            if (!response.ok) {
                container!.innerHTML = '<div class="text-sm text-base-content/50">Poll data unavailable</div>';
                return;
            }
            pollData = await response.json();
            render();
        } catch {
            container!.innerHTML = '<div class="text-sm text-base-content/50">Failed to load poll</div>';
        }
    }

    function render(): void {
        if (!pollData || !container) return;

        const hasVoted = pollData.userVotedOptionIds.length > 0;
        const showResults = hasVoted || pollData.isClosed || !isAuthenticated;
        const maxVotes = Math.max(...pollData.options.map(o => o.voteCount), 1);

        let html = '<div class="poll-options">';

        for (const option of pollData.options.sort((a, b) => a.displayOrder - b.displayOrder)) {
            const percent = pollData.totalVotes > 0
                ? Math.round((option.voteCount / pollData.totalVotes) * 100)
                : 0;
            const isSelected = pollData.userVotedOptionIds.includes(option.id);
            const isLocalSelected = selectedOptionIds.has(option.id);
            const isWinner = option.voteCount === maxVotes && pollData.totalVotes > 0;

            const selectedClass = isSelected ? ' poll-option-selected' : '';
            const winnerClass = showResults && isWinner ? ' poll-option-winner' : '';
            const closedClass = pollData.isClosed ? ' poll-option-closed' : '';
            const clickable = !pollData.isClosed && isAuthenticated && (!hasVoted || pollData.allowChangeVote || pollData.allowMultiple);

            html += `<div class="poll-option${selectedClass}${winnerClass}${closedClass}" data-option-id="${option.id}"
                          ${clickable ? `data-action="poll-toggle-option" data-option-id="${option.id}"` : ''}>`;

            if (showResults) {
                html += `<div class="poll-option-bar" style="width: ${percent}%"></div>`;
            }

            html += '<div class="poll-option-content">';
            html += '<div class="poll-option-left">';

            if (!showResults && clickable) {
                const inputType = pollData.allowMultiple ? 'checkbox' : 'radio';
                html += `<input type="${inputType}" ${isLocalSelected || isSelected ? 'checked' : ''} class="poll-option-input" tabindex="-1" />`;
            }

            html += `<span class="poll-option-text">${escapeHtml(option.text)}</span>`;
            html += '</div>';

            if (showResults) {
                html += `<span class="poll-option-stats">${option.voteCount} (${percent}%)</span>`;
            }

            html += '</div></div>';
        }

        html += '</div>';

        // Footer
        html += '<div class="poll-footer">';
        html += `<span class="poll-total">${pollData.totalVotes} vote${pollData.totalVotes !== 1 ? 's' : ''}</span>`;

        if (pollData.isClosed) {
            html += '<span class="poll-closed-label">Poll closed</span>';
        } else if (pollData.closesAt) {
            const closesAt = new Date(pollData.closesAt);
            const now = new Date();
            const diffMs = closesAt.getTime() - now.getTime();
            if (diffMs > 0) {
                const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
                const diffHours = Math.floor((diffMs % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
                const timeLeft = diffDays > 0 ? `${diffDays}d ${diffHours}h` : `${diffHours}h`;
                html += `<span class="poll-closes-in">Closes in ${timeLeft}</span>`;
            }
        }

        if (!hasVoted && isAuthenticated && !pollData.isClosed) {
            html += `<button class="btn btn-primary btn-sm" data-action="poll-submit-vote">Vote</button>`;
        } else if (hasVoted && pollData.allowChangeVote && !pollData.isClosed) {
            html += `<button class="btn btn-ghost btn-xs" data-action="poll-change-vote">Change vote</button>`;
        }

        html += '</div>';

        container.innerHTML = html;
    }

    function toggleOption(optionId: number): void {
        if (!pollData) return;

        if (pollData.allowMultiple) {
            if (selectedOptionIds.has(optionId)) {
                selectedOptionIds.delete(optionId);
            } else {
                selectedOptionIds.add(optionId);
            }
        } else {
            selectedOptionIds.clear();
            selectedOptionIds.add(optionId);
        }

        render();
    }

    async function submitVote(): Promise<void> {
        if (!pollData || selectedOptionIds.size === 0) return;

        for (const optionId of selectedOptionIds) {
            const response = await fetch(`/bff/discussions/${discussionId}/poll/vote?optionId=${optionId}`, {
                method: 'POST',
            });

            if (!response.ok) {
                const err = await response.json().catch(() => ({ error: 'Vote failed' }));
                console.error('Vote failed:', err);
            }
        }

        selectedOptionIds.clear();
        await loadPoll();
    }

    async function changeVote(): Promise<void> {
        if (!pollData) return;

        // Remove existing votes first
        for (const optionId of pollData.userVotedOptionIds) {
            await fetch(`/bff/discussions/${discussionId}/poll/vote?optionId=${optionId}`, {
                method: 'DELETE',
            });
        }

        // Reload to show voting UI again
        await loadPoll();
    }

    (window as any).SnakkPoll = { loadPoll, toggleOption, submitVote, changeVote };

    // Register with global action delegation
    if (window.SnakkActions) {
        window.SnakkActions.on('poll-toggle-option', (el) => {
            const optionId = parseInt(el.dataset.optionId || '0', 10);
            if (optionId) toggleOption(optionId);
        });
        window.SnakkActions.on('poll-submit-vote', () => submitVote());
        window.SnakkActions.on('poll-change-vote', () => changeVote());
    }

    // Auto-load on page ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', loadPoll);
    } else {
        loadPoll();
    }
})();
