"""Source-backed contracts for the public Markdown documentation."""

from __future__ import annotations

import ast
import re
import unicodedata
from dataclasses import dataclass
from typing import TYPE_CHECKING
from urllib.parse import unquote

if TYPE_CHECKING:
    from pathlib import Path

LINK_RE = re.compile(r"(?<!!)\[[^\]]*\]\(([^)]+)\)")
HEADING_RE = re.compile(r"^(#{1,6})\s+(.+?)\s*$")
EXPLICIT_ID_RE = re.compile(r"\s*\{#([A-Za-z][\w:.-]*)[^}]*\}\s*$")
HTML_ID_RE = re.compile(r"<(?:a|span)\s+[^>]*\bid=[\"']([^\"']+)[\"']", re.IGNORECASE)
PYTHON_FENCE_RE = re.compile(r"```(?:python|py)\s*\n(.*?)```", re.DOTALL | re.IGNORECASE)
FENCE_RE = re.compile(r"^\s*(```+|~~~+)")

LEGACY_PUBLIC_PATHS = (
    "docs/plugins/ui-toolkit-best-practices.md",
    "docs/unity-assistant-2.17-analysis.md",
)
LEGACY_PUBLIC_ANCHORS = {
    "docs/tools/batch.md": {"batch-behavior"},
    "docs/features/intent-tools.md": {"common-workflow"},
    "docs/features/prefab-edit.md": {
        "when-to-use-prefab-edit-vs-set_property",
        "verification",
    },
}


@dataclass(frozen=True)
class ToolSignature:
    positional: tuple[str, ...]
    required: frozenset[str]
    keywords: frozenset[str]
    has_varargs: bool
    has_varkw: bool


def _public_markdown(repo_root: Path) -> list[Path]:
    return sorted((repo_root / "docs").rglob("*.md"))


def _delivered_markdown(repo_root: Path) -> list[Path]:
    """Markdown shipped by the repository or Unity package.

    Ignored contributor-local files (`.claude/`, `Plans/`) are intentionally
    excluded because they are not present in a clean clone or release archive.
    """
    paths = {
        *repo_root.glob("*.md"),
        *(repo_root / ".github").rglob("*.md"),
        *(repo_root / "AI").rglob("*.md"),
        *(repo_root / "docs").rglob("*.md"),
        *(repo_root / "unity-plugin").rglob("*.md"),
        *(repo_root / "unity-test-project" / "Assets").rglob("*.md"),
    }
    return sorted(path for path in paths if path.is_file())


def _without_fenced_code(text: str) -> str:
    lines: list[str] = []
    closing = ""
    for line in text.splitlines():
        fence = FENCE_RE.match(line)
        if fence:
            marker = fence.group(1)
            if not closing:
                closing = marker[0] * len(marker)
            elif marker.startswith(closing):
                closing = ""
            lines.append("")
        elif closing:
            lines.append("")
        else:
            lines.append(line)
    return "\n".join(lines)


def _without_link_like_code_examples(text: str) -> str:
    """Remove code before scanning Markdown links.

    Inline examples such as ``[text](url)`` describe syntax and must not be
    interpreted as repository links. Repeating the simple substitution also
    handles examples wrapped in double backticks.
    """
    text = _without_fenced_code(text)
    while True:
        stripped = re.sub(r"`[^`\n]*`", "", text)
        if stripped == text:
            return text
        text = stripped


def _slugify(value: str) -> str:
    value = EXPLICIT_ID_RE.sub("", value)
    value = re.sub(r"<[^>]+>", "", value)
    value = value.replace("`", "")
    value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    value = re.sub(r"[^\w\s-]", "", value).strip().lower()
    return re.sub(r"[-\s]+", "-", value)


def _anchors(path: Path) -> set[str]:
    text = _without_fenced_code(path.read_text(encoding="utf-8"))
    anchors = set(HTML_ID_RE.findall(text))
    occurrences: dict[str, int] = {}
    for line in text.splitlines():
        heading = HEADING_RE.match(line)
        if not heading:
            continue
        title = heading.group(2)
        explicit = EXPLICIT_ID_RE.search(title)
        if explicit:
            anchors.add(explicit.group(1))
            continue
        base = _slugify(title)
        if not base:
            continue
        number = occurrences.get(base, 0)
        occurrences[base] = number + 1
        anchors.add(base if number == 0 else f"{base}_{number}")
    return anchors


def _link_destination(raw: str) -> str:
    raw = raw.strip()
    if raw.startswith("<") and ">" in raw:
        return raw[1:raw.index(">")]
    # Markdown permits an optional quoted title after the destination.
    return raw.split(maxsplit=1)[0]


def _resolve_target(source: Path, destination: str, docs_root: Path) -> tuple[Path, str]:
    path_text, _, fragment = destination.partition("#")
    path_text = unquote(path_text).replace(r"\(", "(").replace(r"\)", ")")
    if not path_text:
        return source, unquote(fragment)
    target = docs_root / path_text.lstrip("/") if path_text.startswith("/") else source.parent / path_text
    if target.is_dir():
        target = target / "index.md"
    elif not target.suffix and not target.exists():
        markdown_target = target.with_suffix(".md")
        index_target = target / "index.md"
        if markdown_target.exists():
            target = markdown_target
        elif index_target.exists():
            target = index_target
    return target.resolve(), unquote(fragment)


def _tool_names(repo_root: Path) -> set[str]:
    source = repo_root / "server" / "src" / "unity_mcp" / "tools" / "tool_specs.py"
    tree = ast.parse(source.read_text(encoding="utf-8"))
    for node in tree.body:
        if not (
            isinstance(node, ast.AnnAssign)
            and isinstance(node.target, ast.Name)
            and node.target.id == "_SPECS"
            and isinstance(node.value, ast.Dict)
        ):
            continue
        names: set[str] = set()
        for key, value in zip(node.value.keys, node.value.values, strict=False):
            if not isinstance(key, ast.Constant) or not isinstance(key.value, str):
                continue
            internal = False
            if isinstance(value, ast.Call):
                internal = any(
                    keyword.arg == "category"
                    and isinstance(keyword.value, ast.Constant)
                    and keyword.value.value == "_INTERNAL"
                    for keyword in value.keywords
                )
            if not internal:
                names.add(key.value)
        return names
    raise AssertionError("_SPECS dictionary not found")


def _tool_signatures(repo_root: Path) -> dict[str, ToolSignature]:
    names = _tool_names(repo_root)
    signatures: dict[str, ToolSignature] = {}
    tools_root = repo_root / "server" / "src" / "unity_mcp" / "tools"
    source_paths = [*tools_root.glob("*.py"), repo_root / "server" / "src" / "unity_mcp" / "debug" / "snapshots.py"]
    for path in source_paths:
        tree = ast.parse(path.read_text(encoding="utf-8"))
        for node in tree.body:
            if not isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)) or node.name not in names:
                continue
            positional_nodes = [*node.args.posonlyargs, *node.args.args]
            positional = tuple(arg.arg for arg in positional_nodes)
            defaulted_positional = set(positional[len(positional) - len(node.args.defaults):])
            required = set(positional) - defaulted_positional
            for arg, default in zip(node.args.kwonlyargs, node.args.kw_defaults, strict=False):
                if default is None:
                    required.add(arg.arg)
            keywords = {*positional, *(arg.arg for arg in node.args.kwonlyargs)}
            signatures[node.name] = ToolSignature(
                positional=positional,
                required=frozenset(required),
                keywords=frozenset(keywords),
                has_varargs=node.args.vararg is not None,
                has_varkw=node.args.kwarg is not None,
            )
    assert signatures.keys() == names, f"Missing source signatures: {sorted(names - signatures.keys())}"
    return signatures


def test_public_markdown_relative_links_and_anchors(repo_root: Path) -> None:
    docs_root = repo_root / "docs"
    errors: list[str] = []
    anchor_cache: dict[Path, set[str]] = {}

    for source in _public_markdown(repo_root):
        text = _without_link_like_code_examples(source.read_text(encoding="utf-8"))
        for raw in LINK_RE.findall(text):
            destination = _link_destination(raw)
            if not destination or destination.startswith(("http://", "https://", "mailto:", "data:")):
                continue
            target, fragment = _resolve_target(source, destination, docs_root)
            location = source.relative_to(repo_root)
            if not target.exists():
                errors.append(f"{location}: missing target {destination}")
                continue
            if fragment and target.suffix.lower() == ".md":
                anchors = anchor_cache.setdefault(target, _anchors(target))
                if fragment not in anchors:
                    errors.append(f"{location}: missing anchor {destination}")

    assert not errors, "Broken public Markdown links:\n" + "\n".join(errors)


def test_legacy_public_urls_and_anchors_are_preserved(repo_root: Path) -> None:
    """Keep intentionally relocated pages and renamed sections link-compatible."""
    missing_paths = [path for path in LEGACY_PUBLIC_PATHS if not (repo_root / path).is_file()]
    missing_anchors: list[str] = []
    for path, expected in LEGACY_PUBLIC_ANCHORS.items():
        present = _anchors(repo_root / path)
        missing_anchors.extend(
            f"{path}#{anchor}" for anchor in sorted(expected - present)
        )

    assert not missing_paths, f"Missing compatibility pages: {missing_paths}"
    assert not missing_anchors, f"Missing compatibility anchors: {missing_anchors}"


def test_all_delivered_markdown_relative_links_and_anchors(repo_root: Path) -> None:
    docs_root = (repo_root / "docs").resolve()
    errors: list[str] = []
    anchor_cache: dict[Path, set[str]] = {}

    for source in _delivered_markdown(repo_root):
        text = _without_link_like_code_examples(source.read_text(encoding="utf-8"))
        for raw in LINK_RE.findall(text):
            destination = _link_destination(raw)
            if not destination or destination.startswith(
                ("http://", "https://", "mailto:", "data:")
            ):
                continue
            absolute_root = docs_root if source.resolve().is_relative_to(docs_root) else repo_root
            target, fragment = _resolve_target(source, destination, absolute_root)
            location = source.relative_to(repo_root)
            if not target.exists():
                errors.append(f"{location}: missing target {destination}")
                continue
            if fragment and target.suffix.lower() == ".md":
                anchors = anchor_cache.setdefault(target, _anchors(target))
                if fragment not in anchors:
                    errors.append(f"{location}: missing anchor {destination}")

    assert not errors, "Broken delivered Markdown links:\n" + "\n".join(errors)


def test_python_tool_examples_match_public_signatures(repo_root: Path) -> None:
    signatures = _tool_signatures(repo_root)
    errors: list[str] = []

    for path in _public_markdown(repo_root):
        text = path.read_text(encoding="utf-8")
        for block_number, block in enumerate(PYTHON_FENCE_RE.findall(text), start=1):
            if "# INVALID:" in block:
                continue
            try:
                tree = ast.parse(block)
            except SyntaxError as exc:
                errors.append(f"{path.relative_to(repo_root)} block {block_number}: invalid Python ({exc.msg})")
                continue
            for call in (node for node in ast.walk(tree) if isinstance(node, ast.Call)):
                if not isinstance(call.func, ast.Name) or call.func.id not in signatures:
                    continue
                name = call.func.id
                signature = signatures[name]
                keyword_names = {keyword.arg for keyword in call.keywords if keyword.arg is not None}
                unknown = keyword_names - signature.keywords
                if unknown and not signature.has_varkw:
                    errors.append(
                        f"{path.relative_to(repo_root)} block {block_number}: "
                        f"{name} has unknown arguments {sorted(unknown)}"
                    )
                if len(call.args) > len(signature.positional) and not signature.has_varargs:
                    errors.append(
                        f"{path.relative_to(repo_root)} block {block_number}: "
                        f"{name} has too many positional arguments"
                    )
                supplied = set(signature.positional[:len(call.args)]) | keyword_names
                missing = signature.required - supplied
                if missing:
                    errors.append(
                        f"{path.relative_to(repo_root)} block {block_number}: "
                        f"{name} is missing required arguments {sorted(missing)}"
                    )

    assert not errors, "Invalid public tool examples:\n" + "\n".join(errors)


def test_every_public_tool_is_discoverable_in_authored_docs(repo_root: Path) -> None:
    names = _tool_names(repo_root)
    authored = "\n".join(
        path.read_text(encoding="utf-8")
        for path in _public_markdown(repo_root)
        if path != repo_root / "docs" / "tools-schema" / "index.md"
    )
    missing = {
        name for name in names
        if not re.search(rf"(?<![A-Za-z0-9_]){re.escape(name)}(?![A-Za-z0-9_])", authored)
    }
    assert not missing, f"Public tools absent from authored documentation: {sorted(missing)}"
