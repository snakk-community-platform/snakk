(function () {
    'use strict';

    if (window._snakkFollowingInit) return;
    window._snakkFollowingInit = true;

    document.addEventListener('click', async function (e) {
        var btn = e.target.closest('[data-unfollow-type]');
        if (!btn) return;

        var type = btn.dataset.unfollowType;
        var id   = btn.dataset.unfollowId;
        if (!type || !id) return;

        var url = type === 'space'      ? '/bff/spaces/'      + id + '/follow'
                : type === 'discussion' ? '/bff/discussions/' + id + '/follow'
                :                        '/bff/users/'        + id + '/follow';

        btn.disabled = true;
        try {
            var res = await fetch(url, { method: 'POST', credentials: 'include' });
            if (res.ok) {
                var item = btn.closest('[data-following-item]');
                if (item) item.remove();
            }
        } catch (_) {
            btn.disabled = false;
        }
    });
})();
