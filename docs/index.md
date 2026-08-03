---
hide:
  - navigation
  - toc
---

<div class="ubm-hero">

<img src="assets/hero.svg" width="100%" alt="Concept illustration of an AI client sending a live MCP signal through Unity Biome MCP to the Unity Editor">

# Unity Biome MCP

Control the Unity Editor from any MCP-compatible AI client — or from chat inside Unity.

<div class="ubm-hero-actions">
  <a href="getting-started/" class="ubm-btn-primary">Get Started</a>
  <a href="tools/" class="ubm-btn-secondary">{{ meta.tools }} Tools</a>
  <a href="https://github.com/german-krasnikov/unity-biome-mcp" class="ubm-btn-secondary">GitHub</a>
</div>

</div>

<div class="ubm-features">

<div class="ubm-feature">

### Scene & Objects

Inspect and modify GameObjects, components, assets, materials, shaders, and UI elements.

</div>

<div class="ubm-feature">

### PlayTest DSL

Deterministic test workflows with assertions, wait conditions, sweeps, and visual baselines.

</div>

<div class="ubm-feature">

### Batch Operations

Group compatible operations into a single call with undo-backed rollback on failure.

</div>

<div class="ubm-feature">

### Animation & VFX

Work with clips, Animator controllers, Timeline, particles, materials, and Shader Graph.

</div>

<div class="ubm-feature">

### In-Unity Chat

Ask or Agent mode inside the editor with context chips, undo grouping, and multi-backend support.

</div>

<div class="ubm-feature">

### Plugin System

Extend with project-specific server tools, Unity commands, chat chips, and hook points.

</div>

</div>

## Start Here

| Goal | Guide |
|---|---|
| Install and connect | [Getting Started](getting-started/index.md) |
| Choose an MCP client | [Client Guides](install/index.md) |
| Install AI skills | [AI Skills and Agents](install/ai-skills.md) |
| Configure settings | [Settings](settings.md) |
| Use In-Unity Chat | [MCP Chat](chat/index.md) |
| Compare products | [Product Comparison](comparison.md) |

## Tools

| Category | Reference |
|---|---|
| All tools | [Tool Reference](tools/index.md) |
| Decision guide | [Tool Decision Guide](features/tool-guide.md) |
| Scene | [Scene Tools](tools/scene.md) |
| Objects | [Object Tools](tools/objects.md) |
| Batch | [Batch Operations](tools/batch.md) |
| Animation | [Animation Tools](tools/animation.md) |
| Shaders | [Shader & Material Tools](tools/shaders.md) |
| UI | [UI Tools](tools/ui.md) |
| Screenshots | [Screenshot & Visual Diff](tools/screenshots.md) |
| Components | [Component & Event Tools](tools/components.md) |
| Assets | [Asset Tools](tools/assets.md) |
| Diagnostics | [Diagnostics](tools/diagnostics.md) |
| Runtime | [Runtime & Playtest](tools/runtime.md) |

## Architecture

<img src="assets/architecture.svg" width="100%" alt="Architecture diagram: external clients and In-Unity Chat reach the Python MCP server and Unity Editor plugin through their local transport paths">

## Project Inventory

<img src="assets/stats.svg" width="100%" alt="Project inventory stats">

## Workflows

- [Prompting Tips](features/prompting-tips.md)
- [Intent Tools](features/intent-tools.md)
- [Playtest DSL](features/playtest.md)
- [Playtest Composer](features/playtest-composer.md)
- [Wait Conditions](features/wait-conditions.md)
- [Prefab Editing](features/prefab-edit.md)
- [Region Selection](features/region-tool.md)
- [Code Execution](features/code-execution.md)
- [Skills and Templates](features/session-skills.md)

## Developer Reference

- [Plugin Quick Start](plugins/index.md)
- [Plugin API Reference](plugins/api-reference.md)
- [Extending Chat Chip Kinds](chat/extending-chips.md)
- [UI Toolkit Engineering Guide](plugins/ui-toolkit-best-practices.md)
