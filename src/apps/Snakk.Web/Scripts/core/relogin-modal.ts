(function () {
    'use strict';

    const modal = document.getElementById('relogin-modal') as HTMLDialogElement;
    if (!modal) return;

    const strings = {
        wrongPassword:   document.querySelector<HTMLMetaElement>('meta[name="snakk-relogin-wrong-password"]')?.content ?? 'Incorrect password.',
        generic:         document.querySelector<HTMLMetaElement>('meta[name="snakk-relogin-generic"]')?.content ?? 'Something went wrong.',
        fullExpired:     document.querySelector<HTMLMetaElement>('meta[name="snakk-relogin-full-expired"]')?.content ?? 'Your session has fully expired. Please use another sign-in method.',
        inactivity:      document.querySelector<HTMLMetaElement>('meta[name="snakk-relogin-inactivity"]')?.content ?? 'You were signed out due to inactivity.',
    };

    let openReason: 'inactivity' | 'server' | null = null;
    let closedByRemote = false;

    // Scroll lock — position:fixed + negative top preserves scroll position (same pattern as markdown-editor)
    let scrollLocked = false;
    let lockedScrollY = 0;

    function lockBodyScroll(): void {
        if (scrollLocked) return;
        scrollLocked = true;
        lockedScrollY = window.scrollY;
        document.body.style.top = `-${lockedScrollY}px`;
        document.body.style.overflow = 'hidden';
        document.body.style.position = 'fixed';
        document.body.style.width = '100%';
    }

    function unlockBodyScroll(): void {
        if (!scrollLocked) return;
        scrollLocked = false;
        document.body.style.overflow = '';
        document.body.style.position = '';
        document.body.style.top = '';
        document.body.style.width = '';
        window.scrollTo(0, lockedScrollY);
    }

    // Listen for successful relogin in another tab → just dismiss; cookies are shared
    window.SnakkBroadcast?.on('auth:relogin-success', () => {
        if (!modal.open) return;
        try { localStorage.removeItem('snakk:session:expires'); } catch {}
        closedByRemote = true;
        modal.close();
    });

    // Another tab chose to stay logged out → close modal (if open) and reload
    window.SnakkBroadcast?.on('auth:stay-logged-out', () => {
        closedByRemote = true;
        if (modal.open) modal.close();
        window.location.reload();
    });

    function focusPassword(): void {
        setTimeout(() => {
            (modal.querySelector<HTMLInputElement>('#relogin-password'))?.focus();
        }, 150);
    }

    // Auto-open on server-detected revocation (?session-expired=1)
    if (modal.hasAttribute('data-auto-open')) {
        openReason = 'server';
        modal.showModal();
        lockBodyScroll();
        const url = new URL(window.location.href);
        url.searchParams.delete('session-expired');
        history.replaceState(null, '', url.toString());
        focusPassword();
    }

    // On dismiss reload — server auth middleware handles the redirect if session is gone
    modal.addEventListener('close', () => {
        unlockBodyScroll();
        if (closedByRemote || !openReason) return;
        window.SnakkBroadcast?.send({ type: 'auth:stay-logged-out' });
        window.location.reload();
    });

    // Inactivity path — session-guard fires this event
    document.addEventListener('snakk:session:expired', (e: Event) => {
        const detail = (e as CustomEvent<{ reason?: string }>).detail;
        if (detail?.reason === 'inactivity') {
            const subtitle = modal.querySelector<HTMLElement>('#relogin-subtitle');
            if (subtitle) subtitle.textContent = strings.inactivity;
        }
        openReason = 'inactivity';
        if (!modal.open) {
            modal.showModal();
            lockBodyScroll();
        }
        focusPassword();
    });

    // --- Password form ---
    const form      = modal.querySelector<HTMLFormElement>('#relogin-form');
    const errorEl   = modal.querySelector<HTMLElement>('#relogin-error');
    const submitBtn = modal.querySelector<HTMLButtonElement>('#relogin-submit');

    form?.addEventListener('submit', async (e: Event) => {
        e.preventDefault();
        const password = (form.querySelector<HTMLInputElement>('#relogin-password'))?.value ?? '';
        if (!password) return;

        if (errorEl) errorEl.classList.add('sn-hidden');
        if (submitBtn) submitBtn.disabled = true;

        try {
            const res = await fetch('/bff/auth/relogin', {
                method:  'POST',
                headers: { 'Content-Type': 'application/json' },
                body:    JSON.stringify({ password }),
            });

            if (res.ok) {
                form.reset();
                try { localStorage.removeItem('snakk:session:expires'); } catch {}
                window.SnakkBroadcast?.send({ type: 'auth:relogin-success' });
                openReason = null;
                modal.close();
                return;
            }

            if (errorEl) {
                if (res.status === 401) {
                    const body = await res.json().catch(() => ({})) as { error?: string };
                    errorEl.textContent = body.error === 'no_session'
                        ? strings.fullExpired
                        : strings.wrongPassword;
                } else {
                    errorEl.textContent = strings.generic;
                }
                errorEl.classList.remove('sn-hidden');
            }
        } catch {
            if (errorEl) {
                errorEl.textContent = strings.generic;
                errorEl.classList.remove('sn-hidden');
            }
        } finally {
            if (submitBtn) submitBtn.disabled = false;
        }
    });

    // --- Passkey ---
    initReloginPasskey();

    // --- Last-used provider collapse ---
    initReloginLastUsed();

    function initReloginPasskey(): void {
        const btn = modal.querySelector<HTMLButtonElement>('[data-nudge-passkey]');
        if (!btn || !window.PublicKeyCredential) return;
        btn.dataset['nudgePasskeyInit'] = '1'; // prevent site.ts initNudgePasskey() from double-claiming
        btn.style.display = '';
        const divider = modal.querySelector<HTMLElement>('#relogin-alt-divider');
        if (divider) divider.style.display = '';

        const passkeyErrorEl = modal.querySelector<HTMLElement>('#relogin-passkey-error');
        const returnUrl = btn.dataset['returnUrl'] || '/';

        function b64urlToBuffer(s: string): ArrayBuffer {
            const base64 = s.replace(/-/g, '+').replace(/_/g, '/');
            const padded = base64.padEnd(base64.length + (4 - base64.length % 4) % 4, '=');
            const binary = atob(padded);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
            return bytes.buffer;
        }

        function bufToB64url(buf: ArrayBuffer): string {
            const bytes = new Uint8Array(buf);
            let binary = '';
            for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]!);
            return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
        }

        function prepareOptions(optionsJson: string): PublicKeyCredentialRequestOptions {
            const raw = JSON.parse(optionsJson) as {
                challenge: string;
                allowCredentials?: Array<{ id: string; type: string; transports?: AuthenticatorTransport[] }>;
                timeout?: number;
                userVerification?: UserVerificationRequirement;
                rpId?: string;
            };
            return {
                challenge: b64urlToBuffer(raw.challenge),
                allowCredentials: raw.allowCredentials?.map(c => ({
                    id: b64urlToBuffer(c.id),
                    type: c.type as PublicKeyCredentialType,
                    transports: c.transports
                })),
                timeout: raw.timeout,
                userVerification: raw.userVerification ?? 'required',
                rpId: raw.rpId
            };
        }

        function serializeAssertion(cred: PublicKeyCredential): string {
            const resp = cred.response as AuthenticatorAssertionResponse;
            return JSON.stringify({
                id: cred.id,
                rawId: bufToB64url(cred.rawId),
                type: cred.type,
                response: {
                    authenticatorData: bufToB64url(resp.authenticatorData),
                    clientDataJSON:    bufToB64url(resp.clientDataJSON),
                    signature:         bufToB64url(resp.signature),
                    userHandle:        resp.userHandle ? bufToB64url(resp.userHandle) : null
                }
            });
        }

        btn.addEventListener('click', () => {
            btn.disabled = true;
            if (passkeyErrorEl) { passkeyErrorEl.textContent = ''; passkeyErrorEl.classList.add('sn-hidden'); }

            fetch('/auth/passkey/begin-login', {
                method:  'POST',
                headers: { 'Content-Type': 'application/json' },
                body:    JSON.stringify({ email: null })
            })
            .then(res => {
                if (!res.ok) throw new Error('Could not start passkey login');
                return res.json() as Promise<{ optionsJson: string; challengeId: string }>;
            })
            .then(data =>
                navigator.credentials.get({ publicKey: prepareOptions(data.optionsJson) })
                    .then(cred => fetch('/auth/passkey/complete-login', {
                        method:  'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body:    JSON.stringify({
                            challengeId:          data.challengeId,
                            assertionResponseJson: serializeAssertion(cred as PublicKeyCredential),
                            returnUrl
                        })
                    }))
            )
            .then(res => {
                if (!res.ok) {
                    return (res.json().catch(() => ({})) as Promise<{ error?: string }>)
                        .then(e => { throw new Error(e.error || 'Passkey login failed'); });
                }
                return res.json() as Promise<{ twoFactorRequired?: boolean; email?: string; redirectUrl?: string }>;
            })
            .then(data => {
                try { localStorage.setItem('snakk:lastOAuthProvider', 'passkey'); } catch {}
                if (data.twoFactorRequired) {
                    window.location.href = '/auth/twofactorverify?email='
                        + encodeURIComponent(data.email || '')
                        + '&returnUrl=' + encodeURIComponent(returnUrl)
                        + '&provider=passkey';
                    return;
                }
                form?.reset();
                try { localStorage.removeItem('snakk:session:expires'); } catch {}
                window.SnakkBroadcast?.send({ type: 'auth:relogin-success' });
                openReason = null;
                modal.close();
            })
            .catch((err: unknown) => {
                btn.disabled = false;
                if (err instanceof Error && err.name === 'NotAllowedError') return;
                if (passkeyErrorEl) {
                    passkeyErrorEl.textContent = err instanceof Error ? err.message : 'Passkey login failed. Please try again.';
                    passkeyErrorEl.classList.remove('sn-hidden');
                }
            });
        });
    }

    function initReloginLastUsed(): void {
        const container = modal.querySelector<HTMLElement>('.sn-nudge-oauth');
        if (!container) return;
        container.dataset['lastUsedInit'] = '1'; // prevent site.ts initNudgeOAuthLastUsed() from double-claiming

        const STORAGE_KEY = 'snakk:lastOAuthProvider';
        let last: string | null = null;
        try { last = localStorage.getItem(STORAGE_KEY); } catch {}
        if (!last) return;

        const allBtns = Array.from(container.querySelectorAll<HTMLElement>('.sn-nudge-oauth-btn[data-provider]'));
        if (!allBtns.length) return;

        const lastBtn = allBtns.find(b => b.dataset['provider'] === last);
        if (!lastBtn) return;
        if (last === 'passkey' && lastBtn.style.display === 'none') return;

        if (!lastBtn.querySelector('.sn-badge-last-used')) {
            const badge = document.createElement('span');
            badge.className = 'sn-badge-last-used';
            badge.textContent = 'Last used';
            lastBtn.appendChild(badge);
        }

        const oauthBtns = allBtns.filter(b => b.dataset['provider'] !== 'passkey');
        const toHide = last === 'passkey' ? oauthBtns : oauthBtns.filter(b => b !== lastBtn);
        if (!toHide.length) return;

        const hadGrid = container.classList.contains('sn-nudge-oauth--grid');
        if (hadGrid) container.classList.remove('sn-nudge-oauth--grid');
        toHide.forEach(b => { b.style.display = 'none'; });

        const moreBtn = document.createElement('button');
        moreBtn.type = 'button';
        moreBtn.style.cssText = 'width:100%;text-align:center;font-size:var(--font-size-sm);color:var(--text-tertiary);background:transparent;border:none;padding:var(--space-1) 0;cursor:pointer;transition:color 0.15s';
        moreBtn.addEventListener('mouseenter', () => { moreBtn.style.color = 'var(--text-secondary)'; });
        moreBtn.addEventListener('mouseleave', () => { moreBtn.style.color = 'var(--text-tertiary)'; });
        moreBtn.textContent = 'More sign-in options';
        container.appendChild(moreBtn);

        moreBtn.addEventListener('click', () => {
            toHide.forEach(b => { b.style.display = ''; });
            if (hadGrid) container.classList.add('sn-nudge-oauth--grid');
            moreBtn.remove();
        });
    }
})();
