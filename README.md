# LocalWiki

LocalWiki is a local, browser-based Markdown wiki for personal documentation. It gives ordinary `.md` files a visual editor, a nested page tree, search, backlinks, and a portable HTML copy. Your pages remain readable in a text editor or Git without LocalWiki.

## What it does

- Edit pages visually with Milkdown or edit the Markdown source directly.
- Create, rename, delete, and organize pages with drag and drop.
- Link pages with `[[Page title]]` or `[[Page title|link text]]`, and see backlinks.
- Search pages and navigate headings with the table of contents.
- Add images to the workspace's `Assets` folder.
- Choose an optional background image for each workspace; the default is plain.
- Detect external file changes and offer a choice when an open page has conflicting edits.
- Rebuild a read-only HTML site in `Published` after changes. That site can be copied or opened without running LocalWiki.

## How it works

LocalWiki runs an ASP.NET Core server on `127.0.0.1` and opens the interface in your default browser. It does not need IIS, Docker, a database, or an internet connection at runtime. The editor and browser dependencies are bundled with the app.

A workspace is an ordinary directory:

```text
MyDocs/
├── Pages/                 # Markdown source files
│   ├── index.md
│   └── spacecraft.md
├── Assets/                # Images used by pages
├── .wiki/navigation.json  # Page titles, aliases, order, and hierarchy
└── Published/             # Generated static HTML site
    └── index.html
```

Background images chosen in the app are copied to `.wiki/backgrounds` and the selection is saved in `.wiki/appearance.json`. No background image is bundled with LocalWiki.

Markdown in `Pages` is the source of truth for page content. The separate navigation file controls the sidebar, so moving a page in the tree does not move or rename its Markdown file. Renaming a page changes its display title and keeps the old title as a wiki-link alias. LocalWiki watches workspace files for edits made by other tools.

The generated `Published` site uses relative links and local assets. Open `Published/index.html` in a browser to read it without the editor or server. You can copy the entire `Published` directory to another location.

## Use it

1. Run `LocalWiki.exe`. On Windows, it opens your default browser automatically.
2. Choose a recent workspace or enter the path to a directory. A new directory gets a starter page; an existing workspace can be reopened. Markdown files already in its `Pages` folder are imported when there is no navigation file yet.
3. Select a page in the sidebar. Use **Visual** or **Markdown** to edit, then **Save page** or `Ctrl+S`.
4. Use **+** to create a page. Use a page's tree controls to rename or delete it, and drag pages to change their order or parent.
5. Type `[[Another page]]` to link to a page. Use the search field or `Ctrl+K` to find pages.
6. Open `Published/index.html` for the static version. Use **Rebuild Published Docs** if you want to rebuild it manually.

Recent workspace paths are saved in `%LOCALAPPDATA%\LocalWiki\settings.json`, outside the workspace. To open a specific workspace directly:

```powershell
& ".\LocalWiki.exe" --workspace "C:\path\to\MyDocs"
```

Other options are `--no-browser`, `--port <number>`, and `--publish-only`. The last option requires `--workspace` and rebuilds the static site without starting the editor:

```powershell
& ".\LocalWiki.exe" --workspace "C:\path\to\MyDocs" --publish-only
```

## Build from source

You need the .NET 10 SDK, Node.js, and npm to build. From the repository root in PowerShell:

```powershell
.\build.ps1       # Install frontend packages, bundle assets, build, and test
.\publish.ps1     # Do the above, then make a self-contained Windows x64 build
```

The distribution is written to `artifacts\publish\win-x64`. Copy **the whole folder** when moving the app to another Windows computer. That computer does not need Node.js or a separate .NET runtime. Build output is intentionally excluded from Git; the GitHub repository contains the source and build scripts.

LocalWiki uses .NET 10, Blazor Interactive Server, Markdig, Milkdown/Crepe, SortableJS, and local filesystem storage.

## License

LocalWiki is licensed under the [MIT License](LICENSE). Keep the copyright and license notice when redistributing the source or a published build.
