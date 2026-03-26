/**
 * Admin Editor — JS interop bridge for Blazor to use the Milkdown markdown editor.
 * Depends on markdown-editor.js being loaded first (provides window.SnakkEditor).
 */
(function() {
    'use strict';

    var editors = {};

    window.SnakkAdminEditor = {
        init: async function(containerId, textareaId, placeholder, initialValue) {
            var container = document.getElementById(containerId);
            var textarea = document.getElementById(textareaId);
            if (!container || !textarea) {
                console.error('[AdminEditor] Container or textarea not found:', containerId, textareaId);
                return;
            }

            if (!window.SnakkEditor) {
                console.error('[AdminEditor] window.SnakkEditor not available. Is markdown-editor.js loaded?');
                return;
            }

            var editor = await window.SnakkEditor.init({
                container: container,
                textarea: textarea,
                placeholder: placeholder || 'Write your content...',
                initialValue: initialValue || ''
            });

            if (editor) {
                editors[containerId] = editor;

                // Click to focus (skip toolbar/footer)
                container.addEventListener('click', function(e) {
                    if (!e.target.closest('.milkdown-toolbar, .milkdown-footer')) {
                        editor.focus();
                    }
                });
            }
        },

        getMarkdown: function(containerId) {
            var editor = editors[containerId];
            if (!editor) return '';
            return editor.getMarkdown();
        },

        setMarkdown: function(containerId, markdown) {
            var editor = editors[containerId];
            if (editor) editor.setMarkdown(markdown);
        },

        destroy: function(containerId) {
            var editor = editors[containerId];
            if (editor) {
                editor.destroy();
                delete editors[containerId];
            }
        }
    };
})();
