/**
 * Settings Page
 * Handles profile settings, avatar upload, display name update, bio, timezone, and preferences.
 */

interface SettingsPageConfig {
    turnstileSiteKey: string | null;
}

(function () {
    'use strict';

    let userId: string | null = null;
    let userData: any = null;

    let pendingAvatarFile: File | null = null;
    let avatarBlobUrl: string | null = null;

    // Sudo state — token stored in httpOnly cookie, only expiry tracked in JS
    let SUDO_EXPIRES_KEY = 'snakk:sudo-expires';
    let sudoExpiresAt: number = 0;
    let sudoCallback: (() => void) | null = null;
    let pendingPasskeyChallenge: { challengeId: string; assertionJson: string } | null = null;
    let pendingPassword: string | null = null;
    let sudoTotpAutoSubmit = true;
    let enable2FaAutoSubmit = true;
    let sudoIconTimer: ReturnType<typeof setInterval> | null = null;

    // Config available via #settings-page-config JSON tag if needed
    // const configEl = document.getElementById('settings-page-config');
    // const config: SettingsPageConfig = configEl ? JSON.parse(configEl.textContent || '{}') : {};

    // Client-side authentication check and profile data loading
    async function initProfileSettings(): Promise<void> {
        try {
            const response = await fetch('/bff/me', {
                credentials: 'include'
            });

            if (response.status === 401) {
                window.location.href = '/auth/login?returnUrl=/settings';
                return;
            }

            if (!response.ok) {
                showMessage('Could not load your settings. Please refresh the page.', true);
                return;
            }

            userData = await response.json();
            userId = userData.publicId;

            populateProfileForm(userData);
        } catch (error) {
            console.error('[Profile Settings] Failed to load profile:', error);
            showMessage('Could not load your settings. Please refresh the page.', true);
        }
    }

    function populateProfileForm(data: any): void {
        // Update avatar -- use custom avatar URL if set, otherwise keep generated SVG
        const avatarImg = document.getElementById('avatar-preview') as HTMLImageElement | null;
        if (avatarImg && data.avatarUrl) {
            avatarImg.src = data.avatarUrl;
        }

        // Show current display name as text
        const currentDisplayName = document.getElementById('current-display-name');
        if (currentDisplayName) {
            currentDisplayName.textContent = data.displayName || '';
        }

        // Update preferences checkboxes
        const autoFollowCheckbox = document.querySelector('input[name="autoFollowOnReply"]') as HTMLInputElement | null;
        if (autoFollowCheckbox) {
            autoFollowCheckbox.checked = data.autoFollowOnReply !== false;
        }

        const adultRadios = document.querySelectorAll<HTMLInputElement>('input[name="allowAdultContent"][type="radio"]');
        if (adultRadios.length > 0) {
            const value = data.allowAdultContent === true ? 'allow'
                : data.allowAdultContent === false ? 'hide'
                : 'ask';
            adultRadios.forEach(r => { r.checked = r.value === value; });
        }

        const adultPreviewRadios = document.querySelectorAll<HTMLInputElement>('input[name="adultPreviewImageMode"][type="radio"]');
        if (adultPreviewRadios.length > 0) {
            const mode = typeof data.adultPreviewImageMode === 'number' ? data.adultPreviewImageMode : 0;
            const value = mode === 1 ? 'blur' : mode === 2 ? 'hide' : 'show';
            adultPreviewRadios.forEach(r => { r.checked = r.value === value; });
        }

        // Update timezone select
        const timezoneSelect = document.getElementById('timezone-select') as HTMLSelectElement | null;
        if (timezoneSelect) {
            timezoneSelect.value = data.timezone || 'UTC';
        }

        // Update bio
        const bioInput = document.getElementById('bio-input') as HTMLTextAreaElement | null;
        const bioCharCount = document.getElementById('bio-char-count');
        if (bioInput) {
            bioInput.value = data.bio || '';
            if (bioCharCount) bioCharCount.textContent = (data.bio || '').length;
        }

        // Feed token
        updateFeedTokenUI(data.feedToken || null);

        // Display name change restrictions
        const statusEl = document.getElementById('display-name-status');
        const passwordField = document.getElementById('password-field');
        const changeNameBtn = document.getElementById('change-name-btn') as HTMLButtonElement | null;

        if (data.isDisplayNameLocked) {
            if (statusEl) {
                statusEl.textContent = (window as any).T?.settings?.displayNameLocked ?? 'Your display name has been locked by an administrator.';
                statusEl.className = 'text-sm mb-3 text-warning';
                statusEl.classList.remove('hidden');
            }
            if (changeNameBtn) changeNameBtn.disabled = true;
        } else if (data.displayNameChangedAt) {
            const changedAt = new Date(data.displayNameChangedAt);
            const daysSince = Math.floor((Date.now() - changedAt.getTime()) / (1000 * 60 * 60 * 24));
            const cooldownDays = 30;
            if (daysSince < cooldownDays) {
                const remaining = cooldownDays - daysSince;
                if (statusEl) {
                    const tpl = remaining === 1
                        ? ((window as any).T?.settings?.displayNameCooldownOne ?? 'You can change your display name again in {0} day.')
                        : ((window as any).T?.settings?.displayNameCooldownOther ?? 'You can change your display name again in {0} days.');
                    statusEl.textContent = tpl.replace('{0}', String(remaining));
                    statusEl.className = 'text-sm mb-3 text-base-content/60';
                    statusEl.classList.remove('hidden');
                }
                if (changeNameBtn) changeNameBtn.disabled = true;
            }
        }

        // Hide password field for OAuth-only users (no password set)
        if (!data.passwordHash && data.oAuthProvider && passwordField) {
            passwordField.style.display = 'none';
        }

        document.querySelectorAll('.profile-userid').forEach(el => el.textContent = data.publicId);

        const snakkSettings = (window as any).snakkSettings;
        if (snakkSettings) {
            snakkSettings.hasPassword = data.hasPassword !== false;
            snakkSettings.connectedProviders = Array.isArray(data.connectedProviders) ? data.connectedProviders : [];
        }
    }

    function showStatus(message: string, isError: boolean = false): void {
        const statusDiv = document.getElementById('upload-status');
        if (!statusDiv) return;
        statusDiv.textContent = message;
        statusDiv.className = 'mt-3 text-sm ' + (isError ? 'text-error' : 'text-success');
        statusDiv.classList.remove('hidden');
    }

    function showUploadProgress(): void {
        const progressRow = document.getElementById('upload-progress-row');
        const chipsEl = document.getElementById('upload-image-chips');
        const statusEl = document.getElementById('upload-status-text');
        if (!progressRow || !chipsEl || !statusEl) return;

        chipsEl.innerHTML = '';
        const chip = document.createElement('span');
        chip.className = 'upload-chip upload-chip-uploading';
        chip.id = 'avatar-upload-chip';
        chipsEl.appendChild(chip);
        statusEl.textContent = (window as any).T?.settings?.avatarUploading ?? 'Uploading avatar…';
        progressRow.classList.remove('hidden');
    }

    function finishUploadProgress(success: boolean): void {
        const progressRow = document.getElementById('upload-progress-row');
        const chip = document.getElementById('avatar-upload-chip');
        if (!progressRow) return;

        if (success && chip) {
            chip.className = 'upload-chip upload-chip-done';
            setTimeout(() => progressRow.classList.add('hidden'), 800);
        } else {
            progressRow.classList.add('hidden');
        }
    }

    function showMessage(message: string, isError: boolean = false): void {
        // Remove existing messages
        const existingAlerts = document.querySelectorAll('.profile-alert-message');
        existingAlerts.forEach(el => el.remove());

        // Create new message
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert ${isError ? 'alert-error' : 'alert-success'} mb-6 profile-alert-message`;

        const span = document.createElement('span');
        span.textContent = message;
        alertDiv.appendChild(span);

        // Insert after page header
        const pageHeader = document.querySelector('.page-header');
        if (pageHeader) {
            pageHeader.insertAdjacentElement('afterend', alertDiv);
        }

        setTimeout(() => { alertDiv.remove(); }, 5000);
    }

    async function refreshAvatar(forceGenerated = false): Promise<void> {
        const avatarPreview = document.getElementById('avatar-preview') as HTMLImageElement | null;
        if (!avatarPreview || !userId) return;
        try {
            const response = await fetch('/bff/me', { credentials: 'include' });
            if (response.ok) {
                const data = await response.json();
                const timestamp = new Date().getTime();
                const navSrc = data.avatarUrl
                    ? data.avatarUrl + '?t=' + timestamp
                    : avatarPreview.dataset.generatedUrl || '';

                // Only update the preview if we got a real URL, or the caller explicitly
                // wants the generated avatar shown (e.g. after deleting the custom one).
                // Without this guard, a stale API response returning avatarUrl=null after
                // upload would overwrite the preview with the generated avatar.
                if (data.avatarUrl) {
                    avatarPreview.src = navSrc;
                } else if (forceGenerated) {
                    avatarPreview.src = avatarPreview.dataset.generatedUrl || '';
                }

                document.querySelectorAll<HTMLImageElement>(
                    '#auth-nav .avatar-sm img, #auth-nav .sn-user-menu-card-avatar, .sn-tabbar .avatar-sm img, .sn-tabbar .sn-user-menu-card-avatar'
                ).forEach(img => { img.src = navSrc; });
            }
        } catch { /* ignore */ }
    }

    function setAvatarFile(file: File): void {
        const avatarPreview = document.getElementById('avatar-preview') as HTMLImageElement | null;
        const uploadBtn = document.getElementById('upload-btn') as HTMLButtonElement | null;
        const dropLabel = document.getElementById('avatar-drop-label');
        if (!avatarPreview) return;

        const allowed = ['image/jpeg', 'image/png', 'image/webp'];
        if (!allowed.includes(file.type)) {
            showStatus('Invalid file type. Allowed: JPEG, PNG, WebP.', true);
            return;
        }

        if (file.size > 2 * 1024 * 1024) {
            showStatus('File is too large. Maximum size is 2MB.', true);
            return;
        }

        if (avatarBlobUrl) URL.revokeObjectURL(avatarBlobUrl);
        avatarBlobUrl = URL.createObjectURL(file);
        pendingAvatarFile = file;
        avatarPreview.src = avatarBlobUrl;

        if (uploadBtn) uploadBtn.disabled = false;
        if (dropLabel) dropLabel.textContent = file.name;
    }

    function initAvatarDropZone(): void {
        const dropZone = document.getElementById('avatar-drop-zone');
        const fileInput = document.getElementById('avatar-file') as HTMLInputElement | null;
        if (!dropZone || !fileInput) return;

        dropZone.addEventListener('click', () => fileInput.click());

        fileInput.addEventListener('change', (e) => {
            const target = e.target as HTMLInputElement;
            if (target.files && target.files[0]) setAvatarFile(target.files[0]);
        });

        dropZone.addEventListener('dragover', (e) => {
            e.preventDefault();
            dropZone.classList.add('gallery-drop-zone-active');
        });

        dropZone.addEventListener('dragleave', () => {
            dropZone.classList.remove('gallery-drop-zone-active');
        });

        dropZone.addEventListener('drop', (e) => {
            e.preventDefault();
            dropZone.classList.remove('gallery-drop-zone-active');
            const file = e.dataTransfer?.files[0];
            if (file) setAvatarFile(file);
        });
    }

    function resetDropZone(): void {
        const dropLabel = document.getElementById('avatar-drop-label');
        if (dropLabel) dropLabel.textContent = 'Click or drag an image here';
    }

    async function handleAvatarUploadSubmit(e: Event): Promise<void> {
        e.preventDefault();

        const uploadBtn = document.getElementById('upload-btn') as HTMLButtonElement | null;
        if (!uploadBtn) return;

        const file = pendingAvatarFile;
        if (!file) {
            showStatus('Please select a file first.', true);
            return;
        }

        uploadBtn.disabled = true;
        showUploadProgress();

        try {
            const formData = new FormData();
            formData.append('avatar', file);

            const result = await window.SnakkUpload.uploadWithProgress<{ error?: string }>({
                url: '/bff/avatars/upload',
                formData,
                onProgress: () => {},
                onProcessing: () => {},
            });

            if (result.ok) {
                finishUploadProgress(true);
                // Await so the blob stays visible during the API round-trip.
                // revokeObjectURL runs only after the new URL is set (or confirmed absent).
                await refreshAvatar();
                pendingAvatarFile = null;
                if (avatarBlobUrl) { URL.revokeObjectURL(avatarBlobUrl); avatarBlobUrl = null; }
                resetDropZone();
                uploadBtn.disabled = true;
                const delBtn = document.getElementById('delete-avatar-btn') as HTMLButtonElement | null;
                if (delBtn) delBtn.disabled = false;
            } else {
                finishUploadProgress(false);
                showStatus(result.data?.error || result.error || 'Failed to upload avatar.', true);
                await refreshAvatar();
            }
        } catch (error) {
            finishUploadProgress(false);
            showStatus('Network error. Please try again.', true);
            await refreshAvatar();
        } finally {
            uploadBtn.disabled = !pendingAvatarFile;
        }
    }

    async function handleDeleteClick(): Promise<void> {
        if (!confirm('Revert to the auto-generated avatar?')) return;

        const deleteBtn = document.getElementById('delete-avatar-btn') as HTMLButtonElement | null;
        if (!deleteBtn) return;

        deleteBtn.disabled = true;
        deleteBtn.textContent = (window as any).T?.settings?.avatarRevertLabel ?? 'Reverting...';

        try {
            const response = await fetch('/bff/avatars', {
                method: 'DELETE',
                credentials: 'include'
            });

            if (response.ok) {
                refreshAvatar(true);
                deleteBtn.disabled = true;
                deleteBtn.textContent = (window as any).T?.settings?.avatarUseGenerated ?? 'Use Generated';
            } else {
                deleteBtn.disabled = false;
                deleteBtn.textContent = (window as any).T?.settings?.avatarUseGenerated ?? 'Use Generated';
                const result = await response.json();
                showStatus(result.error || 'Failed to delete avatar.', true);
            }
        } catch (error) {
            deleteBtn.disabled = false;
            deleteBtn.textContent = 'Use Generated';
            showStatus('Network error. Please try again.', true);
        }
    }

    function openDisplayNameModal(): void {
        const input = document.getElementById('display-name-input') as HTMLInputElement | null;
        const snakkModal = (window as any).snakkModal as { open: (id: string) => void } | undefined;
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(() => {
            if (input) input.value = '';
            clearModalStatus('display-name-modal-status');
            snakkModal?.open('modal-display-name');
        });
    }

    async function handleDisplayNameModalSubmit(): Promise<void> {
        const input = document.getElementById('display-name-input') as HTMLInputElement | null;
        const submitBtn = document.getElementById('display-name-modal-submit') as HTMLButtonElement | null;
        const displayName = input?.value?.trim();

        if (!displayName) {
            showModalStatus('display-name-modal-status', 'Display name cannot be empty', true);
            return;
        }

        if (!isSudoValid()) {
            showModalStatus('display-name-modal-status', 'Authentication required', true);
            return;
        }

        if (submitBtn) submitBtn.disabled = true;
        try {
            const response = await fetch('/bff/me/profile', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ displayName })
            });
            const result = await response.json().catch(() => null);
            if (response.ok) {
                const currentEl = document.getElementById('current-display-name');
                if (currentEl) currentEl.textContent = displayName;
                const snakkModal = (window as any).snakkModal as { close: (id: string) => void } | undefined;
                snakkModal?.close('modal-display-name');
            } else if (response.status === 403) {
                invalidateSudo();
                showModalStatus('display-name-modal-status', 'Session expired. Please re-authenticate.', true);
            } else {
                showModalStatus('display-name-modal-status', result?.error || 'Failed to update display name', true);
            }
        } catch {
            showModalStatus('display-name-modal-status', 'Network error. Please try again.', true);
        } finally {
            if (submitBtn) submitBtn.disabled = false;
        }
    }

    async function handleBioSave(): Promise<void> {
        const bioInput = document.getElementById('bio-input') as HTMLTextAreaElement | null;
        const saveBtn = document.getElementById('save-bio-btn') as HTMLButtonElement | null;
        if (!bioInput || !saveBtn) return;

        const bio = bioInput.value.trim();
        saveBtn.disabled = true;
        saveBtn.textContent = 'Saving...';

        try {
            const response = await fetch('/bff/me/preferences', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ bio })
            });

            if (!response.ok) {
                showMessage('Failed to update bio', true);
            }
        } catch {
            showMessage('Network error. Please try again.', true);
        } finally {
            saveBtn.disabled = false;
            saveBtn.textContent = 'Save Bio';
        }
    }

    // --- Feed Token ---

    function updateFeedTokenUI(token: string | null): void {
        const emptyEl = document.getElementById('feed-token-empty');
        const activeEl = document.getElementById('feed-token-active');
        const valueEl = document.getElementById('feed-token-value') as HTMLInputElement | null;

        if (!emptyEl || !activeEl) return;

        if (token) {
            emptyEl.classList.add('hidden');
            activeEl.classList.remove('hidden');
            if (valueEl) valueEl.value = token;
        } else {
            emptyEl.classList.remove('hidden');
            activeEl.classList.add('hidden');
            if (valueEl) valueEl.value = '';
        }
    }

    function handleGenerateFeedToken(): void {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(async () => {
            try {
                const response = await fetch('/bff/me/feed-token', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' }
                });
                if (response.ok) {
                    const data = await response.json();
                    updateFeedTokenUI(data.token);
                } else if (response.status === 403) {
                    invalidateSudo();
                    showMessage('Session expired. Please re-authenticate.', true);
                } else {
                    showMessage('Failed to generate feed token', true);
                }
            } catch {
                showMessage('Network error. Please try again.', true);
            }
        });
    }

    function handleRevokeFeedToken(): void {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(async () => {
            try {
                const response = await fetch('/bff/me/feed-token', {
                    method: 'DELETE',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' }
                });
                if (response.ok) {
                    updateFeedTokenUI(null);
                } else if (response.status === 403) {
                    invalidateSudo();
                    showMessage('Session expired. Please re-authenticate.', true);
                } else {
                    showMessage('Failed to revoke feed token', true);
                }
            } catch {
                showMessage('Network error. Please try again.', true);
            }
        });
    }

    function updateDiscordUI(isLinked: boolean, username: string | null): void {
        const notLinked = document.getElementById('discord-not-linked');
        const linked = document.getElementById('discord-linked');
        const display = document.getElementById('discord-username-display');
        if (!notLinked || !linked) return;
        if (isLinked) {
            notLinked.classList.add('hidden');
            linked.classList.remove('hidden');
            if (display) display.textContent = username ?? '';
        } else {
            notLinked.classList.remove('hidden');
            linked.classList.add('hidden');
        }
    }

    async function loadDiscordStatus(): Promise<void> {
        if (!document.getElementById('section-discord')) return;
        try {
            const response = await fetch('/bff/me/discord/status', { credentials: 'include' });
            if (!response.ok) return;
            const data = await response.json();
            updateDiscordUI(data.isLinked, data.discordUsername ?? null);
        } catch {
            // Silently fail
        }
    }

    async function initPasskeyStatus(): Promise<void> {
        if (!window.PublicKeyCredential) return;
        try {
            const response = await fetch('/bff/auth/passkeys/', { credentials: 'include' });
            if (!response.ok) return;
            const data = await response.json() as unknown[];
            const s = (window as any).snakkSettings;
            if (s) s.hasPasskeys = data.length > 0;
        } catch {
            // Silently fail
        }
    }

    function handleDiscordLink(): void {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(async () => {
            try {
                const response = await fetch('/bff/me/discord/link-token', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' }
                });
                if (!response.ok) {
                    if (response.status === 403) { invalidateSudo(); showMessage('Session expired. Please re-authenticate.', true); return; }
                    showMessage('Failed to initiate Discord link', true); return;
                }
                const data = await response.json();
                window.location.href = data.linkUrl;
            } catch {
                showMessage('Network error. Please try again.', true);
            }
        });
    }

    function handleDiscordUnlink(): void {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(async () => {
            try {
                const response = await fetch('/bff/me/discord', {
                    method: 'DELETE',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' }
                });
                if (response.ok) {
                    updateDiscordUI(false, null);
                } else if (response.status === 403) {
                    invalidateSudo();
                    showMessage('Session expired. Please re-authenticate.', true);
                } else {
                    showMessage('Failed to disconnect Discord', true);
                }
            } catch {
                showMessage('Network error. Please try again.', true);
            }
        });
    }

    async function handleTimezoneChange(select: HTMLSelectElement): Promise<void> {
        const timezone = select.value;

        try {
            const response = await fetch('/bff/me/preferences', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ timezone })
            });

            if (!response.ok) {
                showMessage('Failed to update timezone', true);
            }
        } catch (error) {
            showMessage('Network error. Please try again.', true);
        }
    }

    async function handleAutoFollowPreferenceChange(checkbox: HTMLInputElement): Promise<void> {
        const autoFollowOnReply = checkbox.checked;

        try {
            const response = await fetch('/bff/me/preferences', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ autoFollowOnReply })
            });

            if (!response.ok) {
                showMessage('Failed to update preference', true);
                checkbox.checked = !autoFollowOnReply;
            }
        } catch (error) {
            showMessage('Network error. Please try again.', true);
            checkbox.checked = !autoFollowOnReply;
        }
    }

    async function handleAdultContentPreferenceChange(radio: HTMLInputElement): Promise<void> {
        const value = radio.value;
        const allowAdultContent: boolean | null = value === 'allow' ? true : value === 'hide' ? false : null;

        try {
            const response = await fetch('/bff/me/preferences', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ allowAdultContent, clearAllowAdultContent: allowAdultContent === null })
            });

            if (!response.ok) {
                showMessage('Failed to update preference', true);
            }
        } catch (error) {
            showMessage('Network error. Please try again.', true);
        }
    }

    async function handleAdultPreviewImageModeChange(radio: HTMLInputElement): Promise<void> {
        const value = radio.value;
        const adultPreviewImageMode = value === 'blur' ? 1 : value === 'hide' ? 2 : 0;

        try {
            const response = await fetch('/bff/me/preferences', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ adultPreviewImageMode })
            });

            if (!response.ok) {
                showMessage('Failed to update preference', true);
            }
        } catch (error) {
            showMessage('Network error. Please try again.', true);
        }
    }

    // --- Device Management ---
    async function loadDevices(): Promise<void> {
        const container = document.getElementById('devices-list');
        if (!container) return;
        const section = document.getElementById('devices-section');

        try {
            const response = await fetch('/bff/me/devices', { credentials: 'include' });
            if (!response.ok) {
                container.innerHTML = '<p class="sn-devices-empty">Unable to load devices.</p>';
                return;
            }

            const devices: any[] = await response.json();
            if (devices.length === 0) {
                if (section) section.style.display = 'none';
                return;
            }
            if (section) section.style.display = '';

            container.innerHTML = devices.map((d: any) => {
                const metaParts: string[] = [];
                if (d.lastUsedIp) metaParts.push(escapeHtml(d.lastUsedIp));
                metaParts.push(`Added ${formatRelativeDate(d.trustedAt)}`);
                if (d.lastUsedAt) metaParts.push(`Last used ${formatRelativeDate(d.lastUsedAt)}`);

                const badge = d.isActive
                    ? '<span class="sn-device-badge sn-device-badge--active">Active</span>'
                    : '<span class="sn-device-badge sn-device-badge--expired">Expired</span>';

                return `
                <div class="sn-device-item" data-device-id="${escapeHtml(d.publicId)}">
                    <span class="icon icon-display-monitor sn-device-icon" aria-hidden="true"></span>
                    <div class="sn-device-info">
                        <div class="sn-device-name">
                            <span>${escapeHtml(d.deviceName || parseUserAgent(d.deviceName))}</span>
                            ${badge}
                        </div>
                        <div class="sn-device-meta">${metaParts.join(' · ')}</div>
                    </div>
                    <button class="sn-device-revoke" data-device-id="${escapeHtml(d.publicId)}">
                        Revoke
                    </button>
                </div>`;
            }).join('');

            container.querySelectorAll<HTMLButtonElement>('.sn-device-revoke').forEach(btn => {
                btn.addEventListener('click', async () => {
                    const deviceId = btn.dataset.deviceId;
                    if (!deviceId) return;
                    btn.disabled = true;
                    try {
                        const res = await fetch(`/bff/me/devices/${deviceId}`, {
                            method: 'DELETE',
                            credentials: 'include'
                        });
                        if (res.ok) {
                            btn.closest('.sn-device-item')?.remove();
                            if (container.querySelectorAll('.sn-device-item').length === 0) {
                                if (section) section.style.display = 'none';
                            }
                        }
                    } catch {
                        // silently fail
                    } finally {
                        btn.disabled = false;
                    }
                });
            });
        } catch {
            container.innerHTML = '<p class="sn-devices-empty">Unable to load devices.</p>';
        }
    }

    function parseBrowser(ua: string): string {
        if (!ua) return 'browser';
        if (ua.includes('Edg/') || ua.includes('EdgA/')) return 'edge';
        if (ua.includes('OPR/') || ua.includes('Opera')) return 'opera';
        if (ua.includes('SamsungBrowser')) return 'samsung';
        if (ua.includes('Chrome') || ua.includes('Chromium')) return 'chrome';
        if (ua.includes('Firefox') || ua.includes('FxiOS')) return 'firefox';
        if (ua.includes('Safari')) return 'safari';
        return 'browser';
    }

    function parseOs(ua: string): string {
        if (!ua) return 'unknown';
        if (ua.includes('Android')) return 'android';
        if (ua.includes('iPhone') || ua.includes('iPad') || ua.includes('iPod')) return 'ios';
        if (ua.includes('Windows')) return 'windows';
        if (ua.includes('Mac OS X') || ua.includes('Macintosh')) return 'macos';
        if (ua.includes('Linux') || ua.includes('X11')) return 'linux';
        return 'unknown';
    }

    function buildDeviceIconStack(ua: string): HTMLElement {
        const stack = document.createElement('div');
        stack.className = 'sn-device-icon-stack';

        const browser = parseBrowser(ua);
        const browserImg = document.createElement('img');
        browserImg.src = `/icons/brand-${browser}-color.svg`;
        browserImg.alt = browser;
        browserImg.className = 'sn-device-browser-icon';
        stack.appendChild(browserImg);

        const os = parseOs(ua);
        if (os !== 'unknown') {
            const osImg = document.createElement('img');
            osImg.src = `/icons/os-${os}-color.svg`;
            osImg.alt = os;
            osImg.className = 'sn-device-os-badge';
            stack.appendChild(osImg);
        }

        return stack;
    }

    function parseUserAgent(ua: string): string {
        if (!ua) return 'Unknown Device';
        if (ua.includes('Firefox')) return 'Firefox';
        if (ua.includes('Edg/')) return 'Microsoft Edge';
        if (ua.includes('Chrome')) return 'Chrome';
        if (ua.includes('Safari')) return 'Safari';
        return ua.substring(0, 50);
    }

    const formatRelativeDate = (dateStr: string): string => (window as any).SnakkUtils.formatRelativeDate(dateStr);

    const escapeHtml = (text: string): string => (window as any).SnakkUtils.escapeHtml(text);

    function initEmbedPreferences(): void {
        const container = document.getElementById('embed-providers');
        if (!container) return;

        let autoPrefs: Record<string, boolean> = {};
        let deniedPrefs: Record<string, boolean> = {};
        try {
            autoPrefs   = JSON.parse(localStorage.getItem('snakk:embed-providers')        || '{}');
            deniedPrefs = JSON.parse(localStorage.getItem('snakk:embed-denied-providers') || '{}');
        } catch { /* ignore */ }

        function updateEmbedIndicator(select: HTMLSelectElement): void {
            let indicator = select.previousElementSibling as HTMLElement | null;
            if (!indicator || !indicator.classList.contains('embed-state-indicator')) {
                indicator = document.createElement('span');
                indicator.className = 'embed-state-indicator';
                indicator.style.cssText = 'font-size:0.875rem;font-weight:700;flex-shrink:0;width:1rem;text-align:center;line-height:1';
                select.parentElement!.insertBefore(indicator, select);
            }
            if (select.value === 'allow') {
                indicator.textContent = '✓';
                indicator.style.color = '#16a34a';
            } else if (select.value === 'deny') {
                indicator.textContent = '✗';
                indicator.style.color = '#dc2626';
            } else {
                indicator.textContent = '';
            }
        }

        const selects = container.querySelectorAll<HTMLSelectElement>('select[data-embed-provider]');
        selects.forEach(select => {
            const provider = select.dataset.embedProvider || '';
            if (autoPrefs[provider])        select.value = 'allow';
            else if (deniedPrefs[provider]) select.value = 'deny';
            else                            select.value = 'ask';
            updateEmbedIndicator(select);
        });

        container.addEventListener('change', (e) => {
            const select = (e.target as HTMLElement).closest('select[data-embed-provider]') as HTMLSelectElement | null;
            if (!select) return;

            const provider = select.dataset.embedProvider || '';
            const value    = select.value;

            if (value === 'allow') {
                autoPrefs[provider] = true;
                delete deniedPrefs[provider];
            } else if (value === 'deny') {
                deniedPrefs[provider] = true;
                delete autoPrefs[provider];
            } else {
                delete autoPrefs[provider];
                delete deniedPrefs[provider];
            }

            localStorage.setItem('snakk:embed-providers',        JSON.stringify(autoPrefs));
            localStorage.setItem('snakk:embed-denied-providers', JSON.stringify(deniedPrefs));
            updateEmbedIndicator(select);
        });
    }

    function initDisplayPreferences(): void {
        const listLayoutSelect = document.getElementById('pref-discussion-list-layout') as HTMLSelectElement | null;
        const animationsToggle = document.getElementById('pref-animations') as HTMLInputElement | null;
        const linkPreviewToggle = document.getElementById('pref-link-preview-large') as HTMLInputElement | null;
        const hoverPopupToggle = document.getElementById('pref-hover-popup') as HTMLInputElement | null;
        if (!listLayoutSelect && !animationsToggle && !linkPreviewToggle && !hoverPopupToggle) return;

        try {
            if (listLayoutSelect) {
                listLayoutSelect.value = localStorage.getItem('snakk:discussion-list-layout') ?? 'compact';
                listLayoutSelect.addEventListener('change', () => {
                    const value = listLayoutSelect.value;
                    localStorage.setItem('snakk:discussion-list-layout', value);
                    const root = document.documentElement;
                    root.classList.remove('no-discussion-previews', 'discussion-list-compact');
                    if (value === 'none') root.classList.add('no-discussion-previews');
                    else if (value === 'compact') root.classList.add('discussion-list-compact');
                });
            }

            if (animationsToggle) {
                animationsToggle.checked = localStorage.getItem('snakk:disable-animations') !== 'true';
                animationsToggle.addEventListener('change', () => {
                    const disabled = !animationsToggle.checked;
                    if (disabled) {
                        localStorage.setItem('snakk:disable-animations', 'true');
                        document.documentElement.classList.add('no-animations');
                    } else {
                        localStorage.removeItem('snakk:disable-animations');
                        document.documentElement.classList.remove('no-animations');
                    }
                });
            }
            if (linkPreviewToggle) {
                linkPreviewToggle.checked = localStorage.getItem('snakk:link-preview-compact') !== 'true';
                linkPreviewToggle.addEventListener('change', () => {
                    const compact = !linkPreviewToggle.checked;
                    if (compact) {
                        localStorage.setItem('snakk:link-preview-compact', 'true');
                        document.documentElement.classList.add('link-preview-compact');
                    } else {
                        localStorage.removeItem('snakk:link-preview-compact');
                        document.documentElement.classList.remove('link-preview-compact');
                    }
                });
            }
            if (hoverPopupToggle) {
                hoverPopupToggle.checked = localStorage.getItem('snakk:disable-hover-popup') !== 'true';
                hoverPopupToggle.addEventListener('change', () => {
                    if (!hoverPopupToggle.checked) {
                        localStorage.setItem('snakk:disable-hover-popup', 'true');
                    } else {
                        localStorage.removeItem('snakk:disable-hover-popup');
                    }
                });
            }
        } catch { /* localStorage unavailable */ }
    }

    function initSidebarStickiness(): void {
        const select = document.getElementById('pref-sidebar-sticky') as HTMLSelectElement | null;
        if (!select) return;

        try {
            const stored = localStorage.getItem('snakk:sidebar-sticky');
            const current = (stored === 'none' || stored === 'both') ? stored : 'left-only';
            select.value = current;

            select.addEventListener('change', () => {
                const value = select.value;
                const root = document.documentElement;
                root.classList.remove('sticky-none', 'sticky-left-only');
                if (value === 'none') {
                    localStorage.setItem('snakk:sidebar-sticky', 'none');
                    root.classList.add('sticky-none');
                } else if (value === 'both') {
                    localStorage.setItem('snakk:sidebar-sticky', 'both');
                } else {
                    localStorage.setItem('snakk:sidebar-sticky', 'left-only');
                    root.classList.add('sticky-left-only');
                }
                window.dispatchEvent(new Event('snakk:sticky-changed'));
            });
        } catch { /* localStorage unavailable */ }
    }

    function initSkinTonePicker(): void {
        const picker = document.getElementById('skin-tone-picker');
        if (!picker) return;

        const stored = localStorage.getItem('snakk:skin-tone') || '';
        const swatches = picker.querySelectorAll('.skin-tone-swatch');
        swatches.forEach(btn => {
            btn.classList.toggle('active', (btn as HTMLElement).dataset.tone === stored);
        });

        picker.addEventListener('click', (e) => {
            const btn = (e.target as HTMLElement).closest('.skin-tone-swatch') as HTMLElement | null;
            if (!btn) return;

            const tone = btn.dataset.tone || '';
            localStorage.setItem('snakk:skin-tone', tone);

            swatches.forEach(s => s.classList.toggle('active', s === btn));
        });
    }

    function initContentWidth(): void {
        const openBtn   = document.getElementById('btn-content-width');
        const modal     = document.getElementById('content-width-modal');
        const backdrop  = document.getElementById('content-width-backdrop');
        const slider    = document.getElementById('pref-content-width') as HTMLInputElement | null;
        const label     = document.getElementById('pref-content-width-label');
        const applyBtn  = document.getElementById('btn-content-width-apply');
        const cancelBtn = document.getElementById('btn-content-width-cancel');
        const cancelX   = document.getElementById('btn-content-width-cancel-x');
        const resetBtn  = document.getElementById('btn-content-width-reset');
        if (!openBtn || !modal || !slider || !label || !applyBtn || !cancelBtn || !cancelX || !resetBtn) return;

        document.body.appendChild(modal);

        let originalValue = 54;

        function applyPreview(val: number): void {
            slider!.value = String(val);
            label!.textContent = val === 54 ? 'Default' : `${val} rem`;
            if (val === 54) {
                document.documentElement.style.removeProperty('--content-width');
            } else {
                document.documentElement.style.setProperty('--content-width', `${val}rem`);
            }
        }

        function openModal(): void {
            const stored = localStorage.getItem('snakk:content-width');
            originalValue = stored ? parseInt(stored, 10) : 54;
            applyPreview(originalValue);
            modal!.classList.remove('hidden');
            document.addEventListener('keydown', onEscape);
        }

        function closeApply(): void {
            const val = parseInt(slider!.value, 10);
            if (val === 54) localStorage.removeItem('snakk:content-width');
            else localStorage.setItem('snakk:content-width', String(val));
            closeModal();
        }

        function closeRevert(): void {
            applyPreview(originalValue);
            closeModal();
        }

        function closeModal(): void {
            modal!.classList.add('hidden');
            document.removeEventListener('keydown', onEscape);
        }

        function onEscape(e: KeyboardEvent): void {
            if (e.key === 'Escape') closeRevert();
        }

        openBtn.addEventListener('click', openModal);
        applyBtn.addEventListener('click', closeApply);
        cancelBtn.addEventListener('click', closeRevert);
        cancelX.addEventListener('click', closeRevert);
        backdrop!.addEventListener('click', closeRevert);
        resetBtn.addEventListener('click', () => applyPreview(54));
        slider.addEventListener('input', () => applyPreview(parseInt(slider.value, 10)));
    }

    function attachEventListeners(): void {
        initDisplayPreferences();
        initSidebarStickiness();
        initSkinTonePicker();
        initContentWidth();
        initEmbedPreferences();

        const avatarForm = document.getElementById('avatar-upload-form') as HTMLFormElement | null;
        const deleteBtn = document.getElementById('delete-avatar-btn') as HTMLButtonElement | null;

        if (avatarForm && !avatarForm.dataset.initialized) {
            avatarForm.dataset.initialized = 'true';
            initAvatarDropZone();
            avatarForm.addEventListener('submit', handleAvatarUploadSubmit);
            if (deleteBtn) deleteBtn.addEventListener('click', handleDeleteClick);
        }

        const changeNameBtn = document.getElementById('change-name-btn') as HTMLButtonElement | null;
        if (changeNameBtn && !changeNameBtn.dataset.initialized) {
            changeNameBtn.dataset.initialized = 'true';
            changeNameBtn.addEventListener('click', openDisplayNameModal);
        }

        const displayNameModalSubmit = document.getElementById('display-name-modal-submit');
        if (displayNameModalSubmit && !displayNameModalSubmit.dataset.initialized) {
            displayNameModalSubmit.dataset.initialized = 'true';
            displayNameModalSubmit.addEventListener('click', handleDisplayNameModalSubmit);
            document.getElementById('display-name-input')?.addEventListener('keydown', (e) => {
                if ((e as KeyboardEvent).key === 'Enter') handleDisplayNameModalSubmit();
            });
            document.getElementById('display-name-modal-cancel')?.addEventListener('click', () => closeModal('modal-display-name'));
            document.getElementById('display-name-modal-cancel-x')?.addEventListener('click', () => closeModal('modal-display-name'));
            document.getElementById('modal-display-name')?.addEventListener('mousedown', (e) => {
                if (e.target === e.currentTarget) closeModal('modal-display-name');
            });
            document.addEventListener('keydown', (e) => {
                if (e.key !== 'Escape') return;
                const el = document.getElementById('modal-display-name');
                if (el && el.style.display !== 'none') closeModal('modal-display-name');
            });
        }

        const bioInput = document.getElementById('bio-input') as HTMLTextAreaElement | null;
        const bioCharCount = document.getElementById('bio-char-count');
        const saveBioBtn = document.getElementById('save-bio-btn') as HTMLButtonElement | null;
        if (bioInput && !bioInput.dataset.initialized) {
            bioInput.dataset.initialized = 'true';
            bioInput.addEventListener('input', () => {
                if (bioCharCount) bioCharCount.textContent = String(bioInput.value.length);
            });
            if (saveBioBtn) saveBioBtn.addEventListener('click', handleBioSave);
        }

        const autoFollowCheckbox = document.querySelector('input[name="autoFollowOnReply"][type="checkbox"]') as HTMLInputElement | null;
        if (autoFollowCheckbox && !autoFollowCheckbox.dataset.initialized) {
            autoFollowCheckbox.dataset.initialized = 'true';
            autoFollowCheckbox.addEventListener('change', (e) => handleAutoFollowPreferenceChange(e.target as HTMLInputElement));
        }

        document.querySelectorAll<HTMLInputElement>('input[name="allowAdultContent"][type="radio"]').forEach(radio => {
            if (radio.dataset.initialized) return;
            radio.dataset.initialized = 'true';
            radio.addEventListener('change', (e) => handleAdultContentPreferenceChange(e.target as HTMLInputElement));
        });

        document.querySelectorAll<HTMLInputElement>('input[name="adultPreviewImageMode"][type="radio"]').forEach(radio => {
            if (radio.dataset.initialized) return;
            radio.dataset.initialized = 'true';
            radio.addEventListener('change', (e) => handleAdultPreviewImageModeChange(e.target as HTMLInputElement));
        });

        const timezoneSelect = document.getElementById('timezone-select') as HTMLSelectElement | null;
        if (timezoneSelect && !timezoneSelect.dataset.initialized) {
            timezoneSelect.dataset.initialized = 'true';
            timezoneSelect.addEventListener('change', (e) => handleTimezoneChange(e.target as HTMLSelectElement));
        }

        const generateTokenBtn = document.getElementById('generate-feed-token-btn');
        if (generateTokenBtn && !generateTokenBtn.dataset.initialized) {
            generateTokenBtn.dataset.initialized = 'true';
            generateTokenBtn.addEventListener('click', handleGenerateFeedToken);
        }

        const regenerateTokenBtn = document.getElementById('regenerate-feed-token-btn');
        if (regenerateTokenBtn && !regenerateTokenBtn.dataset.initialized) {
            regenerateTokenBtn.dataset.initialized = 'true';
            regenerateTokenBtn.addEventListener('click', handleGenerateFeedToken);
        }

        const revokeTokenBtn = document.getElementById('revoke-feed-token-btn');
        if (revokeTokenBtn && !revokeTokenBtn.dataset.initialized) {
            revokeTokenBtn.dataset.initialized = 'true';
            revokeTokenBtn.addEventListener('click', handleRevokeFeedToken);
        }

        const discordLinkBtn = document.getElementById('discord-link-btn');
        if (discordLinkBtn && !discordLinkBtn.dataset.initialized) {
            discordLinkBtn.dataset.initialized = 'true';
            discordLinkBtn.addEventListener('click', handleDiscordLink);
        }

        const discordUnlinkBtn = document.getElementById('discord-unlink-btn');
        if (discordUnlinkBtn && !discordUnlinkBtn.dataset.initialized) {
            discordUnlinkBtn.dataset.initialized = 'true';
            discordUnlinkBtn.addEventListener('click', handleDiscordUnlink);
        }
    }

    // --- Section nav links ---

    function initScrollspy(): void {
        const navLinks = document.querySelectorAll<HTMLAnchorElement>('[data-scrollspy-link]');
        if (navLinks.length === 0) return;

        navLinks.forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const id = link.getAttribute('href')?.replace('#', '') ?? '';
                const target = document.getElementById(id);
                if (target) {
                    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            });
        });
    }

    // ===== Modal helpers (shared across settings-privacy.ts via window.snakkModal) =====

    function openModal(id: string): void {
        const el = document.getElementById(id);
        if (!el) return;
        el.style.display = '';
        document.body.style.overflow = 'hidden';
        requestAnimationFrame(() => {
            const first = el.querySelector<HTMLElement>(
                'input:not([type="hidden"]):not([disabled]):not(.hidden), textarea:not([disabled]), select:not([disabled])'
            );
            first?.focus();
        });
    }

    function closeModal(id: string): void {
        const el = document.getElementById(id);
        if (!el) return;
        el.style.display = 'none';
        document.body.style.overflow = '';
    }

    function showModalStatus(statusId: string, text: string, isError = false): void {
        const el = document.getElementById(statusId);
        if (!el) return;
        el.textContent = text;
        el.className = 'text-sm mt-2 ' + (isError ? 'text-error' : 'text-success');
        el.classList.remove('hidden');
    }

    function clearModalStatus(statusId: string): void {
        const el = document.getElementById(statusId);
        if (el) { el.textContent = ''; el.classList.add('hidden'); }
    }

    // Expose for other scripts on the same page
    (window as any).snakkModal = { open: openModal, close: closeModal };
    (window as any).snakkSettings = { twoFactorEnabled: false, hasPassword: true, connectedProviders: [] as string[] };

    // ===== Sudo Modal =====

    function getEnable2FATotpValue(): string {
        return Array.from(document.querySelectorAll<HTMLInputElement>('.enable-2fa-totp-digit'))
            .map(el => el.value).join('');
    }

    function clearEnable2FATotpInputs(): void {
        document.querySelectorAll<HTMLInputElement>('.enable-2fa-totp-digit').forEach(el => { el.value = ''; });
    }

    function getSudoTotpValue(): string {
        return Array.from(document.querySelectorAll<HTMLInputElement>('.sudo-totp-digit'))
            .map(el => el.value).join('');
    }

    function clearSudoTotpInputs(): void {
        document.querySelectorAll<HTMLInputElement>('.sudo-totp-digit').forEach(el => { el.value = ''; });
    }

    function focusSudoTotpFirst(): void {
        (document.querySelector('.sudo-totp-digit') as HTMLInputElement | null)?.focus();
    }

    function setSudoExpiry(expiresInSeconds: number): void {
        sudoExpiresAt = Date.now() + expiresInSeconds * 1000;
        try { sessionStorage.setItem(SUDO_EXPIRES_KEY, String(sudoExpiresAt)); } catch { /* ignore */ }
        updateSudoLockIcons();
        startSudoIconTimer();
    }

    function invalidateSudo(): void {
        sudoExpiresAt = 0;
        try { sessionStorage.removeItem(SUDO_EXPIRES_KEY); } catch { /* ignore */ }
        stopSudoIconTimer();
        updateSudoLockIcons();
    }

    function restoreSudoExpiry(): void {
        try {
            const stored = sessionStorage.getItem(SUDO_EXPIRES_KEY);
            if (stored) sudoExpiresAt = parseInt(stored, 10) || 0;
        } catch { /* ignore */ }
        if (isSudoValid()) {
            requestAnimationFrame(() => { updateSudoLockIcons(); startSudoIconTimer(); });
        }
    }

    function updateSudoLockIcons(): void {
        const valid = isSudoValid();
        const icons = document.querySelectorAll<HTMLElement>('.sn-settings-section-title .icon-lock-closed');
        icons.forEach(icon => {
            const existingGroup = icon.closest<HTMLElement>('.sn-sudo-verified-group');
            if (valid) {
                const mins = Math.max(1, Math.ceil((sudoExpiresAt - Date.now()) / 60_000));
                const tip = `Verified — active for ~${mins} min`;
                icon.style.color = 'var(--accent-success,#10b981)';
                icon.title = tip;
                if (!existingGroup) {
                    if (!icon.dataset.origTitle) icon.dataset.origTitle = icon.getAttribute('title') ?? '';
                    const group = document.createElement('span');
                    group.className = 'sn-sudo-verified-group';
                    group.style.cssText = 'display:inline-flex;align-items:center;gap:0.1875rem;flex-shrink:0';
                    icon.parentNode!.insertBefore(group, icon);
                    group.appendChild(icon);
                    const check = document.createElement('span');
                    check.className = 'sn-sudo-check';
                    check.textContent = '✓';
                    check.setAttribute('aria-hidden', 'true');
                    check.style.cssText = 'font-size:0.6rem;font-weight:700;color:var(--accent-success,#10b981);line-height:1';
                    group.appendChild(check);
                }
            } else {
                icon.style.color = 'var(--text-tertiary)';
                if (icon.dataset.origTitle !== undefined) icon.title = icon.dataset.origTitle;
                if (existingGroup) {
                    existingGroup.parentNode?.insertBefore(icon, existingGroup);
                    existingGroup.remove();
                }
            }
        });
    }

    function startSudoIconTimer(): void {
        stopSudoIconTimer();
        sudoIconTimer = setInterval(() => {
            updateSudoLockIcons();
            if (!isSudoValid()) stopSudoIconTimer();
        }, 60_000);
    }

    function stopSudoIconTimer(): void {
        if (sudoIconTimer !== null) { clearInterval(sudoIconTimer); sudoIconTimer = null; }
    }

    function isSudoValid(): boolean {
        return Date.now() < sudoExpiresAt;
    }

    function ensureSudo(callback: () => void): void {
        const settings = (window as any).snakkSettings as { hasPassword?: boolean; twoFactorEnabled?: boolean } | undefined;
        const hasPassword = settings?.hasPassword !== false;
        const has2FA = settings?.twoFactorEnabled === true;

        if (isSudoValid()) {
            callback();
            return;
        }

        if (!hasPassword && !has2FA) {
            issueSudoToken('', '').then(ok => { if (ok) callback(); });
            return;
        }

        sudoCallback = callback;
        openSudoModal();
    }

    function openSudoModal(): void {
        const settings = (window as any).snakkSettings as { hasPasskeys?: boolean; connectedProviders?: string[] } | undefined;
        const pwdInput = document.getElementById('sudo-password') as HTMLInputElement | null;
        const pwdSection = document.getElementById('sudo-password-section');
        const passkeySection = document.getElementById('sudo-passkey-section');
        const oauthSection = document.getElementById('sudo-oauth-section');
        const oauthButtons = document.getElementById('sudo-oauth-buttons');

        if (pwdInput) pwdInput.value = '';
        pendingPassword = null;
        clearModalStatus('sudo-auth-status');
        pendingPasskeyChallenge = null;

        if (settings?.hasPasskeys) {
            passkeySection?.classList.remove('hidden');
            pwdSection?.classList.add('hidden');
        } else {
            passkeySection?.classList.add('hidden');
            pwdSection?.classList.remove('hidden');
        }

        const providers = settings?.connectedProviders ?? [];
        if (providers.length > 0 && oauthSection && oauthButtons) {
            oauthButtons.innerHTML = '';
            for (const p of providers) {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'btn btn-ghost btn-sm sudo-oauth-btn w-full';
                btn.dataset.provider = p;
                btn.textContent = (window as any).T?.settings?.sudoContinueWith
                    ? `${(window as any).T.settings.sudoContinueWith} ${p.charAt(0).toUpperCase() + p.slice(1)}`
                    : `Continue with ${p.charAt(0).toUpperCase() + p.slice(1)}`;
                oauthButtons.appendChild(btn);
            }
            oauthSection.classList.remove('hidden');
        } else {
            oauthSection?.classList.add('hidden');
        }

        openModal('modal-sudo-auth');
        if (!settings?.hasPasskeys) setTimeout(() => pwdInput?.focus(), 50);
    }

    const base64urlToBuffer = (base64url: string): ArrayBuffer => (window as any).SnakkWebAuthn.base64urlToBuffer(base64url) as ArrayBuffer;
    const bufferToBase64url = (buffer: ArrayBuffer): string => (window as any).SnakkWebAuthn.bufferToBase64url(buffer) as string;

    function serializeAssertion(cred: PublicKeyCredential): string {
        const resp = cred.response as AuthenticatorAssertionResponse;
        return JSON.stringify({
            id: cred.id,
            rawId: bufferToBase64url(cred.rawId),
            type: cred.type,
            response: {
                authenticatorData: bufferToBase64url(resp.authenticatorData),
                clientDataJSON: bufferToBase64url(resp.clientDataJSON),
                signature: bufferToBase64url(resp.signature),
                userHandle: resp.userHandle ? bufferToBase64url(resp.userHandle) : null
            }
        });
    }

    async function beginSudoPasskey(): Promise<void> {
        const btn = document.getElementById('sudo-passkey-btn') as HTMLButtonElement | null;
        if (btn) btn.disabled = true;
        clearModalStatus('sudo-auth-status');

        try {
            const beginRes = await fetch('/bff/auth/sudo/passkey/begin', {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: '{}'
            });
            if (!beginRes.ok) {
                const d = await beginRes.json().catch(() => ({}));
                showModalStatus('sudo-auth-status', (d as any).error || 'Could not start passkey verification', true);
                return;
            }
            const beginData = await beginRes.json() as { optionsJson: string; challengeId: string };

            const raw = JSON.parse(beginData.optionsJson) as {
                challenge: string;
                allowCredentials?: Array<{ id: string; type: string; transports?: AuthenticatorTransport[] }>;
                timeout?: number;
                userVerification?: UserVerificationRequirement;
                rpId?: string;
            };

            const opts: PublicKeyCredentialRequestOptions = {
                challenge: base64urlToBuffer(raw.challenge),
                allowCredentials: raw.allowCredentials?.map(c => ({
                    id: base64urlToBuffer(c.id),
                    type: c.type as PublicKeyCredentialType,
                    transports: c.transports
                })),
                timeout: raw.timeout,
                userVerification: raw.userVerification ?? 'required',
                rpId: raw.rpId
            };

            const cred = await navigator.credentials.get({ publicKey: opts });
            if (!cred) { showModalStatus('sudo-auth-status', 'No credential returned', true); return; }

            pendingPasskeyChallenge = {
                challengeId: beginData.challengeId,
                assertionJson: serializeAssertion(cred as PublicKeyCredential)
            };

            await completeSudoPasskey();
        } catch (err) {
            if (err instanceof Error && (err.name === 'NotAllowedError' || err.name === 'AbortError')) return;
            showModalStatus('sudo-auth-status', 'Passkey verification failed', true);
        } finally {
            if (btn) btn.disabled = false;
        }
    }

    async function completeSudoPasskey(): Promise<void> {
        if (!pendingPasskeyChallenge) { showModalStatus('sudo-auth-status', 'No pending passkey challenge', true); return; }

        try {
            const response = await fetch('/bff/auth/sudo/passkey/complete', {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    challengeId: pendingPasskeyChallenge.challengeId,
                    assertionResponseJson: pendingPasskeyChallenge.assertionJson
                })
            });
            const data = await response.json();
            if (!response.ok) {
                showModalStatus('sudo-auth-status', data.error || 'Authentication failed', true);
                return;
            }

            setSudoExpiry(data.expiresInSeconds ?? 300);
            pendingPasskeyChallenge = null;

            closeModal('modal-sudo-auth');
            const cb = sudoCallback;
            sudoCallback = null;
            cb?.();
        } catch {
            showModalStatus('sudo-auth-status', 'Network error', true);
        }
    }

    async function issueSudoToken(password: string, totpCode: string, statusId: string = 'sudo-auth-status'): Promise<boolean> {
        try {
            const response = await fetch('/bff/auth/sudo', {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    password: password || undefined,
                    totpCode: totpCode || undefined
                })
            });
            const data = await response.json();
            if (!response.ok) {
                showModalStatus(statusId, data.error || 'Authentication failed', true);
                return false;
            }
            setSudoExpiry(data.expiresInSeconds ?? 300);
            return true;
        } catch {
            showModalStatus(statusId, 'Network error', true);
            return false;
        }
    }

    async function handleSudoAuthConfirm(): Promise<void> {
        const btn = document.getElementById('sudo-auth-confirm-btn') as HTMLButtonElement | null;
        const pwdInput = document.getElementById('sudo-password') as HTMLInputElement | null;
        const settings = (window as any).snakkSettings as { hasPassword?: boolean; twoFactorEnabled?: boolean } | undefined;

        const password = pwdInput?.value || '';

        if (settings?.hasPassword !== false && !password) {
            showModalStatus('sudo-auth-status', 'Enter your password', true);
            return;
        }

        if (settings?.twoFactorEnabled) {
            if (btn) btn.disabled = true;
            try {
                const verifyRes = await fetch('/bff/me/verify-credential', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ password })
                });
                if (!verifyRes.ok) {
                    const d = await verifyRes.json().catch(() => ({}));
                    showModalStatus('sudo-auth-status', (d as any).error || 'Incorrect password', true);
                    return;
                }
            } catch {
                showModalStatus('sudo-auth-status', 'Network error', true);
                return;
            } finally {
                if (btn) btn.disabled = false;
            }
            pendingPassword = password;
            sudoTotpAutoSubmit = true;
            closeModal('modal-sudo-auth');
            clearModalStatus('sudo-totp-status');
            clearSudoTotpInputs();
            openModal('modal-sudo-totp');
            setTimeout(() => focusSudoTotpFirst(), 50);
            return;
        }

        if (btn) btn.disabled = true;
        const ok = await issueSudoToken(password, '', 'sudo-auth-status');
        if (ok) {
            closeModal('modal-sudo-auth');
            const cb = sudoCallback;
            sudoCallback = null;
            cb?.();
        }
        if (btn) btn.disabled = false;
    }

    async function handleSudoTotpConfirm(): Promise<void> {
        const btn = document.getElementById('sudo-totp-confirm-btn') as HTMLButtonElement | null;
        const totp = getSudoTotpValue();

        if (totp.length < 6) {
            showModalStatus('sudo-totp-status', 'Enter your 2FA code', true);
            return;
        }

        if (btn) btn.disabled = true;
        const ok = await issueSudoToken(pendingPassword ?? '', totp, 'sudo-totp-status');
        if (ok) {
            pendingPassword = null;
            closeModal('modal-sudo-totp');
            const cb = sudoCallback;
            sudoCallback = null;
            cb?.();
        } else {
            sudoTotpAutoSubmit = false;
        }
        if (btn) btn.disabled = false;
    }

    function initSudoModals(): void {
        // Always expose snakkSudo so it's available on Account and Integrations pages too
        (window as any).snakkSudo = {
            ensure: ensureSudo,
            isValid: isSudoValid
        };

        // Only wire DOM events if the modals are present on this page
        if (!document.getElementById('modal-sudo-auth')) return;
        const sudoModal = document.getElementById('modal-sudo-auth') as HTMLElement;
        if (sudoModal.dataset.initialized) return;
        sudoModal.dataset.initialized = 'true';

        // Auth modal (step 1)
        document.getElementById('sudo-auth-confirm-btn')?.addEventListener('click', handleSudoAuthConfirm);
        document.getElementById('sudo-auth-cancel-btn')?.addEventListener('click', () => closeModal('modal-sudo-auth'));
        document.getElementById('sudo-auth-cancel-btn-footer')?.addEventListener('click', () => closeModal('modal-sudo-auth'));
        document.getElementById('sudo-passkey-btn')?.addEventListener('click', beginSudoPasskey);
        document.getElementById('sudo-oauth-buttons')?.addEventListener('click', (e) => {
            const btn = (e.target as HTMLElement).closest<HTMLButtonElement>('.sudo-oauth-btn');
            if (btn?.dataset.provider) beginSudoOAuth(btn.dataset.provider);
        });
        document.getElementById('sudo-use-password-link')?.addEventListener('click', () => {
            document.getElementById('sudo-passkey-section')?.classList.add('hidden');
            document.getElementById('sudo-password-section')?.classList.remove('hidden');
            (document.getElementById('sudo-password') as HTMLInputElement | null)?.focus();
        });
        document.getElementById('sudo-password')?.addEventListener('keydown', (e) => {
            if ((e as KeyboardEvent).key === 'Enter') handleSudoAuthConfirm();
        });
        document.getElementById('modal-sudo-auth')?.addEventListener('mousedown', (e) => {
            if (e.target === e.currentTarget) closeModal('modal-sudo-auth');
        });

        // TOTP modal (step 2)
        document.getElementById('sudo-totp-confirm-btn')?.addEventListener('click', handleSudoTotpConfirm);
        document.getElementById('sudo-totp-cancel-btn')?.addEventListener('click', () => closeModal('modal-sudo-totp'));
        document.getElementById('sudo-totp-cancel-btn-footer')?.addEventListener('click', () => closeModal('modal-sudo-totp'));
        document.getElementById('modal-sudo-totp')?.addEventListener('mousedown', (e) => {
            if (e.target === e.currentTarget) closeModal('modal-sudo-totp');
        });

        // 6-digit TOTP input wiring
        const totpDigits = Array.from(document.querySelectorAll<HTMLInputElement>('.sudo-totp-digit'));
        totpDigits.forEach((input, i) => {
            input.addEventListener('keydown', (e) => {
                const key = e.key;
                if (key === 'Backspace' && !input.value && i > 0) {
                    totpDigits[i - 1]!.value = '';
                    totpDigits[i - 1]!.focus();
                    e.preventDefault();
                    return;
                }
                if (!/^\d$/.test(key) && !['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight'].includes(key)) {
                    e.preventDefault();
                }
            });
            input.addEventListener('input', () => {
                input.value = input.value.replace(/\D/g, '').slice(0, 1);
                if (input.value && i < totpDigits.length - 1) {
                    totpDigits[i + 1]!.focus();
                }
                if (totpDigits.every(d => d.value) && sudoTotpAutoSubmit) {
                    handleSudoTotpConfirm();
                }
            });
            input.addEventListener('paste', (e) => {
                e.preventDefault();
                const digits = ((e as ClipboardEvent).clipboardData?.getData('text') ?? '').replace(/\D/g, '').slice(0, 6 - i);
                digits.split('').forEach((d, j) => {
                    if (totpDigits[i + j]) totpDigits[i + j]!.value = d;
                });
                const nextEmpty = totpDigits.findIndex(d => !d.value);
                (totpDigits[nextEmpty === -1 ? totpDigits.length - 1 : nextEmpty] as HTMLInputElement).focus();
                if (totpDigits.every(d => d.value) && sudoTotpAutoSubmit) {
                    handleSudoTotpConfirm();
                }
            });
            input.addEventListener('focus', () => input.select());
        });
    }

    // ===== Two-Factor Authentication =====

    function show2FAPageState(state: 'loading' | 'disabled' | 'enabled'): void {
        const loading = document.getElementById('2fa-loading');
        const status = document.getElementById('2fa-status');
        const disabled = document.getElementById('2fa-state-disabled');
        const enabled = document.getElementById('2fa-state-enabled');
        if (loading) loading.classList.toggle('hidden', state !== 'loading');
        if (status) status.classList.toggle('hidden', state === 'loading');
        if (disabled) disabled.classList.toggle('hidden', state !== 'disabled');
        if (enabled) enabled.classList.toggle('hidden', state !== 'enabled');
    }

    async function load2FAStatus(): Promise<void> {
        show2FAPageState('loading');
        try {
            const response = await fetch('/bff/auth/2fa/status', { credentials: 'include' });
            if (!response.ok) { show2FAPageState('disabled'); return; }
            const data = await response.json();
            const snakkSettings = (window as any).snakkSettings;
            if (data.isEnabled) {
                if (snakkSettings) snakkSettings.twoFactorEnabled = true;
                show2FAPageState('enabled');
                const backupStatus = document.getElementById('2fa-backup-status');
                if (backupStatus && data.unusedBackupCodes !== undefined) {
                    backupStatus.textContent = `${data.unusedBackupCodes} backup codes remaining`;
                }
            } else {
                if (snakkSettings) snakkSettings.twoFactorEnabled = false;
                show2FAPageState('disabled');
            }
        } catch {
            show2FAPageState('disabled');
        }
    }

    function openEnableModal(): void {
        clearModalStatus('2fa-enable-status');
        clearEnable2FATotpInputs();
        enable2FaAutoSubmit = true;
        ensureSudo(() => handle2FASetupContinue());
    }

    async function handle2FASetupContinue(): Promise<void> {
        clearModalStatus('2fa-enable-status');

        try {
            const response = await fetch('/bff/auth/2fa/setup', {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: '{}'
            });
            const data = await response.json();
            if (!response.ok) {
                showModalStatus('2fa-enable-status', data.error || 'Failed to start setup', true);
                return;
            }

            const qrImg = document.getElementById('2fa-qr-img') as HTMLImageElement | null;
            const manualKey = document.getElementById('2fa-manual-key');
            if (qrImg) qrImg.src = `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(data.qrCodeUri || data.qrCodeUrl || '')}`;
            if (manualKey) manualKey.textContent = data.secret;

            clearEnable2FATotpInputs();
            enable2FaAutoSubmit = true;

            openModal('modal-2fa-enable');
        } catch {
            showModalStatus('2fa-enable-status', 'Network error', true);
        }
    }

    async function handle2FAVerify(): Promise<void> {
        const btn = document.getElementById('2fa-verify-btn') as HTMLButtonElement | null;
        const code = getEnable2FATotpValue();
        if (code.length < 6) { showModalStatus('2fa-enable-status', 'Enter a code', true); return; }
        if (btn) btn.disabled = true;

        try {
            const response = await fetch('/bff/auth/2fa/enable', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ code })
            });
            const data = await response.json();
            if (!response.ok) {
                enable2FaAutoSubmit = false;
                showModalStatus('2fa-enable-status', data.error || 'Invalid code', true);
                return;
            }

            const grid = document.getElementById('2fa-codes-grid');
            if (grid && data.backupCodes) {
                grid.innerHTML = '';
                (data.backupCodes as string[]).forEach(c => {
                    const el = document.createElement('div');
                    el.className = 'py-0.5';
                    el.textContent = c;
                    grid.appendChild(el);
                });
            }

            closeModal('modal-2fa-enable');
            openModal('modal-2fa-backup-codes');
            load2FAStatus();
        } catch {
            enable2FaAutoSubmit = false;
            showModalStatus('2fa-enable-status', 'Network error', true);
        } finally {
            if (btn) btn.disabled = false;
        }
    }

    function openDisableModal(): void {
        clearModalStatus('2fa-disable-status');
        ensureSudo(() => openModal('modal-2fa-disable'));
    }

    async function handle2FADisable(): Promise<void> {
        const btn = document.getElementById('2fa-confirm-disable-btn') as HTMLButtonElement | null;
        if (!isSudoValid()) { showModalStatus('2fa-disable-status', 'Authentication required', true); return; }
        if (btn) btn.disabled = true;

        try {
            const response = await fetch('/bff/auth/2fa/disable', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: '{}'
            });
            if (response.ok) {
                closeModal('modal-2fa-disable');
                const snakkSettings = (window as any).snakkSettings;
                if (snakkSettings) snakkSettings.twoFactorEnabled = false;
                show2FAPageState('disabled');
            } else if (response.status === 403) {
                invalidateSudo();
                showModalStatus('2fa-disable-status', 'Session expired. Please re-authenticate.', true);
            } else {
                const data = await response.json();
                showModalStatus('2fa-disable-status', data.error || 'Failed to disable', true);
            }
        } catch {
            showModalStatus('2fa-disable-status', 'Network error', true);
        } finally {
            if (btn) btn.disabled = false;
        }
    }

    function openRegenModal(): void {
        clearModalStatus('regen-status');
        const codesArea = document.getElementById('regen-codes-area') as HTMLElement | null;
        const confirmBtn = document.getElementById('regen-confirm-btn') as HTMLElement | null;
        const cancelBtn = document.getElementById('regen-btn-cancel') as HTMLElement | null;
        const copyBtn = document.getElementById('regen-copy-codes-btn') as HTMLElement | null;
        const doneBtn = document.getElementById('regen-done-btn') as HTMLElement | null;
        if (codesArea) codesArea.style.display = 'none';
        if (confirmBtn) confirmBtn.style.display = '';
        if (cancelBtn) cancelBtn.style.display = '';
        if (copyBtn) copyBtn.style.display = 'none';
        if (doneBtn) doneBtn.style.display = 'none';
        ensureSudo(() => openModal('modal-2fa-regen'));
    }

    async function handle2FARegenerate(): Promise<void> {
        const btn = document.getElementById('regen-confirm-btn') as HTMLButtonElement | null;
        if (!isSudoValid()) { showModalStatus('regen-status', 'Authentication required', true); return; }
        if (btn) btn.disabled = true;

        try {
            const response = await fetch('/bff/auth/2fa/backup-codes/regenerate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: '{}'
            });
            if (response.status === 403) { invalidateSudo(); showModalStatus('regen-status', 'Session expired. Please re-authenticate.', true); return; }
            const data = await response.json();
            if (!response.ok) { showModalStatus('regen-status', data.error || 'Failed', true); return; }

            const grid = document.getElementById('regen-codes-grid');
            if (grid && data.backupCodes) {
                grid.innerHTML = '';
                (data.backupCodes as string[]).forEach(code => {
                    const el = document.createElement('div');
                    el.className = 'py-0.5';
                    el.textContent = code;
                    grid.appendChild(el);
                });
            }

            const ra = document.getElementById('regen-codes-area') as HTMLElement | null;
            const rcBtn = document.getElementById('regen-confirm-btn') as HTMLElement | null;
            const rkBtn = document.getElementById('regen-btn-cancel') as HTMLElement | null;
            const cpBtn = document.getElementById('regen-copy-codes-btn') as HTMLElement | null;
            const dnBtn = document.getElementById('regen-done-btn') as HTMLElement | null;
            if (ra) ra.style.display = '';
            if (rcBtn) rcBtn.style.display = 'none';
            if (rkBtn) rkBtn.style.display = 'none';
            if (cpBtn) cpBtn.style.display = '';
            if (dnBtn) dnBtn.style.display = '';

            load2FAStatus();
        } catch {
            showModalStatus('regen-status', 'Network error', true);
        } finally {
            if (btn) btn.disabled = false;
        }
    }

    function init2FA(): void {
        initSudoModals();

        const section = document.getElementById('two-factor-section');
        if (!section || section.dataset.initialized) return;
        section.dataset.initialized = 'true';

        // Page-level buttons
        document.getElementById('2fa-setup-btn')?.addEventListener('click', openEnableModal);
        document.getElementById('2fa-disable-btn')?.addEventListener('click', openDisableModal);
        document.getElementById('2fa-regen-btn')?.addEventListener('click', openRegenModal);

        // Enable modal — TOTP verify + copy secret
        document.getElementById('2fa-verify-btn')?.addEventListener('click', handle2FAVerify);
        document.getElementById('2fa-copy-secret-btn')?.addEventListener('click', () => {
            const key = document.getElementById('2fa-manual-key')?.textContent?.trim() || '';
            if (key) navigator.clipboard.writeText(key);
        });

        // 6-digit enable-2FA TOTP input wiring
        const enable2FaDigits = Array.from(document.querySelectorAll<HTMLInputElement>('.enable-2fa-totp-digit'));
        enable2FaDigits.forEach((input, i) => {
            input.addEventListener('keydown', (e) => {
                const key = e.key;
                if (key === 'Enter') { e.preventDefault(); handle2FAVerify(); return; }
                if (key === 'Backspace' && !input.value && i > 0) {
                    enable2FaDigits[i - 1]!.value = '';
                    enable2FaDigits[i - 1]!.focus();
                    e.preventDefault();
                    return;
                }
                if (!/^\d$/.test(key) && !['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight'].includes(key)) {
                    e.preventDefault();
                }
            });
            input.addEventListener('input', () => {
                input.value = input.value.replace(/\D/g, '').slice(0, 1);
                if (input.value && i < enable2FaDigits.length - 1) {
                    enable2FaDigits[i + 1]!.focus();
                }
                if (enable2FaDigits.every(d => d.value) && enable2FaAutoSubmit) {
                    handle2FAVerify();
                }
            });
            input.addEventListener('paste', (e) => {
                e.preventDefault();
                const digits = ((e as ClipboardEvent).clipboardData?.getData('text') ?? '').replace(/\D/g, '').slice(0, 6 - i);
                digits.split('').forEach((d, j) => {
                    if (enable2FaDigits[i + j]) enable2FaDigits[i + j]!.value = d;
                });
                const nextEmpty = enable2FaDigits.findIndex(d => !d.value);
                (enable2FaDigits[nextEmpty === -1 ? enable2FaDigits.length - 1 : nextEmpty] as HTMLInputElement).focus();
                if (enable2FaDigits.every(d => d.value) && enable2FaAutoSubmit) {
                    handle2FAVerify();
                }
            });
            input.addEventListener('focus', () => input.select());
        });

        // Backup codes modal
        document.getElementById('2fa-codes-done-btn')?.addEventListener('click', () => {
            closeModal('modal-2fa-backup-codes');
        });
        document.getElementById('2fa-copy-codes-btn')?.addEventListener('click', () => {
            const grid = document.getElementById('2fa-codes-grid');
            if (grid) navigator.clipboard.writeText(Array.from(grid.children).map(el => el.textContent?.trim() || '').filter(Boolean).join('\n'));
        });

        // Disable modal
        document.getElementById('2fa-confirm-disable-btn')?.addEventListener('click', handle2FADisable);

        // Regen modal
        document.getElementById('regen-confirm-btn')?.addEventListener('click', handle2FARegenerate);
        document.getElementById('regen-copy-codes-btn')?.addEventListener('click', () => {
            const grid = document.getElementById('regen-codes-grid');
            if (grid) navigator.clipboard.writeText(Array.from(grid.children).map(el => el.textContent?.trim() || '').filter(Boolean).join('\n'));
        });

        // Generic .sn-modal-close handlers
        document.querySelectorAll<HTMLElement>('.sn-modal-close[data-modal]').forEach(btn => {
            btn.addEventListener('click', () => closeModal(btn.dataset.modal!));
        });

        const allModals = ['modal-sudo-auth', 'modal-sudo-totp', 'modal-2fa-enable', 'modal-2fa-backup-codes', 'modal-2fa-disable', 'modal-2fa-regen'];

        // Backdrop click to close modals
        allModals.forEach(id => {
            document.getElementById(id)?.addEventListener('mousedown', (e) => {
                if (e.target === e.currentTarget) closeModal(id);
            });
        });

        // Escape key
        document.addEventListener('keydown', (e) => {
            if (e.key !== 'Escape') return;
            allModals.forEach(id => {
                const el = document.getElementById(id);
                if (el && el.style.display !== 'none') closeModal(id);
            });
        });

        load2FAStatus();
    }

    // ─── Security page ────────────────────────────────────────────────────────

    function initSecurity(): void {
        if (!document.getElementById('section-sessions')) return;

        loadSessions();

        document.getElementById('revoke-all-sessions-btn')?.addEventListener('click', () => {
            const currentId = document.getElementById('section-sessions')?.dataset.currentSessionId ?? '';
            if (!currentId) return;
            const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
            snakkSudo?.ensure(async () => {
                try {
                    const res = await fetch('/bff/me/sessions', {
                        method: 'DELETE',
                        credentials: 'include',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ excludeSessionId: currentId })
                    });
                    if (res.ok) {
                        showSessionStatus('All other sessions have been signed out.', false);
                        loadSessions();
                    } else if (res.status === 403) {
                        invalidateSudo();
                        showSessionStatus('Session expired. Please re-authenticate.', true);
                    } else {
                        showSessionStatus('Something went wrong. Please try again.', true);
                    }
                } catch {
                    showSessionStatus('Network error. Please try again.', true);
                }
            });
        });

        document.getElementById('view-security-log-btn')?.addEventListener('click', () => {
            const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
            snakkSudo?.ensure(() => openSecurityLogModal());
        });

        document.getElementById('security-log-close-btn')?.addEventListener('click', () => closeModal('modal-security-log'));
        document.getElementById('security-log-close-btn-footer')?.addEventListener('click', () => closeModal('modal-security-log'));
    }

    async function loadSessions(): Promise<void> {
        const loadingEl = document.getElementById('sessions-loading');
        const listEl = document.getElementById('sessions-list');
        const emptyEl = document.getElementById('sessions-empty');
        const actionsEl = document.getElementById('sessions-actions');
        const statusEl = document.getElementById('sessions-status');

        if (loadingEl) loadingEl.classList.remove('hidden');
        if (listEl) { listEl.innerHTML = ''; listEl.classList.add('hidden'); }
        if (emptyEl) emptyEl.classList.add('hidden');
        if (actionsEl) actionsEl.classList.add('hidden');
        if (statusEl) statusEl.classList.add('hidden');

        try {
            const res = await fetch('/bff/me/sessions', { credentials: 'include' });
            if (loadingEl) loadingEl.classList.add('hidden');
            if (!res.ok) return;

            const data = await res.json();
            const sessions: any[] = data.sessions ?? [];

            if (sessions.length === 0) {
                if (emptyEl) emptyEl.classList.remove('hidden');
                return;
            }

            if (listEl) {
                sessions.forEach(s => listEl.appendChild(buildSessionCard(s)));
                listEl.classList.remove('hidden');
            }

            const current = sessions.find(s => s.isCurrent);
            if (current) {
                const section = document.getElementById('section-sessions');
                if (section) section.dataset.currentSessionId = current.id;
            }

            if (actionsEl && sessions.some(s => !s.isCurrent))
                actionsEl.classList.remove('hidden');
        } catch {
            if (loadingEl) loadingEl.classList.add('hidden');
        }
    }

    function buildSessionCard(session: any): HTMLElement {
        const item = document.createElement('div');
        item.className = 'sn-device-item';

        item.appendChild(buildDeviceIconStack(session.userAgent || ''));

        const info = document.createElement('div');
        info.className = 'sn-device-info';

        const nameRow = document.createElement('div');
        nameRow.className = 'sn-device-name';

        const device = document.createElement('span');
        device.textContent = session.deviceHint || 'Unknown device';
        nameRow.appendChild(device);

        if (session.isCurrent) {
            const badge = document.createElement('span');
            badge.className = 'sn-device-badge sn-device-badge--active';
            badge.textContent = (window as any).T?.settings?.secSessionsCurrent ?? 'This device';
            nameRow.appendChild(badge);
        }

        const meta = document.createElement('div');
        meta.className = 'sn-device-meta';
        meta.textContent = `${session.ipAddress || 'Unknown IP'} · ${relativeTime(new Date(session.createdAt))}`;

        info.appendChild(nameRow);
        info.appendChild(meta);
        item.appendChild(info);

        if (!session.isCurrent) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'sn-device-revoke';
            btn.textContent = (window as any).T?.settings?.secSessionsRevokeBtn ?? 'Revoke';
            btn.addEventListener('click', () => {
                const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
                snakkSudo?.ensure(async () => {
                    btn.disabled = true;
                    try {
                        const res = await fetch(`/bff/me/sessions/${encodeURIComponent(session.id)}`, {
                            method: 'DELETE',
                            credentials: 'include'
                        });
                        if (res.ok) {
                            item.remove();
                        } else if (res.status === 403) {
                            invalidateSudo();
                            showSessionStatus('Session expired. Please re-authenticate.', true);
                            btn.disabled = false;
                        } else {
                            showSessionStatus('Could not revoke session.', true);
                            btn.disabled = false;
                        }
                    } catch {
                        showSessionStatus('Network error.', true);
                        btn.disabled = false;
                    }
                });
            });
            item.appendChild(btn);
        }

        return item;
    }

    function showSessionStatus(message: string, isError: boolean): void {
        const el = document.getElementById('sessions-status');
        if (!el) return;
        el.textContent = message;
        el.style.color = isError ? 'var(--color-error,#dc2626)' : 'var(--color-success,#16a34a)';
        el.classList.remove('hidden');
        setTimeout(() => el.classList.add('hidden'), 4000);
    }

    async function openSecurityLogModal(): Promise<void> {
        openModal('modal-security-log');
        const loadingEl = document.getElementById('security-log-loading');
        const listEl = document.getElementById('security-log-list');
        const emptyEl = document.getElementById('security-log-empty');

        if (loadingEl) loadingEl.classList.remove('hidden');
        if (listEl) { listEl.innerHTML = ''; listEl.classList.add('hidden'); }
        if (emptyEl) emptyEl.classList.add('hidden');

        try {
            const res = await fetch('/bff/me/login-history', { credentials: 'include' });
            if (loadingEl) loadingEl.classList.add('hidden');
            if (!res.ok) { if (emptyEl) emptyEl.classList.remove('hidden'); return; }

            const entries: any[] = await res.json();
            if (entries.length === 0) { if (emptyEl) emptyEl.classList.remove('hidden'); return; }

            if (listEl) {
                entries.forEach(e => listEl.appendChild(buildLogEntry(e)));
                listEl.classList.remove('hidden');
            }
        } catch {
            if (loadingEl) loadingEl.classList.add('hidden');
            if (emptyEl) emptyEl.classList.remove('hidden');
        }
    }

    function buildLogEntry(entry: any): HTMLElement {
        const item = document.createElement('div');
        item.className = 'sn-device-item';

        item.appendChild(buildDeviceIconStack(entry.userAgent || ''));

        const info = document.createElement('div');
        info.className = 'sn-device-info';

        const nameRow = document.createElement('div');
        nameRow.className = 'sn-device-name';

        const device = document.createElement('span');
        device.textContent = entry.deviceHint || 'Unknown device';
        nameRow.appendChild(device);

        const badge = document.createElement('span');
        badge.className = 'sn-device-badge ' + (entry.success ? 'sn-device-badge--active' : 'sn-device-badge--expired');
        badge.textContent = entry.success ? 'Signed in' : 'Failed attempt';
        nameRow.appendChild(badge);

        const meta = document.createElement('div');
        meta.className = 'sn-device-meta';
        meta.textContent = `${entry.ipAddress || 'Unknown IP'} · ${relativeTime(new Date(entry.createdAt))}`;

        info.appendChild(nameRow);
        info.appendChild(meta);
        item.appendChild(info);

        return item;
    }

    function relativeTime(date: Date): string {
        const sec = Math.floor((Date.now() - date.getTime()) / 1000);
        if (sec < 60) return 'just now';
        const min = Math.floor(sec / 60);
        if (min < 60) return `${min}m ago`;
        const hr = Math.floor(min / 60);
        if (hr < 24) return `${hr}h ago`;
        const days = Math.floor(hr / 24);
        if (days < 30) return `${days}d ago`;
        return date.toLocaleDateString();
    }

    // ─── OAuth Sudo Re-auth ───────────────────────────────────────────────────

    function beginSudoOAuth(provider: string): void {
        const w = 600, h = 700;
        const left = Math.round(window.screenX + (window.outerWidth - w) / 2);
        const top = Math.round(window.screenY + (window.outerHeight - h) / 2);
        const popup = window.open(
            `/auth/oauth/${encodeURIComponent(provider)}/challenge?sudoMode=true`,
            'snakk-sudo-oauth',
            `width=${w},height=${h},left=${left},top=${top},toolbar=no,menubar=no,location=no`
        );
        if (!popup) {
            showModalStatus('sudo-auth-status', 'Could not open popup. Check your pop-up blocker.', true);
        }
    }

    async function completeSudoOAuth(nonce: string): Promise<void> {
        try {
            const res = await fetch('/bff/auth/sudo/oauth', {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ nonce })
            });
            if (!res.ok) {
                showModalStatus('sudo-auth-status', 'OAuth re-authentication failed. Please try again.', true);
                return;
            }
            const data = await res.json();
            const expiresIn: number = data.expiresInSeconds ?? 300;
            sudoExpiresAt = Date.now() + expiresIn * 1000;
            try { sessionStorage.setItem(SUDO_EXPIRES_KEY, String(sudoExpiresAt)); } catch { /* ignore */ }

            closeModal('modal-sudo-auth');
            const cb = sudoCallback;
            sudoCallback = null;
            cb?.();
        } catch {
            showModalStatus('sudo-auth-status', 'OAuth re-authentication failed. Please try again.', true);
        }
    }

    // Global postMessage handler for OAuth sudo popup
    window.addEventListener('message', (event: MessageEvent) => {
        if (event.origin !== window.location.origin) return;
        if (!event.data || event.data.type !== 'snakk:sudo:oauth-complete') return;
        const nonce: string = event.data.nonce;
        if (nonce) completeSudoOAuth(nonce);
    });

    // ─── Connected Accounts ───────────────────────────────────────────────────

    const PROVIDER_NAMES: Record<string, string> = {
        google: 'Google', github: 'GitHub', discord: 'Discord',
        facebook: 'Facebook', microsoft: 'Microsoft', steam: 'Steam'
    };

    const PROVIDER_ICONS: Record<string, string> = {
        google:    `<img src="/icons/brand-google-color.svg"    width="20" height="20" aria-hidden="true" style="flex-shrink:0" alt="">`,
        github:    `<img src="/icons/brand-github.svg"         width="20" height="20" aria-hidden="true" style="flex-shrink:0" alt="">`,
        discord:   `<img src="/icons/brand-discord-color.svg"  width="20" height="20" aria-hidden="true" style="flex-shrink:0" alt="">`,
        facebook:  `<img src="/icons/brand-facebook-color.svg" width="20" height="20" aria-hidden="true" style="flex-shrink:0" alt="">`,
        microsoft: `<img src="/icons/brand-microsoft-color.svg" width="20" height="20" aria-hidden="true" style="flex-shrink:0" alt="">`,
        steam:     `<img src="/icons/brand-steam.svg"          width="20" height="20" aria-hidden="true" style="flex-shrink:0" alt="">`
    };

    async function initConnectedAccounts(): Promise<void> {
        const list = document.getElementById('connected-accounts-list');
        if (!list) return;

        let connections: any[] = [];
        try {
            const res = await fetch('/bff/settings/oauth-connections', { credentials: 'include' });
            if (res.ok) connections = await res.json();
        } catch { /* ignore */ }

        const connectedMap = new Map(connections.map((c: any) => [c.provider, c]));
        const allProviders = ['google', 'github', 'discord', 'facebook', 'microsoft', 'steam'];

        list.innerHTML = '';

        for (const provider of allProviders) {
            const conn = connectedMap.get(provider);
            const row = document.createElement('div');
            row.className = 'sn-device-item';

            // Logo + name/status
            const left = document.createElement('div');
            left.style.cssText = 'display:flex;align-items:center;gap:0.625rem;flex:1;min-width:0';

            const iconWrap = document.createElement('span');
            iconWrap.innerHTML = PROVIDER_ICONS[provider] ?? '';
            left.appendChild(iconWrap);

            const info = document.createElement('div');
            info.className = 'sn-device-info';

            const nameEl = document.createElement('p');
            nameEl.style.cssText = 'font-weight:500;font-size:0.875rem';
            if (!conn) nameEl.style.color = 'var(--text-tertiary)';
            nameEl.textContent = PROVIDER_NAMES[provider] ?? provider;
            info.appendChild(nameEl);

            if (conn) {
                const meta = document.createElement('p');
                meta.className = 'sn-device-meta';
                meta.textContent = `Connected ${new Date(conn.connectedAt).toLocaleDateString()}`;
                info.appendChild(meta);
            }

            left.appendChild(info);
            row.appendChild(left);

            // Actions
            const actions = document.createElement('div');
            actions.style.cssText = 'display:flex;align-items:center;gap:0.75rem';

            if (conn) {
                if (provider !== 'discord') {
                    const toggle = document.createElement('label');
                    toggle.className = 'flex items-center gap-1.5 text-xs cursor-pointer';
                    toggle.style.color = 'var(--text-tertiary)';
                    const chk = document.createElement('input');
                    chk.type = 'checkbox';
                    chk.className = 'checkbox checkbox-xs';
                    chk.checked = conn.require2FA;
                    chk.addEventListener('change', async () => {
                        await fetch(`/bff/settings/oauth-connections/${provider}/require-2fa`, {
                            method: 'PATCH', credentials: 'include',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ require: chk.checked })
                        });
                    });
                    const span = document.createElement('span');
                    span.textContent = '2FA';
                    toggle.appendChild(chk);
                    toggle.appendChild(span);
                    actions.appendChild(toggle);
                }

                const disconnectBtn = document.createElement('button');
                disconnectBtn.type = 'button';
                disconnectBtn.className = 'sn-device-revoke';
                disconnectBtn.textContent = (window as any).T?.settings?.connAccDisconnect ?? 'Disconnect';
                disconnectBtn.addEventListener('click', () => ensureSudo(async () => {
                    disconnectBtn.disabled = true;
                    const r = await fetch(`/bff/settings/oauth-connections/${provider}`, {
                        method: 'DELETE', credentials: 'include'
                    });
                    if (r.ok) {
                        window.location.href = '/settings/privacy';
                    } else {
                        const e = await r.json().catch(() => ({}));
                        if (e.error === 'LAST_AUTH_METHOD') {
                            window.location.href = '/settings/privacy?error=LAST_AUTH_METHOD#section-connected-accounts';
                        } else {
                            disconnectBtn.disabled = false;
                        }
                    }
                }));
                actions.appendChild(disconnectBtn);
            } else {
                const connectUrl = `/auth/oauth/${provider}/challenge?connectMode=true&returnUrl=/settings/privacy`;
                const connectBtn = document.createElement('button');
                connectBtn.type = 'button';
                connectBtn.className = 'btn btn-primary btn-sm';
                connectBtn.textContent = (window as any).T?.settings?.connAccConnect ?? 'Connect';
                connectBtn.addEventListener('click', () => ensureSudo(() => { window.location.href = connectUrl; }));
                actions.appendChild(connectBtn);
            }

            row.appendChild(actions);
            list.appendChild(row);
        }
    }

    // ─── Account – Social Links ───────────────────────────────────────────────

    function initAccountSocialLinks(): void {
        const section = document.getElementById('section-social');
        if (!section) return;

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
        interface SocialIconDef { inner: string; hex: string; viewBox: string; }
        const icons: Record<string, SocialIconDef> = (window as any).snakkSocialIcons ?? {};

        let links: SocialLink[] = [];
        let selectedPlatform: PlatformDef | null = null;

        const loadingEl    = document.getElementById('social-links-loading')!;
        const listEl       = document.getElementById('social-links-list')!;
        const containerEl  = document.getElementById('social-links-container')!;
        const addBtn       = document.getElementById('social-add-btn')!;
        const searchInput  = document.getElementById('social-search') as HTMLInputElement;
        const searchClear  = document.getElementById('social-search-clear')!;
        const resultsEl    = document.getElementById('social-results')!;
        const inputSection = document.getElementById('social-input-section')!;
        const deselectBtn  = document.getElementById('social-deselect-btn')!;
        const selectedIconEl  = document.getElementById('social-selected-icon')!;
        const selectedNameEl  = document.getElementById('social-selected-name')!;
        const valueInput   = document.getElementById('social-value-input') as HTMLInputElement;
        const inputHint    = document.getElementById('social-input-hint')!;
        const addError     = document.getElementById('social-add-error')!;
        const modalAddBtn  = document.getElementById('social-modal-add') as HTMLButtonElement;

        function makeSvg(def: SocialIconDef): SVGSVGElement {
            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('viewBox', def.viewBox);
            svg.setAttribute('aria-hidden', 'true');
            svg.innerHTML = def.inner;
            return svg;
        }

        function renderResults(query: string): void {
            resultsEl.innerHTML = '';
            const q = query.trim().toLowerCase();
            const used = new Set(links.map(l => l.platform));
            const filtered = platforms.filter(p =>
                !used.has(p.key) &&
                (!q || p.displayName.toLowerCase().includes(q) || p.key.toLowerCase().includes(q) || p.category.toLowerCase().includes(q))
            );

            if (filtered.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'social-picker-empty';
                empty.textContent = q ? 'No platforms match your search.' : 'All platforms have been added.';
                resultsEl.appendChild(empty);
                return;
            }

            if (q) {
                for (const p of filtered) renderItem(p);
            } else {
                const byCategory = new Map<string, PlatformDef[]>();
                for (const p of filtered) {
                    const group = byCategory.get(p.category) ?? [];
                    group.push(p);
                    byCategory.set(p.category, group);
                }
                for (const [cat, items] of byCategory) {
                    const divider = document.createElement('div');
                    divider.className = 'social-picker-category-divider';
                    divider.textContent = cat;
                    resultsEl.appendChild(divider);
                    for (const p of items) renderItem(p);
                }
            }
        }

        function renderItem(p: PlatformDef): void {
            const item = document.createElement('div');
            item.className = 'social-picker-item';
            item.setAttribute('role', 'option');

            const iconWrap = document.createElement('div');
            iconWrap.className = 'social-picker-item-icon';
            const iconDef = icons[p.key];
            if (iconDef) {
                iconWrap.style.color = `#${iconDef.hex}`;
                iconWrap.appendChild(makeSvg(iconDef));
            }

            const name = document.createElement('span');
            name.className = 'social-picker-item-name';
            name.textContent = p.displayName;

            item.appendChild(iconWrap);
            item.appendChild(name);
            item.addEventListener('click', () => selectPlatform(p));
            resultsEl.appendChild(item);
        }

        function selectPlatform(p: PlatformDef): void {
            selectedPlatform = p;

            selectedIconEl.innerHTML = '';
            selectedIconEl.style.color = '';
            const selectedIconDef = icons[p.key];
            if (selectedIconDef) {
                selectedIconEl.style.color = `#${selectedIconDef.hex}`;
                selectedIconEl.appendChild(makeSvg(selectedIconDef));
            }
            selectedNameEl.textContent = p.displayName;

            valueInput.value = '';
            valueInput.placeholder = p.placeholder;
            inputHint.textContent = p.hasUrl
                ? 'You can paste a full profile URL or just your username.'
                : 'Enter your username only.';
            inputHint.classList.remove('hidden');
            addError.classList.add('hidden');
            modalAddBtn.disabled = false;

            resultsEl.classList.add('hidden');
            inputSection.classList.remove('hidden');
            valueInput.focus();
        }

        function resetModal(): void {
            selectedPlatform = null;
            searchInput.value = '';
            searchClear.classList.add('hidden');
            resultsEl.classList.remove('hidden');
            inputSection.classList.add('hidden');
            addError.classList.add('hidden');
            modalAddBtn.disabled = true;
            valueInput.value = '';
            renderResults('');
        }

        async function doAdd(): Promise<void> {
            if (!selectedPlatform) return;
            const raw = valueInput.value.trim();
            if (!raw) {
                addError.textContent = (window as any).T?.settings?.socialEnterUsername ?? 'Please enter a username or profile URL.';
                addError.classList.remove('hidden');
                return;
            }
            if (links.some(l => l.platform === selectedPlatform!.key)) {
                addError.textContent = `You already have a ${selectedPlatform.displayName} link.`;
                addError.classList.remove('hidden');
                return;
            }
            if (links.length >= 10) {
                addError.textContent = (window as any).T?.settings?.socialMaxLinks ?? 'Maximum 10 social links allowed.';
                addError.classList.remove('hidden');
                return;
            }

            const addedPlatform = selectedPlatform.key;
            links.push({ platform: addedPlatform, displayName: selectedPlatform.displayName, username: raw, url: null });
            closeModal('modal-add-social');
            renderLinks();
            const ok = await saveLinks();
            if (!ok) {
                links = links.filter(l => l.platform !== addedPlatform);
                renderLinks();
            }
        }

        async function saveLinks(): Promise<boolean> {
            const payload = { links: links.map(l => ({ platform: l.platform, value: l.username })) };
            try {
                const res = await fetch('/bff/me/social', {
                    method: 'PUT',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                if (res.ok) { await loadLinks(); return true; }
                let msg = 'Could not save your social links. Please try again.';
                try { const d = await res.json(); if (d.error) msg = d.error; } catch {}
                showMessage(msg, true);
                return false;
            } catch {
                showMessage('Could not save your social links. Please try again.', true);
                return false;
            }
        }

        async function loadLinks(): Promise<void> {
            try {
                const res = await fetch('/bff/me/social', { credentials: 'include' });
                if (!res.ok) { loadingEl.classList.add('hidden'); containerEl.classList.add('hidden'); return; }
                const data = await res.json();
                links = data.links || [];
            } catch {
                loadingEl.classList.add('hidden');
                containerEl.classList.add('hidden');
                return;
            }
            renderLinks();
        }

        function renderLinks(): void {
            loadingEl.classList.add('hidden');
            listEl.innerHTML = '';
            if (links.length === 0) {
                listEl.classList.add('hidden');
                containerEl.classList.add('hidden');
            } else {
                containerEl.classList.remove('hidden');
                for (const link of links) listEl.appendChild(buildLinkRow(link));
                listEl.classList.remove('hidden');
            }
        }

        function buildLinkRow(link: SocialLink): HTMLElement {
            const item = document.createElement('div');
            item.className = 'sn-device-item';

            const left = document.createElement('div');
            left.style.cssText = 'display:flex;align-items:center;gap:0.625rem;flex:1;min-width:0';

            const iconWrap = document.createElement('div');
            iconWrap.style.cssText = 'width:1.25rem;height:1.25rem;flex-shrink:0';
            const rowIconDef = icons[link.platform];
            if (rowIconDef) {
                iconWrap.style.color = `#${rowIconDef.hex}`;
                const svg = makeSvg(rowIconDef);
                svg.style.cssText = 'width:1.25rem;height:1.25rem;fill:currentColor';
                iconWrap.appendChild(svg);
            }
            left.appendChild(iconWrap);

            const info = document.createElement('div');
            info.className = 'sn-device-info';

            const nameRow = document.createElement('div');
            nameRow.style.cssText = 'font-weight:500;font-size:0.875rem';
            nameRow.textContent = link.displayName;

            const meta = document.createElement('div');
            meta.className = 'sn-device-meta';
            if (link.url) {
                const a = document.createElement('a');
                a.href = link.url;
                a.target = '_blank';
                a.rel = 'noopener noreferrer';
                a.textContent = link.username;
                meta.appendChild(a);
            } else {
                meta.textContent = link.username;
            }

            info.appendChild(nameRow);
            info.appendChild(meta);
            left.appendChild(info);
            item.appendChild(left);

            const removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'sn-device-revoke';
            removeBtn.textContent = (window as any).T?.settings?.socialRemove ?? 'Remove';
            removeBtn.addEventListener('click', async () => {
                const prev = [...links];
                links = links.filter(l => l.platform !== link.platform);
                renderLinks();
                const ok = await saveLinks();
                if (!ok) { links = prev; renderLinks(); }
            });
            item.appendChild(removeBtn);

            return item;
        }

        // Wire up
        addBtn.addEventListener('click', () => { resetModal(); openModal('modal-add-social'); });
        deselectBtn.addEventListener('click', () => {
            selectedPlatform = null;
            resultsEl.classList.remove('hidden');
            inputSection.classList.add('hidden');
            modalAddBtn.disabled = true;
            valueInput.value = '';
            addError.classList.add('hidden');
            searchInput.focus();
        });
        searchInput.addEventListener('input', () => {
            const q = searchInput.value;
            searchClear.classList.toggle('hidden', !q);
            renderResults(q);
        });
        searchClear.addEventListener('click', () => {
            searchInput.value = '';
            searchClear.classList.add('hidden');
            renderResults('');
            searchInput.focus();
        });
        document.getElementById('social-modal-close')?.addEventListener('click', () => closeModal('modal-add-social'));
        document.getElementById('social-modal-cancel')?.addEventListener('click', () => closeModal('modal-add-social'));
        modalAddBtn.addEventListener('click', () => doAdd());
        valueInput.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); doAdd(); } });
        valueInput.addEventListener('input', () => { if (valueInput.value.trim()) addError.classList.add('hidden'); });

        loadLinks();
    }

    // ─── Boot ─────────────────────────────────────────────────────────────────

    function boot(): void {
        const userId = document.querySelector('meta[name="current-user-id"]')?.getAttribute('content') ?? '';
        if (userId) SUDO_EXPIRES_KEY = `snakk:sudo-expires:${userId}`;
        restoreSudoExpiry();
        const isAuthenticated = !!document.querySelector('meta[name="current-user-id"]');
        if (isAuthenticated) {
            initProfileSettings().then(() => { attachEventListeners(); loadDevices(); init2FA(); initScrollspy(); loadDiscordStatus(); initPasskeyStatus(); initSecurity(); initConnectedAccounts(); initAccountSocialLinks(); });
        } else {
            initDisplayPreferences();
            initSidebarStickiness();
            initSkinTonePicker();
            initContentWidth();
            initEmbedPreferences();
            initScrollspy();
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }

    document.body.addEventListener('htmx:load', function () {
        if (document.getElementById('avatar-upload-form')) {
            boot();
        }
    });
})();

// ===== Delete Account =====
(function () {
    'use strict';

    const openBtn = document.getElementById('delete-account-btn') as HTMLButtonElement | null;
    if (!openBtn) return;

    const MODAL_ID = 'modal-delete-account';

    function modal() {
        return (window as any).snakkModal as { open: (id: string) => void; close: (id: string) => void } | undefined;
    }

    function showStatus(msg: string, isError: boolean): void {
        const el = document.getElementById('delete-account-modal-status');
        if (!el) return;
        el.textContent = msg;
        el.className = 'text-sm mt-2 ' + (isError ? 'text-error' : 'text-success');
        el.classList.remove('hidden');
    }

    function clearStatus(): void {
        const el = document.getElementById('delete-account-modal-status');
        if (el) { el.textContent = ''; el.classList.add('hidden'); }
    }

    openBtn.addEventListener('click', () => {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(() => {
            const input = document.getElementById('delete-account-confirm-input') as HTMLInputElement | null;
            if (input) input.value = '';
            const submitBtn = document.getElementById('delete-account-modal-submit') as HTMLButtonElement | null;
            if (submitBtn) submitBtn.disabled = true;
            clearStatus();
            modal()?.open(MODAL_ID);
            input?.focus();
        });
    });

    // Enable submit only when something is typed
    document.getElementById('delete-account-confirm-input')?.addEventListener('input', function (this: HTMLInputElement) {
        const submitBtn = document.getElementById('delete-account-modal-submit') as HTMLButtonElement | null;
        if (submitBtn) submitBtn.disabled = this.value.trim().length === 0;
    });

    document.getElementById('delete-account-modal-submit')?.addEventListener('click', async () => {
        const submitBtn = document.getElementById('delete-account-modal-submit') as HTMLButtonElement | null;
        const input = document.getElementById('delete-account-confirm-input') as HTMLInputElement | null;
        const typedEmail = input?.value.trim() ?? '';
        if (!typedEmail) return;
        if (submitBtn) submitBtn.disabled = true;
        clearStatus();
        try {
            const res = await fetch('/bff/me/account', {
                method: 'DELETE',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ confirmation: typedEmail })
            });
            if (res.ok) { window.location.href = '/'; return; }
            if (res.status === 403) { showStatus('Session expired. Please re-authenticate.', true); return; }
            let errorMsg = 'Failed to delete account. Please try again.';
            try { const d = await res.json(); if (d?.error) errorMsg = d.error; } catch { /* ignore */ }
            showStatus(errorMsg, true);
        } catch {
            showStatus('Network error. Please try again.', true);
        } finally {
            if (submitBtn) submitBtn.disabled = false;
        }
    });

    document.getElementById('delete-account-modal-cancel')?.addEventListener('click', () => modal()?.close(MODAL_ID));
    document.getElementById('delete-account-modal-cancel-x')?.addEventListener('click', () => modal()?.close(MODAL_ID));
    document.getElementById(MODAL_ID)?.addEventListener('mousedown', (e) => {
        if (e.target === e.currentTarget) modal()?.close(MODAL_ID);
    });
    document.addEventListener('keydown', (e) => {
        if (e.key !== 'Escape') return;
        const el = document.getElementById(MODAL_ID);
        if (el && el.style.display !== 'none') modal()?.close(MODAL_ID);
    });
})();
