"""ask_user — interactive question shown as Unity UI card; blocks until user submits."""
from ._common import bind
from .generated.catalog_tools import tools

_send = None


async def ask_user(questions: str) -> str:
    """Show a question card in Unity chat; wait for user answer (interactive UI — use `ask` for read-only AI scene questions instead).

    questions: JSON array matching AskUserQuestion schema:
      [{"question":"...","header":"...","options":[{"label":"..."}],"multiSelect":false}]
    Returns JSON map of question→answer (or free text if Other field used).
    Use this instead of AskUserQuestion for in-Unity interactive prompts.
    """
    return await _send("ask_user", {"questions": questions}, timeout=300.0)


def register(mcp, send, args):
    bind(globals(), send, args)
    tools.register(mcp, ask_user)
