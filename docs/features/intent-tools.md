# Intent Tools

Intent tools translate a plain-language request into deterministic Unity tool
calls. They are useful when the desired result is clear but the exact object or
tool sequence is not. For known paths and exact changes, prefer the typed tool or
[`batch`](../tools/batch.md): it is easier to review and reproduce.

Intent tools are direct calls; they cannot run inside `batch`.

| Tool | Use it for | Mutates the project? |
|---|---|---|
| `ask` | Answer a read-only question about the current scene | No |
| `do` | Plan and apply a scene change from a broad request | Yes |
| `animator_intent` | Build Animator parameters, states, and transitions | Yes |
| `vfx_intent` | Configure a Particle System from a preset or description | Yes |
| `ui_intent` | Build a Canvas-based UI hierarchy | Yes |
| [`uitk_intent`](../tools/ui.md#generate-a-panel-from-intent) | Build a UI Toolkit UXML/USS panel | Yes |

Generated plans are validated, but they still operate on your project. Preview
ambiguous or wide changes and verify the result with a read tool.

## `ask(question)`

Use `ask` for scene questions that would otherwise require several reads.

```python
answer = await ask("Which enemies have a Health component?")
```

The router gathers read-only scene context and may use configured LLM sampling to
summarize a complex result. Questions that look mutating are rejected; use `do` or
a typed mutation tool instead.

## `do(intent, dry_run=False)`

Use `do` when the request is broad or the necessary scene paths are not known.
Preview first when the operation could affect several objects:

```python
plan = await do(
    "Create three evenly spaced spawn points under /Level/Spawns",
    dry_run=True,
)

# Review the returned DSL. To execute those exact commands, copy the plan after
# the "DRY RUN plan:" line into a direct batch call.
result = await batch(commands="""
create_object name=Spawn1 parent=/Level/Spawns
set_property path=/Level/Spawns/Spawn1 component=Transform prop=position value=-2,0,0
create_object name=Spawn2 parent=/Level/Spawns
set_property path=/Level/Spawns/Spawn2 component=Transform prop=position value=0,0,0
create_object name=Spawn3 parent=/Level/Spawns
set_property path=/Level/Spawns/Spawn3 component=Transform prop=position value=2,0,0
""", on_error="stop")
```

`do` reads a compact hierarchy, generates a batch plan, validates the plan, and
then executes it. A second `do` call samples a new plan; a dry run is advisory,
not an approval token for the next call. Execution uses `on_error="continue"`.
After a small partial failure, `do` may sample and run one repair plan, while
successful earlier operations remain applied. An invalid or unavailable initial
plan fails without applying commands.

When every required command is in its Unity-Undo allowlist, use the
[guarded scene-change workflow](../tools/scene.md#apply-a-guarded-scene-change)
for explicit stop-on-error, verification, and save gates.

## `animator_intent(target, intent, dry_run=False)` {#animator_intent}

Creates Animator parameters, states, a default state, and transitions from a
description.

```python
plan = await animator_intent(
    target="/Player",
    intent="Idle and Walk states controlled by a Speed float at threshold 0.1",
    dry_run=True,
)
```

The generated DSL supports float, int, bool, and trigger parameters; states with
animation clips; a default state; and transition conditions. Confirm that named
clips exist before applying the plan. Use the typed [`animator`](../tools/animation.md#animator)
tool when state names and transitions are already known.

## `vfx_intent(target, intent, kind="auto", dry_run=False)` {#vfx_intent}

Configures a Particle System. `kind` currently accepts `auto` or `particle`;
shader/material intent is not implemented by this tool.

Five exact preset names bypass LLM generation:

- `fire_explosion`
- `magic_burst`
- `dissolve`
- `glow_outline`
- `smoke_trail`

```python
# Deterministic preset.
result = await vfx_intent("/Effects/Explosion", "fire_explosion")

# Preview a generated particle plan.
plan = await vfx_intent(
    target="/Effects/Dust",
    intent="A subtle dust puff that fades quickly",
    kind="particle",
    dry_run=True,
)
```

The target must already contain the Particle System that the generated `particle`
commands configure.

## `ui_intent(intent, parent=None, template=None, dry_run=False)` {#ui_intent}

Builds Canvas-based (uGUI) elements. It does not create UI Toolkit UXML or USS;
use [`uitk_intent`](../tools/ui.md#generate-a-panel-from-intent) for that.

The deterministic templates are `hud`, `menu`, `dialog`, and `grid`:

```python
plan = await ui_intent(
    "Create the standard heads-up display",
    parent="/UI",
    template="hud",
    dry_run=True,
)
```

Without a template, configured LLM sampling generates a uGUI hierarchy plan:

```python
plan = await ui_intent(
    "Create a centered pause panel with Resume and Quit buttons",
    parent="/UI/Canvas",
    dry_run=True,
)
```

See [UI authoring](../tools/ui.md) for typed uGUI and UI Toolkit workflows.

## Sampling and budget status

Generated intent plans require optional LLM sampling to be enabled and the
supported Claude CLI backend to be installed and authenticated. Deterministic VFX
presets and UI templates do not make a generation call. Configure sampling under
**MCP > Settings > LLM Sampling**; see [Settings](../settings.md#llm-sampling).

`budget_status()` reports the current session/day estimate and any features skipped
by the budget router. Treat it as an estimate, not a provider invoice. Limits and
model pricing can change, so this guide intentionally does not hard-code a
per-request price.

<span id="common-workflow"></span>

## Reliable workflow

1. Use a typed tool when the target and operation are known.
2. Otherwise, call the intent tool with `dry_run=True` when it supports preview.
3. Check target paths, asset names, and the breadth of the returned plan.
4. Apply the request.
5. Verify with `inspect`, `get_hierarchy`, an Animator read, or a screenshot as
   appropriate.

See also [Tool Decision Guide](tool-guide.md), [Batch Operations](../tools/batch.md),
and [Screenshots and Visual Comparison](../tools/screenshots.md).
