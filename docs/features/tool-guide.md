# Tool Decision Guide

Choose the narrowest tool that expresses the task. Typed tools are predictable,
easy to verify, and preferred when you know the object path and desired value.
Use intent tools only when the request is genuinely ambiguous.

## Choose by task

| Task | Start with | Follow with |
|---|---|---|
| Understand the scene | `get_hierarchy` | `search_scene` to narrow the result |
| Read one component | `get_component` | Read only the fields you need |
| Read several objects | `inspect` | Use one call for related state |
| Create or remove an object | `create_object` / `delete_object` | `get_hierarchy` or `search_scene` |
| Change a serialized value | `set_property` | `get_component` |
| Add or remove a component | `manage_component` | `get_component` |
| Connect a UnityEvent | `wire_event` | `list_events` |
| Change an asset | `asset`, `material`, `shader`, or `prefab` | Re-read the asset and check the Console |
| Exercise live behavior | `run_playtest` | Inspect its assertions and console evidence |
| Run NUnit tests | `run_tests_wait` | Accept only its correlated terminal result |
| Diagnose a broken session | `doctor` | Use the specific recovery action it recommends |
| Apply several compatible calls | `batch` | Verify the important resulting state |
| Plan a broad scene request | `do(..., dry_run=True)` | Review, apply, then verify |

The [generated tool schema](../tools-schema/index.md) is the exhaustive source for
parameters and defaults. The task guides under [Tools](../tools/index.md) explain
safe workflows.

## Batch compatible operations

Use `batch` when several tools support the batch surface and belong to one logical
change. Every command uses `key=value` arguments:

```python
result = await batch(
    """
create_object name=Player primitive=Capsule
manage_component path=/Player type=Rigidbody action=add
set_property path=/Player component=Transform prop=position value=0,1,0
get_component path=/Player type=Transform
""",
    on_error="stop",
)
```

Do not assume every tool can be batched. Use
`discover_tools(enable=False, structured=True)` as the source of truth for
supported surfaces. The generated schema owns parameters and signatures, not
batch eligibility. Intent tools, test runners, and other direct-only tools must
be called through their typed interface. See [Batch Operations](../tools/batch.md)
for atomic and failure behavior.

## Read several objects together

Prefer one `inspect` call when the reads form a single snapshot:

```python
state = await inspect(
    paths="/Player,/EnemyA,/EnemyB",
    components="Transform,Health",
    fields="position,currentHealth",
)
```

Use separate `get_component` calls when each result drives a different decision or
when a bulk response would be harder to review.

## Use intent tools deliberately

Preview a broad request before it mutates the scene:

```python
plan = await do(
    "Create three evenly spaced spawn points under /Level/Spawns",
    dry_run=True,
)
```

For an exact operation such as setting `/Player`'s Rigidbody mass, use
`set_property` directly. See [Intent Tools](intent-tools.md) for sampling and
deterministic-template behavior.

## Verify the outcome

Verification should test the user-visible contract, not merely repeat the write:

- Read serialized state after object, component, asset, or event changes.
- Check `get_compile_errors` and relevant Console entries after C# changes.
- Use `run_playtest` for runtime behavior and `run_tests_wait` for NUnit coverage.
- Capture a screenshot only when appearance is part of acceptance. A multi-view
  capture needs a target, for example
  `await screenshot(camera="multi_view", path="/Player")`.

For multi-step production changes, use the
[guarded scene-change workflow](../tools/scene.md#apply-a-guarded-scene-change).
