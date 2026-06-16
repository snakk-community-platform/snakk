// ===== Presence toggle =====
(function () {
    'use strict';
    const toggle = document.getElementById('hide-presence-toggle') as HTMLInputElement | null;
    if (!toggle) return;
    toggle.addEventListener('change', async function () {
        const newValue = toggle.checked;
        try {
            const res = await fetch('/bff/me/preferences', {
                method: 'PUT',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ hidePresence: newValue })
            });
            if (res.ok) {
                (window as any).SnakkRealtime?.reconnect();
            } else {
                toggle.checked = !newValue;
                console.error('[Settings] Failed to update presence preference:', res.status);
            }
        } catch (err) {
            toggle.checked = !newValue;
            console.error('[Settings] Network error updating presence preference:', err);
        }
    });
})();

// ===== Disable History toggle =====
(function () {
    'use strict';
    const toggle = document.getElementById('disable-history-toggle') as HTMLInputElement | null;
    if (!toggle) return;
    const COOKIE = '.Snakk.Pref.DisableHistory';
    toggle.addEventListener('change', function () {
        if (toggle.checked) {
            document.cookie = `${COOKIE}=1; path=/; max-age=31536000; SameSite=Lax`;
        } else {
            document.cookie = `${COOKIE}=; path=/; max-age=0; SameSite=Lax`;
        }
    });
})();

// ===== Change Password Modal =====
(function () {
    'use strict';

    const openBtn = document.getElementById('open-change-password') as HTMLButtonElement | null;
    if (!openBtn) return;

    const snakkModal = (window as any).snakkModal as { open: (id: string) => void; close: (id: string) => void } | undefined;
    const MODAL_ID = 'modal-change-password';

    function showStatus(msg: string, isError: boolean): void {
        const el = document.getElementById('change-password-status');
        if (!el) return;
        el.textContent = msg;
        el.className = 'text-sm mt-2 ' + (isError ? 'text-error' : 'text-success');
        el.classList.remove('hidden');
    }

    function clearStatus(): void {
        const el = document.getElementById('change-password-status');
        if (el) { el.textContent = ''; el.classList.add('hidden'); }
    }

    openBtn.addEventListener('click', () => {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(() => {
            const form = document.getElementById('change-password-form') as HTMLFormElement | null;
            form?.reset();
            clearStatus();
            snakkModal?.open(MODAL_ID);
        });
    });

    document.getElementById(MODAL_ID)?.addEventListener('mousedown', (e) => {
        if (e.target === e.currentTarget) snakkModal?.close(MODAL_ID);
    });

    document.addEventListener('keydown', (e) => {
        if (e.key !== 'Escape') return;
        const el = document.getElementById(MODAL_ID);
        if (el && el.style.display !== 'none') snakkModal?.close(MODAL_ID);
    });

    const form = document.getElementById('change-password-form') as HTMLFormElement | null;
    if (!form) return;

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const newPassword = (form.elements.namedItem('newPassword') as HTMLInputElement)?.value || '';
        const confirmPassword = (form.elements.namedItem('confirmPassword') as HTMLInputElement)?.value || '';
        const submitBtn = document.getElementById('change-password-btn') as HTMLButtonElement | null;

        clearStatus();

        if (!newPassword) { showStatus('Enter a new password', true); return; }
        if (newPassword !== confirmPassword) { showStatus('Passwords do not match', true); return; }

        const snakkSudo = (window as any).snakkSudo as { isValid: () => boolean } | undefined;
        if (!snakkSudo?.isValid()) { showStatus('Authentication required', true); return; }

        if (submitBtn) submitBtn.disabled = true;

        try {
            const response = await fetch('/bff/me/password', {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ newPassword, confirmPassword })
            });
            if (response.status === 403) { showStatus('Session expired. Please re-authenticate.', true); return; }
            if (!response.ok) {
                let errorMsg = 'Failed to change password';
                try {
                    const data = await response.json();
                    if (data?.error) errorMsg = data.error;
                } catch { /* ignore parse errors */ }
                showStatus(errorMsg, true);
                return;
            }
            snakkModal?.close(MODAL_ID);
        } catch {
            showStatus('Network error', true);
        } finally {
            if (submitBtn) submitBtn.disabled = false;
        }
    });
})();

// ===== Passkeys =====
(function () {
    'use strict';

    const listEl = document.getElementById('passkey-list');
    if (!listEl) return;
    const list: HTMLElement = listEl;

    const addArea = document.getElementById('passkey-add-area') as HTMLElement | null;
    const snakkModal = (window as any).snakkModal as { open: (id: string) => void; close: (id: string) => void } | undefined;
    const ADD_MODAL_ID = 'modal-passkey';
    const DEL_MODAL_ID = 'modal-passkey-delete';

    let currentDeleteId: number | null = null;

    function showAddModalStatus(msg: string, isError: boolean): void {
        const el = document.getElementById('passkey-modal-status');
        if (!el) return;
        el.textContent = msg;
        el.className = 'text-sm mt-2 ' + (isError ? 'text-error' : 'text-success');
        el.classList.remove('hidden');
    }

    function clearAddModalStatus(): void {
        const el = document.getElementById('passkey-modal-status');
        if (el) { el.textContent = ''; el.classList.add('hidden'); }
    }

    function showDelModalStatus(msg: string, isError: boolean): void {
        const el = document.getElementById('passkey-delete-status');
        if (!el) return;
        el.textContent = msg;
        el.className = 'text-sm mt-2 ' + (isError ? 'text-error' : 'text-success');
        el.classList.remove('hidden');
    }

    function clearDelModalStatus(): void {
        const el = document.getElementById('passkey-delete-status');
        if (el) { el.textContent = ''; el.classList.add('hidden'); }
    }

    const base64urlToBuffer = (base64url: string): ArrayBuffer => (window as any).SnakkWebAuthn.base64urlToBuffer(base64url) as ArrayBuffer;
    const bufferToBase64url = (buffer: ArrayBuffer): string => (window as any).SnakkWebAuthn.bufferToBase64url(buffer) as string;

    interface AttestationJSON {
        id: string;
        rawId: string;
        type: string;
        response: {
            attestationObject: string;
            clientDataJSON: string;
            transports: string[];
        };
    }

    function serializeAttestation(cred: PublicKeyCredential): string {
        const resp = cred.response as AuthenticatorAttestationResponse;
        const att: AttestationJSON = {
            id: cred.id,
            rawId: bufferToBase64url(cred.rawId),
            type: cred.type,
            response: {
                attestationObject: bufferToBase64url(resp.attestationObject),
                clientDataJSON: bufferToBase64url(resp.clientDataJSON),
                transports: resp.getTransports ? resp.getTransports() : []
            }
        };
        return JSON.stringify(att);
    }

    interface PasskeyItem {
        id: number;
        friendlyName: string | null;
        createdAt: string | null;
        lastUsedAt: string | null;
        authenticatorName: string | null;
        authenticatorIcon: string | null;
    }

    function renderPasskeys(passkeys: PasskeyItem[]): void {
        if (passkeys.length === 0) {
            list.style.display = 'none';
            list.innerHTML = '';
            return;
        }

        list.style.display = '';
        list.innerHTML = passkeys.map(p => {
            const name = p.friendlyName ?? p.authenticatorName ?? 'Passkey';
            const escapedName = name.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
            const iconHtml = p.authenticatorIcon && p.authenticatorIcon.startsWith('data:image/')
                ? `<img src="${p.authenticatorIcon}" class="sn-device-icon" aria-hidden="true" width="24" height="24" alt="">`
                : `<span class="icon icon-lock-closed sn-device-icon" aria-hidden="true"></span>`;
            const metaParts: string[] = [];
            if (p.authenticatorName) metaParts.push(p.authenticatorName.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'));
            if (p.createdAt) metaParts.push('Added ' + new Date(p.createdAt).toLocaleDateString());
            metaParts.push('Last used ' + (p.lastUsedAt ? new Date(p.lastUsedAt).toLocaleDateString() : '—'));
            return `
                <div class="sn-device-item">
                    ${iconHtml}
                    <div class="sn-device-info">
                        <div class="sn-device-name">${escapedName}</div>
                        <div class="sn-device-meta">${metaParts.join(' · ')}</div>
                    </div>
                    <button type="button" class="sn-device-revoke passkey-delete-btn" data-id="${p.id}">Remove</button>
                </div>`;
        }).join('');

        list.querySelectorAll<HTMLButtonElement>('.passkey-delete-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const id = parseInt(btn.dataset['id'] || '0', 10);
                openDeleteModal(id);
            });
        });
    }

    function openDeleteModal(id: number): void {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(() => {
            currentDeleteId = id;
            clearDelModalStatus();
            snakkModal?.open(DEL_MODAL_ID);
        });
    }

    function loadPasskeys(): void {
        list.style.display = '';
        list.innerHTML = '<div class="flex justify-center py-4"><span class="loading loading-spinner loading-md"></span></div>';
        fetch('/bff/auth/passkeys/', { credentials: 'include' })
            .then(res => res.ok ? res.json() as Promise<PasskeyItem[]> : Promise.reject(new Error('load failed')))
            .then(data => {
                renderPasskeys(data);
                const s = (window as any).snakkSettings;
                if (s) s.hasPasskeys = data.length > 0;
            })
            .catch(() => { list.innerHTML = '<p class="sn-devices-empty">Could not load passkeys.</p>'; });
    }

    if (!window.PublicKeyCredential) {
        list.innerHTML = '<p class="sn-devices-empty">Passkeys are not supported by this browser.</p>';
    } else {
        if (addArea) addArea.style.display = '';
        loadPasskeys();
    }

    // Add passkey modal open
    document.getElementById('open-passkey-modal')?.addEventListener('click', () => {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(() => {
            const nameInput = document.getElementById('passkey-name-input') as HTMLInputElement | null;
            if (nameInput) nameInput.value = '';
            clearAddModalStatus();
            snakkModal?.open(ADD_MODAL_ID);
        });
    });

    document.getElementById(ADD_MODAL_ID)?.addEventListener('mousedown', (e) => {
        if (e.target === e.currentTarget) snakkModal?.close(ADD_MODAL_ID);
    });

    document.getElementById(DEL_MODAL_ID)?.addEventListener('mousedown', (e) => {
        if (e.target === e.currentTarget) snakkModal?.close(DEL_MODAL_ID);
    });

    document.addEventListener('keydown', (e) => {
        if (e.key !== 'Escape') return;
        [ADD_MODAL_ID, DEL_MODAL_ID].forEach(id => {
            const el = document.getElementById(id);
            if (el && el.style.display !== 'none') snakkModal?.close(id);
        });
    });

    document.querySelectorAll<HTMLElement>('.sn-modal-close[data-modal="' + ADD_MODAL_ID + '"], .sn-modal-close[data-modal="' + DEL_MODAL_ID + '"]').forEach(btn => {
        btn.addEventListener('click', () => snakkModal?.close(btn.dataset.modal!));
    });

    // Register passkey
    const registerBtn = document.getElementById('passkey-register-btn') as HTMLButtonElement | null;
    if (!registerBtn) return;

    // Enter in name field submits
    document.getElementById('passkey-name-input')?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); registerBtn.click(); }
    });

    interface BeginRegResponse {
        optionsJson: string;
        challengeId: string;
    }

    interface RawCreationOptions {
        challenge: string;
        user: { id: string; name: string; displayName: string };
        rp: { id: string; name: string };
        pubKeyCredParams: PublicKeyCredentialParameters[];
        timeout?: number;
        excludeCredentials?: Array<{ id: string; type: string; transports?: AuthenticatorTransport[] }>;
        authenticatorSelection?: AuthenticatorSelectionCriteria;
        attestation?: AttestationConveyancePreference;
    }

    registerBtn.addEventListener('click', () => {
        const snakkSudo = (window as any).snakkSudo as { isValid: () => boolean } | undefined;
        if (!snakkSudo?.isValid()) { showAddModalStatus('Authentication required', true); return; }

        const nameInput = document.getElementById('passkey-name-input') as HTMLInputElement | null;
        const friendlyName = nameInput?.value.trim() ?? '';
        if (!friendlyName) { showAddModalStatus('Please enter a name for this passkey.', true); nameInput?.focus(); return; }

        registerBtn.disabled = true;
        clearAddModalStatus();

        fetch('/bff/auth/passkeys/begin-registration', {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: '{}'
        })
            .then(res => {
                if (!res.ok) return res.json().then(d => Promise.reject(new Error(d.error || 'Could not start passkey registration')));
                return res.json() as Promise<BeginRegResponse>;
            })
            .then(data => {
                const raw = JSON.parse(data.optionsJson) as RawCreationOptions;
                const opts: PublicKeyCredentialCreationOptions = {
                    ...raw,
                    challenge: base64urlToBuffer(raw.challenge),
                    user: { ...raw.user, id: base64urlToBuffer(raw.user.id) },
                    excludeCredentials: raw.excludeCredentials?.map(c => ({
                        id: base64urlToBuffer(c.id),
                        type: c.type as PublicKeyCredentialType,
                        transports: c.transports
                    }))
                };
                return navigator.credentials.create({ publicKey: opts })
                    .then(cred => {
                        if (!cred) throw new Error('No credential returned');
                        return fetch('/bff/auth/passkeys/complete-registration', {
                            method: 'POST',
                            credentials: 'include',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({
                                challengeId: data.challengeId,
                                attestationResponseJson: serializeAttestation(cred as PublicKeyCredential),
                                friendlyName
                            })
                        });
                    });
            })
            .then(res => {
                if (!res.ok) throw new Error('Could not register passkey. Please try again.');
                snakkModal?.close(ADD_MODAL_ID);
                loadPasskeys();
            })
            .catch((err: unknown) => {
                if (err instanceof Error && err.name === 'NotAllowedError') return;
                const msg = err instanceof Error ? err.message : 'Could not register passkey. Please try again.';
                showAddModalStatus(msg, true);
            })
            .finally(() => { registerBtn.disabled = false; });
    });

    // Delete passkey modal — confirm button
    const confirmDeleteBtn = document.getElementById('passkey-confirm-delete-btn') as HTMLButtonElement | null;
    confirmDeleteBtn?.addEventListener('click', async () => {
        if (currentDeleteId === null) return;

        const snakkSudo = (window as any).snakkSudo as { isValid: () => boolean } | undefined;
        if (!snakkSudo?.isValid()) { showDelModalStatus('Authentication required', true); return; }

        confirmDeleteBtn.disabled = true;
        clearDelModalStatus();

        try {
            const res = await fetch('/bff/auth/passkeys/' + currentDeleteId, {
                method: 'DELETE',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: '{}'
            });

            if (res.ok) {
                snakkModal?.close(DEL_MODAL_ID);
                loadPasskeys();
            } else if (res.status === 403) {
                showDelModalStatus('Session expired. Please re-authenticate.', true);
            } else {
                const data = await res.json().catch(() => ({}));
                showDelModalStatus((data as any).error || 'Could not remove passkey', true);
            }
        } catch {
            showDelModalStatus('Network error', true);
        } finally {
            confirmDeleteBtn.disabled = false;
        }
    });
})();

// ===== Download My Data =====
(function () {
    'use strict';

    const downloadBtn = document.getElementById('download-data-btn') as HTMLButtonElement | null;
    if (!downloadBtn) return;

    const statusEl = document.getElementById('download-data-status') as HTMLElement | null;

    function setStatus(msg: string, isError: boolean): void {
        if (!statusEl) return;
        statusEl.textContent = msg;
        statusEl.className = 'text-sm mt-2 ' + (isError ? 'text-error' : 'text-success');
        statusEl.classList.remove('hidden');
    }

    function clearStatus(): void {
        if (statusEl) { statusEl.textContent = ''; statusEl.classList.add('hidden'); }
    }

    downloadBtn.addEventListener('click', () => {
        const snakkSudo = (window as any).snakkSudo as { ensure: (cb: () => void) => void } | undefined;
        snakkSudo?.ensure(async () => {
            downloadBtn.disabled = true;
            clearStatus();
            const originalText = downloadBtn.innerHTML;
            downloadBtn.innerHTML = '<span class="loading loading-spinner loading-xs"></span> Preparing export...';

            try {
                const res = await fetch('/bff/me/data-export', { credentials: 'include' });
                if (res.status === 403) { setStatus('Session expired. Please re-authenticate.', true); return; }
                if (!res.ok) {
                    let errorMsg = 'Export failed. Please try again.';
                    try {
                        const data = await res.json();
                        if (data?.error) errorMsg = data.error;
                    } catch { /* ignore */ }
                    setStatus(errorMsg, true);
                    return;
                }

                const blob = await res.blob();
                const url = URL.createObjectURL(blob);
                const filename = `snakk-data-export-${new Date().toISOString().slice(0, 10)}.json`;

                const a = document.createElement('a');
                a.href = url;
                a.download = filename;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            } catch {
                setStatus('Network error. Please try again.', true);
            } finally {
                downloadBtn.disabled = false;
                downloadBtn.innerHTML = originalText;
            }
        });
    });
})();
