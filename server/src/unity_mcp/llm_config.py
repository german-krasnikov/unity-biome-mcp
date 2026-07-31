"""Universal LLM profile system — DRY replacement for hardcoded model/timeout in sampling.py."""
from dataclasses import dataclass, field
from typing import Optional
import os

__all__ = ["LlmProfile", "get_profile", "set_profile", "apply_config", "reset", "parse_tcp_config"]


@dataclass
class LlmProfile:
    model: str = "haiku"
    max_turns: int = 1
    timeout: float = 15.0
    max_tokens: Optional[int] = None
    backend: str = "claude"  # "" or absent → "claude" (backward compat)

    def to_cli_args(self) -> list[str]:
        args = ["--model", self.model, "--max-turns", str(self.max_turns)]
        if self.max_tokens:
            args += ["--max-tokens", str(self.max_tokens)]
        return args


_DEFAULTS: dict[str, LlmProfile] = {
    "visual_verify":       LlmProfile("haiku", max_turns=2, timeout=15.0),
    "screenshot_describe": LlmProfile("haiku", max_turns=2, timeout=20.0),
    "visual_diff":         LlmProfile("haiku", max_turns=2, timeout=25.0),
    "do_intent":           LlmProfile("haiku", max_turns=1, timeout=15.0),
    "ui_intent":           LlmProfile("haiku", max_turns=1, timeout=15.0),
    "vfx_intent":          LlmProfile("haiku", max_turns=1, timeout=15.0),
    "animator_intent":     LlmProfile("haiku", max_turns=1, timeout=15.0),
    "summarize":           LlmProfile("haiku", max_turns=1, timeout=15.0),
    "distiller":           LlmProfile("haiku", max_turns=1, timeout=15.0, max_tokens=500),
}

_overrides: dict[str, LlmProfile] = {}


def get_profile(feature: str) -> LlmProfile:
    env_model = os.environ.get(f"UNITY_MCP_LLM_MODEL_{feature.upper()}")
    base = _overrides.get(feature) or _DEFAULTS.get(feature) or LlmProfile()
    if env_model:
        return LlmProfile(model=env_model, max_turns=base.max_turns,
                          timeout=base.timeout, max_tokens=base.max_tokens)
    return base


def set_profile(feature: str, profile: LlmProfile) -> None:
    _overrides[feature] = profile


def apply_config(config_dict: dict) -> None:
    for feature, params in config_dict.items():
        if isinstance(params, dict):
            _overrides[feature] = LlmProfile(
                model=params.get("model", "haiku"),
                max_turns=params.get("max_turns", 1),
                timeout=params.get("timeout", 15.0),
                max_tokens=params.get("max_tokens"),
                backend=params.get("backend", "claude") or "claude",
            )


def reset() -> None:
    """Clear runtime overrides (for tests)."""
    _overrides.clear()


def parse_tcp_config(payload: str) -> dict:
    """Parse TCP plain-text: feature:model,turns,timeout,max_tokens[,backend]

    Backend is optional 5th field — absent means 'claude' (backward compat).
    """
    result = {}
    for line in payload.strip().splitlines():
        if ":" not in line:
            continue
        key, vals = line.split(":", 1)
        parts = vals.split(",")
        if len(parts) < 3:
            continue
        max_tokens_raw = parts[3].strip() if len(parts) > 3 else "0"
        backend_raw = parts[4].strip() if len(parts) > 4 else ""
        try:
            result[key.strip()] = {
                "model":      parts[0].strip(),
                "max_turns":  int(parts[1]),
                "timeout":    float(parts[2]),
                "max_tokens": int(max_tokens_raw) if max_tokens_raw and max_tokens_raw != "0" else None,
                "backend":    backend_raw or "claude",
            }
        except (ValueError, IndexError):
            continue
    return result
