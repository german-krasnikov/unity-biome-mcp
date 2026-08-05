---
name: unity-materials-shaders
description: Use when creating, inspecting, or changing materials, shaders, Shader Graphs, shader properties, keywords, or render-material diagnostics.
---

# Unity Materials And Shaders

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Enable `ASSETS` for
material assets and shader authoring. Enable `MEDIA` only for gated rendering
diagnostics required by the task.

## Choose The Surface

| Goal | Tool |
|---|---|
| Quick scene-object color and shader | `set_material` |
| Material assets, slots, properties, copies | `material` |
| Shader source or Shader Graph | `shader` |
| Scene-wide material and texture health | `material_audit` |
| Rendering analysis | `render_analyze` |

## Examples

```text
batch(
  commands="""
material action=create path=Assets/Materials/Marker.mat shader="Universal Render Pipeline/Lit"
material action=set path=Assets/Materials/Marker.mat prop=_BaseColor value=#39E6A3
material action=get path=Assets/Materials/Marker.mat
""",
  on_error="stop"
)
```

```text
shader(
  action="create",
  path="Assets/Shaders/BiomePulse.shader",
  preset="unlit",
  shader_name="Biome/Pulse"
)
shader(action="get", path="Assets/Shaders/BiomePulse.shader")
```

## Rules

- Inspect available material properties before setting unfamiliar names.
- Distinguish shared, instance, and asset mutation explicitly.
- Keep Shader Graph node and edge edits small and verify graph structure after
  each logical group.
- Save node layout with `graph_get_layout` before major graph changes; restore
  with `graph_set_layout` or use `graph_auto_layout` for topological re-arrangement.
- Run material or render diagnostics after bulk shader changes.
- Use a screenshot only for appearance; inspect material/shader data for exact
  assignments.
- Material and shader asset writes are not guaranteed to roll back with Unity
  Undo; inspect partial assets after a stopped batch.
- Do not duplicate asset-move guidance from `unity-assets-prefabs`.
