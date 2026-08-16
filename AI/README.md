# Developer documentation

`AI/` is the implementation reference for developers and coding agents working
on Unity Biome MCP. It records current contracts and invariants; user workflows
belong in `docs/`, and exhaustive public tool parameters are generated in
`docs/tools-schema/`.

## Start here

- [`architecture.md`](architecture.md): system boundaries and major components.
- [`structure.md`](structure.md): repository layout and ownership.
- [`api-design-standards.md`](api-design-standards.md): cross-language API rules.
- [`testing.md`](testing.md): repository test policy and durable verification.
- [`reload-reference.md`](reload-reference.md): Unity compilation and reload recovery.
- [`tools-reference.md`](tools-reference.md): tool registration and metadata rules.

## Domain references

| Domain | Canonical reference |
|---|---|
| TCP lifecycle | [`tcp-bridge.md`](tcp-bridge.md), [`mcp-server.md`](mcp-server.md) |
| Chat and providers | [`agent-chat.md`](agent-chat.md), [`chat-view.md`](chat-view.md) |
| Hierarchy and search | [`hierarchy-serializer.md`](hierarchy-serializer.md), [`search.md`](search.md) |
| Runtime and playtests | [`runtime-playtest.md`](runtime-playtest.md), [`playtest-dsl.md`](playtest-dsl.md), [`playtest-composer.md`](playtest-composer.md) |
| Assets and references | [`assets.md`](assets.md), [`references.md`](references.md) |
| UI and Editor UI style | [`ui.md`](ui.md), [`ui-style.md`](ui-style.md) |
| Animation and media | [`animation.md`](animation.md), [`animator-controller.md`](animator-controller.md), [`particles.md`](particles.md), [`timeline.md`](timeline.md), [`shaders.md`](shaders.md) |
| Spatial and regions | [`spatial.md`](spatial.md), [`region-tool.md`](region-tool.md) |
| Intents and reusable sessions | [`intent-tools.md`](intent-tools.md), [`session-skills.md`](session-skills.md) |

Update the smallest canonical document that owns the changed contract. Link to
it from secondary documents instead of copying volatile tool lists or workflows.
