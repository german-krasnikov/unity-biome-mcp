# Screenshot & Visual Diff Tools

Capture game view, save visual baselines, and compare screenshots for regression testing. Use these tools for visual verification and automated visual testing.

## screenshot

Capture the current game view as a PNG image with optional object annotations and AI description.

**Parameters:**
- `width` (int, default=640) — Image width in pixels
- `height` (int, default=480) — Image height in pixels
- `camera` (string, optional) — Camera preset: "scene_view" | "scene_view_frame" | "multi_view" | "single_view" | "overview" | "overview_game" | custom camera name
- `path` (string, optional) — Scene path to target object (for framing)
- `output_path` (string, optional) — Save path (default: auto-generated in Screenshots/)
- `describe` (string, optional) — AI description mode: "haiku" (Haiku model, 15-100x token reduction)
- `raw` (bool, default=false) — Force path output instead of description
- `zoom` (float, optional) — Zoom level (higher = closer)
- `angles` (string, optional) — Per-view rotation (Euler angles): "ex,ey,ez|..." (use "_" to skip)
- `supersample` (int, optional) — Antialiasing level (1-4)
- `offset` (string, optional) — Framing offset (x,y)
- `fixed_size` (float, optional) — Fixed framing size
- `highlight` (string, optional) — Object paths to highlight with bounding box: "path1:path2:#RRGGBB"
- `show_colliders` (bool, optional) — Overlay collider wireframes
- `angle` (string, optional) — Camera angle for single_view: "front" | "left" | "top" | "iso" | "ex,ey,ez"
- `annotation_id` (string, optional) — Draw object path labels (auto sets camera=annotation_frame)

**Camera Presets:**

| Camera | Purpose | Use Case |
|--------|---------|----------|
| scene_view | Standard editor scene view | General screenshots |
| scene_view_frame | Frame around target | Focused object capture |
| multi_view | 4-view layout (top/front/left/perspective) | Debugging spatial issues |
| single_view | Single perspective camera | Player POV |
| overview | Top-down orthographic | Level layout overview |
| overview_game | Game view top-down | Build playable perspective |

**Output:** File saved to `unity-test-project/ScreenShots/YYYY-MM-DD_HH-MM-SS.png` with optional AI description appended.

**Example:**

```python
# Basic screenshot
img = await screenshot()

# Custom size and camera
img = await screenshot(width=1280, height=720, camera="scene_view")

# Multi-view for debugging
img = await screenshot(camera="multi_view", zoom=1.5)

# With object highlighting (bounding box)
img = await screenshot(highlight="Player:Enemy:#FF0000", 
                      camera="scene_view")

# Show colliders
img = await screenshot(camera="scene_view", show_colliders=True)

# Haiku AI description (token-efficient)
desc = await screenshot(describe="haiku", camera="scene_view")
# → "[AI analysis] Player standing at position (0,5,0), health UI visible..."
# → "[img:/path/to/screenshot.png]"

# With annotation ID (labels on objects)
img = await screenshot(annotation_id="Player", camera="annotation_frame")

# Single-view with specific angle
img = await screenshot(camera="single_view", angle="front", width=800, height=600)
```

**Use Cases:**
- Visual regression testing (compare with baseline)
- Playtest documentation
- Debugging spatial/rendering issues
- CI/CD visual verification

---

## screenshot_baseline

Save current screenshot as reference for visual regression testing.

**Parameters:**
- `name` (string, default="default") — Baseline identifier
- `width` (int, default=640) — Image width
- `height` (int, default=480) — Image height
- `camera` (string, optional) — Camera preset (same as screenshot)

**Output:** Baseline saved to `.claude/baselines/{name}.png`

**Example:**

```python
# Save main menu baseline
baseline = await screenshot_baseline(name="main_menu", camera="scene_view")

# Save gameplay baseline at specific resolution
baseline = await screenshot_baseline(name="combat_start", 
                                    width=1920, height=1080,
                                    camera="overview")
```

**Use Cases:**
- Establish visual reference before changes
- Create regression testing suite
- Document expected visual state

---

## screenshot_compare

Compare current screenshot with saved baseline. Highlights differences and calculates similarity.

**Parameters:**
- `name` (string, default="default") — Baseline identifier
- `width` (int, default=640) — Image width
- `height` (int, default=480) — Image height
- `camera` (string, optional) — Camera preset (same as screenshot)
- `mode` (string, default="auto") — Comparison algorithm: "auto" | "pixel" | "structural" | "targeted" | "ui_layout" | "animation" | "color" | "position"
- `question` (string, optional) — Custom question for "targeted" mode (e.g., "Did the health bar change?")

**Comparison Modes:**

| Mode | Detects | Cost | Use Case |
|------|---------|------|----------|
| auto | Pixel diffs → structural escalation | ~$0.002 | Default, comprehensive |
| pixel | Direct pixel comparison | ~$0.0 | Quick pixel-perfect tests |
| structural | Haiku general layout analysis | ~$0.005 | Layout/composition changes |
| targeted | Answer specific question | ~$0.01 | "Did button move?" |
| ui_layout | UI element positioning | ~$0.005 | HUD layout changes |
| animation | Motion/frame differences | ~$0.005 | Animation state verification |
| color | Color/appearance changes | ~$0.003 | Color/material changes |
| position | Object position differences | ~$0.003 | Spatial/transform changes |

**Output:** Comparison report with diff image and similarity percentage.

**Example:**

```python
# Setup: save baseline
await screenshot_baseline(name="level_1", camera="overview")

# Later: verify unchanged
result = await screenshot_compare(name="level_1", camera="overview")
# → "Similarity: 99.8% (5 pixels different)"

# Detect layout changes
result = await screenshot_compare(name="main_menu", mode="ui_layout")

# Targeted question
result = await screenshot_compare(name="hud", mode="targeted",
                                 question="Did the health bar color change?")

# Color detection
result = await screenshot_compare(name="environment", mode="color")

# Auto mode (starts with pixel, escalates if needed)
result = await screenshot_compare(name="gameplay", mode="auto")
```

**Workflow:**

1. **Baseline Setup** (first run)
   ```python
   await screenshot_baseline(name="gameplay", camera="overview")
   ```

2. **Make Changes**
   - Modify game state, UI, objects, etc.

3. **Verify Changes**
   ```python
   result = await screenshot_compare(name="gameplay", mode="auto")
   # → Report differences
   ```

4. **Update Baseline** (when intended)
   ```python
   await screenshot_baseline(name="gameplay", camera="overview")
   ```

**Use Cases:**
- Automated visual regression testing
- Verify UI doesn't shift unexpectedly
- Detect animation state changes
- Validate color/material updates
- CI/CD visual quality gates

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Capture scene | screenshot() | `await screenshot(camera="scene_view")` |
| Capture with colliders | screenshot() | `await screenshot(show_colliders=True)` |
| Multi-view for debugging | screenshot() | `await screenshot(camera="multi_view", zoom=1.5)` |
| Highlight objects | screenshot() | `await screenshot(highlight="Player:Enemy:#FF0000")` |
| Get AI description | screenshot() | `await screenshot(describe="haiku")` |
| Save baseline | screenshot_baseline() | `await screenshot_baseline(name="gameplay")` |
| Compare with baseline | screenshot_compare() | `await screenshot_compare(name="gameplay", mode="auto")` |
| Detect color changes | screenshot_compare() | `await screenshot_compare(name="hud", mode="color")` |
| Verify layout | screenshot_compare() | `await screenshot_compare(name="menu", mode="ui_layout")` |
| Custom diff question | screenshot_compare() | `await screenshot_compare(name="x", mode="targeted", question="Did button move?")` |

---

**See also:** [Scene Tools](scene.md) for screenshot variants and editor control, [UI Tools](ui.md) for object positioning verification via `get_spatial_context`.
