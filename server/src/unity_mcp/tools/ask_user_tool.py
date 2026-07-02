"""ask_user — interactive question shown as Unity UI card; blocks until user submits."""
from ._annotations import RO as _RO
from ._common import bind

_send = None


async def ask_user(questions: str) -> str:
    """Show a question card in Unity chat; wait for user answer.

    questions: JSON array matching AskUserQuestion schema:
      [{"question":"...","header":"...","options":[{"label":"..."}],"multiSelect":false}]
    Returns JSON map of question→answer (or free text if Other field used).
    Use this instead of AskUserQuestion for in-Unity interactive prompts.
    """
    return await _send("ask_user", {"questions": questions}, timeout=300.0)


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(ask_user)
