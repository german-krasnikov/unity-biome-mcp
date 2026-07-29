# UI Toolkit Best Practices

This guide is the project standard for editor UI in Unity Biome MCP. It applies to
Settings, Setup Wizard, diagnostics, plugin settings, and future editor windows.

## Visual Direction

- Use Unity's active editor theme. Do not add a Biome light/dark theme switch or
  maintain separate application themes.
- Use `--unity-colors-*` variables for window backgrounds, text, fields, and
  neutral borders.
- Reserve Biome arcade accents for meaning: green for ready/success, amber for
  waiting or attention, and red/pink for errors. Do not tint whole panels.
- Keep operational screens compact and scannable. Decorative motion must never
  displace fields, resize lists, or compete with the current action.
- Keep card radius at 6px or less. Use cards for repeated selectable items, not
  as wrappers around whole page sections.

## Structure

- Put static presentation in USS. Inline styles are only for data-driven values
  such as progress, runtime visibility, or a measured transform.
- Use UXML for stable, reusable window shells when the visual tree is mostly
  static. Build catalog-driven rows and plugin-provided content in C#.
- Use BEM-like class names for new components: `component`, `component__part`,
  and `component--state`.
- Load the shared styles through `BiomeUI.LoadCoreStyles`. Do not repeat
  `AssetDatabase.LoadAssetAtPath` sequences in each window.
- Build controls through the shared utilities before adding a local helper:
  `BiomeUI`, `BiomeToggleGroup`, `WizardUI`, `PluginUIHelpers`, and `IconCanvas`.

## Controls And Navigation

- Use native `Button`, `Toggle`, `DropdownField`, `TextField`, and `ListView`
  controls so keyboard, focus, disabled, and validation states remain intact.
- Do not make a clickable `VisualElement` for a command or selection. A card
  that can be selected must be a focusable `Button`.
- Keep command buttons at least 24px high; Wizard actions are at least 32px.
- Expose one primary action per state. Disable Continue/Finish until the required
  operation succeeds, and provide an explicit skip action when skipping is valid.
- Every page that can exceed the minimum window height must own a `ScrollView`.
  Use `ListView` virtualization for large flat repeated datasets. Grouped lists
  may use lazy disclosures when virtualization would destroy hierarchy.
- Preserve a visible keyboard focus state and a logical tab order. Do not remove
  native focus styling without replacing it with an equally clear state.
- Use familiar icon buttons for icon-only commands and always set a tooltip and
  stable control name. Where a text label fits, keep it visible; a tooltip alone
  is not an accessible name.
- Pair color-coded status with text or a familiar icon. Color must not be the only
  distinction between ready, working, warning, and error states.

## Motion

- UI Toolkit supports USS transitions; it does not support web CSS
  `@keyframes`/`animation-name`. Use short class transitions or scheduled
  transform steps.
- Transition endpoints must use matching units, for example `100%` to `0%`, not
  `100%` to `0px`.
- Prefer `translate`, `scale`, `rotate`, and `opacity`. Avoid repeatedly changing
  width, height, margins, border width, or absolute positions.
- Set `UsageHints.DynamicTransform` before an animated element joins a panel.
- Schedule recurring work on the animated element itself. Its scheduler pauses
  when the element is detached; never host a page animation on a permanent root.
- A closed or detached panel must be idle: cancel discovery tasks, stop child
  processes, unsubscribe events, and guard delayed callbacks with `panel != null`.
- Poll state at 500-1000ms unless the underlying data requires faster updates.
  Keep data polling separate from visual motion.
- Change USS state classes only when the state actually changes.
- Motion must explain state: connection flow, enabled tool ratio, permission
  policy, update checking, or selected version. Organic motion should use
  deterministic, independent harmonic profiles rather than per-frame RNG.
- Event particles use the pooled `BiomeParticleBurst` control and are reserved
  for meaningful milestones or page entry. Header ambience uses the fixed
  `BiomeAmbientParticles` pool and `ArcadeAnim.SmoothLoop`. The loop must belong
  to the detachable header element, pause explicitly on detach, and restart its
  time origin on attach.
- Working-state effects use `ArcadeAnim.ControlledSmoothLoop`. Activate it when
  work starts and deactivate it on every idle, cancellation, failure, and window
  shutdown path. It must remain paused while the panel is attached but idle.
- A smooth loop may run at 16ms only for a small, fixed pool of transform-only
  elements on the currently visible surface. Do not query the visual tree,
  allocate collections, use LINQ, create elements, or mutate layout in that loop.
- Do not combine per-frame transform/opacity writes with USS transitions for the
  same properties. It creates visible lag because the transition continually
  chases a moving target. Keep USS transitions for infrequent color/state changes.
- UI Toolkit has no portable USS blur filter. Build a small aura from one or two
  larger translucent elements behind the core instead of a blur shader or
  repeatedly generated texture.
- Nonessential motion must have one project-level off switch, not separate
  per-window settings. When motion is disabled, skip scheduled loops and render
  the final state immediately.
- A static frame must communicate the same status and action as the animated
  state. Tests must cover the motion-disabled path and verify that it schedules
  no recurring work.

## State And Feedback

- Never silently revert invalid input. Restore the valid value, show a compact
  status message, and use a brief shake only as secondary feedback.
- Async commands have explicit idle, running, success, and error states.
- Subscribe to real completion events. Do not use a fixed delay to guess when an
  external operation has finished.
- Unsubscribe window-local events on `DetachFromPanelEvent` or `OnDisable`.
  Prefer named callbacks when a root can be rebuilt; anonymous callbacks cannot
  be explicitly unregistered.
- Presets must refresh existing controls with `SetValueWithoutNotify`; rebuilding
  the entire page loses search, scroll, and disclosure state.

## Performance Checklist

- No scheduler faster than 160ms except a lifecycle-bound, fixed-size
  transform-only `SmoothLoop`.
- No recurring layout-property animation.
- No allocations, visual-tree queries, or per-frame random generation in a
  smooth animation callback.
- Every fast loop pauses on `DetachFromPanelEvent`; repeated builders and particle
  attachment must be idempotent and must not register duplicate callbacks.
- A working-only loop performs zero ticks in idle. Particle counts are fixed and
  particles are reused; never create or destroy particle elements per frame.
- No synchronous process discovery while constructing a visual tree.
- No repeated full-catalog reads inside a per-row loop.
- No internal queries into Unity control implementation trees such as
  `foldout.Q<Toggle>()`.
- Large visual lists use virtualization or collapsed/lazy content.

## Verification

- Add EditMode tests for visual-tree contracts, keyboard focusability, preset
  refresh, state gating, and navigation lifecycle.
- Keep `server/tests/test_editor_ui_styles.py` green. It guards unsupported USS
  animation syntax and settings classes that have no stylesheet rule.
- Verify Settings at narrow and normal editor widths, and verify Wizard at its
  declared minimum size.
- Check both Unity editor skins through the native theme only; do not introduce
  a separate Biome theme implementation.
