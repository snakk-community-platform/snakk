/**
 * Image Group NodeView for the Milkdown/ProseMirror editor.
 * Overrides code_block rendering: image-group language → rich thumbnail widget,
 * everything else → standard <pre><code> with ProseMirror-managed content.
 */

import { $prose } from '@milkdown/kit/utils';
import { Fragment } from '@milkdown/kit/prose/model';
import type { Node as PNode } from '@milkdown/kit/prose/model';
import type { EditorView, NodeView } from '@milkdown/kit/prose/view';
import { Decoration, DecorationSet } from '@milkdown/kit/prose/view';
import { Plugin, PluginKey, TextSelection } from '@milkdown/kit/prose/state';
import { gapCursor } from 'prosemirror-gapcursor';

const IMAGE_GROUP_KEY = new PluginKey('snakkImageGroup');
const LAYOUTS = ['grid', 'masonry', 'justified', 'carousel', 'single'] as const;
type Layout = typeof LAYOUTS[number];

// Callback registered by markdown-editor.ts so ChartNodeView can open the modal
type ChartEditHandler = (type: string, initialConfig: string, onConfirm: (config: string) => void) => void;
let chartEditHandler: ChartEditHandler | null = null;
export function setChartEditCallback(handler: ChartEditHandler): void {
    chartEditHandler = handler;
}

export interface TableModalData {
    headers: string[];
    rows: string[][];
    alignments: Array<'left' | 'center' | 'right' | null>;
}
type TableEditHandler = (data: TableModalData, onConfirm: (data: TableModalData) => void) => void;
let tableEditHandler: TableEditHandler | null = null;
export function setTableEditCallback(handler: TableEditHandler): void {
    tableEditHandler = handler;
}

export interface ImageModalData {
    images: Array<{ src: string; alt: string }>;
    layout: Layout;
}
type ImageEditHandler = (data: ImageModalData, view: EditorView, onConfirm: (data: ImageModalData) => void) => void;
let imageEditHandler: ImageEditHandler | null = null;
export function setImageEditCallback(handler: ImageEditHandler): void {
    imageEditHandler = handler;
}

export interface CodeModalData { language: string; code: string; }
type CodeEditHandler = (data: CodeModalData, onConfirm: (data: CodeModalData) => void) => void;
let codeEditHandler: CodeEditHandler | null = null;
export function setCodeEditCallback(handler: CodeEditHandler): void {
    codeEditHandler = handler;
}

export interface ListItem { text: string; checked?: boolean; }
export interface ListModalData { type: 'bullet' | 'ordered' | 'task'; items: ListItem[]; }
type ListEditHandler = (data: ListModalData, onConfirm: (data: ListModalData) => void) => void;
let listEditHandler: ListEditHandler | null = null;
export function setListEditCallback(handler: ListEditHandler): void {
    listEditHandler = handler;
}

export function countImagesFromView(view: EditorView): number {
    let count = 0;
    view.state.doc.descendants((node) => {
        if (node.type.name === 'image') { count++; return false; }
        if (node.type.name === 'code_block') {
            const lang = (node.attrs as { language?: string }).language ?? '';
            if (lang.startsWith('image-group')) {
                count += (node.textContent ?? '').split('\n').filter((l: string) => l.trim()).length;
            }
            return false;
        }
        return true;
    });
    return count;
}

export interface BlockCounts {
    charts: number;
    codeBlocks: number;
    lists: number;
    callouts: number;
    hrs: number;
    tables: number;
    blockquotes: number;
    imageBlocks: number;
}

export function countBlocksInEditor(view: EditorView): BlockCounts {
    const counts: BlockCounts = {
        charts: 0, codeBlocks: 0, lists: 0, callouts: 0,
        hrs: 0, tables: 0, blockquotes: 0, imageBlocks: 0,
    };
    view.state.doc.descendants((node) => {
        const name = node.type.name;
        if (name === 'code_block') {
            const lang = ((node.attrs as { language?: string }).language ?? '').trim().toLowerCase();
            if (lang === 'chart') counts.charts++;
            else if (lang.startsWith('image-group')) counts.imageBlocks++;
            else if (lang.startsWith('callout-')) counts.callouts++;
            else counts.codeBlocks++;
            return false;
        }
        if (name === 'bullet_list' || name === 'ordered_list') { counts.lists++; return false; }
        if (name === 'horizontal_rule') { counts.hrs++; return false; }
        if (name === 'table') { counts.tables++; return false; }
        if (name === 'blockquote') { counts.blockquotes++; return false; }
        return true;
    });
    return counts;
}

const CALLOUT_META: Record<string, { icon: string; label: string }> = {
    info:    { icon: 'ℹ', label: 'Info' },
    warning: { icon: '⚠', label: 'Warning' },
    tip:     { icon: '💡', label: 'Tip' },
    note:    { icon: '📝', label: 'Note' },
};

const LANG_LABELS: Record<string, string> = {
    javascript: 'JavaScript', typescript: 'TypeScript', csharp: 'C#',
    html: 'HTML', css: 'CSS', sql: 'SQL', json: 'JSON',
    bash: 'Bash', python: 'Python', markdown: 'Markdown',
    yaml: 'YAML', xml: 'XML', markup: 'HTML',
};

// Inline SVGs
const SVG_CHEVRON_LEFT = `<svg width="14" height="14" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M6.5 2L3.5 5l3 3"/></svg>`;
const SVG_CHEVRON_RIGHT = `<svg width="14" height="14" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M3.5 2L6.5 5l-3 3"/></svg>`;

function isNonTypeableBlock(node: PNode): boolean {
    return node.type.name === 'code_block' || node.type.name === 'horizontal_rule';
}

const GAP_PARA_KEY = new PluginKey<DecorationSet>('snakkGapPara');

function isEmptyParagraph(node: PNode): boolean {
    if (node.type.name !== 'paragraph') return false;
    if (node.childCount === 0) return true;
    return node.childCount === 1 && node.firstChild!.type.name === 'hard_break';
}

function buildGapParaDecos(doc: PNode, selectionPos: number): DecorationSet {
    // Collect positions adjacent to non-typeable blocks
    const nonTypeableEnds = new Set<number>();
    const nonTypeableStarts = new Set<number>();
    doc.forEach((node, pos) => {
        if (isNonTypeableBlock(node)) {
            nonTypeableEnds.add(pos + node.nodeSize);
            nonTypeableStarts.add(pos);
        }
    });

    const decos: Decoration[] = [];
    doc.forEach((node, pos) => {
        if (!isEmptyParagraph(node)) return;
        const adjacent = nonTypeableEnds.has(pos) || nonTypeableStarts.has(pos + node.nodeSize);
        if (!adjacent) return;
        const cursorHere = selectionPos > pos && selectionPos < pos + node.nodeSize;
        decos.push(Decoration.node(pos, pos + node.nodeSize, {
            class: cursorHere ? 'gap-para gap-para-active' : 'gap-para',
        }));
    });

    return DecorationSet.create(doc, decos);
}

function parseLayout(language: string): Layout {
    const m = /layout=(\w+)/i.exec(language);
    const val = m?.[1]?.toLowerCase() ?? 'grid';
    return (LAYOUTS as readonly string[]).includes(val) ? (val as Layout) : 'grid';
}


function parseLines(text: string): Array<{ src: string; alt: string }> {
    return text.split('\n')
        .map(l => l.trim())
        .filter(Boolean)
        .map(l => {
            const sep = l.indexOf('|');
            return sep >= 0
                ? { src: l.slice(0, sep).trim(), alt: l.slice(sep + 1).trim() }
                : { src: l, alt: '' };
        });
}

// ============================================================================
// Upload validation helpers
// ============================================================================

export const MAX_IMAGE_BYTES = 10 * 1024 * 1024; // 10 MB

export function validateImageFile(file: File): string | null {
    if (file.size > MAX_IMAGE_BYTES) {
        const mb = (file.size / (1024 * 1024)).toFixed(1);
        return `Image is too large (${mb} MB). The maximum allowed size is 10 MB.`;
    }
    return null;
}


export function updateUploadProgress(blobUrl: string, value: number | 'processing'): void {
    document.querySelectorAll<HTMLElement>('[data-upload-src]').forEach(el => {
        if (el.dataset.uploadSrc !== blobUrl) return;
        const bar = el.querySelector<HTMLProgressElement>('progress');
        const lbl = el.querySelector<HTMLElement>('.sn-upload-pct, .images-upload-item-label');
        if (value === 'processing') {
            if (bar) bar.value = 100;
            if (lbl) lbl.textContent = '…';
        } else {
            if (bar) bar.value = value;
            if (lbl) lbl.textContent = `${value}%`;
        }
    });
}

// ============================================================================
// Shared block controls factory
// ============================================================================

interface BlockControlDef {
    label: string;
    title?: string;
    isDelete?: boolean;
    onMousedown: () => void;
}

function buildBlockLabel(text: string): HTMLElement {
    const label = document.createElement('span');
    label.className = 'sn-block-label';
    label.textContent = text;
    return label;
}

function buildBlockControls(defs: BlockControlDef[]): HTMLElement {
    const controls = document.createElement('div');
    controls.className = 'sn-block-controls';
    controls.contentEditable = 'false';
    for (const def of defs) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = def.isDelete ? 'sn-block-btn sn-block-btn-delete' : 'sn-block-btn';
        if (def.title) btn.title = def.title;
        btn.textContent = def.label;
        btn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            e.stopPropagation();
            def.onMousedown();
        });
        controls.appendChild(btn);
    }
    return controls;
}

// Place cursor on the line after a block node (mirrors placeCursorAfterBlock in markdown-editor.ts).
function focusAfterNode(view: EditorView, endPos: number): void {
    const { state } = view;
    let tr = state.tr;
    const nodeAfter = endPos < state.doc.content.size ? state.doc.nodeAt(endPos) : null;
    if (!nodeAfter || nodeAfter.type.name !== 'paragraph') {
        tr = tr.insert(endPos, state.schema.nodes.paragraph!.create());
    }
    tr = tr.setSelection(TextSelection.create(tr.doc, endPos + 1));
    view.dispatch(tr);
    view.focus();
}

function buildAndReplaceGroupNode(
    view: EditorView, pos: number, oldNode: PNode, data: ImageModalData
): void {
    const layout = data.images.length === 1 ? 'single' : (data.layout === 'single' ? 'grid' : data.layout);
    const content = data.images.map(i => `${i.src}|${i.alt}`).join('\n');
    const schema = view.state.schema;
    const newNode = schema.nodes.code_block!.create(
        { language: `image-group layout=${layout}` },
        content ? [schema.text(content)] : undefined
    );
    view.dispatch(view.state.tr.replaceWith(pos, pos + oldNode.nodeSize, newNode));
    focusAfterNode(view, pos + newNode.nodeSize);
}

// ============================================================================
// ImageGroupNodeView
// ============================================================================

class ImageGroupNodeView implements NodeView {
    dom: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;
    private currentSlide = 0;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-block-wrapper image-group';
        this.dom.setAttribute('contenteditable', 'false');
        this.render();
    }

    update(node: PNode): boolean {
        if (node.type !== this.node.type) return false;
        if (!node.attrs.language?.startsWith('image-group')) return false;
        this.node = node;
        this.render();
        return true;
    }

    private render(): void {
        this.dom.innerHTML = '';
        const language: string = this.node.attrs.language ?? 'image-group layout=grid';
        const layout = parseLayout(language);
        const images = parseLines(this.node.textContent);

        const inner = document.createElement('div');
        inner.className = 'sn-img-group-node';

        // ── Hover controls (Edit + Delete) ────────────────────────────────
        const controls = buildBlockControls([
            { label: 'Edit', onMousedown: () => {
                if (!imageEditHandler) return;
                imageEditHandler({ images, layout }, this.view, (newData) => {
                    const pos = this.getPos();
                    if (pos === undefined) return;
                    buildAndReplaceGroupNode(this.view, pos, this.node, newData);
                });
            }},
            { label: '×', title: 'Remove image group', isDelete: true, onMousedown: () => {
                const pos = this.getPos();
                if (pos == null) return;
                this.view.dispatch(this.view.state.tr.delete(pos, pos + this.node.nodeSize));
            }},
        ]);
        this.dom.appendChild(inner);
        this.dom.appendChild(controls);
        this.dom.appendChild(buildBlockLabel('Image Group'));

        // ── Thumbnails ────────────────────────────────────────────────────
        if (images.length === 0) {
            const thumbsRow = document.createElement('div');
            thumbsRow.className = 'sn-img-group-thumbs';
            const empty = document.createElement('span');
            empty.className = 'sn-img-group-empty';
            empty.textContent = 'No images';
            thumbsRow.appendChild(empty);
            inner.appendChild(thumbsRow);
        } else if (images.length === 1) {
            const img = images[0]!;
            const wrap = document.createElement('div');
            wrap.className = 'ig-single';
            wrap.dataset.full = img.src;

            const imgEl = document.createElement('img');
            imgEl.src = img.src;
            imgEl.alt = img.alt;

            const onLoad = () => {
                if (imgEl.naturalWidth > 0) {
                    wrap.style.maxWidth = `${imgEl.naturalWidth}px`;
                }
            };
            if (imgEl.complete && imgEl.naturalWidth > 0) {
                onLoad();
            } else {
                imgEl.addEventListener('load', onLoad, { once: true });
            }

            wrap.appendChild(imgEl);
            inner.appendChild(wrap);

        } else if (layout === 'carousel') {
            this.currentSlide = Math.min(this.currentSlide, images.length - 1);
            const carouselEl = document.createElement('div');
            carouselEl.className = 'gup-carousel sn-img-carousel-preview';

            const track = document.createElement('div');
            track.className = 'gup-carousel-track';
            track.style.transform = `translateX(-${this.currentSlide * 100}%)`;

            images.forEach((img) => {
                const item = document.createElement('div');
                item.className = 'images-upload-item';
                item.style.backgroundImage = `url(${img.src})`;
                item.style.backgroundSize = 'cover';
                item.style.backgroundPosition = 'center';
                const imgEl = document.createElement('img');
                imgEl.src = img.src;
                imgEl.alt = img.alt;
                if (img.src.startsWith('blob:')) {
                    item.classList.add('images-item-loaded');
                } else if (imgEl.complete) {
                    item.classList.add('images-item-loaded');
                } else {
                    imgEl.addEventListener('load', () => item.classList.add('images-item-loaded'), { once: true });
                }
                item.appendChild(imgEl);
                track.appendChild(item);
            });
            carouselEl.appendChild(track);

            if (images.length > 1) {
                const counter = document.createElement('span');
                counter.className = 'gup-carousel-counter';
                counter.textContent = `${this.currentSlide + 1} / ${images.length}`;

                const prevBtn = document.createElement('button');
                prevBtn.type = 'button';
                prevBtn.className = 'gup-carousel-arrow gup-carousel-prev';
                prevBtn.innerHTML = SVG_CHEVRON_LEFT;
                prevBtn.title = 'Previous';
                prevBtn.addEventListener('mousedown', e => {
                    e.preventDefault();
                    this.currentSlide = (this.currentSlide - 1 + images.length) % images.length;
                    track.style.transform = `translateX(-${this.currentSlide * 100}%)`;
                    counter.textContent = `${this.currentSlide + 1} / ${images.length}`;
                });

                const nextBtn = document.createElement('button');
                nextBtn.type = 'button';
                nextBtn.className = 'gup-carousel-arrow gup-carousel-next';
                nextBtn.innerHTML = SVG_CHEVRON_RIGHT;
                nextBtn.title = 'Next';
                nextBtn.addEventListener('mousedown', e => {
                    e.preventDefault();
                    this.currentSlide = (this.currentSlide + 1) % images.length;
                    track.style.transform = `translateX(-${this.currentSlide * 100}%)`;
                    counter.textContent = `${this.currentSlide + 1} / ${images.length}`;
                });

                carouselEl.appendChild(prevBtn);
                carouselEl.appendChild(nextBtn);
                carouselEl.appendChild(counter);
            }

            inner.appendChild(carouselEl);
        } else {
            const thumbsRow = document.createElement('div');
            thumbsRow.className = `sn-img-group-thumbs gup-${layout}`;

            images.forEach((img, i) => {
                const cell = document.createElement('div');
                cell.className = 'images-upload-item';
                cell.dataset.imgIdx = String(i);

                if (layout === 'grid' || layout === 'justified') {
                    cell.style.backgroundImage = `url(${img.src})`;
                    cell.style.backgroundSize = 'cover';
                    cell.style.backgroundPosition = 'center';
                }

                const imgEl = document.createElement('img');
                imgEl.src = img.src;
                imgEl.alt = img.alt;
                if (img.src.startsWith('blob:')) {
                    cell.classList.add('images-item-loaded');
                } else if (layout === 'grid' || layout === 'justified') {
                    if (imgEl.complete) cell.classList.add('images-item-loaded');
                    else imgEl.addEventListener('load', () => cell.classList.add('images-item-loaded'), { once: true });
                }
                cell.appendChild(imgEl);

                thumbsRow.appendChild(cell);
            });

            inner.appendChild(thumbsRow);
        }
    }

    stopEvent(event: Event): boolean {
        return this.dom.contains(event.target as HTMLElement);
    }

    ignoreMutation(): boolean {
        return true;
    }

    destroy(): void {}
}

// ============================================================================
// DefaultCodeBlockNodeView — standard <pre><code> for all other code blocks
// ============================================================================

class DefaultCodeBlockNodeView implements NodeView {
    dom: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;
    private codeEl: HTMLElement;
    private langLabel: HTMLElement;
    private gutter: HTMLElement;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-block-wrapper code-block';
        this.dom.contentEditable = 'false';

        const inner = document.createElement('div');
        inner.className = 'code-node-view';

        // Hover controls
        const controls = buildBlockControls([
            { label: 'Edit', onMousedown: () => this.openEdit() },
            { label: '×', isDelete: true, onMousedown: () => {
                const pos = this.getPos();
                if (pos === undefined) return;
                this.view.dispatch(this.view.state.tr.delete(pos, pos + this.node.nodeSize));
            }},
        ]);
        this.dom.appendChild(controls);

        // Header bar with language label
        const header = document.createElement('div');
        header.className = 'code-block-header';
        this.langLabel = document.createElement('span');
        this.langLabel.className = 'code-language-label';
        header.appendChild(this.langLabel);
        inner.appendChild(header);

        // Body: gutter + pre
        const body = document.createElement('div');
        body.className = 'code-node-body';

        this.gutter = document.createElement('div');
        this.gutter.className = 'code-node-gutter';
        this.gutter.setAttribute('aria-hidden', 'true');

        const pre = document.createElement('pre');
        this.codeEl = document.createElement('code');
        pre.appendChild(this.codeEl);
        body.append(this.gutter, pre);
        inner.appendChild(body);
        this.dom.appendChild(inner);
        this.dom.appendChild(buildBlockLabel('Code'));

        this.renderPreview();
    }

    private updateGutter(code: string): void {
        const lineCount = code.split('\n').length;
        while (this.gutter.childElementCount < lineCount) {
            this.gutter.appendChild(document.createElement('span'));
        }
        while (this.gutter.childElementCount > lineCount) {
            this.gutter.lastChild!.remove();
        }
    }

    private renderPreview(): void {
        const lang = (this.node.attrs as { language?: string }).language ?? '';
        const code = this.node.textContent;
        const prism = (window as any).Prism;
        const grammar = prism?.languages?.[lang];
        if (grammar && prism) {
            this.codeEl.innerHTML = prism.highlight(code, grammar, lang);
        } else {
            this.codeEl.textContent = code;
            // Prism not ready yet — retry once it loads, but only if lang is known
            if (lang) (window as any).SnakkSyntax?.loadPrism?.().then((p: any) => {
                if (!p || !(window as any).Prism?.languages?.[lang]) return;
                this.renderPreview();
            });
        }
        this.codeEl.className = lang ? `language-${lang}` : '';
        this.langLabel.textContent = LANG_LABELS[lang] ?? (lang || 'Plain text');
        this.updateGutter(code);
    }

    private openEdit(): void {
        if (!codeEditHandler) return;
        const lang = (this.node.attrs as { language?: string }).language ?? '';
        codeEditHandler({ language: lang, code: this.node.textContent }, (newData) => {
            const pos = this.getPos();
            if (pos === undefined) return;
            const schema = this.view.state.schema;
            const newNode = schema.nodes['code_block']!.create(
                { language: newData.language },
                newData.code ? [schema.text(newData.code)] : undefined
            );
            this.view.dispatch(this.view.state.tr.replaceWith(pos, pos + this.node.nodeSize, newNode));
            focusAfterNode(this.view, pos + newNode.nodeSize);
        });
    }

    update(node: PNode): boolean {
        if (node.type.name !== 'code_block') return false;
        if ((node.attrs.language as string)?.startsWith('image-group')) return false;
        if (node.attrs.language === 'chart') return false;
        if ((node.attrs.language as string)?.startsWith('callout-')) return false;
        this.node = node;
        this.renderPreview();
        return true;
    }

    ignoreMutation(): boolean { return true; }
}

// ============================================================================
// ChartNodeView — renders chart canvas + Edit/Delete controls
// ============================================================================

class ChartNodeView implements NodeView {
    dom: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;
    private canvas: HTMLCanvasElement;
    private currentText: string;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;
        this.currentText = node.textContent;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-block-wrapper chart-block';
        this.dom.contentEditable = 'false';

        this.canvas = document.createElement('canvas');
        this.dom.appendChild(this.canvas);
        this.dom.appendChild(buildBlockLabel('Chart'));

        const controls = buildBlockControls([
            { label: 'Edit', onMousedown: () => this.openEdit() },
            { label: '×', title: 'Remove chart', isDelete: true, onMousedown: () => {
                const pos = this.getPos();
                if (pos == null) return;
                this.view.dispatch(this.view.state.tr.delete(pos, pos + this.node.nodeSize));
            }},
        ]);
        this.dom.appendChild(controls);

        this.renderChart(this.currentText);
    }

    private renderChart(config: string): void {
        (window as any).SnakkCharts?.destroyCanvas?.(this.canvas);
        if (config.trim()) {
            setTimeout(() => (window as any).SnakkCharts?.renderIntoCanvas?.(this.canvas, config), 0);
        }
    }

    private openEdit(): void {
        if (!chartEditHandler) return;
        const config = this.node.textContent;
        const typeMatch = config.match(/^type:\s*(\w+)/m);
        const chartType = typeMatch?.[1] ?? 'bar';
        chartEditHandler(chartType, config, (newConfig) => {
            const pos = this.getPos();
            if (pos == null) return;
            const node = this.view.state.doc.nodeAt(pos);
            if (!node) return;
            const tr = this.view.state.tr.replaceWith(
                pos + 1,
                pos + 1 + node.content.size,
                this.view.state.schema.text(newConfig),
            );
            this.view.dispatch(tr);
            const updatedNode = this.view.state.doc.nodeAt(pos);
            if (updatedNode) focusAfterNode(this.view, pos + updatedNode.nodeSize);
        });
    }

    update(node: PNode): boolean {
        if (node.type.name !== 'code_block' || node.attrs.language !== 'chart') return false;
        if (node.textContent !== this.currentText) {
            this.node = node;
            this.currentText = node.textContent;
            this.renderChart(this.currentText);
        }
        return true;
    }

    ignoreMutation(): boolean { return true; }
    stopEvent(): boolean { return false; }

    destroy(): void {
        (window as any).SnakkCharts?.destroyCanvas?.(this.canvas);
    }
}

// ============================================================================
// CalloutNodeView — styled admonition block with editable body text
// ============================================================================

class CalloutNodeView implements NodeView {
    dom: HTMLElement;
    contentDOM: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        const type = (node.attrs.language as string)?.replace('callout-', '') ?? 'info';
        const meta = CALLOUT_META[type] ?? CALLOUT_META['info']!;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-block-wrapper callout-block';

        const inner = document.createElement('div');
        inner.className = `callout-node-view callout-node-${type}`;

        const icon = document.createElement('span');
        icon.className = 'callout-node-icon';
        icon.textContent = meta.icon;
        icon.contentEditable = 'false';

        this.contentDOM = document.createElement('div');
        this.contentDOM.className = 'callout-node-body';

        inner.appendChild(icon);
        inner.appendChild(this.contentDOM);

        const controls = buildBlockControls([
            { label: '×', title: 'Remove callout', isDelete: true, onMousedown: () => {
                const pos = this.getPos();
                if (pos == null) return;
                this.view.dispatch(this.view.state.tr.delete(pos, pos + this.node.nodeSize));
            }},
        ]);

        this.dom.appendChild(inner);
        this.dom.appendChild(controls);
        this.dom.appendChild(buildBlockLabel('Callout'));
    }

    update(node: PNode): boolean {
        if (node.type.name !== 'code_block') return false;
        if (!node.attrs.language?.startsWith('callout-')) return false;
        this.node = node;
        return true;
    }

    ignoreMutation(mutation: { target: Node }): boolean {
        return !this.contentDOM.contains(mutation.target);
    }

    stopEvent(event: Event): boolean {
        return event.target === this.dom;
    }
}

// ============================================================================
// TableNodeView — read-only table widget with Edit/Delete controls
// ============================================================================

function extractTableData(node: PNode): TableModalData {
    const headers: string[] = [];
    const rows: string[][] = [];
    const alignments: Array<'left' | 'center' | 'right' | null> = [];

    node.forEach((rowNode) => {
        if (rowNode.type.name === 'table_header_row') {
            rowNode.forEach((cell, _offset, i) => {
                headers.push(cell.textContent);
                if (i === 0 || alignments.length <= i) {
                    const align = cell.attrs['alignment'] as string | null;
                    alignments[i] = (align === 'left' || align === 'center' || align === 'right') ? align : null;
                }
            });
        } else if (rowNode.type.name === 'table_row') {
            const rowCells: string[] = [];
            rowNode.forEach((cell) => { rowCells.push(cell.textContent); });
            rows.push(rowCells);
        }
    });

    return { headers, rows, alignments };
}

class TableNodeView implements NodeView {
    dom: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;
    private tableWrapper: HTMLElement;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-block-wrapper table-block';
        this.dom.contentEditable = 'false';

        this.tableWrapper = document.createElement('div');
        this.tableWrapper.className = 'table-node-table';

        const controls = buildBlockControls([
            { label: 'Edit', onMousedown: () => this.openEdit() },
            { label: '×', title: 'Remove table', isDelete: true, onMousedown: () => {
                const pos = this.getPos();
                if (pos == null) return;
                this.view.dispatch(this.view.state.tr.delete(pos, pos + this.node.nodeSize));
            }},
        ]);
        this.dom.appendChild(this.tableWrapper);
        this.dom.appendChild(controls);
        this.dom.appendChild(buildBlockLabel('Table'));

        this.renderTable(node);
    }

    private renderTable(node: PNode): void {
        this.tableWrapper.innerHTML = '';
        const table = document.createElement('table');
        const thead = document.createElement('thead');
        const tbody = document.createElement('tbody');

        node.forEach((rowNode) => {
            const tr = document.createElement('tr');
            if (rowNode.type.name === 'table_header_row') {
                rowNode.forEach((cell) => {
                    const th = document.createElement('th');
                    th.textContent = cell.textContent;
                    const align = cell.attrs['alignment'] as string | null;
                    if (align) th.style.textAlign = align;
                    tr.appendChild(th);
                });
                thead.appendChild(tr);
            } else {
                rowNode.forEach((cell) => {
                    const td = document.createElement('td');
                    td.textContent = cell.textContent;
                    const align = cell.attrs['alignment'] as string | null;
                    if (align) td.style.textAlign = align;
                    tr.appendChild(td);
                });
                tbody.appendChild(tr);
            }
        });

        if (thead.childNodes.length) table.appendChild(thead);
        if (tbody.childNodes.length) table.appendChild(tbody);
        this.tableWrapper.appendChild(table);
    }

    private openEdit(): void {
        if (!tableEditHandler) return;
        tableEditHandler(extractTableData(this.node), (newData) => {
            const pos = this.getPos();
            if (pos == null) return;
            const table = this.buildTableNode(this.view.state, newData);
            if (!table) return;
            this.view.dispatch(this.view.state.tr.replaceWith(pos, pos + this.node.nodeSize, table));
            focusAfterNode(this.view, pos + table.nodeSize);
        });
    }

    private buildTableNode(state: { schema: any }, data: TableModalData): PNode | null {
        const schema = (state as any).schema;
        const makeCell = (type: string, text: string, align: string | null): PNode => {
            const content = text.trim() ? [schema.text(text)] : [];
            return schema.nodes[type].create(
                { alignment: align },
                content.length ? content : undefined,
            );
        };
        try {
            const headerCells = data.headers.map((h, i) =>
                makeCell('table_header', h, data.alignments[i] ?? null));
            const headerRow = schema.nodes.table_header_row.create(null, headerCells);
            const bodyRows = data.rows.map(row =>
                schema.nodes.table_row.create(null,
                    row.map((cell, i) => makeCell('table_cell', cell, data.alignments[i] ?? null))));
            return schema.nodes.table.create(null, [headerRow, ...bodyRows]);
        } catch {
            return null;
        }
    }

    update(node: PNode): boolean {
        if (node.type.name !== 'table') return false;
        this.node = node;
        this.renderTable(node);
        return true;
    }

    ignoreMutation(): boolean { return true; }
    stopEvent(): boolean { return false; }
}

// ============================================================================
// SingleImageNodeView — compact inline preview for standalone uploaded images
// ============================================================================

class SingleImageNodeView implements NodeView {
    dom: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        this.dom = document.createElement('span');
        this.dom.className = 'sn-single-img-node';
        this.dom.setAttribute('contenteditable', 'false');
        this.dom.draggable = true;
        this.render();
        this.dom.addEventListener('dragstart', (e) => {
            const src = this.node.attrs.src as string;
            const alt = (this.node.attrs.alt as string) ?? '';
            e.dataTransfer!.effectAllowed = 'move';
            e.dataTransfer!.setData('application/x-snakk-img', JSON.stringify({ src, alt }));
            e.stopPropagation();
        });
    }

    update(node: PNode): boolean {
        if (node.type !== this.node.type) return false;
        this.node = node;
        this.render();
        return true;
    }

    private render(): void {
        this.dom.innerHTML = '';

        const src = this.node.attrs.src as string;
        const alt = (this.node.attrs.alt as string) ?? '';

        this.dom.classList.remove('sn-img-uploading');
        delete this.dom.dataset.uploadSrc;

        this.dom.style.backgroundImage = `url(${src})`;
        this.dom.style.backgroundSize = 'cover';
        this.dom.style.backgroundPosition = 'center';

        const imgEl = document.createElement('img');
        imgEl.src = src;
        imgEl.alt = alt;
        imgEl.className = 'sn-single-img-thumb';
        this.dom.appendChild(imgEl);

        // Hover controls (Edit + Delete)
        const defs: BlockControlDef[] = [];
        if (!src.startsWith('blob:')) {
            defs.push({ label: 'Edit', onMousedown: () => {
                if (!imageEditHandler) return;
                imageEditHandler({ images: [{ src, alt }], layout: 'grid' }, this.view, (newData) => {
                    const pos = this.getPos();
                    if (pos === undefined) return;
                    if (newData.images.length === 1) {
                        const newImg = newData.images[0]!;
                        this.view.dispatch(this.view.state.tr.setNodeMarkup(pos, undefined, {
                            ...this.node.attrs,
                            src: newImg.src,
                            alt: newImg.alt,
                        }));
                    } else {
                        // Promote to image group
                        const state = this.view.state;
                        const $pos = state.doc.resolve(pos);
                        const paraStart = $pos.depth > 0 ? $pos.before($pos.depth) : pos;
                        const paraEnd = $pos.depth > 0 ? $pos.after($pos.depth) : pos + this.node.nodeSize;
                        const content = newData.images.map(i => `${i.src}|${i.alt}`).join('\n');
                        const groupBlock = state.schema.nodes.code_block!.create(
                            { language: `image-group layout=${newData.layout}` },
                            [state.schema.text(content)]
                        );
                        const emptyPara = state.schema.nodes.paragraph!.create();
                        this.view.dispatch(state.tr.replaceWith(
                            paraStart, paraEnd, Fragment.from([groupBlock, emptyPara])
                        ));
                    }
                });
            }});
        }
        defs.push({ label: '×', title: 'Remove image', isDelete: true, onMousedown: () => {
            removeImageFromEditor(this.view, src);
        }});
        const controls = buildBlockControls(defs);
        this.dom.appendChild(controls);
    }

    stopEvent(event: Event): boolean {
        return this.dom.contains(event.target as Node);
    }

    ignoreMutation(): boolean {
        return true;
    }

    destroy(): void {}
}

// ============================================================================
// removeImageFromEditor — remove all occurrences of a src URL from the doc
// ============================================================================

export function removeImageFromEditor(view: EditorView, src: string): void {
    const state = view.state;
    type Op = { from: number; to: number; replacement?: PNode };
    const ops: Op[] = [];

    state.doc.descendants((node, pos) => {
        if (node.type.name === 'image' && node.attrs.src === src) {
            const $pos = state.doc.resolve(pos);
            if ($pos.depth > 0 && $pos.parent.childCount === 1) {
                ops.push({ from: $pos.before($pos.depth), to: $pos.after($pos.depth) });
            } else {
                ops.push({ from: pos, to: pos + node.nodeSize });
            }
        } else if (
            node.type.name === 'code_block' &&
            node.attrs.language?.startsWith('image-group') &&
            node.textContent.includes(src)
        ) {
            const lines = node.textContent.split('\n');
            const remaining = lines.filter(l => {
                const lineSrc = l.indexOf('|') >= 0 ? l.slice(0, l.indexOf('|')).trim() : l.trim();
                return lineSrc !== '' && lineSrc !== src;
            });
            if (remaining.length === 0) {
                ops.push({ from: pos, to: pos + node.nodeSize });
            } else {
                const newText = remaining.join('\n');
                const replacement = node.type.create(node.attrs, [state.schema.text(newText)]);
                ops.push({ from: pos, to: pos + node.nodeSize, replacement });
            }
        }
    });

    if (ops.length === 0) return;

    ops.sort((a, b) => b.from - a.from);
    let tr = state.tr;
    for (const op of ops) {
        if (op.replacement) {
            tr = tr.replaceWith(op.from, op.to, op.replacement);
        } else {
            tr = tr.delete(op.from, op.to);
        }
    }
    view.dispatch(tr);
}

// ============================================================================
// replaceImageSrc — swap one src URL for another everywhere in the doc
// ============================================================================

export function replaceImageSrc(view: EditorView, oldSrc: string, newSrc: string): void {
    const state = view.state;
    let tr = state.tr;
    let changed = false;

    state.doc.descendants((node, pos) => {
        if (node.type.name === 'image' && node.attrs.src === oldSrc) {
            tr = tr.setNodeMarkup(pos, undefined, { ...node.attrs, src: newSrc });
            changed = true;
        } else if (
            node.type.name === 'code_block' &&
            node.attrs.language?.startsWith('image-group') &&
            node.textContent.includes(oldSrc)
        ) {
            const newText = node.textContent.split(oldSrc).join(newSrc);
            const newNode = node.type.create(node.attrs, [state.schema.text(newText)]);
            tr = tr.replaceWith(pos, pos + node.nodeSize, newNode);
            changed = true;
        }
    });

    if (changed) view.dispatch(tr);
}

// ============================================================================
// Plugin export
// ============================================================================

// ============================================================================
// List NodeView — bullet_list and ordered_list rendered read-only with edit button
// ============================================================================

function extractListData(node: PNode): ListModalData {
    const items: ListItem[] = [];
    node.forEach((child) => {
        if (child.type.name === 'list_item') {
            const text = child.textContent;
            const checked = (child.attrs as any).checked;
            if (checked !== null && checked !== undefined) {
                items.push({ text, checked: !!checked });
            } else {
                items.push({ text });
            }
        }
    });
    const isTask = items.some(i => i.checked !== undefined);
    const type = node.type.name === 'ordered_list' ? 'ordered' : (isTask ? 'task' : 'bullet');
    return { type, items };
}

export function buildListNode(schema: any, data: ListModalData): PNode | null {
    try {
        const safeItems = data.items.length > 0 ? data.items : [{ text: '' }];
        const listItems = safeItems.map(item => {
            const textContent = item.text.trim() ? [schema.text(item.text)] : [];
            const para = schema.nodes.paragraph!.create(null, textContent.length ? textContent : undefined);
            const attrs = data.type === 'task' ? { checked: item.checked ?? false } : {};
            return schema.nodes.list_item!.create(attrs, [para]);
        });
        if (data.type === 'ordered') {
            return schema.nodes.ordered_list!.create(null, listItems);
        } else {
            return schema.nodes.bullet_list!.create(null, listItems);
        }
    } catch {
        return null;
    }
}

class ListNodeView implements NodeView {
    dom: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;
    private listEl: HTMLElement;
    private labelEl: HTMLElement;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-block-wrapper';
        this.dom.contentEditable = 'false';

        this.listEl = document.createElement(node.type.name === 'ordered_list' ? 'ol' : 'ul');
        this.listEl.className = 'list-node-view-list';

        const controls = buildBlockControls([
            { label: 'Edit', onMousedown: () => this.openEdit() },
            { label: '×', title: 'Remove list', isDelete: true, onMousedown: () => {
                const pos = this.getPos();
                if (pos == null) return;
                this.view.dispatch(this.view.state.tr.delete(pos, pos + this.node.nodeSize));
            }},
        ]);
        this.labelEl = buildBlockLabel('List');
        this.dom.appendChild(this.listEl);
        this.dom.appendChild(controls);
        this.dom.appendChild(this.labelEl);

        this.renderList(node);
    }

    private renderList(node: PNode): void {
        this.listEl.innerHTML = '';
        const data = extractListData(node);
        this.dom.classList.remove('bullet-list', 'ordered-list', 'check-list');
        const typeClass = data.type === 'ordered' ? 'ordered-list' : data.type === 'task' ? 'check-list' : 'bullet-list';
        this.dom.classList.add(typeClass);
        if (this.labelEl) {
            this.labelEl.textContent =
                data.type === 'ordered' ? 'Ordered List' :
                data.type === 'task'    ? 'Task List' : 'Bullet List';
        }

        for (const item of data.items) {
            const li = document.createElement('li');
            li.className = 'list-node-view-item';

            if (data.type === 'task') {
                li.className += ' list-node-view-item-task';
                const cb = document.createElement('input');
                cb.type = 'checkbox';
                cb.checked = item.checked ?? false;
                cb.disabled = true;
                cb.className = 'list-node-view-checkbox';
                li.appendChild(cb);
            }

            const span = document.createElement('span');
            span.textContent = item.text;
            li.appendChild(span);
            this.listEl.appendChild(li);
        }
    }

    private openEdit(): void {
        if (!listEditHandler) return;
        listEditHandler(extractListData(this.node), (newData) => {
            const pos = this.getPos();
            if (pos == null) return;
            const newNode = buildListNode(this.view.state.schema, newData);
            if (!newNode) return;
            this.view.dispatch(this.view.state.tr.replaceWith(pos, pos + this.node.nodeSize, newNode));
            focusAfterNode(this.view, pos + newNode.nodeSize);
        });
    }

    update(node: PNode): boolean {
        if (node.type !== this.node.type) return false;
        this.node = node;
        this.renderList(node);
        return true;
    }

    ignoreMutation(): boolean { return true; }
    stopEvent(): boolean { return false; }
}

// ============================================================================
// BlockquoteNodeView — editable blockquote with delete control
// ============================================================================

class BlockquoteNodeView implements NodeView {
    dom: HTMLElement;
    contentDOM: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-block-wrapper blockquote-block';

        this.contentDOM = document.createElement('blockquote');
        this.contentDOM.className = 'blockquote-node-body';

        const controls = buildBlockControls([
            { label: '×', title: 'Remove blockquote', isDelete: true, onMousedown: () => {
                const pos = this.getPos();
                if (pos == null) return;
                this.view.dispatch(this.view.state.tr.delete(pos, pos + this.node.nodeSize));
            }},
        ]);

        this.dom.appendChild(this.contentDOM);
        this.dom.appendChild(controls);
        this.dom.appendChild(buildBlockLabel('Blockquote'));
    }

    update(node: PNode): boolean {
        if (node.type.name !== 'blockquote') return false;
        this.node = node;
        return true;
    }

    ignoreMutation(mutation: { target: Node }): boolean {
        return !this.contentDOM.contains(mutation.target);
    }

    stopEvent(event: Event): boolean {
        return event.target === this.dom;
    }
}

// ============================================================================
// HorizontalRuleNodeView — read-only divider with delete control
// ============================================================================

class HorizontalRuleNodeView implements NodeView {
    dom: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-block-wrapper hr-block';
        this.dom.contentEditable = 'false';

        const hr = document.createElement('hr');
        hr.className = 'hr-node-rule';

        const controls = buildBlockControls([
            { label: '×', title: 'Remove divider', isDelete: true, onMousedown: () => {
                const pos = this.getPos();
                if (pos == null) return;
                this.view.dispatch(this.view.state.tr.delete(pos, pos + this.node.nodeSize));
            }},
        ]);

        this.dom.appendChild(hr);
        this.dom.appendChild(controls);
        this.dom.appendChild(buildBlockLabel('Divider'));
    }

    update(node: PNode): boolean {
        if (node.type.name !== 'horizontal_rule') return false;
        this.node = node;
        return true;
    }

    ignoreMutation(): boolean { return true; }
    stopEvent(): boolean { return false; }
}

export function createImageGroupPlugin() {
    return $prose(() => new Plugin({
        key: IMAGE_GROUP_KEY,
        props: {
            handleDOMEvents: {
                drop(view, event) {
                    const de = event as DragEvent;
                    const data = de.dataTransfer?.getData('application/x-snakk-img');
                    if (!data) return false;
                    de.preventDefault();
                    de.stopPropagation();

                    const { src, alt } = JSON.parse(data) as { src: string; alt: string };

                    // Capture drop position from DOM BEFORE any doc changes to avoid position drift
                    const resolved = view.posAtCoords({ left: de.clientX, top: de.clientY });

                    const state = view.state;
                    let tr = state.tr;

                    // Build removal in the same transaction (mirrors removeImageFromEditor)
                    state.doc.descendants((node, pos) => {
                        if (node.type.name === 'image' && node.attrs.src === src) {
                            const $p = state.doc.resolve(pos);
                            if ($p.depth > 0 && $p.parent.childCount === 1)
                                tr = tr.delete($p.before($p.depth), $p.after($p.depth));
                            else
                                tr = tr.delete(pos, pos + node.nodeSize);
                        } else if (
                            node.type.name === 'code_block' &&
                            node.attrs.language?.startsWith('image-group') &&
                            node.textContent.includes(src)
                        ) {
                            const lines = node.textContent.split('\n');
                            const remaining = lines.filter(l => {
                                const s = l.indexOf('|') >= 0 ? l.slice(0, l.indexOf('|')).trim() : l.trim();
                                return s !== '' && s !== src;
                            });
                            if (remaining.length === 0) {
                                tr = tr.delete(pos, pos + node.nodeSize);
                            } else {
                                const newNode = node.type.create(
                                    node.attrs, [state.schema.text(remaining.join('\n'))]
                                );
                                tr = tr.replaceWith(pos, pos + node.nodeSize, newNode);
                            }
                        }
                    });

                    // Map the pre-captured drop position through all removal steps
                    const rawInsert = resolved ? tr.mapping.map(resolved.pos) : tr.doc.content.size;
                    const safeInsert = Math.min(rawInsert, tr.doc.content.size);
                    const $ins = tr.doc.resolve(safeInsert);
                    const blockEnd = Math.min(
                        $ins.depth > 0 ? $ins.after($ins.depth) : safeInsert,
                        tr.doc.content.size
                    );

                    const imageNode = state.schema.nodes.image?.create({ src, alt });
                    if (!imageNode) { view.dispatch(tr); return true; }
                    const emptyPara = state.schema.nodes.paragraph!.create();
                    const imagePara = state.schema.nodes.paragraph!.create({}, [imageNode]);
                    tr = tr.insert(blockEnd, Fragment.from([imagePara, emptyPara]));
                    tr = tr.setSelection(TextSelection.create(
                        tr.doc, Math.min(blockEnd + imagePara.nodeSize + 1, tr.doc.content.size - 1)
                    ));
                    view.dispatch(tr);
                    view.focus();
                    return true;
                },
            },
            nodeViews: {
                code_block: (node: PNode, view: EditorView, getPos: () => number | undefined) => {
                    if (node.attrs.language?.startsWith('image-group')) {
                        return new ImageGroupNodeView(node, view, getPos);
                    }
                    if (node.attrs.language === 'chart') {
                        return new ChartNodeView(node, view, getPos);
                    }
                    if (node.attrs.language?.startsWith('callout-')) {
                        return new CalloutNodeView(node, view, getPos);
                    }
                    return new DefaultCodeBlockNodeView(node, view, getPos);
                },
                image: (node: PNode, view: EditorView, getPos: () => number | undefined) => {
                    return new SingleImageNodeView(node, view, getPos);
                },
                table: (node: PNode, view: EditorView, getPos: () => number | undefined) => {
                    return new TableNodeView(node, view, getPos);
                },
                bullet_list: (node: PNode, view: EditorView, getPos: () => number | undefined) => {
                    return new ListNodeView(node, view, getPos);
                },
                ordered_list: (node: PNode, view: EditorView, getPos: () => number | undefined) => {
                    return new ListNodeView(node, view, getPos);
                },
                blockquote: (node: PNode, view: EditorView, getPos: () => number | undefined) => {
                    return new BlockquoteNodeView(node, view, getPos);
                },
                horizontal_rule: (node: PNode, view: EditorView, getPos: () => number | undefined) => {
                    return new HorizontalRuleNodeView(node, view, getPos);
                },
            },
        },
        appendTransaction(transactions, _oldState, newState) {
            if (!transactions.some(tr => tr.docChanged)) return null;

            type Op = { from: number; to: number; nodes: PNode[] };
            const ops: Op[] = [];

            // Pass 1: split paragraphs that either mix image nodes with text, or contain
            // multiple standalone image nodes (hard block rule — images inside image-group
            // code_blocks intentionally flow and are not touched here).
            newState.doc.descendants((node, pos) => {
                if (node.type.name !== 'paragraph') return;

                let imageCount = 0;
                let hasNonImage = false;
                node.forEach(child => {
                    if (child.type.name === 'image') imageCount++;
                    else if (!child.isText || (child.text ?? '').trim().length > 0) hasNonImage = true;
                });
                if (imageCount === 0) return;
                if (imageCount === 1 && !hasNonImage) return;

                const schema = newState.schema;
                const newParas: PNode[] = [];
                let buffer: PNode[] = [];

                node.forEach(child => {
                    if (child.type.name === 'image') {
                        if (buffer.some(n => !n.isText || (n.text ?? '').trim().length > 0)) {
                            newParas.push(schema.nodes.paragraph!.create(null, buffer));
                        }
                        buffer = [];
                        newParas.push(schema.nodes.paragraph!.create(null, [child]));
                    } else {
                        buffer.push(child);
                    }
                });
                if (buffer.some(n => !n.isText || (n.text ?? '').trim().length > 0)) {
                    newParas.push(schema.nodes.paragraph!.create(null, buffer));
                }

                if (newParas.length > 1) {
                    ops.push({ from: pos, to: pos + node.nodeSize, nodes: newParas });
                }
            });

            // Pass 2: ensure typeable paragraphs exist around non-typeable blocks (code_block,
            // horizontal_rule) so the cursor always has somewhere to land. The gap cursor plugin
            // handles in-between navigation, but the doc must have at least one text position.
            const schema = newState.schema;
            const docChildren: Array<{ node: PNode; pos: number }> = [];
            newState.doc.forEach((node, offset) => docChildren.push({ node, pos: offset }));

            // Paragraph before the first child if it's non-typeable
            if (docChildren.length > 0 && isNonTypeableBlock(docChildren[0]!.node)) {
                ops.push({ from: 0, to: 0, nodes: [schema.nodes.paragraph!.create()] });
            }

            for (let i = 0; i < docChildren.length; i++) {
                const { node, pos } = docChildren[i]!;
                if (!isNonTypeableBlock(node)) continue;
                const next = docChildren[i + 1];
                // Insert after this block when it's last OR when the next sibling is also non-typeable
                if (!next || isNonTypeableBlock(next.node)) {
                    const insertPos = pos + node.nodeSize;
                    ops.push({ from: insertPos, to: insertPos, nodes: [schema.nodes.paragraph!.create()] });
                }
            }

            if (ops.length === 0) return null;

            const tr = newState.tr;
            for (const op of ops.sort((a, b) => b.from - a.from)) {
                tr.replaceWith(op.from, op.to, Fragment.from(op.nodes));
            }
            return tr;
        },
    }));
}

export function createGapCursorPlugin() {
    return $prose(() => gapCursor());
}

export function createGapParaPlugin() {
    return $prose(() => new Plugin<DecorationSet>({
        key: GAP_PARA_KEY,
        state: {
            init(_config, state) {
                return buildGapParaDecos(state.doc, state.selection.$from.pos);
            },
            apply(tr, old, _oldState, newState) {
                if (!tr.docChanged && !tr.selectionSet) return old;
                return buildGapParaDecos(newState.doc, newState.selection.$from.pos);
            },
        },
        props: {
            decorations(state) {
                return GAP_PARA_KEY.getState(state) ?? DecorationSet.empty;
            },
        },
    }));
}

// ============================================================================
// Empty Paragraph Cursor Plugin
// Fires a callback when the cursor enters/leaves a top-level empty paragraph.
// createBlockAdder in markdown-editor.ts registers the callback after editor
// init to show the "+" button via keyboard navigation, not just mouse hover.
// ============================================================================

let _onCursorEmptyPara: ((pos: number | null) => void) | null = null;

export function setEmptyParaCursorCallback(cb: (pos: number | null) => void): void {
    _onCursorEmptyPara = cb;
}

export function createEmptyParaCursorPlugin() {
    return $prose(() => new Plugin({
        view() {
            return {
                update(view: EditorView, prevState) {
                    const { state } = view;
                    if (state.selection.eq(prevState.selection) && state.doc.eq(prevState.doc)) return;
                    const $from = state.selection.$from;
                    if ($from.depth === 1 && isEmptyParagraph($from.parent)) {
                        _onCursorEmptyPara?.($from.before(1));
                    } else {
                        _onCursorEmptyPara?.(null);
                    }
                },
            };
        },
    }));
}
