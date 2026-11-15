"""ask() meta-tool: NL question → router → Unity tools → Haiku summarize."""
from ..ask.executor import AskExecutor
from ..ask.router import is_mutating, route
from ..ask.summarizer import Summarizer
from ..sampling import sampling_service as _sampling
from ._common import bind
from .generated.catalog_tools import ask  # noqa: F401 -- wrapper the catalog generates; this module provides _ask_body

# Module-level references — patched in tests
_send = None


async def _ask_body(question: str) -> str:
    """Answer a read-only question about the Unity scene (AI-routed, not interactive — use `ask_user` to show a UI card and wait for user input).

    Routes to deterministic tool plans for common patterns,
    uses Haiku summarization for complex multi-tool results.
    """
    if is_mutating(question):
        return "ask is read-only — use other tools to mutate the scene"

    plan = route(question)

    if plan is None:
        return "ask is for scene questions only (no matching Unity context found)"

    executor = AskExecutor(_send)
    results = await executor.run(plan)

    summarizer = Summarizer(_sampling)
    return await summarizer.summarize(question, results, hint=plan.hint)


def register(mcp, send, args):
    bind(globals(), send, args)
