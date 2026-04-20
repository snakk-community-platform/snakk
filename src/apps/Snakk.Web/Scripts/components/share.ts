/**
 * Share page action — copies the current URL to the clipboard.
 */
(function () {
    'use strict';

    window.SnakkActions.on('share-page', (el) => {
        const label = el.querySelector('span');
        if (!label) return;

        navigator.clipboard.writeText(window.location.href).then(() => {
            const orig = label.textContent ?? '';
            label.textContent = 'Copied!';
            setTimeout(() => { label.textContent = orig; }, 2000);
        }).catch(() => { /* clipboard unavailable */ });
    });

})();
