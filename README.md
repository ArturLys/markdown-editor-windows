# MarkdownView — fullscreen WYSIWYG Markdown editor for Windows

Double-click a `.md` file and it opens as a rendered page you can type into. No toolbar, no split pane, no raw `#` symbols. Dark theme, fullscreen, Ctrl+S to save. That's the whole app.

![MarkdownView editing a markdown file fullscreen on Windows, dark theme, no toolbar](docs/screenshot.png)

**[Download the latest release](https://github.com/ArturLys/markdown-editor-windows/releases/latest)** · Windows 10/11 · free · MIT

## Credit where it's due

This is [MarkdownView by DBMaster](https://apps.microsoft.com/detail/9n6pkz6fp1ml) from the Microsoft Store, with one change: **the text is editable.**

Their app is a beautiful fullscreen markdown *viewer*. I loved the look and hated that I had to open a second program to change a word. So I decompiled it to keep the exact theme, then rebuilt the inside as an editor. The dark palette, the type, the spacing, the layout: all theirs. Go install the original if you only need to read.

## What it does

- **Opens straight into editing.** No view mode, no edit mode. The document is the editor.
- **Markdown as you type.** `# ` becomes a heading, `- ` a list, `> ` a quote, `` ``` `` a code block, `[ ] ` a checkbox, `**bold**` bold. The syntax disappears and the formatting stays, the way Notion or Typora do it.
- **One shortcut.** Ctrl+S saves the file as plain markdown. Esc or the ✕ closes, saving first if anything changed.
- **Nothing else on screen.** Fullscreen, borderless, no status bar, no mode switch, no settings.
- **Real `.md` on disk.** It reads and writes ordinary markdown, so the file still works in Obsidian, GitHub, VS Code, wherever.

Good for: quick notes, READMEs, journals, todo lists, reading docs you occasionally need to fix a typo in. Not trying to be Obsidian.

## Install

1. Grab `MarkdownView-win-x64.zip` from [Releases](https://github.com/ArturLys/markdown-editor-windows/releases/latest) and unzip it anywhere.
2. Right-click any `.md` file → **Open with** → **Choose another app** → browse to `MarkdownView.exe` → tick **Always**.

That's it. There is no installer. The `-selfcontained` zip bundles the .NET runtime; the smaller one needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Both need the WebView2 runtime, which Windows 11 ships with.

## How it's built

| Layer | What |
|---|---|
| Shell | WPF on .NET 10, one borderless maximized window |
| Surface | WebView2 rendering a local page over a virtual host |
| Editor | [TipTap](https://tiptap.dev) (ProseMirror) with [tiptap-markdown](https://github.com/aguingand/tiptap-markdown), bundled offline by esbuild |

The C# side owns the file: it reads it on launch, gets the markdown back over the WebView message channel, and writes it to disk. The web page never touches the filesystem.

Bullets are drawn with a `::before` dot instead of a native list marker. Native `::marker` geometry can't be styled or measured, so task checkboxes couldn't be lined up with it. Drawing both by hand puts them on the same axis.

### Build from source

Needs the .NET 10 SDK and Node.

```bash
cd src/web-src && npm install && npm run build
cd .. && dotnet publish -c Release -o ../dist
```

First command bundles the editor into `src/web/editor.js`, second one produces `dist/MarkdownView.exe`.

## Why not just use…

- **Typora**: paid, and more than I needed.
- **Obsidian**: a vault, plugins, a sidebar. I wanted a file.
- **VS Code**: raw markdown with a preview pane. I wanted the page.
- **Notepad**: no.

## Setting it as the default .md app

Windows validates the default-app registry entry with a per-user hash, so the association can't be set by a script without reproducing that hash. `tools/userchoice_hash.py` is an unfinished port of it. Use the Open-with dialog instead; it takes ten seconds.

## License

MIT. The theme is DBMaster's, see [LICENSE](LICENSE). Their compiled app is not in this repo.
