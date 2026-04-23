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

    // Config available via #settings-page-config JSON tag if needed
    // const configEl = document.getElementById('settings-page-config');
    // const config: SettingsPageConfig = configEl ? JSON.parse(configEl.textContent || '{}') : {};

    // Client-side authentication check and profile data loading
    async function initProfileSettings(): Promise<void> {
        try {
            const response = await fetch('/bff/me', {
                credentials: 'include'
            });

            if (!response.ok) {
                throw new Error('Failed to load profile');
            }

            userData = await response.json();
            userId = userData.publicId;

            console.log('[Profile Settings] Loaded profile data:', userData);
            populateProfileForm(userData);
        } catch (error) {
            console.error('[Profile Settings] Failed to load profile:', error);
            window.location.href = '/auth/login?returnUrl=/settings';
        }
    }

    function populateProfileForm(data: any): void {
        // Update avatar -- use custom avatar URL if set, otherwise keep generated SVG
        const avatarImg = document.getElementById('avatar-preview') as HTMLImageElement | null;
        if (avatarImg && data.avatarUrl) {
            avatarImg.src = data.avatarUrl;
        }

        // Update display name input
        const displayNameInput = document.querySelector('input[name="displayName"]') as HTMLInputElement | null;
        if (displayNameInput) {
            displayNameInput.value = data.displayName || '';
        }

        // Update preferences checkboxes
        const autoFollowCheckbox = document.querySelector('input[name="autoFollowOnReply"]') as HTMLInputElement | null;
        if (autoFollowCheckbox) {
            autoFollowCheckbox.checked = data.autoFollowOnReply !== false;
        }

        const adultContentCheckbox = document.querySelector('input[name="allowAdultContent"]') as HTMLInputElement | null;
        if (adultContentCheckbox) {
            adultContentCheckbox.checked = data.allowAdultContent === true;
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
        const displayNameForm = document.getElementById('display-name-form');

        if (data.isDisplayNameLocked) {
            if (statusEl) {
                statusEl.textContent = 'Your display name has been locked by an administrator.';
                statusEl.className = 'text-sm mb-3 text-warning';
            }
            if (displayNameForm) {
                displayNameForm.querySelectorAll('input, button').forEach((el) => {
                    (el as HTMLInputElement | HTMLButtonElement).disabled = true;
                });
            }
        } else if (data.displayNameChangedAt) {
            const changedAt = new Date(data.displayNameChangedAt);
            const daysSince = Math.floor((Date.now() - changedAt.getTime()) / (1000 * 60 * 60 * 24));
            const cooldownDays = 30;
            if (daysSince < cooldownDays) {
                const remaining = cooldownDays - daysSince;
                if (statusEl) {
                    statusEl.textContent = `You can change your display name again in ${remaining} day${remaining === 1 ? '' : 's'}.`;
                    statusEl.className = 'text-sm mb-3 text-base-content/60';
                }
                if (displayNameForm) {
                    displayNameForm.querySelectorAll('input, button').forEach((el) => {
                        (el as HTMLInputElement | HTMLButtonElement).disabled = true;
                    });
                }
            }
        }

        // Hide password field for OAuth-only users (no password set)
        if (!data.passwordHash && data.oAuthProvider && passwordField) {
            passwordField.style.display = 'none';
        }

        // Update account info sections (mobile + desktop use class selectors)
        if (data.email) {
            document.querySelectorAll('.profile-email').forEach(el => el.textContent = data.email);
            document.querySelectorAll('.profile-email-badge').forEach(el => {
                el.textContent = data.emailVerified ? 'Verified' : 'Not verified';
                el.className = data.emailVerified
                    ? 'profile-email-badge badge badge-success badge-sm shrink-0'
                    : 'profile-email-badge badge badge-warning badge-sm shrink-0';
            });
        }

        if (data.oAuthProvider) {
            document.querySelectorAll('.profile-oauth').forEach(el => el.textContent = data.oAuthProvider);
            document.querySelectorAll('.profile-oauth-row').forEach(el => (el as HTMLElement).style.display = '');
        }

        document.querySelectorAll('.profile-userid').forEach(el => el.textContent = data.publicId);
    }

    function showStatus(message: string, isError: boolean = false): void {
        const statusDiv = document.getElementById('upload-status');
        if (!statusDiv) return;
        statusDiv.textContent = message;
        statusDiv.className = 'mt-3 text-sm ' + (isError ? 'text-error' : 'text-success');
        statusDiv.classList.remove('hidden');
    }

    function showProgress(percent: number): void {
        const statusDiv = document.getElementById('upload-status');
        if (!statusDiv) return;
        statusDiv.className = 'mt-3 text-sm text-base-content/70';
        statusDiv.classList.remove('hidden');
        // Build once, then just update the value/label on subsequent ticks
        let progress = statusDiv.querySelector<HTMLProgressElement>('progress.avatar-upload-progress');
        let label = statusDiv.querySelector<HTMLSpanElement>('span.avatar-upload-progress-label');
        if (!progress || !label) {
            statusDiv.textContent = '';
            progress = document.createElement('progress');
            progress.className = 'progress progress-primary avatar-upload-progress w-full mt-1';
            progress.max = 100;
            label = document.createElement('span');
            label.className = 'avatar-upload-progress-label';
            statusDiv.appendChild(label);
            statusDiv.appendChild(progress);
        }
        progress.value = percent;
        label.textContent = `Uploading… ${percent}%`;
    }

    function showProcessing(): void {
        const statusDiv = document.getElementById('upload-status');
        if (!statusDiv) return;
        statusDiv.textContent = 'Processing…';
        statusDiv.className = 'mt-3 text-sm text-base-content/70';
        statusDiv.classList.remove('hidden');
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

    async function refreshAvatar(): Promise<void> {
        const avatarPreview = document.getElementById('avatar-preview') as HTMLImageElement | null;
        if (!avatarPreview || !userId) return;
        // Re-fetch profile to get the current avatar URL
        try {
            const response = await fetch('/bff/me', { credentials: 'include' });
            if (response.ok) {
                const data = await response.json();
                const timestamp = new Date().getTime();
                if (data.avatarUrl) {
                    avatarPreview.src = data.avatarUrl + '?t=' + timestamp;
                } else {
                    // Fall back to generated SVG
                    avatarPreview.src = avatarPreview.dataset.generatedUrl || '';
                }
            }
        } catch { /* ignore */ }
    }

    function setAvatarFile(file: File): void {
        const avatarPreview = document.getElementById('avatar-preview') as HTMLImageElement | null;
        const fileInput = document.getElementById('avatar-file') as HTMLInputElement | null;
        const uploadBtn = document.getElementById('upload-btn') as HTMLButtonElement | null;
        const dropLabel = document.getElementById('avatar-drop-label');
        if (!avatarPreview || !fileInput) return;

        const allowed = ['image/jpeg', 'image/png', 'image/webp'];
        if (!allowed.includes(file.type)) {
            showStatus('Invalid file type. Allowed: JPEG, PNG, WebP.', true);
            return;
        }

        if (file.size > 2 * 1024 * 1024) {
            showStatus('File is too large. Maximum size is 2MB.', true);
            return;
        }

        // Transfer file to the hidden input via DataTransfer
        const dt = new DataTransfer();
        dt.items.add(file);
        fileInput.files = dt.files;

        if (uploadBtn) uploadBtn.disabled = false;
        if (dropLabel) dropLabel.textContent = file.name;

        const reader = new FileReader();
        reader.onload = function (e) { avatarPreview.src = (e.target as FileReader).result as string; };
        reader.readAsDataURL(file);
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

        const fileInput = document.getElementById('avatar-file') as HTMLInputElement | null;
        const uploadBtn = document.getElementById('upload-btn') as HTMLButtonElement | null;
        if (!fileInput || !uploadBtn) return;

        const file = fileInput.files?.[0];
        if (!file) {
            showStatus('Please select a file first.', true);
            return;
        }

        uploadBtn.disabled = true;
        uploadBtn.textContent = 'Uploading...';
        showProgress(0);

        try {
            const formData = new FormData();
            formData.append('avatar', file);

            const result = await window.SnakkUpload.uploadWithProgress<{ error?: string }>({
                url: '/bff/avatars/upload',
                formData,
                onProgress: (percent: number) => showProgress(percent),
                onProcessing: () => showProcessing(),
            });

            if (result.ok) {
                showStatus('Avatar uploaded successfully!');
                refreshAvatar();
                fileInput.value = '';
                resetDropZone();
                uploadBtn.disabled = true;
                const delBtn = document.getElementById('delete-avatar-btn') as HTMLButtonElement | null;
                if (delBtn) delBtn.disabled = false;
                htmx.ajax('GET', location.pathname, { target: '#auth-nav', select: '#auth-nav > *', swap: 'innerHTML' } as any);
            } else {
                showStatus(result.data?.error || result.error || 'Failed to upload avatar.', true);
                refreshAvatar();
            }
        } catch (error) {
            showStatus('Network error. Please try again.', true);
            refreshAvatar();
        } finally {
            uploadBtn.disabled = false;
            uploadBtn.textContent = 'Upload Avatar';
        }
    }

    async function handleDeleteClick(): Promise<void> {
        if (!confirm('Revert to the auto-generated avatar?')) return;

        const deleteBtn = document.getElementById('delete-avatar-btn') as HTMLButtonElement | null;
        if (!deleteBtn) return;

        deleteBtn.disabled = true;
        deleteBtn.textContent = 'Reverting...';

        try {
            const response = await fetch('/bff/avatars', {
                method: 'DELETE',
                credentials: 'include'
            });

            if (response.ok) {
                showStatus('Now using generated avatar.');
                refreshAvatar();
                deleteBtn.disabled = true;
                deleteBtn.textContent = 'Use Generated';
                htmx.ajax('GET', location.pathname, { target: '#auth-nav', select: '#auth-nav > *', swap: 'innerHTML' } as any);
            } else {
                deleteBtn.disabled = false;
                deleteBtn.textContent = 'Use Generated';
                const result = await response.json();
                showStatus(result.error || 'Failed to delete avatar.', true);
            }
        } catch (error) {
            deleteBtn.disabled = false;
            deleteBtn.textContent = 'Use Generated';
            showStatus('Network error. Please try again.', true);
        }
    }

    async function handleDisplayNameUpdate(e: Event): Promise<void> {
        e.preventDefault();

        const form = e.target as HTMLFormElement;
        const displayNameInput = form.querySelector('input[name="displayName"]') as HTMLInputElement | null;
        const passwordInput = form.querySelector('input[name="password"]') as HTMLInputElement | null;
        const displayName = displayNameInput?.value?.trim();
        const password = passwordInput?.value || null;

        if (!displayName) {
            showMessage('Display name cannot be empty', true);
            return;
        }

        // Get Turnstile token
        const turnstileToken = typeof (window as any).turnstile !== 'undefined'
            ? (window as any).turnstile.getResponse()
            : null;

        const submitBtn = form.querySelector('button[type="submit"]') as HTMLButtonElement | null;
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.textContent = 'Updating...';
        }

        try {
            const response = await fetch('/bff/me/profile', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ displayName, password, turnstileToken })
            });

            const result = await response.json().catch(() => null);

            if (response.ok) {
                showMessage('Display name updated successfully');
                if (passwordInput) passwordInput.value = '';
                // Reset Turnstile for next use
                if (typeof (window as any).turnstile !== 'undefined') (window as any).turnstile.reset();
            } else {
                showMessage(result?.error || 'Failed to update display name', true);
                if (typeof (window as any).turnstile !== 'undefined') (window as any).turnstile.reset();
            }
        } catch (error) {
            console.error('[Profile Settings] Update display name error:', error);
            showMessage('Network error. Please try again.', true);
        } finally {
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.textContent = 'Update Display Name';
            }
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

    async function handleGenerateFeedToken(): Promise<void> {
        try {
            const response = await fetch('/bff/me/feed-token', {
                method: 'POST',
                credentials: 'include'
            });
            if (response.ok) {
                const data = await response.json();
                updateFeedTokenUI(data.token);
            } else {
                showMessage('Failed to generate feed token', true);
            }
        } catch {
            showMessage('Network error. Please try again.', true);
        }
    }

    async function handleRevokeFeedToken(): Promise<void> {
        try {
            const response = await fetch('/bff/me/feed-token', {
                method: 'DELETE',
                credentials: 'include'
            });
            if (response.ok) {
                updateFeedTokenUI(null);
            } else {
                showMessage('Failed to revoke feed token', true);
            }
        } catch {
            showMessage('Network error. Please try again.', true);
        }
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

    async function handleAdultContentPreferenceChange(checkbox: HTMLInputElement): Promise<void> {
        const allowAdultContent = checkbox.checked;

        try {
            const response = await fetch('/bff/me/preferences', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ allowAdultContent })
            });

            if (!response.ok) {
                showMessage('Failed to update preference', true);
                checkbox.checked = !allowAdultContent;
            }
        } catch (error) {
            showMessage('Network error. Please try again.', true);
            checkbox.checked = !allowAdultContent;
        }
    }

    // --- Device Management ---
    async function loadDevices(): Promise<void> {
        const container = document.getElementById('devices-list');
        if (!container) return;

        try {
            const response = await fetch('/bff/me/devices', { credentials: 'include' });
            if (!response.ok) {
                container.innerHTML = '<p class="text-sm text-muted">Unable to load devices.</p>';
                return;
            }

            const devices: any[] = await response.json();
            if (devices.length === 0) {
                container.innerHTML = '<p class="text-sm text-muted">No trusted devices.</p>';
                return;
            }

            container.innerHTML = devices.map((d: any) => `
                <div class="flex items-center justify-between py-3 border-b border-subtle last:border-0" data-device-id="${escapeHtml(d.publicId)}">
                    <div class="flex-1 min-w-0">
                        <div class="flex items-center gap-2">
                            <span class="font-medium text-sm">${escapeHtml(d.deviceName || parseUserAgent(d.deviceName))}</span>
                            ${d.isActive ? '<span class="badge badge-success badge-xs">Active</span>' : '<span class="badge badge-ghost badge-xs">Expired</span>'}
                        </div>
                        <div class="text-xs text-muted mt-1">
                            ${d.lastUsedIp ? `IP: ${escapeHtml(d.lastUsedIp)} · ` : ''}
                            Added ${formatRelativeDate(d.trustedAt)}
                            ${d.lastUsedAt ? ` · Last used ${formatRelativeDate(d.lastUsedAt)}` : ''}
                        </div>
                    </div>
                    <button data-action="revoke-device" data-device-id="${escapeHtml(d.publicId)}" class="btn btn-ghost btn-xs text-error">
                        Revoke
                    </button>
                </div>
            `).join('');
        } catch {
            container.innerHTML = '<p class="text-sm text-muted">Unable to load devices.</p>';
        }
    }

    function parseUserAgent(ua: string): string {
        if (!ua) return 'Unknown Device';
        if (ua.includes('Firefox')) return 'Firefox';
        if (ua.includes('Edg/')) return 'Microsoft Edge';
        if (ua.includes('Chrome')) return 'Chrome';
        if (ua.includes('Safari')) return 'Safari';
        return ua.substring(0, 50);
    }

    function formatRelativeDate(dateStr: string): string {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
        if (diffDays === 0) return 'today';
        if (diffDays === 1) return 'yesterday';
        if (diffDays < 30) return `${diffDays} days ago`;
        return date.toLocaleDateString();
    }

    function escapeHtml(text: string): string {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function initEmbedPreferences(): void {
        const container = document.getElementById('embed-providers');
        if (!container) return;

        const storageKey = 'snakk:embed-providers';
        let prefs: Record<string, boolean> = {};
        try {
            prefs = JSON.parse(localStorage.getItem(storageKey) || '{}');
        } catch { /* ignore */ }

        const toggles = container.querySelectorAll<HTMLInputElement>('input[data-embed-provider]');
        toggles.forEach(toggle => {
            const provider = toggle.dataset.embedProvider || '';
            toggle.checked = !!prefs[provider];
        });

        container.addEventListener('change', (e) => {
            const toggle = (e.target as HTMLElement).closest('input[data-embed-provider]') as HTMLInputElement | null;
            if (!toggle) return;

            const provider = toggle.dataset.embedProvider || '';
            prefs[provider] = toggle.checked;

            // Clean up false entries
            if (!prefs[provider]) delete prefs[provider];

            localStorage.setItem(storageKey, JSON.stringify(prefs));
        });
    }

    function initDisplayPreferences(): void {
        const previewsToggle = document.getElementById('pref-discussion-previews') as HTMLInputElement | null;
        const animationsToggle = document.getElementById('pref-animations') as HTMLInputElement | null;
        if (!previewsToggle && !animationsToggle) return;

        try {
            if (previewsToggle) {
                previewsToggle.checked = localStorage.getItem('snakk:disable-previews') !== 'true';
                previewsToggle.addEventListener('change', () => {
                    const disabled = !previewsToggle.checked;
                    if (disabled) {
                        localStorage.setItem('snakk:disable-previews', 'true');
                        document.documentElement.classList.add('no-discussion-previews');
                    } else {
                        localStorage.removeItem('snakk:disable-previews');
                        document.documentElement.classList.remove('no-discussion-previews');
                    }
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

    function attachEventListeners(): void {
        initDisplayPreferences();
        initSidebarStickiness();
        initSkinTonePicker();
        initEmbedPreferences();

        const avatarForm = document.getElementById('avatar-upload-form') as HTMLFormElement | null;
        const deleteBtn = document.getElementById('delete-avatar-btn') as HTMLButtonElement | null;

        if (avatarForm && !avatarForm.dataset.initialized) {
            avatarForm.dataset.initialized = 'true';
            initAvatarDropZone();
            avatarForm.addEventListener('submit', handleAvatarUploadSubmit);
            if (deleteBtn) deleteBtn.addEventListener('click', handleDeleteClick);
        }

        const displayNameForm = document.getElementById('display-name-form') as HTMLFormElement | null;
        if (displayNameForm && !displayNameForm.dataset.initialized) {
            displayNameForm.dataset.initialized = 'true';
            displayNameForm.addEventListener('submit', handleDisplayNameUpdate);
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

        const adultContentCheckbox = document.querySelector('input[name="allowAdultContent"][type="checkbox"]') as HTMLInputElement | null;
        if (adultContentCheckbox && !adultContentCheckbox.dataset.initialized) {
            adultContentCheckbox.dataset.initialized = 'true';
            adultContentCheckbox.addEventListener('change', (e) => handleAdultContentPreferenceChange(e.target as HTMLInputElement));
        }

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
    }

    // Register revoke-device action with global delegation system
    if (window.SnakkActions) {
        window.SnakkActions.on('revoke-device', async (el) => {
            const deviceId = el.dataset.deviceId;
            if (!deviceId) return;

            try {
                const response = await fetch(`/bff/me/devices/${deviceId}`, {
                    method: 'DELETE',
                    credentials: 'include'
                });

                if (response.ok) {
                    const row = el.closest('[data-device-id]');
                    if (row) row.remove();

                    const container = document.getElementById('devices-list');
                    if (container && container.children.length === 0) {
                        container.innerHTML = '<p class="text-sm text-muted">No trusted devices.</p>';
                    }
                }
            } catch {
                // Silently fail
            }
        });
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

    // ===== Two-Factor Authentication =====

    function show2FAMessage(text: string, isError = false): void {
        const el = document.getElementById('2fa-status-message');
        if (!el) return;
        el.textContent = text;
        el.className = `mt-3 text-sm ${isError ? 'text-error' : 'text-success'}`;
        el.classList.remove('hidden');
        setTimeout(() => el.classList.add('hidden'), 5000);
    }

    function show2FAPanel(panelId: string): void {
        ['2fa-loading', '2fa-disabled', '2fa-setup', '2fa-backup-codes', '2fa-enabled']
            .forEach(id => document.getElementById(id)?.classList.add('hidden'));
        document.getElementById(panelId)?.classList.remove('hidden');
    }

    async function load2FAStatus(): Promise<void> {
        try {
            const response = await fetch('/bff/auth/2fa/status', { credentials: 'include' });
            if (!response.ok) { show2FAPanel('2fa-disabled'); return; }
            const data = await response.json();
            if (data.isEnabled) {
                show2FAPanel('2fa-enabled');
                const backupStatus = document.getElementById('2fa-backup-status');
                if (backupStatus && data.unusedBackupCodes !== undefined) {
                    backupStatus.textContent = `${data.unusedBackupCodes} backup codes remaining`;
                }
            } else {
                show2FAPanel('2fa-disabled');
            }
        } catch {
            show2FAPanel('2fa-disabled');
        }
    }

    async function handle2FASetup(): Promise<void> {
        const btn = document.getElementById('2fa-setup-btn') as HTMLButtonElement | null;
        if (btn) { btn.disabled = true; btn.textContent = 'Setting up...'; }
        try {
            const response = await fetch('/bff/auth/2fa/setup', { method: 'POST', credentials: 'include' });
            const data = await response.json();
            if (!response.ok) { show2FAMessage(data.error || 'Setup failed', true); return; }

            const qrImg = document.getElementById('2fa-qr-img') as HTMLImageElement | null;
            const manualKey = document.getElementById('2fa-manual-key');
            if (qrImg) qrImg.src = `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(data.qrCodeUri)}`;
            if (manualKey) manualKey.textContent = data.secret;

            show2FAPanel('2fa-setup');
        } catch {
            show2FAMessage('Network error', true);
        } finally {
            if (btn) { btn.disabled = false; btn.textContent = 'Enable 2FA'; }
        }
    }

    async function handle2FAVerify(): Promise<void> {
        const codeInput = document.getElementById('2fa-verify-code') as HTMLInputElement | null;
        const btn = document.getElementById('2fa-verify-btn') as HTMLButtonElement | null;
        if (!codeInput?.value) { show2FAMessage('Enter a code', true); return; }
        if (btn) { btn.disabled = true; btn.textContent = 'Verifying...'; }

        try {
            const response = await fetch('/bff/auth/2fa/enable', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ code: codeInput.value.trim() })
            });
            const data = await response.json();
            if (!response.ok) { show2FAMessage(data.error || 'Invalid code', true); return; }

            // Show backup codes
            const grid = document.getElementById('2fa-codes-grid');
            if (grid && data.backupCodes) {
                grid.innerHTML = '';
                (data.backupCodes as string[]).forEach(code => {
                    const el = document.createElement('div');
                    el.className = 'p-1 text-center';
                    el.textContent = code;
                    grid.appendChild(el);
                });
            }
            show2FAPanel('2fa-backup-codes');
        } catch {
            show2FAMessage('Network error', true);
        } finally {
            if (btn) { btn.disabled = false; btn.textContent = 'Verify & Enable'; }
        }
    }

    async function handle2FADisable(): Promise<void> {
        const code = prompt('Enter your current 2FA code to disable:');
        if (!code) return;
        const password = prompt('Enter your password to confirm:');
        if (!password) return;

        try {
            const response = await fetch('/bff/auth/2fa/disable', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ password, totpCode: code })
            });
            if (response.ok) {
                show2FAMessage('2FA disabled');
                show2FAPanel('2fa-disabled');
            } else {
                const data = await response.json();
                show2FAMessage(data.error || 'Failed to disable', true);
            }
        } catch {
            show2FAMessage('Network error', true);
        }
    }

    async function handle2FARegenerate(): Promise<void> {
        const password = prompt('Enter your password to regenerate backup codes:');
        if (!password) return;

        try {
            const response = await fetch('/bff/auth/2fa/backup-codes/regenerate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ password })
            });
            const data = await response.json();
            if (!response.ok) { show2FAMessage(data.error || 'Failed', true); return; }

            const grid = document.getElementById('2fa-codes-grid');
            if (grid && data.backupCodes) {
                grid.innerHTML = '';
                (data.backupCodes as string[]).forEach(code => {
                    const el = document.createElement('div');
                    el.className = 'p-1 text-center';
                    el.textContent = code;
                    grid.appendChild(el);
                });
            }
            show2FAPanel('2fa-backup-codes');
        } catch {
            show2FAMessage('Network error', true);
        }
    }

    function init2FA(): void {
        const section = document.getElementById('two-factor-section');
        if (!section || section.dataset.initialized) return;
        section.dataset.initialized = 'true';

        document.getElementById('2fa-setup-btn')?.addEventListener('click', handle2FASetup);
        document.getElementById('2fa-verify-btn')?.addEventListener('click', handle2FAVerify);
        document.getElementById('2fa-cancel-btn')?.addEventListener('click', () => show2FAPanel('2fa-disabled'));
        document.getElementById('2fa-codes-done-btn')?.addEventListener('click', () => { show2FAPanel('2fa-enabled'); load2FAStatus(); });
        document.getElementById('2fa-disable-btn')?.addEventListener('click', handle2FADisable);
        document.getElementById('2fa-regen-btn')?.addEventListener('click', handle2FARegenerate);
        document.getElementById('2fa-copy-codes-btn')?.addEventListener('click', () => {
            const grid = document.getElementById('2fa-codes-grid');
            if (grid) navigator.clipboard.writeText(grid.textContent?.replace(/\s+/g, '\n') || '');
        });

        // Also allow Enter key in verify code input
        document.getElementById('2fa-verify-code')?.addEventListener('keydown', (e) => {
            if ((e as KeyboardEvent).key === 'Enter') handle2FAVerify();
        });

        load2FAStatus();
    }

    function boot(): void {
        initProfileSettings().then(() => { attachEventListeners(); loadDevices(); init2FA(); initScrollspy(); });
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
