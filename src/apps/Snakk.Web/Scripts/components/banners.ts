/**
 * Banner dismissal — hides previously dismissed banners and handles dismiss clicks.
 */
(function () {
    'use strict';

    // Hide already-dismissed banners on load
    document.querySelectorAll<HTMLElement>('.banner-banner[data-dismissible="true"]').forEach(banner => {
        const id = banner.dataset.bannerId;
        if (id && localStorage.getItem('banner-dismissed-' + id)) {
            banner.style.display = 'none';
        }
    });

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
