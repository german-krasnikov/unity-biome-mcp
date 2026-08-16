# Component events

`wire_event`, `list_events`, and `unwire_event` manage **persistent listeners on
serialized `UnityEvent` fields**. They do not turn ordinary Unity callbacks such
as `OnTriggerEnter` into events.

Enable the `COMPONENTS` category before using these tools.

<span id="list_events"></span>

## Inspect listeners first

Read the current listener list before changing it:

```python
listeners = await list_events(
    path="/Canvas/StartButton",
    component="Button",
    event="onClick",
)
```

The response includes the zero-based listener index, target path and type,
method, call state, and any static argument. An empty event reports zero
listeners.

Use the serialized event-field name. Built-in components commonly use names
such as `onClick` and `onValueChanged`; custom scripts may expose fields such as
`_onCompleted`.

<span id="wire_event"></span>

## Add a persistent listener

For a no-argument method:

```python
await wire_event(
    path="/Canvas/StartButton",
    component="Button",
    event="onClick",
    target="/GameManager",
    method="StartGame",
)
```

For a static argument, set both its type and value:

```python
await wire_event(
    path="/Canvas/DifficultyButton",
    component="Button",
    event="onClick",
    target="/GameManager",
    method="SetDifficulty",
    arg_type="int",
    arg_value="2",
    target_component_type="GameManager",
    parameter_types="int",
)
```

Supported static argument types are `void`, `bool`, `int`, `float`, `string`,
and `object`. An object argument resolves from a scene path or asset path.

When several target components expose the method, pass
`target_component_type`. When the selected component has overloads, also pass
comma-separated `parameter_types`, for example `"string"` or `"int,float"`.
The tool rejects an unresolved ambiguity instead of choosing silently.

The target can be a scene object or an asset. For scene objects, a component
owning the method is selected; `GameObject` itself remains a fallback for
methods such as `SetActive`.

<span id="unwire_event"></span>

## Remove listeners

Remove one listener by the index returned from `list_events`:

```python
await unwire_event(
    path="/Canvas/StartButton",
    component="Button",
    event="onClick",
    index=0,
)
```

Omit `index` to clear all persistent listeners from that one event:

```python
await unwire_event(
    path="/Canvas/StartButton",
    component="Button",
    event="onClick",
)
```

Clearing is intentionally broad. Inspect first when existing listeners may
belong to another system.

## Verify the complete workflow

```python
before = await list_events(
    path="/Canvas/StartButton", component="Button", event="onClick"
)

await wire_event(
    path="/Canvas/StartButton",
    component="Button",
    event="onClick",
    target="/GameManager",
    method="StartGame",
    target_component_type="GameManager",
)

after = await list_events(
    path="/Canvas/StartButton", component="Button", event="onClick"
)
```

Require `after` to contain the expected target type and method. Then enter Play
Mode and exercise the control if runtime behavior matters:

```python
await editor(action="play")
try:
    result = await run_playtest(
        script="CLICK /Canvas/StartButton WAIT 0.5\nASSERT_CONSOLE_CLEAN"
    )
finally:
    await editor(action="stop")
```

`wire_event` and `unwire_event` participate in Unity Undo for scene components.
Save the scene only after readback and runtime verification succeed.

See [UI Tools](ui.md) for creating controls and the
[generated schema](../tools-schema/index.md) for exact parameters.
