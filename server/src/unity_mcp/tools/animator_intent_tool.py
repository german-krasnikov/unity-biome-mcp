"""animator_intent — NL → animator DSL → batch commands."""
import re
from typing import Optional
from ..sampling import sampling_service as _sampling
from .intent_common import build_batch_line, run_intent_pipeline
from ._annotations import RW as _RW
from ._common import bind

_send = None

_PROMPT_TEMPLATE = """\
Generate an animator DSL for Unity. Use ONLY these keywords:
PARAM <name> <type> <default>    (types: float|int|bool|trigger)
STATE <name> <clip.anim>
DEFAULT <state>
TRANS <src> -> <dst> dur=<float> [if <Param><op><value>]

Example:
PARAM Speed float 0
STATE Idle Idle.anim
STATE Walk Walk.anim
DEFAULT Idle
TRANS Idle -> Walk dur=0.15 if Speed>0.1
TRANS Walk -> Idle dur=0.15 if Speed<0.1

Target object: {target}
Intent: {intent}

Output ONLY the DSL, no explanation, no fences."""


def parse_animator_dsl(dsl: str) -> dict:
    result = {"params": [], "states": [], "default": None, "transitions": []}
    for line in dsl.splitlines():
        line = line.strip()
        if not line:
            continue
        if line.startswith("PARAM "):
            parts = line.split()
            if len(parts) >= 4:
                result["params"].append((parts[1], parts[2], parts[3]))
        elif line.startswith("STATE "):
            parts = line.split(None, 2)
            if len(parts) >= 3:
                result["states"].append((parts[1], parts[2]))
        elif line.startswith("DEFAULT "):
            result["default"] = line.split(None, 1)[1]
        elif line.startswith("TRANS "):
            m = re.match(r"TRANS ([\w*]+) -> (\w+) dur=([\d.]+)(?:\s+if\s+(.+))?", line)
            if m:
                result["transitions"].append({
                    "source": m.group(1), "target": m.group(2),
                    "duration": m.group(3), "condition": m.group(4),
                })
    return result


def validate_animator_dsl(data: dict) -> Optional[str]:
    state_names = {s[0] for s in data["states"]}
    param_names = {p[0] for p in data["params"]}
    for t in data["transitions"]:
        if t["target"] not in state_names:
            return f"Undeclared state in transition target: '{t['target']}'"
        if t["source"] not in state_names and t["source"] != "*":
            return f"Undeclared state in transition source: '{t['source']}'"
        cond = t.get("condition")
        if cond:
            # Extract param name from condition like Speed>0.1
            m = re.match(r"([A-Za-z_]\w*)", cond)
            if m and m.group(1) not in param_names:
                return f"Condition references undeclared param: '{m.group(1)}'"
    return None


def build_animator_batch(target: str, data: dict) -> list[str]:
    lines = []
    if data["params"]:
        params_str = ";".join(f"{n}:{t}:{d}" for n, t, d in data["params"])
        lines.append(build_batch_line("animator", path=target, action="add_param", params=params_str))
    if data["states"]:
        states_str = ";".join(f"{n}:{c}" for n, c in data["states"])
        lines.append(build_batch_line("animator", path=target, action="add_state", states=states_str))
    if data["default"]:
        lines.append(build_batch_line("animator", path=target, action="set_default", state=data["default"]))
    for t in data["transitions"]:
        lines.append(build_batch_line(
            "animator", path=target, action="add_transition",
            source=t["source"], target=t["target"], duration=t["duration"],
            conditions=t["condition"],
        ))
    return lines


async def animator_intent(target: str, intent: str, dry_run: bool = False) -> str:
    """Convert NL intent to Unity Animator Controller setup via DSL.

    dry_run=True returns the batch plan without executing it.
    """
    from .intent_common import sanitize_intent
    prompt = _PROMPT_TEMPLATE.format(target=sanitize_intent(target), intent=sanitize_intent(intent))
    return await run_intent_pipeline(
        send=_send, sampling=_sampling, prompt=prompt, feature="animator_intent",
        parse_fn=parse_animator_dsl,
        validate_fn=validate_animator_dsl,
        build_fn=lambda d: build_animator_batch(target, d),
        dry_run=dry_run,
    )


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(animator_intent)
