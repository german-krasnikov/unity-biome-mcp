"""P3 Macro tools — expand to batch internally.

Reduces multi-step setup to a single tool call.
"""
from ._common import bind
from ._annotations import RW as _RW
from ..utils import parse_kv, _KV_RE

_send = None


def _quote_if_spaces(v: str) -> str:
    """Wrap value in quotes if it contains spaces and is not already quoted/parened."""
    if ' ' not in v:
        return v
    if (v.startswith('"') and v.endswith('"')) or (v.startswith('(') and v.endswith(')')):
        return v
    return f'"{v}"'


async def setup_objects(specs: str) -> str:
    """Create+configure multiple objects in one call.
    One per line: name [primitive=X] [parent=Y] [pos=(x,y,z)] [components=A,B]
    Example: NPC1 primitive=Capsule pos=(1,0,0) components=Health"""
    lines = []
    full_paths = []
    for spec in specs.strip().splitlines():
        spec = spec.strip()
        if not spec:
            continue
        parts = spec.split()
        name = parts[0]
        kv = parse_kv(" ".join(parts[1:]))

        cmd = f"create_object name={name}"
        parent = kv.get("parent", "")
        if "primitive" in kv:
            cmd += f" primitive={kv['primitive']}"
        if parent:
            cmd += f" parent={parent}"
        lines.append(cmd)

        # Compute full path: /Parent/Name when parent is set
        parent_norm = f"/{parent}" if parent and not parent.startswith("/") else parent
        full_path = f"{parent_norm}/{name}" if parent_norm else f"/{name}"
        full_paths.append(full_path)

        if "pos" in kv:
            lines.append(
                f"set_property path={full_path} component=Transform "
                f"prop=m_LocalPosition value={kv['pos']}"
            )
        if "components" in kv:
            for comp in kv["components"].split(","):
                lines.append(f"manage_component path={full_path} type={comp.strip()} action=add")

    if not full_paths:
        return "No valid object specs found"

    lines.append(f"inspect paths={','.join(full_paths)} components=Transform")
    return await _send("batch", {"commands": "\n".join(lines), "on_error": "continue"})


async def set_properties(path: str, props: str) -> str:
    """Set multiple properties on ONE object. For multiple objects, use configure_objects instead.
    Format: component.prop=value per line or semicolon-separated.
    Example: Transform.m_LocalPosition=(1,0,0);Rigidbody.mass=5"""
    lines = []
    components: set[str] = set()

    for pair in props.replace(";", "\n").strip().splitlines():
        pair = pair.strip()
        if not pair or "=" not in pair or "." not in pair.split("=")[0]:
            continue
        left, value = pair.split("=", 1)
        component, prop = left.rsplit(".", 1)
        component = component.strip()
        prop = prop.strip()
        lines.append(
            f"set_property path={path} component={component} prop={prop} value={_quote_if_spaces(value.strip())}"
        )
        components.add(component)

    if not lines:
        return "No valid property pairs found"

    for comp in sorted(components):
        lines.append(f"get_component path={path} type={comp}")

    return await _send("batch", {"commands": "\n".join(lines), "on_error": "continue"})


async def configure_objects(config: str) -> str:
    """Configure multiple objects at once.
    Format: /Path component.prop=value [...] per line.
    Example:
    /NPC1 Transform.m_LocalPosition=(1,0,0) Health.maxHp=100
    /NPC2 Transform.m_LocalPosition=(3,0,0)"""
    lines = []
    paths: set[str] = set()

    for line in config.strip().splitlines():
        line = line.strip()
        if not line:
            continue
        first_space = line.find(' ')
        if first_space == -1:
            continue
        obj_path = line[:first_space]
        if not obj_path.startswith("/") and ":/" not in obj_path:
            continue
        paths.add(obj_path)
        rest = line[first_space + 1:]
        for m in _KV_RE.finditer(rest):
            key = m.group(1)
            value = m.group(2).strip('"')
            if "." not in key:
                continue
            component, prop = key.rsplit(".", 1)
            lines.append(
                f"set_property path={obj_path} component={component} prop={prop} value={_quote_if_spaces(value)}"
            )

    if not lines:
        return "No valid config lines found"

    lines.append(f"inspect paths={','.join(sorted(paths))}")
    return await _send("batch", {"commands": "\n".join(lines), "on_error": "continue"})


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(setup_objects)
    mcp.tool(annotations=_RW)(set_properties)
    mcp.tool(annotations=_RW)(configure_objects)
