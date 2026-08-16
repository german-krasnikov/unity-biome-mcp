# Unity MCP Product Comparison

<img src="assets/comparison-hero.svg" width="100%" alt="Unity MCP ecosystem comparison with five product nodes around Unity Biome MCP.">

This August 16, 2026 snapshot compares publicly documented Unity MCP products.
It does not assign an overall winner: projects optimize for different Unity
versions, deployment models, safety controls, and workflows.

<details>
<summary>Method and terminology</summary>

- The official Unity MCP Server is checked against the linked Assistant
  2.17.0-pre.1 documentation and changelog.
- Open-source snapshots are pinned to
  [MCP for Unity v10.1.2](https://github.com/CoplayDev/unity-mcp/releases/tag/v10.1.2)
  ([`4ce7dd3`](https://github.com/CoplayDev/unity-mcp/commit/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50)),
  [AI Game Developer 0.87.0](https://github.com/IvanMurzak/Unity-MCP/releases/tag/0.87.0)
  ([`3a9eb6c`](https://github.com/IvanMurzak/Unity-MCP/commit/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2)),
  and MCP Unity at default-branch commit
  [`0e9fdb6`](https://github.com/CoderGamester/mcp-unity/commit/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7).
- **Documented** means the linked source or documentation describes the capability.
- **Limited** means the documented scope is narrower than the capability definition.
- **Not documented** means no comparable capability was found in that public
  snapshot. It does not prove that the capability is impossible.
- Tool-entrypoint counts are inventory, not a quality or coverage score. Projects
  group actions differently.

</details>

## Product Guide

### Unity Biome MCP

- **Best fit:** deterministic [PlayTest DSL and suites](features/playtest.md),
  [pixel baselines with optional semantic comparison](tools/screenshots.md#screenshot_compare),
  and a broad local Unity tool surface.
- **Snapshot:** [Unity 6000.0](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/unity-plugin/package.json);
  [160 registered tools](assets/_meta.json); Python 3.10+ through `uv`;
  [MIT](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/LICENSE).
- **Relevant constraint:** Unity 6 and a Python/`uv` server. Semantic comparison
  requires a configured Claude CLI and LLM budget. Remote transport and MCP in a
  compiled player are not documented.

### Unity MCP Server / Assistant 2.17.0-pre.1

- **Best fit:** first-party [local relay and IPC bridge](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-overview.html#how-mcp-server-works)
  with [connection approval and multi-client controls](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-overview.html#connection-security).
- **Snapshot:** [Unity 6000.0](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-get-started.html#prerequisites);
  the package installs a local relay, and MCP and gateway connections are
  [not entitlement-gated](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/changelog/CHANGELOG.html#2160-pre1---2026-07-21).
  Fixed tool count, public source, and license are not stated in the cited
  official documentation.
- **Relevant constraint:** the exposed Unity MCP Server is local; the package's
  [separate Streamable HTTP feature](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/changelog/CHANGELOG.html#2110-pre1---2026-06-05)
  configures Assistant as a client of remote MCP servers.

### MCP for Unity v10.1.2 / `4ce7dd3`

- **Best fit:** the broadest declared compatibility floor at
  [Unity 2021.3 LTS](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/MCPForUnity/package.json#L6),
  plus opt-in [2D/3D asset generation and import](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/website/docs/migrations/v10.md#ai-asset-generation).
- **Snapshot:** [47 published tool entrypoints](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/README.md#what-it-does);
  [Python 3.10+ through `uv`](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/Server/README.md#requirements);
  [MIT](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/LICENSE).
- **Relevant constraint:** generation uses third-party providers and
  user-supplied keys. Unity applies batch commands sequentially; rollback is
  not documented.

### AI Game Developer 0.87.0 / `3a9eb6c`

- **Best fit:** [MCP in a compiled game](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#runtime-usage-in-game)
  and [local, HTTP, cloud, and Docker deployment](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#unity-mcp-server-setup).
- **Snapshot:** [Unity 2022.3](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/package.json#L19);
  [70+ published tools](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#skills-and-tools-reference);
  prebuilt server and npm setup CLI; [Apache-2.0](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/LICENSE).
- **Relevant constraint:** compiled-player runtime starts without built-in tools;
  project code must add them. HTTP authorization supports `none`, `oauth`, or
  `token` and defaults to `none`. No general batch or Undo rollback is documented.

### MCP Unity / `0e9fdb6`

- **Best fit:** [optional Unity Undo rollback for batch operations](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/Editor/Tools/BatchExecuteTool.cs)
  and an [interactive MCP App dashboard and resources](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/README.md#mcp-app-tools).
- **Snapshot:** [README declares Unity 6](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/README.md#requirements) while the
  [manifest declares 2022.3](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/package.json#L7);
  [33 tools are listed](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/README.md#mcp-server-tools);
  Node.js 18+; [MIT](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/LICENSE.md).
- **Relevant constraint:** Undo rollback covers recorded Unity changes, not
  every external side effect. The dashboard requires VS Code 1.109+.

## Capability Evidence

The two-column layout remains readable on narrow GitHub views. Products listed
as not documented had no comparable capability in the cited snapshot.

### Setup and operation

| Capability | Documented support |
|---|---|
| Assisted client setup | **Biome:** [Setup Wizard](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/README.md#3-configure-your-client)<br>**Unity MCP Server:** [automatic integration setup](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-get-started.html#auto-configuration)<br>**MCP for Unity:** [configure detected clients](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/README.md#quickstart)<br>**AI Game Developer:** [Editor configuration and CLI](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#step-3-configure-ai-agent)<br>**MCP Unity:** [Editor configuration buttons](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/README.md#step-3-configure-ai-llm-client) |
| General cross-tool batch | **Biome:** [`batch`](tools/batch.md)<br>**MCP for Unity:** Unity-side sequential [`batch_execute`](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/MCPForUnity/Editor/Tools/BatchExecute.cs)<br>**MCP Unity:** [`batch_execute`](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/Server~/src/tools/batchExecuteTool.ts)<br>**Not documented:** AI Game Developer or Unity MCP Server; Unity's [batch-mode setting](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/reference/unity-mcp-reference.html#general-settings) controls connection approval, not operation batching |
| Undo rollback for a general batch | **Biome:** [optional Unity Undo rollback](tools/batch.md#batch)<br>**MCP Unity:** [optional Unity Undo rollback](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/Editor/Tools/BatchExecuteTool.cs)<br>**Not documented:** Unity MCP Server, MCP for Unity, AI Game Developer |
| Multiple loaded scenes | **Biome:** [add, close, activate, and transfer](tools/scene.md#scene)<br>**Unity MCP Server:** [limited scene management](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-overview.html#tools-in-mcp-server); multi-loaded-scene operations are not specified<br>**MCP for Unity:** [`manage_scene` additive workflow](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/MCPForUnity/Editor/Tools/ManageScene.cs)<br>**AI Game Developer:** [list, open, activate, and unload](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#L129-L154)<br>**MCP Unity:** [`load_scene` additive loading](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/README.md#L88-L113) |
| Multiple projects or Unity instances | **Biome:** [project-aware port discovery](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/server/src/unity_mcp/server_filtering.py)<br>**Unity MCP Server:** [target by project path or Editor PID](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-get-started.html#target-a-specific-unity-instance)<br>**MCP for Unity:** [explicit multi-instance routing](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/README.md#advanced)<br>**AI Game Developer:** [deterministic per-project ports](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/cli/README.md#L1523-L1525)<br>**Not documented:** MCP Unity |
| Multiple clients on one Unity instance | **Biome:** [up to eight clients per port](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/unity-plugin/Editor/ClientSlot.cs)<br>**Unity MCP Server:** [explicit multi-client support](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-overview.html#key-features)<br>**Not documented:** MCP for Unity, AI Game Developer, MCP Unity |
| Tool visibility controls | **Biome:** [capability categories](settings.md#tools-and-permissions)<br>**Unity MCP Server:** [per-tool enable/disable](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-overview.html#project-settings)<br>**MCP for Unity:** [`manage_tools` groups](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/Server/src/services/tools/manage_tools.py)<br>**AI Game Developer:** [per-tool enable state](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Tool.SetEnabledState.cs)<br>**Not documented:** MCP Unity |

### Verification and development

| Capability | Documented support |
|---|---|
| Unity Test Runner | **Biome:** [EditMode and PlayMode tools](tools/tests.md#run_tests)<br>**MCP for Unity:** [`run_tests`](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/Server/src/services/tools/run_tests.py)<br>**AI Game Developer:** [`tests-run`](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#L159-L174)<br>**MCP Unity:** [`run_tests`](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/README.md#L73-L77)<br>**Not documented:** Unity MCP Server |
| Deterministic scenario DSL | **Biome:** [PlayTest DSL and suites](features/playtest.md)<br>**Not documented:** Unity MCP Server, MCP for Unity, AI Game Developer, MCP Unity |
| Screenshot capture | **Biome:** [Game, Scene, and multi-view](tools/screenshots.md)<br>**MCP for Unity:** [Game, Scene, and multi-view](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/MCPForUnity/Editor/Tools/ManageScene.cs)<br>**AI Game Developer:** [Camera, Game, Scene, and isolated](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#L151-L154)<br>**Not documented:** Unity MCP Server, MCP Unity |
| Visual baseline and diff | **Biome:** [pixel comparison with optional semantic comparison](tools/screenshots.md#screenshot_compare) through a configured Claude CLI and LLM budget<br>**Not documented:** Unity MCP Server, MCP for Unity, AI Game Developer, MCP Unity |
| Roslyn validation or execution | **Biome:** [code execution and analysis](features/code-execution.md)<br>**Unity MCP Server:** [limited script validation levels](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/reference/unity-mcp-reference.html#general-settings); implementation and arbitrary execution are not documented<br>**MCP for Unity:** [optional script validation](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/README.md#advanced)<br>**AI Game Developer:** [`script-execute`](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#L159-L174)<br>**Not documented:** MCP Unity |
| Custom project tools | **Biome:** [plugin API and entry points](plugins/index.md)<br>**Unity MCP Server:** [attributes, interfaces, and runtime registration](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-tool-registration.html)<br>**MCP for Unity:** [dynamic custom-tool service](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/Server/src/services/custom_tool_service.py)<br>**AI Game Developer:** [attribute-based custom tools](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#add-custom-tool)<br>**MCP Unity:** [manual C# and TypeScript extension](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/README.md#L592-L596) |

### Interaction and deployment

| Capability | Documented support |
|---|---|
| Direct-client connection approval | **Unity MCP Server:** [first direct connection requires approval](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-get-started.html#step-3-approve-the-connection)<br>**MCP for Unity:** [remote API-key authentication](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/Server/README.md#remote-hosted-mode); local interactive approval is not documented<br>**AI Game Developer:** [optional transport authentication](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#unity-mcp-server-setup); interactive approval is not documented<br>**Not documented:** Biome, MCP Unity |
| Chat window inside Unity | **Biome:** [bundled multi-backend Chat](chat/index.md)<br>**Unity MCP Server:** [separate Assistant UI in the same package](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/install/getting-started.html)<br>**Not documented:** MCP for Unity, AI Game Developer, MCP Unity |
| MCP inside a compiled player | **AI Game Developer:** [documented runtime support](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/README.md#runtime-usage-in-game); built-in tools are not included in runtime<br>**Not documented:** Biome, Unity MCP Server, MCP for Unity, MCP Unity |
| Remote MCP transport | **Unity MCP Server:** [local stdio relay and IPC bridge](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-overview.html#how-mcp-server-works); the exposed server has no documented remote transport<br>**MCP for Unity:** [authenticated HTTP transport](https://github.com/CoplayDev/unity-mcp/blob/4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50/Server/README.md#remote-hosted-mode)<br>**AI Game Developer:** [Streamable HTTP and Docker deployment](https://github.com/IvanMurzak/Unity-MCP/blob/3a9eb6cbe368d36cfc2b8e10af2e4912a3d790b2/docs/DOCKER_DEPLOYMENT.md)<br>**MCP Unity:** MCP transport is [stdio](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/Server~/src/index.ts#L135-L142); its [remote option](https://github.com/CoderGamester/mcp-unity/blob/0e9fdb65b3ccf695c1b1be59f5faaf7bd148e9b7/README.md#optional-allow-remote-mcp-bridge-connections) is for the Node-to-Unity WebSocket bridge<br>**Not documented:** Biome |

## Maintenance

This page is intentionally dated because competitor capabilities change quickly.
When updating it:

1. Re-check every external project at one explicit commit or package version.
2. Prefer source code and official product documentation over announcements.
3. Replace stale claims instead of adding historical notes to the matrix.
4. Keep unknowns as **not documented** and avoid inferred negatives.
5. Verify the published desktop and narrow/mobile GitHub layouts.

[Back to README](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/README.md)
