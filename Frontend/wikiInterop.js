import Sortable from 'sortablejs';
import { Crepe } from '@milkdown/crepe';
import '@milkdown/crepe/theme/common/style.css';
import '@milkdown/crepe/theme/frame.css';

let editor = null;
let bridge = null;
let sortables = [];
let editorRoot = null;

export function setup(dotNetReference) {
  bridge = dotNetReference;
  if (window.localWikiKeysInstalled) return;
  window.localWikiKeysInstalled = true;
  document.addEventListener('keydown', event => {
    const key = event.key.toLowerCase();
    if ((event.ctrlKey || event.metaKey) && (key === 's' || key === 'k')) {
      event.preventDefault();
      bridge?.invokeMethodAsync('OnShortcut', key === 's' ? 'save' : 'search');
    } else if (key === 'escape') bridge?.invokeMethodAsync('OnShortcut', 'escape');
  });
}

export function attachTree(dotNetReference) {
  // Sortable owns only the transient drag gesture. Blazor/C# owns the tree and persists moves.
  sortables.forEach(item => item.destroy());
  sortables = [];
  document.querySelectorAll('#wiki-tree .sortable-list').forEach(list => {
    sortables.push(new Sortable(list, {
      group: 'wiki-pages', animation: 130, draggable: '.tree-item', handle: '.tree-row', filter: 'button',
      forceFallback: true, fallbackOnBody: true, swapThreshold: 0.65,
      onStart() { document.body.classList.add('tree-dragging'); },
      onEnd: async event => {
        document.body.classList.remove('tree-dragging');
        const id = event.item?.dataset.id;
        const parent = event.to?.dataset.parent || null;
        if (!id) return;
        try { await dotNetReference.invokeMethodAsync('OnMove', id, parent, event.newIndex ?? 0); }
        catch (error) { console.error('Navigation move failed', error); window.location.reload(); }
      }
    }));
  });
}

function liveImageUrl(url) {
  return url.startsWith('../Assets/') ? '/workspace-assets/' + encodeURIComponent(url.slice(10)) : url;
}
async function upload(file) {
  const base64 = await new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result).split(',')[1]);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
  return await bridge.invokeMethodAsync('UploadImage', file.name, base64);
}

export async function createEditor(element, markdown, dotNetReference) {
  await destroyEditor();
  bridge = dotNetReference;
  editorRoot = element;
  editor = new Crepe({
    root: element,
    defaultValue: markdown,
    featureConfigs: {
      [Crepe.Feature.ImageBlock]: {
        onUpload: upload, inlineOnUpload: upload, blockOnUpload: upload,
        proxyDomURL: liveImageUrl
      }
    }
  });
  editor.on(listener => listener.markdownUpdated((_ctx, value, previous) => {
    if (value !== previous) bridge?.invokeMethodAsync('OnEditorChanged', value);
  }));
  await editor.create();
}
export async function setMarkdown(markdown) {
  if (editorRoot) await createEditor(editorRoot, markdown, bridge);
}
export function getMarkdown() { return editor?.getMarkdown() ?? ''; }
export function focusEditor() { editorRoot?.querySelector('[contenteditable="true"]')?.focus(); }
export async function destroyEditor() { if (editor) { await editor.destroy(); editor = null; } }
export function focusSearch() { document.getElementById('wiki-search')?.focus(); }
