# Source Patch (FastScriptReload) — P1-30 release evidence index

FINAL — coordinator sign-off granted 2026-09-01. Independent Architecture,
Test, and Release reviewers returned their verdicts against this index on
2026-09-01: Architecture GO, Test GO_WITH_GAPS (7 surviving disclosures,
all confirmed honest and non-blocking), Release GO. Full detail in §11.

Status: P1-20 CI gate closed (two unchanged Aggregate-PASS attempts, same
SHA — see §6). P0 engineering MVP and P1 supported-release evidence below.
Scope: `unity-plugin/Editor/SourcePatch*`, `Assets/Plugins/Roslyn` coexistence,
the optional FastScriptReload provider integration, and the CI qualification
matrix that proves it (`scripts/run_fsr_qualification_cell.py`,
`.github/workflows/fsr-qualification.yml`), built against an internal
release-readiness plan covering the full engineering-MVP-through-release
dependency chain for this optional provider integration.

## 1. Frozen SHA set

| Field | Value |
|---|---|
| `base_product_sha` (lock) | `7875430f73d28a043806742164ab478145dedafe` |
| `final_fsr_adapter_sha` / `FINAL_FSR_ADAPTER_SHA` | `b90a5c3fd7cfa452f23e8a807cc7bd61dc934bbf` |
| `fsr_upstream_sha` | `51140b71d9e5df1de231b33ec20ee089b18bebec` |
| Provider pin ref | `b90a5c3fd7cfa452f23e8a807cc7bd61dc934bbf` (branch `biome-mcp-fat-blob-qualification`) |
| Provider pin sha256 (`scripts/source_patch_provider_pin.json`) | `97a354ba6ae96178e4f3073b6248c18d122c47e4937933fd57009af0d47de8c3` |
| Harmony sha256 (lock) | `77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d` |
| Two Aggregate-PASS runs bound to | `b4ca2c16e2812d56f5372d5dcf31d59cea811063` (both attempts, §6) |

Source of truth: `scripts/fsr_qualification_lock.json`,
`scripts/source_patch_provider_pin.json`.

### 1.1 Pin format (offline install, never a base dependency)

The provider is a Git-pinned UPM package resolved only inside disposable
qualification workers (`create_unity_test_worker.py
--source-patch-provider-pin`), never in the base product's `package.json` or
the tracked `unity-test-project/Packages/manifest.json`:

```json
{
  "schema_version": 1,
  "package_name": "com.handzlikchris.fastscriptreload",
  "git_url": "https://github.com/german-krasnikov/FastScriptReload.git?path=/Assets",
  "ref": "b90a5c3fd7cfa452f23e8a807cc7bd61dc934bbf"
}
```

### 1.2 Base-payload deny-check (P1-30 item 1)

Grep across `unity-plugin/` (all `.cs`/`.asmdef`/`.json`, production and test)
for `fastscriptreload|harmony|monomod|handzlik`, plus a full read of every
asmdef `references`/`precompiledReferences` array and `unity-plugin/package.json`:

- `unity-plugin/package.json`: zero hits — no provider dependency, no `git_url`.
- All 13 asmdefs: zero `references`/`precompiledReferences` entries name FSR,
  Harmony, MonoMod, or the fork's package id
  (`com.handzlikchris.fastscriptreload`).
  `UnityMCP.Editor.SourcePatch.asmdef` — the boundary assembly hosting the
  optional-provider seam — has an **empty** `references` array
  (`noEngineReferences: true`); it depends on nothing from the provider at
  compile time. Test asmdefs only add `nunit.framework.dll`.
- Whole-tree grep for the four name fragments across `.cs`/`.asmdef`/`.json`:
  **zero matches outside `unity-plugin/Editor/Tests/`** (the qualification
  fixtures under `scripts/fixtures/fsr_qualification/` and the CI driver
  reference the provider by design — they are the qualification harness, not
  the shipped package payload, and live outside `unity-plugin/`).
- **Stronger than the checklist asks**: `CodeExecutor.cs`'s
  `IsAllowedAssembly` (lines 365-375) is an **active security denylist**, not
  mere absence — it rejects `Microsoft.CodeAnalysis*` and `Mono.Cecil*` by
  name prefix for any assembly an `execute_code` snippet could reference.
- **Explicit exception, by design**: `unity-plugin/Editor/Roslyn/RoslynLoader.cs`
  (+ `RoslynLoaderTests.cs`, `RoslynLoaderFallbackTests.cs`) load
  `Microsoft.CodeAnalysis*` — this is the product's **own legitimate Roslyn
  compiler host** for `execute_code`, unrelated to any Cecil/MonoMod/FSR
  provider seam, and is not part of the optional-package boundary this item
  is checking. Recorded here explicitly per the checklist's own carve-out
  ("кроме нашего legitimate RoslynLoader").

**Verdict: PASS.** No FSR/Harmony/MonoMod/provider-Cecil/provider-Roslyn name
enters the base package, any asmdef, or a compiled reference; the one
Roslyn-family user in the payload is the product's own `RoslynLoader`, an
explicitly carved-out exception.

### 1.3 Optional-package license inventory (P1-30 item 2)

Verified directly against the frozen fork checkout ("upstream fork worker",
`HEAD` = `b90a5c3fd7cfa452f23e8a807cc7bd61dc934bbf` — exact match to
`final_fsr_adapter_sha`, §1):

| Component | License file | License | Holder |
|---|---|---|---|
| FastScriptReload (fork root) | `LICENSE` | MIT | Chris Handzlik, 2020 |
| FastScriptReload (`Assets/`) | `Assets/LICENSE.md` | MIT | Chris Handzlik, 2022 |
| Bundled Harmony | `Assets/Plugins/Harmony/HarmonyLicense.txt` | MIT | Andreas Pardeike, 2017 |
| Bundled ImmersiveVrToolsCommon | `Assets/Plugins/ImmersiveVrToolsCommon/ImmersiveVRToolsCommonLicense.txt` | inherits parent license | — |
| Vestigial Cecil license doc | `Assets/Documentation~/CecilLicense.txt` | MIT | Jb Evain / Novell |

- No Cecil DLL is actually bundled by the fork — `Assets/Plugins/` contains
  only `Harmony/`, `Roslyn/`, `ImmersiveVrToolsCommon/`; the Cecil license
  file is vestigial fork documentation, not an active bundled dependency.
- Bundled Harmony DLL hash confirmed exact: `shasum -a 256` on
  `Assets/Plugins/Harmony/Editor/0Harmony.dll.bytes` at `b90a5c3f` =
  `77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d`, an exact
  match to `harmony_sha256` in `scripts/fsr_qualification_lock.json` (§1).
- All licenses are MIT (or inherit an MIT-licensed parent); no GPL/copyleft
  or field-of-use-restricted component enters the optional package.

**Verdict: PASS.** Full MIT chain confirmed component-by-component; the one
hash the lock file pins (Harmony) is byte-exact against the frozen fork SHA.

## 2. Local P0-80 product cycles (pre-CI, fixed-adapter proof)

Two-fresh-final-SHA cycles per P0-80's DoD, run against successive adapter
states as the dialog-suppression fix landed:

| Cycle | Worker | Outcome SHA | Duration | Adapter | Main head |
|---|---|---|---|---|---|
| A2 | Cycle A2 worker | `26873f9a0230c7456615883303c7bffcce93f7bebc15bf28dcdfab5f71e0f05f` | 100.3s | `e50d43dd` | `180c7929` |
| B | Cycle B worker | `d8763ddecf7d2add9566b41e48cc0a053cb3efe0e434803d414635fb821c2ded` | 133.7s | (same as A2) | `180c7929` |
| A3 (requalified) | Cycle A3 worker | `6dd4a8f438c6d08d3e8baaf91325dba5b35a5be1c4ab970ac111eee394e5dbfa` | 140.7s | `b90a5c3f` | `e15eea55` |
| B2 (requalified) | Cycle B2 worker | `68065337d934ea4657f73bcbb2796626997c0f78f8bfadd150e099b5ba5d91ed` | 135.4s | `b90a5c3f` | `e15eea55` |

A3/B2 requalified on the final adapter SHA (`b90a5c3f`) after the
dialog-suppression fix (pin `7c7788fe`) landed. Key oracle evidence: same
retained-object instance ID (`-2190`) through the full ON sequence
(`1 -> 2 -> invalid (typed, rejected, stays 2) -> 3`, `compileCount=0`
throughout, stable domain stamp) and through the OFF reload
(`epoch 0 -> 1`, same PID) — the exact same shape the CI matrix (§5-6) later
proved structurally, not just locally.

**Re-verification note (evidence-update pass):** the four worker directories
still exist on disk. A2/B's outcome SHA and durations were cross-checked
verbatim against the orchestrator's internal engineering ledger and match
exactly. A3/B2's requalified figures have no corresponding ledger entry to
cross-check against (not logged there) and — like all four "Outcome SHA"
values — were originally produced by a live in-Editor evidence hash at
cycle time, not by any checked-in, offline-rerunnable script; reproducing
them now would require re-running that live Unity procedure, which is out
of scope for this read-only, Unity-untouched pass. All four values are
therefore carried forward as transcribed, not independently re-derived this
cycle.

P0-80 local audit verdict: **GO_WITH_GAPS**.

### 2.1 W2 terminal recovery (P0-05 frozen envelope)

Terminal: `unity-test-project/Library/UnityMCP/FastScriptReloadCurrentProjectW2/a39219c02a39487da46674798168bc0c/recovered-mvid-noop.json`,
sha256 `9cfcf9b2…` (full value in the frozen envelope, §2.2 of the plan doc).
Scenario `a39219c02a39487da46674798168bc0c`, candidate `14fea1a1a86bc92937e53c008cbf36bfed7c86e4`.
Full frozen envelope (PID, evidence file hashes, restored-source hash): plan
doc §2.2.

## 3. P1-10 canonical regression (pre-CI-qualification full suite)

Canonical run `run-57a2917e` (2026-08-31, commit `05c31100`
`test(source-patch): prove ON OFF product cycles`): full EditMode — 9076
leaves, 9037 passed, 34 skipped (self-guard `Ignore`s, expected), **5
FAILED** — `RelayBackendTests`/`ApproveChatWindowTests` `WaitUntilAsync`
timeout signature (pre-existing, unrelated to source-patch; re-verified
below, §3.3). Repo Python 949/1 (known flake,
`test_installed_entrypoint_serves_public_stdio`), installer 78/1 (pwsh
skip, benign), server non-live 6621/1 (schema-path off-by-one,
pre-existing, harmless).

Live subset (package-absent/OFF) at that time: **261 total, 10 failed, 10
errors** — two distinct, unrelated root causes, both fixed since (§3.1):

1. `test_multiscene_live.py` + `test_multiscene_stress_live.py` (10 failed
   + 7 of 10 errors): `conftest.py`'s `_ok()` helper skipped
   `strip_markers()` before an exact-string assertion, so a periodic CLI
   `execute_code` CS1002 console-error annotation (an unrelated, still-open
   side issue — a `CodeExecutor.WrapIfBareCode`-class bug, here in
   `check_unity.py`'s own bare-statement probe) broke `additive_scene`'s
   raw path-equality check.
2. `test_sync_live.py` (3 of 10 errors + 1 of 10 failed): `unity_state_owner`
   teardown's `_capture_unity_state` raced a genuine in-progress domain
   reload the test itself triggered, tripping `ff44cc8c`'s
   fail-closed-on-uncertain-delivery transport behavior.

### 3.1 Fixes landed

- `d41bc6e0` `fix(live): stabilize live lane against reload and console
  noise` — `server/tests/live/conftest.py`'s `_ok()` now calls
  `strip_markers()`, matching `_execute_checked`/`_response_data`;
  `server/scripts/check_unity.py`'s `_probe_guard_locked` SessionState
  probe now ends with `;` (the actual CS1002 root cause — a
  missing-semicolon snippet compiles fine against the mocked
  `execute_code` in unit tests but is invalid C# against a live Roslyn
  compile). Regression tests: `server/tests/test_reload_stability.py`,
  `server/tests/test_strip_markers.py`.
- `7875430f` `fix(live): settle domain reload before state capture in
  teardown` — new bounded, read-only `_wait_compile_idle()`
  `compile_status` poll in `server/tests/live/conftest.py` (never
  closes/reconnects the bridge); `_capture_unity_state` now waits out a
  reload-related exception in place instead of closing the connection;
  `unity_state_owner`'s teardown settles compile-idle before its
  lease-renew/state-capture read; `test_sync_live.py` gets its own
  `pytest.mark.timeout(120)` (domain reload can legitimately run up to
  90s) plus explicit `_wait_compile_idle()` calls after each sync
  assertion. New tests: `server/tests/test_live_unity_state_owner.py`.

### 3.2 Live-lane re-run after the fixes (this evidence-update pass)

`pytest tests/live -m "live and not live_cli"` — port 9600, host
127.0.0.1, worker `unity-test-project`, HEAD
`b4ca2c16e2812d56f5372d5dcf31d59cea811063` (both fixes are ancestors).
**278 passed, 9 deselected, 0 failed, 0 errors, 900.18s (0:15:00)**, one
benign warning (`test_chat_ui_monkey.py` auto-closed an orphan
`MCPChatWindow` left from a prior run — self-healing, not a failure).
**Blocker Test #1 closed.**

Full EditMode has **not** been re-run after these fixes yet — that is a
separate, subsequent gate, honestly left open (§11), not claimed here.

### 3.3 Filtered C# re-check: RelayBackendTests + ApproveChatWindowTests (blocker Test #2)

Baseline `uptime` immediately before the run: load averages **3.88 / 4.32
/ 3.64** (11 users, 18-day uptime) — well below the **9.52 / 9.09 / 10.06**
load recorded at the original `run-57a2917e` failure.

The first dispatch attempt (`run-ab18bbdfc7a94fb494c5aaf7600dd73a`) was
correctly refused by the pre-flight scene-baseline guard: the Editor's
open scene was a leftover
`Assets/TestsTemp/PythonLive/<pid>_<guid>/GridTest.unity` copy from the
just-finished live-lane session (§3.2),
which cannot serve as a trustworthy user-scene baseline for a test run.
Resolved per the reload-recovery skill's pre-flight rule (never force a
dirty/foreign scene through the gate) by opening the real
`Assets/Scenes/GridTest.unity` instead — `dirty=False` before and after,
no data at risk.

The second attempt (`run-342b8614c57a4dfcb919985b6a64314f`, `--filter
"UnityMCP.Editor.Chat.Tests.RelayBackendTests|UnityMCP.Editor.Chat.Tests.ApproveChatWindowTests"`,
EditMode, 02:00:26-02:01:00 UTC, 22.30s): **25 tests, 20 passed, 5
FAILED** — identical failure signature to `run-57a2917e`:

| Test | Duration | Message |
|---|---|---|
| `RelayBackendTests.SetModeAsync_WhenProcNull_CallsCallbackWithFalse` | 2.59s | SetModeAsync with null proc did not callback |
| `RelayBackendTests.SetModeAsync_WhenSendFails_CallsCallbackWithFalse` | 3.49s | SetModeAsync ok=false did not callback |
| `RelayBackendTests.SetModeAsync_WhenSendOk_CallsCallbackWithTrue` | 3.50s | SetModeAsync ok=true did not callback |
| `ApproveChatWindowTests.ApproveAndExecute_WhenSetModeFails_DoesNotSetAgentMode` | 3.35s | SetModeAsync onDone was never called |
| `ApproveChatWindowTests.ApproveAndExecute_WhenSetModeOk_SetsAgentMode` | 3.70s | ApproveAndExecute with ok=true did not set _agentMode |

All five fail inside `WaitUntilAsync`, waiting on `EditorApplication.delayCall`
(stack traces confirm `RelayBackendTests.cs:136` /
`ApproveChatWindowTests.cs:57`) — same shape as the original: a
2000-3000ms timeout budget, ~2.6-3.7s actual.

**Verdict: not reproducible-only-at-high-load.** This reproduces at load
~3.9, roughly a third of the original ~9.5-10 reading — evidence *against*
"this specific shared Mac never gives a quiet window" as the sole
explanation, and *for* the alternative the engineering ledger already
flagged: `WaitUntilAsync`'s timeout/sync primitive itself needs hardening
(a fixed 2000-3000ms budget against `EditorApplication.delayCall` is
marginal regardless of load). Per explicit instruction, **not fixed** —
this is pre-existing and unrelated to source-patch (zero diff on
`RelayBackendTests.cs`/`ApproveChatWindowTests.cs` this branch); recorded
here as escalation data, not treated as a P1-30 blocker for this feature.

## 4. CI qualification driver — architecture

`scripts/run_fsr_qualification_cell.py` (`--mode pilot` / `--mode full`) drives
one frozen `U_MIN` cell end to end: fixture-free GUI pilot, then the 9-step
P0-80 product cycle (package-absent OFF, install, ON retained-object,
disable, uninstall, package-absent OFF again, exact restoration). Every
required-pass cell receipt now carries structural, machine-readable
`off_mode_evidence` (P1-20 reviewer gap #2 — closed by commit `49751e01`):

- **Step 6 (disable)**: `_phase_off_disable_evidence` — one disable call,
  never retried (`_call_effect_expecting_reload_disconnect`); the reload
  fact and every structural field (pid, epoch, `compileStartedCount`) come
  from a new `domain-loads.jsonl` record (`_wait_for_new_domain_load`), not
  a live before/after oracle pair. AssetImportWorker's own
  `[InitializeOnLoad]` records are excluded via `isBatchMode`
  (`5b952081`). The only live call is one read-only `compute` oracle read
  after the reload is confirmed.
- **Steps 8-9 (uninstall + restore)**: `_phase_final_restore` — same
  file-oracle design for the v4 legacy write and the v0 restore write.
  Assembly-needle absence is matched by assembly **location** containing
  `fastscriptreload`, not name (name-based `cecil` false-positived on
  Unity's own built-in `Unity.Cecil.dll` and the base project's
  `com.unity.burst`/`com.unity.nuget.mono-cecil` — `397ee069`). Restore
  content is compared BOM-stripped, not raw (`AssetDatabaseHelper.WriteText`
  writes a UTF-8 BOM on the legacy route — a **frozen, deliberate,
  pre-SourcePatch behavior**, per `AssetHelperTests.cs`'s own
  `WriteText_WritesUtf8ByteOrderMark`; never "fixed" in production code —
  `6e129470`).

Full commit series `ff44cc8c..b4ca2c16`: 40 commits, `git log --oneline
ff44cc8c..b4ca2c16`.

## 5. Qualifying matrix (narrowed after run 5, coordinator decision)

| Cell | Unity/revision | Status |
|---|---|---|
| `min-macos-arm64` | `6000.0.65f1` / `a18e2220bd50` | **Required PASS** |
| `min-linux-x64` | `6000.0.65f1` / `a18e2220bd50` | **Required PASS** |
| `min-windows-x64` | `6000.0.65f1` / `a18e2220bd50` | Documented `INFRASTRUCTURE_BLOCKED`, non-blocking |
| `max-macos-arm64` / `max-linux-x64` (`6000.5.10f1`) | — | **Shelved to P2-07** |

- **Windows**: `setup_ok=true`, `license_ok=true`, `display_ok=false` on
  every observed run (including run 24, both attempts) — no window station
  for a headed GUI Editor from the GH-hosted runner service context.
  Engineering-supported, CI-qualification pending; never a green skip —
  `validate_receipt_set` requires a present, honestly-labeled
  `INFRASTRUCTURE_BLOCKED` receipt or the aggregate fails.
- **u_max**: both `max-linux-x64`/`max-macos-arm64` hung in the
  fixture-free, package-absent pilot after licensing succeeded, before any
  asset import (run 5, `33387852561`) — unaffected by any fix proven
  working on `u_min` in the same run. `6000.5/staging`, a pre-release
  branch. Shelved reason recorded in `scripts/fsr_qualification_lock.json`.

## 6. CI gate — two unchanged Aggregate-PASS attempts

Run `33432095408`, both attempts on checkout SHA `b4ca2c16` (zero code/CI
changes between them — `gh run rerun`):

| | Attempt 1 | Attempt 2 |
|---|---|---|
| `min-linux-x64` | success (19:43:17→19:50:56) | success (20:14:04→20:21:30) |
| `min-macos-arm64` | success (19:43:16→19:49:27) | success (20:14:05→20:24:00) |
| `min-windows-x64` | failure/`INFRASTRUCTURE_BLOCKED` (19:43:16→20:08:08) | failure/`INFRASTRUCTURE_BLOCKED` (20:14:03→20:48:41) |
| **Aggregate** | **success** (2/2 required-pass + 1 documented-blocked) | **success** (2/2 required-pass + 1 documented-blocked) |

Cell-receipt detail, attempt 2 (both required cells, identical shape):

| Field | min-linux-x64 | min-macos-arm64 |
|---|---|---|
| ON sequence (`v1,v2,invalid,v3`) | applied, applied, rejected(expected), applied | applied, applied, rejected(expected), applied |
| OFF epoch | `0 -> 1` | `0 -> 1` |
| `compile_started_count` | `1` | `1` |
| `compute` after disable | `3` | `3` |
| `same_pid` | `true` (pid `13236`) | `true` (pid `13762`) |
| `manifest_matches_pre_pin` | `true` | `true` |
| `assembly_needles_absent` | `true` | `true` |
| `compute` after legacy write | `4` | `4` |
| `restore_sha_matches` (BOM-stripped) | `true` | `true` |
| `candidate_sha` / `lock_base_product_sha` | `b90a5c3f…` / `7875430f…` | `b90a5c3f…` / `7875430f…` |

Attempt 1's per-cell receipts were overwritten by the rerun (GitHub Actions
artifact behavior on `gh run rerun`); its job-level conclusions/timings above
are retained via the runs API (`/attempts/1/jobs`) and match attempt 2
exactly (same cells green, same Windows-blocked shape).

## 7. P1-20 reviewer disclosures (GO_WITH_GAPS verdict)

1. **Aggregate-PASS repeatability**: at review time, only one Aggregate-PASS
   existed (run 12, `33403410433`); run 10 (`33399420344`) had both cells
   PASS but the aggregate itself failed on a packaging-infra gap
   (`f290d8d1`). **Closed**: two unchanged Aggregate-PASS attempts, §6.
2. **OFF/uninstall/package-absent structural evidence**: previously
   unstructured `unity.log` text only. **Closed**: `off_mode_evidence` in
   every required-pass receipt, first applied in run 13 (`33410330964`,
   iterated through run 24) — §4, §6.
3. **Windows**: `INFRASTRUCTURE_BLOCKED` — no window station for headed GUI
   from the runner-service context; engineering-supported, CI-qualification
   pending. See §5.
4. **u_max shelved to P2-07**: staging-branch build, pilot hang
   package-absent on both OS. Endpoint move is a reviewed compatibility
   change per plan §1.1. See §5.
5. **Workflow advisory, not required**: push-scoped trigger stays advisory
   until a second unchanged Aggregate-PASS — now satisfied (§6); promotion
   to a required/blocking gate is a separate, later decision.
6. **Flake policy**: run 11's Linux UPM-registry hang was corroborated
   transient by run 12's clean pass on the same code. Infra failures may
   rerun once, pre-arm, unchanged; a deterministic semantic failure is
   never retried.

## 8. Workspace cleanliness (P1-30 item 3)

`git status --short` (full repo) at evidence-audit time: clean except two
pre-existing, unrelated categories — never the qualification fixture, temp
pin, receipts, or caches this item asks about:

- A **doc-keeper pass** (`AI/architecture.md`, `AI/structure.md`,
  `AI/testing.md`, `CHANGELOG.md`, `docs/features/index.md`, `mkdocs.yml`,
  `docs/features/mutation-mode.md`) documenting this same Source Patch
  feature — unrelated to the FSR qualification matrix itself, but the
  correct doc set to land in the same P1-30 docs commit as this index
  (`docs(source-patch): record final evidence and limitations`), once
  reviewed. Not the qualification-fixture/temp-pin/receipts/caches
  residue this cleanliness check is about.
- `unity-test-project/unity-test-project.sln` — a Rider/VS-regenerated IDE
  artifact; never committed by convention, and confirmed absent from every
  commit in the fix series (below). This is the checklist's own named "sln
  delta" category, and it stays working-tree-only by design, excluded from
  the P1-30 docs commit.
- This evidence index itself, `AI/source-patch-release-evidence.md` —
  drafted, reviewed, and landed in the same P1-30 docs commit as the
  doc-keeper set above; not committed until coordinator and independent
  reviewer sign-off (§11) was obtained.

`git log --stat` across the full fix series (`ff44cc8c..HEAD`, 40+ commits)
confirms none of the following was ever part of a tracked commit's diff:
zero `.sln` files, zero `receipt*.json`/`evidence*.json` artifacts, zero
`Library/`/cache paths. Every file the series ever touched is legitimate
product/test source under `unity-plugin/`, `server/`, `scripts/` (driver,
fixtures, and the two frozen-SHA lock/pin config files —
`scripts/fsr_qualification_lock.json`, `scripts/source_patch_provider_pin.json`
— which are intentional final-SHA config, not iteration-time "temp pins"),
and `.github/workflows/`.

**Correction to the checklist's named "known dirty residue" list**
(`unity-test-project/manifest.json`, `.sln`, `RoslynLoader.cs`, untracked
harness/tests): direct re-check at evidence time found three of the four
already clean and committed, and the fourth already gone rather than dirty:

| File | Status | Detail |
|---|---|---|
| `unity-plugin/Editor/Roslyn/RoslynLoader.cs` | clean | committed `5cfcc4df`, 2026-08-30 (`fix(roslyn): adopt coherent loaded compiler pair`) |
| `unity-plugin/Editor/Tests/Roslyn/RoslynLoaderTests.cs` + `RoslynLoaderFallbackTests.cs` | clean, tracked | committed `5cfcc4df` |
| `unity-test-project/Assets/UnityMCPFastScriptReloadHarness/` | **absent, never committed** | `git log --all` on this path is empty; `5cfcc4df` never touched it (verified via `git show --stat`). It existed only as an untracked working-tree directory and was removed by the §9 P0-25 offline-decommission step ("harness dir + `.meta` were removed") — not a residue-avoidance case, just a stale checklist reference to a path that no longer exists. |
| `unity-test-project/Packages/manifest.json` | clean | committed `4c0b183c`, 2026-08-06 (`feat: 51 production bug fixes...`, unrelated, much earlier) |

Three of the four are ordinary, already-landed product/test commits needing
no "never commit" note. The fourth needs no note either, for a different
reason: it no longer exists to commit.

**Verdict: PASS.** Repo-wide `git status --short` and the full fix-series
`git log --stat` both confirm the qualification fixture, temp pins, sln
delta, receipts, and caches never entered a product commit or the
`unity-plugin/` package payload. The two dirty items that remain
(doc-keeper's pre-existing files, the perennial `.sln`) are unrelated to this
work and documented above as not-to-be-committed-here.

## 9. Offline removal / physical isolation (P1-30 item 4, P0-25)

Source: the orchestrator's internal engineering ledger (gitignored,
repository-private, never part of the public package or a product commit)
— transcribed here. Re-checked against that ledger at this evidence-update
pass: the ledger still exists on disk and the transcription below matches
it verbatim (read-only re-check only; the underlying P0-25 procedure itself
was not re-run — Unity untouched, per this pass's constraint).

- **Offline phase** (2026-08-30): `unity-test-project/Packages/manifest.json`
  and the `.sln` were `git checkout`-restored; `packages-lock.json`
  (gitignored) had its FSR block removed; the harness dir + `.meta` were
  removed; `git status` was empty afterward; `Library/` was left untouched
  (W2 evidence SHA still matched post-restore). Verified with a disposable,
  untracked local check (manifest FSR-absence, harness-absence, and
  git-clean assertions) that was never part of the tracked repository.
- **Phase 2 — fresh package-absent compile** (2026-08-30, ~21:0x): a fresh
  Editor (PID `90309`, port `9600`) launched on the restored project;
  final package resolve = 55 packages, **zero** FSR entries; a live
  `AppDomain`/`CompilationPipeline` scan enumerated 65 loaded assemblies
  with **zero** FSR/Harmony/MonoMod/harness hits; the only Roslyn present
  was Unity's own Hub-path compiler (not the provider's); `isCompiling` was
  `false`; `get_compile_errors` was clean; `Editor.log` had 0 errors; `git
  status` was clean. DoD: **YES**.
- **Reviewer**: independent GO — 65 assemblies enumerated, 0 provider hits,
  W2 evidence intact, log block parsed correctly.
- Preceding identity check (same ledger): confirmed exactly one Unity
  process was live on the project path before the stop/restore sequence
  (old PID `48570` already gone; a subsequent PID `49875` was flagged for
  the user to stop and not reopen before the offline restore proceeded) —
  i.e., the restore ran only after the owned Editor was confirmed stopped,
  per the checklist's "stopped owned Editor and zero lease" requirement.

**Verdict: PASS** (evidence transcribed from the orchestrator's internal
ledger, re-checked verbatim against that ledger at this evidence-update
pass; the underlying procedure itself was not re-executed this cycle —
Unity was not touched, per the read-only constraint on this pass). A
fresh, package-absent Editor loaded no provider assembly; manifest/lock
were restored exactly; the restore only proceeded after the owned Editor
was confirmed stopped.

## 10. Known limitations / documented exceptions

- **MonoBehaviour-derived mutation targets are unsupported.** The harness
  target (`FastReloadTarget.cs`) is a plain POCO, not
  MonoBehaviour-derived — a MonoBehaviour-derived mutation target has no
  on-disk script path for FSR's dynamic-assembly patch type to resolve
  against, tripping a native `gpath.c` assertion in the underlying Mono
  runtime. Recovery envelope: standard W2 terminal recovery path (§2.1);
  not a supported mutation shape for this MVP.
- **Legacy `.cs` write route writes a UTF-8 BOM** (`AssetDatabaseHelper
  .WriteText`, used whenever ON mode is disabled/absent) — a
  pre-SourcePatch, frozen, tested behavior
  (`AssetHelperTests.cs::WriteText_WritesUtf8ByteOrderMark`), never changed
  by this work. Any content-identity check against legacy-written `.cs`
  files must compare BOM-stripped, not raw bytes (`_strip_utf8_bom`,
  `6e129470`).
- **Windows CI qualification**: infrastructure-blocked, not a product
  limitation — see §5/§7.3.
- **u_max (`6000.5.10f1`) compatibility**: unqualified, shelved to P2-07 —
  see §5/§7.4.

## 11. Final sign-off

Final-reviewer pass (Architecture/Test/Release), 2026-09-01: Architecture
GO. Test **GO_WITH_GAPS**, 7 surviving disclosures, each reviewed and
confirmed an honest, non-blocking limitation rather than a gap in the
evidence itself:

1. **A3/B2 not offline-rerunnable** — the two requalified P0-80 local
   cycles' Outcome SHA values were produced by a live in-Editor hash at
   cycle time with no checked-in, offline-rerunnable script; the worker
   directories exist but the hash cannot be independently re-derived
   without live Unity execution (§3, re-verification note).
2. **Windows `INFRASTRUCTURE_BLOCKED`** — no window station for a headed
   GUI Editor from the GH-hosted runner service context; engineering-
   supported, CI-qualification pending (§5/§7.3).
3. **BOM-stripped compare** — the legacy `.cs` write route's UTF-8 BOM is
   a frozen, pre-SourcePatch, deliberately tested behavior; restore-content
   identity is checked BOM-stripped, not raw (§4, §10).
4. **MonoBehaviour limitation** — MonoBehaviour-derived mutation targets
   are unsupported (no on-disk script path for FSR's dynamic-assembly
   patch type; trips a native `gpath.c` Mono assertion) (§10).
5. **Full C# EditMode not re-run after the Python-only live-lane fixes**
   (`d41bc6e0`/`7875430f`) — honestly left open; a separate, subsequent
   gate (§3.2).
6. **5 `WaitUntilAsync` fails escalated as a separate follow-up** —
   reproduced fresh at load ~3.9 (a third of the original ~9.5-10),
   confirmed pre-existing and unrelated to source-patch, deliberately not
   fixed; recorded as escalation data for the `RelayBackendTests`/
   `ApproveChatWindowTests` timeout primitive itself (§3.3).
7. **u_max shelved to P2-07** — `6000.5.10f1` pilot hang, package-absent,
   both OS, before any fix under test; a reviewed compatibility change
   with its own matrix evidence, not part of this MVP's qualified window
   (§5/§7.4).

Release **GO** — the docs-accuracy fixes this pass required
(`docs/features/mutation-mode.md`'s example, this file's harness
attribution) are applied and self-checked clean.

- [x] Coordinator sign-off — granted 2026-09-01.
- [x] Independent architecture/test/release reviewer GO against the P1-30
      release-evidence checklist — Architecture GO, Test GO_WITH_GAPS (7
      disclosures above, all PASS as honest), Release GO. Obtained
      2026-09-01.
