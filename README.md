# MarkdownView

A fullscreen markdown editor for Windows that opens a `.md` file straight into a rendered, editable page. No toolbar, no status bar, no mode switch. Type `# ` and it becomes a heading while you write.

It started as a patch of a read-only markdown viewer from the Microsoft Store, which had no way to edit the file you were looking at. The viewer was decompiled to recover its look, then rebuilt from scratch as an editor that keeps the same dark theme.

## What it does

- Opens straight into edit mode. The file is live text, not a preview you have to switch away from.
- Markdown input rules as you type: `# `, `## `, `- `, `1. `, `> `, `` ``` ``, `[ ] `, `**bold**`, `*italic*`, `` `code` ``.
- **Ctrl+S** saves. That is the only shortcut worth remembering.
- **Esc**, **Alt+F4**, or the ✕ in the corner closes, saving first if anything changed.
- Fullscreen and chromeless. Nothing on screen but the document.

## How it is built

| Layer | What |
|---|---|
| Shell | WPF, .NET 10, a borderless maximized window |
| Surface | WebView2 hosting a local page over a virtual host mapping |
| Editor | TipTap (ProseMirror) with markdown serialization, bundled offline by esbuild |

The C# side owns the file: it reads on load, receives the markdown back over the WebView message channel, and writes to disk. The page never touches the filesystem.

Bullets are drawn with a `::before` dot rather than a native list marker. Native `::marker` geometry cannot be addressed from CSS or measured from script, so task checkboxes could not be aligned to it; drawing both markers by hand puts them on one axis.

## Build

Needs the .NET 10 SDK and Node.

```bash
cd src/web-src && npm install && npm run build
cd .. && dotnet publish -c Release -o ../dist
```

The first command bundles the editor into `src/web/editor.js`. The second produces `dist/MarkdownView.exe`.

## Use it for .md files

Run the exe with a path, or right-click a `.md` file → Open with → Choose another app → browse to `dist/MarkdownView.exe` → tick Always.

Windows validates the default-app registry entry with a per-user hash, so the association cannot be scripted without reproducing that hash. `tools/userchoice_hash.py` is an unfinished port of it, kept only as a starting point.

## Note on the original

The dark stylesheet comes from the Store viewer this replaced, recovered by decompiling it. The app's own binaries are not in this repository. Everything else here is a rewrite.
