/**
 * Banner dismissal — hides previously dismissed banners and handles dismiss clicks.
 */
(function () {
    'use strict';

    // Register dismiss action with global delegation system
    window.SnakkActions.on('dismiss-banner', (el) => {
        const bannerId = el.dataset.bannerId;
        if (bannerId) {
            localStorage.setItem('banner-dismissed-' + bannerId, '1');
        }
        const banner = el.closest('.banner-banner') as HTMLElement | null;
        if (banner) banner.style.display = 'none';
    });
})();
