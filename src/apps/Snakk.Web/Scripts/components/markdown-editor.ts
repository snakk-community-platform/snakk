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
    wrapInBulletListCommand,
    wrapInOrderedListCommand,
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
    addRowAfterCommand,
    addColAfterCommand,
    deleteSelectedCellsCommand,
    selectRowCommand,
    selectColCommand,
    selectTableCommand,
    setAlignCommand,
} from '@milkdown/kit/preset/gfm';
import { history } from '@milkdown/kit/plugin/history';
import { listener, listenerCtx } from '@milkdown/kit/plugin/listener';
import { upload, uploadConfig } from '@milkdown/kit/plugin/upload';
import { replaceAll, getMarkdown, callCommand, $inputRule, $markAttr, $markSchema, $command, $remark } from '@milkdown/kit/utils';
import { markRule } from '@milkdown/kit/prose';
import { toggleMark } from '@milkdown/kit/prose/commands';
import { Decoration } from '@milkdown/kit/prose/view';
import type { Node } from '@milkdown/kit/prose/model';

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
];

// ============================================================================
// Type Definitions
// ============================================================================

interface EditorInstance {
    getMarkdown(): string;
    setMarkdown(markdown: string): void;
    focus(): void;
    destroy(): void;
}

interface SnakkEditorOptions {
    container: HTMLElement;
    textarea: HTMLTextAreaElement;
    placeholder?: string;
    initialValue?: string;
    height?: string;
    onChange?: (markdown: string) => void;
}

interface SnakkEditorAPI {
    init(options: SnakkEditorOptions): Promise<EditorInstance | null>;
    getInstance(container: HTMLElement): EditorInstance | null;
    destroy(container: HTMLElement): void;
    getPendingUploads(container: HTMLElement): Map<string, File>;
    clearPendingUploads(container: HTMLElement): void;
}

interface ToolbarItem {
    icon: string;
    title: string;
    action: (editor: Editor) => void;
    className?: string;
    allowInTable?: boolean;
    opensDialog?: boolean;
    hasDropdown?: boolean;
    separator?: false;
}

interface ToolbarSeparator {
    separator: true;
}

interface ToolbarGroup {
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
// Toolbar Definition
// ============================================================================

function buildToolbarItems(): ToolbarEntry[] {
    return [
        {
            icon: '😀', title: 'Emoji', className: 'toolbar-emoji', allowInTable: true, hasDropdown: true,
            action: (e) => showEmojiPicker(e),
        },
        { separator: true },
        {
            icon: 'B', title: 'Bold (Ctrl+B)', className: 'toolbar-bold', allowInTable: true,
            action: (e) => e.action(callCommand(toggleStrongCommand.key)),
        },
        {
            icon: 'I', title: 'Italic (Ctrl+I)', className: 'toolbar-italic', allowInTable: true,
            action: (e) => e.action(callCommand(toggleEmphasisCommand.key)),
        },
        {
            icon: 'S', title: 'Strikethrough (Ctrl+Shift+X)', className: 'toolbar-strikethrough', allowInTable: true,
            action: (e) => e.action(callCommand(toggleStrikethroughCommand.key)),
        },
        {
            groupIcon: 'Aₓ', groupTitle: 'Text Effects', groupClassName: 'toolbar-group-extras',
            children: [
                {
                    icon: 'x₂', title: 'Subscript', className: 'toolbar-subscript', allowInTable: true,
                    action: (e) => e.action(callCommand(toggleSubscriptCommand.key)),
                },
                {
                    icon: 'x²', title: 'Superscript', className: 'toolbar-superscript', allowInTable: true,
                    action: (e) => e.action(callCommand(toggleSuperscriptCommand.key)),
                },
                {
                    icon: 'ins', title: 'Inserted', className: 'toolbar-inserted', allowInTable: true,
                    action: (e) => e.action(callCommand(toggleInsertedCommand.key)),
                },
                {
                    icon: 'mark', title: 'Highlight', className: 'toolbar-marked', allowInTable: true,
                    action: (e) => e.action(callCommand(toggleMarkedCommand.key)),
                },
                {
                    icon: '||', title: 'Spoiler', className: 'toolbar-spoiler', allowInTable: true,
                    action: (e) => e.action(callCommand(toggleSpoilerCommand.key)),
                },
            ],
        },
        { separator: true },
        {
            groupIcon: 'H', groupTitle: 'Headings', groupClassName: 'toolbar-group-headings',
            children: [
                {
                    icon: 'H1', title: 'Heading 1',
                    action: (e) => e.action(callCommand(wrapInHeadingCommand.key, 1)),
                },
                {
                    icon: 'H2', title: 'Heading 2',
                    action: (e) => e.action(callCommand(wrapInHeadingCommand.key, 2)),
                },
                {
                    icon: 'H3', title: 'Heading 3',
                    action: (e) => e.action(callCommand(wrapInHeadingCommand.key, 3)),
                },
            ],
        },
        { separator: true },
        {
            groupIcon: '☰', groupTitle: 'Lists', groupClassName: 'toolbar-group-lists',
            children: [
                {
                    icon: '•', title: 'Bullet List',
                    action: (e) => e.action(callCommand(wrapInBulletListCommand.key)),
                },
                {
                    icon: '1.', title: 'Ordered List',
                    action: (e) => e.action(callCommand(wrapInOrderedListCommand.key)),
                },
                {
                    icon: '☑', title: 'Task List',
                    action: (e) => insertTaskList(e),
                },
            ],
        },
        { separator: true },
        {
            icon: '❝', title: 'Blockquote',
            action: (e) => e.action(callCommand(wrapInBlockquoteCommand.key)),
        },
        {
            icon: '⟨⟩', title: 'Inline Code', allowInTable: true,
            action: (e) => e.action(callCommand(toggleInlineCodeCommand.key)),
        },
        {
            icon: '{ }', title: 'Code Block',
            action: (e) => e.action(callCommand(createCodeBlockCommand.key)),
        },
        { separator: true },
        {
            icon: '🔗', title: 'Link', className: 'toolbar-link', allowInTable: true, opensDialog: true,
            action: (e) => showLinkDialog(e),
        },
        {
            icon: '🖼', title: 'Image',
            action: (e) => insertImageFromFile(e),
        },
        { separator: true },
        {
            icon: '⊞', title: 'Table', className: 'toolbar-table', hasDropdown: true,
            action: (e) => showTablePicker(e),
        },
        {
            icon: '—', title: 'Horizontal Rule',
            action: (e) => e.action(callCommand(insertHrCommand.key)),
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
    closeTablePicker();
    closeLinkDialog();

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

        btn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            e.stopPropagation();
            child.action(editor);
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
// Link Dialog
// ============================================================================

let activeLinkDialog: HTMLElement | null = null;

function showLinkDialog(editor: Editor): void {
    // Close any existing dialog/picker
    closeLinkDialog();
    closeGroupDropdown();

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
            <button type="button" class="link-dialog-cancel">Cancel</button>
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
// Task List
// ============================================================================

function insertTaskList(editor: Editor): void {
    // First wrap in bullet list, then convert the current list item to a task item
    editor.action(callCommand(wrapInBulletListCommand.key));
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const { $from } = view.state.selection;

        // Walk up to find the list_item node
        for (let d = $from.depth; d > 0; d--) {
            const node = $from.node(d);
            if (node.type.name === 'list_item') {
                const pos = $from.before(d);
                const tr = view.state.tr.setNodeMarkup(pos, undefined, {
                    ...node.attrs,
                    checked: false,
                });
                view.dispatch(tr);
                break;
            }
        }
        view.focus();
    });
}

function setupTaskListClickHandler(editor: Editor, contentArea: HTMLElement): void {
    contentArea.addEventListener('click', (e) => {
        const target = e.target as HTMLElement;
        const taskItem = target.closest('li[data-item-type="task"]') as HTMLElement;
        if (!taskItem) return;

        // Only toggle if clicking near the checkbox area (left side of the li)
        const itemRect = taskItem.getBoundingClientRect();
        if (e.clientX > itemRect.left + 28) return;

        e.preventDefault();

        editor.action((ctx) => {
            const view = ctx.get(editorViewCtx);
            const pos = view.posAtDOM(taskItem, 0);
            const $pos = view.state.doc.resolve(pos);

            for (let d = $pos.depth; d >= 0; d--) {
                const node = $pos.node(d);
                if (node.type.name === 'list_item' && node.attrs.checked != null) {
                    const nodePos = $pos.before(d);
                    const tr = view.state.tr.setNodeMarkup(nodePos, undefined, {
                        ...node.attrs,
                        checked: !node.attrs.checked,
                    });
                    view.dispatch(tr);
                    break;
                }
            }
        });
    });
}

// ============================================================================
// Image File Picker
// ============================================================================

function insertImageFromFile(editor: Editor): void {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.style.display = 'none';

    input.addEventListener('change', () => {
        const file = input.files?.[0];
        if (!file || !file.type.startsWith('image/')) return;

        // Show loading overlay on the editor
        const editorWrapper = document.querySelector('.milkdown-editor') as HTMLElement;
        const container = editorWrapper?.closest('#editor-container') as HTMLElement
            || editorWrapper?.parentElement as HTMLElement;

        let overlay: HTMLElement | null = null;
        if (editorWrapper) {
            overlay = document.createElement('div');
            overlay.className = 'milkdown-loading-overlay';
            overlay.innerHTML = '<div class="milkdown-loading-spinner"></div>';
            editorWrapper.appendChild(overlay);
        }

        // Defer image insertion to next frame so the overlay renders first
        requestAnimationFrame(() => {
            const blobUrl = URL.createObjectURL(file);

            if (container) {
                (window as any).SnakkEditor.addPendingUpload(container, blobUrl, file);
            }

            // Insert image node at current cursor position
            editor.action((ctx) => {
                const view = ctx.get(editorViewCtx);
                const { from } = view.state.selection;
                const imageNode = view.state.schema.nodes.image?.create({
                    src: blobUrl,
                    alt: 'user uploaded image',
                });
                if (imageNode) {
                    const tr = view.state.tr.insert(from, imageNode);
                    view.dispatch(tr);
                }
                view.focus();
            });

            // Remove overlay after image is inserted
            overlay?.remove();
            input.remove();
        });
    });

    document.body.appendChild(input);
    input.click();
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
// Table Picker (Word-style grid)
// ============================================================================

const TABLE_ROWS = 6;
const TABLE_COLS = 6;
let activeTablePicker: HTMLElement | null = null;

function showTablePicker(editor: Editor): void {
    closeTablePicker();
    closeLinkDialog();
    closeGroupDropdown();


    const picker = document.createElement('div');
    picker.className = 'milkdown-table-picker';

    const label = document.createElement('div');
    label.className = 'table-picker-label';
    label.textContent = 'Select size';

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

    // Hover: highlight from (0,0) to hovered position, minimum 2 rows
    grid.addEventListener('mouseover', (e) => {
        const target = (e.target as HTMLElement).closest('.table-picker-cell') as HTMLElement;
        if (!target) return;

        const hoverRow = Math.max(1, parseInt(target.dataset.row!));
        const hoverCol = parseInt(target.dataset.col!);

        cells.forEach(cell => {
            const cr = parseInt(cell.dataset.row!);
            const cc = parseInt(cell.dataset.col!);
            cell.classList.toggle('highlighted', cr <= hoverRow && cc <= hoverCol);
        });

        label.textContent = `${hoverCol + 1} × ${hoverRow + 1}`;
    });

    grid.addEventListener('mouseleave', () => {
        cells.forEach(cell => cell.classList.remove('highlighted'));
        label.textContent = 'Select size';
    });

    // Click: insert table (minimum 2 rows)
    grid.addEventListener('click', (e) => {
        const target = (e.target as HTMLElement).closest('.table-picker-cell') as HTMLElement;
        if (!target) return;

        const row = Math.max(2, parseInt(target.dataset.row!) + 1);
        const col = parseInt(target.dataset.col!) + 1;

        editor.action(callCommand(insertTableCommand.key, { row, col }));
        closeTablePicker();
        editor.action((ctx) => ctx.get(editorViewCtx).focus());
    });

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

function closeTablePicker(): void {
    if (activeTablePicker) {
        activeTablePicker.remove();
        activeTablePicker = null;
    }
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
    closeTablePicker();
    closeLinkDialog();
    closeGroupDropdown();


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
// Table Overlay Controls (floating buttons on active table)
// ============================================================================

let tableOverlays: HTMLElement[] = [];
let imageOverlays: HTMLElement[] = [];
let languagePickerOpen = false;

function deleteTableAtDOM(editor: Editor, tableEl: HTMLElement): void {
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const pos = view.posAtDOM(tableEl, 0);
        const $pos = view.state.doc.resolve(pos);
        for (let d = $pos.depth; d > 0; d--) {
            if ($pos.node(d).type.name === 'table') {
                const start = $pos.before(d);
                const node = $pos.node(d);
                view.dispatch(view.state.tr.delete(start, start + node.nodeSize));
                break;
            }
        }
        view.focus();
    });
}

function cycleAlignment(current: string): string {
    if (current === 'left') return 'center';
    if (current === 'center') return 'right';
    return 'left';
}

function getAlignIcon(align: string): string {
    if (align === 'center') return '<svg width="12" height="10" viewBox="0 0 12 10"><line x1="2" y1="1" x2="10" y2="1" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/><line x1="3.5" y1="5" x2="8.5" y2="5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/><line x1="2" y1="9" x2="10" y2="9" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>';
    if (align === 'right') return '<svg width="12" height="10" viewBox="0 0 12 10"><line x1="2" y1="1" x2="10" y2="1" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/><line x1="5" y1="5" x2="10" y2="5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/><line x1="2" y1="9" x2="10" y2="9" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>';
    // left (default)
    return '<svg width="12" height="10" viewBox="0 0 12 10"><line x1="2" y1="1" x2="10" y2="1" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/><line x1="2" y1="5" x2="7" y2="5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/><line x1="2" y1="9" x2="10" y2="9" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>';
}

function updateOverlays(editor: Editor, contentArea: HTMLElement): void {
    // Don't rebuild overlays while a language picker is open
    if (languagePickerOpen) return;

    removeAllOverlays();

    const contentRect = contentArea.getBoundingClientRect();

    // --- Table overlays (on ALL tables) ---
    const tables = contentArea.querySelectorAll('.ProseMirror table');
    tables.forEach((table) => {
        const tableEl = table as HTMLElement;
        const tableRect = tableEl.getBoundingClientRect();

        const overlay = document.createElement('div');
        overlay.className = 'milkdown-table-overlay';
        overlay.contentEditable = 'false';
        overlay.style.top = `${tableRect.top - contentRect.top + contentArea.scrollTop}px`;
        overlay.style.left = `${tableRect.left - contentRect.left + contentArea.scrollLeft}px`;
        overlay.style.width = `${tableRect.width}px`;
        overlay.style.height = `${tableRect.height}px`;

        // × Delete table (top-right corner)
        const deleteBtn = document.createElement('button');
        deleteBtn.type = 'button';
        deleteBtn.className = 'table-ctl table-ctl-delete';
        deleteBtn.title = 'Delete table';
        deleteBtn.textContent = '\u00d7';
        deleteBtn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            deleteTableAtDOM(editor, tableEl);
        });
        overlay.appendChild(deleteBtn);

        // − Delete row buttons (left border of first td, skip header and first data row)
        const rows = tableEl.querySelectorAll('tr');
        rows.forEach((row, index) => {
            if (index <= 1) return;

            const firstCell = row.querySelector('td:first-child, th:first-child') as HTMLElement;
            if (!firstCell) return;
            const cellRect = firstCell.getBoundingClientRect();
            const btnTop = cellRect.top + cellRect.height / 2 - tableRect.top;
            const btnLeft = cellRect.left - tableRect.left;

            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'table-ctl table-ctl-delete-row';
            btn.title = 'Delete row';
            btn.textContent = '\u2212';
            btn.style.top = `${btnTop}px`;
            btn.style.left = `${btnLeft}px`;
            btn.addEventListener('mousedown', (e) => {
                e.preventDefault();
                editor.action((ctx) => {
                    const view = ctx.get(editorViewCtx);
                    const pos = view.posAtDOM(row, 0);
                    const tr = view.state.tr.setSelection(
                        view.state.selection.constructor.create(view.state.doc, pos) as any
                    );
                    view.dispatch(tr);
                });
                editor.action(callCommand(selectRowCommand.key, { index }));
                editor.action(callCommand(deleteSelectedCellsCommand.key));
                editor.action((ctx) => {
                    const view = ctx.get(editorViewCtx);
                    const { to } = view.state.selection;
                    const tr = view.state.tr.setSelection(
                        view.state.selection.constructor.near(view.state.doc.resolve(to)) as any
                    );
                    view.dispatch(tr);
                    view.focus();
                });
            });
            overlay.appendChild(btn);
        });

        // + Add row (centered on bottom border of table)
        {
            const addRowBtn = document.createElement('button');
            addRowBtn.type = 'button';
            addRowBtn.className = 'table-ctl table-ctl-add-row';
            addRowBtn.title = 'Add row';
            addRowBtn.textContent = '+';
            addRowBtn.style.top = `${tableRect.height}px`;
            addRowBtn.style.left = `${tableRect.width / 2}px`;
            addRowBtn.addEventListener('mousedown', (e) => {
                e.preventDefault();
                const lastRow = tableEl.querySelector('tr:last-child');
                const rowCount = tableEl.querySelectorAll('tr').length;
                if (lastRow) {
                    editor.action((ctx) => {
                        const view = ctx.get(editorViewCtx);
                        const pos = view.posAtDOM(lastRow, 0);
                        const tr = view.state.tr.setSelection(
                            view.state.selection.constructor.create(view.state.doc, pos) as any
                        );
                        view.dispatch(tr);
                    });
                }
                editor.action(callCommand(selectRowCommand.key, { index: rowCount - 1 }));
                editor.action(callCommand(addRowAfterCommand.key));
                // Collapse selection to cursor
                editor.action((ctx) => {
                    const view = ctx.get(editorViewCtx);
                    const { to } = view.state.selection;
                    const tr = view.state.tr.setSelection(
                        view.state.selection.constructor.near(view.state.doc.resolve(to)) as any
                    );
                    view.dispatch(tr);
                    view.focus();
                });
            });
            overlay.appendChild(addRowBtn);
        }

        // + Add column (centered on right border of table)
        {
            const addColBtn = document.createElement('button');
            addColBtn.type = 'button';
            addColBtn.className = 'table-ctl table-ctl-add-col';
            addColBtn.title = 'Add column';
            addColBtn.textContent = '+';
            addColBtn.style.top = `${tableRect.height / 2}px`;
            addColBtn.style.left = `${tableRect.width}px`;
            addColBtn.addEventListener('mousedown', (e) => {
                e.preventDefault();
                // Move cursor into last column's last cell
                const lastHeaderCell = tableEl.querySelector('tr:first-child th:last-child, tr:first-child td:last-child');
                if (lastHeaderCell) {
                    editor.action((ctx) => {
                        const view = ctx.get(editorViewCtx);
                        const pos = view.posAtDOM(lastHeaderCell, 0);
                        const tr = view.state.tr.setSelection(
                            view.state.selection.constructor.create(view.state.doc, pos) as any
                        );
                        view.dispatch(tr);
                    });
                }
                editor.action(callCommand(addColAfterCommand.key));
                // Collapse selection to cursor
                editor.action((ctx) => {
                    const view = ctx.get(editorViewCtx);
                    const { to } = view.state.selection;
                    const tr = view.state.tr.setSelection(
                        view.state.selection.constructor.near(view.state.doc.resolve(to)) as any
                    );
                    view.dispatch(tr);
                    view.focus();
                });
            });
            overlay.appendChild(addColBtn);
        }

        // Column alignment toggle + delete column buttons
        const headerRow = tableEl.querySelector('tr:first-child');
        const dataRow = tableEl.querySelector('tr:nth-child(2)');
        if (headerRow && dataRow) {
            const borderY = headerRow.getBoundingClientRect().bottom - tableRect.top;
            const headerCells = headerRow.querySelectorAll('th, td');

            headerCells.forEach((cell, colIndex) => {
                const cellRect = cell.getBoundingClientRect();
                const cellCenterX = cellRect.left + cellRect.width / 2 - tableRect.left;

                // Read current alignment from the DOM style
                const currentAlign = (cell as HTMLElement).style.textAlign || 'left';

                const alignBtn = document.createElement('button');
                alignBtn.type = 'button';
                alignBtn.className = 'table-ctl table-ctl-align';
                alignBtn.title = `Alignment: ${currentAlign}`;
                alignBtn.innerHTML = getAlignIcon(currentAlign);
                alignBtn.style.top = `${borderY}px`;
                alignBtn.style.left = `${cellCenterX}px`;

                alignBtn.addEventListener('mousedown', (e) => {
                    e.preventDefault();
                    const nextAlign = cycleAlignment(currentAlign);

                    editor.action((ctx) => {
                        const view = ctx.get(editorViewCtx);
                        const pos = view.posAtDOM(cell, 0);
                        const tr = view.state.tr.setSelection(
                            view.state.selection.constructor.create(view.state.doc, pos) as any
                        );
                        view.dispatch(tr);
                    });
                    editor.action(callCommand(selectColCommand.key, { index: colIndex }));
                    editor.action(callCommand(setAlignCommand.key, nextAlign));
                    // Collapse selection to cursor
                    editor.action((ctx) => {
                        const view = ctx.get(editorViewCtx);
                        const { to } = view.state.selection;
                        const tr = view.state.tr.setSelection(
                            view.state.selection.constructor.near(view.state.doc.resolve(to)) as any
                        );
                        view.dispatch(tr);
                        view.focus();
                    });
                });

                overlay.appendChild(alignBtn);

                // − Delete column button (top border of header cell, skip first column)
                if (colIndex > 0) {
                    const colTopY = cellRect.top - tableRect.top;
                    const delColBtn = document.createElement('button');
                    delColBtn.type = 'button';
                    delColBtn.className = 'table-ctl table-ctl-delete-col';
                    delColBtn.title = 'Delete column';
                    delColBtn.textContent = '\u2212';
                    delColBtn.style.top = `${colTopY}px`;
                    delColBtn.style.left = `${cellCenterX}px`;
                    delColBtn.addEventListener('mousedown', (e) => {
                        e.preventDefault();
                        editor.action((ctx) => {
                            const view = ctx.get(editorViewCtx);
                            const pos = view.posAtDOM(cell, 0);
                            const tr = view.state.tr.setSelection(
                                view.state.selection.constructor.create(view.state.doc, pos) as any
                            );
                            view.dispatch(tr);
                        });
                        editor.action(callCommand(selectColCommand.key, { index: colIndex }));
                        editor.action(callCommand(deleteSelectedCellsCommand.key));
                        editor.action((ctx) => {
                            const view = ctx.get(editorViewCtx);
                            const { to } = view.state.selection;
                            const tr = view.state.tr.setSelection(
                                view.state.selection.constructor.near(view.state.doc.resolve(to)) as any
                            );
                            view.dispatch(tr);
                            view.focus();
                        });
                    });
                    overlay.appendChild(delColBtn);
                }
            });
        }

        contentArea.appendChild(overlay);
        tableOverlays.push(overlay);
    });

    // --- Element overlays (× delete on images, hr, code blocks) ---
    const elements: { el: HTMLElement; type: string }[] = [];

    contentArea.querySelectorAll('.ProseMirror img').forEach((el) => {
        const rect = (el as HTMLElement).getBoundingClientRect();
        if (rect.width >= 32 && rect.height >= 32) {
            elements.push({ el: el as HTMLElement, type: 'image' });
        }
    });
    contentArea.querySelectorAll('.ProseMirror hr').forEach((el) => {
        elements.push({ el: el as HTMLElement, type: 'hr' });
    });
    contentArea.querySelectorAll('.ProseMirror pre').forEach((el) => {
        elements.push({ el: el as HTMLElement, type: 'code_block' });
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

        // Language picker for code blocks
        if (type === 'code_block') {
            const select = document.createElement('select');
            select.className = 'element-ctl-language';
            select.title = 'Code language';
            CODE_LANGUAGES.forEach(lang => {
                const option = document.createElement('option');
                option.value = lang.value;
                option.textContent = lang.label;
                select.appendChild(option);
            });

            // Read current language from the ProseMirror node
            const currentLang = el.getAttribute('data-language') || '';
            select.value = currentLang;

            select.addEventListener('mousedown', (e) => { e.stopPropagation(); e.stopImmediatePropagation(); });
            select.addEventListener('mouseup', (e) => { e.stopPropagation(); e.stopImmediatePropagation(); });
            select.addEventListener('click', (e) => { e.stopPropagation(); e.stopImmediatePropagation(); });
            select.addEventListener('focus', () => { languagePickerOpen = true; });
            select.addEventListener('blur', () => { languagePickerOpen = false; });
            select.addEventListener('change', () => {
                const newLang = select.value;
                editor.action((ctx) => {
                    const view = ctx.get(editorViewCtx);
                    const pos = view.posAtDOM(el, 0);
                    const $pos = view.state.doc.resolve(pos);
                    for (let d = $pos.depth; d >= 0; d--) {
                        if ($pos.node(d).type.name === 'code_block') {
                            const nodePos = $pos.before(d);
                            view.dispatch(view.state.tr.setNodeAttribute(nodePos, 'language', newLang));
                            break;
                        }
                    }
                });
            });
            overlay.appendChild(select);
        }

        const deleteBtn = document.createElement('button');
        deleteBtn.type = 'button';
        deleteBtn.className = type === 'hr' ? 'element-ctl-delete-hr' : 'element-ctl-delete';
        deleteBtn.title = `Remove ${type === 'code_block' ? 'code block' : type}`;
        deleteBtn.textContent = '\u00d7';
        deleteBtn.addEventListener('mousedown', (e) => {
            e.preventDefault();

            // Clean up pending upload if removing an image with a blob URL
            if (type === 'image') {
                const imgSrc = (el as HTMLImageElement).src;
                if (imgSrc && imgSrc.startsWith('blob:')) {
                    const container = contentArea.parentElement?.parentElement as HTMLElement;
                    if (container) {
                        (window as any).SnakkEditor?.removePendingUpload(container, imgSrc);
                    }
                }
            }

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
                        || (type === 'code_block' && node.type.name === 'code_block')
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
    tableOverlays.forEach(o => o.remove());
    tableOverlays = [];
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

    document.body.style.overflow = 'hidden';
    document.body.appendChild(backdrop);
    document.body.appendChild(modal);
    activeHelpModal = modal;
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
    document.body.style.overflow = '';
}

// ============================================================================
// Editor Expand / Collapse
// ============================================================================

const EXPAND_SVG = '<svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M1 4V1h3M8 1h3v3M11 8v3H8M4 11H1V8"/></svg>';
const COLLAPSE_SVG = '<svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M4 1v3H1M11 4H8V1M8 11V8h3M1 8h3v3"/></svg>';

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
        document.documentElement.style.overflowY = 'hidden';
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

        // Escape to collapse
        expandEscapeHandler = (e: KeyboardEvent) => {
            if (e.key === 'Escape') toggleEditorExpand(btn);
        };
        document.addEventListener('keydown', expandEscapeHandler);
    } else {
        wrapper.classList.remove('milkdown-expanded');
        document.documentElement.style.overflowY = '';
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
    allowInTable: boolean;
}

function createToolbarDOM(editor: Editor): { toolbar: HTMLElement; buttons: ToolbarButtonRef[] } {
    const toolbar = document.createElement('div');
    toolbar.className = 'milkdown-toolbar';
    const buttons: ToolbarButtonRef[] = [];

    for (const item of buildToolbarItems()) {
        if ('separator' in item && item.separator) {
            const sep = document.createElement('span');
            sep.className = 'milkdown-toolbar-separator';
            toolbar.appendChild(sep);
            continue;
        }

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

            const allAllowInTable = item.children.every((c) => c.allowInTable === true);
            buttons.push({ btn, allowInTable: allAllowInTable });
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
        buttons.push({ btn, allowInTable: item.allowInTable === true });
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

function isCursorInTable(editor: Editor): boolean {
    let inTable = false;
    editor.action((ctx) => {
        const view = ctx.get(editorViewCtx);
        const { $from } = view.state.selection;
        for (let d = $from.depth; d > 0; d--) {
            if ($from.node(d).type.name === 'table') {
                inTable = true;
                break;
            }
        }
    });
    return inTable;
}

function updateToolbarState(editor: Editor, buttons: ToolbarButtonRef[], contentArea: HTMLElement): void {
    const inTable = isCursorInTable(editor);
    for (const { btn, allowInTable } of buttons) {
        btn.classList.toggle('toolbar-disabled', inTable && !allowInTable);
    }

    updateOverlays(editor, contentArea);
}

// ============================================================================
// Editor Implementation
// ============================================================================

(function(): void {
    'use strict';

    if ((window as any).SnakkEditor) return;

    const instances = new Map<HTMLElement, { editor: Editor; wrapper: EditorInstance }>();
    const pendingUploads = new Map<HTMLElement, Map<string, File>>();

    async function init(options: SnakkEditorOptions): Promise<EditorInstance | null> {
        const { container, textarea, placeholder, initialValue, height, onChange } = options;

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

                    // Upload plugin: intercept pasted/dropped images
                    ctx.update(uploadConfig.key, (prev) => ({
                        ...prev,
                        uploader: async (files, schema, _ctx, _insertPos) => {
                            const nodes: Node[] = [];
                            for (let i = 0; i < files.length; i++) {
                                const file = files.item(i);
                                if (!file || !file.type.startsWith('image/')) continue;

                                // Create blob URL for immediate preview
                                const blobUrl = URL.createObjectURL(file);

                                // Track for later upload on form submit
                                let containerUploads = pendingUploads.get(container);
                                if (!containerUploads) {
                                    containerUploads = new Map<string, File>();
                                    pendingUploads.set(container, containerUploads);
                                }
                                containerUploads.set(blobUrl, file);

                                const node = schema.nodes.image?.createAndFill({
                                    src: blobUrl,
                                    alt: 'user uploaded image',
                                });
                                if (node) nodes.push(node);
                            }
                            return nodes;
                        },
                        uploadWidgetFactory: (pos: number, spec: Parameters<typeof Decoration.widget>[2]) => {
                            const span = document.createElement('span');
                            span.className = 'milkdown-upload-placeholder';
                            span.textContent = 'Uploading...';
                            return Decoration.widget(pos, span, spec);
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
                .create();

            // Build toolbar, footer and assemble the editor
            const { toolbar, buttons: toolbarButtons } = createToolbarDOM(editor);
            const footer = document.createElement('div');
            footer.className = 'milkdown-footer';

            const helpLink = document.createElement('button');
            helpLink.type = 'button';
            helpLink.className = 'md-help-link';
            helpLink.textContent = 'Markdown help';
            helpLink.addEventListener('click', showMarkdownHelp);
            footer.appendChild(helpLink);

            editorWrapper.appendChild(toolbar);
            editorWrapper.appendChild(editorRoot);
            editorWrapper.appendChild(footer);

            // Update toolbar button states on selection changes (e.g. disable block items in tables)
            const pm = editorRoot.querySelector('.ProseMirror');
            if (pm) {
                const observer = new MutationObserver(() => updateToolbarState(editor, toolbarButtons, editorRoot));
                observer.observe(pm, { childList: true, subtree: true, characterData: true });
            }
            // Also update on click/keyup for cursor moves
            editorRoot.addEventListener('keyup', () => updateToolbarState(editor, toolbarButtons, editorRoot));
            editorRoot.addEventListener('mouseup', () => updateToolbarState(editor, toolbarButtons, editorRoot));

            // Enable checkbox toggling for task lists
            setupTaskListClickHandler(editor, editorRoot);

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

    function addPendingUpload(container: HTMLElement, blobUrl: string, file: File): void {
        let containerUploads = pendingUploads.get(container);
        if (!containerUploads) {
            containerUploads = new Map<string, File>();
            pendingUploads.set(container, containerUploads);
        }
        containerUploads.set(blobUrl, file);
        console.log('[SnakkEditor] addPendingUpload: container=', container.id, 'total=', containerUploads.size);
    }

    function getPendingUploads(container: HTMLElement): Map<string, File> {
        const uploads = pendingUploads.get(container) || new Map<string, File>();
        console.log('[SnakkEditor] getPendingUploads: container=', container.id, 'size=', uploads.size);
        return uploads;
    }

    function removePendingUpload(container: HTMLElement, blobUrl: string): void {
        const uploads = pendingUploads.get(container);
        if (uploads && uploads.has(blobUrl)) {
            URL.revokeObjectURL(blobUrl);
            uploads.delete(blobUrl);
        }
    }

    function clearPendingUploads(container: HTMLElement): void {
        const uploads = pendingUploads.get(container);
        if (uploads) {
            for (const blobUrl of uploads.keys()) {
                URL.revokeObjectURL(blobUrl);
            }
            uploads.clear();
        }
    }

    function destroyEditor(container: HTMLElement): void {
        clearPendingUploads(container);
        const entry = instances.get(container);
        if (entry) {
            entry.editor.destroy();
            instances.delete(container);
            pendingUploads.delete(container);
        }
    }

    (window as any).SnakkEditor = {
        init,
        getInstance,
        destroy: destroyEditor,
        addPendingUpload,
        removePendingUpload,
        getPendingUploads,
        clearPendingUploads,
    } as any;
})();
