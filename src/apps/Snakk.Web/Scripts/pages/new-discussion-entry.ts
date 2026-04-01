/**
 * New Discussion Entry Page — space selector + type picker
 * Handles: searchable space dropdown, fetching allowed types, building type cards.
 */

(function () {
    'use strict';

    interface SpaceResult {
        publicId: string;
        name: string;
        slug: string;
        hubSlug: string;
        hubName: string;
        communitySlug: string;
        discussionCount: number;
        communityName: string;
        avatarUrl: string;
    }

    interface TypeInfo {
        slug: string;
        type: number;
        icon: string;
        label: string;
        description: string;
        features: string;
    }

    const TYPE_DATA: TypeInfo[] = [
        { type: 0, slug: 'standard', icon: '', label: 'Discussion', description: 'Start a conversation, share thoughts, or discuss a topic with the community.', features: 'Replies in chronological order, rich text formatting, reactions' },
        { type: 1, slug: 'question', icon: '❓', label: 'Question', description: 'Ask the community a question. The best answer can be marked as the accepted solution.', features: 'Accepted answer highlighting, mark as solved' },
        { type: 2, slug: 'poll', icon: '📊', label: 'Poll', description: 'Put a question to a vote. Add options and see how the community feels in real time.', features: 'Single or multi-vote, optional end date, live results' },
        { type: 4, slug: 'link', icon: '🔗', label: 'Link', description: 'Share an interesting link with the community and start a discussion around it.', features: 'URL with preview, commentary, link metadata' },
        { type: 5, slug: 'gallery', icon: '🖼️', label: 'Gallery', description: 'Share a collection of images with descriptions.', features: 'Image uploads, captions, gallery layout' },
        { type: 6, slug: 'guide', icon: '📖', label: 'Guide', description: 'Write a structured how-to guide or tutorial with step-by-step instructions.', features: 'Summary, prerequisites, numbered steps' },
        { type: 7, slug: 'debate', icon: '⚖️', label: 'Debate', description: 'Pose a topic and let the community argue different sides.', features: '2-3 defined positions, colored labels, filter by side' },
        { type: 8, slug: 'journal', icon: '📓', label: 'Journal', description: 'A living thread where the author posts timestamped updates over time.', features: 'Append-only updates, progress tracking' },
        { type: 9, slug: 'iama', icon: '🎤', label: 'AMA', description: 'Host an Ask Me Anything session. The community asks questions and you answer live.', features: 'Official answers, best questions highlight, verification note' },
    ];

    const selector = document.getElementById('space-selector')!;
    const input = document.getElementById('space-search-input') as HTMLInputElement;
    const dropdown = document.getElementById('space-dropdown')!;
    const hiddenId = document.getElementById('selected-space-id') as HTMLInputElement | null;
    const hiddenSlug = document.getElementById('selected-space-slug') as HTMLInputElement | null;
    const hiddenHubSlug = document.getElementById('selected-hub-slug') as HTMLInputElement | null;
    const hiddenCommunitySlug = document.getElementById('selected-community-slug') as HTMLInputElement | null;
    const typePickerSection = document.getElementById('type-picker-section')!;
    const typeCards = document.getElementById('type-cards')!;

    if (!selector || !input || !dropdown || !typePickerSection || !typeCards) return;

    let scopeHubId = selector.dataset.scopeHubId || '';
    let scopeCommunityId = selector.dataset.scopeCommunityId || '';
    const scopeHubName = selector.dataset.scopeHubName || '';
    const scopeCommunityName = selector.dataset.scopeCommunityName || '';
    const preselectedSpaceId = selector.dataset.preselectedSpaceId || '';

    let selectedSpace: SpaceResult | null = null;
    let debounceTimer: ReturnType<typeof setTimeout> | null = null;
    let isOpen = false;

    // --- Scope label ---

    const inputWrapper = input.parentElement!;
    let scopeLabel: HTMLElement | null = null;

    function buildScopeLabelText(): string {
        if (scopeHubId && scopeCommunityName && scopeHubName)
            return `${scopeCommunityName} › ${scopeHubName}`;
        if (scopeCommunityId && scopeCommunityName)
            return scopeCommunityName;
        return '';
    }

    function updatePlaceholder(): void {
        if (scopeHubId && scopeHubName)
            input.placeholder = `Search for a space in ${scopeHubName}...`;
        else if (scopeCommunityId && scopeCommunityName)
            input.placeholder = `Search for a space in ${scopeCommunityName}...`;
        else
            input.placeholder = 'Search for a space...';
    }

    function createScopeLabel(): void {
        const text = buildScopeLabelText();
        if (!text) return;

        scopeLabel = document.createElement('span');
        scopeLabel.className = 'scope-label';
        scopeLabel.innerHTML = `<span class="scope-label-text">${escapeHtml(text)}</span>`;

        const closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.className = 'scope-label-close';
        closeBtn.setAttribute('aria-label', 'Clear scope filter');
        closeBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>';
        closeBtn.addEventListener('click', clearScope);
        scopeLabel.appendChild(closeBtn);

        inputWrapper.classList.add('has-scope-label');
        inputWrapper.insertBefore(scopeLabel, input);
    }

    function clearScope(): void {
        scopeHubId = '';
        scopeCommunityId = '';

        if (scopeLabel) {
            scopeLabel.remove();
            scopeLabel = null;
        }

        inputWrapper.classList.remove('has-scope-label');
        updatePlaceholder();

        // Clear current selection and re-search globally
        if (selectedSpace) {
            selectedSpace = null;
            input.value = '';
            if (hiddenId) hiddenId.value = '';
            typePickerSection.classList.add('hidden');
        }

        input.focus();
    }

    // Initialize scope label and placeholder
    updatePlaceholder();
    createScopeLabel();

    function escapeHtml(text: string): string {
        const el = document.createElement('span');
        el.textContent = text;
        return el.innerHTML;
    }

    async function fetchSpaces(query?: string): Promise<SpaceResult[]> {
        const params = new URLSearchParams();
        if (query) params.set('q', query);
        if (scopeHubId) params.set('hubId', scopeHubId);
        if (scopeCommunityId) params.set('communityId', scopeCommunityId);
        params.set('limit', '10');

        try {
            const resp = await fetch(`/bff/search/spaces?${params.toString()}`);
            if (!resp.ok) return [];
            return await resp.json();
        } catch {
            return [];
        }
    }

    function renderDropdown(spaces: SpaceResult[]): void {
        if (spaces.length === 0) {
            dropdown.innerHTML = '<div class="px-4 py-3 text-sm text-base-content/50">No spaces found</div>';
        } else {
            dropdown.innerHTML = spaces.map(s => `
                <button type="button" class="space-option w-full text-left px-4 py-2.5 hover:bg-base-200 transition-colors cursor-pointer flex items-center gap-3"
                        data-id="${escapeHtml(s.publicId)}"
                        data-name="${escapeHtml(s.name)}"
                        data-slug="${escapeHtml(s.slug)}"
                        data-hub-slug="${escapeHtml(s.hubSlug)}"
                        data-hub-name="${escapeHtml(s.hubName)}"
                        data-community-slug="${escapeHtml(s.communitySlug)}"
                        data-community-name="${escapeHtml(s.communityName)}"
                        data-avatar-url="${escapeHtml(s.avatarUrl)}"
                        data-discussion-count="${s.discussionCount}">
                    <img src="${escapeHtml(s.avatarUrl)}" alt="" width="32" height="32" class="w-8 h-8 rounded-full shrink-0" />
                    <div class="flex-1 min-w-0">
                        <div class="font-medium text-sm">${escapeHtml(s.name)}</div>
                        <div class="text-xs text-base-content/50">${escapeHtml(s.communityName)} &rsaquo; ${escapeHtml(s.hubName)}</div>
                    </div>
                    <span class="flex items-center gap-1 text-xs text-base-content/40 shrink-0">
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path></svg>
                        ${s.discussionCount}
                    </span>
                </button>
            `).join('');
        }

        dropdown.classList.remove('hidden');
        isOpen = true;
    }

    function closeDropdown(): void {
        dropdown.classList.add('hidden');
        isOpen = false;
    }

    function selectSpace(space: SpaceResult): void {
        selectedSpace = space;
        input.value = space.name;

        if (hiddenId) hiddenId.value = space.publicId;
        if (hiddenSlug) hiddenSlug.value = space.slug;
        if (hiddenHubSlug) hiddenHubSlug.value = space.hubSlug;
        if (hiddenCommunitySlug) hiddenCommunitySlug.value = space.communitySlug;

        closeDropdown();

        // Fetch allowed types and show type picker
        loadAllowedTypes(space);
    }

    function showTypeSkeletons(): void {
        typeCards.innerHTML = Array.from({ length: 4 }, () => `
            <div class="new-discussion-type-card">
                <div class="flex items-center gap-2 mb-1.5">
                    <div class="skeleton w-6 h-6 rounded"></div>
                    <div class="skeleton h-4 w-24"></div>
                </div>
                <div class="skeleton h-3 w-3/4 mb-2"></div>
                <div class="skeleton h-3 w-1/2"></div>
            </div>
        `).join('');
        typePickerSection.classList.remove('hidden');
    }

    async function loadAllowedTypes(space: SpaceResult): Promise<void> {
        showTypeSkeletons();

        try {
            const resp = await fetch(`/bff/spaces/${encodeURIComponent(space.publicId)}/allowed-types`);
            if (!resp.ok) return;

            const data = await resp.json();
            const allowedTypes: number[] = data.allowedTypes;

            renderTypeCards(allowedTypes, space);
        } catch {
            // Fallback: show all types
            renderTypeCards([0, 1, 2, 4, 5, 6, 7, 8, 9], space);
        }
    }

    function buildDiscussionUrl(space: SpaceResult, typeSlug: string): string {
        return `/new/${typeSlug}?spaceId=${encodeURIComponent(space.publicId)}`;
    }

    function renderTypeCards(allowedTypes: number[], space: SpaceResult): void {
        const filtered = TYPE_DATA.filter(t => allowedTypes.includes(t.type));

        typeCards.innerHTML = filtered.map(t => {
            const url = buildDiscussionUrl(space, t.slug);
            return `
                <a href="${escapeHtml(url)}" class="new-discussion-type-card" hx-boost="false">
                    <div class="flex items-center gap-2 mb-1.5">
                        ${t.icon ? `<span class="text-lg">${t.icon}</span>` : ''}
                        <span class="font-semibold">${escapeHtml(t.label)}</span>
                    </div>
                    <p class="text-sm text-base-content/70 mb-2">${escapeHtml(t.description)}</p>
                    <p class="text-xs text-base-content/40">${escapeHtml(t.features)}</p>
                </a>
            `;
        }).join('');

        typePickerSection.classList.remove('hidden');
    }

    // Pre-select space by ID (fetches details from BFF)
    if (preselectedSpaceId) {
        (async () => {
            try {
                const resp = await fetch(`/bff/spaces/${encodeURIComponent(preselectedSpaceId)}/info`);
                if (!resp.ok) return;
                const space: SpaceResult = await resp.json();
                selectSpace(space);
            } catch { /* ignore */ }
        })();
    }

    // Event: focus on input → load initial spaces
    input.addEventListener('focus', async () => {
        if (isOpen) return;
        const spaces = await fetchSpaces();
        renderDropdown(spaces);
    });

    // Event: typing in input → debounced search
    input.addEventListener('input', () => {
        // Clear previous selection when user types
        if (selectedSpace) {
            selectedSpace = null;
            if (hiddenId) hiddenId.value = '';
            typePickerSection.classList.add('hidden');
        }

        if (debounceTimer) clearTimeout(debounceTimer);
        debounceTimer = setTimeout(async () => {
            const query = input.value.trim();
            const spaces = await fetchSpaces(query || undefined);
            renderDropdown(spaces);
        }, 250);
    });

    // Event: click on a space option
    dropdown.addEventListener('click', (e) => {
        const btn = (e.target as HTMLElement).closest('.space-option') as HTMLElement | null;
        if (!btn) return;

        selectSpace({
            publicId: btn.dataset.id || '',
            name: btn.dataset.name || '',
            slug: btn.dataset.slug || '',
            hubSlug: btn.dataset.hubSlug || '',
            hubName: btn.dataset.hubName || '',
            communitySlug: btn.dataset.communitySlug || '',
            discussionCount: parseInt(btn.dataset.discussionCount || '0', 10),
            communityName: btn.dataset.communityName || '',
            avatarUrl: btn.dataset.avatarUrl || ''
        });
    });

    // Event: click outside → close dropdown
    document.addEventListener('click', (e) => {
        if (!selector.contains(e.target as Node)) {
            closeDropdown();
        }
    });

    // Event: Escape key → close dropdown
    input.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeDropdown();
            input.blur();
        }
    });
})();
