# Testing Architecture V2 implementation roadmap

Status: active  
Architecture authority: [Testing Architecture V2](architecture-v2.md)  
Audit authority: [v1.26 Testing Audit](v126-audit.md)
Last progress update: 2026-08-09, commit `d0972db5`

Current implementation checkpoint:

- complete: hosted disposable Unity conformance on GitHub-hosted
  Linux/macOS/Windows runners;
- complete: public stdio profile in the same conformance workflow;
- next: built-player PlayTest foundation, with only minimal extra disposable
  worker hardening unless a release gate requires it.

## Delivery rule

Every product defect or infrastructure change follows:

```text
minimal causal RED
-> smallest correct fix
-> focused GREEN
-> public-boundary acceptance RED/GREEN
-> cleanup and non-interference proof
-> CI profile integration
-> exact receipt
```

Refactoring may follow GREEN. A test is not weakened to fit existing behavior.
If the public acceptance cannot be run, the item remains `BLOCKED`, not fixed.

## Phase map

```mermaid
flowchart LR
    P0[P0 Honest gates] --> P1[P1 Gauntlet kernel]
    P1 --> P2[P2 Public stdio]
    P2 --> P25[P2.5 Genuine PlayMode foundation]
    P25 --> P3[P3 Real Unity lifecycle]
    P3 --> P4[P4 C# defect seams]
    P4 --> P5[P5 Built-player PlayTest]
    P5 --> P6[P6 Stateful soak and AI discovery]
```

## P0 — Make green mean green

Goal: eliminate release false positives before expanding coverage.

- add a strict conformance evidence schema and validator;
- add a versioned release-policy manifest with exact active
  profile×OS×runtime×canonical-scenario requirements and declared lifecycle
  ordering edges;
- require exact source SHA, frozen harness-lock digest,
  artifact-manifest digest, typed wheel/UPM digests, verified worker identities
  and cleanup receipts;
- fail on zero selection, unexpected skip, failed cleanup or missing artifact;
- fail on missing/extra canonical scenario IDs, declared ordering-edge
  violations, and `BLOCKED`/`UNTESTED` outcomes;
- make required conformance jobs fail when Unity is unavailable;
- make workflow dispatch inputs authoritative;
- require the conformance marker to include dual-project tests when requested;
- split dual coverage into writable-isolation and read-only-policy profiles so
  neither release profile relies on a conditional skip;
- add Hypothesis to locked dev dependencies and remove silent import skip;
- install authoritative lanes from the frozen harness lock and record its
  digest; test floating dependency ranges only in a separate compatibility job;
- add Python 3.10 to the advertised-support matrix;
- trigger Python, Unity and cross-layer checks from changes on either protocol
  side, and make one required-check aggregator own the result;
- add the public-release privacy gate before publication: tracked content and
  paths; commit identity/messages/history; deleted/unreachable objects after
  rewrites; binary metadata; Unity `.meta` GUID provenance/correlation; package
  metadata; generated artifacts/logs; and live hosting surfaces such as
  description, topics, homepage, tags, releases and action output;
- make release preflight validate evidence rather than workflow conclusion;
- replace independent tag-triggered publication/preflight with one release DAG
  (or draft-to-publish promotion) whose `publish` job explicitly depends on all
  required evidence;
- build wheel/UPM artifacts once, test those exact digests in every profile,
  install-smoke the wheel, and publish the same bytes;
- provision active Unity release profiles from the staged UPM artifact and
  verify distinct worker identity/lease receipts; a pre-existing Editor cannot
  attest which package bytes its loaded assembly came from.

P0 owns the minimal evidence surface:

```text
scripts/gauntlet/release_evidence.py
scripts/tests/test_release_evidence.py
scripts/gauntlet/release_policy.py
scripts/tests/test_release_policy.py
```

Exit gate: a synthetic unit-only/skipped/stale-SHA workflow cannot satisfy the
gate, and no public release can exist before exact-artifact evidence passes.

## P1 — Contract Gauntlet kernel

Goal: one small reusable implementation of contracts, receipts, oracles and
release evidence.

Initial files:

```text
scripts/gauntlet/model.py
scripts/gauntlet/receipts.py
scripts/gauntlet/runner.py
scripts/tests/test_gauntlet_receipts.py
scripts/tests/test_gauntlet_runner.py
```

First RED contracts:

- identity mismatch blocks before dispatch;
- pure-read protected-state delta fails;
- `isError=false` plus known error text fails envelope truth;
- journal tampering and sequence gaps fail;
- stale SHA, zero tests, skips, failed cleanup and missing workers fail release
  evidence.

Then add:

- independently reviewed contract catalog schema;
- effect-domain completeness check;
- deterministic generator and serialized seed;
- fresh-run replay and delta-debug minimizer;
- JUnit adapter and artifact manifest.

Exit gate: pure and in-memory test drivers produce deterministic verdicts and
hash-verified journals across Win/macOS/Linux. The scripted protocol peer is a
P2 deliverable.

## P2 — Installed stdio and deterministic external surfaces

Goal: exercise the released user boundary cheaply on every PR.

- build/install the wheel into an isolated environment;
- connect with a standard MCP stdio client;
- implement a scripted framed-TCP Unity peer;
- assert initialize identity, version, discovery and schemas;
- generate unknown-parameter, read-only, dry-run, capability and envelope
  contracts for all catalog entries;
- test A -> B -> A reconnect against two fake identities;
- add lost/delayed/duplicate/truncated response plans;
- create deterministic fake executables for supported CLI/AI providers;
- exercise chat relay ordering, replacement, backpressure and process cleanup.

Exit gate: product version, schema enforcement, mutability, reconnect and
process cleanup are proved through the installed process on all three OSes.

## P2.5 — Genuine PlayMode foundation

Goal: establish L3 before any L4 PlayTest lifecycle claim.

- split the current authoring guards so EditMode continues to forbid coroutine
  tests while a dedicated PlayMode allowlist can use `[UnityTest]`;
- introduce a runtime-safe PlayMode isolation base instead of the Editor-only
  `UnityMcpTestBase` requirement;
- update both C# `TestAuthoringGuardTests` and Python Unity-source hygiene tests
  to enforce the two explicit policies;
- create the dedicated PlayMode asmdef and check in a minimal neutral
  `.playtest` and `.defs` corpus;
- extract one idempotent PlayTest cleanup scope used by normal and exception
  paths;
- prove real-frame entry, failure, timeout and final stopped/clean state;
- keep the broader runner state-machine refactor in P4.

Exit gate: the minimal corpus executes through actual Play Mode, and no P3
worker scenario claims PlayTest cleanup without this receipt.

## P3 — Disposable Unity workers and lifecycle epochs

Goal: run the same contracts against the real Editor.

- [partial] extract shared `WorkerFactory`, `WorkerLease`, `ProcessSupervisor`, identity,
  transport and receipt modules from existing large scripts;
- [pending] stage one immutable artifact manifest containing exact wheel and UPM digests
  into symmetric A/B workers;
- [complete] run hosted disposable A/B workers on GitHub-hosted
  Linux/macOS/Windows;
- [complete] run persistent one-project conformance scenarios on disposable
  workers;
- [complete] run A/B routing and isolation conformance against disposable
  workers;
- [pending] add test-only compile/domain/Play/scene epoch evidence;
- [pending] run delayed compile start and require a newer terminal epoch;
- [pending] run failed PlayTest suite and require stopped/clean state;
- [pending] run 900 sequential churn with per-attempt baseline;
- [pending] run separate 7 -> 8 -> 9 admission and bounded concurrent startup;
- [pending] inject lost ACK around real mutation commits and reconcile post-state.

Exit gate: every v1.26 live defect has a causal test and a public real-Unity
acceptance receipt, or remains explicitly open.

## P4 — Unity C# production seams

Goal: make difficult lifecycle behavior testable without relying on static
callbacks or source-text assertions.

### Lifecycle coordinator

Extract a `ServerLifecycleCoordinator` with injected event source, listener
factory, clock, port store and dispatcher. Interactive and batch acceptance use
the same `Register/Start/Stop` graph. Test subscribe/unsubscribe exactly once
across compile, reload, Play and quit.

### Connection admission

Replace implicit replacement with `TryAdd -> Accepted | Busy`. Separate accepted
sockets from protocol-validated clients, enforce a bounded first-frame timeout, and
never evict an established client for a newcomer or probe.

### Durable operation receipts

Persist `Accepted -> Committed -> Responded`, including each non-atomic batch
child. Add named test-only fault seams before dispatch, after each commit and
before ACK. Retry replays the same full semantic envelope only for operations
whose reviewed contract is idempotent or externally reconcilable;
`APPLIED_UNKNOWN` otherwise stops for reconciliation.

The complete replay envelope lives only in a project-local, untracked,
owner-only, atomic store with bounded TTL and cleanup verification. Public
Gauntlet JSONL stores its digest and lifecycle state, never the sensitive
payload itself.

### PlayTest state machine

Refactor the update-loop closure behind `IPlaytestWorld`, clock, scene loader,
monitor set and cleanup scope. One idempotent `finally` owns unsubscribe,
teardown, monitors, simulator/config, fresh state and `Time.timeScale`.

### Genuine PlayMode corpus

Create a dedicated PlayMode asmdef where `[UnityTest]` is allowed. Check in a
small `.playtest` and `.defs` corpus. Exercise real frames, physics/Animator,
fresh reload, setup failure, main skip, teardown, timeout, exception and Editor
stop.

### Current source anchors and ownership

| Seam | v1.26 source anchors | Target dependency contract |
|---|---|---|
| lifecycle | `unity-plugin/Editor/MCPServer.cs` | one coordinator owns callback registration and listener start/stop |
| admission | `unity-plugin/Editor/ClientSlot.cs`, `ClientConnectionHandler.cs` | accepted socket becomes protocol-validated within a bounded timeout; no eviction |
| commit/ACK | `unity-plugin/Editor/CommandRouter.cs`, `DedupRegistry.cs`, `BatchHelper.cs` | durable child receipts precede response/ACK and survive replay boundaries |
| PlayTest | `unity-plugin/Editor/PlaytestRunner.cs` and adjacent runner partials | one injected state machine and one idempotent final cleanup scope |

Each seam is a separate PR ownership boundary. The lifecycle coordinator must
not absorb admission or PlayTest policy; the external Gauntlet remains the
independent acceptance owner.

Exit gate: lifecycle, admission, commit/ACK and PlayTest cleanup pass both
small C# causal tests and the external public Gauntlet.

## P5 — Built-player PlayTest

Goal: prove the DSL against immutable standalone builds on Win/Linux/macOS.

- extract runtime-neutral parser/IR/comparisons/variables;
- retain AssetDatabase and Editor lifecycle in an Editor adapter;
- implement player world/action/lifecycle adapters;
- build a minimal demo player with embedded scenario manifest;
- emit atomic JSONL and JUnit, plus deterministic process exit code;
- add hard watchdog and process-tree cleanup;
- compare Editor and player traces/outcomes;
- add one intentional negative scenario to prove the gate can fail;
- gate production/Luna builds on zero test-runner assembly and asset footprint.

Rollout:

1. Linux smoke on PR;
2. Linux/Windows/macOS nightly;
3. all three release-gating after flake budget reaches zero;
4. later WebGL/Luna adapter with JS receipt callback.

The Luna profile runs portrait and landscape, requires the final playable to be
at most 5 MB, and proves that test assemblies and assets add zero production
footprint.

Exit gate: each OS proves build, boot, scenario, data assertion, receipt, exit and
cleanup from the exact release artifact.

## P6 — Stateful, performance and AI-assisted discovery

Goal: explore new risks without weakening deterministic release evidence.

- replace silent optional properties with locked Hypothesis state machines;
- track transition and invariant coverage, not only line/test counts;
- run mutation testing around lifecycle, cleanup, receipt and guards;
- add a real performance marker with project-specific stable budgets;
- preserve deterministic seeds and shrink failures on fresh workers;
- use AI to propose uncovered transition combinations and cluster failures;
- require replay/minimization before promoting an AI/chaos finding.

Exit gate: every scheduled failure has a reproducible receipt or is explicitly
classified as non-reproducible evidence, never silently retried green.

## v1.26 regression ownership

| Neutral regression contract | Primary layer |
|---|---|
| partial-batch lost-ACK replay | P1/P2/P3/P4 |
| failed-suite lifecycle cleanup | P2.5/P3/P4 |
| fresh component/schema cache coherence | P2/P3/P4 |
| delayed diagnostics and version/discovery truth | P0/P2 |
| mutability, read-only and bulk dry-run invariance | P1/P2/P3 |
| conditional capability, schema and envelope truth | P1/P2/P3 |
| reconnect identity, nested Animator and compile epoch | P2/P3/P4 |
| slot admission and concurrent startup | P2/P3/P4 |

## Pull-request sizing

Keep each change independently reviewable:

1. test and receipt schema;
2. implementation that turns that RED green;
3. public-boundary acceptance;
4. workflow gate;
5. documentation and migration of duplicated infrastructure.

Do not mix product fixes for unrelated defects. Do not let two runners own the
same lifecycle transition. Do not delete old infrastructure until receipts show
the replacement covers its acceptance contract.

## Progress ledger

| Checkpoint | State | Evidence |
|---|---|---|
| source/version/worktree lock | complete | v1.26.0 / `695bfbb1`; existing RO fixture files preserved |
| Python/CLI/chat/CI audit | complete | exact collection and workflow inspection in v1.26 audit |
| Unity C#/test topology audit | complete | EditMode-only and lifecycle gaps recorded |
| two disposable endpoint preflight | complete | ports 9600/9699, clean/stopped/idle, plugin 1.26.0 |
| normative V2 architecture | complete | `architecture-v2.md` |
| Gauntlet causal RED | complete | missing package and raw-receipt leak reproduced before fixes |
| Gauntlet GREEN | complete | 30 focused cases; strict evidence and payload privacy included |
| property tests enabled | complete | previous collection skip replaced by 5 executed cases |
| public stdio lane | complete | GitHub Actions run `31323708585`, job `Attested public stdio profile`, success on commit `d0972db5` |
| hosted disposable Unity conformance | complete | GitHub Actions run `31323708585`, Linux/macOS/Windows jobs success on commit `d0972db5`; single profile `31/31`, dual profile `15/15` proved on Windows logs |
| real Unity fault/churn profiles | pending | compile epoch, failed PlayTest cleanup, 900 churn, admission overflow and lost-ACK receipts |
| built-player profile | pending | Win/Linux/macOS receipts |
| final independent review | pending | review findings resolved or recorded |
