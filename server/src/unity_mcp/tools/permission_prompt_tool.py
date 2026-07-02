"""permission_prompt — --permission-prompt-tool MCP handler for Claude CLI."""
import json
import sys
from ._annotations import RO as _RO
from ._common import bind

_send = None


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
    return json.dumps({"behavior": "allow", "updatedInput": input})


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(permission_prompt)
