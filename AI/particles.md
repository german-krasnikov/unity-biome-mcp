# Feature: Particle System Management

## Overview

The consolidated `particle` MCP tool reads, creates, edits, applies presets to,
and controls ParticleSystem components. It supports the documented modules and the
built-in `fire`, `smoke`, `sparks`, `rain`, `snow`, `explosion`, `magic`, `dust`,
`blood`, and `trail` presets. `ParticleSerializer` owns reads; `ParticleHelper`
owns mutations and playback.

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     particle tool               CommandRouter.MediaHandlers
                                                 ExecParticleConsolidated
                                                         │
                                              ┌──────────┴──────────┐
                                              │                     │
                                        ParticleSystem         ParticleSystem
                                        Serializer (read)      Helper (write/CRUD)
```

## Tool Actions

| Action | Description |
|--------|-------------|
| `get` | Read particle system: overview all modules or specific module detail |
| `create` | Create an empty or preset ParticleSystem (`path` required by the public schema; `name` and `preset` optional) |
| `set` | Set single module property (module, prop, value required) |
| `apply` | Apply preset to existing ParticleSystem (overrides current settings) |
| `play` | Start particle system playback (calls `ParticleSystem.Play()`) |
| `stop` | Stop particle system playback (calls `ParticleSystem.Stop()`) |
| `pause` | Pause particle system playback (calls `ParticleSystem.Pause()`) |

## Modules

| Module | Type | Readable | Writable | Use Case |
|--------|------|----------|----------|----------|
| `main` | Main settings | ✓ | ✓ | Duration, loop, start speed/size/color/rotation |
| `emission` | Emission | ✓ | ✓ | Rate over time/distance, burst events |
| `shape` | Shape | ✓ | ✓ | Sphere/box/cone/circle emission area |
| `colorOverLifetime` | Gradient | ✓ | ✓ | Color fade/transition over lifetime |
| `sizeOverLifetime` | Size curve | ✓ | ✓ | Size scaling over lifetime |
| `velocityOverLifetime` | Velocity | ✓ | ✓ | Speed/direction over lifetime |
| `noise` | Noise | ✓ | ✓ | Turbulence, wind, randomness |
| `renderer` | Rendering | ✓ | ✓ | Material, render mode, order |
| `trails` | Trails | ✓ | ✓ | Particle trails — 9 settable props: lifetime, material, textureMode, colorOverLifetime, widthOverTrail, minVertexDistance, dieWithParticles, ratio, sizeAffectsWidth |
| `collision` | Physics | ✓ | ✓ | Bounce, lifetime on hit |
| `rotationOverLifetime` | Rotation | ✓ | ✓ | Angular velocity curves |

## Presets

| Preset | Main | Emission | Shape | Color | Size | Use Case |
|--------|------|----------|-------|-------|------|----------|
| `fire` | loop, default | 40/s rate | cone | orange→red→black fade | grow+shrink | Fire, explosions |
| `smoke` | loop, default | 15/s rate | cone | gray fade → transparent | slow grow | Smoke, fog |
| `sparks` | !loop, 0.5s | burst 30-60 | cone | yellow→orange fade | tiny shrink | Sparks, magic |
| `rain` | loop, default | 500/s rate | box | default (white) | 3D stretch | Rain, weather |
| `snow` | loop, default | 100/s rate | box | white | default | Snow, blizzard |
| `explosion` | !loop, 0.5s | burst 50-100 | sphere | white→orange→dark fade | fast shrink | Explosions |
| `magic` | loop, default | 30/s rate | sphere | blue→purple fade | pulse | Magic spells |
| `dust` | loop, default | 5/s rate | box | brown fade in/out | default | Dust clouds |
| `blood` | !loop, 0.3s | burst 20-40 | cone | red→dark fade | grow | Blood splatter |
| `trail` | loop, default | 10/distance | shape disabled | cyan→fade | default | Trails, streaks |

## Key Implementation Details

### Create
- When `path` does not resolve, it is interpreted as the desired new object path.
  When it resolves, it is used as the parent and `name` names the new object.
- If preset provided, applies all preset values immediately
- Returns the created object's path
- Empty ParticleSystem has sensible defaults (duration=5, loop=true, 10 particles/s)

### Get
- Without `module` parameter: returns every supported module in overview format
  (one-line summary each)
- With `module` parameter: returns detailed properties for that module
- Read-only, no state changes
- Handles missing ParticleSystem gracefully (error message with suggestion)

### Set
- Single property mutation: `module`, `prop`, `value` all required
- Only works on existing ParticleSystem
- Auto-records Undo before modification
- Returns updated module state for verification

### Apply
- Updates the modules and properties defined by the selected preset
- Works on existing ParticleSystem only
- Enables modules used by that preset as needed
- Does not reset unrelated modules first; values from an earlier configuration
  may remain unless the preset explicitly overwrites them
- All changes recorded with single Undo action
- Returns a concise preset-applied confirmation

## Text Output Format

```
ParticleSystem on '/FX'
main: duration=3 loop=true startLifetime=0.5..1.5 startSpeed=1..3 startSize=0.3..0.8 maxParticles=200
emission: enabled rateOverTime=40
shape: enabled type=Cone angle=15 radius=0.3
colorOverLifetime: enabled
sizeOverLifetime: enabled
velocityOverLifetime: disabled
noise: enabled
trails: disabled
collision: disabled
rotationOverLifetime: disabled
renderer: Billboard
```

## Files

| File | Role |
|------|------|
| `unity-plugin/Editor/ParticleSerializer.cs` | ParticleSystem text serialization |
| `unity-plugin/Editor/ParticleHelper.cs` | Creation, property edits, and playback |
| `unity-plugin/Editor/ParticleHelper.Presets.cs` | Built-in preset definitions |
| `unity-plugin/Editor/CommandRouter.MediaHandlers.cs` | C# action dispatch |
| `server/src/unity_mcp/tools/animation.py` | Public `particle` wrapper |

## Tests

- `server/tests/test_server_particle.py` verifies the Python wrapper contract.
- `unity-plugin/Editor/Tests/SerializerTests.cs` covers particle serialization.
- `unity-plugin/Editor/Tests/BiomeParticleBurstTests.cs` covers the separate Editor UI particle system below.

Use [`AI/testing.md`](testing.md) for current commands, ownership, and acceptance
criteria rather than copying volatile test counts here.

## Error Handling

- Missing GameObject → "ParticleSystem not found at path, create one first"
- Missing ParticleSystem component → "Add ParticleSystem component to path"
- Invalid module name → "Unknown module: {name}"
- Invalid preset → "Unknown preset: {name}"
- Read-only `get` → always succeeds (or friendly not-found message)

## Notes for Agents

### Senior Developer
- ParticleHelper uses explicit switch statements for module property dispatch
- Presets stored as static methods within ParticleHelper class
- Create uses Undo.RegisterCreatedObjectUndo(), Set/Apply use Undo.RecordObject()
- A preset application uses one Undo record for the ParticleSystem

### Architect
- Particle system is read-lightweight (inspecting costs nothing)
- Presets enable quick scene setup (fire effect in 1 call vs 10+ manual sets)
- Module structure mirrors Unity ParticleSystem API hierarchy

### Code Reviewer
- Check ParticleSerializer handles all 11 modules consistently
- Verify preset changes remain explicit; do not assume unrelated modules are
  disabled or reset
- Confirm presets have sensible defaults (visible, useful, not extreme)
- Ensure text output format matches architecture.md for consistency

## Related

- Tool: `vfx_intent` — NL intent tool for visual effects (See `AI/intent-tools.md`)
- Architecture: `AI/architecture.md` — System-wide structure
- Consumer workflow: `unity-plugin/ClientSkills/skills/unity-particles-vfx/SKILL.md`

---

## Editor UI Particle System (BiomeParticleBurst.cs)

**Not an MCP tool.** Pure editor-side UI Toolkit particle effects for the plugin's own UI (Hub, Settings headers, Chat, Updates, etc.).

### Classes

| Class | Purpose | Count |
|-------|---------|-------|
| `BiomeParticleBurst` | One-shot burst on demand (celebrations, wizard completion, level-up) | 12 particles |
| `BiomeAmbientParticles` | Continuous ambient field with harmonic motion, pauses on detach | 9 particles |
| `BiomeParticlePattern` | Enum (8 values) — selects motion profile for ambient field | — |

### BiomeParticleBurst

- `BiomeParticleBurst.Emit(host)` — attach + fire. Pooled: second call reuses the same element, just re-plays.
- Particles: 12 `VisualElement`s fanned 360°, radius 30–51 px. CSS classes: `--accent`, `--success`, `--warning` (cycling mod-3).
- Animation: inline style transitions (`translate`, `rotate`, `scale`, `opacity`) via `schedule.Execute`. No permanent update loop.
- Generation counter prevents stale callbacks from a prior burst.

### BiomeAmbientParticles

- `BiomeAmbientParticles.Attach(host, pattern, entryBurst=true)` — pooled attach. Returns existing instance on repeat calls.
- Motion: incommensurate harmonics (sin/cos combinations) per-particle, seeded from pattern index. Driven by `ArcadeAnim.SmoothLoop`.
- `SetState("up"|"listen"|"down")` — toggles `conn-*` CSS class (connection status visual cue).
- Entry burst: schedules `BiomeParticleBurst.Emit` 220 ms after first `AttachToPanelEvent`.

### Patterns (BiomeParticlePattern)

| Pattern | Motion override |
|---------|----------------|
| `DataFlow` | Horizontal bias, compressed Y |
| `Tools` | Vertical ripple, scale pulse |
| `Shield` | Orbital paths around center |
| `Chat` | Smaller orbits, faster spin |
| `Sampling` | Narrow X, vertical wave |
| `Updates` | Compressed X, slow drift |
| `Ecosystem` | Wide X, gentle vertical wave |
| `Timeline` | Horizontal sweep, flat Y |

### Files

| File | Role |
|------|------|
| `unity-plugin/Editor/BiomeParticleBurst.cs` | Both classes and `BiomeParticlePattern` enum |
| `unity-plugin/Editor/Tests/BiomeParticleBurstTests.cs` | Pooling, particle-count, and CSS-class tests |

### Key Design Constraints

- `pickingMode = PickingMode.Ignore` on all elements — never blocks UI interaction.
- `UsageHints.DynamicTransform | DynamicColor` per particle for GPU-friendly style updates.
- `UsageHints.GroupTransform` on container for batch transform.
- No `Update()` loop. Burst: one-shot schedule. Ambient: `ArcadeAnim.SmoothLoop` (pauses when detached).
