"""permission_prompt — --permission-prompt-tool MCP handler for Claude CLI."""
import json
import sys

from ._annotations import RO as _RO
from ._common import bind

_send = None
# Lazily initialized on first call to break circular import
# (middleware_types → tool_specs → tools/__init__ → this module → permission_broker).
# Tests override via monkeypatch.setattr(mod, "_broker", ...)
_broker = None

_MCP_PREFIX = "mcp__unity-biome-mcp__"


def _get_broker():
    """Return the active broker; lazy-import on first call in production."""
    if _broker is not None:
        return _broker
    from unity_mcp.permission_broker import _broker as _default  # noqa: PLC0415
    return _default


async def permission_prompt(tool_name: str, input: dict, tool_use_id: str):
    """Handle Claude permission prompts via MCP.

    Registered as --permission-prompt-tool so Claude routes all permission
    checks here instead of blocking on stdin.
    """
    if tool_name == "AskUserQuestion":
        try:
            questions = input.get("questions", [])
            answers_raw = await _send("ask_user", {"questions": json.dumps(questions)}, timeout=300.0)
            answers = json.loads(answers_raw)
            return json.dumps({
                "behavior": "allow",
                "updatedInput": {"questions": questions, "answers": answers},
            })
        except Exception as e:
            print(f"[permission_prompt] ask_user failed: {e}", file=sys.stderr)
            msg = "Unity not connected" if "connection" in str(e).lower() else "ask_user unavailable"
            return json.dumps({"behavior": "deny", "message": msg})

    cmd = tool_name[len(_MCP_PREFIX):] if tool_name.startswith(_MCP_PREFIX) else tool_name
    decision = _get_broker().decide(cmd)
    if decision.outcome == "deny":
        return json.dumps({"behavior": "deny", "message": decision.reason})
    return json.dumps({"behavior": "allow", "updatedInput": input})


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(permission_prompt)
