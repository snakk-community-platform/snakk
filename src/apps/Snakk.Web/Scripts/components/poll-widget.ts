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
    isSecret: boolean;
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
        const showResults = (hasVoted || pollData.isClosed || !isAuthenticated) && !(pollData.isSecret && !pollData.isClosed);
        const maxVotes = Math.max(...pollData.options.map(o => o.voteCount), 1);

        // Color shades for each option (subtle variation)
        const barColors = [
            'oklch(0.55 0.15 155)',  // primary-500
            'oklch(0.62 0.13 155)',  // primary-400
            'oklch(0.69 0.11 155)',  // primary-300
            'oklch(0.55 0.12 200)',  // blue-ish
            'oklch(0.60 0.10 200)',
            'oklch(0.55 0.14 130)',  // warm green
            'oklch(0.62 0.12 130)',
            'oklch(0.55 0.10 260)',  // purple-ish
            'oklch(0.60 0.08 260)',
            'oklch(0.55 0.12 30)',   // amber
        ];

        let html = '<div class="poll-options">';
        const sorted = pollData.options.sort((a, b) => a.displayOrder - b.displayOrder);

        for (let idx = 0; idx < sorted.length; idx++) {
            const option = sorted[idx]!;
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
            const barColor = barColors[idx % barColors.length];

            html += `<div class="poll-option${selectedClass}${winnerClass}${closedClass}" data-option-id="${option.id}"
                          style="--poll-bar-color: ${barColor}"
                          ${clickable ? `data-action="poll-toggle-option" data-option-id="${option.id}"` : ''}
                          ${showResults ? `title="${escapeHtml(option.text)}: ${option.voteCount} vote${option.voteCount !== 1 ? 's' : ''} (${percent}%)"` : ''}>`;

            html += '<div class="poll-option-label">';

            if (showResults && isSelected) {
                html += '<svg class="poll-option-check" xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>';
            } else if (!showResults && clickable) {
                const inputType = pollData.allowMultiple ? 'checkbox' : 'radio';
                html += `<input type="${inputType}" ${isLocalSelected || isSelected ? 'checked' : ''} class="poll-option-input" tabindex="-1" />`;
            }

            html += `<span class="poll-option-text">${escapeHtml(option.text)}</span>`;

            if (showResults) {
                html += `<span class="poll-option-stats"><span class="poll-option-pct">${percent}%</span><span class="poll-option-votes">(${option.voteCount})</span></span>`;
            }

            html += '</div>';

            if (showResults) {
                html += `<div class="poll-option-bar-row"><div class="poll-option-track"><div class="poll-option-fill" data-target-width="${percent}"></div></div></div>`;
            }

            html += '</div>';
        }

        html += '</div>';

        if (pollData.isSecret && !pollData.isClosed && hasVoted) {
            html += '<div class="text-sm text-base-content/50 mt-2">✓ Your vote has been recorded. Results will be revealed when the poll closes.</div>';
        }

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
                const d = Math.floor(diffMs / (1000 * 60 * 60 * 24));
                const h = Math.floor((diffMs % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
                const m = Math.floor((diffMs % (1000 * 60 * 60)) / (1000 * 60));
                const s = Math.floor((diffMs % (1000 * 60)) / 1000);
                html += `<span class="poll-closes-in" data-countdown-to="${pollData.closesAt}">Closes in <span class="countdown font-mono"><span data-unit="days" style="--value:${d};">${d}</span>d <span data-unit="hours" style="--value:${h};">${h}</span>h <span data-unit="minutes" style="--value:${m};">${m}</span>m <span data-unit="seconds" style="--value:${s};">${s}</span>s</span></span>`;
            }
        }

        if (!hasVoted && isAuthenticated && !pollData.isClosed) {
            html += `<button class="btn btn-primary btn-sm" data-action="poll-submit-vote">Vote</button>`;
        } else if (hasVoted && pollData.allowChangeVote && !pollData.isClosed) {
            html += `<button class="btn btn-ghost btn-xs" data-action="poll-change-vote">Change vote</button>`;
        }

        html += '</div>';

        container.innerHTML = html;

        // Animate bars from 0 → target width
        requestAnimationFrame(() => {
            container.querySelectorAll<HTMLElement>('.poll-option-fill[data-target-width]').forEach(fill => {
                fill.style.width = fill.dataset.targetWidth + '%';
            });
        });
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
