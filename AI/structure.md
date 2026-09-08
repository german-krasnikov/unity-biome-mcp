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
| `server/src/unity_mcp/tools/sync_spec.py` | SyncModule ToolSpec metadata and owned wire-command mapping (N1b): decouples `tool_specs.py` from importing `sync.py` to prevent circular dependencies. |
| `server/src/unity_mcp/tools/sync_algorithm.py` | Reload algorithm facade with replaceable swappable interface (N2): narrow `IReloadAlgorithm` abstraction and `Bind` composition seam beneath the public sync surface. |
| `server/src/unity_mcp/tools/sync_module.py` | Second self-owned Python module pilot (N1b): owns public `sync_unity` MCP tool with instance-scoped send/state, delegating the poll/recovery algorithm to `sync.py`'s legacy implementations. |
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
| `server/src/unity_mcp/plugins/_atomic.py` | Atomic plugin registration (PR-04): snapshots and restores all registries (tools, READ/WRITE_CMDS, dsl_tools, gating, budget) on failure to leave zero stale state. |
| `server/src/unity_mcp/plugins/_owner.py` | Scoped plugin identity and API-v1 metadata journal (N1a): tracks which names were declared by the currently-registering plugin for commit-time validation of reserved names, collisions, and owned-name checks. |
| `server/src/unity_mcp/plugin_api.py` | Supported Python plugin facade. |
| `server/src/unity_mcp/config/` | MCP client discovery, merge, backup, and validation. |
| `server/src/unity_mcp/adapters/` | Chat backend protocol adapters. |
| `server/src/unity_mcp/chat_relay.py` | Chat relay process and backend lifecycle. |
| `server/tests/` | Python unit, integration, conformance, and live tests. |
| `server/tests/seams/` | Live conformance seam tests (round-trip, batch, surface, differential). |
| `server/tests/wire/` | Protocol-level wire tests (no Unity, FakeServer, MITM, cassettes). |
| `server/tests/mutation/` | Mutation regression suite (stories S11–S16c): explicit disable, source restore, provider absence, re-add/remove workflows. Uses shared helpers in `_canary.py` and `_provider_lifecycle.py`. Marker: `mutation_live`. Requires disposable provider worker; run via `pytest tests/mutation -m mutation_live` or `scripts/run_mutation_regression_cell.py --mode full`. |
| `server/tests/test_sync_compile_guard.py` | Offline tests for compile-guard absorption in `sync_unity` (ade3bde7): guard→ready, guard→errors, foreign error text, guard→timeout. Validates constant `SYNC_COMPILE_GUARD_TEXT` parity with C# CommandRouter. |
| `server/tests/test_docstring_hygiene.py` | Hygiene guard: prevents production module docstrings from containing ticket codes (which would leak internal identifiers into public tools and errors). Repository and production modules scanned. |
| `scripts/gauntlet/mutation_regression.py` | Mutation regression lane orchestrator: dispatches Python mutation suite (`pytest tests/mutation`) and receives receipt validation from `validate_mutation_regression_receipts.py`. CI job budget tracked via constant. |
| `scripts/gauntlet/fetch_adapter_sources.py` | Fetch pinned provider adapter sources with SHA256 atomic lock (fail-closed, malformed-input guards). Adapter used by `MutationAdapterContract` offline tests. |
| `scripts/run_mutation_regression_cell.py` | Mutation regression cell driver: Python-side orchestration; reads `BIOME_FINAL_PORT_A`, `BIOME_WORKER_A` and runs mutation suite with 1800 s budget. Supersedes legacy FSR-qualification driver. |
| `scripts/validate_mutation_regression_receipts.py` | Receipt validator for mutation regression lane: parses story outputs, enforces pass verdicts, cross-checks with test run state. Fails CI on contradictory results. |
| `scripts/run_ab_reload_identity.py` | A/B reload identity harness (N3): proves cross-worker reload identity and loss-of-ACK recovery by orchestrating synchronous reload cycles, port persistence, and epoch monotonicity checks via two workers with independent ports. Integrates reload-port outcome and noop_recovery contract verification. |
| `scripts/gauntlet/ab_reload_*.py` | A/B reload test slices (compile recovery, identity, live seams, lost-ACK, negative controls, owner safety, proxy, receipt). Each slice covers one narrow failure mode. Run via `run_ab_reload_identity.py`. |
| `scripts/fixtures/ab_reload_harness/` | C# fixtures for A/B harness: `AbReloadNonce.cs` and `UnityMCP.Worker.ABReloadHarness.asmdef` for reload identity verification across workers. |
| `scripts/tests/test_ab_reload_*.py` | Python unit tests for A/B harness phases, lost-ACK, and negative controls without live workers. |
| `scripts/tests/test_run_ab_reload_identity.py` | Integration tests for A/B harness orchestration: port discovery, worker staging, sync/poll, receipt validation. |
| `scripts/check_skills_freshness.py` | Static validation: skills refs, agent versions, tool parity (includes `csharp_parity` marker detection). |
| `scripts/coverage_hotspots.py` | Coverage hotspot detection: parses OpenCover XML, computes top-20 changed methods by coverage/complexity/churn, outputs to `docs/quality/` on CI (includes `--repo-root` flag for relative path mapping). |
| `scripts/tests/test_unity_test_source_hygiene.py` | Hygiene guard: validates Unity C# test structure (fixture bases, ownership registration, async patterns, disposable-worker marking) to prevent false-green tests and coverage gaps. |

Do not maintain a tool roster in this file. Derive it from `tool_specs.py` and
the registration/parity tests. Public parameter documentation is generated into
`docs/tools-schema/`.

## Main Unity Package

`unity-plugin/package.json` is the package manifest. The implementation is split
by runtime boundary:

| Path | Primary responsibility |
|---|---|
| `unity-plugin/Editor/MCPServer.cs` | Unity listener lifecycle, connection ownership, and dispatch scheduling. |
| `unity-plugin/Editor/MainThreadDispatcher.cs` | Single queueing point for non-network Editor API calls; subscribed to `EditorApplication.update` to survive focus loss and domain reload. Replaces `EditorTickOnce`. |
| `unity-plugin/Editor/AtomicFile.cs` | Atomic file writes via temp + `File.Replace` for configuration, state, and test-run store files to prevent data loss under file-locking (Windows OneDrive/AV). |
| `unity-plugin/Editor/CommandRouter.cs` and `CommandRouter.*.cs` | Guarded command dispatch and domain registrations. |
| `unity-plugin/Editor/CommandRegistry.cs` | Command handlers and their mutability, runtime, validation, dispatch, and trust metadata. |
| `unity-plugin/Editor/CommandOptions.cs` | Internal structured registration options behind the public bool overloads. |
| `unity-plugin/Editor/PluginRegistry.cs` and `IMCPPlugin.cs` | C# plugin discovery and registration contract. |
| `unity-plugin/Editor/SyncHelper.cs` | Epoch-based compile/reload state machine used by `sync_unity`. |
| `unity-plugin/Editor/ObjectIdCompat.cs` | Platform compat bridge for Unity 6.0–6.3 (instance-ID) and 6.4+ (EntityId) object identity APIs. |
| `unity-plugin/Editor/UIPanelHost.cs` | Compat layer for `UIDocument` (Unity 6.0) and `PanelRenderer` (Unity 6.4+); used by playtest UI commands and intent tools. |
| `unity-plugin/Runtime/Playtest/Core/` | Engine-free playtest parser core (v1.53.0+): `PlaytestParser.cs` and split files (Directives, Internals, Mcp, Subroutines), `PlaytestHeaderScanner`, utility types (`Float3`, `NumericParsing`, `StringDistance`), `IAliasSource` interface, and `PlayerProfilePreflight` (F1: pre-flight validation of Player script support). Assembly definition includes `noEngineReferences: true` to prevent Editor imports. Consumed by both Editor runner and Player runtime. |
| `unity-plugin/Editor/PlaytestRunner*.cs` | PlayMode and EditMode DSL execution with support for MCP step dispatch and stateful DSL playtest corpus. `PreparedPlaytest` (PR-07) exists as an immutable snapshot type with pure dotnet tests (Tests~/Pure/PreparedPlaytestTests.cs); integration with PlaytestRunner pending. |
| `unity-plugin/Editor/Tests/DelayCallSourceHygieneTests.cs` | Hygiene guard: prevents non-GUI modules from using `delayCall` (which silently fails when Editor loses focus). Allowlist: GUI-only contexts (Chat, Wizard, menus, status bar). |
| `unity-plugin/Editor/Tests/DesyncWarnLimiterTests.cs` | Hygiene guard: HTTP/TLS probes on the TCP port classified as known foreign protocol and throttled to one warning per 30 seconds. |
| `unity-plugin/Editor/Tests/HttpGarbageProbeTests.cs` | Protocol resilience: recognizes HTTP GET and TLS handshakes misrouted to TCP listener and classifies as recoverable desync, not fatal errors. |
| `unity-plugin/Editor/SourcePatch/` (neutral asmdef) | Optional Source Patch provider contract: immutable DTOs, state machine (`Unavailable`/`Off`/`OnReady`/`Busy`/`Disabling`/`Recovery`), coordinator, and registration slot. Depends on no FSR/Harmony/provider types; main Editor depends on it. |
| `unity-plugin/Editor/SourcePatchHost.cs` | Seam in `asset(write_text)` path; routes `.cs` writes to provider or legacy based on intent/capability. Implements `ISourcePatchReloadPort` for reload module isolation (PR-04R). |
| `unity-plugin/Editor/SourcePatchReloadPort.cs` | Interface and registration for Reload module seam (PR-04R): lets `SourcePatchModePolicy.RequestDisable()` delegate without calling `SyncHelper.TriggerSync` directly. |
| `unity-plugin/Editor/SourcePatchPathGuard.cs` | Pre-effect path boundary check (ROI #1): rejects empty/absolute/non-.cs paths, paths outside `Assets/`, and `..` traversal before any Read/Write/Lease effects. Pure string/Path logic, fully unit-testable without live Editor. |
| `unity-plugin/Editor/SourcePatchUnityPorts.cs` (`UnityAutoRefreshLeasePort`, implementing `IAutoRefreshLeasePort` from `SourcePatch/SourcePatchCoordinator.cs`) | Auto-refresh disable/restore lease coordination for grouped provider writes. |
| `unity-plugin/Editor/SourcePatchHost.cs` (`GuardLegacyCsWrite`) | Guard invoked from the legacy `.cs` write path when the provider is off/absent. |
| `unity-plugin/Editor/MutationModeToggle.cs` | MCP Settings Hub UI shell for the "Mutation Mode (experimental)" checkbox (P2-04). Polls `SourcePatchHost`/`SourcePatchModePolicy` every 600ms; forwards clicks to `SetMutationIntent`. |
| `unity-plugin/Editor/MutationModeToggleState.cs` | Pure view-model mapping (state, intentOn, providerPresent, isPlaying) → (Checked, Enabled, Tooltip, ShowRecoveryWarning). Zero side effects. |
| `unity-plugin/Editor/Chat/` | In-Unity chat presentation and relay integration. |
| `unity-plugin/Editor/Tests/` | EditMode and PlayMode implementation fixtures. |
| `unity-plugin/Runtime/` | Runtime/player assemblies and test helpers. |
| `unity-plugin/Tests~/Pure/` | Pure dotnet test lane for Core parser (v1.53.0+): `UnityMCP.Playtest.Core.Tests.csproj` runs NUnit tests with zero Unity install. The folder name ends in `~` intentionally — Unity's asset importer skips `~` paths, keeping `Microsoft.NET.Test.Sdk` references invisible to the Editor. Source files compiled from `Runtime/Playtest/Core/*.cs` in isolation. |
| `unity-plugin/Tests~/AssemblyFreshness/` | Offline NUnit project (`UnityMCP.AssemblyFreshness.Tests.csproj`) validating DLL/PDB freshness detection and import logic without Unity Editor. Covers `AssemblySourceFreshness` bytecode comparison and readiness contract. |
| `unity-plugin/Tests~/SourcePatchReadiness/` | Offline NUnit project (`UnityMCP.SourcePatchReadiness.Tests.csproj`) proving reload-readiness state transitions and ACK-based patch lease validity. Tests `SourcePatchReloadAckTests` and reload block reason propagation without Editor. |
| `unity-plugin/Tests~/MutationAdapterContract/` | Offline NUnit project (`AdapterContract.Tests.csproj`) validating SourcePatch seam contract and adapter Apply outcomes via `AdapterApplyOutcomeTests` and stubs. Seam-drift negative control (`Seam.csproj`) proves renamed seam members break the build. Adapter sources fetched and pinned by `scripts/gauntlet/fetch_adapter_sources.py`. Runs before mutation regression lane in CI. |
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
