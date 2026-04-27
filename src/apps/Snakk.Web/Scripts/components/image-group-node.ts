/**
 * Image Group NodeView for the Milkdown/ProseMirror editor.
 * Overrides code_block rendering: image-group language → rich thumbnail widget,
 * everything else → standard <pre><code> with ProseMirror-managed content.
 */

import { $prose } from '@milkdown/kit/utils';
import { Fragment } from '@milkdown/kit/prose/model';
import type { Node as PNode } from '@milkdown/kit/prose/model';
import type { EditorView, NodeView } from '@milkdown/kit/prose/view';
import { Plugin, PluginKey, TextSelection } from '@milkdown/kit/prose/state';

const IMAGE_GROUP_KEY = new PluginKey('snakkImageGroup');
const LAYOUTS = ['grid', 'masonry', 'justified', 'carousel'] as const;
type Layout = typeof LAYOUTS[number];

const LAYOUT_LABELS: Record<Layout, string> = {
    grid: 'Grid',
    masonry: 'Masonry',
    justified: 'Justified',
    carousel: 'Carousel',
};

// Inline SVGs for toolbar icons
const SVG_LAYOUT = `<svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5">
  <rect x="1" y="1" width="6" height="6" rx="1"/><rect x="9" y="1" width="6" height="6" rx="1"/>
  <rect x="1" y="9" width="6" height="6" rx="1"/><rect x="9" y="9" width="6" height="6" rx="1"/>
</svg>`;
const SVG_CHEVRON = `<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M2 3.5l3 3 3-3"/></svg>`;
const SVG_PLUS = `<svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M8 2v12M2 8h12"/></svg>`;
const SVG_IMAGE = `<svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><rect x="1" y="3" width="14" height="10" rx="1.5"/><circle cx="5.5" cy="7" r="1.5"/><path d="M1 12l3.5-3.5L8 12l2.5-2.5L15 13"/></svg>`;
const SVG_CHEVRON_LEFT = `<svg width="14" height="14" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M6.5 2L3.5 5l3 3"/></svg>`;
const SVG_CHEVRON_RIGHT = `<svg width="14" height="14" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M3.5 2L6.5 5l-3 3"/></svg>`;
const SVG_CLOSE = `<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M1 1l8 8M9 1L1 9"/></svg>`;

function parseLayout(language: string): Layout {
    const m = /layout=(\w+)/i.exec(language);
    const val = m?.[1]?.toLowerCase() ?? 'grid';
    return (LAYOUTS as readonly string[]).includes(val) ? (val as Layout) : 'grid';
}

function setLayout(language: string, layout: Layout): string {
    return /layout=\w+/i.test(language)
        ? language.replace(/layout=\w+/i, `layout=${layout}`)
        : `image-group layout=${layout}`;
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

function showImageError(message: string): void {
    const dlg = document.createElement('dialog');
    dlg.className = 'modal modal-bottom sm:modal-middle';
    dlg.innerHTML = `
        <div class="modal-box max-w-sm">
            <h3 class="font-bold text-lg" style="color:var(--accent-danger)">Cannot upload image</h3>
            <p class="py-4 text-sm">${message}</p>
            <div class="modal-action"><button class="btn btn-sm" autofocus>OK</button></div>
        </div>`;
    (dlg.querySelector('button') as HTMLButtonElement).addEventListener('click', () => { dlg.close(); dlg.remove(); });
    document.body.appendChild(dlg);
    (dlg as HTMLDialogElement).showModal();
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
// ImageGroupNodeView
// ============================================================================

class ImageGroupNodeView implements NodeView {
    dom: HTMLElement;
    private node: PNode;
    private view: EditorView;
    private getPos: () => number | undefined;
    private dropdownEl: HTMLElement | null = null;
    private outsideClickHandler: ((e: MouseEvent) => void) | null = null;
    private currentSlide = 0;
    private dragSrcIdx: number | null = null;

    constructor(node: PNode, view: EditorView, getPos: () => number | undefined) {
        this.node = node;
        this.view = view;
        this.getPos = getPos;

        this.dom = document.createElement('div');
        this.dom.className = 'sn-img-group-node';
        this.dom.setAttribute('contenteditable', 'false');
        this.render();
        this._wireDropHandlers();
    }

    update(node: PNode): boolean {
        if (node.type !== this.node.type) return false;
        if (!node.attrs.language?.startsWith('image-group')) return false;
        this.node = node;
        this.render();
        return true;
    }

    private render(): void {
        this.closeDropdown();
        this.dom.innerHTML = '';
        const language: string = this.node.attrs.language ?? 'image-group layout=grid';
        const layout = parseLayout(language);
        const images = parseLines(this.node.textContent);

        // ── Toolbar ────────────────────────────────────────────────────────
        const toolbar = document.createElement('div');
        toolbar.className = 'sn-img-group-toolbar';

        // Layout picker: trigger button + dropdown panel
        const picker = document.createElement('div');
        picker.className = 'sn-layout-picker';

        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'sn-layout-trigger';
        trigger.title = 'Change layout';

        const labelEl = document.createElement('span');
        labelEl.className = 'sn-layout-label';
        labelEl.textContent = LAYOUT_LABELS[layout];

        trigger.innerHTML = SVG_LAYOUT;
        trigger.appendChild(labelEl);
        const chevron = document.createElement('span');
        chevron.innerHTML = SVG_CHEVRON;
        chevron.className = 'sn-layout-chevron';
        trigger.appendChild(chevron);

        const dropdown = document.createElement('div');
        dropdown.className = 'sn-layout-dropdown';
        dropdown.hidden = true;
        this.dropdownEl = dropdown;

        LAYOUTS.forEach(l => {
            const opt = document.createElement('button');
            opt.type = 'button';
            opt.className = 'sn-layout-option' + (l === layout ? ' sn-layout-option--active' : '');
            opt.textContent = LAYOUT_LABELS[l];
            opt.addEventListener('mousedown', e => {
                e.preventDefault();
                this.closeDropdown();
                this.updateLayout(l);
            });
            dropdown.appendChild(opt);
        });

        trigger.addEventListener('mousedown', e => {
            e.preventDefault();
            dropdown.hidden ? this.openDropdown() : this.closeDropdown();
        });

        picker.appendChild(trigger);
        picker.appendChild(dropdown);
        toolbar.appendChild(picker);

        // Add image button (icon only)
        const addBtn = document.createElement('button');
        addBtn.type = 'button';
        addBtn.className = 'sn-add-img-btn';
        addBtn.title = 'Add image to group';
        addBtn.innerHTML = SVG_IMAGE + SVG_PLUS;
        addBtn.addEventListener('mousedown', e => { e.preventDefault(); this.pickAndUpload(); });
        toolbar.appendChild(addBtn);

        this.dom.appendChild(toolbar);

        // ── Thumbnails ────────────────────────────────────────────────────
        if (images.length === 0) {
            const thumbsRow = document.createElement('div');
            thumbsRow.className = 'sn-img-group-thumbs';
            const empty = document.createElement('span');
            empty.className = 'sn-img-group-empty';
            empty.textContent = 'No images';
            thumbsRow.appendChild(empty);
            this.dom.appendChild(thumbsRow);
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
                if (!img.src.startsWith('blob:')) {
                    item.style.backgroundImage = `url(${img.src})`;
                    item.style.backgroundSize = 'cover';
                    item.style.backgroundPosition = 'center';
                }
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

            this.dom.appendChild(carouselEl);
        } else {
            const thumbsRow = document.createElement('div');
            thumbsRow.className = `sn-img-group-thumbs gup-${layout}`;

            images.forEach((img, i) => {
                const cell = document.createElement('div');
                cell.className = 'images-upload-item';
                cell.dataset.imgIdx = String(i);

                if (img.src.startsWith('blob:')) {
                    cell.classList.add('sn-img-uploading');
                    cell.dataset.uploadSrc = img.src;
                }

                // Blob URLs are already in memory — skip the blur-up background-image
                // placeholder (it relies on inline style url() which may be CSP-blocked
                // regardless of img-src, and the pulsing animation already signals uploading).
                if ((layout === 'grid' || layout === 'justified') && !img.src.startsWith('blob:')) {
                    cell.style.backgroundImage = `url(${img.src})`;
                    cell.style.backgroundSize = 'cover';
                    cell.style.backgroundPosition = 'center';
                }

                const imgEl = document.createElement('img');
                imgEl.src = img.src;
                imgEl.alt = img.alt;
                if ((layout === 'grid' || layout === 'justified') && !img.src.startsWith('blob:')) {
                    if (imgEl.complete) cell.classList.add('images-item-loaded');
                    else imgEl.addEventListener('load', () => cell.classList.add('images-item-loaded'), { once: true });
                } else if (img.src.startsWith('blob:')) {
                    // Blob images load instantly — mark as loaded immediately so the blur
                    // overlay (::before) is never applied on top of the in-memory preview.
                    cell.classList.add('images-item-loaded');
                }
                cell.appendChild(imgEl);

                // Progress overlay (visible only while blob URL is active)
                if (img.src.startsWith('blob:')) {
                    const bar = document.createElement('progress');
                    bar.className = 'images-upload-item-progress progress progress-primary';
                    bar.max = 100; bar.value = 0;
                    const lbl = document.createElement('div');
                    lbl.className = 'images-upload-item-label';
                    lbl.textContent = '0%';
                    cell.appendChild(bar);
                    cell.appendChild(lbl);
                }

                // Delete button
                const deleteBtn = document.createElement('button');
                deleteBtn.type = 'button';
                deleteBtn.className = 'images-upload-item-delete';
                deleteBtn.title = 'Remove image';
                deleteBtn.innerHTML = SVG_CLOSE;
                deleteBtn.addEventListener('mousedown', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    removeImageFromEditor(this.view, img.src);
                });
                cell.appendChild(deleteBtn);

                // Drag-to-reorder within the group
                cell.draggable = true;
                cell.addEventListener('dragstart', (e) => {
                    e.stopPropagation();
                    this.dragSrcIdx = i;
                    e.dataTransfer!.effectAllowed = 'move';
                    e.dataTransfer!.setData('application/x-snakk-group-reorder', String(i));
                });
                cell.addEventListener('dragend', () => {
                    this.dragSrcIdx = null;
                    cell.classList.remove('sn-drag-over');
                });
                cell.addEventListener('dragover', (e) => {
                    if (!e.dataTransfer?.types.includes('application/x-snakk-group-reorder')) return;
                    e.preventDefault();
                    e.dataTransfer.dropEffect = 'move';
                    cell.classList.add('sn-drag-over');
                });
                cell.addEventListener('dragleave', () => cell.classList.remove('sn-drag-over'));
                cell.addEventListener('drop', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    cell.classList.remove('sn-drag-over');
                    if (this.dragSrcIdx === null || this.dragSrcIdx === i) return;
                    const imgs = parseLines(this.node.textContent);
                    const [moved] = imgs.splice(this.dragSrcIdx, 1);
                    if (!moved) return;
                    imgs.splice(i, 0, moved);
                    const newText = imgs.map(m => `${m.src}|${m.alt}`).join('\n');
                    const pos = this.getPos();
                    if (pos === undefined) return;
                    const newNode = this.node.type.create(
                        this.node.attrs, [this.view.state.schema.text(newText)]
                    );
                    this.view.dispatch(this.view.state.tr.replaceWith(pos, pos + this.node.nodeSize, newNode));
                    this.dragSrcIdx = null;
                });

                thumbsRow.appendChild(cell);
            });

            this.dom.appendChild(thumbsRow);
        }
    }

    private openDropdown(): void {
        if (!this.dropdownEl) return;
        this.dropdownEl.hidden = false;

        const handler = (e: MouseEvent) => {
            if (!this.dom.contains(e.target as Node)) {
                this.closeDropdown();
            }
        };
        this.outsideClickHandler = handler;
        // Use capture so we get it before ProseMirror swallows it
        document.addEventListener('mousedown', handler, { capture: true });
    }

    private closeDropdown(): void {
        if (this.dropdownEl) this.dropdownEl.hidden = true;
        if (this.outsideClickHandler) {
            document.removeEventListener('mousedown', this.outsideClickHandler, { capture: true });
            this.outsideClickHandler = null;
        }
    }

    private updateLayout(layout: Layout): void {
        const pos = this.getPos();
        if (pos === undefined) return;
        const language: string = this.node.attrs.language ?? 'image-group';
        const tr = this.view.state.tr.setNodeMarkup(pos, undefined, {
            ...this.node.attrs,
            language: setLayout(language, layout),
        });
        this.view.dispatch(tr);
    }

    private pickAndUpload(): void {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/*';
        input.addEventListener('change', () => {
            const file = input.files?.[0];
            if (!file) { input.remove(); return; }

            const err = validateImageFile(file);
            if (err) { input.remove(); showImageError(err); return; }

            const blobUrl = URL.createObjectURL(file);

            // Insert blob URL immediately so the image appears before upload finishes
            const pos = this.getPos();
            if (pos !== undefined) {
                const images = parseLines(this.node.textContent);
                images.push({ src: blobUrl, alt: 'uploading…' });
                const newContent = images.map(im => `${im.src}|${im.alt}`).join('\n');
                const schema = this.view.state.schema;
                const newNode = schema.nodes.code_block!.create(this.node.attrs, [schema.text(newContent)]);
                this.view.dispatch(this.view.state.tr.replaceWith(pos, pos + this.node.nodeSize, newNode));
            }

            const formData = new FormData();
            formData.append('file', file, file.name);

            (window as any).SnakkUpload.uploadWithProgress({
                url: '/bff/media/upload',
                formData,
                onProgress: (pct: number) => updateUploadProgress(blobUrl, pct),
                onProcessing: () => updateUploadProgress(blobUrl, 'processing'),
            })
            .then((result: any) => {
                if (!result?.ok || !result?.data?.url) {
                    removeImageFromEditor(this.view, blobUrl);
                    return;
                }
                replaceImageSrc(this.view, blobUrl, result.data.url as string);
            })
            .catch(console.error)
            .finally(() => {
                URL.revokeObjectURL(blobUrl);
                input.remove();
            });
        });
        document.body.appendChild(input);
        input.click();
    }

    private _wireDropHandlers(): void {
        this.dom.addEventListener('dragover', (e) => {
            if (!e.dataTransfer?.types.includes('application/x-snakk-img')) return;
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
            this.dom.classList.add('sn-drop-target');
        });
        this.dom.addEventListener('dragleave', (e) => {
            if (!this.dom.contains(e.relatedTarget as Node)) {
                this.dom.classList.remove('sn-drop-target');
            }
        });
        this.dom.addEventListener('drop', (e) => {
            e.preventDefault();
            e.stopPropagation();
            this.dom.classList.remove('sn-drop-target');
            const payload = e.dataTransfer?.getData('application/x-snakk-img');
            if (!payload) return;
            const { src: droppedSrc, alt: droppedAlt } = JSON.parse(payload) as { src: string; alt: string };

            removeImageFromEditor(this.view, droppedSrc);

            const pos = this.getPos();
            if (pos === undefined) return;
            const state = this.view.state;
            const node = state.doc.nodeAt(pos);
            if (!node?.attrs.language?.startsWith('image-group')) return;

            const currentText = node.textContent;
            const newContent = currentText ? `${currentText}\n${droppedSrc}|${droppedAlt}` : `${droppedSrc}|${droppedAlt}`;
            const newNode = node.type.create(node.attrs, [state.schema.text(newContent)]);
            this.view.dispatch(state.tr.replaceWith(pos, pos + node.nodeSize, newNode));
        });
    }

    stopEvent(event: Event): boolean {
        const el = event.target as HTMLElement;
        // Stop all events originating inside our NodeView DOM
        return this.dom.contains(el);
    }

    ignoreMutation(): boolean {
        return true;
    }

    destroy(): void {
        this.closeDropdown();
    }
}

// ============================================================================
// DefaultCodeBlockNodeView — standard <pre><code> for all other code blocks
// ============================================================================

class DefaultCodeBlockNodeView implements NodeView {
    dom: HTMLElement;
    contentDOM: HTMLElement;

    constructor(node: PNode) {
        this.dom = document.createElement('pre');
        this.contentDOM = document.createElement('code');
        this.contentDOM.setAttribute('spellcheck', 'false');
        if (node.attrs.language) {
            this.contentDOM.className = `language-${node.attrs.language}`;
        }
        this.dom.appendChild(this.contentDOM);
    }

    update(node: PNode): boolean {
        if (node.type.name !== 'code_block') return false;
        if (node.attrs.language?.startsWith('image-group')) return false;
        this.contentDOM.className = node.attrs.language
            ? `language-${node.attrs.language}`
            : '';
        return true;
    }
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
        this._wireDropHandlers();
        this.dom.addEventListener('dragstart', (e) => {
            const src = this.node.attrs.src as string;
            const alt = (this.node.attrs.alt as string) ?? '';
            e.dataTransfer!.effectAllowed = 'move';
            e.dataTransfer!.setData('application/x-snakk-img', JSON.stringify({ src, alt }));
            // Prevent ProseMirror from registering this as a node drag and inserting a copy.
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

        if (src.startsWith('blob:')) {
            this.dom.classList.add('sn-img-uploading');
            this.dom.dataset.uploadSrc = src;
        } else {
            this.dom.classList.remove('sn-img-uploading');
            delete this.dom.dataset.uploadSrc;
        }

        const imgEl = document.createElement('img');
        imgEl.src = src;
        imgEl.alt = alt;
        imgEl.className = 'sn-single-img-thumb';
        this.dom.appendChild(imgEl);

        // Progress overlay (visible only while blob URL is active)
        if (src.startsWith('blob:')) {
            const bar = document.createElement('progress');
            bar.className = 'sn-upload-bar progress progress-primary';
            bar.max = 100; bar.value = 0;
            const pct = document.createElement('div');
            pct.className = 'sn-upload-pct';
            pct.textContent = '0%';
            this.dom.appendChild(bar);
            this.dom.appendChild(pct);
        }

        // Delete button (top-left, hover-revealed)
        const deleteBtn = document.createElement('button');
        deleteBtn.type = 'button';
        deleteBtn.className = 'sn-img-delete-btn';
        deleteBtn.title = 'Remove image';
        deleteBtn.innerHTML = SVG_CLOSE;
        deleteBtn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            e.stopPropagation();
            removeImageFromEditor(this.view, src);
        });
        this.dom.appendChild(deleteBtn);

        // Add-another button (bottom-right, hover-revealed)
        const addBtn = document.createElement('button');
        addBtn.type = 'button';
        addBtn.className = 'sn-single-add-btn';
        addBtn.title = 'Add another image';
        addBtn.innerHTML = SVG_PLUS;
        addBtn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            e.stopPropagation();
            this.pickAndUpload();
        });
        this.dom.appendChild(addBtn);
    }

    private pickAndUpload(): void {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/*';
        input.addEventListener('change', () => {
            const file = input.files?.[0];
            if (!file) { input.remove(); return; }

            const err = validateImageFile(file);
            if (err) { input.remove(); showImageError(err); return; }

            const blobUrl = URL.createObjectURL(file);

            // Build the group immediately with the blob URL so the preview appears at once
            const pos = this.getPos();
            if (pos === undefined) { URL.revokeObjectURL(blobUrl); input.remove(); return; }
            const state = this.view.state;
            const existingNode = state.doc.nodeAt(pos);
            if (!existingNode || existingNode.type.name !== 'image') {
                URL.revokeObjectURL(blobUrl);
                input.remove();
                return;
            }

            const existingSrc = existingNode.attrs.src as string;
            const existingAlt = (existingNode.attrs.alt as string) || '';
            const $pos = state.doc.resolve(pos);
            const paraStart = $pos.before($pos.depth);
            const paraEnd = $pos.after($pos.depth);

            const groupBlock = state.schema.nodes.code_block!.create(
                { language: 'image-group layout=grid' },
                [state.schema.text(`${existingSrc}|${existingAlt}\n${blobUrl}|uploading…`)]
            );
            const emptyPara = state.schema.nodes.paragraph!.create();
            this.view.dispatch(state.tr.replaceWith(paraStart, paraEnd, Fragment.from([groupBlock, emptyPara])));

            const formData = new FormData();
            formData.append('file', file, file.name);

            (window as any).SnakkUpload.uploadWithProgress({
                url: '/bff/media/upload',
                formData,
                onProgress: (pct: number) => updateUploadProgress(blobUrl, pct),
                onProcessing: () => updateUploadProgress(blobUrl, 'processing'),
            })
            .then((result: any) => {
                if (!result?.ok || !result?.data?.url) {
                    removeImageFromEditor(this.view, blobUrl);
                    return;
                }
                replaceImageSrc(this.view, blobUrl, result.data.url as string);
            })
            .catch(console.error)
            .finally(() => {
                URL.revokeObjectURL(blobUrl);
                input.remove();
            });
        });
        document.body.appendChild(input);
        input.click();
    }

    private _wireDropHandlers(): void {
        this.dom.addEventListener('dragover', (e) => {
            if (!e.dataTransfer?.types.includes('application/x-snakk-img')) return;
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
            this.dom.classList.add('sn-drop-target');
        });
        this.dom.addEventListener('dragleave', (e) => {
            if (!this.dom.contains(e.relatedTarget as Node)) {
                this.dom.classList.remove('sn-drop-target');
            }
        });
        this.dom.addEventListener('drop', (e) => {
            e.preventDefault();
            e.stopPropagation();
            this.dom.classList.remove('sn-drop-target');
            const payload = e.dataTransfer?.getData('application/x-snakk-img');
            if (!payload) return;
            const { src: droppedSrc, alt: droppedAlt } = JSON.parse(payload) as { src: string; alt: string };

            // Dropping an image onto itself — no-op
            if (droppedSrc === (this.node.attrs.src as string)) return;

            removeImageFromEditor(this.view, droppedSrc);

            const pos = this.getPos();
            if (pos === undefined) return;
            const state = this.view.state;
            const existingNode = state.doc.nodeAt(pos);
            if (!existingNode || existingNode.type.name !== 'image') return;

            const existingSrc = existingNode.attrs.src as string;
            const existingAlt = (existingNode.attrs.alt as string) || '';
            const $pos = state.doc.resolve(pos);
            const paraStart = $pos.before($pos.depth);
            const paraEnd = $pos.after($pos.depth);

            const groupBlock = state.schema.nodes.code_block!.create(
                { language: 'image-group layout=grid' },
                [state.schema.text(`${existingSrc}|${existingAlt}\n${droppedSrc}|${droppedAlt}`)]
            );
            const emptyPara = state.schema.nodes.paragraph!.create();
            this.view.dispatch(state.tr.replaceWith(paraStart, paraEnd, Fragment.from([groupBlock, emptyPara])));
        });
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
                    return new DefaultCodeBlockNodeView(node);
                },
                image: (node: PNode, view: EditorView, getPos: () => number | undefined) => {
                    return new SingleImageNodeView(node, view, getPos);
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

            // Pass 2: ensure a typeable paragraph follows every image-paragraph / image-group
            // at the end of the document so the cursor can be placed after the block.
            const schema = newState.schema;
            const docChildren: Array<{ node: PNode; pos: number }> = [];
            newState.doc.forEach((node, offset) => docChildren.push({ node, pos: offset }));

            for (let i = 0; i < docChildren.length; i++) {
                const entry = docChildren[i];
                if (!entry) continue;
                const { node, pos } = entry;
                const isImagePara = node.type.name === 'paragraph' &&
                    node.childCount === 1 && node.firstChild?.type.name === 'image';
                const isImageGroup = node.type.name === 'code_block' &&
                    node.attrs.language?.startsWith('image-group');
                if (!isImagePara && !isImageGroup) continue;

                // Only insert when this is the last child — no following sibling at all
                if (i < docChildren.length - 1) continue;

                ops.push({
                    from: pos + node.nodeSize,
                    to: pos + node.nodeSize,
                    nodes: [schema.nodes.paragraph!.create()],
                });
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
