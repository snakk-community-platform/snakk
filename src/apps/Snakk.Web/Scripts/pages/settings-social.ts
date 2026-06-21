(function () {
    'use strict';

    interface PlatformDef {
        key: string;
        displayName: string;
        category: string;
        placeholder: string;
        usernamePattern: string;
        hasUrl: boolean;
    }

    interface SocialLink {
        platform: string;
        displayName: string;
        username: string;
        url: string | null;
    }

    const platforms: PlatformDef[] = JSON.parse(
        document.getElementById('platforms-data')?.textContent || '[]'
    );
    const platformMap = new Map<string, PlatformDef>(platforms.map(p => [p.key, p]));

    let serverLinks: SocialLink[] = [];
    let localLinks: SocialLink[] = [];
    let isDirty = false;

    const linksList = document.getElementById('social-links-list')!;
    const loadingEl = document.getElementById('social-links-loading')!;
    const addFormEl = document.getElementById('add-link-form')!;
    const showAddBtn = document.getElementById('show-add-form-btn')!;
    const cancelBtn = document.getElementById('cancel-link-btn')!;
    const addBtn = document.getElementById('add-link-btn')!;
    const saveBtn = document.getElementById('save-links-btn') as HTMLButtonElement;
    const platformSelect = document.getElementById('platform-select') as HTMLSelectElement;
    const valueInput = document.getElementById('platform-value-input') as HTMLInputElement;
    const hintEl = document.getElementById('platform-hint')!;
    const addErrorEl = document.getElementById('add-link-error')!;
    const saveErrorEl = document.getElementById('save-error')!;
    const saveSuccessEl = document.getElementById('save-success')!;

    async function loadLinks(): Promise<void> {
        try {
            const res = await fetch('/bff/me/social', { credentials: 'include' });
            if (!res.ok) { loadingEl.textContent = 'Failed to load social links.'; return; }
            const data = await res.json();
            serverLinks = data.links || [];
            localLinks = [...serverLinks];
        } catch {
            loadingEl.textContent = 'Failed to load social links.';
            return;
        }
        renderLinks();
    }

    function renderLinks(): void {
        loadingEl.classList.add('sn-hidden');
        const existing = linksList.querySelectorAll('.social-link-row');
        existing.forEach(el => el.remove());

        for (const link of localLinks) {
            const row = buildLinkRow(link);
            linksList.appendChild(row);
        }

        populatePlatformSelect();
        updateDirtyState();
    }

    function buildLinkRow(link: SocialLink): HTMLElement {
        const row = document.createElement('div');
        row.className = 'social-link-row sn-flex sn-items-center sn-gap-3 sn-p-3 sn-rounded-lg';
        row.style.cssText = 'background: var(--surface-secondary); border: 1px solid var(--border-secondary)';
        row.dataset.platform = link.platform;

        const nameSpan = document.createElement('span');
        nameSpan.className = 'sn-text-sm sn-font-medium w-28 sn-shrink-0';
        nameSpan.textContent = link.displayName;

        const valueEl = document.createElement('span');
        valueEl.className = 'sn-text-sm sn-flex-1 sn-truncate';
        if (link.url) {
            const a = document.createElement('a');
            a.href = link.url;
            a.target = '_blank';
            a.rel = 'noopener noreferrer';
            a.textContent = link.username;
            a.style.cssText = 'color: var(--color-primary)';
            valueEl.appendChild(a);
        } else {
            valueEl.textContent = link.username;
        }

        const removeBtn = document.createElement('button');
        removeBtn.type = 'button';
        removeBtn.className = 'sn-btn sn-btn-ghost sn-btn-xs sn-shrink-0';
        removeBtn.setAttribute('aria-label', `Remove ${link.displayName}`);
        removeBtn.innerHTML = '<span class="icon icon-trash w-4 h-4" aria-hidden="true"></span>';
        removeBtn.addEventListener('click', () => {
            localLinks = localLinks.filter(l => l.platform !== link.platform);
            renderLinks();
        });

        row.appendChild(nameSpan);
        row.appendChild(valueEl);
        row.appendChild(removeBtn);
        return row;
    }

    function populatePlatformSelect(): void {
        const usedKeys = new Set(localLinks.map(l => l.platform));
        const byCategory = new Map<string, PlatformDef[]>();

        for (const p of platforms) {
            if (usedKeys.has(p.key)) continue;
            if (!byCategory.has(p.category)) byCategory.set(p.category, []);
            byCategory.get(p.category)!.push(p);
        }

        platformSelect.innerHTML = '<option value="">— select a platform —</option>';
        for (const [category, defs] of byCategory) {
            const group = document.createElement('optgroup');
            group.label = category;
            for (const def of defs) {
                const opt = document.createElement('option');
                opt.value = def.key;
                opt.textContent = def.displayName;
                group.appendChild(opt);
            }
            platformSelect.appendChild(group);
        }
    }

    function updateDirtyState(): void {
        isDirty = JSON.stringify(localLinks.map(l => l.platform + ':' + l.username).sort())
            !== JSON.stringify(serverLinks.map(l => l.platform + ':' + l.username).sort());
        saveBtn.classList.toggle('sn-hidden', !isDirty);
    }

    function showAddForm(): void {
        addFormEl.classList.remove('sn-hidden');
        showAddBtn.classList.add('sn-hidden');
        valueInput.value = '';
        hintEl.classList.add('sn-hidden');
        addErrorEl.classList.add('sn-hidden');
        platformSelect.value = '';
        valueInput.focus();
    }

    function hideAddForm(): void {
        addFormEl.classList.add('sn-hidden');
        showAddBtn.classList.remove('sn-hidden');
        addErrorEl.classList.add('sn-hidden');
    }

    function onPlatformChange(): void {
        const key = platformSelect.value;
        const def = platformMap.get(key);
        if (def) {
            valueInput.placeholder = def.placeholder;
            hintEl.textContent = def.hasUrl ? 'You can paste a full profile URL or just your username.' : 'Enter your username only.';
            hintEl.classList.remove('sn-hidden');
        } else {
            valueInput.placeholder = 'Enter username or profile URL';
            hintEl.classList.add('sn-hidden');
        }
        addErrorEl.classList.add('sn-hidden');
    }

    function tryAddLink(): void {
        addErrorEl.classList.add('sn-hidden');
        const key = platformSelect.value;
        if (!key) { showAddError('Please select a platform.'); return; }

        const raw = valueInput.value.trim();
        if (!raw) { showAddError('Please enter a username or profile URL.'); return; }

        const def = platformMap.get(key);
        if (!def) { showAddError('Unknown platform.'); return; }

        if (localLinks.some(l => l.platform === key)) {
            showAddError(`You already have a ${def.displayName} link.`);
            return;
        }

        if (localLinks.length >= 10) {
            showAddError('Maximum 10 social links allowed.');
            return;
        }

        // Show link with raw value as username for local display; server will normalise on save
        const displayUrl = def.hasUrl ? null : null; // URL will be set after server round-trip; keep null locally
        localLinks.push({ platform: key, displayName: def.displayName, username: raw, url: displayUrl });
        renderLinks();
        hideAddForm();
    }

    function showAddError(msg: string): void {
        addErrorEl.textContent = msg;
        addErrorEl.classList.remove('sn-hidden');
    }

    async function saveLinks(): Promise<void> {
        saveErrorEl.classList.add('sn-hidden');
        saveSuccessEl.classList.add('sn-hidden');
        saveBtn.disabled = true;
        saveBtn.textContent = 'Saving…';

        const payload = { links: localLinks.map(l => ({ platform: l.platform, value: l.username })) };

        try {
            const res = await fetch('/bff/me/social', {
                method: 'PUT',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const data = await res.json().catch(() => ({}));

            if (!res.ok) {
                saveErrorEl.textContent = data.error || 'Failed to save. Please try again.';
                saveErrorEl.classList.remove('sn-hidden');
                return;
            }

            // Reload from server to get normalised usernames + URLs
            await loadLinks();
            saveSuccessEl.classList.remove('sn-hidden');
            setTimeout(() => saveSuccessEl.classList.add('sn-hidden'), 3000);
        } catch {
            saveErrorEl.textContent = 'Failed to save. Please check your connection.';
            saveErrorEl.classList.remove('sn-hidden');
        } finally {
            saveBtn.disabled = false;
            saveBtn.textContent = 'Save changes';
        }
    }

    // Wire up events
    showAddBtn.addEventListener('click', showAddForm);
    cancelBtn.addEventListener('click', hideAddForm);
    platformSelect.addEventListener('change', onPlatformChange);
    addBtn.addEventListener('click', tryAddLink);
    saveBtn.addEventListener('click', saveLinks);

    valueInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); tryAddLink(); }
    });

    loadLinks();
})();
