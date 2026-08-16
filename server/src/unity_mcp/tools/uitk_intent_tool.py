"""uitk_intent — NL → DSL → UXML + USS generation pipeline."""

from mcp.server.fastmcp.exceptions import ToolError

from ..sampling import sampling_service as _sampling
from ._annotations import RW as _RW
from ._common import bind
from .intent_common import parse_indent_tree, parse_kv, sanitize_intent, strip_fences

_send = None

_TYPE_TO_ELEMENT: dict[str, str] = {
    "panel":     "ui:VisualElement",
    "label":     "ui:Label",
    "button":    "ui:Button",
    "toggle":    "ui:Toggle",
    "textfield": "ui:TextField",
    "scroll":    "ui:ScrollView",
    "list":      "ui:ListView",
    "image":     "ui:Image",
    "foldout":   "ui:Foldout",
    "spacer":    "ui:VisualElement",
}

_USS_BANNED = [
    "display: grid",
    "display: block",
    "display: inline",
    "@media",
    "calc(",
    "box-shadow",
    "linear-gradient",
    "radial-gradient",
    "conic-gradient",
    "animation:",
    "@keyframes",
    "::before",
    "::after",
    ":nth-child",
    ":not(",
    ":first-child",
    ":last-child",
    "z-index",
    "inherit",
    "data-theme",
]

_TEMPLATES: dict[str, str] = {
    "hud": """\
=TREE=
panel hud-root class="hud"
  label health text="100" class="hud__health"
  label score text="0" class="hud__score"

=STYLE=
.hud {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    padding: 16px;
    flex-direction: row;
    justify-content: space-between;
}
.hud__health {
    font-size: 20px;
    color: #e94560;
    -unity-font-style: bold;
}
.hud__score {
    font-size: 20px;
    color: var(--unity-colors-label-text);
}""",
    "menu": """\
=TREE=
panel menu-root class="menu"
  label menu-title text="Main Menu" class="menu__title"
  button play text="Play" class="menu__btn"
  button settings text="Settings" class="menu__btn"
  button quit text="Quit" class="menu__btn"

=STYLE=
.menu {
    align-self: center;
    flex-direction: column;
    align-items: center;
    padding: 40px;
    background-color: var(--unity-colors-window-background);
}
.menu__title {
    font-size: 32px;
    -unity-font-style: bold;
    margin-bottom: 24px;
}
.menu__btn {
    width: 200px;
    margin-bottom: 12px;
    padding: 12px;
}""",
    "dialog": """\
=TREE=
panel dialog-root class="dialog"
  label dialog-title text="Dialog" class="dialog__title"
  label dialog-msg text="Message here" class="dialog__message"
  button dialog-ok text="OK" class="dialog__btn"

=STYLE=
.dialog {
    align-self: center;
    background-color: var(--unity-colors-window-background);
    border-color: var(--unity-colors-default-border);
    border-width: 1px;
    padding: 24px;
    min-width: 320px;
}
.dialog__title {
    font-size: 20px;
    -unity-font-style: bold;
    margin-bottom: 12px;
}
.dialog__message {
    margin-bottom: 20px;
    white-space: normal;
}
.dialog__btn {
    align-self: flex-end;
    padding: 8px 24px;
}""",
    "settings": """\
=TREE=
panel settings-root class="settings-panel"
  label settings-title text="Settings" class="settings-panel__title"
  toggle sfx text="Sound Effects" class="settings-panel__row"
  toggle music text="Music" class="settings-panel__row"
  button save text="Save" class="settings-panel__save"

=STYLE=
.settings-panel {
    flex-grow: 1;
    padding: 20px;
    background-color: var(--unity-colors-window-background);
}
.settings-panel__title {
    font-size: 24px;
    -unity-font-style: bold;
    color: var(--unity-colors-label-text);
    margin-bottom: 16px;
}
.settings-panel__row {
    margin-bottom: 8px;
}
.settings-panel__save {
    margin-top: 16px;
    align-self: flex-end;
    padding: 8px 24px;
}""",
    "editor_window": """\
=TREE=
panel editor-root class="editor-panel"
  panel toolbar class="editor-panel__toolbar"
    button refresh text="Refresh" class="editor-panel__btn"
    button apply text="Apply" class="editor-panel__btn"
  scroll content-scroll class="editor-panel__scroll"
    panel content-area class="editor-panel__content"

=STYLE=
.editor-panel {
    flex-grow: 1;
    flex-direction: column;
}
.editor-panel__toolbar {
    flex-direction: row;
    padding: 4px;
    background-color: var(--unity-colors-window-background);
    border-color: var(--unity-colors-default-border);
    border-bottom-width: 1px;
}
.editor-panel__btn {
    margin-right: 4px;
}
.editor-panel__scroll {
    flex-grow: 1;
}
.editor-panel__content {
    padding: 8px;
}""",
}

_SYSTEM_PROMPT = """\
Generate a UI Toolkit DSL with two sections: =TREE= and =STYLE=.

=TREE= section — indent-based hierarchy (2 spaces per level):
  Format: <type> <name> [class="..."] [text="..."]
  Types: panel | label | button | toggle | textfield | scroll | list | image | foldout | spacer
  Rules:
  - name = unique identifier, no spaces, kebab-case (e.g. "play-btn", "title-label")
  - class = BEM names: block__element--modifier, double quotes if multiple classes
  - text = visible label text (buttons, labels, toggles, foldouts only)
  - NO style="" attributes on tree nodes

=STYLE= section — USS-compatible CSS rules:
  SUPPORTED: flex properties, box model (margin/padding/width/height/min-/max-),
             text (font-size, color, -unity-font-style: bold|italic,
             -unity-text-align: middle-center|upper-left, white-space),
             transforms (translate, rotate, scale), transitions (transition: prop dur ease),
             custom properties (var(--unity-colors-*)),
             border (border-color, border-width, border-radius),
             position (position: absolute, top/left/right/bottom),
             display: none, display: flex, opacity, visibility

  NOT SUPPORTED — DO NOT USE (triggers validation error and retry):
  - display: grid         → use flex-direction: row + flex-wrap: wrap instead
  - display: block        → use display: flex
  - display: inline       → use display: flex
  - @media                → not supported in USS
  - calc(                 → not supported
  - box-shadow            → not supported
  - linear-gradient       → not supported
  - radial-gradient       → not supported
  - conic-gradient        → not supported
  - animation:            → use transition: instead
  - @keyframes            → not supported
  - ::before / ::after    → pseudo-elements not supported
  - :nth-child / :not( / :first-child / :last-child → not supported
  - z-index               → not supported
  - inherit               → limited support, avoid
  - data-theme            → wrong Unity pattern, use double-class selectors

  BEM naming: block__element--modifier (e.g. .hud__health--low)
  Theme colors (use var() for system UI, not hardcoded hex):
  - var(--unity-colors-window-background) → panel/card backgrounds
  - var(--unity-colors-label-text)        → body text
  - var(--unity-colors-button-background) → standard button bg
  - var(--unity-colors-default-border)    → borders, dividers

File name: {name}
Output path: {path}
Intent: {intent}

Output ONLY the =TREE= and =STYLE= sections. No explanation, no markdown fences."""


def get_template_dsl(name: str) -> str | None:
    return _TEMPLATES.get(name)


def _split_sections(dsl: str) -> tuple[str, str]:
    if "=TREE=" not in dsl:
        raise ToolError("Missing =TREE= section in DSL")
    parts = dsl.split("=TREE=", 1)[1]
    if "=STYLE=" in parts:
        tree_text, style_text = parts.split("=STYLE=", 1)
    else:
        tree_text, style_text = parts, ""
    return tree_text.strip(), style_text.strip()


def parse_uitk_dsl(dsl: str) -> dict:
    tree_text, style_text = _split_sections(dsl)
    nodes = parse_indent_tree(tree_text)
    return {"tree": nodes, "style": style_text}


def validate_uitk_dsl(data: dict) -> str | None:
    style = data.get("style", "")
    for banned in _USS_BANNED:
        if banned in style:
            return f"Unsupported USS feature: '{banned}'. See USS constraints."
    return None


def _parse_node_line(line: str) -> dict:
    parts = line.split(None, 1)
    elem_type = parts[0].lower()
    rest = parts[1] if len(parts) > 1 else ""
    rest_parts = rest.split(None, 1) if rest else []
    if rest_parts and "=" not in rest_parts[0]:
        node_name = rest_parts[0]
        attr_str = rest_parts[1] if len(rest_parts) > 1 else ""
    else:
        node_name = elem_type
        attr_str = rest
    attrs = parse_kv(attr_str) if attr_str else {}
    return {"type": elem_type, "name": node_name, "attrs": attrs}


def build_uitk_uxml(name: str, nodes: list[dict], uss_rel_path: str) -> str:
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<ui:UXML xmlns:ui="UnityEngine.UIElements">',
        f'    <Style src="{uss_rel_path}" />',
    ]
    stack: list[tuple[int, str]] = []  # (depth, tag) for open elements
    for i, node in enumerate(nodes):
        info = _parse_node_line(node["line"])
        depth = node["depth"]
        while stack and stack[-1][0] >= depth:
            d, closed_tag = stack.pop()
            lines.append(f"{'    ' * (d + 1)}</{closed_tag}>")
        tag = _TYPE_TO_ELEMENT.get(info["type"])
        if tag is None:
            raise ValueError(f"Unknown element type: '{info['type']}'")
        indent = "    " * (depth + 1)
        attrs = f' name="{info["name"]}"'
        if info["attrs"].get("text"):
            attrs += f' text="{info["attrs"]["text"]}"'
        if info["attrs"].get("class"):
            attrs += f' class="{info["attrs"]["class"]}"'
        has_children = i + 1 < len(nodes) and nodes[i + 1]["depth"] > depth
        if has_children:
            lines.append(f"{indent}<{tag}{attrs}>")
            stack.append((depth, tag))
        else:
            lines.append(f"{indent}<{tag}{attrs} />")
    while stack:
        d, closed_tag = stack.pop()
        lines.append(f"{'    ' * (d + 1)}</{closed_tag}>")
    lines.append("</ui:UXML>")
    return "\n".join(lines)


def _make_prompt(intent: str, name: str, path: str, error: str | None = None) -> str:
    base = _SYSTEM_PROMPT.format(
        name=sanitize_intent(name),
        path=sanitize_intent(path),
        intent=sanitize_intent(intent),
    )
    if error:
        return base + f"\n\nPREVIOUS ATTEMPT FAILED: {error}\nFix the invalid USS and try again."
    return base


def _result_error(result: str | None) -> str | None:
    """Return an explicit tool error line from an otherwise successful bridge response."""
    for line in (result or "").splitlines():
        stripped = line.strip()
        if stripped.lower().startswith(("err:", "error:")):
            return stripped
    return None


def _confirms_auto_revert(error: str) -> bool:
    """Return True only for UIFileHelper's explicit cleanup acknowledgement."""
    return "— auto-reverted." in error.lower()


def _intent_failure(
    step: str,
    error: str,
    completed: list[str],
    total_ops: int,
    processed_files: list[str],
    attempted_file: str | None = None,
    attempted_file_auto_reverted: bool = False,
) -> str:
    lines = [
        f"uitk_intent failed at {step}: {len(completed)}/{total_ops} ops completed",
        error,
    ]
    if processed_files:
        lines.append("Files already created or modified (not rolled back):")
        lines.extend(f"- {file_path}" for file_path in processed_files)

    if attempted_file and attempted_file not in processed_files:
        if attempted_file_auto_reverted:
            lines.append("Attempted file was auto-reverted by Unity:")
        else:
            lines.append(
                "Attempted file may have been created or modified "
                "(cleanup unconfirmed):"
            )
        lines.append(f"- {attempted_file}")

    if not processed_files and not attempted_file:
        lines.append("No file operations were attempted.")
    return "\n".join(lines)


async def uitk_intent(
    intent: str,
    name: str,
    path: str = "Assets/UI",
    attach_to: str | None = None,
    template: str | None = None,
    dry_run: bool = False,
) -> str:
    """Generate a UXML + USS file pair from a natural-language UI description.
    Side effect: unless dry_run=True, writes both project assets; attach_to also adds a
    UIDocument scene component. Completed steps are not rolled back if a later
    step fails. Failure output distinguishes retained files, confirmed Unity
    auto-reverts, and attempted files whose cleanup is uncertain. Without
    template, invokes
    configured Claude sampling and may
    consume provider quota. For uGUI use ui_intent.
    template: hud|menu|dialog|settings|editor_window bypasses Haiku entirely.
    name: base filename (e.g. "InventoryPanel" → InventoryPanel.uxml + .uss).
    path: output folder, default "Assets/UI".
    attach_to: scene path to UIDocument GameObject to wire after creation.
    dry_run: return UXML+USS text without writing files.
    """
    if template:
        dsl = get_template_dsl(template)
        if dsl is None:
            raise ToolError(f"Unknown template '{template}'. Valid: {', '.join(_TEMPLATES)}")
        data = parse_uitk_dsl(dsl)
    else:
        raw = await _sampling.generate(_make_prompt(intent, name, path), feature="uitk_intent")
        if not raw:
            raise ToolError("Haiku unavailable (set UNITY_MCP_VISUAL_VERIFY=1)")
        data = parse_uitk_dsl(strip_fences(raw))
        err = validate_uitk_dsl(data)
        if err:
            raw2 = await _sampling.generate(
                _make_prompt(intent, name, path, error=err), feature="uitk_intent"
            )
            if not raw2:
                raise ToolError("Haiku unavailable on retry")
            data = parse_uitk_dsl(strip_fences(raw2))
            err2 = validate_uitk_dsl(data)
            if err2:
                raise ToolError(f"DSL validation failed after retry: {err2}")

    if not data["tree"]:
        raise ToolError("DSL produced no nodes")

    uss_rel = f"{name}.uss"
    uxml_path = f"{path}/{name}.uxml"
    uss_path = f"{path}/{name}.uss"
    uxml_str = build_uitk_uxml(name, data["tree"], uss_rel)
    uss_str = data["style"]

    if dry_run:
        return f"{uxml_str}\n---\n{uss_str}"

    total_ops = 3 if attach_to else 2
    completed: list[str] = []
    processed_files: list[str] = []

    async def run_step(command: str, args: dict, label: str, file_path: str | None = None) -> None:
        try:
            result = await _send(command, args)
        except Exception as exc:
            raise ToolError(_intent_failure(
                label,
                f"{type(exc).__name__}: {exc}",
                completed,
                total_ops,
                processed_files,
                file_path,
            )) from None
        error = _result_error(result)
        if error:
            raise ToolError(_intent_failure(
                label,
                error,
                completed,
                total_ops,
                processed_files,
                file_path,
                attempted_file_auto_reverted=_confirms_auto_revert(error),
            ))
        completed.append(label)
        if file_path:
            processed_files.append(file_path)

    # R04: USS must be written before UXML (so <Style src> import works)
    await run_step(
        "uitk_file",
        {"action": "create_uss", "path": uss_path, "content": uss_str},
        "create_uss",
        uss_path,
    )
    await run_step(
        "uitk_file",
        {"action": "create_uxml", "path": uxml_path, "content": uxml_str},
        "create_uxml",
        uxml_path,
    )

    if attach_to:
        await run_step(
            "attach_uitk",
            {"path": attach_to, "uxml": uxml_path},
            "attach_uitk",
        )

    lines = [
        f"uitk_intent: {len(completed)} ops completed",
        f"Processed {uss_path}",
        f"Processed {uxml_path}",
    ]
    if attach_to:
        lines.append(f"Attached {uxml_path} to {attach_to}")
    return "\n".join(lines)


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(uitk_intent)
