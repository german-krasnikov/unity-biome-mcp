"""MkDocs hook: transforms standard markdown into MkDocs Material-compatible format.

Agents write GitHub-friendly markdown; this hook fixes paths and attributes at build time.
"""

import re
from pathlib import Path


_REDIRECT_TEMPLATE = (
    '<meta http-equiv="refresh" content="0; url={url}">'
    '<p>Redirecting to <a href="{url}">{url}</a>...</p>'
)


def on_page_markdown(markdown, page, config, files, **kwargs):
    redirect_to = getattr(page, "meta", {}).get("redirect_to")
    if redirect_to:
        return _REDIRECT_TEMPLATE.format(url=redirect_to)
    markdown = _fix_image_paths(markdown, page, config)
    markdown = _fix_html_img_paths(markdown, page, config)
    markdown = _add_markdown_attr(markdown)
    return markdown


def _page_depth(page, config):
    if not config.get("use_directory_urls", True):
        return 0
    return len(Path(page.file.dest_path).parent.parts)


def _fix_image_paths(md, page, config):
    """![alt](assets/x.svg) → ![alt](../assets/x.svg) based on page depth."""
    depth = _page_depth(page, config)
    if depth == 0:
        return md
    prefix = "../" * depth

    def _fix(m):
        alt, path = m.group(1), m.group(2)
        if path.startswith(("http", "/", "#", "..", "data:")):
            return m.group(0)
        return f"![{alt}]({prefix}{path})"

    parts = re.split(r"(```[\s\S]*?```|`[^`]+`)", md)
    for i in range(0, len(parts), 2):
        parts[i] = re.sub(r"!\[([^\]]*)\]\(([^)]+)\)", _fix, parts[i])
    return "".join(parts)


def _fix_html_img_paths(md, page, config):
    """<img src="assets/x.svg"> → <img src="../assets/x.svg"> based on page depth."""
    depth = _page_depth(page, config)
    if depth == 0:
        return md
    prefix = "../" * depth

    def _fix(m):
        before, path, after = m.group(1), m.group(2), m.group(3)
        if path.startswith(("http", "/", "#", "..", "data:")):
            return m.group(0)
        return f'{before}{prefix}{path}{after}'

    parts = re.split(r"(```[\s\S]*?```|`[^`]+`)", md)
    for i in range(0, len(parts), 2):
        parts[i] = re.sub(r'(<img\s[^>]*?src=["\'])([^"\']+)(["\'])', _fix, parts[i])
    return "".join(parts)


def _add_markdown_attr(md):
    """<details> → <details markdown>, <div class="x"> → <div class="x" markdown>."""
    md = re.sub(
        r"<(details|summary)(?![^>]*\bmarkdown\b)([^>]*)>",
        r"<\1\2 markdown>",
        md,
    )
    md = re.sub(
        r'<div(?![^>]*\bmarkdown\b)(\s+class="[^"]*")>',
        r"<div\1 markdown>",
        md,
    )
    return md
