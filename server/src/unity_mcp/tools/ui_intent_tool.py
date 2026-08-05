"""ui_intent — NL → UI DSL → create_ui/set_rect batch commands."""

from mcp.server.fastmcp.exceptions import ToolError

from ..sampling import sampling_service as _sampling
from ._annotations import RW as _RW
from ._common import bind
from .intent_common import build_batch_line, parse_indent_tree, parse_kv, run_intent_pipeline

_send = None

_TEMPLATES = {
    "hud": """\
canvas Canvas
  panel HUD anchor=stretch
    image HealthBar anchor=top-left pos=20,-20 size=200,30 color=#c33
    text Score anchor=top-right pos=-20,-20 size=120,40 text="0" fontSize=24""",
    "menu": """\
canvas Canvas
  layout Menu anchor=center size=240,300 dir=vertical spacing=10
    button Play text="Play"
    button Settings text="Settings"
    button Quit text="Quit" """,
    "dialog": """\
canvas Canvas
  panel Dialog anchor=center size=400,200
    text Message anchor=center pos=0,20 size=360,80 text="Message" fontSize=20
    button OK anchor=bottom-center pos=0,20 size=120,40 text="OK" """,
    "grid": """\
canvas Canvas
  layout Grid anchor=stretch dir=grid spacing=10
    image Cell1 size=100,100
    image Cell2 size=100,100
    image Cell3 size=100,100""",
}

_LAYOUT_COMPONENTS = {"layout": "VerticalLayoutGroup", "hlayout": "HorizontalLayoutGroup", "grid": "GridLayoutGroup"}

_PROMPT_TEMPLATE = """\
Generate a Unity UI DSL. Use 2-space indent for parent/child. Types: canvas|panel|image|text|button|layout.
Attrs: anchor=<preset> pos=x,y size=w,h color=#hex text="..." fontSize=N dir=vertical|horizontal spacing=N

Anchor presets: top-left top-right bottom-left bottom-right center stretch top-center bottom-center

Example:
canvas Canvas
  panel HUD anchor=stretch
    image HP anchor=top-left pos=20,-20 size=200,30 color=#c33
    text Score anchor=top-right pos=-20,-20 size=120,40 text="0" fontSize=24

Parent path: {parent}
Intent: {intent}

Output ONLY the DSL, no explanation, no fences."""


def get_template_dsl(name: str) -> str | None:
    return _TEMPLATES.get(name)


def parse_ui_dsl(dsl: str) -> list[dict]:
    tree = parse_indent_tree(dsl)
    nodes = []
    for item in tree:
        parts = item["line"].split(None, 1)
        elem_type = parts[0].lower()
        rest = parts[1] if len(parts) > 1 else ""
        # Name is first token, rest are attrs
        rest_parts = rest.split(None, 1) if rest else []
        name = rest_parts[0] if rest_parts else elem_type
        attr_str = rest_parts[1] if len(rest_parts) > 1 else ""
        attrs = parse_kv(attr_str) if attr_str else {}
        parent_name = None
        if item["parent"]:
            pp = item["parent"]["line"].split(None, 2)
            parent_name = pp[1] if len(pp) > 1 else (pp[0] if pp else None)
        nodes.append({"type": elem_type, "name": name, "attrs": attrs, "parent": parent_name})
    return nodes


def _node_path(node: dict, name_map: dict) -> str:
    parts = [node["name"]]
    parent = node.get("parent")
    visited: set = set()
    while parent and parent in name_map and parent not in visited:
        visited.add(parent)
        parts.insert(0, parent)
        parent = name_map[parent].get("parent")
    return "/" + "/".join(parts)


def build_ui_batch(nodes: list[dict], parent: str | None) -> list[str]:
    name_map = {n["name"]: n for n in nodes}
    lines = []
    for node in nodes:
        elem_parent = node.get("parent") or parent
        attrs = node["attrs"]
        # create_ui call
        create_args = {"type": node["type"].capitalize(), "name": node["name"]}
        if elem_parent:
            create_args["parent"] = elem_parent
        if "color" in attrs:
            create_args["color"] = attrs["color"]
        if "text" in attrs:
            create_args["text"] = attrs["text"]
        if "fontSize" in attrs:
            create_args["font_size"] = attrs["fontSize"]
        lines.append(build_batch_line("create_ui", **create_args))

        # set_rect for anchor/pos/size
        rect_args: dict = {}
        if "anchor" in attrs:
            rect_args["anchor"] = attrs["anchor"]
        if "pos" in attrs:
            rect_args["pos"] = attrs["pos"]
        if "size" in attrs:
            rect_args["size"] = attrs["size"]
        if rect_args:
            lines.append(build_batch_line("set_rect", path=_node_path(node, name_map), **rect_args))

        # Layout component
        layout_comp = _LAYOUT_COMPONENTS.get(node["type"])
        if layout_comp:
            node_path = _node_path(node, name_map)
            lines.append(build_batch_line("manage_component", action="add",
                                          path=node_path, type=layout_comp))
            if "spacing" in attrs:
                lines.append(build_batch_line("set_property", path=node_path,
                                              component=layout_comp, prop="spacing",
                                              value=attrs["spacing"]))

    return lines


async def ui_intent(intent: str, parent: str | None = None, template: str | None = None, dry_run: bool = False) -> str:
    """Convert NL intent to Unity UI hierarchy. Templates bypass Haiku.

    template: hud|menu|dialog|grid. dry_run=True skips execution.
    """
    # Template bypass
    if template:
        dsl = get_template_dsl(template)
        if dsl is None:
            raise ToolError(f"Unknown template '{template}'. Valid: {', '.join(_TEMPLATES)}")
        nodes = parse_ui_dsl(dsl)
        if not nodes:
            raise ToolError("DSL produced no nodes")
        batch_lines = build_ui_batch(nodes, parent=parent)
        if not batch_lines:
            raise ToolError("Builder produced no commands")
        if dry_run:
            return "\n".join(batch_lines)
        result = await _send("batch", {"commands": "\n".join(batch_lines)})
        return f"ui_intent: {len(batch_lines)} ops\n{result}"

    from .intent_common import sanitize_intent
    prompt = _PROMPT_TEMPLATE.format(parent=sanitize_intent(parent or "root"), intent=sanitize_intent(intent))

    def _parse_and_validate(dsl: str):
        return parse_ui_dsl(dsl)

    return await run_intent_pipeline(
        send=_send, sampling=_sampling, prompt=prompt, feature="ui_intent",
        parse_fn=_parse_and_validate, build_fn=lambda nodes: build_ui_batch(nodes, parent=parent),
        dry_run=dry_run,
    )


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(ui_intent)
