export function jumpToHeading(mode, index, id, line) {
    if (mode === "source") {
        const editor = document.querySelector(".source-sheet .source-editor");
        if (!editor) return;

        const lines = editor.value.split(/\r\n|\r|\n/);
        const offset = lines.slice(0, line).reduce((sum, text) => sum + text.length + 1, 0);
        editor.focus();
        editor.setSelectionRange(offset, offset);
        const lineHeight = parseFloat(getComputedStyle(editor).lineHeight) || 22;
        editor.scrollTop = Math.max(0, line * lineHeight - editor.clientHeight * 0.2);
        editor.scrollIntoView({ behavior: "smooth", block: "start" });
        return;
    }

    const target = mode === "view"
        ? document.getElementById(id)
        : document.querySelectorAll(".visual-editor .ProseMirror h1, .visual-editor .ProseMirror h2, .visual-editor .ProseMirror h3, .visual-editor .ProseMirror h4, .visual-editor .ProseMirror h5, .visual-editor .ProseMirror h6")[index];
    target?.scrollIntoView({ behavior: "smooth", block: "start" });
}
