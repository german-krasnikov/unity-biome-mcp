"""Static feature metadata: priority, difficulty, token estimates."""
from dataclasses import dataclass
from typing import Literal

Priority = Literal["critical", "medium", "low"]


@dataclass(frozen=True)
class FeatureMeta:
    priority: Priority
    difficulty: float    # 0.0 trivial, 1.0 vision-only
    est_in: int          # input token estimate
    est_out: int         # output token estimate
    image: bool          # adds ~1500 in_tok if True


FEATURES: dict[str, FeatureMeta] = {
    "do_intent":           FeatureMeta("critical", 0.5, 800,  300, False),
    "ask":                 FeatureMeta("critical", 0.5, 600,  200, False),
    "visual_verify":       FeatureMeta("critical", 0.7, 400,  100, True),
    "visual_diff":         FeatureMeta("medium",   0.9, 400,  300, True),
    "screenshot_describe": FeatureMeta("medium",   0.9, 400,  200, True),
    "scene_brief":         FeatureMeta("medium",   0.4, 2000, 400, False),
    "watchdog":            FeatureMeta("low",      0.3, 500,  100, False),
    "summarize":           FeatureMeta("low",      0.2, 1500, 200, False),
    "speculation":         FeatureMeta("low",      0.4, 600,  150, False),
    "animator_intent":     FeatureMeta("medium",   0.6, 800,  400, False),
    "vfx_intent":          FeatureMeta("medium",   0.6, 800,  400, False),
    "ui_intent":           FeatureMeta("medium",   0.6, 800,  400, False),
    "uitk_intent":         FeatureMeta("medium",   0.6, 900,  500, False),
    "som_visual":          FeatureMeta("medium",   0.9, 1700, 200, True),
    "distiller":           FeatureMeta("medium",   0.5, 2000, 400, False),
}

DEFAULT_FEATURE = FeatureMeta("medium", 0.5, 500, 200, False)


def get_feature(name: str) -> FeatureMeta:
    """Returns FeatureMeta or default for unknown features."""
    return FEATURES.get(name, DEFAULT_FEATURE)
