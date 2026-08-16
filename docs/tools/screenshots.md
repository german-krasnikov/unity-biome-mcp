# Screenshots and visual diffs

Use `screenshot` to capture a Unity view, then optionally save a named baseline
and compare later captures. These tools are useful for visual verification, but
a model-generated description is not a pixel-perfect assertion.

<span id="screenshot"></span>

## Capture an image

The default capture is 640 × 480 and is saved under the project-local
`ScreenShots/` directory:

```python
image = await screenshot(camera="scene_view", width=1280, height=720)
```

Common camera modes are:

| Camera | Result |
|---|---|
| `scene_view` | Current Editor Scene view |
| `scene_view_frame` | Scene view framed around Unity's current selection |
| `multi_view` | Combined diagnostic views of the object required by `path` |
| `single_view` | One generated view of the object required by `path`, using `angle` |
| `overview` | Top-down scene overview |
| `overview_game` | Orthographic overview aligned to the main camera, or a default perspective when none exists |

For a focused capture:

```python
image = await screenshot(
    camera="single_view",
    path="/Player",
    angle="iso",
    width=800,
    height=600,
    zoom=1.4,
    highlight="/Player:#00FF88",
    show_colliders=True,
)
```

`single_view` accepts `front`, `left`, `top`, `iso`, or explicit Euler angles:

```python
image = await screenshot(
    camera="single_view",
    path="/Vehicle",
    angle="front",
    supersample=2,
)
```

`output_path` may choose a different destination inside the Unity project. The
plugin rejects paths outside the project. Old automatic captures are pruned by
the plugin, so copy or baseline an image that must be retained.

### Request a description

Set `describe` to a built-in prompt key or a short custom question. Built-in
keys include `auto`, `scene_overview`, `verify_position`, `verify_color`,
`verify_visible`, `ui_check`, `animation`, `particle`, and `multi_view`.

```python
await editor(action="select", path="/Player")
description = await screenshot(
    camera="scene_view_frame",
    describe="verify_visible",
)

custom = await screenshot(
    camera="scene_view",
    describe="Is the pause menu clipped at any edge?",
)
```

Description requires configured sampling. If sampling is disabled, unavailable,
or refuses the image, the tool degrades to the capture result. Use `raw=True`
when the caller needs the image path even if `describe` is also supplied.
`scene_view_frame` frames Unity's current selection; select the target first as
shown above. With `scene_view` or `scene_view_frame`, `path` is treated as an
output path for compatibility, so prefer the unambiguous `output_path`
parameter.

`annotation_id` switches to the annotation frame and frames a saved Region Tool
selection with that ID. Multi-object marks and chat annotation are covered in
[Screenshot annotation](../chat/annotation.md).

<span id="screenshot_baseline"></span>

## Save a baseline

`screenshot_baseline` captures the requested view and copies it to
`.claude/baselines/<name>.png`:

```python
baseline = await screenshot_baseline(
    name="main-menu-1280x720",
    camera="scene_view",
    width=1280,
    height=720,
)
```

Baseline creation writes a project-local file. Choose stable camera state,
resolution, scene state, and render settings; otherwise later comparisons can
measure capture drift rather than the change under test. Baseline names become
file names: they must be non-empty and cannot contain `/`, `\\`, or `..`.

<span id="screenshot_compare"></span>

## Compare with a baseline

Use the same capture settings as the baseline:

```python
result = await screenshot_compare(
    name="main-menu-1280x720",
    camera="scene_view",
    width=1280,
    height=720,
    mode="auto",
)
```

Modes have different evidence and cost:

| Mode | Behavior |
|---|---|
| `pixel` | Local pixel comparison only |
| `auto` | Pixel comparison, then configured analysis when escalation is useful |
| `structural` | General model-assisted composition analysis |
| `targeted` | Model-assisted answer to the required `question` |
| `ui_layout` | UI alignment and layout analysis |
| `animation` | Pose or frame-state analysis |
| `color` | Color and appearance analysis |
| `position` | Relative object-position analysis |
| `regression` | Model-assisted pass/fail check for removed, broken, or corrupted content |

```python
result = await screenshot_compare(
    name="hud",
    mode="targeted",
    question="Did the health bar move or change color?",
)
```

`pixel` is deterministic and local. Model-assisted modes require sampling and
consume its configured budget; treat their prose as supporting evidence. The
result can include a diff image and similarity data depending on mode.

## Reliable workflow

1. Put the scene and camera into a deterministic state.
2. Capture once with `raw=True` and inspect framing.
3. Save a versioned, clearly named baseline.
4. Recreate the same state after the change.
5. Run `pixel` for strict regression evidence or an appropriate assisted mode
   for semantic evidence.
6. Update the baseline only when the visual change is intentional and reviewed.

For UI authoring, combine this workflow with [UI linting](ui.md). For runtime
state setup and deterministic assertions, see the
[Playtest guide](../features/playtest.md).
