/**
 * Snakk Markdown Editor Component
 * Uses Milkdown Kit (ProseMirror-based) for WYSIWYG markdown editing.
 * Keeps a hidden textarea synced for form submission.
 * Includes a custom toolbar for formatting commands.
 */

import { Editor, rootCtx, defaultValueCtx, editorViewCtx } from '@milkdown/kit/core';
import { commonmark } from '@milkdown/kit/preset/commonmark';
import {
    toggleStrongCommand,
    toggleEmphasisCommand,
    toggleInlineCodeCommand,
    toggleLinkCommand,
    wrapInHeadingCommand,
    wrapInBlockquoteCommand,
    createCodeBlockCommand,
    updateCodeBlockLanguageCommand,
    insertHrCommand,
} from '@milkdown/kit/preset/commonmark';
import {
    schema as gfmSchema,
    inputRules as gfmInputRules,
    pasteRules as gfmPasteRules,
    keymap as gfmKeymap,
    commands as gfmCommands,
    plugins as gfmPlugins,
    remarkGFMPlugin,
    strikethroughSchema,
    toggleStrikethroughCommand,
    insertTableCommand,
} from '@milkdown/kit/preset/gfm';
import { history } from '@milkdown/kit/plugin/history';
import { listener, listenerCtx } from '@milkdown/kit/plugin/listener';
import { upload, uploadConfig } from '@milkdown/kit/plugin/upload';
import { replaceAll, getMarkdown, callCommand, $inputRule, $markAttr, $markSchema, $command, $remark, $prose } from '@milkdown/kit/utils';
import { markRule } from '@milkdown/kit/prose';
import { toggleMark } from '@milkdown/kit/prose/commands';
import { Decoration } from '@milkdown/kit/prose/view';
import { Plugin, PluginKey, TextSelection } from '@milkdown/kit/prose/state';
import { Slice, Fragment } from '@milkdown/kit/prose/model';
import type { Node } from '@milkdown/kit/prose/model';
import { createImageGroupPlugin, createGapCursorPlugin, createGapParaPlugin, createEmptyParaCursorPlugin, setEmptyParaCursorCallback, setChartEditCallback, setTableEditCallback, setImageEditCallback, setCodeEditCallback, setListEditCallback, countImagesFromView, countBlocksInEditor, replaceImageSrc, buildListNode, type TableModalData, type ImageModalData, type CodeModalData, type ListModalData, type ListItem, type BlockCounts } from './editor-node-views';
import { T } from '../locales/t.g';

// ============================================================================
// Code Block Language Options (must match Prism grammars in prism-entry.mjs)
// ============================================================================

const CODE_LANGUAGES: { value: string; label: string }[] = [
    { value: '', label: 'Plain text' },
    { value: 'javascript', label: 'JavaScript' },
    { value: 'typescript', label: 'TypeScript' },
    { value: 'csharp', label: 'C#' },
    { value: 'html', label: 'HTML' },
    { value: 'css', label: 'CSS' },
    { value: 'sql', label: 'SQL' },
    { value: 'json', label: 'JSON' },
    { value: 'bash', label: 'Bash' },
    { value: 'python', label: 'Python' },
    { value: 'markdown', label: 'Markdown' },
    { value: 'yaml', label: 'YAML' },
    { value: 'xml', label: 'XML' },
    { value: 'c', label: 'C' },
    { value: 'cpp', label: 'C++' },
    { value: 'java', label: 'Java' },
    { value: 'lua', label: 'Lua' },
    { value: 'perl', label: 'Perl' },
    { value: 'php', label: 'PHP' },
    { value: 'powershell', label: 'PowerShell' },
    { value: 'r', label: 'R' },
    { value: 'ruby', label: 'Ruby' },
    { value: 'rust', label: 'Rust' },
    { value: 'chart', label: 'Chart' },
];

// ============================================================================
// Type Definitions
// ============================================================================

interface EditorInstance {
    getMarkdown(): string;
    setMarkdown(markdown: string): void;
    focus(): void;
    destroy(): void;
    hasPendingUploads(): boolean;
    flushUploads(onProgress?: (done: number, total: number) => void): Promise<void>;
    getImageCount(): number;
}

interface SnakkEditorOptions {
    container: HTMLElement;
    textarea: HTMLTextAreaElement;
    placeholder?: string;
    initialValue?: string;
    height?: string;
    onChange?: (markdown: string) => void;
    onUploadStateChange?: (uploading: boolean) => void;
    // When set, only toolbar items whose feature tag is in this list are shown.
    // Omit for the default full toolbar. Block adder is hidden when no block
    // features (image, code, table, blockquote, list, heading) are included.
    allowedFeatures?: string[];
}

interface SnakkEditorAPI {
    init(options: SnakkEditorOptions): Promise<EditorInstance | null>;
    getInstance(container: HTMLElement): EditorInstance | null;
    destroy(container: HTMLElement): void;
    showImageModal(options: ImageModalOptions): void;
    renderLayoutPreview(container: HTMLElement, images: Array<{ url: string; alt: string }>, layout: string): void;
    createImagePickerWidget(container: HTMLElement, options: ImagePickerWidgetOptions): ImagePickerWidgetController;
}

interface ToolbarItem {
    feature?: string;
    icon: string;
    title: string;
    action: (editor: Editor) => void;
    className?: string;
    allowInBlock?: boolean;
    opensDialog?: boolean;
    hasDropdown?: boolean;
    separator?: false;
}

interface ToolbarSeparator {
    separator: true;
}

interface ToolbarGroup {
    feature?: string;
    groupIcon: string;
    groupTitle: string;
    groupClassName?: string;
    children: ToolbarItem[];
}

type ToolbarEntry = ToolbarItem | ToolbarSeparator | ToolbarGroup;

// ============================================================================
// Custom Strikethrough Input Rule (double-tilde only)
// ============================================================================

/**
 * Replaces the default GFM strikethrough input rule which matches both ~ and ~~.
 * This version only matches ~~ so that single ~ is reserved for subscript (Markdig).
 */
const doubleStrikethroughInputRule = $inputRule((ctx) => {
    return markRule(
        /(?<![\w:/])(~~)(.+?)\1(?!\w|\/)/,
        strikethroughSchema.type(ctx)
    );
});

// ============================================================================
// Emphasis Extras: Mark Schemas, Input Rules, Commands
// ============================================================================
// Defines proper ProseMirror marks for subscript (~), superscript (^),
// inserted (++), and marked/highlight (==). These are Markdig extensions
// not supported by remark, so we also provide a custom remark plugin for
// round-trip markdown parsing/serialization.

interface EmphasisExtraDef {
    id: string;
    tag: string;
    mdastType: string;
    delimiter: string;
    inputRegex: RegExp;
    htmlClass?: string;
}

const EMPHASIS_EXTRAS: EmphasisExtraDef[] = [
    {
        id: 'subscript', tag: 'sub', mdastType: 'subscript',
        delimiter: '~',
        inputRegex: /(?<!\~)\~([^\~\n]+?)\~(?!\~)/,
    },
    {
        id: 'superscript', tag: 'sup', mdastType: 'superscript',
        delimiter: '^',
        inputRegex: /(?<!\^)\^([^\^\n]+?)\^(?!\^)/,
    },
    {
        id: 'inserted', tag: 'ins', mdastType: 'inserted',
        delimiter: '++',
        inputRegex: /\+\+([^\n]+?)\+\+/,
    },
    {
        id: 'marked', tag: 'mark', mdastType: 'marked',
        delimiter: '==',
        inputRegex: /==([^\n]+?)==/,
    },
    {
        id: 'spoiler', tag: 'span', mdastType: 'spoiler',
        delimiter: '||',
        inputRegex: /\|\|([^\n]+?)\|\|/,
        htmlClass: 'spoiler',
    },
];

// -- Mark attributes --
const subscriptAttr = $markAttr('subscript');
const superscriptAttr = $markAttr('superscript');
const insertedAttr = $markAttr('inserted');
const markedAttr = $markAttr('marked');
const spoilerAttr = $markAttr('spoiler');

const markAttrs = [subscriptAttr, superscriptAttr, insertedAttr, markedAttr, spoilerAttr];

// -- Mark schemas --
function buildMarkSchemaFor(def: EmphasisExtraDef, attr: ReturnType<typeof $markAttr>) {
    return $markSchema(def.id, (ctx) => ({
        parseDOM: def.htmlClass
            ? [{ tag: `${def.tag}[class="${def.htmlClass}"]` }]
            : [{ tag: def.tag }],
        toDOM: (mark) => def.htmlClass
            ? [def.tag, { class: def.htmlClass }, 0]
            : [def.tag, ctx.get(attr.key)(mark), 0],
        parseMarkdown: {
            match: (node) => node.type === def.mdastType,
            runner: (state, node, markType) => {
                state.openMark(markType);
                state.next(node.children);
                state.closeMark(markType);
            },
        },
        toMarkdown: {
            match: (mark) => mark.type.name === def.id,
            runner: (state, mark) => {
                state.withMark(mark, def.mdastType);
            },
        },
    }));
}

const subscriptSchema = buildMarkSchemaFor(EMPHASIS_EXTRAS[0]!, subscriptAttr);
const superscriptSchema = buildMarkSchemaFor(EMPHASIS_EXTRAS[1]!, superscriptAttr);
const insertedSchema = buildMarkSchemaFor(EMPHASIS_EXTRAS[2]!, insertedAttr);
const markedSchema = buildMarkSchemaFor(EMPHASIS_EXTRAS[3]!, markedAttr);
const spoilerSchema = buildMarkSchemaFor(EMPHASIS_EXTRAS[4]!, spoilerAttr);

const markSchemas = [subscriptSchema, superscriptSchema, insertedSchema, markedSchema, spoilerSchema];

// -- Input rules (consume delimiters, apply mark) --
const subscriptInputRule = $inputRule((ctx) =>
    markRule(EMPHASIS_EXTRAS[0]!.inputRegex, subscriptSchema.type(ctx)));
const superscriptInputRule = $inputRule((ctx) =>
    markRule(EMPHASIS_EXTRAS[1]!.inputRegex, superscriptSchema.type(ctx)));
const insertedInputRule = $inputRule((ctx) =>
    markRule(EMPHASIS_EXTRAS[2]!.inputRegex, insertedSchema.type(ctx)));
const markedInputRule = $inputRule((ctx) =>
    markRule(EMPHASIS_EXTRAS[3]!.inputRegex, markedSchema.type(ctx)));
const spoilerInputRule = $inputRule((ctx) =>
    markRule(EMPHASIS_EXTRAS[4]!.inputRegex, spoilerSchema.type(ctx)));

const markInputRules = [subscriptInputRule, superscriptInputRule, insertedInputRule, markedInputRule, spoilerInputRule];

// -- Toggle commands (for toolbar buttons) --
const toggleSubscriptCommand = $command('ToggleSubscript', (ctx) => () =>
    toggleMark(subscriptSchema.type(ctx)));
const toggleSuperscriptCommand = $command('ToggleSuperscript', (ctx) => () =>
    toggleMark(superscriptSchema.type(ctx)));
const toggleInsertedCommand = $command('ToggleInserted', (ctx) => () =>
    toggleMark(insertedSchema.type(ctx)));
const toggleMarkedCommand = $command('ToggleMarked', (ctx) => () =>
    toggleMark(markedSchema.type(ctx)));
const toggleSpoilerCommand = $command('ToggleSpoiler', (ctx) => () =>
    toggleMark(spoilerSchema.type(ctx)));

const markCommands = [toggleSubscriptCommand, toggleSuperscriptCommand, toggleInsertedCommand, toggleMarkedCommand, toggleSpoilerCommand];

// ============================================================================
// Custom Remark Plugin for Emphasis Extras
// ============================================================================
// Remark/micromark doesn't natively parse ~sub~, ^sup^, ++ins++, ==mark==.
// This plugin provides:
// 1. A tree transform (parse direction): finds delimiter patterns in text nodes
//    and converts them to custom mdast nodes (subscript, superscript, etc.)
// 2. Stringify handlers (serialize direction): converts custom mdast nodes back
//    to delimited markdown text.

function emphasisExtrasRemarkPluginFn(this: any) {
    const data = this.data();

    // Register stringify handlers so remark-stringify can serialize our custom types
    const toMdExts: any[] = data.toMarkdownExtensions || (data.toMarkdownExtensions = []);
    toMdExts.push({
        handlers: Object.fromEntries(EMPHASIS_EXTRAS.map((def) => [
            def.mdastType,
            (node: any, _parent: any, state: any, info: any) => {
                const tracker = state.createTracker(info);
                const exit = state.enter(def.mdastType);
                let value = tracker.move(def.delimiter);
                value += state.containerPhrasing(node, {
                    ...tracker.current(),
                    before: value,
                    after: def.delimiter.charAt(0),
                });
                value += tracker.move(def.delimiter);
                exit();
                return value;
            },
        ])),
        unsafe: [
            { character: '~', inConstruct: 'phrasing' },
            { character: '^', inConstruct: 'phrasing' },
            { character: '|', inConstruct: 'phrasing' },
        ],
    });

    // Tree transform: convert text patterns to custom mdast nodes after parsing
    return (tree: any) => {
        visitTextNodes(tree);
    };
}

/** Walk the mdast tree and split text nodes that contain emphasis extras patterns. */
function visitTextNodes(node: any): void {
    if (!node.children) return;

    for (let i = 0; i < node.children.length; i++) {
        const child = node.children[i];

        if (child.type === 'text' && child.value) {
            const replacements = splitTextNode(child.value);
            if (replacements.length > 1 || replacements[0]?.type !== 'text') {
                node.children.splice(i, 1, ...replacements);
                i += replacements.length - 1;
            }
        } else {
            visitTextNodes(child);
        }
    }
}

/** Recursively split a text value on the first matching emphasis pattern. */
function splitTextNode(text: string): any[] {
    const patterns = [
        { regex: /\+\+(.+?)\+\+/, type: 'inserted' },
        { regex: /==(.+?)==/, type: 'marked' },
        { regex: /\|\|(.+?)\|\|/, type: 'spoiler' },
        { regex: /(?<!\~)\~([^\~]+?)\~(?!\~)/, type: 'subscript' },
        { regex: /(?<!\^)\^([^\^]+?)\^(?!\^)/, type: 'superscript' },
    ];

    for (const { regex, type } of patterns) {
        const match = regex.exec(text);
        if (match) {
            const result: any[] = [];
            const before = text.slice(0, match.index);
            const content = match[1]!;
            const after = text.slice(match.index + match[0].length);

            if (before) result.push(...splitTextNode(before));
            result.push({ type, children: [{ type: 'text', value: content }] });
            if (after) result.push(...splitTextNode(after));
            return result;
        }
    }

    return [{ type: 'text', value: text }];
}

const remarkEmphasisExtras = $remark('emphasisExtras', () => emphasisExtrasRemarkPluginFn);

// ============================================================================
// Paste Sanitization Plugin
// Strips disallowed nodes (images, iframes, etc.) from pasted HTML content.
// Images should only come from our upload flow, not from external paste.
// ============================================================================

const ALLOWED_NODE_TYPES = new Set([
    'doc', 'paragraph', 'blockquote', 'bullet_list', 'ordered_list',
    'list_item', 'code_block', 'horizontal_rule', 'table', 'table_row',
    'table_header', 'table_cell', 'text', 'hard_break',
]);

function sanitizePastedFragment(fragment: Fragment): Fragment {
    const sanitized: Node[] = [];
    fragment.forEach((node) => {
        if (node.isText) {
            sanitized.push(node);
            return;
        }

        if (!ALLOWED_NODE_TYPES.has(node.type.name)) {
            // For disallowed block nodes (image, etc.), extract any text content as plain text
            if (node.textContent) {
                const textNode = node.type.schema.text(node.textContent);
                sanitized.push(textNode);
            }
            return;
        }

        // Recurse into allowed container nodes
        if (node.childCount > 0) {
            const cleanedContent = sanitizePastedFragment(node.content);
            const copy = node.copy(cleanedContent);
            sanitized.push(copy);
        } else {
            sanitized.push(node.copy());
        }
    });

    return Fragment.from(sanitized);
}

const pasteSanitize = $prose(() => new Plugin({
    key: new PluginKey('paste-sanitize'),
    props: {
        transformPasted(slice) {
            const cleaned = sanitizePastedFragment(slice.content);
            return new Slice(cleaned, slice.openStart, slice.openEnd);
        },
    },
}));

// ============================================================================
// Toolbar Definition
// ============================================================================

function buildToolbarItems(): ToolbarEntry[] {
    return [
        {
            feature: 'emoji',
            icon: '😀', title: 'Emoji', className: 'toolbar-emoji', allowInBlock: true, hasDropdown: true,
            action: (e) => showEmojiPicker(e),
        },
        { separator: true },
        {
            feature: 'bold',
            icon: 'B', title: 'Bold (Ctrl+B)', className: 'toolbar-bold', allowInBlock: true,
            action: (e) => e.action(callCommand(toggleStrongCommand.key)),
        },
        {
            feature: 'italic',
            icon: 'I', title: 'Italic (Ctrl+I)', className: 'toolbar-italic', allowInBlock: true,
            action: (e) => e.action(callCommand(toggleEmphasisCommand.key)),
        },
        {
            feature: 'underline',
            icon: 'U', title: 'Underline (Ctrl+U)', className: 'toolbar-underline', allowInBlock: true,
            action: (e) => e.action(callCommand(toggleInsertedCommand.key)),
        },
        {
            feature: 'text-effects',
            groupIcon: 'Aₓ', groupTitle: 'Text Effects', groupClassName: 'toolbar-group-extras',
            children: [
                {
                    icon: 'S', title: 'Strikethrough (Ctrl+Shift+X)', className: 'toolbar-strikethrough', allowInBlock: true,
                    action: (e) => e.action(callCommand(toggleStrikethroughCommand.key)),
                },
                {
                    icon: 'x₂', title: 'Subscript', className: 'toolbar-subscript', allowInBlock: true,
                    action: (e) => e.action(callCommand(toggleSubscriptCommand.key)),
                },
                {
                    icon: 'x²', title: 'Superscript', className: 'toolbar-superscript', allowInBlock: true,
                    action: (e) => e.action(callCommand(toggleSuperscriptCommand.key)),
                },
            ],
        },
        { separator: true },
        {
            feature: 'highlight',
            icon: '🖌', title: 'Highlight', className: 'toolbar-marked', allowInBlock: true,
            action: (e) => e.action(callCommand(toggleMarkedCommand.key)),
        },
        {
            feature: 'inline-code',
            icon: '⟨⟩', title: 'Inline Code',
            action: (e) => e.action(callCommand(toggleInlineCodeCommand.key)),
        },
        {
            feature: 'spoiler',
            icon: '👁', title: 'Spoiler', className: 'toolbar-spoiler', allowInBlock: true,
            action: (e) => e.action(callCommand(toggleSpoilerCommand.key)),
        },
        { separator: true },
        {
            feature: 'link',
            icon: '🔗', title: 'Link', className: 'toolbar-link', allowInBlock: true, opensDialog: true,
            action: (e) => showLinkDialog(e),
        },
    ];
}

// ============================================================================
// Toolbar Group Dropdown
// ============================================================================

let activeGroupDropdown: HTMLElement | null = null;

function showGroupDropdown(editor: Editor, triggerBtn: HTMLElement, children: ToolbarItem[]): void {
    closeGroupDropdown();
    closeEmojiPicker();
    closeTableModal();
    closeLinkDialog();
    closeChartModal();
    closeImageModal();
    closeCodeModal();

    const dropdown = document.createElement('div');
    dropdown.className = 'milkdown-group-dropdown';

    children.forEach((child) => {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'milkdown-group-dropdown-btn';

        const label = document.createElement('span');
        label.className = 'group-dropdown-icon';
        label.textContent = child.icon;
        btn.appendChild(label);

        const text = document.createElement('span');
        text.className = 'group-dropdown-label';
        text.textContent = child.title;
        btn.appendChild(text);

        btn.title = child.title;
        btn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            e.stopPropagation();

            // Check if source mode is active — if so, use source action
            const editorEl = triggerBtn.closest('.milkdown-editor') as HTMLElement | null;
            const srcActions = editorEl ? (editorEl as any).__sourceActions as Record<string, () => void> | undefined : undefined;
            const srcMode = editorEl ? (editorEl as any).__isSourceMode as (() => boolean) | undefined : undefined;

            if (srcMode?.() && srcActions?.[child.title]) {
                srcActions[child.title]();
            } else {
                child.action(editor);
            }

            closeGroupDropdown();
        });

        dropdown.appendChild(btn);
    });

    // Prevent ProseMirror from stealing focus
    dropdown.addEventListener('mousedown', (e) => {
        e.stopPropagation();
    });

    document.body.appendChild(dropdown);
    activeGroupDropdown = dropdown;

    // Position below the trigger button, arrow near left edge pointing at button
    const btnRect = triggerBtn.getBoundingClientRect();
    const btnCenterX = btnRect.left + btnRect.width / 2;
    const top = btnRect.bottom + 6;
    const notchOffset = 16;

    const dropdownWidth = dropdown.offsetWidth;
    let left = btnCenterX - notchOffset;
    left = Math.max(8, Math.min(left, window.innerWidth - dropdownWidth - 8));

    dropdown.style.top = `${top}px`;
    dropdown.style.left = `${left}px`;

    const notchLeft = btnCenterX - left;
    dropdown.style.setProperty('--notch-left', `${notchLeft}px`);

    // Close on outside click or scroll
    const onOutsideClick = (e: MouseEvent) => {
        if (!dropdown.contains(e.target as Node)) cleanup();
    };
    const onScroll = (e: Event) => {
        if (e.target instanceof Node && dropdown.contains(e.target)) return;
        cleanup();
    };
    const cleanup = () => {
        closeGroupDropdown();
        document.removeEventListener('mousedown', onOutsideClick);
        window.removeEventListener('scroll', onScroll, true);
    };
    setTimeout(() => {
        document.addEventListener('mousedown', onOutsideClick);
        window.addEventListener('scroll', onScroll, true);
    }, 0);
}

function closeGroupDropdown(): void {
    if (activeGroupDropdown) {
        activeGroupDropdown.remove();
        activeGroupDropdown = null;
    }
}

// ============================================================================
// Block Adder State
// ============================================================================

let activeBlockPicker: HTMLElement | null = null;
let blockPickerCleanup: (() => void) | null = null;

function closeBlockPicker(): void {
    if (activeBlockPicker) {
        activeBlockPicker.remove();
        activeBlockPicker = null;
    }
    if (blockPickerCleanup) {
        blockPickerCleanup();
        blockPickerCleanup = null;
    }
}

// ============================================================================
// Link Dialog
// ============================================================================

let activeLinkDialog: HTMLElement | null = null;

function showLinkDialog(editor: Editor): void {
    // Close any existing dialog/picker
    closeLinkDialog();
    closeGroupDropdown();
    closeImageModal();

    // Get selected text to pre-fill link text
    let selectedText = '';
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const { from, to } = view.state.selection;
        if (from !== to) {
            selectedText = view.state.doc.textBetween(from, to);
        }
    });

    // Build dialog
    const dialog = document.createElement('div');
    dialog.className = 'milkdown-link-dialog';
    dialog.innerHTML = `
        <div class="link-dialog-field">
            <label>URL</label>
            <input type="url" class="link-dialog-url" placeholder="https://example.com" />
        </div>
        <div class="link-dialog-field">
            <label>Link text <span class="text-muted">(optional)</span></label>
            <input type="text" class="link-dialog-text" placeholder="Display text" />
        </div>
        <div class="link-dialog-actions">
            <button type="button" class="link-dialog-cancel">${T.common.cancel}</button>
            <button type="button" class="link-dialog-insert">Insert Link</button>
        </div>
    `;

    // Pre-fill link text with selection
    const textInput = dialog.querySelector('.link-dialog-text') as HTMLInputElement;
    if (selectedText) textInput.value = selectedText;

    const urlInput = dialog.querySelector('.link-dialog-url') as HTMLInputElement;

    // Insert handler
    const insertLink = () => {
        const href = urlInput.value.trim();
        if (!href) return;

        const linkText = textInput.value.trim();

        // Use URL as display text when no link text provided and no selection
        const displayText = linkText || href;

        editor.action((ctx) => {
            const view = ctx.get(editorViewCtx);
            const { from, to } = view.state.selection;
            const hasSelection = from !== to;

            if (!hasSelection) {
                // Insert display text then apply link
                const tr = view.state.tr.insertText(displayText, from);
                view.dispatch(tr);
                // Select the inserted text
                const newFrom = from;
                const newTo = from + displayText.length;
                const selectTr = view.state.tr.setSelection(
                    view.state.selection.constructor.create(view.state.doc, newFrom, newTo) as any
                );
                view.dispatch(selectTr);
            }
        });

        // Apply link to selection
        editor.action(callCommand(toggleLinkCommand.key, { href }));
        closeLinkDialog();
        editor.action((ctx) => ctx.get(editorViewCtx).focus());
    };

    dialog.querySelector('.link-dialog-insert')!.addEventListener('click', insertLink);
    dialog.querySelector('.link-dialog-cancel')!.addEventListener('click', () => {
        closeLinkDialog();
        editor.action((ctx) => ctx.get(editorViewCtx).focus());
    });

    // Enter key inserts, Escape cancels
    dialog.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); insertLink(); }
        if (e.key === 'Escape') {
            closeLinkDialog();
            editor.action((ctx) => ctx.get(editorViewCtx).focus());
        }
    });

    // Close on click outside or scroll
    const onClickOutside = (e: MouseEvent) => {
        if (!dialog.contains(e.target as Node)) {
            cleanup();
        }
    };
    const onScroll = () => cleanup();
    const cleanup = () => {
        closeLinkDialog();
        document.removeEventListener('mousedown', onClickOutside);
        window.removeEventListener('scroll', onScroll, true);
    };
    // Defer so the current mousedown doesn't immediately trigger it
    setTimeout(() => {
        document.addEventListener('mousedown', onClickOutside);
        window.addEventListener('scroll', onScroll, true);
    }, 0);

    // Mount dialog on document.body with fixed positioning to fully isolate
    // from ProseMirror's focus management
    const editorWrapper = document.querySelector('.milkdown-editor') as HTMLElement;
    if (editorWrapper) {
        document.body.appendChild(dialog);
        activeLinkDialog = dialog;

        const linkBtn = editorWrapper.querySelector('.toolbar-link') as HTMLElement;
        if (linkBtn) {
            const btnRect = linkBtn.getBoundingClientRect();
            const btnCenterX = btnRect.left + btnRect.width / 2;
            const top = btnRect.bottom + 6;
            const notchOffset = 16;

            const dialogWidth = dialog.offsetWidth;
            let left = btnCenterX - notchOffset;
            left = Math.max(8, Math.min(left, window.innerWidth - dialogWidth - 8));

            dialog.style.top = `${top}px`;
            dialog.style.left = `${left}px`;

            const notchLeft = btnCenterX - left;
            dialog.style.setProperty('--notch-left', `${notchLeft}px`);
        }

        urlInput.focus();
    }
}

function closeLinkDialog(): void {
    if (activeLinkDialog) {
        activeLinkDialog.remove();
        activeLinkDialog = null;
    }
}

// ============================================================================
// Scroll lock helpers
// Uses position:fixed + negative top to prevent scroll-position reset.
// ============================================================================

let scrollLockDepth = 0;
let scrollLockY = 0;

function lockBodyScroll(): void {
    if (scrollLockDepth++ > 0) return;
    scrollLockY = window.scrollY;
    document.body.style.top = `-${scrollLockY}px`;
    document.body.style.overflow = 'hidden';
    document.body.style.position = 'fixed';
    document.body.style.width = '100%';
}

function unlockBodyScroll(): void {
    if (scrollLockDepth === 0 || --scrollLockDepth > 0) return;
    document.body.style.overflow = '';
    document.body.style.position = '';
    document.body.style.top = '';
    document.body.style.width = '';
    window.scrollTo(0, scrollLockY);
}

// ============================================================================
// Chart Editor Modal
// ============================================================================

let activeChartModal: HTMLElement | null = null;

function closeChartModal(): void {
    if (activeChartModal) {
        activeChartModal.remove();
        activeChartModal = null;
        unlockBodyScroll();
    }
}

function showChartPickerModal(editor: Editor): void {
    closeChartModal();
    closeListModal();

    const chartTypes = [
        { icon: '≡',   label: 'Bar',         type: 'bar' },
        { icon: '∿',   label: 'Line',        type: 'line' },
        { icon: '◿',   label: 'Area',        type: 'area' },
        { icon: '▤',   label: 'Stacked Bar', type: 'stacked' },
        { icon: '◔',   label: 'Pie',         type: 'pie' },
        { icon: '◎',   label: 'Donut',       type: 'donut' },
        { icon: '∷',   label: 'Scatter',     type: 'scatter' },
    ];

    const backdrop = document.createElement('div');
    backdrop.className = 'sn-modal-backdrop';
    activeChartModal = backdrop;

    const modal = document.createElement('div');
    modal.className = 'sn-modal chart-picker-modal';

    const header = document.createElement('div');
    header.className = 'sn-modal-header';
    const title = document.createElement('span');
    title.textContent = T.editor.insertChart;
    const closeBtn = document.createElement('button');
    closeBtn.className = 'sn-modal-close';
    closeBtn.innerHTML = '&times;';
    closeBtn.onclick = closeChartModal;
    header.append(title, closeBtn);

    const body = document.createElement('div');
    body.className = 'sn-modal-body chart-picker-body';
    const grid = document.createElement('div');
    grid.className = 'chart-picker-grid';

    for (const ct of chartTypes) {
        const card = document.createElement('button');
        card.className = 'chart-picker-card';
        card.type = 'button';
        const iconSpan = document.createElement('span');
        iconSpan.className = 'chart-picker-card-icon';
        iconSpan.textContent = ct.icon;
        const labelSpan = document.createElement('span');
        labelSpan.className = 'chart-picker-card-label';
        labelSpan.textContent = ct.label;
        card.append(iconSpan, labelSpan);
        card.onclick = () => {
            closeChartModal();
            showChartEditorModal(ct.type, { onConfirm: (c) => insertChartBlock(editor, c) });
        };
        grid.appendChild(card);
    }

    body.appendChild(grid);
    modal.append(header, body);
    backdrop.appendChild(modal);
    document.body.appendChild(backdrop);
    lockBodyScroll();

    backdrop.addEventListener('click', (e) => { if (e.target === backdrop) closeChartModal(); });
}

function buildChartModalField(labelText: string, input: HTMLElement): HTMLElement {
    const wrap = document.createElement('div');
    wrap.className = 'sn-modal-field';
    const lbl = document.createElement('label');
    lbl.className = 'sn-modal-label';
    lbl.textContent = labelText;
    wrap.appendChild(lbl);
    wrap.appendChild(input);
    return wrap;
}

function makeChartInput(placeholder: string, value = '', extraClass = ''): HTMLInputElement {
    const inp = document.createElement('input');
    inp.type = 'text';
    inp.className = `sn-modal-input${extraClass ? ' ' + extraClass : ''}`;
    inp.placeholder = placeholder;
    inp.value = value;
    return inp;
}

interface ChartFormData {
    title: string;
    labels: string;
    series: Array<{ name: string; values: string }>;
    data: Array<{ label: string; value: string }>;
}

function parseChartFormData(configStr: string): Partial<ChartFormData> {
    const result: Partial<ChartFormData> = {};
    const lines = configStr.split('\n');
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i]!.trim();
        if (line.startsWith('title:')) {
            result.title = line.slice(6).trim();
        } else if (line.startsWith('labels:')) {
            result.labels = line.slice(7).trim().replace(/^\[|\]$/g, '');
        } else if (line === 'data:') {
            const items: Array<{ label: string; value: string }> = [];
            i++;
            while (i < lines.length && (lines[i]!.startsWith(' ') || lines[i]!.startsWith('\t'))) {
                const dl = lines[i]!.trim();
                const colon = dl.indexOf(':');
                if (colon > 0) items.push({ label: dl.slice(0, colon).trim(), value: dl.slice(colon + 1).trim() });
                i++;
            }
            result.data = items;
            i--;
        } else if (line === 'series:') {
            const series: Array<{ name: string; values: string }> = [];
            let cur: { name: string; values: string } | null = null;
            i++;
            while (i < lines.length) {
                const sl = lines[i]!;
                if (!sl.trim()) { i++; continue; }
                const nm = sl.match(/^\s*-\s+name:\s*(.+)/);
                if (nm) { cur = { name: nm[1]!.trim(), values: '' }; series.push(cur); i++; continue; }
                const dm = sl.match(/^\s+data:\s*(\[.+\])/);
                if (dm && cur) {
                    const raw = dm[1]!.trim();
                    // Scatter [[x,y],...] → "x,y x,y"; others [v,...] → "v, v"
                    cur.values = raw.startsWith('[[')
                        ? raw.replace(/^\[|\]$/g, '').split(/\],\s*\[/).map(p => p.replace(/[\[\]]/g, '').trim()).join(' ')
                        : raw.replace(/^\[|\]$/g, '');
                    i++; continue;
                }
                if (!sl.startsWith(' ') && !sl.startsWith('\t') && !sl.trim().startsWith('-')) break;
                i++;
            }
            result.series = series;
            i--;
        }
    }
    return result;
}

function buildChartForm(type: string, form: HTMLElement, initial?: Partial<ChartFormData>): void {
    form.appendChild(buildChartModalField(
        'Title (optional)',
        makeChartInput('My Chart', initial?.title ?? '', 'chart-title-input'),
    ));

    const CHECK_ICON = '<span class="icon icon-check" style="width:12px;height:12px" aria-hidden="true"></span>';
    const WARN_ICON = '<span class="icon icon-exclamation-triangle" style="width:12px;height:12px" aria-hidden="true"></span>';

    if (type === 'pie' || type === 'donut') {
        const listWrap = document.createElement('div');
        listWrap.className = 'sn-modal-field';
        const lbl = document.createElement('label');
        lbl.className = 'sn-modal-label';
        lbl.textContent = T.editor.data;
        listWrap.appendChild(lbl);

        const list = document.createElement('div');
        list.className = 'chart-series-list';
        listWrap.appendChild(list);

        const addRow = (label = '', value = '') => {
            const row = document.createElement('div');
            row.className = 'chart-series-row';

            const nameWrap = document.createElement('div');
            nameWrap.className = 'chart-series-name-wrap';
            const nameIcon = document.createElement('span');
            nameIcon.className = 'chart-series-name-icon chart-series-name-icon-hidden';
            nameIcon.setAttribute('aria-hidden', 'true');
            const labelInp = makeChartInput('Label', label, 'chart-series-name');
            nameWrap.appendChild(nameIcon);
            nameWrap.appendChild(labelInp);

            const valInp = document.createElement('input');
            valInp.type = 'number';
            valInp.className = 'sn-modal-input chart-series-values';
            valInp.placeholder = '0';
            valInp.value = value;

            const updateNameIcon = () => {
                const hasName = !!labelInp.value.trim();
                const hasData = !!valInp.value.trim();
                if (!hasData || hasName) {
                    nameIcon.className = 'chart-series-name-icon chart-series-name-icon-hidden';
                } else {
                    nameIcon.className = 'chart-series-name-icon chart-series-name-icon-warn';
                    nameIcon.innerHTML = WARN_ICON;
                }
            };
            labelInp.addEventListener('input', updateNameIcon);
            valInp.addEventListener('input', updateNameIcon);
            updateNameIcon();

            const rm = document.createElement('button');
            rm.type = 'button';
            rm.className = 'chart-series-remove';
            rm.textContent = '×';
            rm.addEventListener('click', () => { if (list.children.length > 1) row.remove(); });
            row.appendChild(nameWrap);
            row.appendChild(valInp);
            row.appendChild(rm);
            list.appendChild(row);
        };
        const pieDefaults = initial?.data?.length
            ? initial.data
            : [{ label: '', value: '' }];
        pieDefaults.forEach(d => addRow(d.label, d.value));

        const addBtn = document.createElement('button');
        addBtn.type = 'button';
        addBtn.className = 'chart-add-btn';
        addBtn.textContent = T.editor.addItem;
        addBtn.addEventListener('click', () => { addRow(); list.dispatchEvent(new Event('input', { bubbles: true })); });
        listWrap.appendChild(addBtn);
        form.appendChild(listWrap);

    } else if (type === 'scatter') {
        const listWrap = document.createElement('div');
        listWrap.className = 'sn-modal-field';
        const lbl = document.createElement('label');
        lbl.className = 'sn-modal-label';
        lbl.textContent = T.editor.series;
        listWrap.appendChild(lbl);

        const list = document.createElement('div');
        list.className = 'chart-series-list';
        listWrap.appendChild(list);

        const addSeries = (name = '', points = '') => {
            const row = document.createElement('div');
            row.className = 'chart-series-row';

            const nameWrap = document.createElement('div');
            nameWrap.className = 'chart-series-name-wrap';
            const nameIcon = document.createElement('span');
            nameIcon.className = 'chart-series-name-icon chart-series-name-icon-hidden';
            nameIcon.setAttribute('aria-hidden', 'true');
            const nameInp = makeChartInput('Name', name, 'chart-series-name');
            nameWrap.appendChild(nameIcon);
            nameWrap.appendChild(nameInp);

            const ptsInp = makeChartInput('0,0 1,1 2,4', points, 'chart-series-values');

            const updateNameIcon = () => {
                const hasName = !!nameInp.value.trim();
                const hasData = !!ptsInp.value.trim();
                if (!hasData || hasName) {
                    nameIcon.className = 'chart-series-name-icon chart-series-name-icon-hidden';
                } else {
                    nameIcon.className = 'chart-series-name-icon chart-series-name-icon-warn';
                    nameIcon.innerHTML = WARN_ICON;
                }
            };
            nameInp.addEventListener('input', updateNameIcon);
            ptsInp.addEventListener('input', updateNameIcon);
            updateNameIcon();

            const rm = document.createElement('button');
            rm.type = 'button';
            rm.className = 'chart-series-remove';
            rm.textContent = '×';
            rm.addEventListener('click', () => { if (list.children.length > 1) row.remove(); });
            row.appendChild(nameWrap);
            row.appendChild(ptsInp);
            row.appendChild(rm);
            list.appendChild(row);
        };
        const scatterDefaults = initial?.series?.length ? initial.series : [{ name: '', values: '' }];
        scatterDefaults.forEach(s => addSeries(s.name, s.values));

        const addBtn = document.createElement('button');
        addBtn.type = 'button';
        addBtn.className = 'chart-add-btn';
        addBtn.textContent = '+ Add Series';
        addBtn.addEventListener('click', () => { addSeries(); list.dispatchEvent(new Event('input', { bubbles: true })); });
        listWrap.appendChild(addBtn);
        form.appendChild(listWrap);

    } else {
        form.appendChild(buildChartModalField(
            'Labels',
            makeChartInput('Jan, Feb, Mar, Apr', initial?.labels ?? '', 'chart-labels-input'),
        ));

        const listWrap = document.createElement('div');
        listWrap.className = 'sn-modal-field';
        const lbl = document.createElement('label');
        lbl.className = 'sn-modal-label';
        lbl.textContent = T.editor.series;
        listWrap.appendChild(lbl);

        const list = document.createElement('div');
        list.className = 'chart-series-list';
        listWrap.appendChild(list);

        const iconUpdaters: Array<() => void> = [];

        const addSeries = (name = '', values = '') => {
            const row = document.createElement('div');
            row.className = 'chart-series-row';

            const nameWrap = document.createElement('div');
            nameWrap.className = 'chart-series-name-wrap';
            const nameIcon = document.createElement('span');
            nameIcon.className = 'chart-series-name-icon chart-series-name-icon-hidden';
            nameIcon.setAttribute('aria-hidden', 'true');
            const nameInp = makeChartInput('Series name', name, 'chart-series-name');
            nameWrap.appendChild(nameIcon);
            nameWrap.appendChild(nameInp);

            const valWrap = document.createElement('div');
            valWrap.className = 'chart-series-val-wrap';
            const icon = document.createElement('span');
            icon.className = 'chart-series-val-icon chart-series-val-icon-hidden';
            icon.setAttribute('aria-hidden', 'true');
            const valInp = makeChartInput('Values (comma-separated)', values, 'chart-series-values');
            valWrap.appendChild(icon);
            valWrap.appendChild(valInp);

            const updateIcon = () => {
                const hasName = !!nameInp.value.trim();
                const hasVals = !!valInp.value.trim();

                // Name icon: warn when vals has content but name is empty
                if (!hasVals || hasName) {
                    nameIcon.className = 'chart-series-name-icon chart-series-name-icon-hidden';
                } else {
                    nameIcon.className = 'chart-series-name-icon chart-series-name-icon-warn';
                    nameIcon.innerHTML = WARN_ICON;
                }

                // Val icon: hidden when no name; ok/warn by label count match
                if (!hasName) {
                    icon.className = 'chart-series-val-icon chart-series-val-icon-hidden';
                    return;
                }
                const labelsEl = form.querySelector<HTMLInputElement>('.chart-labels-input');
                const labelCount = labelsEl ? csvCount(labelsEl.value) : 0;
                if (!labelCount) {
                    icon.className = 'chart-series-val-icon chart-series-val-icon-hidden';
                    return;
                }
                if (csvCount(valInp.value) === labelCount) {
                    icon.className = 'chart-series-val-icon chart-series-val-icon-ok';
                    icon.innerHTML = CHECK_ICON;
                } else {
                    icon.className = 'chart-series-val-icon chart-series-val-icon-warn';
                    icon.innerHTML = WARN_ICON;
                }
            };

            iconUpdaters.push(updateIcon);
            nameInp.addEventListener('input', updateIcon);
            valInp.addEventListener('input', updateIcon);
            updateIcon();

            const rm = document.createElement('button');
            rm.type = 'button';
            rm.className = 'chart-series-remove';
            rm.textContent = '×';
            rm.addEventListener('click', () => {
                if (list.children.length > 1) {
                    const idx = iconUpdaters.indexOf(updateIcon);
                    if (idx !== -1) iconUpdaters.splice(idx, 1);
                    row.remove();
                }
            });
            row.appendChild(nameWrap);
            row.appendChild(valWrap);
            row.appendChild(rm);
            list.appendChild(row);
        };
        const seriesDefaults = initial?.series?.length ? initial.series : [{ name: '', values: '' }];
        seriesDefaults.forEach(s => addSeries(s.name, s.values));

        const labelsInp = form.querySelector<HTMLInputElement>('.chart-labels-input');
        if (labelsInp) labelsInp.addEventListener('input', () => iconUpdaters.forEach(fn => fn()));

        const addBtn = document.createElement('button');
        addBtn.type = 'button';
        addBtn.className = 'chart-add-btn';
        addBtn.textContent = '+ Add Series';
        addBtn.addEventListener('click', () => { addSeries(); list.dispatchEvent(new Event('input', { bubbles: true })); });
        listWrap.appendChild(addBtn);
        form.appendChild(listWrap);
    }
}

function csvCount(s: string): number {
    return s.split(',').filter(t => t.trim()).length;
}

function isChartFormValid(type: string, form: HTMLElement): boolean {
    if (type === 'pie' || type === 'donut') {
        const rows = form.querySelectorAll<HTMLElement>('.chart-series-row');
        for (const row of rows) {
            const name = (row.querySelector('.chart-series-name') as HTMLInputElement | null)?.value.trim() ?? '';
            const val  = (row.querySelector('.chart-series-values') as HTMLInputElement | null)?.value.trim() ?? '';
            if (!name && !val) continue;
            if (!name) return false;
            if (name && val) return true;
        }
        return false;
    } else if (type === 'scatter') {
        const rows = form.querySelectorAll<HTMLElement>('.chart-series-row');
        for (const row of rows) {
            const name = (row.querySelector('.chart-series-name') as HTMLInputElement | null)?.value.trim() ?? '';
            const pts  = (row.querySelector('.chart-series-values') as HTMLInputElement | null)?.value.trim() ?? '';
            if (!name && !pts) continue;
            if (!name) return false;
            if (name && pts) return true;
        }
        return false;
    } else {
        const labels = (form.querySelector('.chart-labels-input') as HTMLInputElement | null)?.value.trim();
        if (!labels) return false;
        const labelCount = csvCount(labels);
        let hasValid = false;
        for (const row of form.querySelectorAll<HTMLElement>('.chart-series-row')) {
            const name = (row.querySelector('.chart-series-name') as HTMLInputElement | null)?.value.trim() ?? '';
            const vals = (row.querySelector('.chart-series-values') as HTMLInputElement | null)?.value.trim() ?? '';
            if (!name && !vals) continue;
            if (!name) return false;
            if (!vals || csvCount(vals) !== labelCount) return false;
            hasValid = true;
        }
        return hasValid;
    }
}

function serializeChartConfig(type: string, form: HTMLElement): string {
    const title = (form.querySelector('.chart-title-input') as HTMLInputElement | null)?.value.trim() || '';
    const lines: string[] = [`type: ${type}`];
    if (title) lines.push(`title: ${title}`);

    if (type === 'pie' || type === 'donut') {
        const rows = form.querySelectorAll<HTMLElement>('.chart-series-row');
        if (!rows.length) return '';
        lines.push('data:');
        rows.forEach(row => {
            const name = (row.querySelector('.chart-series-name') as HTMLInputElement | null)?.value.trim();
            const val  = (row.querySelector('.chart-series-values') as HTMLInputElement | null)?.value.trim();
            if (name && val) lines.push(`  ${name}: ${val}`);
        });
    } else if (type === 'scatter') {
        const activeRows = Array.from(form.querySelectorAll<HTMLElement>('.chart-series-row')).filter(row => {
            const name = (row.querySelector('.chart-series-name') as HTMLInputElement | null)?.value.trim();
            const pts  = (row.querySelector('.chart-series-values') as HTMLInputElement | null)?.value.trim();
            return !!(name || pts);
        });
        if (!activeRows.length) return '';
        lines.push('series:');
        activeRows.forEach(row => {
            const name = (row.querySelector('.chart-series-name') as HTMLInputElement | null)?.value.trim();
            const pts  = (row.querySelector('.chart-series-values') as HTMLInputElement | null)?.value.trim() || '';
            lines.push(`  - name: ${name || 'Series'}`);
            const pairs = pts.split(/\s+/).map(p => {
                const parts = p.split(',');
                const x = parseFloat(parts[0] ?? '0');
                const y = parseFloat(parts[1] ?? '0');
                return isNaN(x) || isNaN(y) ? null : `[${x},${y}]`;
            }).filter(Boolean);
            lines.push(`    data: [${pairs.join(',')}]`);
        });
    } else {
        const labels = (form.querySelector('.chart-labels-input') as HTMLInputElement | null)?.value.trim() || '';
        if (labels) lines.push(`labels: [${labels}]`);
        const activeRows = Array.from(form.querySelectorAll<HTMLElement>('.chart-series-row')).filter(row => {
            const name = (row.querySelector('.chart-series-name') as HTMLInputElement | null)?.value.trim();
            const vals = (row.querySelector('.chart-series-values') as HTMLInputElement | null)?.value.trim();
            return !!(name || vals);
        });
        if (!activeRows.length) return '';
        lines.push('series:');
        activeRows.forEach(row => {
            const name = (row.querySelector('.chart-series-name') as HTMLInputElement | null)?.value.trim();
            const vals = (row.querySelector('.chart-series-values') as HTMLInputElement | null)?.value.trim();
            lines.push(`  - name: ${name || 'Series'}`);
            lines.push(`    data: [${vals || '0'}]`);
        });
    }

    return lines.join('\n');
}

function placeCursorAfterBlock(editor: Editor): void {
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const { state } = view;
        const { $from } = state.selection;

        let blockEnd: number | null = null;
        for (let d = $from.depth; d >= 0; d--) {
            const name = $from.node(d).type.name;
            if (name === 'code_block' || name === 'table' || name === 'image') {
                blockEnd = $from.after(d);
                break;
            }
        }
        if (blockEnd === null) return;

        let tr = state.tr;
        const nodeAfter = blockEnd <= state.doc.content.size ? state.doc.nodeAt(blockEnd) : null;
        if (!nodeAfter || nodeAfter.type.name !== 'paragraph') {
            tr = tr.insert(blockEnd, state.schema.nodes.paragraph!.create());
        }
        tr = tr.setSelection(TextSelection.create(tr.doc, blockEnd + 1));
        view.dispatch(tr);
        view.focus();
    });
}

function insertChartBlock(editor: Editor, configStr: string): void {
    editor.action(callCommand(createCodeBlockCommand.key));
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const { state } = view;
        const { $from } = state.selection;
        for (let d = $from.depth; d >= 0; d--) {
            if ($from.node(d).type.name === 'code_block') {
                const nodePos = $from.before(d);
                const node = $from.node(d);
                let tr = state.tr.setNodeAttribute(nodePos, 'language', 'chart');
                tr = tr.replaceWith(
                    nodePos + 1,
                    nodePos + 1 + node.content.size,
                    state.schema.text(configStr),
                );
                view.dispatch(tr);
                break;
            }
        }
    });
    placeCursorAfterBlock(editor);
}

function insertCallout(editor: Editor, type: string): void {
    editor.action(callCommand(createCodeBlockCommand.key));
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const { state } = view;
        const { $from } = state.selection;
        for (let d = $from.depth; d >= 0; d--) {
            if ($from.node(d).type.name === 'code_block') {
                const pos = $from.before(d);
                view.dispatch(state.tr.setNodeAttribute(pos, 'language', `callout-${type}`));
                break;
            }
        }
    });
}

interface ChartModalOptions {
    onConfirm: (config: string) => void;
    initialConfig?: string;
    isEdit?: boolean;
}

function showChartEditorModal(type: string, options: ChartModalOptions): void {
    closeChartModal();
    closeLinkDialog();
    closeImageModal();
    closeCodeModal();

    const typeLabels: Record<string, string> = {
        bar: 'Bar', line: 'Line', area: 'Area', stacked: 'Stacked Bar',
        pie: 'Pie', donut: 'Donut', scatter: 'Scatter',
    };

    const initial = options.initialConfig ? parseChartFormData(options.initialConfig) : undefined;

    const backdrop = document.createElement('div');
    backdrop.className = 'sn-modal-backdrop';

    const modal = document.createElement('div');
    modal.className = 'sn-modal';
    backdrop.appendChild(modal);

    // Header
    const header = document.createElement('div');
    header.className = 'sn-modal-header';
    const titleSpan = document.createElement('span');
    titleSpan.textContent = `${options.isEdit ? 'Edit' : 'Insert'} ${typeLabels[type] ?? type} Chart`;
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'sn-modal-close';
    closeBtn.textContent = '×';
    const chartHeaderActions = document.createElement('div');
    chartHeaderActions.className = 'sn-modal-header-actions';
    chartHeaderActions.append(createModalExpandBtn(modal), closeBtn);
    header.append(titleSpan, chartHeaderActions);
    modal.appendChild(header);

    // Body
    const body = document.createElement('div');
    body.className = 'sn-modal-body';

    const form = document.createElement('div');
    form.className = 'sn-modal-form';
    buildChartForm(type, form, initial);
    body.appendChild(form);

    const previewPane = document.createElement('div');
    previewPane.className = 'sn-modal-preview';
    const canvas = document.createElement('canvas');
    previewPane.appendChild(canvas);
    body.appendChild(previewPane);

    modal.appendChild(body);

    // Footer
    const footer = document.createElement('div');
    footer.className = 'sn-modal-footer';
    const cancelBtn = document.createElement('button');
    cancelBtn.type = 'button';
    cancelBtn.className = 'sn-modal-cancel';
    cancelBtn.textContent = T.common.cancel;
    const insertBtn = document.createElement('button');
    insertBtn.type = 'button';
    insertBtn.className = 'sn-modal-insert';
    insertBtn.textContent = options.isEdit ? T.editor.updateChart : T.editor.insertChart;
    footer.appendChild(cancelBtn);
    footer.appendChild(insertBtn);
    modal.appendChild(footer);

    document.body.appendChild(backdrop);
    activeChartModal = backdrop;
    lockBodyScroll();

    const close = () => {
        (window as any).SnakkCharts?.destroyCanvas?.(canvas);
        closeChartModal();
        document.removeEventListener('keydown', onKey);
    };
    const confirmClose = () => {
        if (userHasEdited && !window.confirm('Discard changes?')) return;
        close();
    };
    closeBtn.addEventListener('click', confirmClose);
    cancelBtn.addEventListener('click', confirmClose);
    backdrop.addEventListener('mousedown', (e) => { if (e.target === backdrop) confirmClose(); });
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') confirmClose(); };
    document.addEventListener('keydown', onKey);

    // Insert button validity
    const updateInsertState = () => {
        insertBtn.disabled = !isChartFormValid(type, form);
    };
    updateInsertState();
    form.addEventListener('input', updateInsertState);
    form.addEventListener('click', () => setTimeout(updateInsertState, 0));

    // Live preview
    const exampleConfigs: Record<string, string> = {
        bar:     'type: bar\nlabels: [Jan, Feb, Mar, Apr]\nseries:\n  - name: Sales\n    data: [30, 50, 40, 60]',
        line:    'type: line\nlabels: [Jan, Feb, Mar, Apr]\nseries:\n  - name: Revenue\n    data: [20, 35, 30, 50]',
        area:    'type: area\nlabels: [Jan, Feb, Mar, Apr]\nseries:\n  - name: Traffic\n    data: [100, 150, 120, 180]',
        stacked: 'type: stacked\nlabels: [Q1, Q2, Q3, Q4]\nseries:\n  - name: A\n    data: [20, 30, 25, 40]\n  - name: B\n    data: [15, 25, 20, 30]',
        pie:     'type: pie\ndata:\n  Alpha: 40\n  Beta: 35\n  Gamma: 25',
        donut:   'type: donut\ndata:\n  Alpha: 40\n  Beta: 35\n  Gamma: 25',
        scatter: 'type: scatter\nseries:\n  - name: Group A\n    data: [[1,2],[3,4],[5,6],[7,3]]',
    };
    let userHasEdited = !!options.isEdit;
    const renderPreview = () => {
        const config = userHasEdited ? serializeChartConfig(type, form) : (exampleConfigs[type] ?? '');
        (window as any).SnakkCharts?.destroyCanvas?.(canvas);
        if (config) (window as any).SnakkCharts?.renderIntoCanvas?.(canvas, config);
    };
    let previewTimer: ReturnType<typeof setTimeout> | null = null;
    const updatePreview = () => {
        if (previewTimer) clearTimeout(previewTimer);
        previewTimer = setTimeout(renderPreview, 400);
    };
    form.addEventListener('input', () => { userHasEdited = true; updatePreview(); });
    setTimeout(renderPreview, 100);

    // Confirm
    insertBtn.addEventListener('click', () => {
        const config = serializeChartConfig(type, form);
        if (!config) return;
        close();
        options.onConfirm(config);
    });

    // Focus first input
    setTimeout(() => (form.querySelector('input') as HTMLInputElement | null)?.focus(), 50);
}


// ============================================================================
// Image File Picker
// ============================================================================

// ============================================================================
// Image Modal
// ============================================================================

const MAX_POST_IMAGES = 12;
const MAX_FILE_BYTES = 5 * 1024 * 1024; // 5 MB (matches BFF limit)

interface ModalItem {
    id: number;
    blobUrl: string;   // blob: URL for new files; CDN URL for existing images
    file: File | null; // null = already uploaded
    alt: string;
    oversized?: boolean;
}

interface ImageModalOptions {
    editor?: Editor;
    view?: import('@milkdown/kit/prose/view').EditorView;
    data?: ImageModalData;
    onConfirm?: (data: ImageModalData) => void;
    pendingFiles?: Map<string, File>;
    maxImages?: number;
    initialFiles?: File[];
    hideLayout?: boolean;
    externalImageCount?: number;
}

let activeImageModal: HTMLElement | null = null;
let imageModalEscHandler: ((e: KeyboardEvent) => void) | null = null;

function closeImageModal(): void {
    if (activeImageModal) {
        activeImageModal.remove();
        activeImageModal = null;
        unlockBodyScroll();
    }
    if (imageModalEscHandler) {
        document.removeEventListener('keydown', imageModalEscHandler);
        imageModalEscHandler = null;
    }
}

// ============================================================================
// Code Block Modal
// ============================================================================

interface CodeModalOptions {
    editor?: Editor;
    data?: CodeModalData;
    onConfirm?: (d: CodeModalData) => void;
}

let activeCodeModal: HTMLElement | null = null;

function closeCodeModal(): void {
    if (activeCodeModal) {
        activeCodeModal.remove();
        activeCodeModal = null;
        unlockBodyScroll();
    }
}

function insertCodeBlock(editor: Editor, data: CodeModalData): void {
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const schema = view.state.schema;
        const codeBlock = schema.nodes.code_block!.create(
            { language: data.language || null },
            data.code ? [schema.text(data.code)] : undefined
        );
        const para = schema.nodes.paragraph!.create();
        const { tr, selection } = view.state;
        const $from = selection.$from;
        const depth = $from.depth;
        let insertTr = tr;
        let paraPos: number;
        if (depth > 0 && $from.parent.content.size === 0) {
            const blockStart = $from.before(depth);
            const blockEnd = $from.after(depth);
            insertTr = insertTr.replaceWith(blockStart, blockEnd, Fragment.from([codeBlock, para]));
            paraPos = blockStart + codeBlock.nodeSize + 1;
        } else {
            const blockEnd = depth > 0 ? $from.after(depth) : selection.from;
            insertTr = insertTr.insert(blockEnd, Fragment.from([codeBlock, para]));
            paraPos = blockEnd + codeBlock.nodeSize + 1;
        }
        insertTr = insertTr.setSelection(TextSelection.create(insertTr.doc, paraPos));
        view.dispatch(insertTr);
        view.focus();
    });
}

function showCodeModal(options: CodeModalOptions): void {
    closeCodeModal();
    closeLinkDialog();
    closeGroupDropdown();
    closeChartModal();
    closeImageModal();
    closeTableModal();

    const { editor, data, onConfirm } = options;
    const isEdit = !!data;

    let currentLang = data?.language ?? '';

    const backdrop = document.createElement('div');
    backdrop.className = 'sn-modal-backdrop';

    const modal = document.createElement('div');
    modal.className = 'sn-modal';
    modal.style.maxWidth = '48rem';

    const header = document.createElement('div');
    header.className = 'sn-modal-header';
    const title = document.createElement('span');
    title.textContent = isEdit ? 'Edit Code' : 'Insert Code';
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'sn-modal-close';
    closeBtn.innerHTML = '&times;';
    closeBtn.addEventListener('click', closeCodeModal);
    const codeHeaderActions = document.createElement('div');
    codeHeaderActions.className = 'sn-modal-header-actions';
    codeHeaderActions.append(createModalExpandBtn(modal), closeBtn);
    header.append(title, codeHeaderActions);

    const body = document.createElement('div');
    body.className = 'sn-modal-body';
    body.style.flexDirection = 'column';

    const langRow = document.createElement('div');
    langRow.className = 'code-modal-lang-row';

    const textarea = document.createElement('textarea');
    textarea.value = data?.code ?? '';
    textarea.spellcheck = false;
    (textarea as any).autocomplete = 'off';

    const pre = document.createElement('pre');
    const codeEl = document.createElement('code');
    pre.appendChild(codeEl);

    const langBtns: HTMLButtonElement[] = [];
    CODE_LANGUAGES
        .filter(l => l.value !== 'chart')
        .forEach(l => {
            const btn = document.createElement('button');
            btn.type = 'button';
            const iconData = (window as any).snakkLangIcons?.[l.value];
            if (iconData) {
                const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
                const hex = iconData.hex as string;
                const r = parseInt(hex.slice(0, 2), 16);
                const g = parseInt(hex.slice(2, 4), 16);
                const b = parseInt(hex.slice(4, 6), 16);
                const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
                const fill = isDark && luminance < 0.25 ? '#ffffff' : `#${hex}`;
                btn.innerHTML = `<svg viewBox="${iconData.viewBox}" width="14" height="14" aria-hidden="true" style="fill:${fill};flex-shrink:0">${iconData.inner}</svg><span>${l.label}</span>`;
            } else {
                btn.textContent = l.label;
            }
            if (l.value === currentLang) btn.classList.add('active');
            btn.addEventListener('click', () => {
                currentLang = l.value;
                langBtns.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                rehighlight();
                textarea.focus();
            });
            langRow.appendChild(btn);
            langBtns.push(btn);
        });

    const gutter = document.createElement('div');
    gutter.className = 'code-modal-gutter';
    gutter.setAttribute('aria-hidden', 'true');

    const syncGutter = (code: string): void => {
        const lineCount = code.split('\n').length;
        while (gutter.childElementCount < lineCount) {
            gutter.appendChild(document.createElement('span'));
        }
        while (gutter.childElementCount > lineCount) {
            gutter.lastChild!.remove();
        }
        gutter.scrollTop = textarea.scrollTop;
    };

    const rehighlight = (): void => {
        const code = textarea.value;
        const grammar = (window as any).Prism?.languages?.[currentLang];
        if (grammar && (window as any).Prism) {
            codeEl.innerHTML = (window as any).Prism.highlight(code, grammar, currentLang);
        } else {
            codeEl.textContent = code;
        }
        codeEl.className = currentLang ? `language-${currentLang}` : '';
        pre.scrollTop = textarea.scrollTop;
        pre.scrollLeft = textarea.scrollLeft;
        syncGutter(code);
    };

    textarea.addEventListener('input', rehighlight);
    textarea.addEventListener('scroll', () => {
        pre.scrollTop = textarea.scrollTop;
        pre.scrollLeft = textarea.scrollLeft;
        gutter.scrollTop = textarea.scrollTop;
    });
    textarea.addEventListener('keydown', (e) => {
        if (e.key === 'Tab') {
            e.preventDefault();
            const start = textarea.selectionStart;
            const end = textarea.selectionEnd;
            textarea.value = textarea.value.slice(0, start) + '    ' + textarea.value.slice(end);
            textarea.selectionStart = textarea.selectionEnd = start + 4;
            rehighlight();
        }
    });

    const innerArea = document.createElement('div');
    innerArea.className = 'code-modal-inner';
    innerArea.append(pre, textarea);

    const editorContainer = document.createElement('div');
    editorContainer.className = 'code-modal-editor';
    editorContainer.append(gutter, innerArea);
    body.append(langRow, editorContainer);

    const footer = document.createElement('div');
    footer.className = 'sn-modal-footer';
    const cancelBtn = document.createElement('button');
    cancelBtn.type = 'button';
    cancelBtn.className = 'sn-modal-cancel';
    cancelBtn.textContent = T.common.cancel;
    cancelBtn.addEventListener('click', closeCodeModal);
    const insertBtn = document.createElement('button');
    insertBtn.type = 'button';
    insertBtn.className = 'sn-modal-insert';
    insertBtn.textContent = isEdit ? 'Update' : 'Insert';
    insertBtn.disabled = textarea.value.trim() === '';
    textarea.addEventListener('input', () => {
        insertBtn.disabled = textarea.value.trim() === '';
    });
    insertBtn.addEventListener('click', () => {
        const finalData: CodeModalData = { language: currentLang, code: textarea.value };
        if (onConfirm) {
            onConfirm(finalData);
        } else if (editor) {
            insertCodeBlock(editor, finalData);
        }
        closeCodeModal();
    });
    footer.append(cancelBtn, insertBtn);

    modal.append(header, body, footer);
    backdrop.appendChild(modal);
    document.body.appendChild(backdrop);
    activeCodeModal = backdrop;
    lockBodyScroll();

    backdrop.addEventListener('mousedown', (e) => {
        if (e.target === backdrop) closeCodeModal();
    });

    const onKey = (e: KeyboardEvent) => {
        if (e.key === 'Escape') {
            closeCodeModal();
            document.removeEventListener('keydown', onKey);
        }
    };
    document.addEventListener('keydown', onKey);

    rehighlight();
    // If Prism wasn't ready on first render, re-highlight once it loads
    (window as any).SnakkSyntax?.loadPrism?.().then(() => rehighlight());
    requestAnimationFrame(() => textarea.focus());
}


function uploadFile(file: File, onProgress: (pct: number) => void): Promise<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return window.SnakkUpload.uploadWithProgress<{ url: string }>({
        url: '/bff/media/upload',
        formData,
        onProgress,
        onProcessing: () => onProgress(99),
    }).then((result: { ok: boolean; status: number; data?: { url: string }; error?: string }) => {
        if (!result.ok || !result.data?.url) {
            throw new Error(result.error ?? `Upload failed (${result.status})`);
        }
        return { url: result.data.url };
    });
}

// Maps each editor's Milkdown Editor instance to its pending-upload map (blobUrl → File).
// Populated when images are inserted; drained by flushUploads() at submit time.
const editorPendingUploads = new WeakMap<Editor, Map<string, File>>();

function insertImageGroup(editor: Editor, data: ImageModalData): void {
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const schema = view.state.schema;
        const content = data.images.map(i => `${i.src}|${i.alt}`).join('\n');
        const layout = data.images.length === 1 ? 'single' : (data.layout === 'single' ? 'grid' : data.layout);
        const codeBlock = schema.nodes.code_block!.create(
            { language: `image-group layout=${layout}` },
            content ? [schema.text(content)] : undefined
        );
        const para = schema.nodes.paragraph!.create();
        const { tr, selection } = view.state;
        const $from = selection.$from;
        const depth = $from.depth;
        let insertTr = tr;
        let paraPos: number;
        if (depth > 0 && $from.parent.content.size === 0) {
            const blockStart = $from.before(depth);
            const blockEnd = $from.after(depth);
            insertTr = insertTr.replaceWith(blockStart, blockEnd, Fragment.from([codeBlock, para]));
            paraPos = blockStart + codeBlock.nodeSize + 1;
        } else {
            const blockEnd = depth > 0 ? $from.after(depth) : selection.from;
            insertTr = insertTr.insert(blockEnd, Fragment.from([codeBlock, para]));
            paraPos = blockEnd + codeBlock.nodeSize + 1;
        }
        insertTr = insertTr.setSelection(TextSelection.create(insertTr.doc, paraPos));
        view.dispatch(insertTr);
        view.focus();
    });
}

function renderLayoutPreview(
    container: HTMLElement,
    images: Array<{ url: string; alt: string }>,
    layout: string
): void {
    container.innerHTML = '';
    if (images.length === 0) return;

    function buildDisplayCell(img: { url: string; alt: string }): HTMLDivElement {
        const cell = document.createElement('div');
        cell.className = 'images-upload-item images-item-loaded';
        cell.style.backgroundImage = `url("${img.url}")`;
        const imgEl = document.createElement('img');
        imgEl.src = img.url;
        imgEl.alt = img.alt;
        cell.appendChild(imgEl);
        return cell;
    }

    if (layout === 'carousel') {
        const carousel = document.createElement('div');
        carousel.className = 'gup-carousel';
        const track = document.createElement('div');
        track.className = 'gup-carousel-track';
        images.forEach(img => track.appendChild(buildDisplayCell(img)));
        const prevBtn = document.createElement('button');
        prevBtn.type = 'button';
        prevBtn.className = 'gup-carousel-arrow gup-carousel-prev';
        prevBtn.innerHTML = '&#8249;';
        const nextBtn = document.createElement('button');
        nextBtn.type = 'button';
        nextBtn.className = 'gup-carousel-arrow gup-carousel-next';
        nextBtn.innerHTML = '&#8250;';
        const counter = document.createElement('div');
        counter.className = 'gup-carousel-counter';
        let slide = 0;
        const updateSlide = () => {
            track.style.transform = `translateX(-${slide * 100}%)`;
            counter.textContent = `${slide + 1} / ${images.length}`;
            prevBtn.disabled = slide === 0;
            nextBtn.disabled = slide === images.length - 1;
        };
        prevBtn.addEventListener('click', () => { slide--; updateSlide(); });
        nextBtn.addEventListener('click', () => { slide++; updateSlide(); });
        const _utils1 = (window as any).SnakkUtils;
        if (_utils1?.addCarouselSwipe) {
            _utils1.addCarouselSwipe(track, (idx: number) => {
                slide = Math.max(0, Math.min(images.length - 1, idx));
                updateSlide();
            }, () => slide);
        }
        carousel.appendChild(track);
        carousel.appendChild(prevBtn);
        carousel.appendChild(nextBtn);
        carousel.appendChild(counter);
        updateSlide();
        container.appendChild(carousel);
    } else if (layout === 'hero') {
        const hero = document.createElement('div');
        hero.className = 'gup-hero';
        const main = document.createElement('div');
        main.className = 'gup-hero-main';
        main.appendChild(buildDisplayCell(images[0]!));
        hero.appendChild(main);
        if (images.length >= 2) {
            const heroGrid = document.createElement('div');
            heroGrid.className = 'gup-hero-grid';
            images.slice(1).forEach(img => heroGrid.appendChild(buildDisplayCell(img)));
            hero.appendChild(heroGrid);
        }
        container.appendChild(hero);
    } else if (layout === 'compare') {
        const compare = document.createElement('div');
        compare.className = 'gup-compare';
        const before = buildDisplayCell(images[0]!);
        before.classList.add('gup-compare-before');
        const after = buildDisplayCell(images[1]!);
        after.classList.add('gup-compare-after');
        compare.appendChild(before);
        compare.appendChild(after);
        container.appendChild(compare);
    } else {
        const wrap = document.createElement('div');
        wrap.className = `gup-${layout}`;
        images.forEach(img => wrap.appendChild(buildDisplayCell(img)));
        container.appendChild(wrap);
    }
}

// ============================================================================
// Image Picker Widget (reusable — used inline on gallery create and inside modal)
// ============================================================================

interface ImagePickerWidgetOptions {
    pendingFiles?: Map<string, File>;
    initialData?: { images: Array<{ src: string; alt: string }>; layout: string };
    initialFiles?: File[];
    maxImages?: number;
    getExternalCount?: () => number;
    hideLayout?: boolean;
    onChange?: (items: ImagePickerItem[], layout: string) => void;
}

interface ImagePickerWidgetController {
    getItems(): ImagePickerItem[];
    getLayout(): string;
    addFiles(files: File[]): void;
    destroy(): void;
}

interface ImagePickerItem {
    blobUrl: string;
    file: File | null;
    alt: string;
    oversized: boolean;
}

const THUMB_MAX_PX = 600;

async function generateThumbnail(file: File): Promise<string> {
    // Probe natural dimensions off the main thread
    const probe = await createImageBitmap(file);
    const { width, height } = probe;
    probe.close();

    const scale = Math.min(1, THUMB_MAX_PX / Math.max(width, height));
    const w = Math.round(width * scale);
    const h = Math.round(height * scale);

    // Decode + resize off the main thread — avoids the expensive ctx.drawImage
    // scaling that blocks the main thread for 1-2s per large image
    const bmp = await createImageBitmap(file, { resizeWidth: w, resizeHeight: h, resizeQuality: 'medium' });

    // OffscreenCanvas.convertToBlob encodes JPEG off the main thread (Chrome/FF/Safari 17+)
    if (typeof OffscreenCanvas !== 'undefined') {
        const oc = new OffscreenCanvas(w, h);
        oc.getContext('2d')!.drawImage(bmp, 0, 0);
        bmp.close();
        const blob = await oc.convertToBlob({ type: 'image/jpeg', quality: 0.85 });
        return URL.createObjectURL(blob);
    }

    // Fallback: drawImage is now just a fast pixel-copy of the already-small bitmap
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    canvas.getContext('2d')!.drawImage(bmp, 0, 0);
    bmp.close();
    return new Promise<string>((resolve, reject) => {
        canvas.toBlob(blob => {
            if (blob) resolve(URL.createObjectURL(blob));
            else reject(new Error('toBlob failed'));
        }, 'image/jpeg', 0.85);
    });
}

function createImagePickerWidget(
    container: HTMLElement,
    options: ImagePickerWidgetOptions
): ImagePickerWidgetController {
    const cap = options.maxImages ?? MAX_POST_IMAGES;
    let nextItemId = 0;
    const modalItems: ModalItem[] = (options.initialData?.images ?? []).map(img => ({
        id: nextItemId++,
        blobUrl: img.src,
        file: null,
        alt: img.alt,
    }));
    let modalLayout: ImageModalData['layout'] = (options.initialData?.layout as ImageModalData['layout']) ?? 'masonry';
    let carouselSlide = 0;
    let dragSrcIdx: number | null = null;
    let lastRejections: string[] = [];

    const LAYOUTS_MODAL: Array<{ value: ImageModalData['layout']; label: string; glp: string }> = [
        { value: 'grid',      label: 'Grid',      glp: '<div class="glp glp-grid"><div></div><div></div><div></div><div></div></div>' },
        { value: 'masonry',   label: 'Masonry',   glp: '<div class="glp glp-masonry"><div></div><div></div><div></div></div>' },
        { value: 'justified', label: 'Justified', glp: '<div class="glp glp-justified"><div></div><div></div><div></div></div>' },
        { value: 'carousel',  label: 'Carousel',  glp: '<div class="glp glp-carousel"><span>‹</span><div></div><span>›</span></div>' },
        { value: 'hero',      label: 'Hero',      glp: '<div class="glp glp-hero"><div class="glp-hero-big"></div><div class="glp-hero-row"><div></div><div></div><div></div></div></div>' },
        { value: 'compare',   label: 'Compare',   glp: '<div class="glp glp-compare"><div></div><div class="glp-compare-line"></div><div></div></div>' },
    ];

    // ── Drop zone ────────────────────────────────────────────────────────────
    const dropZone = document.createElement('div');
    dropZone.className = 'image-modal-drop-zone';
    const dropHint = document.createElement('p');
    const chooseBtn = document.createElement('button');
    chooseBtn.type = 'button';
    chooseBtn.className = 'image-modal-choose';
    chooseBtn.textContent = T.editor.chooseFiles;
    dropZone.appendChild(dropHint);
    dropZone.appendChild(chooseBtn);

    // ── Rejections ───────────────────────────────────────────────────────────
    const rejectionsEl = document.createElement('div');
    rejectionsEl.className = 'image-modal-rejections';

    // ── Layout row ───────────────────────────────────────────────────────────
    const layoutRow = document.createElement('div');
    layoutRow.className = 'image-modal-layout-row';
    const layoutLabel = document.createElement('div');
    layoutLabel.className = 'text-sm font-medium mb-2';
    layoutLabel.textContent = T.editor.layout;
    const layoutPicker = document.createElement('div');
    layoutPicker.className = 'flex flex-wrap gap-2';
    LAYOUTS_MODAL.forEach(({ value, label, glp }) => {
        const lbl = document.createElement('label');
        lbl.className = 'images-layout-option' + (value === modalLayout ? ' images-layout-active' : '');
        lbl.dataset.layout = value;
        const preview = document.createElement('div');
        preview.className = 'images-layout-preview';
        preview.innerHTML = glp;
        const caption = document.createElement('span');
        caption.className = 'text-xs mt-1';
        caption.textContent = label;
        lbl.appendChild(preview);
        lbl.appendChild(caption);
        lbl.addEventListener('click', () => {
            modalLayout = value;
            carouselSlide = 0;
            layoutPicker.querySelectorAll<HTMLElement>('.images-layout-option')
                .forEach(l => l.classList.toggle('images-layout-active', l.dataset.layout === value));
            renderGrid();
        });
        layoutPicker.appendChild(lbl);
    });
    layoutRow.appendChild(layoutLabel);
    layoutRow.appendChild(layoutPicker);

    // ── Grid ─────────────────────────────────────────────────────────────────
    const grid = document.createElement('div');
    grid.className = 'image-modal-preview';

    container.style.display = 'flex';
    container.style.flexDirection = 'column';
    container.style.gap = '1rem';

    container.appendChild(dropZone);
    container.appendChild(rejectionsEl);
    if (!options.hideLayout) container.appendChild(layoutRow);
    container.appendChild(grid);

    // ── File input ───────────────────────────────────────────────────────────
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.accept = 'image/*';
    fileInput.multiple = true;
    fileInput.style.display = 'none';
    document.body.appendChild(fileInput);
    fileInput.addEventListener('change', () => {
        if (fileInput.files) addFiles(Array.from(fileInput.files));
        fileInput.value = '';
    });
    chooseBtn.addEventListener('click', () => fileInput.click());

    dropZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropZone.classList.add('dragover');
    });
    dropZone.addEventListener('dragleave', () => dropZone.classList.remove('dragover'));
    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropZone.classList.remove('dragover');
        if (e.dataTransfer?.files) addFiles(Array.from(e.dataTransfer.files));
    });

    function getEffectiveMax(): number {
        return cap - (options.getExternalCount?.() ?? 0);
    }

    function notifyChange(): void {
        options.onChange?.(
            modalItems.map(i => ({ blobUrl: i.blobUrl, file: i.file, alt: i.alt, oversized: !!i.oversized })),
            modalLayout
        );
    }

    // ── addFiles ─────────────────────────────────────────────────────────────
    function addFiles(files: File[]): void {
        lastRejections = [];
        const effectiveMax = getEffectiveMax();
        let budget = effectiveMax - modalItems.filter(i => !i.oversized).length;

        const toThumbnail: Array<{ item: ModalItem; file: File }> = [];

        for (const file of files) {
            if (!file.type.startsWith('image/')) {
                lastRejections.push(`${file.name} — not an image`);
                continue;
            }
            if (file.size > MAX_FILE_BYTES) {
                modalItems.push({ id: nextItemId++, blobUrl: URL.createObjectURL(file), file, alt: '', oversized: true });
                continue;
            }
            if (budget <= 0) {
                lastRejections.push(`${file.name} — post limit of ${cap} images reached`);
                continue;
            }
            const item: ModalItem = { id: nextItemId++, blobUrl: '', file, alt: '' };
            modalItems.push(item);
            toThumbnail.push({ item, file });
            budget--;
        }

        // Show placeholders immediately — thumbnails swap in as they decode.
        // Double rAF guarantees at least one frame is painted with skeletons visible
        // before thumbnail generation starts (setTimeout(0) is not reliable enough).
        renderGrid();

        requestAnimationFrame(() => requestAnimationFrame(() => { for (const { item, file } of toThumbnail) {
            generateThumbnail(file)
                .then(thumbUrl => {
                    if (!modalItems.includes(item)) { URL.revokeObjectURL(thumbUrl); return; }
                    item.blobUrl = thumbUrl;
                    options.pendingFiles?.set(thumbUrl, file);
                    const cell = grid.querySelector<HTMLElement>(`[data-img-id="${item.id}"]`);
                    if (cell) {
                        cell.style.backgroundImage = `url("${thumbUrl}")`;
                        cell.classList.remove('images-upload-item-loading', 'skeleton');
                        cell.classList.add('images-item-loaded');
                        const img = cell.querySelector('img');
                        if (img) img.src = thumbUrl;
                    }
                })
                .catch(() => {
                    // Fallback: use original file blob directly
                    if (!modalItems.includes(item)) return;
                    const blobUrl = URL.createObjectURL(file);
                    item.blobUrl = blobUrl;
                    options.pendingFiles?.set(blobUrl, file);
                    const cell = grid.querySelector<HTMLElement>(`[data-img-id="${item.id}"]`);
                    if (cell) {
                        cell.style.backgroundImage = `url("${blobUrl}")`;
                        cell.classList.remove('images-upload-item-loading', 'skeleton');
                        cell.classList.add('images-item-loaded');
                        const img = cell.querySelector('img');
                        if (img) img.src = blobUrl;
                    }
                });
        } }));
    }

    // ── Compare slider for picker context ────────────────────────────────────
    // Wires drag interaction on a freshly-built .gup-compare widget inside the
    // picker. Uses pointer capture on the slider bar so no document-level
    // listeners accumulate across renderGrid calls.
    function initPickerCompareSlider(widget: HTMLElement): void {
        const beforeEl = widget.querySelector<HTMLElement>('.gup-compare-before');
        const afterEl  = widget.querySelector<HTMLElement>('.gup-compare-after');
        const slider   = widget.querySelector<HTMLElement>('.sn-compare-slider');
        if (!beforeEl || !afterEl || !slider) return;

        function setPos(x: number): void {
            const rect = widget.getBoundingClientRect();
            const pct = Math.max(0, Math.min(1, (x - rect.left) / rect.width));
            beforeEl!.style.clipPath = `inset(0 ${(1 - pct) * 100}% 0 0)`;
            afterEl!.style.clipPath  = `inset(0 0 0 ${pct * 100}%)`;
            slider!.style.right      = `${(1 - pct) * 100}%`;
        }

        requestAnimationFrame(() => {
            const r = widget.getBoundingClientRect();
            if (r.width > 0) setPos(r.left + r.width * 0.5);
        });

        slider.addEventListener('pointerdown', (e) => {
            slider.setPointerCapture(e.pointerId);
            e.preventDefault();
            e.stopPropagation();
        });
        slider.addEventListener('pointermove', (e) => {
            if (slider.hasPointerCapture(e.pointerId)) setPos(e.clientX);
        });
    }

    // ── renderGrid ────────────────────────────────────────────────────────────
    function renderGrid(): void {
        const hasItems = modalItems.length > 0;
        const validItems = modalItems.filter(i => !i.oversized);
        const effectiveMax = getEffectiveMax();
        const atLimit = validItems.length >= effectiveMax;

        dropZone.classList.toggle('image-modal-drop-expanded', !hasItems);
        dropHint.textContent = atLimit ? `Post limit of ${cap} images reached` : 'Drop images here';
        chooseBtn.style.display = atLimit ? 'none' : '';

        if (!options.hideLayout) layoutRow.style.display = validItems.length >= 2 ? '' : 'none';

        // Compare: only with exactly 2 images
        const compareLabel = layoutPicker.querySelector<HTMLElement>('[data-layout="compare"]');
        if (compareLabel) {
            const showCompare = validItems.length === 2;
            compareLabel.style.display = showCompare ? '' : 'none';
            if (!showCompare && modalLayout === 'compare') {
                modalLayout = 'masonry';
                layoutPicker.querySelectorAll<HTMLElement>('.images-layout-option').forEach(l =>
                    l.classList.toggle('images-layout-active', l.dataset.layout === 'masonry')
                );
            }
        }

        // Hero: only with ≥ 4 images
        const heroLabel = layoutPicker.querySelector<HTMLElement>('[data-layout="hero"]');
        if (heroLabel) {
            const showHero = validItems.length >= 4;
            heroLabel.style.display = showHero ? '' : 'none';
            if (!showHero && modalLayout === 'hero') {
                modalLayout = 'masonry';
                layoutPicker.querySelectorAll<HTMLElement>('.images-layout-option').forEach(l =>
                    l.classList.toggle('images-layout-active', l.dataset.layout === 'masonry')
                );
            }
        }

        // Rebuild rejection chips
        rejectionsEl.innerHTML = '';
        lastRejections.forEach(msg => {
            const chip = document.createElement('div');
            chip.className = 'image-modal-rejection-chip';
            chip.textContent = `⚠ ${msg}`;
            rejectionsEl.appendChild(chip);
        });
        modalItems.forEach(item => {
            if (!item.oversized || !item.file) return;
            const mb = (item.file.size / (1024 * 1024)).toFixed(1);
            const chip = document.createElement('div');
            chip.className = 'image-modal-rejection-chip';
            chip.textContent = `⚠ ${item.file.name} — ${mb} MB exceeds the 5 MB limit`;
            rejectionsEl.appendChild(chip);
        });

        notifyChange();

        grid.innerHTML = '';
        if (!hasItems) return;

        function buildCell(item: ModalItem, idx: number): HTMLDivElement {
            const cell = document.createElement('div');
            const isLoading = !item.blobUrl;
            cell.className = `images-upload-item ${isLoading ? 'images-upload-item-loading skeleton' : 'images-item-loaded'} image-modal-item`;
            cell.dataset.imgId = String(item.id);
            if (!isLoading) cell.style.backgroundImage = `url("${item.blobUrl}")`;

            const img = document.createElement('img');
            if (item.blobUrl) img.src = item.blobUrl;
            img.alt = item.alt;
            cell.appendChild(img);

            if (item.oversized && item.file) {
                const mb = (item.file.size / (1024 * 1024)).toFixed(1);
                const overlay = document.createElement('div');
                overlay.className = 'image-modal-oversized-overlay';
                const label = document.createElement('span');
                label.className = 'image-modal-oversized-label';
                label.textContent = `${mb} MB — exceeds 5 MB limit`;
                const removeBtn = document.createElement('button');
                removeBtn.type = 'button';
                removeBtn.className = 'image-modal-oversized-remove';
                removeBtn.textContent = T.editor.remove;
                removeBtn.addEventListener('click', () => {
                    URL.revokeObjectURL(item.blobUrl);
                    modalItems.splice(idx, 1);
                    renderGrid();
                });
                overlay.appendChild(label);
                overlay.appendChild(removeBtn);
                cell.appendChild(overlay);
            } else {
                const altInput = document.createElement('input');
                altInput.type = 'text';
                altInput.placeholder = 'Alt text';
                altInput.value = item.alt;
                altInput.addEventListener('input', () => {
                    item.alt = altInput.value;
                    notifyChange();
                });
                cell.appendChild(altInput);

                const delBtn = document.createElement('button');
                delBtn.type = 'button';
                delBtn.className = 'image-modal-item-delete';
                delBtn.textContent = '×';
                delBtn.title = 'Remove';
                delBtn.addEventListener('click', () => {
                    if (item.blobUrl.startsWith('blob:')) URL.revokeObjectURL(item.blobUrl);
                    options.pendingFiles?.delete(item.blobUrl);
                    modalItems.splice(idx, 1);
                    renderGrid();
                });
                cell.appendChild(delBtn);

                if (modalItems.filter(i => !i.oversized).length >= 2) {
                    cell.draggable = true;
                    cell.style.cursor = 'grab';
                    cell.addEventListener('dragstart', (e) => {
                        dragSrcIdx = idx;
                        e.dataTransfer!.effectAllowed = 'move';
                        e.dataTransfer!.setData('application/x-img-modal-reorder', String(idx));
                    });
                    cell.addEventListener('dragover', (e) => {
                        if (!e.dataTransfer?.types.includes('application/x-img-modal-reorder')) return;
                        e.preventDefault();
                        cell.classList.add('drag-over');
                    });
                    cell.addEventListener('dragleave', () => cell.classList.remove('drag-over'));
                    cell.addEventListener('drop', (e) => {
                        e.preventDefault();
                        cell.classList.remove('drag-over');
                        if (dragSrcIdx === null || dragSrcIdx === idx) return;
                        const [moved] = modalItems.splice(dragSrcIdx, 1);
                        if (!moved) return;
                        modalItems.splice(idx, 0, moved);
                        dragSrcIdx = null;
                        renderGrid();
                    });
                    cell.addEventListener('dragend', () => { dragSrcIdx = null; });
                }
            }

            return cell;
        }

        const effectiveLayout = modalItems.length < 2 ? 'grid' : modalLayout;

        if (effectiveLayout === 'carousel') {
            const carousel = document.createElement('div');
            carousel.className = 'gup-carousel image-modal-carousel';
            const track = document.createElement('div');
            track.className = 'gup-carousel-track';
            modalItems.forEach((item, i) => track.appendChild(buildCell(item, i)));
            carousel.appendChild(track);
            const counter = document.createElement('div');
            counter.className = 'gup-carousel-counter';
            const prevBtn = document.createElement('button');
            prevBtn.type = 'button';
            prevBtn.className = 'gup-carousel-arrow gup-carousel-prev';
            prevBtn.innerHTML = '&#8249;';
            const nextBtn = document.createElement('button');
            nextBtn.type = 'button';
            nextBtn.className = 'gup-carousel-arrow gup-carousel-next';
            nextBtn.innerHTML = '&#8250;';
            const updateSlide = () => {
                carouselSlide = Math.max(0, Math.min(carouselSlide, modalItems.length - 1));
                track.style.transform = `translateX(-${carouselSlide * 100}%)`;
                counter.textContent = `${carouselSlide + 1} / ${modalItems.length}`;
                prevBtn.disabled = carouselSlide === 0;
                nextBtn.disabled = carouselSlide === modalItems.length - 1;
            };
            prevBtn.addEventListener('click', () => { carouselSlide--; updateSlide(); });
            nextBtn.addEventListener('click', () => { carouselSlide++; updateSlide(); });
            const _utils2 = (window as any).SnakkUtils;
            if (_utils2?.addCarouselSwipe) {
                _utils2.addCarouselSwipe(track, (idx: number) => { carouselSlide = idx; updateSlide(); }, () => carouselSlide);
            }
            carousel.appendChild(prevBtn);
            carousel.appendChild(nextBtn);
            carousel.appendChild(counter);
            updateSlide();
            grid.appendChild(carousel);
        } else if (effectiveLayout === 'hero') {
            const heroContainer = document.createElement('div');
            heroContainer.className = 'gup-hero';
            if (modalItems.length >= 1) {
                const main = document.createElement('div');
                main.className = 'gup-hero-main';
                main.appendChild(buildCell(modalItems[0]!, 0));
                heroContainer.appendChild(main);
            }
            if (modalItems.length >= 2) {
                const heroGrid = document.createElement('div');
                heroGrid.className = 'gup-hero-grid';
                modalItems.slice(1).forEach((item, i) => heroGrid.appendChild(buildCell(item, i + 1)));
                heroContainer.appendChild(heroGrid);
            }
            grid.appendChild(heroContainer);
        } else if (effectiveLayout === 'compare') {
            const compareContainer = document.createElement('div');
            compareContainer.className = 'gup-compare sn-compare-widget';
            if (modalItems.length >= 1) {
                const before = buildCell(modalItems[0]!, 0);
                before.classList.add('gup-compare-before');
                compareContainer.appendChild(before);
            }
            if (modalItems.length >= 2) {
                const after = buildCell(modalItems[1]!, 1);
                after.classList.add('gup-compare-after');
                compareContainer.appendChild(after);
            }
            const sliderEl = document.createElement('div');
            sliderEl.className = 'gup-compare-slider sn-compare-slider';
            compareContainer.appendChild(sliderEl);
            initPickerCompareSlider(compareContainer);
            grid.appendChild(compareContainer);
        } else {
            const flatContainer = document.createElement('div');
            flatContainer.className = `gup-${effectiveLayout}`;
            modalItems.forEach((item, i) => flatContainer.appendChild(buildCell(item, i)));
            grid.appendChild(flatContainer);
        }
    }

    if (options.initialFiles && options.initialFiles.length > 0) {
        addFiles(options.initialFiles);
    } else {
        renderGrid();
    }

    return {
        getItems: () => modalItems.map(i => ({ blobUrl: i.blobUrl, file: i.file, alt: i.alt, oversized: !!i.oversized })),
        getLayout: () => modalLayout,
        addFiles,
        destroy: () => { fileInput.remove(); },
    };
}

function showImageModal(options: ImageModalOptions): void {
    closeImageModal();
    closeLinkDialog();
    closeChartModal();
    closeTableModal();
    closeGroupDropdown();
    closeEmojiPicker();
    closeCodeModal();

    const isEdit = !!options.onConfirm;

    // Compute budget
    let totalInDoc = 0;
    if (options.editor) {
        options.editor.action((ctx) => { totalInDoc = countImagesFromView(ctx.get(editorViewCtx)); });
    } else if (options.view) {
        totalInDoc = countImagesFromView(options.view);
    }
    const cap = options.maxImages ?? MAX_POST_IMAGES;
    const currentGroupSize = options.data?.images.length ?? 0;
    const maxImages = cap - (options.externalImageCount ?? 0) - (totalInDoc - currentGroupSize);

    // ── DOM ─────────────────────────────────────────────────────────────────
    const backdrop = document.createElement('div');
    backdrop.className = 'sn-modal-backdrop';

    const modal = document.createElement('div');
    modal.className = 'sn-modal';
    modal.style.maxWidth = '42rem';

    // Body
    const body = document.createElement('div');
    body.className = 'sn-modal-body';
    body.style.flexDirection = 'column';

    // Insert button (created before widget so onChange can reference it)
    const insertBtn = document.createElement('button');
    insertBtn.type = 'button';
    insertBtn.className = 'sn-modal-insert';
    insertBtn.textContent = isEdit ? 'Update' : 'Insert';
    insertBtn.disabled = true;

    // Widget renders the interactive picker into body
    const widget = createImagePickerWidget(body, {
        pendingFiles: options.pendingFiles,
        initialData: options.data ? {
            images: options.data.images.map(i => ({ src: i.src, alt: i.alt })),
            layout: options.data.layout,
        } : undefined,
        initialFiles: options.initialFiles,
        maxImages,
        hideLayout: options.hideLayout,
        onChange: (items) => {
            const hasOversized = items.some(i => i.oversized);
            const stillLoading = items.some(i => !i.oversized && !i.blobUrl);
            insertBtn.disabled = items.length === 0 || hasOversized || stillLoading;
        },
    });

    const confirmClose = () => {
        if (widget.getItems().length > 0 && !window.confirm('Discard changes?')) return;
        widget.destroy();
        closeImageModal();
    };

    // Header
    const header = document.createElement('div');
    header.className = 'sn-modal-header';
    const title = document.createElement('span');
    title.textContent = isEdit ? 'Edit Images' : 'Upload Images';
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'sn-modal-close';
    closeBtn.innerHTML = '&times;';
    closeBtn.addEventListener('click', confirmClose);
    const imageHeaderActions = document.createElement('div');
    imageHeaderActions.className = 'sn-modal-header-actions';
    imageHeaderActions.append(createModalExpandBtn(modal), closeBtn);
    header.append(title, imageHeaderActions);

    // Footer
    const footer = document.createElement('div');
    footer.className = 'sn-modal-footer';
    const statusEl = document.createElement('span');
    statusEl.className = 'image-modal-status';
    const cancelBtn = document.createElement('button');
    cancelBtn.type = 'button';
    cancelBtn.className = 'sn-modal-cancel';
    cancelBtn.textContent = T.common.cancel;
    cancelBtn.addEventListener('click', confirmClose);
    footer.appendChild(statusEl);
    footer.appendChild(cancelBtn);
    footer.appendChild(insertBtn);

    modal.appendChild(header);
    modal.appendChild(body);
    modal.appendChild(footer);
    backdrop.appendChild(modal);
    document.body.appendChild(backdrop);
    activeImageModal = backdrop;
    lockBodyScroll();

    backdrop.addEventListener('mousedown', (e) => {
        if (e.target === backdrop) confirmClose();
    });

    imageModalEscHandler = (e: KeyboardEvent) => { if (e.key === 'Escape') confirmClose(); };
    document.addEventListener('keydown', imageModalEscHandler);

    // ── Insert / Update ───────────────────────────────────────────────────────
    insertBtn.addEventListener('click', () => {
        const items = widget.getItems();
        if (items.length === 0) return;

        // Register new files for deferred upload on submit (editor case)
        if (options.editor) {
            const pending = editorPendingUploads.get(options.editor);
            if (pending) {
                for (const item of items) {
                    if (item.file && !item.oversized) pending.set(item.blobUrl, item.file);
                }
            }
        }

        const data: ImageModalData = {
            images: items.map(m => ({ src: m.blobUrl, alt: m.alt })),
            layout: widget.getLayout(),
        };

        widget.destroy();
        closeImageModal();

        if (options.onConfirm) {
            options.onConfirm(data);
        } else if (options.editor) {
            insertImageGroup(options.editor, data);
        }
    });
}

// ============================================================================
// Table Markdown Cleanup
// ============================================================================

/**
 * Cleans GFM tables in markdown: removes empty rows, empty columns,
 * and strips entirely empty tables.
 */
function cleanTableMarkdown(md: string): string {
    // Match GFM table blocks: lines starting with | (with optional leading whitespace)
    // A table block is a contiguous run of lines that start with |
    const lines = md.split('\n');
    const result: string[] = [];
    let i = 0;

    while (i < lines.length) {
        // Detect start of a table block (line starts with optional whitespace then |)
        if (/^\s*\|/.test(lines[i])) {
            const tableLines: string[] = [];
            while (i < lines.length && /^\s*\|/.test(lines[i])) {
                tableLines.push(lines[i]);
                i++;
            }
            const cleaned = cleanSingleTable(tableLines);
            if (cleaned) result.push(cleaned);
        } else {
            result.push(lines[i]);
            i++;
        }
    }

    return result.join('\n');
}

function cleanSingleTable(tableLines: string[]): string | null {
    if (tableLines.length < 2) return null; // Need at least header + separator

    // Parse rows into cells (strip <br /> which Milkdown inserts for empty paragraphs)
    const parseCells = (line: string): string[] =>
        line.split('|').slice(1, -1).map(c => c.replace(/<br\s*\/?>/g, '').trim());

    // Find the separator line (contains only :, -, |, spaces)
    const sepIndex = tableLines.findIndex(l => /^\s*\|[\s:|-]+\|\s*$/.test(l));
    if (sepIndex < 0) return tableLines.join('\n'); // Not a valid table, return as-is

    const headerLines = tableLines.slice(0, sepIndex);
    const sepLine = tableLines[sepIndex];
    const dataLines = tableLines.slice(sepIndex + 1);

    const headerRows = headerLines.map(parseCells);
    const sepCells = parseCells(sepLine);
    const dataRows = dataLines.map(parseCells);

    const colCount = sepCells.length;
    if (colCount === 0) return null;

    // Normalize all rows to colCount
    const normalize = (row: string[]): string[] => {
        const r = row.slice(0, colCount);
        while (r.length < colCount) r.push('');
        return r;
    };
    const normHeaders = headerRows.map(normalize);
    const normData = dataRows.map(normalize);

    // Remove entirely empty columns
    const nonEmptyCols: number[] = [];
    for (let c = 0; c < colCount; c++) {
        const hasContent = normHeaders.some(row => row[c] !== '')
            || normData.some(row => row[c] !== '');
        if (hasContent) nonEmptyCols.push(c);
    }

    if (nonEmptyCols.length === 0) return null; // Entirely empty table

    // Remove entirely empty data rows
    const filteredData = normData.filter(row =>
        nonEmptyCols.some(c => row[c] !== '')
    );

    // If no data rows remain, strip the whole table
    if (filteredData.length === 0) {
        const hasHeaderContent = normHeaders.some(row =>
            nonEmptyCols.some(c => row[c] !== '')
        );
        if (!hasHeaderContent) return null;
    }

    // Rebuild table with only non-empty columns
    const buildRow = (cells: string[]): string =>
        '| ' + nonEmptyCols.map(c => cells[c]).join(' | ') + ' |';

    const buildSep = (): string =>
        '| ' + nonEmptyCols.map(c => sepCells[c]).join(' | ') + ' |';

    const rebuilt: string[] = [];
    normHeaders.forEach(row => rebuilt.push(buildRow(row)));
    rebuilt.push(buildSep());
    filteredData.forEach(row => rebuilt.push(buildRow(row)));

    return rebuilt.join('\n');
}

// ============================================================================
// Table Modal
// ============================================================================

interface TableModalOptions {
    onConfirm: (data: TableModalData) => void;
    data?: TableModalData;
    isEdit?: boolean;
}

let activeTableModal: HTMLElement | null = null;

function closeTableModal(): void {
    if (activeTableModal) {
        activeTableModal.remove();
        activeTableModal = null;
        unlockBodyScroll();
    }
}

function showTableModal(options: TableModalOptions): void {
    closeTableModal();
    closeLinkDialog();
    closeGroupDropdown();
    closeChartModal();
    closeListModal();
    closeImageModal();
    closeCodeModal();

    const isEdit = options.isEdit ?? false;
    const initial = options.data;
    let hasEdited = isEdit;

    let currentRows = initial ? initial.rows.length : 2;
    let currentCols = initial ? initial.headers.length : 3;
    const alignments: Array<'left' | 'center' | 'right' | null> =
        initial ? [...initial.alignments] : Array.from({ length: currentCols }, () => 'left' as const);
    let headerValues: string[] = initial ? [...initial.headers] : [];
    let cellValues: string[][] = initial ? initial.rows.map(r => [...r]) : [];
    let updateInsertState: () => void = () => {};

    const backdrop = document.createElement('div');
    backdrop.className = 'sn-modal-backdrop';

    const modal = document.createElement('div');
    modal.className = 'sn-modal';
    modal.style.maxWidth = '54rem';
    backdrop.appendChild(modal);

    // Header
    const header = document.createElement('div');
    header.className = 'sn-modal-header';
    const titleSpan = document.createElement('span');
    titleSpan.textContent = isEdit ? T.editor.updateTable : T.editor.insertTable;
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'sn-modal-close';
    closeBtn.textContent = '×';
    const tableHeaderActions = document.createElement('div');
    tableHeaderActions.className = 'sn-modal-header-actions';
    tableHeaderActions.append(createModalExpandBtn(modal), closeBtn);
    header.append(titleSpan, tableHeaderActions);
    modal.appendChild(header);

    // Body (vertical, no preview panel)
    const body = document.createElement('div');
    body.className = 'sn-modal-body';
    body.style.flexDirection = 'column';
    modal.appendChild(body);

    const form = document.createElement('div');
    form.className = 'sn-modal-form';
    body.appendChild(form);

    // Cell grid container
    const gridSection = document.createElement('div');
    gridSection.className = 'tmc-grid-section';
    form.appendChild(gridSection);

    const TMC_MAX_ROWS = 100;
    const TMC_MAX_COLS = 10;
    const ALIGN_CYCLE: Array<'left' | 'center' | 'right' | null> = [null, 'left', 'center', 'right'];
    const ALIGN_ICONS: { [k: string]: string } = { left: '←', center: '↔', right: '→', '': '—' };
    const nextAlign = (cur: 'left' | 'center' | 'right' | null) =>
        ALIGN_CYCLE[(ALIGN_CYCLE.indexOf(cur) + 1) % ALIGN_CYCLE.length]!;

    const rebuildGrid = (rows: number, cols: number) => {
        gridSection.innerHTML = '';
        const table = document.createElement('table');
        table.className = 'tmc-table';

        // thead
        const thead = document.createElement('thead');
        const headRow = document.createElement('tr');
        for (let c = 0; c < cols; c++) {
            const th = document.createElement('th');
            th.className = 'tmc-th';
            const inner = document.createElement('div');
            inner.className = 'tmc-th-inner';
            const inp = document.createElement('input');
            inp.type = 'text';
            inp.className = 'tmc-input tmc-input-header';
            inp.placeholder = `Col ${c + 1}`;
            inp.value = headerValues[c] ?? '';
            inp.dataset.row = '-1';
            inp.dataset.col = String(c);
            const align = alignments[c] ?? 'left';
            inp.style.textAlign = align;
            const alignBtn = document.createElement('button');
            alignBtn.type = 'button';
            alignBtn.className = 'tmc-align-btn';
            alignBtn.dataset.col = String(c);
            alignBtn.textContent = ALIGN_ICONS[align] ?? '←';
            alignBtn.title = `Alignment: ${align}`;
            alignBtn.dataset.align = align;
            const delBtn = document.createElement('button');
            delBtn.type = 'button';
            delBtn.className = 'tmc-del-col-btn';
            delBtn.dataset.col = String(c);
            delBtn.innerHTML = '&times;';
            delBtn.title = 'Delete column';
            inner.append(inp, alignBtn, delBtn);
            th.appendChild(inner);
            headRow.appendChild(th);
        }
        const addColTh = document.createElement('th');
        addColTh.className = 'tmc-th tmc-add-col-th';
        if (cols < TMC_MAX_COLS) {
            const addColBtn = document.createElement('button');
            addColBtn.type = 'button';
            addColBtn.className = 'tmc-add-col-btn';
            addColBtn.textContent = '+';
            addColBtn.title = 'Add column';
            addColTh.appendChild(addColBtn);
        }
        headRow.appendChild(addColTh);
        thead.appendChild(headRow);
        table.appendChild(thead);

        // tbody
        const tbody = document.createElement('tbody');
        for (let r = 0; r < rows; r++) {
            const tr = document.createElement('tr');
            for (let c = 0; c < cols; c++) {
                const td = document.createElement('td');
                td.className = 'tmc-td';
                const inp = document.createElement('input');
                inp.type = 'text';
                inp.className = 'tmc-input';
                inp.value = cellValues[r]?.[c] ?? '';
                inp.dataset.row = String(r);
                inp.dataset.col = String(c);
                inp.style.textAlign = alignments[c] ?? 'left';
                td.appendChild(inp);
                tr.appendChild(td);
            }
            const delTd = document.createElement('td');
            delTd.className = 'tmc-td tmc-del-row-td';
            const delRowBtn = document.createElement('button');
            delRowBtn.type = 'button';
            delRowBtn.className = 'tmc-del-row-btn';
            delRowBtn.dataset.row = String(r);
            delRowBtn.innerHTML = '&times;';
            delRowBtn.title = 'Delete row';
            delTd.appendChild(delRowBtn);
            tr.appendChild(delTd);
            tbody.appendChild(tr);
        }
        if (rows < TMC_MAX_ROWS) {
            const addTr = document.createElement('tr');
            const addTd = document.createElement('td');
            addTd.className = 'tmc-td tmc-add-row-td';
            addTd.colSpan = cols;
            const addRowBtn = document.createElement('button');
            addRowBtn.type = 'button';
            addRowBtn.className = 'tmc-add-row-btn';
            addRowBtn.textContent = '+ Add row';
            addTd.appendChild(addRowBtn);
            addTr.appendChild(addTd);
            const addRowSpaceTd = document.createElement('td');
            addRowSpaceTd.className = 'tmc-del-row-td';
            addTr.appendChild(addRowSpaceTd);
            tbody.appendChild(addTr);
        }
        table.appendChild(tbody);
        gridSection.appendChild(table);
    };

    rebuildGrid(currentRows, currentCols);
    gridSection.addEventListener('input', () => { hasEdited = true; updateInsertState(); });

    const saveCells = () => {
        gridSection.querySelectorAll<HTMLInputElement>('[data-row="-1"]').forEach(inp => {
            headerValues[parseInt(inp.dataset.col!)] = inp.value;
        });
        gridSection.querySelectorAll<HTMLInputElement>('[data-row]:not([data-row="-1"])').forEach(inp => {
            const r = parseInt(inp.dataset.row!);
            const c = parseInt(inp.dataset.col!);
            if (!cellValues[r]) cellValues[r] = [];
            cellValues[r]![c] = inp.value;
        });
    };

    gridSection.addEventListener('mousedown', (e) => {
        const target = e.target as HTMLElement;
        if (target.classList.contains('tmc-del-col-btn')) {
            e.preventDefault();
            saveCells();
            const c = parseInt(target.dataset.col!);
            headerValues.splice(c, 1);
            cellValues.forEach(row => row.splice(c, 1));
            alignments.splice(c, 1);
            currentCols = Math.max(1, currentCols - 1);
            rebuildGrid(currentRows, currentCols);
            updateInsertState();
            return;
        }
        if (target.classList.contains('tmc-del-row-btn')) {
            e.preventDefault();
            saveCells();
            const r = parseInt(target.dataset.row!);
            cellValues.splice(r, 1);
            currentRows = Math.max(1, currentRows - 1);
            rebuildGrid(currentRows, currentCols);
            updateInsertState();
            return;
        }
        if (target.classList.contains('tmc-add-col-btn')) {
            e.preventDefault();
            if (currentCols >= TMC_MAX_COLS) return;
            saveCells();
            alignments[currentCols] = alignments[currentCols] ?? 'left';
            currentCols++;
            rebuildGrid(currentRows, currentCols);
            updateInsertState();
            return;
        }
        if (target.classList.contains('tmc-add-row-btn')) {
            e.preventDefault();
            if (currentRows >= TMC_MAX_ROWS) return;
            saveCells();
            currentRows++;
            rebuildGrid(currentRows, currentCols);
            updateInsertState();
            return;
        }
        if (target.classList.contains('tmc-align-btn')) {
            e.preventDefault();
            const c = parseInt(target.dataset.col!);
            alignments[c] = nextAlign(alignments[c] ?? null);
            const effectiveAlign = alignments[c] ?? 'left';
            target.textContent = ALIGN_ICONS[effectiveAlign] ?? '←';
            target.title = `Alignment: ${effectiveAlign}`;
            target.dataset.align = effectiveAlign;
            gridSection.querySelectorAll<HTMLInputElement>(`input[data-col="${c}"]`).forEach(inp => {
                inp.style.textAlign = effectiveAlign;
            });
            return;
        }
    });

    gridSection.addEventListener('paste', (e) => {
        const focused = gridSection.querySelector<HTMLInputElement>('input:focus');
        if (!focused) return;
        e.preventDefault();
        saveCells();
        const startRow = parseInt(focused.dataset.row!);
        const startCol = parseInt(focused.dataset.col!);

        let pastedData: string[][] | null = null;
        const html = e.clipboardData?.getData('text/html') ?? '';
        if (html) {
            const doc = new DOMParser().parseFromString(html, 'text/html');
            const trs = Array.from(doc.querySelectorAll('tr'));
            if (trs.length > 0) {
                pastedData = trs.map(tr =>
                    Array.from(tr.querySelectorAll('td, th')).map(cell => cell.textContent?.trim() ?? '')
                );
            }
        }
        if (!pastedData) {
            const text = e.clipboardData?.getData('text/plain') ?? '';
            if (text) pastedData = text.split('\n').filter(r => r.length > 0).map(r => r.split('\t'));
        }
        if (!pastedData || pastedData.length === 0) return;

        const neededRows = Math.max(0, startRow + pastedData.length);
        const neededCols = startCol + Math.max(...pastedData.map(r => r.length));
        const newRows = Math.min(TMC_MAX_ROWS, Math.max(currentRows, neededRows));
        const newCols = Math.min(TMC_MAX_COLS, Math.max(currentCols, neededCols));

        for (let pi = 0; pi < pastedData.length; pi++) {
            const targetRow = startRow + pi;
            for (let pj = 0; pj < pastedData[pi]!.length; pj++) {
                const targetCol = startCol + pj;
                if (targetCol >= newCols) break;
                const val = pastedData[pi]![pj] ?? '';
                if (targetRow === -1) {
                    headerValues[targetCol] = val;
                } else {
                    if (targetRow >= newRows) break;
                    if (!cellValues[targetRow]) cellValues[targetRow] = [];
                    cellValues[targetRow]![targetCol] = val;
                }
            }
        }
        currentRows = newRows;
        currentCols = newCols;
        rebuildGrid(currentRows, currentCols);
        updateInsertState();
    });

    gridSection.addEventListener('keydown', (e) => {
        const focused = gridSection.querySelector<HTMLInputElement>('input:focus');
        if (!focused) return;

        if (e.key === 'Tab') {
            e.preventDefault();
            const inputs = Array.from(gridSection.querySelectorAll<HTMLInputElement>('input[data-row]'))
                .sort((a, b) => {
                    const rowDiff = parseInt(a.dataset.row!) - parseInt(b.dataset.row!);
                    return rowDiff !== 0 ? rowDiff : parseInt(a.dataset.col!) - parseInt(b.dataset.col!);
                });
            const idx = inputs.indexOf(focused);
            if (idx === -1) return;
            const next = inputs[(idx + (e.shiftKey ? -1 + inputs.length : 1)) % inputs.length]!;
            next.focus();
            next.select();
            return;
        }

        if (e.key === 'Enter') {
            const row = parseInt(focused.dataset.row!);
            if (row !== currentRows - 1 || currentRows >= TMC_MAX_ROWS) return;
            e.preventDefault();
            saveCells();
            currentRows++;
            rebuildGrid(currentRows, currentCols);
            updateInsertState();
            gridSection.querySelector<HTMLInputElement>(
                `input[data-row="${currentRows - 1}"][data-col="0"]`
            )?.focus();
        }
    });

    // Footer
    const footer = document.createElement('div');
    footer.className = 'sn-modal-footer';
    const cancelBtn = document.createElement('button');
    cancelBtn.type = 'button';
    cancelBtn.className = 'sn-modal-cancel';
    cancelBtn.textContent = T.common.cancel;
    const insertBtn = document.createElement('button');
    insertBtn.type = 'button';
    insertBtn.className = 'sn-modal-insert';
    insertBtn.textContent = isEdit ? T.editor.updateTable : T.editor.insertTable;
    footer.appendChild(cancelBtn);
    footer.appendChild(insertBtn);
    modal.appendChild(footer);

    // Wire updateInsertState now that insertBtn exists
    updateInsertState = () => {
        const hasData = Array.from(
            gridSection.querySelectorAll<HTMLInputElement>('[data-row]:not([data-row="-1"])')
        ).some(inp => inp.value.trim() !== '');
        insertBtn.disabled = !hasData;
    };
    updateInsertState();

    const close = () => {
        backdrop.remove();
        activeTableModal = null;
        unlockBodyScroll();
        document.removeEventListener('keydown', onKey);
    };
    const confirmClose = () => {
        if (hasEdited && !window.confirm('Discard changes?')) return;
        close();
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.preventDefault(); confirmClose(); } };
    closeBtn.addEventListener('mousedown', (e) => { e.preventDefault(); confirmClose(); });
    cancelBtn.addEventListener('mousedown', (e) => { e.preventDefault(); confirmClose(); });
    backdrop.addEventListener('mousedown', (e) => { if (e.target === backdrop) confirmClose(); });
    document.addEventListener('keydown', onKey);

    insertBtn.addEventListener('mousedown', (e) => {
        e.preventDefault();
        saveCells();
        const headers: string[] = [];
        gridSection.querySelectorAll<HTMLInputElement>('[data-row="-1"]').forEach(inp => headers.push(inp.value));
        const rows: string[][] = Array.from({ length: currentRows }, (_, r) =>
            Array.from({ length: currentCols }, (__, c) => {
                const inp = gridSection.querySelector<HTMLInputElement>(`[data-row="${r}"][data-col="${c}"]`);
                return inp?.value ?? '';
            })).filter(row => row.some(cell => cell.trim() !== ''));
        const finalAlignments = Array.from({ length: currentCols }, (_, i) => alignments[i] ?? null);
        close();
        options.onConfirm({ headers, rows, alignments: finalAlignments });
    });

    document.body.appendChild(backdrop);
    activeTableModal = backdrop;
    lockBodyScroll();
}

function insertTable(editor: Editor, data: TableModalData): void {
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const { state } = view;
        const schema = state.schema;
        const makeCell = (type: string, text: string, align: string | null) => {
            const content = text.trim() ? [schema.text(text)] : [];
            return schema.nodes[type]!.create({ alignment: align }, content.length ? content : undefined);
        };
        try {
            const headerCells = data.headers.map((h, i) =>
                makeCell('table_header', h, data.alignments[i] ?? null));
            const headerRow = schema.nodes.table_header_row!.create(null, headerCells);
            const bodyRows = data.rows.map(row =>
                schema.nodes.table_row!.create(null,
                    row.map((cell, i) => makeCell('table_cell', cell, data.alignments[i] ?? null))));
            const table = schema.nodes.table!.create(null, [headerRow, ...bodyRows]);
            view.dispatch(state.tr.replaceSelectionWith(table));
        } catch (err) {
            console.warn('Table insert failed', err);
        }
    });
    placeCursorAfterBlock(editor);
}

// ============================================================================
// Old table picker removed — replaced by showTableModal above
// ============================================================================
function _oldTablePickerRemoved() { const picker = document.createElement('div');

    const label = document.createElement('div');
    label.className = 'table-picker-label';
    label.textContent = T.editor.selectSize;

    const grid = document.createElement('div');
    grid.className = 'table-picker-grid';

    const cells: HTMLElement[] = [];

    for (let r = 0; r < TABLE_ROWS; r++) {
        for (let c = 0; c < TABLE_COLS; c++) {
            const cell = document.createElement('div');
            cell.className = `table-picker-cell${r === 0 ? ' table-picker-header' : ''}`;
            cell.dataset.row = String(r);
            cell.dataset.col = String(c);
            grid.appendChild(cell);
            cells.push(cell);
        }
    }

    // Shared: highlight from (0,0) up to the cell at (row, col)
    const highlightTo = (target: HTMLElement) => {
        const hoverRow = Math.max(1, parseInt(target.dataset.row!));
        const hoverCol = parseInt(target.dataset.col!);

        cells.forEach(cell => {
            const cr = parseInt(cell.dataset.row!);
            const cc = parseInt(cell.dataset.col!);
            cell.classList.toggle('highlighted', cr <= hoverRow && cc <= hoverCol);
        });

        label.textContent = `${hoverCol + 1} × ${hoverRow + 1}`;
    };

    const insertFromCell = (target: HTMLElement) => {
        const row = Math.max(2, parseInt(target.dataset.row!) + 1);
        const col = parseInt(target.dataset.col!) + 1;

        editor.action(callCommand(insertTableCommand.key, { row, col }));
        closeTablePicker();
        editor.action((ctx) => ctx.get(editorViewCtx).focus());
    };

    // Mouse path: hover to preview, click to insert.
    grid.addEventListener('mouseover', (e) => {
        const target = (e.target as HTMLElement).closest('.table-picker-cell') as HTMLElement;
        if (!target) return;
        highlightTo(target);
    });

    grid.addEventListener('mouseleave', () => {
        cells.forEach(cell => cell.classList.remove('highlighted'));
        label.textContent = T.editor.selectSize;
    });

    grid.addEventListener('click', (e) => {
        const target = (e.target as HTMLElement).closest('.table-picker-cell') as HTMLElement;
        if (!target) return;
        insertFromCell(target);
    });

    // Touch path: press + drag across cells to preview size, release to insert.
    // elementFromPoint is used because pointermove events on the initial cell
    // don't re-target as the finger moves to siblings.
    let touchActive = false;
    let lastTouchCell: HTMLElement | null = null;

    grid.addEventListener('pointerdown', (e: PointerEvent) => {
        if (e.pointerType !== 'touch' && e.pointerType !== 'pen') return;
        const target = (e.target as HTMLElement).closest('.table-picker-cell') as HTMLElement;
        if (!target) return;
        e.preventDefault();
        touchActive = true;
        lastTouchCell = target;
        highlightTo(target);
    });

    grid.addEventListener('pointermove', (e: PointerEvent) => {
        if (!touchActive) return;
        const el = document.elementFromPoint(e.clientX, e.clientY) as HTMLElement | null;
        const target = el?.closest('.table-picker-cell') as HTMLElement | null;
        if (!target || target === lastTouchCell) return;
        lastTouchCell = target;
        highlightTo(target);
    });

    const endTouch = (e: PointerEvent) => {
        if (!touchActive) return;
        touchActive = false;
        if (e.type === 'pointercancel') {
            cells.forEach(cell => cell.classList.remove('highlighted'));
            label.textContent = T.editor.selectSize;
            lastTouchCell = null;
            return;
        }
        const target = lastTouchCell;
        lastTouchCell = null;
        if (target) insertFromCell(target);
    };
    grid.addEventListener('pointerup', endTouch);
    grid.addEventListener('pointercancel', endTouch);

    picker.appendChild(label);
    picker.appendChild(grid);

    // Mount inside editor wrapper, positioned below the table button
    const editorWrapper = document.querySelector('.milkdown-editor') as HTMLElement;
    if (editorWrapper) {
        editorWrapper.appendChild(picker);
        activeTablePicker = picker;

        // Position below the table button with notch aligned
        const tableBtn = editorWrapper.querySelector('.toolbar-table') as HTMLElement;
        if (tableBtn) {
            const btnRect = tableBtn.getBoundingClientRect();
            const wrapperRect = editorWrapper.getBoundingClientRect();
            const pickerWidth = picker.offsetWidth;
            const btnCenterX = btnRect.left + btnRect.width / 2 - wrapperRect.left;
            const notchOffset = 16;
            picker.style.top = `${btnRect.bottom - wrapperRect.top + 6}px`;

            let left = btnCenterX - notchOffset;
            left = Math.max(4, Math.min(left, wrapperRect.width - pickerWidth - 4));
            picker.style.left = `${left}px`;

            const notchLeft = btnCenterX - left;
            picker.style.setProperty('--notch-left', `${notchLeft}px`);
        }
    }

    // Close on outside click or scroll
    const onOutsideClick = (e: MouseEvent) => {
        if (!picker.contains(e.target as Node)) {
            cleanupPicker();
        }
    };
    const onScroll = () => cleanupPicker();
    const cleanupPicker = () => {
        closeTablePicker();
        document.removeEventListener('mousedown', onOutsideClick);
        window.removeEventListener('scroll', onScroll, true);
    };
    // Defer to avoid immediate close from the toolbar button click
    setTimeout(() => {
        document.addEventListener('mousedown', onOutsideClick);
        window.addEventListener('scroll', onScroll, true);
    }, 0);
}

// ============================================================================
// List Modal
// ============================================================================

interface ListModalOptions {
    onConfirm: (data: ListModalData) => void;
    data?: ListModalData;
    isEdit?: boolean;
}

let activeListModal: HTMLElement | null = null;

function closeListModal(): void {
    if (activeListModal) {
        activeListModal.remove();
        activeListModal = null;
        unlockBodyScroll();
    }
}

const LIST_TYPE_META: Record<ListModalData['type'], { icon: string; label: string }> = {
    bullet:  { icon: '•',  label: 'Bullet List'  },
    ordered: { icon: '1.', label: 'Ordered List' },
    task:    { icon: '☑',  label: 'Checklist'    },
};

function showListPickerModal(editor: Editor): void {
    closeListModal();
    closeChartModal();
    closeTableModal();

    const backdrop = document.createElement('div');
    backdrop.className = 'sn-modal-backdrop';
    activeListModal = backdrop;

    const modal = document.createElement('div');
    modal.className = 'sn-modal chart-picker-modal';

    const header = document.createElement('div');
    header.className = 'sn-modal-header';
    const title = document.createElement('span');
    title.textContent = T.editor.insertList;
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'sn-modal-close';
    closeBtn.innerHTML = '&times;';
    closeBtn.onclick = closeListModal;
    header.append(title, closeBtn);

    const body = document.createElement('div');
    body.className = 'sn-modal-body chart-picker-body';
    const grid = document.createElement('div');
    grid.className = 'chart-picker-grid';

    for (const [type, meta] of Object.entries(LIST_TYPE_META) as [ListModalData['type'], { icon: string; label: string }][]) {
        const card = document.createElement('button');
        card.className = 'chart-picker-card';
        card.type = 'button';
        const iconSpan = document.createElement('span');
        iconSpan.className = 'chart-picker-card-icon';
        iconSpan.textContent = meta.icon;
        const labelSpan = document.createElement('span');
        labelSpan.className = 'chart-picker-card-label';
        labelSpan.textContent = meta.label;
        card.append(iconSpan, labelSpan);
        card.onclick = () => {
            closeListModal();
            showListEditorModal(type, { onConfirm: (data) => insertListBlock(editor, data) });
        };
        grid.appendChild(card);
    }

    body.appendChild(grid);
    modal.append(header, body);
    backdrop.appendChild(modal);
    document.body.appendChild(backdrop);
    lockBodyScroll();

    backdrop.addEventListener('click', (e) => { if (e.target === backdrop) closeListModal(); });
}

function showListEditorModal(type: ListModalData['type'], options: ListModalOptions): void {
    closeListModal();

    const isEdit = options.isEdit ?? false;
    const initial = options.data;
    const meta = LIST_TYPE_META[type];

    const backdrop = document.createElement('div');
    backdrop.className = 'sn-modal-backdrop';
    activeListModal = backdrop;

    const modal = document.createElement('div');
    modal.className = 'sn-modal';
    modal.style.maxWidth = '32rem';
    backdrop.appendChild(modal);

    // Header
    const header = document.createElement('div');
    header.className = 'sn-modal-header';
    const titleSpan = document.createElement('span');
    titleSpan.textContent = isEdit ? `Edit ${meta.label}` : `Insert ${meta.label}`;
    function isDirty(): boolean {
        return Array.from(itemsContainer.querySelectorAll<HTMLInputElement>('.lmc-item-input'))
            .some(inp => inp.value.trim() !== '');
    }

    function confirmClose(): void {
        if (isDirty() && !window.confirm('Discard changes?')) return;
        closeListModal();
    }

    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'sn-modal-close';
    closeBtn.textContent = '×';
    closeBtn.onclick = confirmClose;
    const headerActions = document.createElement('div');
    headerActions.className = 'sn-modal-header-actions';
    headerActions.append(createModalExpandBtn(modal), closeBtn);
    header.append(titleSpan, headerActions);
    modal.appendChild(header);

    // Body
    const body = document.createElement('div');
    body.className = 'sn-modal-body';
    body.style.flexDirection = 'column';
    modal.appendChild(body);

    const itemsContainer = document.createElement('div');
    itemsContainer.className = 'lmc-items';
    body.appendChild(itemsContainer);

    function addItemRow(text = '', checked = false, insertAfter?: HTMLElement): HTMLElement {
        const row = document.createElement('div');
        row.className = 'lmc-item-row';

        if (type === 'task') {
            const cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.className = 'lmc-item-checkbox';
            cb.checked = checked;
            row.appendChild(cb);
        }

        const inp = document.createElement('input');
        inp.type = 'text';
        inp.className = 'sn-modal-input lmc-item-input';
        inp.placeholder = 'List item…';
        inp.value = text;
        inp.addEventListener('keydown', (e) => {
            if ((e.key === 'Enter' || e.key === 'Tab') && !e.ctrlKey && !e.metaKey) {
                e.preventDefault();
                const newRow = addItemRow('', false, row);
                newRow.querySelector<HTMLInputElement>('.lmc-item-input')?.focus();
            } else if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                e.preventDefault();
                insertBtn.click();
            } else if (e.key === 'Escape') {
                e.preventDefault();
                confirmClose();
            } else if (e.key === 'Backspace' && inp.value === '') {
                e.preventDefault();
                const rows = Array.from(itemsContainer.children) as HTMLElement[];
                if (rows.length <= 1) return;
                const idx = rows.indexOf(row);
                const prev = rows[idx - 1];
                row.remove();
                prev?.querySelector<HTMLInputElement>('.lmc-item-input')?.focus();
            }
        });
        row.appendChild(inp);

        const delBtn = document.createElement('button');
        delBtn.type = 'button';
        delBtn.className = 'lmc-item-delete';
        delBtn.innerHTML = '&times;';
        delBtn.title = 'Remove item';
        delBtn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            if (itemsContainer.children.length > 1) row.remove();
        });
        row.appendChild(delBtn);

        if (insertAfter) {
            insertAfter.insertAdjacentElement('afterend', row);
        } else {
            itemsContainer.appendChild(row);
        }

        return row;
    }

    const startItems = initial?.items?.length ? initial.items : [{ text: '' }];
    for (const item of startItems) {
        addItemRow(item.text, item.checked ?? false);
    }

    const addItemBtn = document.createElement('button');
    addItemBtn.type = 'button';
    addItemBtn.className = 'lmc-add-btn';
    addItemBtn.textContent = T.editor.addItem;
    addItemBtn.addEventListener('click', () => {
        const newRow = addItemRow();
        newRow.querySelector<HTMLInputElement>('.lmc-item-input')?.focus();
    });
    body.appendChild(addItemBtn);

    // Footer
    const footer = document.createElement('div');
    footer.className = 'sn-modal-footer';
    modal.appendChild(footer);

    const cancelBtn = document.createElement('button');
    cancelBtn.type = 'button';
    cancelBtn.className = 'sn-modal-cancel';
    cancelBtn.textContent = T.common.cancel;
    cancelBtn.onclick = confirmClose;

    const insertBtn = document.createElement('button');
    insertBtn.type = 'button';
    insertBtn.className = 'sn-modal-insert';
    insertBtn.textContent = isEdit ? 'Update' : 'Insert';
    insertBtn.addEventListener('click', () => {
        const items: ListItem[] = [];
        for (const row of Array.from(itemsContainer.children) as HTMLElement[]) {
            const text = row.querySelector<HTMLInputElement>('.lmc-item-input')?.value ?? '';
            const cb = row.querySelector<HTMLInputElement>('.lmc-item-checkbox');
            if (type === 'task') {
                items.push({ text, checked: cb?.checked ?? false });
            } else {
                items.push({ text });
            }
        }
        const filtered = items.filter(i => i.text.trim() !== '');
        if (filtered.length === 0) { closeListModal(); return; }
        closeListModal();
        options.onConfirm({ type, items: filtered });
    });

    footer.append(cancelBtn, insertBtn);

    document.body.appendChild(backdrop);
    lockBodyScroll();

    backdrop.addEventListener('click', (e) => { if (e.target === backdrop) confirmClose(); });

    setTimeout(() => {
        (itemsContainer.querySelector('.lmc-item-input') as HTMLInputElement | null)?.focus();
    }, 50);
}

function insertListBlock(editor: Editor, data: ListModalData): void {
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const schema = view.state.schema;
        const listNode = buildListNode(schema, data);
        if (!listNode) return;

        const para = schema.nodes.paragraph!.create();
        const { tr, selection } = view.state;
        const $from = selection.$from;
        const depth = $from.depth;
        let insertTr = tr;
        let paraPos: number;

        if (depth > 0 && $from.parent.content.size === 0) {
            const blockStart = $from.before(depth);
            const blockEnd = $from.after(depth);
            insertTr = insertTr.replaceWith(blockStart, blockEnd, Fragment.from([listNode, para]));
            paraPos = blockStart + listNode.nodeSize + 1;
        } else {
            const blockEnd = depth > 0 ? $from.after(depth) : selection.from;
            insertTr = insertTr.insert(blockEnd, Fragment.from([listNode, para]));
            paraPos = blockEnd + listNode.nodeSize + 1;
        }

        insertTr = insertTr.setSelection(TextSelection.create(insertTr.doc, paraPos));
        view.dispatch(insertTr);
        view.focus();
    });
}

// ============================================================================
// Emoji Picker
// ============================================================================

const EMOJI_DATA: Array<{ cat: string; emojis: string[] }> = [
    { cat: 'Smileys', emojis: [
        '😀','😃','😄','😁','😆','😅','🤣','😂','🙂','😊',
        '😇','🥰','😍','🤩','😘','😋','😛','😜','🤪','😝',
        '🤑','🤗','🤭','🤫','🤔','🫡','😐','😑','😶','🫥',
        '😏','😒','🙄','😬','😮‍💨','🤥','😌','😔','😪','🤤',
        '😴','😷','🤒','🤕','🤢','🤮','🥵','🥶','🥴','😵',
        '🤯','🥳','🤠','🫠','😎','🤓','🧐','😕','🫤','😟',
        '🙁','😮','😯','😲','😳','🥺','🥹','😦','😧','😨',
        '😰','😥','😢','😭','😱','😖','😣','😞','😓','😩',
        '😫','🥱','😤','😡','😠','🤬','💀','☠️','💩','🤡',
        '👹','👺','👻','👽','👾','🤖',
    ]},
    { cat: 'Gestures', emojis: [
        '👋','🤚','🖐️','✋','🖖','🫱','🫲','🫳','🫴','👌',
        '🤌','🤏','✌️','🤞','🫰','🤟','🤘','🤙','👈','👉',
        '👆','🖕','👇','☝️','🫵','👍','👎','✊','👊','🤛',
        '🤜','👏','🙌','🫶','👐','🤲','🤝','🙏','💪','🦾',
    ]},
    { cat: 'Hearts', emojis: [
        '❤️','🧡','💛','💚','💙','💜','🖤','🤍','🤎','💔',
        '❤️‍🔥','❤️‍🩹','❣️','💕','💞','💓','💗','💖','💘','💝',
        '💟','♥️','🫶',
    ]},
    { cat: 'Objects', emojis: [
        '🔥','✨','⭐','🌟','💫','🎉','🎊','🎈','🎁','🏆',
        '🏅','🥇','🥈','🥉','⚽','🏀','🎯','🎮','🎲','🔔',
        '🎵','🎶','🎤','🎧','📱','💻','⌨️','🖥️','📷','📹',
        '💡','🔦','📚','📖','✏️','📝','📌','📎','🔑','🔒',
        '🔓','🛠️','⚙️','🧲','💣','🧨','💊','🩹','🧪','🔬',
    ]},
    { cat: 'Nature', emojis: [
        '🌞','🌝','🌛','🌜','🌚','🌕','🌖','🌗','🌑','🌒',
        '⭐','🌟','🌠','☁️','⛅','🌤️','🌥️','🌦️','🌧️','⛈️',
        '🌩️','🌈','☀️','🌊','🔥','❄️','☃️','⛄','🌸','🌺',
        '🌻','🌹','🌷','🌱','🌲','🌳','🌴','🍀','🍁','🍂',
        '🍃','🐶','🐱','🐭','🐰','🦊','🐻','🐼','🐸','🐵',
    ]},
    { cat: 'Food', emojis: [
        '🍎','🍐','🍊','🍋','🍌','🍉','🍇','🍓','🫐','🍒',
        '🍑','🥭','🍍','🥥','🥝','🍅','🥑','🌽','🌶️','🫑',
        '🥕','🧅','🧄','🥔','🍞','🥐','🥖','🧀','🥚','🍳',
        '🥞','🧇','🥓','🍔','🍟','🍕','🌭','🌮','🌯','🥗',
        '🍝','🍜','🍲','🍣','🍱','🍩','🍪','🎂','🍰','🧁',
        '☕','🍵','🥤','🍺','🍻','🥂','🍷','🍸','🍹',
    ]},
    { cat: 'Travel', emojis: [
        '🚗','🚕','🚙','🚌','🚎','🏎️','🚓','🚑','🚒','🚐',
        '🚚','🚛','🚜','🛵','🏍️','🚲','🛴','✈️','🚀','🛸',
        '🚁','🛶','⛵','🚤','🛳️','⛴️','🏠','🏡','🏢','🏰',
        '🗼','🗽','⛪','🕌','🏖️','🏝️','🏔️','⛰️','🌋','🗻',
    ]},
    { cat: 'Flags', emojis: [
        '🏁','🚩','🎌','🏴','🏳️','🏳️‍🌈','🏳️‍⚧️','🏴‍☠️',
    ]},
    { cat: 'Symbols', emojis: [
        '✅','❌','❓','❗','‼️','⁉️','💯','🔴','🟠','🟡',
        '🟢','🔵','🟣','⚫','⚪','🟤','🔶','🔷','🔸','🔹',
        '▪️','▫️','◾','◽','⬛','⬜','♠️','♣️','♥️','♦️',
        '🔀','🔁','🔂','▶️','⏩','⏭️','⏯️','◀️','⏪','⏮️',
        '⏸️','⏹️','⏺️','⏏️','➕','➖','➗','✖️','♾️','💲',
        '©️','®️','™️','🔘','🔳','🔲','⏰','⏱️','⏲️','🕐',
    ]},
];

// Emojis that support skin tone modifiers (hands, pointing, people gestures)
const SKIN_TONE_COMPATIBLE = new Set([
    '👋','🤚','🖐️','✋','🖖','🫱','🫲','🫳','🫴','👌',
    '🤌','🤏','✌️','🤞','🫰','🤟','🤘','🤙','👈','👉',
    '👆','🖕','👇','☝️','🫵','👍','👎','✊','👊','🤛',
    '🤜','👏','🙌','🫶','👐','🤲','🤝','🙏','💪',
    '🤗','🤭','🤫','🫡','🤔','🧐',
]);

function applySkinTone(emoji: string): string {
    const tone = localStorage.getItem('snakk:skin-tone') || '';
    if (!tone || !SKIN_TONE_COMPATIBLE.has(emoji)) return emoji;
    // Strip any existing skin tone modifier before applying new one
    const base = emoji.replace(/[\u{1F3FB}-\u{1F3FF}]/u, '');
    return base + tone;
}

let activeEmojiPicker: HTMLElement | null = null;

function showEmojiPicker(editor: Editor): void {
    closeEmojiPicker();
    closeTableModal();
    closeLinkDialog();
    closeGroupDropdown();
    closeImageModal();
    closeCodeModal();


    // Save cursor position before the picker steals focus
    let savedFrom = 0;
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        savedFrom = view.state.selection.from;
    });

    const picker = document.createElement('div');
    picker.className = 'milkdown-emoji-picker';

    // Category tabs
    const tabBar = document.createElement('div');
    tabBar.className = 'emoji-picker-tabs';
    EMOJI_DATA.forEach((cat, idx) => {
        const tab = document.createElement('button');
        tab.type = 'button';
        tab.className = `emoji-picker-tab${idx === 0 ? ' active' : ''}`;
        tab.textContent = cat.emojis[0]!;
        tab.title = cat.cat;
        tab.dataset.cat = String(idx);
        tabBar.appendChild(tab);
    });
    picker.appendChild(tabBar);

    // Scrollable emoji grid area
    const body = document.createElement('div');
    body.className = 'emoji-picker-body';

    // Build all category sections
    EMOJI_DATA.forEach((cat) => {
        const section = document.createElement('div');
        section.className = 'emoji-picker-section';
        section.dataset.catName = cat.cat;

        const heading = document.createElement('div');
        heading.className = 'emoji-picker-cat-label';
        heading.textContent = cat.cat;
        section.appendChild(heading);

        const grid = document.createElement('div');
        grid.className = 'emoji-picker-grid';

        cat.emojis.forEach((emoji) => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'emoji-picker-item';
            const toned = applySkinTone(emoji);
            btn.textContent = toned;
            btn.title = toned;
            grid.appendChild(btn);
        });

        section.appendChild(grid);
        body.appendChild(section);
    });

    picker.appendChild(body);

    // Prevent ProseMirror from stealing focus when interacting with the picker
    picker.addEventListener('mousedown', (e) => {
        e.stopPropagation();
    });

    // Tab click: scroll to category
    tabBar.addEventListener('click', (e) => {
        const tab = (e.target as HTMLElement).closest('.emoji-picker-tab') as HTMLElement;
        if (!tab) return;
        const idx = parseInt(tab.dataset.cat!);
        const sections = body.querySelectorAll('.emoji-picker-section');
        sections[idx]?.scrollIntoView({ block: 'start', behavior: 'smooth' });
        tabBar.querySelectorAll('.emoji-picker-tab').forEach((t, i) =>
            t.classList.toggle('active', i === idx));
    });

    // Emoji click: insert at saved cursor position
    body.addEventListener('click', (e) => {
        const item = (e.target as HTMLElement).closest('.emoji-picker-item') as HTMLElement;
        if (!item) return;
        const emoji = item.textContent!;

        editor.action((ctx) => {
            const view = ctx.get(editorViewCtx);
            const tr = view.state.tr.insertText(emoji, savedFrom);
            view.dispatch(tr);
            // Advance saved position past the inserted emoji
            savedFrom += emoji.length;
            view.focus();
        });

        closeEmojiPicker();
    });

    // Mount on document.body with fixed positioning to fully isolate
    // from ProseMirror's focus management (same proven approach as link dialog)
    const editorWrapper = document.querySelector('.milkdown-editor') as HTMLElement;
    if (editorWrapper) {
        document.body.appendChild(picker);
        activeEmojiPicker = picker;

        const emojiBtn = editorWrapper.querySelector('.toolbar-emoji') as HTMLElement;
        if (emojiBtn) {
            const btnRect = emojiBtn.getBoundingClientRect();
            const btnCenterX = btnRect.left + btnRect.width / 2;
            const top = btnRect.bottom + 6;
            const notchOffset = 16;

            const pickerWidth = picker.offsetWidth;
            let left = btnCenterX - notchOffset;
            left = Math.max(8, Math.min(left, window.innerWidth - pickerWidth - 8));

            picker.style.top = `${top}px`;
            picker.style.left = `${left}px`;

            const notchLeft = btnCenterX - left;
            picker.style.setProperty('--notch-left', `${notchLeft}px`);
        }

    }

    // Block ProseMirror from reclaiming focus while the picker is open
    const editorEl = document.querySelector('.ProseMirror') as HTMLElement;
    const blockEditorFocus = (e: FocusEvent) => {
        if (activeEmojiPicker) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    };
    if (editorEl) {
        editorEl.addEventListener('focusin', blockEditorFocus, true);
    }

    // Close on outside click or scroll (but not scrolling inside the picker itself)
    const onOutsideClick = (e: MouseEvent) => {
        if (!picker.contains(e.target as Node)) cleanupPicker();
    };
    const onScroll = (e: Event) => {
        if (e.target instanceof Node && picker.contains(e.target)) return;
        cleanupPicker();
    };
    const cleanupPicker = () => {
        if (editorEl) editorEl.removeEventListener('focusin', blockEditorFocus, true);
        closeEmojiPicker();
        document.removeEventListener('mousedown', onOutsideClick);
        window.removeEventListener('scroll', onScroll, true);
    };
    setTimeout(() => {
        document.addEventListener('mousedown', onOutsideClick);
        window.addEventListener('scroll', onScroll, true);
    }, 0);
}

function closeEmojiPicker(): void {
    if (activeEmojiPicker) {
        activeEmojiPicker.remove();
        activeEmojiPicker = null;
    }
}

// ============================================================================
// Element Overlay Controls (delete button on images, hr)
// ============================================================================

let imageOverlays: HTMLElement[] = [];

function updateOverlays(editor: Editor, contentArea: HTMLElement): void {
    removeAllOverlays();

    const contentRect = contentArea.getBoundingClientRect();

    // --- Element overlays (× delete on images, hr) ---
    const elements: { el: HTMLElement; type: string }[] = [];

    contentArea.querySelectorAll('.ProseMirror img').forEach((el) => {
        if ((el as HTMLElement).closest('.sn-single-img-node, .sn-img-group-node')) return;
        const rect = (el as HTMLElement).getBoundingClientRect();
        if (rect.width >= 32 && rect.height >= 32) {
            elements.push({ el: el as HTMLElement, type: 'image' });
        }
    });
    contentArea.querySelectorAll('.ProseMirror hr').forEach((el) => {
        elements.push({ el: el as HTMLElement, type: 'hr' });
    });

    elements.forEach(({ el, type }) => {
        const elRect = el.getBoundingClientRect();

        const overlay = document.createElement('div');
        overlay.className = 'milkdown-element-overlay';
        overlay.contentEditable = 'false';
        overlay.style.top = `${elRect.top - contentRect.top + contentArea.scrollTop}px`;
        overlay.style.left = `${elRect.left - contentRect.left + contentArea.scrollLeft}px`;
        overlay.style.width = `${elRect.width}px`;
        overlay.style.height = `${elRect.height}px`;

        const deleteBtn = document.createElement('button');
        deleteBtn.type = 'button';
        deleteBtn.className = type === 'hr' ? 'element-ctl-delete-hr' : 'element-ctl-delete';
        deleteBtn.title = `Remove ${type}`;
        deleteBtn.textContent = '\u00d7';
        deleteBtn.addEventListener('mousedown', (e) => {
            e.preventDefault();

            editor.action((ctx) => {
                const view = ctx.get(editorViewCtx);
                const pos = view.posAtDOM(el, 0);
                const $pos = view.state.doc.resolve(pos);

                // Walk up to find the block-level node to delete
                for (let d = $pos.depth; d >= 0; d--) {
                    const node = $pos.node(d);
                    if (
                        (type === 'image' && node.type.name === 'image')
                        || (type === 'hr' && node.type.name === 'hr')
                    ) {
                        const start = $pos.before(d);
                        view.dispatch(view.state.tr.delete(start, start + node.nodeSize));
                        view.focus();
                        return;
                    }
                }

                // Fallback: try nodeAt for inline nodes like image
                const node = view.state.doc.nodeAt(pos);
                if (node) {
                    view.dispatch(view.state.tr.delete(pos, pos + node.nodeSize));
                }
                view.focus();
            });
        });
        overlay.appendChild(deleteBtn);

        contentArea.appendChild(overlay);
        imageOverlays.push(overlay);
    });
}

function removeAllOverlays(): void {
    imageOverlays.forEach(o => o.remove());
    imageOverlays = [];
}

// ============================================================================
// Markdown Help Modal
// ============================================================================

let activeHelpModal: HTMLElement | null = null;

function showMarkdownHelp(): void {
    if (activeHelpModal) {
        closeMarkdownHelp();
        return;
    }

    const rows = [
        ['**bold**', '<strong>bold</strong>'],
        ['*italic*', '<em>italic</em>'],
        ['~~strikethrough~~', '<del>strikethrough</del>'],
        ['~subscript~', '<sub>subscript</sub>'],
        ['^superscript^', '<sup>superscript</sup>'],
        ['++inserted++', '<ins>inserted</ins>'],
        ['==highlighted==', '<mark>highlighted</mark>'],
        ['`inline code`', '<code>inline code</code>'],
        ['[link text](url)', '<a href="#">link text</a>'],
        ['![alt](image-url)', '<em>(embedded image)</em>'],
        ['# Heading 1', '<strong style="font-size:1.25em">Heading 1</strong>'],
        ['## Heading 2', '<strong style="font-size:1.1em">Heading 2</strong>'],
        ['### Heading 3', '<strong>Heading 3</strong>'],
        ['- item', '&bull; item'],
        ['1. item', '1. item'],
        ['> blockquote', '<span style="border-left:3px solid currentColor;padding-left:0.5em;opacity:0.7">blockquote</span>'],
        ['---', '<hr style="margin:0.25rem 0"/>'],
        ['```\\ncode block\\n```', '<code style="display:block;padding:0.25em 0.5em;background:var(--bg-tertiary);border-radius:4px">code block</code>'],
        ['- [x] task done', '&#9745; task done'],
        ['- [ ] task todo', '&#9744; task todo'],
        ['| A | B |\\n|---|---|\\n| 1 | 2 |', '<span style="opacity:0.7">(table)</span>'],
    ];

    const backdrop = document.createElement('div');
    backdrop.className = 'md-help-backdrop';

    const modal = document.createElement('div');
    modal.className = 'md-help-modal';

    const header = document.createElement('div');
    header.className = 'md-help-header';
    header.innerHTML = '<span>Markdown Reference</span>';
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'md-help-close';
    closeBtn.textContent = '\u00d7';
    closeBtn.addEventListener('click', closeMarkdownHelp);
    header.appendChild(closeBtn);

    const body = document.createElement('div');
    body.className = 'md-help-body';

    const table = document.createElement('table');
    table.innerHTML = '<thead><tr><th>You type</th><th>You get</th></tr></thead>';
    const tbody = document.createElement('tbody');

    for (const [syntax, result] of rows) {
        const tr = document.createElement('tr');
        const tdSyntax = document.createElement('td');
        const codeEl = document.createElement('code');
        codeEl.textContent = syntax.replace(/\\n/g, '\n');
        tdSyntax.appendChild(codeEl);
        const tdResult = document.createElement('td');
        tdResult.innerHTML = result;
        tr.appendChild(tdSyntax);
        tr.appendChild(tdResult);
        tbody.appendChild(tr);
    }

    table.appendChild(tbody);
    body.appendChild(table);
    modal.appendChild(header);
    modal.appendChild(body);

    backdrop.addEventListener('click', (e) => {
        if (e.target === backdrop) closeMarkdownHelp();
    });

    const onEsc = (e: KeyboardEvent) => {
        if (e.key === 'Escape') closeMarkdownHelp();
    };
    document.addEventListener('keydown', onEsc);

    document.body.appendChild(backdrop);
    document.body.appendChild(modal);
    activeHelpModal = modal;
    lockBodyScroll();
    (activeHelpModal as any).__backdrop = backdrop;
    (activeHelpModal as any).__escHandler = onEsc;
}

function closeMarkdownHelp(): void {
    if (!activeHelpModal) return;
    const backdrop = (activeHelpModal as any).__backdrop as HTMLElement;
    const onEsc = (activeHelpModal as any).__escHandler as (e: KeyboardEvent) => void;
    backdrop?.remove();
    if (onEsc) document.removeEventListener('keydown', onEsc);
    activeHelpModal.remove();
    activeHelpModal = null;
    unlockBodyScroll();
}

// ============================================================================
// Editor Expand / Collapse
// ============================================================================

const EXPAND_SVG = '<span class="icon icon-code-expand" style="width:12px;height:12px" aria-hidden="true"></span>';
const COLLAPSE_SVG = '<span class="icon icon-code-collapse" style="width:12px;height:12px" aria-hidden="true"></span>';

// Shared expand/collapse button for all modals (code, image, table, chart editor).
// Returns a button sized like .sn-modal-close that toggles .sn-modal-expanded
// on the given modal element and saves/restores any inline maxWidth.
function createModalExpandBtn(modal: HTMLElement): HTMLButtonElement {
    let savedMaxWidth = '';

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'sn-modal-close';
    btn.title = 'Expand';
    btn.innerHTML = EXPAND_SVG;

    btn.addEventListener('click', () => {
        if (modal.classList.contains('sn-modal-expanded')) {
            modal.classList.remove('sn-modal-expanded');
            modal.style.maxWidth = savedMaxWidth;
            btn.innerHTML = EXPAND_SVG;
            btn.title = 'Expand';
        } else {
            savedMaxWidth = modal.style.maxWidth;
            modal.style.maxWidth = '';
            modal.classList.add('sn-modal-expanded');
            btn.innerHTML = COLLAPSE_SVG;
            btn.title = 'Collapse';
        }
    });

    return btn;
}

let expandBackdrop: HTMLElement | null = null;
let expandPlaceholder: HTMLElement | null = null;
let expandEscapeHandler: ((e: KeyboardEvent) => void) | null = null;

function toggleEditorExpand(btn: HTMLElement): void {
    const wrapper = btn.closest('.milkdown-editor') as HTMLElement;
    if (!wrapper) return;

    const isExpanding = !wrapper.classList.contains('milkdown-expanded');

    if (isExpanding) {
        // Placeholder to keep layout space
        const rect = wrapper.getBoundingClientRect();
        const placeholder = document.createElement('div');
        placeholder.style.height = `${rect.height}px`;
        wrapper.parentElement?.insertBefore(placeholder, wrapper);
        expandPlaceholder = placeholder;

        wrapper.classList.add('milkdown-expanded');
        lockBodyScroll();
        btn.innerHTML = COLLAPSE_SVG;
        btn.title = 'Collapse editor';

        // Backdrop
        const backdrop = document.createElement('div');
        backdrop.className = 'milkdown-backdrop';
        backdrop.addEventListener('click', () => toggleEditorExpand(btn));
        document.body.appendChild(backdrop);
        expandBackdrop = backdrop;

        // Reposition table overlay after layout shift
        requestAnimationFrame(() => {
            const pm = wrapper.querySelector('.ProseMirror') as HTMLElement;
            pm?.dispatchEvent(new Event('mouseup', { bubbles: true }));
        });

        // Escape to collapse — but not when a modal is open (each modal handles its own ESC)
        expandEscapeHandler = (e: KeyboardEvent) => {
            if (e.key !== 'Escape') return;
            if (activeChartModal || activeImageModal || activeCodeModal || activeTableModal || activeListModal) return;
            toggleEditorExpand(btn);
        };
        document.addEventListener('keydown', expandEscapeHandler);
    } else {
        wrapper.classList.remove('milkdown-expanded');
        unlockBodyScroll();
        btn.innerHTML = EXPAND_SVG;
        btn.title = 'Expand editor';

        expandBackdrop?.remove();
        expandBackdrop = null;
        expandPlaceholder?.remove();
        expandPlaceholder = null;

        if (expandEscapeHandler) {
            document.removeEventListener('keydown', expandEscapeHandler);
            expandEscapeHandler = null;
        }

        // Reposition table overlay after layout shift
        requestAnimationFrame(() => {
            const pm = wrapper.querySelector('.ProseMirror') as HTMLElement;
            pm?.dispatchEvent(new Event('mouseup', { bubbles: true }));
        });
    }
}

interface ToolbarButtonRef {
    btn: HTMLButtonElement;
    allowInBlock: boolean;
}

function createToolbarDOM(editor: Editor, options?: { allowedFeatures?: string[] }): { toolbar: HTMLElement; buttons: ToolbarButtonRef[] } {
    const toolbar = document.createElement('div');
    toolbar.className = 'milkdown-toolbar';
    const buttons: ToolbarButtonRef[] = [];

    function isAllowed(feature?: string): boolean {
        return !options?.allowedFeatures || !feature || options.allowedFeatures.includes(feature);
    }

    // Collect pending separator so we can suppress leading/consecutive ones
    let pendingSep = false;

    for (const item of buildToolbarItems()) {
        if ('separator' in item && item.separator) {
            pendingSep = true;
            continue;
        }

        const feature = 'feature' in item ? item.feature : undefined;
        if (!isAllowed(feature)) continue;

        if (pendingSep && toolbar.children.length > 0) {
            const sep = document.createElement('span');
            sep.className = 'milkdown-toolbar-separator';
            toolbar.appendChild(sep);
        }
        pendingSep = false;

        if ('groupIcon' in item) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = `milkdown-toolbar-btn toolbar-group-btn${item.groupClassName ? ` ${item.groupClassName}` : ''}`;
            btn.title = item.groupTitle;

            const iconSpan = document.createElement('span');
            iconSpan.textContent = item.groupIcon;
            btn.appendChild(iconSpan);

            const chevron = document.createElement('span');
            chevron.className = 'toolbar-chevron';
            chevron.textContent = '▾';
            btn.appendChild(chevron);

            btn.addEventListener('mousedown', (e) => {
                e.preventDefault();
                if (!btn.classList.contains('toolbar-disabled')) {
                    showGroupDropdown(editor, btn, item.children);
                }
            });
            toolbar.appendChild(btn);

            const allAllowInTable = item.children.every((c) => c.allowInBlock === true);
            buttons.push({ btn, allowInBlock: allAllowInTable });
            continue;
        }

        const btn = document.createElement('button');
        btn.type = 'button';
        const dropdownClass = item.hasDropdown ? ' toolbar-group-btn' : '';
        btn.className = `milkdown-toolbar-btn${dropdownClass}${item.className ? ` ${item.className}` : ''}`;
        btn.title = item.title;

        if (item.hasDropdown) {
            const iconSpan = document.createElement('span');
            iconSpan.textContent = item.icon;
            btn.appendChild(iconSpan);
            const chevron = document.createElement('span');
            chevron.className = 'toolbar-chevron';
            chevron.textContent = '▾';
            btn.appendChild(chevron);
        } else {
            btn.textContent = item.icon;
        }

        btn.addEventListener('mousedown', (e) => {
            if (!item.opensDialog) e.preventDefault(); // Prevent editor blur (skip for dialog buttons)
            if (!btn.classList.contains('toolbar-disabled')) {
                item.action(editor);
            }
        });
        toolbar.appendChild(btn);
        buttons.push({ btn, allowInBlock: item.allowInBlock === true });
    }

    // Spacer + expand button (right-aligned)
    const spacer = document.createElement('span');
    spacer.style.flex = '1';
    toolbar.appendChild(spacer);

    const expandBtn = document.createElement('button');
    expandBtn.type = 'button';
    expandBtn.className = 'milkdown-toolbar-btn toolbar-expand';
    expandBtn.title = 'Expand editor';
    expandBtn.innerHTML = EXPAND_SVG;
    expandBtn.addEventListener('mousedown', (e) => {
        e.preventDefault();
        toggleEditorExpand(expandBtn);
    });
    toolbar.appendChild(expandBtn);

    return { toolbar, buttons };
}

function isCursorInRestrictedContext(editor: Editor): boolean {
    let restricted = false;
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const { state } = view;
        const { $from } = state.selection;
        const blockTypes = new Set([
            'bullet_list', 'ordered_list', 'task_list_item',
            'blockquote', 'code_block', 'table',
        ]);
        for (let d = $from.depth; d > 0; d--) {
            if (blockTypes.has($from.node(d).type.name)) {
                restricted = true;
                return;
            }
        }
        const codeMarkType = state.schema.marks['code'];
        if (codeMarkType && $from.marks().some(m => m.type === codeMarkType)) {
            restricted = true;
        }
    });
    return restricted;
}

function updateToolbarState(editor: Editor, buttons: ToolbarButtonRef[], contentArea: HTMLElement): void {
    const restricted = isCursorInRestrictedContext(editor);
    for (const { btn, allowInBlock } of buttons) {
        btn.classList.toggle('toolbar-disabled', restricted && !allowInBlock);
    }

    updateOverlays(editor, contentArea);
}

// ============================================================================
// Block Adder — Notion-style "+" button in the left gutter
// ============================================================================

function insertAtEmptyPara(editor: Editor, paraPos: number, insertFn: () => void): void {
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const tr = view.state.tr.setSelection(
            TextSelection.create(view.state.doc, paraPos + 1)
        );
        view.dispatch(tr);
    });
    insertFn();
}

function createBlockAdder(editor: Editor, editorRoot: HTMLElement): void {
    let hoveredBlockEl: Element | null = null;
    let hoveredBlockPos: number | null = null;
    let cursorParaPos: number | null = null;
    let pickerOpen = false;

    const btn = document.createElement('div');
    btn.className = 'sn-block-adder';
    btn.textContent = '+';
    editorRoot.appendChild(btn);

    function updateButtonPosition(blockEl: Element): void {
        const contentRect = editorRoot.getBoundingClientRect();
        const blockRect = blockEl.getBoundingClientRect();
        btn.style.top = (blockRect.top - contentRect.top + editorRoot.scrollTop) + 'px';
        btn.style.display = 'flex';
    }

    function getElForCursorPos(pos: number): Element | null {
        try {
            return editor.action((ctx) => {
                const view = ctx.get(editorViewCtx);
                const domInfo = view.domAtPos(pos + 1);
                const node = domInfo.node;
                if (node instanceof Element) return node;
                return node instanceof Node ? (node.parentElement ?? null) : null;
            }) ?? null;
        } catch {
            return null;
        }
    }

    function getActiveSource(): { el: Element; pos: number } | null {
        if (hoveredBlockEl && hoveredBlockPos !== null) return { el: hoveredBlockEl, pos: hoveredBlockPos };
        if (cursorParaPos !== null) {
            const el = getElForCursorPos(cursorParaPos);
            if (el) return { el, pos: cursorParaPos };
        }
        return null;
    }

    function syncButton(): void {
        if (pickerOpen) return;
        const src = getActiveSource();
        if (src) updateButtonPosition(src.el);
        else btn.style.display = 'none';
    }

    function getBlockAndPosAtY(clientY: number): { el: Element; pos: number } | null {
        const pm = editorRoot.querySelector('.ProseMirror');
        if (!pm) return null;
        for (const child of Array.from(pm.children)) {
            const rect = child.getBoundingClientRect();
            if (clientY >= rect.top && clientY <= rect.bottom) {
                try {
                    const pos = editor.action((ctx) => {
                        const view = ctx.get(editorViewCtx);
                        const rawPos = view.posAtDOM(child, 0);
                        const $pos = view.state.doc.resolve(rawPos);
                        if ($pos.parent.type.name !== 'paragraph' || $pos.parent.content.size !== 0) {
                            return null;
                        }
                        return $pos.depth > 0 ? $pos.before($pos.depth) : 0;
                    });
                    if (pos === null) return null;
                    return { el: child, pos };
                } catch {
                    return null;
                }
            }
        }
        return null;
    }

    editorRoot.addEventListener('mousemove', (e) => {
        if (pickerOpen) return;
        const hit = getBlockAndPosAtY(e.clientY);
        hoveredBlockEl = hit?.el ?? null;
        hoveredBlockPos = hit?.pos ?? null;
        syncButton();
    });

    editorRoot.addEventListener('mouseleave', () => {
        hoveredBlockEl = null;
        hoveredBlockPos = null;
        syncButton();
    });

    editorRoot.addEventListener('scroll', () => {
        if (pickerOpen) return;
        const src = getActiveSource();
        if (src) updateButtonPosition(src.el);
    });

    type PickerLeafItem  = { icon: string; label: string; action: (pos: number) => void; disabled?: boolean };
    type PickerGroupItem = { icon: string; label: string; children: PickerLeafItem[]; disabled?: boolean };
    type PickerSeparator = { separator: true };
    type PickerItem = PickerLeafItem | PickerGroupItem | PickerSeparator;

    const chartChildren: PickerLeafItem[] = [
        { icon: '≡',  label: 'Bar',         action: (pos) => insertAtEmptyPara(editor, pos, () => showChartEditorModal('bar',     { onConfirm: (c) => insertChartBlock(editor, c) })) },
        { icon: '∿',  label: 'Line',        action: (pos) => insertAtEmptyPara(editor, pos, () => showChartEditorModal('line',    { onConfirm: (c) => insertChartBlock(editor, c) })) },
        { icon: '◿',  label: 'Area',        action: (pos) => insertAtEmptyPara(editor, pos, () => showChartEditorModal('area',    { onConfirm: (c) => insertChartBlock(editor, c) })) },
        { icon: '▤',  label: 'Stacked Bar', action: (pos) => insertAtEmptyPara(editor, pos, () => showChartEditorModal('stacked', { onConfirm: (c) => insertChartBlock(editor, c) })) },
        { icon: '◔',  label: 'Pie',         action: (pos) => insertAtEmptyPara(editor, pos, () => showChartEditorModal('pie',     { onConfirm: (c) => insertChartBlock(editor, c) })) },
        { icon: '◎',  label: 'Donut',       action: (pos) => insertAtEmptyPara(editor, pos, () => showChartEditorModal('donut',   { onConfirm: (c) => insertChartBlock(editor, c) })) },
        { icon: '∷',  label: 'Scatter',     action: (pos) => insertAtEmptyPara(editor, pos, () => showChartEditorModal('scatter', { onConfirm: (c) => insertChartBlock(editor, c) })) },
    ];

    const listChildren: PickerLeafItem[] = [
        { icon: '•',  label: 'Bullet List',  action: (pos) => insertAtEmptyPara(editor, pos, () => showListEditorModal('bullet',  { onConfirm: (data) => insertListBlock(editor, data) })) },
        { icon: '1.', label: 'Ordered List', action: (pos) => insertAtEmptyPara(editor, pos, () => showListEditorModal('ordered', { onConfirm: (data) => insertListBlock(editor, data) })) },
        { icon: '☑',  label: 'Checklist',   action: (pos) => insertAtEmptyPara(editor, pos, () => showListEditorModal('task',    { onConfirm: (data) => insertListBlock(editor, data) })) },
    ];

    const calloutChildren: PickerLeafItem[] = [
        { icon: 'ℹ',  label: 'Info',    action: (pos) => insertAtEmptyPara(editor, pos, () => insertCallout(editor, 'info')) },
        { icon: '⚠',  label: 'Warning', action: (pos) => insertAtEmptyPara(editor, pos, () => insertCallout(editor, 'warning')) },
        { icon: '💡', label: 'Tip',     action: (pos) => insertAtEmptyPara(editor, pos, () => insertCallout(editor, 'tip')) },
        { icon: '📝', label: 'Note',    action: (pos) => insertAtEmptyPara(editor, pos, () => insertCallout(editor, 'note')) },
    ];

    const pickerItems: PickerItem[] = [
        { icon: '❝',  label: 'Blockquote',    action: (pos) => insertAtEmptyPara(editor, pos, () => editor.action(callCommand(wrapInBlockquoteCommand.key))) },
        { icon: '🖼',  label: 'Image',         action: (pos) => insertAtEmptyPara(editor, pos, () => showImageModal({ editor, externalImageCount: (window as any).SnakkGalleryNew?.getImageCount() ?? 0 })) },
        { icon: '{ }', label: 'Code snippet',  action: (pos) => insertAtEmptyPara(editor, pos, () => showCodeModal({ editor })) },
        { icon: '⊞',  label: 'Table',         action: (pos) => insertAtEmptyPara(editor, pos, () => showTableModal({ onConfirm: (d) => insertTable(editor, d) })) },
        { separator: true },
        { icon: '☰',  label: 'List',          children: listChildren },
        { icon: '📊',  label: 'Chart',         children: chartChildren },
        { icon: '💬',  label: 'Callout',       children: calloutChildren },
        { separator: true },
        { icon: '—',  label: 'Horizontal Rule', action: (pos) => insertAtEmptyPara(editor, pos, () => editor.action(callCommand(insertHrCommand.key))) },
    ];

    function populatePicker(picker: HTMLElement, capturedPos: number, items: PickerItem[], onBack?: () => void): void {
        while (picker.firstChild) picker.removeChild(picker.firstChild);

        if (onBack) {
            const backRow = document.createElement('div');
            backRow.className = 'sn-block-picker-item sn-block-picker-back';
            backRow.addEventListener('mousedown', (ev) => { ev.preventDefault(); ev.stopPropagation(); onBack(); });
            const backIcon = document.createElement('span');
            backIcon.className = 'sn-picker-icon';
            backIcon.textContent = '←';
            const backLabel = document.createElement('span');
            backLabel.textContent = T.editor.back;
            backRow.appendChild(backIcon);
            backRow.appendChild(backLabel);
            picker.appendChild(backRow);
            const backSep = document.createElement('div');
            backSep.className = 'sn-block-picker-separator';
            picker.appendChild(backSep);
        }

        items.forEach(item => {
            if ('separator' in item) {
                const sep = document.createElement('div');
                sep.className = 'sn-block-picker-separator';
                picker.appendChild(sep);
                return;
            }
            const row = document.createElement('div');
            row.className = 'sn-block-picker-item';
            if (item.disabled) row.classList.add('sn-block-picker-item--disabled');
            const icon = document.createElement('span');
            icon.className = 'sn-picker-icon';
            icon.textContent = item.icon;
            const label = document.createElement('span');
            label.textContent = item.label;
            row.appendChild(icon);
            row.appendChild(label);
            if (!item.disabled) {
                if ('children' in item) {
                    const chevron = document.createElement('span');
                    chevron.className = 'sn-picker-chevron';
                    chevron.textContent = '›';
                    row.appendChild(chevron);
                    row.addEventListener('mousedown', (ev) => {
                        ev.preventDefault();
                        ev.stopPropagation();
                        populatePicker(picker, capturedPos, item.children, () => populatePicker(picker, capturedPos, items, onBack));
                    });
                } else {
                    row.addEventListener('mousedown', (ev) => {
                        ev.preventDefault();
                        ev.stopPropagation();
                        closeBlockPicker();
                        item.action(capturedPos);
                    });
                }
            }
            picker.appendChild(row);
        });
    }

    function openPickerAt(anchorEl: HTMLElement, capturedPos: number): void {
        closeBlockPicker();
        pickerOpen = true;
        const picker = document.createElement('div');
        picker.className = 'sn-block-picker';
        activeBlockPicker = picker;
        const MAX_BLOCKS = 3;
        let blockCounts: BlockCounts = { charts: 0, codeBlocks: 0, lists: 0, callouts: 0, hrs: 0, tables: 0, blockquotes: 0, imageBlocks: 0 };
        editor.action((ctx) => { blockCounts = countBlocksInEditor(ctx.get(editorViewCtx)); });

        const constrainedItems: PickerItem[] = pickerItems.map(item => {
            if ('separator' in item) return item;
            const disabled =
                (item.label === 'Code snippet'     && blockCounts.codeBlocks  >= MAX_BLOCKS) ||
                (item.label === 'Chart'            && blockCounts.charts      >= MAX_BLOCKS) ||
                (item.label === 'Callout'          && blockCounts.callouts    >= MAX_BLOCKS) ||
                (item.label === 'List'             && blockCounts.lists       >= MAX_BLOCKS) ||
                (item.label === 'Blockquote'       && blockCounts.blockquotes >= MAX_BLOCKS) ||
                (item.label === 'Horizontal Rule'  && blockCounts.hrs         >= MAX_BLOCKS) ||
                (item.label === 'Table'            && blockCounts.tables      >= MAX_BLOCKS) ||
                (item.label === 'Image'            && blockCounts.imageBlocks >= MAX_BLOCKS);
            return disabled ? { ...item, disabled: true } : item;
        });

        populatePicker(picker, capturedPos, constrainedItems);
        document.body.appendChild(picker);

        const anchorRect = anchorEl.getBoundingClientRect();
        let top = anchorRect.bottom + 4;
        let left = anchorRect.left;
        picker.style.top = `${top}px`;
        picker.style.left = `${left}px`;
        const pickerRect = picker.getBoundingClientRect();
        if (pickerRect.right > window.innerWidth - 8) {
            left = window.innerWidth - pickerRect.width - 8;
            picker.style.left = `${left}px`;
        }
        if (pickerRect.bottom > window.innerHeight - 8) {
            picker.style.top = '';
            picker.style.bottom = `${window.innerHeight - anchorRect.top + 4}px`;
        }

        const onOutside = (ev: MouseEvent) => {
            if (!picker.contains(ev.target as Node) && ev.target !== anchorEl) closeBlockPicker();
        };
        const onEsc = (ev: KeyboardEvent) => { if (ev.key === 'Escape') closeBlockPicker(); };
        const onScroll = (ev: Event) => {
            if (ev.target instanceof Node && picker.contains(ev.target)) return;
            closeBlockPicker();
        };
        blockPickerCleanup = () => {
            document.removeEventListener('mousedown', onOutside);
            document.removeEventListener('keydown', onEsc);
            window.removeEventListener('scroll', onScroll, true);
            pickerOpen = false;
        };
        setTimeout(() => {
            document.addEventListener('mousedown', onOutside);
            document.addEventListener('keydown', onEsc);
            window.addEventListener('scroll', onScroll, true);
        }, 0);
    }

    btn.addEventListener('mousedown', (e) => {
        e.preventDefault();
        e.stopPropagation();
        const capturedPos = hoveredBlockPos ?? cursorParaPos;
        if (capturedPos === null) return;
        openPickerAt(btn, capturedPos);
    });

    // Always-visible end-of-content "add block" button
    const endBtn = document.createElement('div');
    endBtn.className = 'sn-block-adder sn-block-adder-end';
    endBtn.textContent = '+';
    editorRoot.appendChild(endBtn);

    function updateEndButton(): void {
        if (pickerOpen) return;
        const pm = editorRoot.querySelector('.ProseMirror');
        if (!pm || !pm.lastElementChild) { endBtn.style.display = 'none'; return; }
        const lastIsEmptyPara = editor.action((ctx) => {
            const view = ctx.get(editorViewCtx);
            const last = view.state.doc.lastChild;
            return last?.type.name === 'paragraph' && last.content.size === 0;
        }) ?? false;
        // Hide when last line is already an empty paragraph — the hover "+" covers that case
        if (lastIsEmptyPara) { endBtn.style.display = 'none'; return; }
        const contentRect = editorRoot.getBoundingClientRect();
        const lastRect = pm.lastElementChild.getBoundingClientRect();
        endBtn.style.top = `${lastRect.bottom - contentRect.top + editorRoot.scrollTop}px`;
        endBtn.style.display = 'flex';
    }

    let endBtnRafId: number | null = null;
    const scheduleEndBtnUpdate = () => {
        if (endBtnRafId !== null) return;
        endBtnRafId = requestAnimationFrame(() => { endBtnRafId = null; updateEndButton(); });
    };
    const pmObs = editorRoot.querySelector('.ProseMirror');
    if (pmObs) {
        const mo = new MutationObserver(scheduleEndBtnUpdate);
        mo.observe(pmObs, { childList: true, subtree: true, characterData: true });
    }
    editorRoot.addEventListener('scroll', scheduleEndBtnUpdate);
    updateEndButton();

    endBtn.addEventListener('mousedown', (e) => {
        e.preventDefault();
        e.stopPropagation();
        const capturedPos = editor.action((ctx) => {
            const view = ctx.get(editorViewCtx);
            const { state } = view;
            const { doc, schema } = state;
            const last = doc.lastChild;
            if (last?.type.name === 'paragraph' && last.content.size === 0) {
                return doc.content.size - last.nodeSize;
            }
            const para = schema.nodes.paragraph!.create();
            const endPos = doc.content.size;
            view.dispatch(state.tr.insert(endPos, para));
            return endPos;
        });
        openPickerAt(endBtn, capturedPos);
    });

    setEmptyParaCursorCallback((pos) => {
        cursorParaPos = pos;
        syncButton();
    });
}

// ============================================================================
// Editor Implementation
// ============================================================================

(function(): void {
    'use strict';

    if ((window as any).SnakkEditor) return;

    const instances = new Map<HTMLElement, { editor: Editor; wrapper: EditorInstance }>();

    async function init(options: SnakkEditorOptions): Promise<EditorInstance | null> {
        const { container, textarea, placeholder, initialValue, height, onChange, onUploadStateChange, allowedFeatures } = options;

        // Already initialized for this container
        const existing = instances.get(container);
        if (existing) return existing.wrapper;

        try {
            // Clear skeleton content
            container.innerHTML = '';

            // Create the editor wrapper with toolbar + editing area
            const editorWrapper = document.createElement('div');
            editorWrapper.className = 'milkdown-editor';

            const editorRoot = document.createElement('div');
            editorRoot.className = 'milkdown-editor-content';
            if (height) editorRoot.style.minHeight = height;

            container.appendChild(editorWrapper);

            // Hide the textarea — editor replaces it
            textarea.style.display = 'none';

            const initialContent = initialValue || textarea.value || '';

            const pendingUploads = new Map<string, File>();

            const editor = await Editor.make()
                .config((ctx) => {
                    ctx.set(rootCtx, editorRoot);
                    if (initialContent) {
                        ctx.set(defaultValueCtx, initialContent);
                    }

                    // Reserve single ~ for subscript (Markdig), only ~~ for strikethrough
                    ctx.set(remarkGFMPlugin.options.key, { singleTilde: false });

                    // Listener for content changes — sync to hidden textarea
                    ctx.get(listenerCtx)
                        .markdownUpdated((_ctx, markdown) => {
                            const cleaned = cleanTableMarkdown(
                                markdown
                                    // Strip <br /> inside table rows before generic replacement
                                    .replace(/^(\s*\|.+\|)\s*$/gm, (line) => line.replace(/<br\s*\/?>/g, ''))
                                    .replace(/<br\s*\/?>\n?/g, '\n')
                                    .replace(/^(#{1,6}) \n+/gm, '$1 ')
                                    .replace(/^(```\w*)\n\n+/gm, '$1\n')
                            );
                            textarea.value = cleaned;
                            onChange?.(cleaned);
                        });

                    // Defer paste/drop images: insert blob URL immediately, upload on submit.
                    ctx.update(uploadConfig.key, (prev) => ({
                        ...prev,
                        uploader: async (files, schema) => {
                            const nodes: Node[] = [];
                            for (let i = 0; i < files.length; i++) {
                                const file = files.item(i);
                                if (!file || !file.type.startsWith('image/')) continue;
                                if (file.size > MAX_FILE_BYTES) {
                                    alert(`"${file.name}" is too large (${(file.size / 1024 / 1024).toFixed(1)} MB). Maximum is 5 MB.`);
                                    continue;
                                }
                                const blobUrl = URL.createObjectURL(file);
                                pendingUploads.set(blobUrl, file);
                                const node = schema.nodes.image?.createAndFill({
                                    src: blobUrl,
                                    alt: 'user uploaded image',
                                });
                                if (node) nodes.push(node);
                            }
                            return nodes;
                        },
                    }));
                })
                .use(commonmark)
                .use(gfmSchema)
                .use(gfmInputRules)
                .use(gfmPasteRules)
                // Skip GFM markInputRules (single-tilde strikethrough) — use custom double-tilde rule
                .use(doubleStrikethroughInputRule)
                .use(gfmKeymap)
                .use(gfmCommands)
                .use(gfmPlugins)
                // Emphasis extras: subscript, superscript, inserted, marked
                .use(remarkEmphasisExtras)
                .use(markAttrs)
                .use(markSchemas)
                .use(markInputRules)
                .use(markCommands)
                .use(history)
                .use(listener)
                .use(upload)
                .use(createGapCursorPlugin())
                .use(createGapParaPlugin())
                .use(createEmptyParaCursorPlugin())
                .use(createImageGroupPlugin())
                .use(pasteSanitize)
                .create();

            editorPendingUploads.set(editor, pendingUploads);

            setChartEditCallback((type, initialConfig, onConfirm) => {
                showChartEditorModal(type, { initialConfig, onConfirm, isEdit: true });
            });

            setTableEditCallback((data, onConfirm) => {
                showTableModal({ data, onConfirm, isEdit: true });
            });

            setImageEditCallback((data, view, onConfirm) => {
                showImageModal({ data, view, onConfirm, externalImageCount: (window as any).SnakkGalleryNew?.getImageCount() ?? 0 });
            });

            setCodeEditCallback((data, onConfirm) => {
                showCodeModal({ data, onConfirm });
            });

            setListEditCallback((data, onConfirm) => {
                showListEditorModal(data.type, { data, onConfirm, isEdit: true });
            });

            // Build toolbar, footer and assemble the editor
            const { toolbar, buttons: toolbarButtons } = createToolbarDOM(editor, { allowedFeatures });
            const footer = document.createElement('div');
            footer.className = 'milkdown-footer';

            // Source mode toggle
            const sourceTextarea = document.createElement('textarea');
            sourceTextarea.className = 'milkdown-source';
            sourceTextarea.style.display = 'none';
            sourceTextarea.spellcheck = false;
            if (height) sourceTextarea.style.minHeight = height;

            // Eye icon (visual) / code icon (source)
            const eyeIcon = '<span class="icon icon-eye" style="width:14px;height:14px" aria-hidden="true"></span>';
            const codeIcon = '<span class="icon icon-code" style="width:14px;height:14px" aria-hidden="true"></span>';
            const helpIcon = '<span class="icon icon-question-circle" style="width:14px;height:14px" aria-hidden="true"></span>';

            const modeJoin = document.createElement('div');
            modeJoin.className = 'join';

            const visualBtn = document.createElement('button');
            visualBtn.type = 'button';
            visualBtn.className = 'join-item md-mode-btn';
            visualBtn.innerHTML = eyeIcon + ' Visual';

            const sourceBtn = document.createElement('button');
            sourceBtn.type = 'button';
            sourceBtn.className = 'join-item md-mode-btn';
            sourceBtn.innerHTML = codeIcon + ' Source';

            modeJoin.appendChild(visualBtn);
            modeJoin.appendChild(sourceBtn);

            const helpLink = document.createElement('button');
            helpLink.type = 'button';
            helpLink.className = 'md-footer-btn';
            helpLink.innerHTML = helpIcon + ' Markdown';
            helpLink.addEventListener('click', showMarkdownHelp);

            let isSourceMode = localStorage.getItem('snakk:editor-mode') === 'source';

            // Source mode markdown insertion helper
            function sourceInsert(before: string, after: string = '', blockPrefix = false): void {
                const ta = sourceTextarea;
                const start = ta.selectionStart;
                const end = ta.selectionEnd;
                const selected = ta.value.substring(start, end);

                let insertion: string;
                if (blockPrefix) {
                    if (selected) {
                        insertion = selected.split('\n').map(line => before + line).join('\n');
                    } else {
                        insertion = before;
                    }
                } else {
                    insertion = before + selected + after;
                }

                ta.setRangeText(insertion, start, end, 'select');

                // If no text was selected and there's a closing tag, place cursor between the tags
                if (!selected && after) {
                    const cursorPos = start + before.length;
                    ta.setSelectionRange(cursorPos, cursorPos);
                }

                ta.focus();
                textarea.value = ta.value;
                onChange?.(ta.value);
            }

            // Map toolbar titles to source-mode markdown insertions
            const sourceActions: Record<string, () => void> = {
                'Bold (Ctrl+B)':                () => sourceInsert('**', '**'),
                'Italic (Ctrl+I)':              () => sourceInsert('*', '*'),
                'Strikethrough (Ctrl+Shift+X)': () => sourceInsert('~~', '~~'),
                'Subscript':                    () => sourceInsert('~', '~'),
                'Superscript':                  () => sourceInsert('^', '^'),
                'Underline (Ctrl+U)':           () => sourceInsert('++', '++'),
                'Highlight':                    () => sourceInsert('==', '=='),
                'Spoiler':                      () => sourceInsert('||', '||'),
                'Heading 1':                    () => sourceInsert('# ', '', true),
                'Heading 2':                    () => sourceInsert('## ', '', true),
                'Heading 3':                    () => sourceInsert('### ', '', true),
                'Bullet List':                  () => sourceInsert('- ', '', true),
                'Ordered List':                 () => sourceInsert('1. ', '', true),
                'Task List':                    () => sourceInsert('- [ ] ', '', true),
                'Blockquote':                   () => sourceInsert('> ', '', true),
                'Inline Code':                  () => sourceInsert('`', '`'),
                'Code Block':                   () => sourceInsert('```\n', '\n```'),
                'Horizontal Rule':              () => sourceInsert('\n---\n'),
                'Link':                         () => sourceInsert('[', '](url)'),
                'Table':                        () => sourceInsert('\n| Column 1 | Column 2 |\n| --- | --- |\n| Cell | Cell |\n'),
                'Info':                         () => sourceInsert('```callout-info\n', '\n```'),
                'Warning':                      () => sourceInsert('```callout-warning\n', '\n```'),
                'Tip':                          () => sourceInsert('```callout-tip\n', '\n```'),
                'Note':                         () => sourceInsert('```callout-note\n', '\n```'),
            };

            // Expose source mode state on the wrapper element for dropdown access
            (editorWrapper as any).__sourceActions = sourceActions;
            (editorWrapper as any).__isSourceMode = () => isSourceMode;

            // Intercept toolbar mousedown in source mode (toolbar buttons use mousedown, not click)
            toolbar.addEventListener('mousedown', (e) => {
                if (!isSourceMode) return;

                const btn = (e.target as HTMLElement).closest('button[title]') as HTMLButtonElement | null;
                if (!btn) return;

                const title = btn.getAttribute('title') || '';

                // For group buttons (Headings, Lists, Text Effects) — let dropdown open normally
                if (btn.classList.contains('toolbar-group-btn')) return;

                // For Image button — block in source mode
                if (title === 'Image') {
                    e.stopPropagation();
                    e.preventDefault();
                    return;
                }

                // For Link — show a prompt instead of the visual modal
                if (title === 'Link') {
                    e.stopPropagation();
                    e.preventDefault();
                    const url = prompt('URL:');
                    if (!url) return;
                    const ta = sourceTextarea;
                    const start = ta.selectionStart;
                    const end = ta.selectionEnd;
                    const selected = ta.value.substring(start, end) || 'link text';
                    ta.setRangeText(`[${selected}](${url})`, start, end, 'end');
                    ta.focus();
                    textarea.value = ta.value;
                    onChange?.(ta.value);
                    return;
                }

                const handler = sourceActions[title];
                if (handler) {
                    e.stopPropagation();
                    e.preventDefault();
                    handler();
                }
            }, true); // Capture phase to intercept before toolbar's own mousedown

            // Hide image button in source mode
            const imageBtn = toolbar.querySelector('button[title="Image"]') as HTMLElement | null;

            function applyMode(): void {
                editorWrapper.classList.toggle('milkdown-source-mode', isSourceMode);
                if (isSourceMode) {
                    sourceTextarea.value = textarea.value;
                    editorBody.style.display = 'none';
                    sourceTextarea.style.display = '';
                    sourceBtn.classList.add('md-mode-active');
                    visualBtn.classList.remove('md-mode-active');
                    helpLink.style.display = '';
                    if (imageBtn) imageBtn.style.display = 'none';
                } else {
                    editorBody.style.display = '';
                    sourceTextarea.style.display = 'none';
                    visualBtn.classList.add('md-mode-active');
                    sourceBtn.classList.remove('md-mode-active');
                    helpLink.style.display = 'none';
                    if (imageBtn) imageBtn.style.display = '';
                }
            }

            function switchToSource(): void {
                if (isSourceMode) return;
                sourceTextarea.value = wrapper.getMarkdown();
                isSourceMode = true;
                localStorage.setItem('snakk:editor-mode', 'source');
                applyMode();
            }

            function switchToVisual(): void {
                if (!isSourceMode) return;
                const md = sourceTextarea.value;
                editor.action(replaceAll(md));
                textarea.value = md;
                isSourceMode = false;
                localStorage.setItem('snakk:editor-mode', 'visual');
                applyMode();
            }

            visualBtn.addEventListener('click', switchToVisual);
            sourceBtn.addEventListener('click', switchToSource);

            // Create body wrapper before applyMode() so it can be shown/hidden
            const editorBody = document.createElement('div');
            editorBody.className = 'milkdown-editor-body';

            applyMode();

            // Sync source textarea changes to the hidden form textarea
            sourceTextarea.addEventListener('input', () => {
                textarea.value = sourceTextarea.value;
                onChange?.(sourceTextarea.value);
            });

            const footerLeft = document.createElement('div');
            footerLeft.className = 'md-footer-left';
            footerLeft.appendChild(modeJoin);
            footerLeft.appendChild(helpLink);
            footer.appendChild(footerLeft);

            const messageArea = document.createElement('div');
            messageArea.className = 'milkdown-message-area';

            editorWrapper.appendChild(toolbar);
            editorBody.appendChild(editorRoot);
            editorWrapper.appendChild(editorBody);
            editorWrapper.appendChild(sourceTextarea);
            editorWrapper.appendChild(messageArea);
            editorWrapper.appendChild(footer);

            const blockFeatures = ['image', 'code', 'table', 'blockquote', 'list', 'heading', 'callout'];
            const hasBlockFeature = !allowedFeatures || allowedFeatures.some(f => blockFeatures.includes(f));
            if (hasBlockFeature) createBlockAdder(editor, editorRoot);

            // Update toolbar button states on selection changes (e.g. disable block items in tables)
            const pm = editorRoot.querySelector('.ProseMirror');
            if (pm) {
                const observer = new MutationObserver(() => updateToolbarState(editor, toolbarButtons, editorRoot));
                observer.observe(pm, { childList: true, subtree: true, characterData: true });
            }
            // Also update on click/keyup for cursor moves
            editorRoot.addEventListener('keyup', () => updateToolbarState(editor, toolbarButtons, editorRoot));
            editorRoot.addEventListener('mouseup', () => updateToolbarState(editor, toolbarButtons, editorRoot));

            // Notify caller when image uploads start or finish. Blob-URL images
            // (.sn-img-uploading) and paste/drop placeholders (.milkdown-upload-placeholder)
            // are present in the DOM only while an upload is in flight.
            if (onUploadStateChange) {
                let wasUploading = false;
                const uploadObserver = new MutationObserver(() => {
                    const isUploading =
                        container.querySelector('.sn-img-uploading, .milkdown-upload-placeholder') !== null;
                    if (isUploading !== wasUploading) {
                        wasUploading = isUploading;
                        onUploadStateChange(isUploading);
                    }
                });
                uploadObserver.observe(container, { childList: true, subtree: true });
            }

            // Build the wrapper that matches the SnakkEditor API
            const wrapper: EditorInstance = {
                getMarkdown: () => cleanTableMarkdown(
                    editor.action(getMarkdown())
                        .replace(/^(\s*\|.+\|)\s*$/gm, (line) => line.replace(/<br\s*\/?>/g, ''))
                        .replace(/<br\s*\/?>\n?/g, '\n')
                        .replace(/^(#{1,6}) \n+/gm, '$1 ')
                ),
                setMarkdown: (markdown: string) => {
                    editor.action(replaceAll(markdown));
                    textarea.value = markdown;
                },
                focus: () => {
                    editor.action((ctx) => {
                        const view = ctx.get(editorViewCtx);
                        view.focus();
                    });
                },
                destroy: () => {
                    destroyEditor(container);
                },
                hasPendingUploads: () => pendingUploads.size > 0,
                getImageCount: (): number => {
                    let count = 0;
                    editor.action((ctx) => { count = countImagesFromView(ctx.get(editorViewCtx)); });
                    return count;
                },
                flushUploads: async (onProgress?: (done: number, total: number) => void): Promise<void> => {
                    const entries = [...pendingUploads.entries()];
                    onProgress?.(0, entries.length);
                    let done = 0;
                    for (const [blobUrl, file] of entries) {
                        const result = await uploadFile(file, () => {});
                        URL.revokeObjectURL(blobUrl);
                        pendingUploads.delete(blobUrl);
                        editor.action((ctx) => {
                            const view = ctx.get(editorViewCtx);
                            replaceImageSrc(view, blobUrl, result.url);
                        });
                        done++;
                        onProgress?.(done, entries.length);
                    }
                },
            };

            instances.set(container, { editor, wrapper });

            console.log('[SnakkEditor] init complete for container:', container.id);

            // Set placeholder text as CSS custom property for ::before content
            if (placeholder) {
                const pm = editorRoot.querySelector<HTMLElement>('.ProseMirror');
                if (pm) pm.style.setProperty('--editor-placeholder', JSON.stringify(placeholder));
            }

            return wrapper;
        } catch (e) {
            console.error('Failed to initialize Milkdown editor:', e);
            // Show textarea as fallback
            textarea.style.display = '';
            return null;
        }
    }

    function getInstance(container: HTMLElement): EditorInstance | null {
        return instances.get(container)?.wrapper || null;
    }

    function destroyEditor(container: HTMLElement): void {
        const entry = instances.get(container);
        if (entry) {
            entry.editor.destroy();
            instances.delete(container);
        }
    }

    (window as any).SnakkEditor = {
        init,
        getInstance,
        destroy: destroyEditor,
        showImageModal,
        renderLayoutPreview,
        createImagePickerWidget,
    } as any;
})();
