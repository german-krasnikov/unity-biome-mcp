
from ..degrade import degrade, wrap_degraded
from ..sampling import SamplingService
from ..sampling_postproc import is_refusal as _is_refusal  # noqa: F401 — re-export
from ..sampling_postproc import normalize  # noqa: F401 — re-export
from .cache import FingerprintCache
from .prompts import resolve, resolve_som

_cache_singleton = FingerprintCache()


class ScreenshotDescriber:
    def __init__(self, sampling: SamplingService, cache: FingerprintCache):
        self._sampling = sampling
        self._cache = cache

    async def describe(
        self,
        path: str,
        prompt_key: str,
        scene_fp: str | None,
        *,
        mark: bool = False,
        rects: list | None = None,
        legend: str | None = None,
    ) -> str:
        if mark and legend and legend != "(no marks)":
            prompt, _ = resolve_som(prompt_key, legend)
        else:
            prompt, _ = resolve(prompt_key)
        cache_key = scene_fp or path
        # Fast path: warm cache (no API cost)
        hit = self._cache.get(cache_key, prompt)
        if hit:
            return hit

        async def _haiku_call():
            text = await self._sampling.describe_image(prompt, path)
            cleaned, refused = normalize(text, "description")
            if refused:
                return wrap_degraded("screenshot_describe", "haiku_refused",
                                     "(describe unavailable: refused)")
            if cleaned:
                self._cache.put(cache_key, prompt, cleaned)
            return cleaned

        step_name, result = await degrade("screenshot_describe", [
            ("haiku_describe", _haiku_call),
            ("describe_disabled", lambda: "(describe unavailable)"),
        ])
        if step_name != "haiku_describe":
            return wrap_degraded("screenshot_describe", step_name, result)
        return result


def get_describer() -> ScreenshotDescriber:
    return ScreenshotDescriber(SamplingService(), _cache_singleton)
