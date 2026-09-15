import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import TaskList from '@tiptap/extension-task-list';
import TaskItem from '@tiptap/extension-task-item';
import Link from '@tiptap/extension-link';
import Placeholder from '@tiptap/extension-placeholder';
import Table from '@tiptap/extension-table';
import TableRow from '@tiptap/extension-table-row';
import TableCell from '@tiptap/extension-table-cell';
import TableHeader from '@tiptap/extension-table-header';
import { Markdown } from 'tiptap-markdown';

const host = window.chrome && window.chrome.webview;
let loading = false;

const editor = new Editor({
  element: document.getElementById('editor'),
  extensions: [
    StarterKit.configure({
      heading: { levels: [1, 2, 3, 4, 5, 6] },
      codeBlock: { HTMLAttributes: { spellcheck: 'false' } },
    }),
    TaskList,
    TaskItem.configure({ nested: true }),
    Link.configure({ openOnClick: false, autolink: true, linkOnPaste: true }),
    Placeholder.configure({ placeholder: '' }),
    Table.configure({ resizable: false }),
    TableRow,
    TableHeader,
    TableCell,
    Markdown.configure({
      html: true,
      tightLists: true,
      bulletListMarker: '-',
      linkify: false,
      breaks: false,
      transformPastedText: true,
      transformCopiedText: true,
    }),
  ],
  autofocus: 'start',
  editorProps: {
    attributes: { class: 'content', spellcheck: 'false' },
  },
  onUpdate: () => {
    if (!loading && host) host.postMessage({ type: 'dirty' });
  },
});

window.loadMarkdown = function (md) {
  loading = true;
  editor.commands.setContent(md, false);
  editor.commands.focus('start');
  loading = false;
};
window.getMarkdown = function () {
  return editor.storage.markdown.getMarkdown();
};

document.addEventListener('keydown', (e) => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
    e.preventDefault();
    if (host) host.postMessage({ type: 'save', md: window.getMarkdown() });
  } else if (e.key === 'Escape') {
    e.preventDefault();
    if (host) host.postMessage({ type: 'close', md: window.getMarkdown() });
  }
}, true);

// click anywhere below the text -> caret to end, like a real page
document.body.addEventListener('mousedown', (e) => {
  if (e.target === document.body || e.target.id === 'editor') {
    editor.commands.focus('end');
  }
});

if (host) host.postMessage({ type: 'ready' });
