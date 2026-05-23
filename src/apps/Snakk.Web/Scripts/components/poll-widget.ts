/**
 * Poll Widget — renders poll options with voting and results.
 * Supports segmented polls (group breakdown with two-step voting).
 * Loaded conditionally on Poll-type discussion pages.
 */

interface PollOption {
    id: number;
    text: string;
    voteCount: number;
    displayOrder: number;
}

interface PollOptionSegmentVotes {
    optionId: number;
    segmentACount: number;
    segmentBCount: number;
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
    isSegmented: boolean;
    segmentLabel: string | null;
    segmentOptionA: string | null;
    segmentOptionB: string | null;
    userSegmentIndex: number | null;
    segmentVotes: PollOptionSegmentVotes[];
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
    let selectedSegment: number | null = null;
    let resultView: 'overall' | 'byGroup' = 'overall';

    // Bar colors for options
    const barColors = [
        'oklch(0.55 0.15 155)', 'oklch(0.62 0.13 155)', 'oklch(0.69 0.11 155)',
        'oklch(0.55 0.12 200)', 'oklch(0.60 0.10 200)', 'oklch(0.55 0.14 130)',
        'oklch(0.62 0.12 130)', 'oklch(0.55 0.10 260)', 'oklch(0.60 0.08 260)',
        'oklch(0.55 0.12 30)',
    ];

    // Segment group colors
    const segmentColorA = 'oklch(0.55 0.15 230)'; // blue
    const segmentColorB = 'oklch(0.55 0.15 25)';  // orange/red

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
        const sorted = pollData.options.sort((a, b) => a.displayOrder - b.displayOrder);

        let html = '';

        // Segmented poll: segment selection step (before voting)
        if (pollData.isSegmented && !hasVoted && isAuthenticated && !pollData.isClosed) {
            html += '<div class="poll-segment-selector">';
            html += `<div class="poll-segment-question">${escapeHtml(pollData.segmentLabel || 'Which group are you?')}</div>`;
            html += '<div class="poll-segment-buttons">';
            html += `<button class="poll-segment-btn${selectedSegment === 0 ? ' poll-segment-btn-active' : ''}" data-action="poll-select-segment" data-segment="0">${escapeHtml(pollData.segmentOptionA || 'Group A')}</button>`;
            html += `<button class="poll-segment-btn${selectedSegment === 1 ? ' poll-segment-btn-active' : ''}" data-action="poll-select-segment" data-segment="1">${escapeHtml(pollData.segmentOptionB || 'Group B')}</button>`;
            html += '</div></div>';
        }

        // Post-vote: show user's group + choice
        if (pollData.isSegmented && hasVoted && pollData.userSegmentIndex !== null) {
            const groupName = pollData.userSegmentIndex === 0 ? pollData.segmentOptionA : pollData.segmentOptionB;
            html += `<div class="poll-segment-identity">You voted as: <strong>${escapeHtml(groupName || '')}</strong></div>`;
        }

        // Result view toggle (segmented polls only, when showing results)
        if (pollData.isSegmented && showResults && pollData.segmentVotes?.length > 0) {
            html += '<div class="poll-result-toggle">';
            html += `<button class="poll-result-toggle-btn${resultView === 'overall' ? ' active' : ''}" data-action="poll-set-view" data-view="overall">Overall</button>`;
            html += `<button class="poll-result-toggle-btn${resultView === 'byGroup' ? ' active' : ''}" data-action="poll-set-view" data-view="byGroup">By Group</button>`;
            html += '</div>';
        }

        // Options
        html += '<div class="poll-options">';

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
            const canVote = !pollData.isClosed && isAuthenticated && (!hasVoted || pollData.allowChangeVote || pollData.allowMultiple);
            const clickable = canVote && (!pollData.isSegmented || selectedSegment !== null);
            const barColor = barColors[idx % barColors.length];

            html += `<div class="poll-option${selectedClass}${winnerClass}${closedClass}" data-option-id="${option.id}"
                          style="--poll-bar-color: ${barColor}"
                          ${clickable ? `data-action="poll-toggle-option" data-option-id="${option.id}"` : ''}
                          ${showResults ? `title="${escapeHtml(option.text)}: ${option.voteCount} vote${option.voteCount !== 1 ? 's' : ''} (${percent}%)"` : ''}>`;

            html += '<div class="poll-option-label">';

            if (showResults && isSelected) {
                html += '<span class="icon icon-check poll-option-check" aria-hidden="true"></span>';
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
                if (pollData.isSegmented && resultView === 'byGroup' && pollData.segmentVotes?.length > 0) {
                    // By-group view: two bars per option
                    const sv = pollData.segmentVotes.find(s => s.optionId === option.id);
                    const aCount = sv?.segmentACount || 0;
                    const bCount = sv?.segmentBCount || 0;
                    const totalA = pollData.segmentVotes.reduce((sum, s) => sum + s.segmentACount, 0);
                    const totalB = pollData.segmentVotes.reduce((sum, s) => sum + s.segmentBCount, 0);
                    const aPct = totalA > 0 ? Math.round((aCount / totalA) * 100) : 0;
                    const bPct = totalB > 0 ? Math.round((bCount / totalB) * 100) : 0;
                    const userGroup = pollData.userSegmentIndex;

                    html += '<div class="poll-segment-bars">';
                    html += `<div class="poll-segment-bar-row${userGroup === 0 ? ' poll-segment-highlight' : ''}">`;
                    html += `<span class="poll-segment-bar-label">${escapeHtml(pollData.segmentOptionA || 'A')}</span>`;
                    html += `<div class="poll-option-track"><div class="poll-option-fill" style="--poll-bar-color:${segmentColorA}" data-target-width="${aPct}"></div></div>`;
                    html += `<span class="poll-option-pct">${aPct}%</span>`;
                    html += '</div>';
                    html += `<div class="poll-segment-bar-row${userGroup === 1 ? ' poll-segment-highlight' : ''}">`;
                    html += `<span class="poll-segment-bar-label">${escapeHtml(pollData.segmentOptionB || 'B')}</span>`;
                    html += `<div class="poll-option-track"><div class="poll-option-fill" style="--poll-bar-color:${segmentColorB}" data-target-width="${bPct}"></div></div>`;
                    html += `<span class="poll-option-pct">${bPct}%</span>`;
                    html += '</div>';

                    // Difference insight
                    const diff = Math.abs(aPct - bPct);
                    if (diff >= 10 && (aCount + bCount) > 0) {
                        const leansGroup = aPct > bPct ? pollData.segmentOptionA : pollData.segmentOptionB;
                        html += `<div class="poll-segment-insight">Leans ${escapeHtml(leansGroup || '')} (+${diff}%)</div>`;
                    }

                    html += '</div>';
                } else {
                    // Overall view: single bar
                    html += `<div class="poll-option-bar-row"><div class="poll-option-track"><div class="poll-option-fill" data-target-width="${percent}"></div></div></div>`;
                }
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
                html += `<span class="poll-closes-in" data-countdown-to="${pollData.closesAt}">Closes in <span class="countdown font-mono"><span data-unit="days" style="--value:${d};" aria-label="${d}">${d}</span>d <span data-unit="hours" style="--value:${h};" aria-label="${h}">${h}</span>h <span data-unit="minutes" style="--value:${m};" aria-label="${m}">${m}</span>m <span data-unit="seconds" style="--value:${s};" aria-label="${s}">${s}</span>s</span></span>`;
            }
        }

        if (!hasVoted && isAuthenticated && !pollData.isClosed) {
            const canSubmit = selectedOptionIds.size > 0 && (!pollData.isSegmented || selectedSegment !== null);
            html += `<button class="btn btn-primary btn-sm${canSubmit ? '' : ' btn-disabled'}" ${canSubmit ? 'data-action="poll-submit-vote"' : 'disabled'}>Vote</button>`;
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

        // Start live countdown timer
        startCountdown(container);
    }

    let countdownTimer: ReturnType<typeof setInterval> | null = null;

    function startCountdown(container: HTMLElement): void {
        if (countdownTimer) clearInterval(countdownTimer);

        const el = container.querySelector<HTMLElement>('[data-countdown-to]');
        if (!el) return;

        const target = new Date(el.dataset.countdownTo!).getTime();

        function tick(): void {
            const diff = target - Date.now();
            if (diff <= 0) {
                if (countdownTimer) clearInterval(countdownTimer);
                loadPoll();
                return;
            }

            const d = Math.floor(diff / 86400000);
            const h = Math.floor((diff % 86400000) / 3600000);
            const m = Math.floor((diff % 3600000) / 60000);
            const s = Math.floor((diff % 60000) / 1000);

            el!.querySelectorAll<HTMLElement>('[data-unit]').forEach(span => {
                const unit = span.dataset.unit;
                const val = unit === 'days' ? d : unit === 'hours' ? h : unit === 'minutes' ? m : s;
                span.style.setProperty('--value', String(val));
                span.textContent = String(val);
                span.setAttribute('aria-label', String(val));
            });
        }

        tick();
        countdownTimer = setInterval(tick, 1000);
    }

    function selectSegment(index: number): void {
        selectedSegment = index;
        render();
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

    function setResultView(view: string): void {
        resultView = view === 'byGroup' ? 'byGroup' : 'overall';
        render();
    }

    async function submitVote(): Promise<void> {
        if (!pollData || selectedOptionIds.size === 0) return;
        if (pollData.isSegmented && selectedSegment === null) return;

        const segmentParam = pollData.isSegmented && selectedSegment !== null
            ? `&segmentIndex=${selectedSegment}`
            : '';

        for (const optionId of selectedOptionIds) {
            const response = await fetch(`/bff/discussions/${discussionId}/poll/vote?optionId=${optionId}${segmentParam}`, {
                method: 'POST',
            });

            if (!response.ok) {
                const err = await response.json().catch(() => ({ error: 'Vote failed' }));
                console.error('Vote failed:', err);
            }
        }

        selectedOptionIds.clear();
        selectedSegment = null;
        await loadPoll();
    }

    async function changeVote(): Promise<void> {
        if (!pollData) return;

        for (const optionId of pollData.userVotedOptionIds) {
            await fetch(`/bff/discussions/${discussionId}/poll/vote?optionId=${optionId}`, {
                method: 'DELETE',
            });
        }

        selectedSegment = null;
        resultView = 'overall';
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
        window.SnakkActions.on('poll-select-segment', (el) => {
            const idx = parseInt(el.dataset.segment || '0', 10);
            selectSegment(idx);
        });
        window.SnakkActions.on('poll-set-view', (el) => {
            setResultView(el.dataset.view || 'overall');
        });
    }

    // Auto-load on page ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', loadPoll);
    } else {
        loadPoll();
    }
})();
