# Unity MCP Product Comparison

<p align="center">
  <img src="assets/comparison-hero.svg" width="100%"
       alt="Unity MCP ecosystem comparison with five product nodes around Unity Biome MCP.">
</p>

Last verified: **July 29, 2026**.

This page compares publicly documented Unity MCP products. It does not assign an
overall winner: different projects optimize for different Unity versions, deployment
models, safety controls, and workflows.

## Method

- The official Unity MCP Server is checked against the linked Assistant 2.16
  documentation.
- Open-source projects are checked at the exact linked commits.
- **Documented** means the linked source or documentation describes the capability.
- **Limited** means the documented scope is narrower than the row definition.
- **Not documented** means no comparable capability was found in that public
  snapshot. It does not prove that the capability is impossible.
- Tool-entrypoint counts are inventory, not a quality or coverage score. Projects
  group actions differently.

## Product Snapshots

| Project snapshot | Declared Unity floor | Tool entrypoints | Server requirement | Source / license |
|---|---:|---:|---|---|
| **Unity Biome MCP v1.2.0** | [6000.0](../unity-plugin/package.json) | [142 registered](assets/_meta.json) | Python 3.10+ through `uv` | [MIT](../LICENSE) |
| [**Unity MCP Server / Assistant 2.16.0-pre.1**](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-overview.html) | [6000.0](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-get-started.html#prerequisites) | Not publicly listed | AI Assistant package and installed local relay | No public source or license identified in the cited official documentation; [subscription or trial](https://unity.com/blog/unity-ai-how-to-get-started) |
| [**MCP for Unity v10 / `fc70dda`**](https://github.com/CoplayDev/unity-mcp/tree/fc70dda75da27f97e9a012e77b6de86af40c044c) | [2021.3](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/README.md#quickstart) | [47 published](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/README.md#what-it-does) | Python 3.10+ through `uv` | [MIT](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/LICENSE) |
| [**AI Game Developer `f6db1c2`**](https://github.com/IvanMurzak/Unity-MCP/tree/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5) | [2022.3](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/package.json#L19) | [70+ published](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#skills-and-tools-reference) | Prebuilt server; npm CLI for setup | [Apache-2.0](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/LICENSE) |
| [**MCP Unity `bbfb1c0`**](https://github.com/CoderGamester/mcp-unity/tree/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e) | [README: Unity 6](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/README.md#requirements); [manifest: 2022.3](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/package.json#L6) | [34 registered in source](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/Server~/src/index.ts#L68-L102) | Node.js 18+ | [MIT](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/LICENSE.md) |

## Capability Matrix

| Capability | Unity Biome MCP | Unity MCP Server | MCP for Unity | AI Game Developer | MCP Unity |
|---|---|---|---|---|---|
| Assisted client setup | [Setup Wizard](../README.md#3-configure-your-client) | [Automatic integration setup](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-get-started.html#auto-configuration) | [Configure detected clients](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/README.md#quickstart) | [Editor configuration and CLI](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#step-3-configure-ai-agent) | [Editor configuration buttons](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/README.md#step-3-configure-ai-llm-client) |
| General cross-tool batch | [`batch`](tools/batch.md) | Not documented; [batch-mode auto-approval](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/reference/unity-mcp-reference.html#general-settings) is a connection setting, not operation batching | [`batch_execute`](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/MCPForUnity/Editor/Tools/BatchExecute.cs#L13-L60), sequential in Unity | [**Limited:** several tools accept multiple targets](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/GameObject.Duplicate.cs#L29-L46), but no general orchestrator is documented | [`batch_execute`](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/Server~/src/tools/batchExecuteTool.ts#L8-L134) |
| Rollback for Undo-recorded batch changes | [Optional Unity Undo rollback](tools/batch.md#batch) | Not documented for a general batch | Not documented for `batch_execute` | Not documented for a general batch | [Optional Unity Undo rollback](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/Editor/Tools/BatchExecuteTool.cs#L43-L88) |
| Multiple loaded scenes | [Add, close, activate, and transfer](tools/scene.md#scene) | [**Limited:** scene management is documented](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-overview.html#tools-in-unity-mcp), but multi-loaded-scene operations are not specified | [`manage_scene` additive workflow](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/MCPForUnity/Editor/Tools/ManageScene.cs#L1600-L1613) | [List, open, activate, and unload](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#L126-L154) | [`load_scene` supports additive loading](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/README.md#L89-L112) |
| Multiple Unity projects or instances | [Project-aware port discovery](../server/src/unity_mcp/server_filtering.py) | [Target by project path or Editor PID](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-get-started.html#target-a-specific-unity-instance) | [Explicit multi-instance routing](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/README.md#advanced) | [Deterministic per-project ports](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/cli/README.md#L641) | Not documented |
| Multiple clients on one Unity instance | [Up to eight clients per port](../unity-plugin/Editor/ClientSlot.cs) | [Explicit multi-client support](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-overview.html#key-features) | Not documented | Not documented | Not documented |
| Unity Test Runner | [EditMode and PlayMode tools](tools/scene.md#run_tests) | Not documented | [`run_tests`](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/Server/src/services/tools/run_tests.py#L154-L250) | [`tests-run`](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#L156-L174) | [`run_tests`](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/README.md#L74-L78) |
| Deterministic scenario DSL | [PlayTest DSL and suites](features/playtest.md) | Not documented | Not documented | Not documented | Not documented |
| Screenshot capture | [Game, Scene, and multi-view](tools/screenshots.md) | Not documented for the MCP server | [Game, Scene, and multi-view](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/MCPForUnity/Editor/Tools/ManageScene.cs#L517-L1270) | [Camera, Game, Scene, and isolated](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#L126-L154) | Not documented |
| Visual baseline and diff | [Pixel comparison; optional semantic comparison](tools/screenshots.md#screenshot_compare) through a configured Claude CLI and LLM budget | Not documented | Not documented | Not documented | Not documented |
| Roslyn validation or execution | [Code execution and analysis](features/code-execution.md) | [**Limited:** script validation levels](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/reference/unity-mcp-reference.html#general-settings); implementation and arbitrary execution are not documented | [Optional script validation](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/README.md#advanced) | [`script-execute`](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#L156-L174) | Not documented |
| Tool visibility controls | [Capability categories](settings.md#tools-and-permissions) | [Per-tool enable or disable options](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-overview.html#project-settings) | [`manage_tools` groups](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/Server/src/services/tools/manage_tools.py) | [Per-tool enable state](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Tool.SetEnabledState.cs#L25-L47) | Not documented |
| Custom project tools | [Plugin API and entry points](plugins/quickstart.md) | [Attributes, interfaces, and runtime registration](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-tool-registration.html) | [Dynamic custom-tool service](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/Server/src/services/custom_tool_service.py#L330-L419) | [Attribute-based custom tools](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#add-custom-tool) | [Manual C# and TypeScript extension](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/README.md#L576-L588) |
| Direct-client connection approval | Not documented | [First connection requires approval](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-get-started.html#step-3-approve-the-connection) | [Remote-token authentication](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/Server/README.md#remote-hosted-mode); local approval not documented | Not documented | Not documented |
| Chat window inside Unity | [Bundled multi-backend Chat](chat/using-chat.md) | [Separate Unity AI Assistant in the same package](https://unity.com/blog/unity-ai-how-to-get-started) | Not documented in the open-source repository | Not documented | Not documented |
| MCP inside a compiled player | Not documented | Not documented | Not documented | [Documented runtime support](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#runtime-usage-in-game) | Not documented |
| Remote MCP transport | Not documented | [Local stdio relay and IPC bridge](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-overview.html#how-unity-mcp-works); remote transport not documented | [HTTP transport with authentication](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/Server/README.md#remote-hosted-mode) | [HTTP and Docker deployment](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/docs/DOCKER_DEPLOYMENT.md) | Not documented |

## Notable Differentiators

This is a short selection, not a ranking.

| Product | One or two notable strengths | Relevant constraint |
|---|---|---|
| **Unity Biome MCP** | [Deterministic PlayTest DSL and suites](features/playtest.md); [pixel baselines with optional semantic comparison](tools/screenshots.md#screenshot_compare) | Unity 6 and a Python/`uv` server; semantic comparison requires a configured Claude CLI and LLM budget; remote transport and compiled-player MCP are not documented |
| **Unity MCP Server** | First-party [local relay and IPC bridge](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-overview.html#how-unity-mcp-works); [approval and multi-client controls](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.16/manual/integration/unity-mcp-overview.html#connection-security) | Public source, license, and tool count were not identified in the cited official documentation; access is through a [Unity subscription or trial](https://unity.com/blog/unity-ai-how-to-get-started) |
| **MCP for Unity** | Broadest declared compatibility floor at [Unity 2021.3 LTS](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/README.md#quickstart); opt-in [2D/3D asset generation and import](https://github.com/CoplayDev/unity-mcp/blob/fc70dda75da27f97e9a012e77b6de86af40c044c/docs/wiki/V10.md#L56-L83) | Generation uses third-party providers and user-supplied keys; batch execution is sequential and has no documented rollback |
| **AI Game Developer** | [MCP in a compiled game](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#runtime-usage-in-game); [local, HTTP, cloud, and Docker deployment](https://github.com/IvanMurzak/Unity-MCP/blob/f6db1c27e7f0d647dd3a127e2fff3a65c5785cc5/README.md#docker-) | No general cross-tool batch or Undo rollback is documented |
| **MCP Unity** | [Optional Unity Undo rollback for batch operations](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/Editor/Tools/BatchExecuteTool.cs#L43-L88); [interactive MCP App dashboard and resources](https://github.com/CoderGamester/mcp-unity/blob/bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e/README.md#L148-L192) | Undo rollback covers recorded Unity changes, not every possible external side effect; the dashboard requires VS Code 1.109+; its README and package manifest declare different Unity floors |

## Maintenance

This page is intentionally dated because competitor capabilities change quickly.
When updating it:

1. Re-check every external project at one explicit commit or package version.
2. Prefer source code and official product documentation over announcements.
3. Replace stale claims instead of adding historical notes to the matrix.
4. Keep unknowns as **not documented** and avoid inferred negatives.

[Back to README](../README.md)
