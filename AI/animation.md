# Feature: Animation Support

## Overview

The consolidated `animation` MCP tool reads, creates, edits, samples, and adds
events to AnimationClips in the Unity Editor. `AnimationSerializer` owns reads;
`AnimationHelper` owns asset mutations, preview sampling, and event editing.

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     animation tool              CommandRouter.MediaHandlers
                                                  ExecAnimationConsolidated
                                                         │
                                              ┌──────────┴──────────┐
                                              │                     │
                                    AnimationSerializer     AnimationHelper
                                    (read: clips → text)    (write/preview)
```

## Implementation Notes

### Data Storage
- Animation clips live in `.anim` asset files (created in `Assets/Animations/`)
- Curves stored in AnimationClip via EditorCurveBinding
- Keyframes hold (time, value) with interpolation info

### Constraints
- Editor-only: AnimationMode API (sampling, preview)
- Property names map to internal bindings: `localPosition` → `m_LocalPosition.x/.y/.z`
- Vector3 properties require 3 float curves (one per axis)
- Keyframe output limited to 50 per curve (shows `,...+N more` if exceeded)

### Edge Cases
- No Animator/Animation component: `get` returns `No animation clips`; actions
  that require an existing clip return `Clip not found`
- AnimationMode already active: check `InAnimationMode()` before starting
- Legacy Animation vs Animator: support both (check Animator first, then Animation)
- Invalid component_type (v0.57+): "Component type not found: {typeName}" — check ResolveComponentType logic (searches UnityEngine.*, custom assemblies)
- Custom component not found: `ResolveComponentType` checks exact names in
  loaded assemblies. Use the short name for a global-namespace component and
  the fully qualified name for a namespaced component.

### Action Dispatch

Edit operations are top-level `action` values: `add_key`, `remove_key`,
`remove_curve`, `set_keys`, `set_loop`, `set_wrap`, and `set_framerate`.
`action="edit"` remains an alias for adding keys. Dispatch lives in
`unity-plugin/Editor/CommandRouter.MediaHandlers.cs`.

## Code Locations

- Python tool: `server/src/unity_mcp/tools/animation.py` (animation, timeline, animator, particle)
- Serializer: `unity-plugin/Editor/AnimationSerializer.cs`
- Helper: `unity-plugin/Editor/AnimationHelper.cs`
- Commands: `unity-plugin/Editor/CommandRouter.MediaHandlers.cs`
- Python tests: `server/tests/test_server_animation.py`
- C# tests: `unity-plugin/Editor/Tests/SerializerTests.cs`

## MCP Tools

### `animation` (single consolidated tool)

**Parameters:** `action` and `path` are required. Optional fields are `clip`,
`clip_name`, `property`, `keys`, `time`, `component_type`, `binding_path`,
`tangent`, `function_name`, `int_param`, `float_param`, and `string_param`.
Use `resolve_tool_schema(name="animation")` for the current machine-readable
contract.

#### action=get — List clips or show details
```
# List all clips on object
animation(action="get", path="/Player")
→ Animator: Idle, Walk, Jump
  ---
  Idle | 1.0s | 3 curves
  Walk | 0.8s | 6 curves | loop

# Show clip detail (value@time format, comma-separated)
animation(action="get", path="/Player", clip="Walk")
→ clip: Walk | 0.8s | loop
  ---
  m_LocalPosition.x: 0@0,1.5@0.4,0@0.8

# Sample at time
animation(action="get", path="/Player", clip="Walk", time=0.4)
→ sample: Walk @ 0.40s
  ---
  m_LocalPosition.x: 1.5
  m_LocalPosition.y: 0.5
```

#### action=create — Create new AnimationClip
**Params:** `path`, `clip_name`, `property` (default="localPosition"), `keys` (keyframe string), `component_type` (optional, default="Transform")

Creates new AnimationClip with curves, saves to `Assets/Animations/{clip_name}.anim`, attaches to object's Animator.

Key format: `t:<time> v:<value>` separated by `;`
- Vector3: `t:0 v:(0,0,0); t:1 v:(0,2,0)`
- Float: `t:0 v:0; t:0.5 v:1; t:1 v:0`

**component_type** (v0.57+): Specifies the component containing the property. Defaults to Transform (for localPosition, rotation, scale). Other examples:
- `Light` — animate intensity, color, range
- `Camera` — animate fieldOfView, nearClipPlane, farClipPlane
- `Rigidbody` — animate mass, drag, velocity
- Custom components — full type name or short name if in Assembly-CSharp
- Error handling: non-existent component type returns "Component type not found" error

#### action=edit (or add_key|remove_key|remove_curve|set_keys|set_loop|set_wrap|set_framerate)
**Params:** `path`, `clip`, `action`, `property` (optional), `keys` (optional),
`component_type` (optional, default="Transform"), `binding_path` (optional), and
`tangent` (`auto`, `smooth`, `linear`, or `constant`)

Modify existing clip. Sub-actions passed as `action` value:
- `add_key` — insert keyframes (property + keys required); Color properties accept hex (`#FF0000`) as value
- `remove_key` — delete keyframe at time (property + `t:0.5` required)
- `remove_curve` — delete entire curve (property required)
- `set_keys` — replace all keyframes (property + keys required)
- `set_loop` — toggle clip looping (keys="false" to disable, anything else to enable)
- `set_wrap` — set clip wrap mode: `loop`, `once`, `pingpong`, `clamp` (clip required)
- `set_framerate` — set clip sample rate in frames per second (`keys="30"`)

`get_clip_path` is a separate top-level action and requires `path` and `clip`.

**component_type** (v0.57+): Required when editing non-Transform properties (e.g., Light.intensity). Must match the component type used when creating the clip. See create section for full list of examples.

#### action=preview — Sample in Edit Mode
**Params:** `path`, `clip`, `time` (optional, default=0.0)

Samples the clip at `time`, poses the object in `AnimationMode`, and returns the
evaluated curve values. The public dispatcher does not expose the helper's
internal `start` and `stop` branches as separate actions.

#### Animation events

- `get_events`: list events for `path` and `clip`.
- `add_event`: add an event at `time`; `function_name` is required and optional
  `int_param`, `float_param`, and `string_param` are forwarded.
- `remove_event`: remove the event at `time` from `path` and `clip`.

#### Example: Animate Custom Component (v0.57+)
```
# Animate Light intensity from 0.5 to 2.0 over 2 seconds
animation(
  action="create",
  path="/MyLight",
  clip_name="LightFade",
  property="intensity",
  component_type="Light",
  keys="t:0 v:0.5; t:2 v:2.0"
)
→ created: LightFade | 2.0s | 1 curves | saved: Assets/Animations/LightFade.anim

# Edit the clip: add keyframe at 1 second (midpoint)
animation(
  action="add_key",
  path="/MyLight",
  clip="LightFade",
  component_type="Light",
  property="intensity",
  keys="t:1 v:1.25"
)
→ edited: LightFade | add_key intensity
```

**Non-existent component error:**
```
animation(
  action="create",
  path="/Player",
  clip_name="BadClip",
  component_type="NonExistentComponent",
  property="someField"
)
→ [error] Component type not found: NonExistentComponent
```

## Tests

- `server/tests/test_server_animation.py` verifies wrapper arguments and actions.
- `unity-plugin/Editor/Tests/SerializerTests.cs` covers clip serialization and
  helper behavior.
- Use [`AI/testing.md`](testing.md) for current commands and acceptance policy.

## Review Checklist

- [x] Security: no path traversal (GameObject.Find validates), API calls safe
- [x] Performance: keyframe limit 50/curve prevents token bloat, no unnecessary sampling
- [x] Token efficiency: compact text format avoids repeated JSON structure
- [x] Edge cases: missing clips, existing AnimationMode state, and vector
  expansion are handled explicitly

## Related

- Tool: `animator_intent` — NL intent tool for animation (See `AI/intent-tools.md`)
- Consumer workflow: `unity-plugin/ClientSkills/skills/unity-animation/SKILL.md`
- Knowledge: [`AI/architecture.md`](architecture.md)
- Testing: [`AI/testing.md`](testing.md)

---

# UI Animation System (Editor Plugin)

Separate from the MCP tool above. Pure UI Toolkit animation for the plugin's own Editor windows — no AnimationClips, no MCP protocol. All driven by `ArcadeAnim.SmoothLoop` and USS class toggles.

## Core Primitives — `ArcadeAnim.cs`

Static utility class. All animation runs on `VisualElement.schedule` (no Update loop).

| Method | What it does |
|--------|-------------|
| `SmoothLoop(owner, animate, frameMs)` | 16 ms timer loop; epoch resets on reattach (no catch-up jump); auto-pauses on detach |
| `ControlledSmoothLoop(owner, animate)` | Returns a `MotionHandle`; stays paused until `SetActive(true)` |
| `MotionHandle.SetActive(bool)` | Start/stop a controlled loop |
| `AnimateClass(el, hidden, visible, delayMs)` | Add hidden class, swap to visible after delay |
| `FadeIn(el, delayMs)` | `arcade-fade-hidden` → `arcade-fade-visible` |
| `SlideInRight(el, delayMs)` | `arcade-slide-hidden` → `arcade-slide-visible` |
| `ShakeX(el)` | Delegates to `BiomeUI.ShakeX` |
| `PulseOnce(el)` | Adds `arcade-pulse`, removes after 400 ms |
| `FlashClass(el, cls, ms)` | Temporary class for `ms` ms |
| `GlowPulse(el, stateKey, intervalMs)` | Toggles `arcade-glow` + state class every interval |
| `CountUp(label, from, to, durationMs)` | Stepped numeric animation |
| `StaggerFadeIn(els, stepMs)` | Per-element staggered `FadeIn` |
| `Typewriter(label, text, msPerChar)` | Character-by-character text reveal |

`SmoothFrameMs = 16` (const).

## Particle System — `BiomeParticleBurst.cs`

Two classes in one file.

### `BiomeParticlePattern` enum
Eight patterns driving `BiomeAmbientParticles` motion math:
`DataFlow | Tools | Shield | Chat | Sampling | Updates | Ecosystem | Timeline`

### `BiomeParticleBurst`
Event-only burst. 12 pooled `VisualElement` particles; `DynamicTransform + DynamicColor` hints for GPU batching. No persistent tick.

```csharp
BiomeParticleBurst.Emit(host); // static — lazily creates/reuses burst on host
```

Particles fan out radially (360° / 12 per particle, variable radius 30–51 px), shrink and fade over ~300 ms.

### `BiomeAmbientParticles`
Persistent ambient field. 9 particles per pattern, each with an independent `MotionProfile` (seeded by `(int)pattern * 101 + i * 17` — deterministic, no drift between reattaches). Harmonic motion uses incommensurate frequencies to avoid visible looping.

```csharp
var particles = BiomeAmbientParticles.Attach(host, BiomeParticlePattern.Chat);
particles.SetState("up"); // drives conn-up/conn-listen/conn-down CSS class
```

`Attach` is idempotent — safe to call repeatedly. `entryBurst: true` (default) fires `BiomeParticleBurst.Emit` 220 ms after attach.

## Header Animations

Each header is a static `Build(scheduleHost)` factory returning a `VisualElement`. They share the pattern: build elements → `SmoothLoop` for per-frame math → separate `schedule.Execute(...).Every(N)` for state polling.

### Connection-State Headers (enhanced)

All use `BiomeUI.SetExclusiveClass` to enforce one-of-N state class.

| File | Elements | Poll interval | State source |
|------|----------|--------------|-------------|
| `ChatHeaderAnim.cs` | 3 arcs + dot + orbit ring + L/R lines | 600 ms | `ChatBackendProbe.IsChatBackendRunning()` |
| `HubHeaderAnim.cs` | hub geometry | — | MCP connection state |
| `PermissionsHeaderAnim.cs` | shield geometry | — | permission state |
| `SamplingHeaderAnim.cs` | waveform geometry | — | sampling state |
| `ToolsHeaderAnim.cs` | tool nodes | — | tools state |
| `UpdatesHeaderAnim.cs` | update geometry | — | update state |

`ChatHeaderAnim` also attaches `BiomeAmbientParticles` with `BiomeParticlePattern.Chat`.

### `StatusAmbientAnim.cs` (enhanced)
Absolute-positioned background layer for the status panel. Three animated layers:
- **Scanline** — sweeps top-to-bottom on a 2.8 s period
- **Sonar ring** — expands and fades on a 2.2 s period
- **4×4 dot grid** — each dot breathes with an offset harmonic

Polls `ArcadePalette.StateClass` every 700 ms to sync grid color.

### `EcosystemHeaderAnim.cs` (new)

Shared semantic header for multi-item module and version-history contexts. 7 nodes in a horizontal row flanked by connector lines.

```csharp
// Plugin settings — nodes reflect active plugin count
var header = EcosystemHeaderAnim.BuildPlugins();

// Version history — nodes scan left-right
var header = EcosystemHeaderAnim.BuildVersions();

// Sync selected node to current version index
EcosystemHeaderAnim.SetVersionIndex(root, index, total);
```

`BuildPlugins` polls `PluginRegistry.All.Count(p => p.HasSettingsUI)` every 900 ms and activates matching nodes. The animation pulse travels node-to-node using `Mathf.Cos` sweep. `SetVersionIndex` normalizes the index to [0, 6] and calls `ArcadeAnim.PulseOnce` on the selected node.

## Wizard Animations — `Wizard/WizardAmbientAnim.cs` (new)

Contains two `VisualElement` subclasses (not static factories).

### `WizardJourneyAnim`
Step progress bar for the setup wizard. 4 nodes + connecting segments + animated `_packet` element that travels along the route.

```csharp
var journey = new WizardJourneyAnim();
journey.SetStep(currentStep, totalScreens); // updates node states + packet position
```

Node states: `wiz-journey__node--complete`, `--active`, `--pending`. Packet drifts with dual-harmonic noise (`Sin(t * 0.83)` + `Sin(t * 1.71)`). Also attaches `BiomeAmbientParticles` with `BiomeParticlePattern.Ecosystem`.

### `SkillsInstallAnim`
Three module nodes (Skills / Agents / Scripts) with a bouncing packet. Speed doubles when `SetWorking(true)`.

```csharp
var anim = new SkillsInstallAnim();
anim.SetWorking(true);  // packet moves 2× faster, larger pulse
```

Uses `BiomeParticlePattern.Tools`.

## `LevelUpAnimator.cs` (enhanced)

Two entry points:

```csharp
// Idle state — looping lift arrow in the Updates tab badge
VisualElement signal = LevelUpAnimator.BuildIdleSignal();

// Full update sequence — XP bar + sparks + version label
VisualElement panel = LevelUpAnimator.Build(host, fromVersion, toVersion, onComplete);
```

`Build` runs a single `SmoothLoop` for 1480 ms (`DurationMs`). Stages:
1. XP bar fills via `SmoothStep` scale on X axis
2. Version label flashes (`lvlup-version-flash`) at 38% progress
3. 5 sparks orbit with per-spark frequency variation
4. Lift symbol (arrow + inner/outer aura) rises with shimmer
5. `onComplete` fires exactly once via `CompleteOnce()` guard

Test hook: `LevelUpAnimator.SimulateCompletion()` — only compiled with `UNITY_INCLUDE_TESTS`.

## USS Files Changed

| File | What changed |
|------|-------------|
| `ArcadeAnim.uss` | Base animation classes (`arcade-fade-*`, `arcade-slide-*`, `arcade-pulse`, `arcade-glow`) |
| `LevelUpAnim.uss` | Spark and XP-fill styles; idle signal styles |
| `MCPHub.uss` | Hub header geometry styles |
| `MCPStatus.uss` | Status ambient layer styles (scanline, grid, sonar) |
| `SetupWizard.uss` | Wizard journey + skills installer styles |

## File Locations

```
unity-plugin/Editor/
  ArcadeAnim.cs                    # Core primitives
  BiomeParticleBurst.cs            # BiomeParticleBurst + BiomeAmbientParticles + enum
  ChatHeaderAnim.cs                # Chat connection header
  HubHeaderAnim.cs
  PermissionsHeaderAnim.cs
  SamplingHeaderAnim.cs
  StatusAmbientAnim.cs
  ToolsHeaderAnim.cs
  EcosystemHeaderAnim.cs           # NEW — plugin/version header
  Updates/LevelUpAnimator.cs       # Enhanced
  Wizard/WizardAmbientAnim.cs      # NEW — WizardJourneyAnim + SkillsInstallAnim
  ArcadeAnim.uss
  MCPHub.uss
  MCPStatus.uss
  Updates/LevelUpAnim.uss
  Wizard/SetupWizard.uss
```

## Key Patterns

- **Attach-aware loops:** `SmoothLoop` always pauses on `DetachFromPanelEvent`, resets epoch on `AttachToPanelEvent`. No stale timers after panel removal.
- **`UsageHints`:** All animated elements set `DynamicTransform` and/or `DynamicColor` before first paint — never after — to avoid re-layout.
- **State polling vs animation:** State (connection, plugin count) is polled on a coarser interval (600–900 ms). Per-frame math only drives visual parameters, never reads Unity Editor state.
- **No Coroutines, no Update:** Everything uses `VisualElement.schedule`. Safe in Editor windows.
- **CSS-only transitions:** Discrete state changes (fade, slide, pulse) go through class toggles + USS `transition`. Continuous motion (scale, translate, opacity) is written inline per frame.
