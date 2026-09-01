"""T21: ContextBuilder + built-in AttachmentProviders — provider-neutral context assembly."""

import asyncio
from collections.abc import Awaitable, Callable  # noqa: TC003
from typing import Protocol

from .brief import PRIORITY_RANK, AttachmentKind, AttachmentSlot, ContextBrief, Priority
from .editor_log import UNITY_UNREACHABLE

_PROVIDER_TIMEOUT = 5.0


class AttachmentProvider(Protocol):
    """One data source for one attachment kind. Duck-typed — no ABC."""
    kind: AttachmentKind
    priority: Priority
    token_budget: int

    async def fetch(self, send: Callable[..., Awaitable[str]]) -> str: ...


class CompileErrorsProvider:
    kind: AttachmentKind = "compile_errors"
    priority: Priority = "critical"
    token_budget: int = 200

    async def fetch(self, send: Callable[..., Awaitable[str]]) -> str:
        raw = await send("get_compile_errors", {})
        return "clean" if "No compilation errors" in raw else raw


class ConsoleProvider:
    kind: AttachmentKind = "console"
    priority: Priority = "critical"
    token_budget: int = 300

    async def fetch(self, send: Callable[..., Awaitable[str]]) -> str:
        return await send("get_console", {"count": "10", "level": "error,warning"})


class HierarchyProvider:
    kind: AttachmentKind = "hierarchy"
    priority: Priority = "medium"
    token_budget: int = 800

    async def fetch(self, send: Callable[..., Awaitable[str]]) -> str:
        return await send("get_hierarchy", {"summary": "true"})


class SelectionProvider:
    kind: AttachmentKind = "selection"
    priority: Priority = "low"
    token_budget: int = 150

    async def fetch(self, send: Callable[..., Awaitable[str]]) -> str:
        return await send("editor", {"action": "state"})


class ProfilerProvider:
    kind: AttachmentKind = "profiler"
    priority: Priority = "medium"
    token_budget: int = 200

    async def fetch(self, send: Callable[..., Awaitable[str]]) -> str:
        result = await send("get_profile_context", {})
        return "" if result == "no sessions" else result


_DEFAULT_PROVIDERS: list[AttachmentProvider] = [
    CompileErrorsProvider(),
    ConsoleProvider(),
    HierarchyProvider(),
    SelectionProvider(),
    ProfilerProvider(),
]


class ContextBuilder:
    def __init__(self, total_budget: int, send: Callable) -> None:
        self._budget = total_budget
        self._send = send
        self._providers: list = []

    def register(self, provider: AttachmentProvider) -> None:
        self._providers.append(provider)

    async def build(self, kinds: list[str] | None = None) -> ContextBrief:
        providers = self._providers
        if kinds is not None:
            providers = [p for p in providers if p.kind in kinds]

        sorted_providers = sorted(
            providers,
            key=lambda p: (PRIORITY_RANK.get(p.priority, 2), p.kind),
        )
        remaining = self._budget
        slots: list[AttachmentSlot] = []

        for provider in sorted_providers:
            try:
                content = await asyncio.wait_for(
                    provider.fetch(self._send),
                    timeout=_PROVIDER_TIMEOUT,
                )
            except (ConnectionError, OSError):
                # ARC-6 T4: a dead TCP call must yield an explicit unreachable slot,
                # not silent omission — otherwise the agent can't tell "genuinely
                # clean" apart from "couldn't check" for this attachment.
                content = UNITY_UNREACHABLE
            except Exception:
                content = ""

            if not content:
                continue

            if provider.priority == "critical":
                budget = min(provider.token_budget, max(0, remaining))
                slot = AttachmentSlot.of(provider.kind, content, budget)
                remaining -= slot.used_tokens
                slots.append(slot)
            else:
                threshold = provider.token_budget * 0.1
                if remaining < threshold:
                    continue
                budget = min(provider.token_budget, remaining)
                slot = AttachmentSlot.of(provider.kind, content, budget)
                remaining -= slot.used_tokens
                slots.append(slot)

        return ContextBrief.of(slots, self._budget)


def make_default_builder(total_budget: int, send: Callable) -> ContextBuilder:
    """Create a ContextBuilder pre-loaded with all built-in providers."""
    builder = ContextBuilder(total_budget=total_budget, send=send)
    for provider in _DEFAULT_PROVIDERS:
        builder.register(provider)
    return builder
