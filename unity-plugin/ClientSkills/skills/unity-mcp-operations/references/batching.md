# Batching And Token Efficiency

Batch only two or more independent or sequentially compatible commands that
Unity registers on its synchronous batch surface. Prefer a specialized
aggregate tool when it already represents the intent.

## Selection Order

1. One operation: call the typed tool.
2. Repeated same-shape read: use `inspect`.
3. Multi-object create/configure: use `setup_objects` or `configure_objects`.
4. Compatible low-level commands: use `batch`.
5. Tests, playtests, screenshots, waits, prompts, and Python orchestrators:
   keep them as standalone typed calls.

## Good

```text
inspect(
  paths="/Player,/Target",
  components="Transform",
  fields="position,rotation"
)
```

```text
configure_objects(config="""
/KeyLight Light.intensity=1.5
/FillLight Light.intensity=0.8
""")
```

```text
batch(
  commands="""
create_object name=Marker primitive=Sphere
set_property path=/Marker component=Transform prop=m_LocalScale value=(0.25,0.25,0.25)
get_component path=/Marker type=Transform
""",
  on_error="stop",
  atomic=True
)
```

`atomic=True` reverts prior Unity Undo operations after the first failure.
It does not revert filesystem side effects from `execute_code`.

## Bad

```text
batch(commands="run_tests mode=EditMode\nscreenshot")
```

Both commands use special execution paths. Use `run_tests_wait(...)` and
`screenshot(...)` as separate calls.

```text
batch(commands="configure_objects config=...")
```

`configure_objects` is an aggregate Python tool and must be called directly.

```text
batch(commands="set_property ...", validate_aliases=True)
```

`validate_aliases=True` validates and executes nothing. Run validation first,
then make a second batch call without that flag.

## Alias Semantics

- Typed arguments resolve only whole-value aliases.
- Batch expands `$sigils` in command text before parsing.
- Validate uncertain aliases before a destructive batch.
- Do not combine alias validation and expected mutation in one call.

## Reusable MCP Skills

Use `save_skill` only after the same stable, batch-compatible sequence has
appeared at least twice. Store one logical operation and parameterize changing
values with `${name}`.

```text
save_skill(
  name="place_marker",
  description="Create and scale one scene marker.",
  code="""
create_object name=${name} primitive=Sphere parent=${parent}
set_property path=${parent}/${name} component=Transform prop=m_LocalScale value=${scale}
get_component path=${parent}/${name} type=Transform
"""
)
use_skill(
  name="place_marker",
  params="name=Goal,parent=/Environment,scale=(0.25,0.25,0.25)"
)
```

Do not save:

- a one-off sequence;
- direct-only, asynchronous, runtime-wait, screenshot, or test commands;
- a workflow whose targets or safety gates are still uncertain;
- secrets or machine-specific paths.

`use_skill` executes stored batch commands with the normal non-atomic batch
default. Put verification in the stored sequence and avoid destructive learned
skills unless their behavior is independently guarded.

Use `save_template` and `apply_template` for stable parameterized C# scene
scaffolds, not ordinary property edits. Inspect generated scene state and
console output after applying a template.
