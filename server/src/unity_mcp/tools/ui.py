"""UI authoring + visual asset tools: create_ui, set_rect, menu, shader."""
from ._annotations import RW as _RW, RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None


async def create_ui(
    type: str,
    name: str | None = None,
    parent: str | None = None,
    anchor: str | None = None,
    pos: str | None = None,
    size: str | None = None,
    pivot: str | None = None,
    color: str | None = None,
    text: str | None = None,
    fontSize: str | None = None,
) -> str:
    """Create UI element with smart defaults. type: Canvas|Panel|Button|Text|Image. Auto-creates Canvas if needed."""
    return await _send("create_ui", _args(type=type, name=name, parent=parent, anchor=anchor,
                                          pos=pos, size=size, pivot=pivot, color=color,
                                          text=text, fontSize=fontSize))


async def set_rect(
    path: str,
    anchor: str | None = None,
    pos: str | None = None,
    size: str | None = None,
    pivot: str | None = None,
    offsetMin: str | None = None,
    offsetMax: str | None = None,
) -> str:
    """Set RectTransform. anchor: stretch|center|top-left|top-right|bottom-left|bottom-right|etc. pos/size: (x,y)."""
    return await _send("set_rect", _args(path=path, anchor=anchor, pos=pos, size=size,
                                         pivot=pivot, offsetMin=offsetMin, offsetMax=offsetMax))


async def menu(action: str, path: str | None = None) -> str:
    """Execute or list Unity Editor menu items. action: execute|list. execute: run menu item by path. list: show sub-items (omit path for all roots). Note: Edit/ menu items not supported by Unity API."""
    return await _send("menu", _args(action=action, path=path))


async def shader(
    action: str,
    path: str,
    target: str | None = None,
    preset: str | None = None,
    code: str | None = None,
    shader_name: str | None = None,
    prop: str | None = None,
    value: str | None = None,
    keyword: str | None = None,
    enabled: str | None = None,
    node_type: str | None = None,
    node_id: str | None = None,
    node_action: str | None = None,
    output_node: str | None = None,
    output_slot: int | None = None,
    input_node: str | None = None,
    input_slot: int | None = None,
    edge_action: str | None = None,
) -> str:
    """Read or write shader assets (.shader / .shadergraph). Use when you need to inspect shader properties, create a new shader from a preset or raw HLSL, change a shader property/keyword, or build/edit a Shader Graph node network.
    action: get (inspect path — shader name, properties, keywords) | create (new shader; preset=unlit|lit|transparent or code=HLSL string) | set (change prop+value or keyword+enabled on existing shader) | graph_get (read Shader Graph nodes/edges) | graph_create (new .shadergraph) | graph_node (add/remove/configure a node; node_type, node_id, node_action) | graph_edge (connect/disconnect slots; output_node/output_slot, input_node/input_slot, edge_action).
    For material shader assignment use `material` tool instead."""
    return await _send("shader", _args(
        action=action, path=path, target=target, preset=preset,
        code=code, shader_name=shader_name, prop=prop, value=value,
        keyword=keyword, enabled=enabled, node_type=node_type,
        node_id=node_id, node_action=node_action,
        output_node=output_node, output_slot=output_slot,
        input_node=input_node, input_slot=input_slot,
        edge_action=edge_action))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(create_ui)
    mcp.tool(annotations=_RW_IDEM)(set_rect)
    mcp.tool(annotations=_RW)(menu)
    mcp.tool(annotations=_RW)(shader)
