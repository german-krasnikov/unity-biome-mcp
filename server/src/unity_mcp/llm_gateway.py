"""ABC for the server-side LLM gateway used by intent tools, screenshot description, and
visual diff (Task 3.1C). SamplingService (sampling.py) is the sole implementation; call sites
type-hint against this interface instead of the concrete claude-CLI-backed class.
"""
from abc import ABC, abstractmethod


class ILlmGateway(ABC):
    """Interface for server-side LLM calls -- the 5 public methods SamplingService exposes."""

    @abstractmethod
    async def verify_visual(
        self, prompt: str, screenshot_path: str | None = None, *, feature: str = "visual_verify"
    ) -> str | None: ...

    @abstractmethod
    async def generate(self, prompt: str, *, feature: str = "do_intent") -> str | None: ...

    @abstractmethod
    async def describe_image(
        self, prompt: str, image_path: str, *, feature: str = "screenshot_describe"
    ) -> str | None: ...

    @abstractmethod
    async def verify_visual_diff(
        self, before: str, after: str, prompt: str, *, feature: str = "visual_diff"
    ) -> str | None: ...

    @abstractmethod
    async def summarize(
        self, data: str, instruction: str = "Summarize concisely", *, feature: str = "summarize"
    ) -> str | None: ...
