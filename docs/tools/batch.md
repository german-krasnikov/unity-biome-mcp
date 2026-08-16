# Batch operations

Use `batch` when two or more compatible Unity commands can share one call. The
commands still run in order on Unity's main thread, but the caller pays for only
one MCP round trip.

```python
result = await batch(commands="""
create_object name=Enemy primitive=Capsule
manage_component path=/Enemy type=Rigidbody action=add
set_property path=/Enemy component=Rigidbody prop=mass value=4
get_component path=/Enemy type=Rigidbody
""")
```

Use a typed MCP call for a single operation, for a direct-only tool, or when the
next call depends on parsing the previous result.

<span id="batch"></span>
<span id="batch-behavior"></span>

## Check whether a tool is batchable

The installed catalog is authoritative:

```python
catalog = await discover_tools(enable=False, structured=True)
```

An entry with `surfaces=direct,batch` may be used as a batch command. An entry
with `surfaces=direct` must be called through its typed MCP tool. This avoids a
duplicated command roster that becomes stale as tools are added.

Async runners, Python orchestrators, intent tools, and tools with special file
responses are commonly direct-only. If a direct-only command appears in a
batch, the Python server reports it instead of pretending Unity ran it.

## Write the command text

Put one command on each line using `name key=value` syntax. Blank lines and
lines beginning with `#` are ignored.

```python
result = await batch(commands="""
# Values containing spaces may be quoted.
create_object name="Boss Arena" primitive=Cube
set_property path="/Boss Arena" component=Transform \
prop=m_LocalPosition value=(0, 1.5, 6)
inspect paths="/Boss Arena" components=Transform
""")
```

Quoted values support escaped quotes, backslashes, and `\n`, `\r`, or `\t`.
Parenthesized values may contain spaces but must close on the same line. Use the
parameter names from the typed tool's generated schema; for example,
`set_property` uses `prop`, while `manage_component` uses `type`.

Commands can address an object created earlier in the same batch by its known
path. Batch text cannot capture an earlier command's returned value and inject
it into a later command. Project aliases such as `$player` are expanded before
each command is parsed.

## Choose failure behavior

The default `on_error="continue"` runs independent commands after a failure and
returns a mixed result. Use `on_error="stop"` when later commands would be
misleading after the first error:

```python
result = await batch(
    commands="""
create_object name=Checkpoint primitive=Sphere
set_property path=/Checkpoint component=Transform prop=m_LocalPosition value=(2,0,4)
get_component path=/Checkpoint type=Transform
""",
    on_error="stop",
)
```

Stopping does not undo commands that already succeeded. Use `atomic=True` when
the supported Unity mutations must be reverted together:

```python
result = await batch(
    commands="""
create_object name=EnemyA primitive=Capsule
create_object name=EnemyB primitive=Capsule
manage_component path=/EnemyB type=MissingComponent action=add
""",
    atomic=True,
)
```

`atomic=True` stops on the first failed, blocked, or timed-out subcommand and
asks Unity to revert the batch's Undo group. A successful rollback includes an
`ATOMIC_ROLLBACK` line. It covers only mutations correctly recorded in Unity
Undo. Asset files, packages, processes, arbitrary filesystem work, and other
external side effects are not guaranteed to roll back.

## Validate aliases without running commands

Use `validate_aliases=True` to check unresolved `$aliases` before mutation:

```python
check = await batch(
    commands="""
set_property path=$player component=Health prop=hp value=100
set_active path=$hud active=true
""",
    validate_aliases=True,
)
```

This mode executes zero commands. It validates alias expansion only; it is not
a full schema, object-existence, or component-existence dry run.

## Understand guards and results

Every subcommand keeps its Unity MCP Settings toggle, read-only, compile-state,
and Play Mode guards. A disabled Unity handler remains disabled. Deferred
categories control which typed tools appear in the advertised MCP list; they are
not an authorization boundary inside `batch`. A write may be blocked while a
read in the same `continue` batch still runs. Runtime-only commands require Play
Mode, while ordinary Editor mutations are generally blocked there.

Results preserve the zero-based command index and end with a summary such as:

```text
[0] Created /Enemy
[1] err: Component type 'MissingComponent' not found
[2] skip
ok:1 err:1
```

Treat `err:`, `BLOCKED:`, `TIMEOUT:`, and `ATOMIC_ROLLBACK` as explicit outcome
signals. Do not infer success from the MCP call merely returning. The `timeout`
argument bounds the overall call and supplies Unity with a slightly smaller
internal deadline; keep batches focused rather than raising it for unrelated
work.

## Reliable pattern

For a non-atomic bulk change:

1. Read the narrow state you need.
2. Batch independent mutations with an appropriate failure policy.
3. Inspect the summary and every failed line.
4. Read back the changed properties.
5. Run the relevant compile, reference, runtime, or visual verification.
6. Save only after verification succeeds.

For a guarded scene transaction that owns preflight, atomic apply, verification,
and save gating, use
[`scene_change_plan` and `apply_scene_change`](scene.md#apply-a-guarded-scene-change).
For exact `batch` parameters and installed tool signatures, use the
[generated schema](../tools-schema/index.md).
