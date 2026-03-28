// New Discussion picker page — stash-draft-and-navigate action
(function () {
    'use strict';

    window.SnakkActions.on('stash-draft-and-navigate', (_el) => {
        // Nothing to stash from the picker page itself,
        // but future enhancement could stash from the title input.
        // For now the default <a> navigation proceeds unimpeded.
    });
})();
