# Intent Tools

Intent tools convert constrained natural-language input into an intermediate
DSL, validate it, and compose existing Unity tools. They are convenience
orchestrators, not a second mutation protocol.

## Shared Sampling Contract

`SamplingService` is enabled only when `UNITY_MCP_VISUAL_VERIFY=1`. It loads a
feature profile from `llm_config.py` (model, turns, timeout, token limit, and
backend), applies the adaptive budget gate, invokes the configured CLI, and
records successful usage.

The current sampling transport supports only the `claude` backend. A different
configured backend fails closed and logs a warning; it is not silently routed
through Claude. The model within the Claude profile is configurable and must
not be hard-coded in tool documentation.

Shared safety behavior:

- intent text is capped and strips newlines/braces before prompt interpolation;
- output fences are removed before parsing;
- parsers accept a deliberately small DSL;
- an empty or invalid plan fails before batch execution;
- `dry_run=True` returns the compiled batch/file output without mutating Unity;
- intent tools are `direct_only` because they perform Python-side orchestration.

Some existing error messages still say "Haiku unavailable" for compatibility.
That wording is not the architectural contract; it means the configured
sampling path returned no result.

## `do(intent, dry_run=False)`

`do` is for ambiguous scene-structure requests, not targeted changes to known
objects.

```text
intent + hierarchy summary
  -> Planner / do_intent prompt
  -> batch DSL
  -> validate_plan
  -> batch(on_error=continue)
```

The validator checks the constrained command grammar and referenced/declared
scene paths. A dry run returns `DRY RUN plan:` followed by the DSL.

For a mutating run, `Executor` sends the batch with continue-on-error. When the
result contains one to five indexed `err:` lines, it may ask sampling for one
corrected retry, validates that retry against the original and newly declared
paths, then sends only the corrected commands. This is not atomic rollback: the
successful operations from the first batch remain applied.

```python
await do(
    intent="Create a Player cube at 5,0,0",
    dry_run=True,
)
```

## `ask(question)`

`ask` is read-only sampled scene analysis; it is not the interactive
`ask_user` tool.

1. A deterministic keyword router rejects leading mutation verbs and selects a
   canonical read plan for Unity-specific questions.
2. `AskExecutor` runs the plan.
3. A single result shorter than 200 characters is returned verbatim.
4. Longer/multiple results use the `summarize` sampling profile; if sampling is
   unavailable, the wrapper returns a bounded raw fallback.

Out-of-domain questions return the scene-question guidance. Mutating input
returns the read-only error and must be handled with explicit mutation tools,
not by weakening the router.

## `animator_intent(target, intent, dry_run=False)`

The Animator DSL supports four records:

```text
PARAM Speed float 0
STATE Idle Idle.anim
STATE Walk Walk.anim
DEFAULT Idle
TRANS Idle -> Walk dur=0.15 if Speed>0.1
```

Validation requires declared transition states and parameters. The builder
emits action-based `animator` batch commands for parameters, states, the default
state, and transitions. It does not create or infer animation clips beyond the
paths named in the sampled DSL.

## `vfx_intent(target, intent, kind="auto", dry_run=False)`

The current implementation supports particle systems only. `kind="auto"`
therefore resolves to `particle`; shader/material intent is not implemented.

These exact intent strings are deterministic presets and bypass sampling:

```text
fire_explosion, magic_burst, dissolve, glow_outline, smoke_trail
```

All other inputs use this constrained DSL:

```text
SET startColor = #FF2200
SET startSize = 0.5,1.0
MODULE colorOverLifetime ENABLED
GRADIENT color = #FF8800@0;#FF2200@1
```

The builder emits `particle action=set` commands. Gradient output also enables
the `colorOverLifetime` module. Parser extensions need matching validation and
particle-handler tests; unrecognized lines must not become arbitrary commands.

## `ui_intent`

`ui_intent(intent, parent=None, template=None, dry_run=False)` owns uGUI Canvas
generation. Templates `hud`, `menu`, `dialog`, and `grid` bypass sampling.
Sampled and template DSL compile to `create_ui`, `set_rect`,
`manage_component`, and `set_property` batch commands.

The indent parser uses two-space depth and resolves parent names from the last
node at the preceding depth. Changes to node naming/path construction require
tests for nested and duplicate-name cases.

## `uitk_intent`

`uitk_intent(intent, name, path="Assets/UI", attach_to=None, template=None,
dry_run=False)` owns UI Toolkit file generation. Templates `hud`, `menu`,
`dialog`, `settings`, and `editor_window` bypass sampling.

The intermediate format has `=TREE=` and `=STYLE=` sections. USS validation
rejects unsupported web-CSS constructs. A sampled validation failure triggers
one retry. A mutating call creates USS first, UXML second, and optionally calls
`attach_uitk`; completed files are reported but are not rolled back after a
later failure. See `AI/ui.md` for the exact file/attachment contract.

## Budget and Profiles

`budget_status()` reports the live `CostTracker` session spend, configured cap,
daily spend, and skipped-feature counts. It does not query a provider billing
API. The tracker estimates usage from feature registry metadata and configured
rates, persists the daily aggregate, and may fail open on persistence errors
while recording degraded metrics.

Initialization is controlled by:

- `UNITY_MCP_BUDGET=0` to disable tracker/router initialization;
- `UNITY_MCP_HAIKU_BUDGET` for the session cap;
- `UNITY_MCP_HAIKU_DAY_CAP` for the daily cap;
- `UNITY_MCP_BUDGET_DISABLED=1` to bypass adaptive routing in development.

The historical environment-variable names remain even though model selection
is profile-based. Do not copy estimated per-call prices into docs; they depend
on the selected model and current rate constants.

`set_llm_config` updates runtime feature profiles from lines formatted as:

```text
feature:model,max_turns,timeout,max_tokens[,backend]
```

Environment variables of the form `UNITY_MCP_LLM_MODEL_<FEATURE>` override only
the profile model.

## Failure Semantics

| Failure | Required behavior |
|---|---|
| Sampling disabled, budget-skipped, timed out, or unsupported backend | Fail before mutation unless a deterministic template/preset applies |
| Empty parsed DSL | Raise `ToolError`; do not send an empty batch |
| Animator validation error | Raise `INVALID DSL` before mutation |
| Generic intent validation error | Return/raise the validator reason before mutation |
| Batch partial failure | Preserve and report the actual indexed results; never claim rollback |
| UI Toolkit later operation fails | Name completed operations and files already processed |

## Tests and Ownership

- shared pipeline: `server/tests/test_intent_common.py` and sampling tests;
- `do`: `server/tests/test_do_intent.py`;
- `ask`: ask router/executor/summarizer tests;
- domain tools: `test_animator_intent.py`, `test_vfx_intent.py`,
  `test_ui_intent.py`, and `test_uitk_intent.py`.

Use `AI/testing.md` for repository test rules. User examples belong in
`docs/features/intent-tools.md`; consumer-agent procedures belong in the
relevant `unity-plugin/ClientSkills/skills/*/SKILL.md` source.

## Related

- `AI/api-design-standards.md`
- `AI/batch.md`
- `AI/ui.md`
- `AI/mcp-server.md`
