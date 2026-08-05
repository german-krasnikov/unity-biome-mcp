"""Object state snapshots with diffing. In-memory, no C# changes."""
import contextlib

_send = None
_snapshots: dict[str, dict] = {}  # label → {path, state, console}


def parse_inspect_output(text: str) -> dict:
    """Parse inspect output into {component.field: value} dict.
    Component headers end with ':'. Fields use ': ' separator."""
    result = {}
    current_component = ""
    for line in text.split("\n"):
        stripped = line.strip()
        if not stripped:
            continue
        if stripped.endswith(":") and ": " not in stripped:
            current_component = stripped[:-1]
            continue
        if ": " in stripped:
            k, v = stripped.split(": ", 1)
            key = f"{current_component}.{k.strip()}" if current_component else k.strip()
            result[key] = v.strip()
    return result


def diff_snapshots(old: dict, new: dict) -> str:
    """Structured diff of two snapshot dicts. Returns change summary."""
    old_state = old.get("state", {})
    new_state = new.get("state", {})
    lines = []

    for key in sorted(set(old_state) | set(new_state)):
        if key not in old_state:
            lines.append(f"+ {key}={new_state[key]}")
        elif key not in new_state:
            lines.append(f"- {key}")
        elif old_state[key] != new_state[key]:
            lines.append(f"~ {key}: {old_state[key]} → {new_state[key]}")

    old_console = old.get("console", "")
    new_console = new.get("console", "")
    if new_console and new_console != old_console:
        lines.append(f"console: {new_console}")

    return "\n".join(lines) if lines else "no changes detected"


async def snapshot(path: str, label: str = "default", compare: str = "") -> str:
    """Capture or compare object state.

    path: Object path ("/Enemy_01")
    label: Snapshot label ("before", "after")
    compare: Label to diff against (empty = capture only)

    Returns:
        Capture: "snapshot 'label' saved (N fields)"
        Compare: structured diff or error if compare label missing
    """
    if compare:
        if compare not in _snapshots:
            return f"err: snapshot '{compare}' not found"
        if compare == label:
            return "err: compare label same as capture label"

    state_raw = await _send("inspect", {"paths": path, "full": True, "_no_distill": True})
    parsed = parse_inspect_output(state_raw)

    console = ""
    with contextlib.suppress(Exception):
        console = await _send("get_console", {"count": 10})

    _snapshots[label] = {"path": path, "state": parsed, "console": console}

    if not compare:
        return f"snapshot '{label}' saved ({len(parsed)} fields)"

    return diff_snapshots(_snapshots[compare], _snapshots[label])


def register(mcp, send, args):
    global _send
    _send = send
    from unity_mcp.tools._annotations import RO as _RO
    mcp.tool(annotations=_RO)(snapshot)
