# Project Structure

This document maps stable subsystem boundaries and their ownership entrypoints.
It is intentionally not an exhaustive file tree: generated inventories and
release-era annotations become stale as soon as files move.

Use the repository as the inventory source:

```bash
rg --files
rg --files server/src/unity_mcp
rg --files unity-plugin/Editor
rg --files unity-plugin/ClientSkills
```

## Repository Roots

| Path | Ownership |
|---|---|
| `AI/` | Implementation contracts for developers and coding agents. |
| `docs/` | User-facing guides and generated public tool reference. |
| `server/` | Python MCP server package and its pytest suites. |
| `unity-plugin/` | Main Unity Package Manager package. |
| `unity-plugin-reload/` | Independent Unity reload-recovery package. |
| `unity-test-project/` | Writable Unity integration-test project and fixtures. |
| `unity-test-project-ro/` | Read-only worker test project. |
| `install/` | Installer/configuration implementation and tests. |
| `scripts/` | Repository validation, release, evidence, and generation tools. |
| `protocol/` | Versioned cross-process protocol schemas. |
| `.github/` | CI, release workflows, ownership, and repository automation. |

Important root entrypoints:

| File | Ownership |
|---|---|
| `README.md` | Public repository entrypoint. |
| `CHANGELOG.md` | Canonical release history. |
| `CONTRIBUTING.md` | Contributor workflow and validation requirements. |
| `SECURITY.md` | Supported security boundaries and reporting policy. |
| `install.py` | Repository installer CLI entrypoint. |
| `run_unity_tests.py` | Supported Unity test-runner entrypoint. |
| `mkdocs.yml` | User-documentation navigation and build configuration. |

## Python Server

The installable package lives under `server/src/unity_mcp/`.

| Path | Primary responsibility |
|---|---|
| `server/src/unity_mcp/server.py` | FastMCP composition root, lifespan, built-in registration, resources, and plugin loading. |
| `server/src/unity_mcp/tools/` | Typed MCP wrappers and Python-side orchestration. |
| `server/src/unity_mcp/tools/__init__.py` | Built-in tool-module registration. |
| `server/src/unity_mcp/tools/tool_specs.py` | Tool metadata source for category, mutability, timeout, visibility, runtime, and direct-only behavior. |
| `server/src/unity_mcp/tools/gating.py` | Session visibility categories and plugin category registration. |
| `server/src/unity_mcp/tools/schema_registry.py` | Deferred public schema lookup. |
| `server/src/unity_mcp/tools/run_handle.py` | Durable test run metadata (TestRunHandle, TestRunRegistry); persists across transport disconnect. |
| `server/src/unity_mcp/bridge.py` | TCP request lifecycle, reconnect behavior, CommandLedger, and EditorIdentity. |
| `server/src/unity_mcp/connection_slot.py` | Active Unity connection ownership. |
| `server/src/unity_mcp/play_state.py` | Play Mode readiness tracking (PlayReadinessTracker) with epoch and world_ready handshake. |
| `server/src/unity_mcp/suite_verdict.py` | Test suite verdict separation (inner assertion verdicts vs. outer lifecycle verdicts). |
| `server/src/unity_mcp/middleware.py` | Middleware state and feature composition. |
| `server/src/unity_mcp/middleware_pipeline.py` | Ordered pre-call, dispatch, and post-call pipeline. |
| `server/src/unity_mcp/middleware_types.py` | Source-derived read/write/runtime classification and conditional action rules. |
| `server/src/unity_mcp/middleware_guards.py` | Read-only, Play Mode, batch, retry, and verification guards. |
| `server/src/unity_mcp/plugins/` | Plugin discovery. |
| `server/src/unity_mcp/plugin_api.py` | Supported Python plugin facade. |
| `server/src/unity_mcp/config/` | MCP client discovery, merge, backup, and validation. |
| `server/src/unity_mcp/adapters/` | Chat backend protocol adapters. |
| `server/src/unity_mcp/chat_relay.py` | Chat relay process and backend lifecycle. |
| `server/tests/` | Python unit, integration, conformance, and live tests. |
| `server/tests/seams/` | Live conformance seam tests (round-trip, batch, surface, differential). |
| `server/tests/wire/` | Protocol-level wire tests (no Unity, FakeServer, MITM, cassettes). |

Do not maintain a tool roster in this file. Derive it from `tool_specs.py` and
the registration/parity tests. Public parameter documentation is generated into
`docs/tools-schema/`.

## Main Unity Package

`unity-plugin/package.json` is the package manifest. The implementation is split
by runtime boundary:

| Path | Primary responsibility |
|---|---|
| `unity-plugin/Editor/MCPServer.cs` | Unity listener lifecycle, connection ownership, and dispatch scheduling. |
| `unity-plugin/Editor/CommandRouter.cs` and `CommandRouter.*.cs` | Guarded command dispatch and domain registrations. |
| `unity-plugin/Editor/CommandRegistry.cs` | Command handlers and their mutability, runtime, validation, dispatch, and trust metadata. |
| `unity-plugin/Editor/CommandOptions.cs` | Internal structured registration options behind the public bool overloads. |
| `unity-plugin/Editor/PluginRegistry.cs` and `IMCPPlugin.cs` | C# plugin discovery and registration contract. |
| `unity-plugin/Editor/SyncHelper.cs` | Epoch-based compile/reload state machine used by `sync_unity`. |
| `unity-plugin/Editor/ObjectIdCompat.cs` | Platform compat bridge for Unity 6.0–6.3 (instance-ID) and 6.4+ (EntityId) object identity APIs. |
| `unity-plugin/Editor/UIPanelHost.cs` | Compat layer for `UIDocument` (Unity 6.0) and `PanelRenderer` (Unity 6.4+); used by playtest UI commands and intent tools. |
| `unity-plugin/Editor/PlaytestParser.cs` and `PlaytestRunner*.cs` | Playtest DSL parsing and execution. |
| `unity-plugin/Editor/SourcePatch/` (neutral asmdef) | Optional Source Patch provider contract: immutable DTOs, state machine (`Unavailable`/`Off`/`OnReady`/`Busy`/`Disabling`/`Recovery`), coordinator, and registration slot. Depends on no FSR/Harmony/provider types; main Editor depends on it. |
| `unity-plugin/Editor/SourcePatchHost.cs` | Seam in `asset(write_text)` path; routes `.cs` writes to provider or legacy based on intent/capability. |
| `unity-plugin/Editor/SourcePatchUnityPorts.cs` (`UnityAutoRefreshLeasePort`, implementing `IAutoRefreshLeasePort` from `SourcePatch/SourcePatchCoordinator.cs`) | Auto-refresh disable/restore lease coordination for grouped provider writes. |
| `unity-plugin/Editor/SourcePatchHost.cs` (`GuardLegacyCsWrite`) | Guard invoked from the legacy `.cs` write path when the provider is off/absent. |
| `unity-plugin/Editor/MutationModeToggle.cs` | MCP Settings Hub UI shell for the "Mutation Mode (experimental)" checkbox (P2-04). Polls `SourcePatchHost`/`SourcePatchModePolicy` every 600ms; forwards clicks to `SetMutationIntent`. |
| `unity-plugin/Editor/MutationModeToggleState.cs` | Pure view-model mapping (state, intentOn, providerPresent, isPlaying) → (Checked, Enabled, Tooltip, ShowRecoveryWarning). Zero side effects. |
| `unity-plugin/Editor/Chat/` | In-Unity chat presentation and relay integration. |
| `unity-plugin/Editor/Tests/` | EditMode and PlayMode implementation fixtures. |
| `unity-plugin/Runtime/` | Runtime/player assemblies and test helpers. |
| `unity-plugin/ClientSkills/` | Canonical bundled skills, agents, and conversion support. |

Unity `.meta` files are package assets. Preserve them when moving or adding Unity
files; do not treat them as a separate implementation inventory.

## Reload Package

`unity-plugin-reload/package.json` defines a separate package whose Editor
assembly has no dependency on the main plugin assembly. Its stable entrypoints
are under `unity-plugin-reload/Editor/`:

- `ReloadPlugin.cs` owns reload listener lifecycle.
- `ReloadMiniServer.cs` owns the independent recovery transport.
- `ReloadCommands.cs` and `ReloadDiagnoseCommand.cs` expose recovery commands.
- `ReloadDomainStamp.cs` and `ReloadCompileNotifier.cs` expose recovery evidence.
- `Tests/` owns package-specific fixtures.

The public agent workflow is `sync_unity`; recovery command names are internal.
See [`reload-reference.md`](reload-reference.md).

## Installation, Protocol, and Automation

- `install.py` and `install/` own supported installation and client configuration.
- `scripts/` owns validation, generation, evidence, and release automation.
- `protocol/chat-relay/` owns versioned relay event schemas.
- `.github/workflows/` owns required CI and release orchestration.
- `docs/` owns user workflows; `AI/` must not duplicate their parameter tables.

## Change Routing

| Change | Update or verify |
|---|---|
| Public MCP signature or metadata | Python wrapper, `tool_specs.py`, schema/parity tests, then generated schema through its generator. |
| Unity command contract | `CommandRegistry` registration, handler, validation and guard tests, then the owning `AI/` domain reference. |
| Read/write or runtime classification | `ToolSpec`, conditional rules in `middleware_types.py`, C# registry metadata, and cross-language parity tests. |
| TCP lifecycle | Bridge/server code, Unity listener code, and [`tcp-bridge.md`](tcp-bridge.md). |
| Compile or reload behavior | `tools/sync.py`, `SyncHelper.cs`, reload package when applicable, and [`reload-reference.md`](reload-reference.md). |
| Playtest DSL | Parser, runner, focused Unity tests, and [`playtest-dsl.md`](playtest-dsl.md). |
| Client skill or agent | `unity-plugin/ClientSkills/` plus its conversion/freshness checks. |
| User workflow | The smallest canonical page under `docs/`; link from secondary pages. |

## Verification and History

Follow [`testing.md`](testing.md) for test selection, isolation, and acceptable
evidence. Test names and counts belong in source and run artifacts, not this
structure map. Release history belongs only in [`CHANGELOG.md`](../CHANGELOG.md).

When a path in this document changes, update the ownership entrypoint, not an
exhaustive descendant list. Use `rg --files` for the exact current inventory.
