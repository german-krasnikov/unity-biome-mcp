"""vfx_intent — NL → VFX DSL → particle batch commands."""
import re

from mcp.server.fastmcp.exceptions import ToolError

from ..sampling import sampling_service as _sampling
from ._annotations import RW as _RW
from ._common import bind
from .intent_common import build_batch_line, run_intent_pipeline

_send = None

# 5 built-in presets — bypass Haiku entirely
_PRESETS: dict[str, dict] = {
    "fire_explosion": {
        "sets": [("startColor", "#FF2200"), ("startSize", "0.5,2.0"), ("startSpeed", "3,8"), ("maxParticles", "200")],
        "modules": [("colorOverLifetime", "ENABLED"), ("sizeOverLifetime", "ENABLED")],
        "gradients": [("color", "#FF8800@0;#FF2200@0.5;#33000080@1")],
    },
    "magic_burst": {
        "sets": [("startColor", "#8800FF"), ("startSize", "0.2,0.8"), ("startSpeed", "2,5"), ("maxParticles", "100")],
        "modules": [("colorOverLifetime", "ENABLED")],
        "gradients": [("color", "#CC88FF@0;#8800FF@0.6;#00000000@1")],
    },
    "dissolve": {
        "sets": [("startColor", "#FFFFFF"), ("startSize", "0.1,0.3"), ("startSpeed", "0.5,1.5"), ("maxParticles", "300")],
        "modules": [("sizeOverLifetime", "ENABLED")],
        "gradients": [],
    },
    "glow_outline": {
        "sets": [("startColor", "#FFFF00"), ("startSize", "0.05,0.15"), ("startSpeed", "0"), ("maxParticles", "50")],
        "modules": [("colorOverLifetime", "ENABLED")],
        "gradients": [("color", "#FFFF00@0;#FFFF0000@1")],
    },
    "smoke_trail": {
        "sets": [("startColor", "#888888"), ("startSize", "0.3,1.0"), ("startSpeed", "0.5,1.0"), ("maxParticles", "150")],
        "modules": [("colorOverLifetime", "ENABLED"), ("sizeOverLifetime", "ENABLED")],
        "gradients": [("color", "#66666680@0;#33333300@1")],
    },
}


_PROMPT_TEMPLATE = """\
Generate a VFX DSL for Unity particle system. Use ONLY:
SET <prop> = <value>
MODULE <moduleName> ENABLED|DISABLED
GRADIENT <prop> = <color>@<time>;...

Example:
SET startColor = #FF2200
SET startSize = 0.5,1.0
MODULE colorOverLifetime ENABLED
GRADIENT color = #FF8800@0;#FF2200@1

Target: {target}
VFX kind: {kind}
Intent: {intent}

Output ONLY the DSL, no explanation, no fences."""


def get_preset_config(name: str) -> dict | None:
    return _PRESETS.get(name)


def detect_kind(intent: str) -> str:
    """Detect VFX kind. Currently particle-only; shader DSL not yet implemented."""
    return "particle"


def parse_vfx_dsl(dsl: str) -> dict:
    result = {"sets": [], "modules": [], "gradients": []}
    for line in dsl.splitlines():
        line = line.strip()
        if not line:
            continue
        if line.startswith("SET "):
            m = re.match(r"SET\s+(\w+)\s*=\s*(.+)", line)
            if m:
                result["sets"].append((m.group(1), m.group(2).strip()))
        elif line.startswith("MODULE "):
            parts = line.split()
            if len(parts) >= 3:
                result["modules"].append((parts[1], parts[2]))
        elif line.startswith("GRADIENT "):
            m = re.match(r"GRADIENT\s+(\w+)\s*=\s*(.+)", line)
            if m:
                result["gradients"].append((m.group(1), m.group(2).strip()))
    return result


def build_vfx_batch(target: str, data: dict) -> list[str]:
    lines = []
    for prop, val in data.get("sets", []):
        lines.append(build_batch_line("particle", action="set", path=target, module="main", prop=prop, value=val))
    for mod, state in data.get("modules", []):
        enabled = "true" if state.upper() == "ENABLED" else "false"
        lines.append(build_batch_line("particle", action="set", path=target, module=mod, prop="enabled", value=enabled))
    for _prop, grad in data.get("gradients", []):
        lines.append(build_batch_line("particle", action="set", path=target, module="colorOverLifetime", prop="enabled", value="true"))
        lines.append(build_batch_line("particle", action="set", path=target, module="colorOverLifetime", prop="gradient", value=grad))
    return lines


async def vfx_intent(target: str, intent: str, kind: str = "auto", dry_run: bool = False) -> str:
    """Convert NL intent to Unity VFX setup. Presets bypass Haiku entirely.

    kind: particle|auto (shader/material not yet implemented). dry_run=True skips execution.
    """
    # Preset bypass
    preset = get_preset_config(intent.strip())
    if preset is not None:
        batch_lines = build_vfx_batch(target, preset)
        if not batch_lines:
            raise ToolError("DSL produced no commands")
        if dry_run:
            return "\n".join(batch_lines)
        result = await _send("batch", {"commands": "\n".join(batch_lines)})
        return f"vfx_intent: {len(batch_lines)} ops\n{result}"

    if kind == "auto":
        kind = detect_kind(intent)
    from .intent_common import sanitize_intent
    prompt = _PROMPT_TEMPLATE.format(target=sanitize_intent(target), kind=sanitize_intent(kind), intent=sanitize_intent(intent))
    return await run_intent_pipeline(
        send=_send, sampling=_sampling, prompt=prompt, feature="vfx_intent",
        parse_fn=parse_vfx_dsl, build_fn=lambda d: build_vfx_batch(target, d),
        dry_run=dry_run,
    )


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(vfx_intent)
