# Editor UI engineering standard

This is the repository standard for UI Toolkit surfaces in Unity Biome MCP,
including Settings, Setup Wizard, diagnostics, plugin settings, and chat.

## Visual direction

- Use Unity's active Editor theme. Do not add a separate Biome light/dark theme.
- Use `--unity-colors-*` variables for backgrounds, text, fields, and neutral borders.
- Reserve Biome accents for meaning: green for ready/success, amber for waiting
  or attention, and red/pink for errors. Do not tint whole panels.
- Keep operational screens compact. Decorative motion must not displace fields,
  resize lists, or compete with the current action.
- Keep card radius at 6px or less. Use cards for repeated selectable items, not
  as wrappers around entire page sections.

## Structure

- Put static presentation in USS. Inline styles are for data-driven values such
  as progress, runtime visibility, or a measured transform.
- Use UXML for stable, reusable shells when the visual tree is mostly static.
  Build catalog-driven rows and plugin-provided content in C#.
- Use BEM-like class names: `component`, `component__part`, and
  `component--state`.
- Load shared styles through `BiomeUI.LoadCoreStyles`.
- Reuse `BiomeUI`, `BiomeToggleGroup`, `WizardUI`, `PluginUIHelpers`, and
  `IconCanvas` before adding a local helper.

## Controls and navigation

- Use native `Button`, `Toggle`, `DropdownField`, `TextField`, and `ListView`
  controls so keyboard, focus, disabled, and validation states remain intact.
- A command or selectable card must be a focusable control, not a clickable
  plain `VisualElement`.
- Keep command buttons at least 24px high; Wizard actions are at least 32px.
- Expose one primary action per state. Disable Continue or Finish until its
  prerequisite succeeds; provide an explicit Skip only when skipping is valid.
- A page that can exceed its minimum window height owns a `ScrollView`. Use
  `ListView` virtualization for large flat datasets and lazy disclosure for
  grouped content when virtualization would destroy the hierarchy.
- Preserve a visible focus state and logical tab order.
- Icon-only commands require a familiar icon, tooltip, and stable control name.
- Pair status color with text or an icon; color is never the only distinction.

## Motion and lifecycle

- UI Toolkit supports USS transitions, not web CSS `@keyframes` or
  `animation-name`.
- Transition endpoints use matching units. Prefer `translate`, `scale`,
  `rotate`, and `opacity` over layout-property animation.
- Set `UsageHints.DynamicTransform` before an animated element joins a panel.
- Schedule recurring work on the detachable element that owns it. A closed or
  detached panel is idle: cancel work, stop child processes, unsubscribe events,
  and guard delayed callbacks with `panel != null`.
- Poll state at 500–1000ms unless the data requires faster updates. Keep data
  polling separate from motion and update USS classes only on state changes.
- `BiomeParticleBurst` is pooled and reserved for meaningful milestones.
  `BiomeAmbientParticles` uses `ArcadeAnim.SmoothLoop`; its loop pauses on detach
  and resets its time origin on attach.
- Working-state effects use `ArcadeAnim.ControlledSmoothLoop` and deactivate on
  idle, cancellation, failure, and shutdown.
- A 16ms loop is allowed only for a small, fixed transform-only pool on the
  visible surface. It performs no allocations, LINQ, tree queries, element
  creation, random generation, or layout mutation.
- Do not combine per-frame writes and USS transitions on the same properties.
- UI Toolkit has no portable USS blur filter. Use one or two larger translucent
  aura elements instead of generated textures or a blur shader.
- Nonessential motion has one project-level off switch. The static state must
  communicate the same status and action, and tests cover the disabled path.

## State and feedback

- Never silently revert invalid input. Restore the valid value and show a
  compact status message; motion is secondary feedback.
- Async commands have explicit idle, running, success, and error states.
- Subscribe to real completion events instead of guessing with a fixed delay.
- Unsubscribe window-local events on `DetachFromPanelEvent` or `OnDisable`.
  Prefer named callbacks when a root can be rebuilt.
- Presets update existing controls with `SetValueWithoutNotify`; do not rebuild
  the page and lose search, scroll, disclosure, or focus state.

## Verification

- Add EditMode tests for visual-tree contracts, focusability, preset refresh,
  state gating, motion-disabled behavior, and navigation lifecycle.
- Keep `server/tests/test_editor_ui_styles.py` green; it guards unsupported USS
  animation syntax and stylesheet-class drift.
- Verify Settings at narrow and normal widths and Wizard at its declared minimum.
- Check both Unity Editor skins through the native theme.
- Verify every fast loop stops on detach and every working-only loop is idle
  when no operation is running.
