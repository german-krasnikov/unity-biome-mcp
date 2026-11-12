# Pre-merge checklist: `biome-7.1`

Owner decision (2026-11-04): `biome-7.1` is not merged into `master`. This file is the list of what must be green for the branch to count as verified, and the list to redo on the
then-current tip if that decision ever changes. Nothing here merges, pushes or opens anything by itself.

Review finding F-2: the branch is 76 commits (steps 7.1, 7.2, 7.3) that rewrite C# the Unity Editor loads (`CommandRouter`, `CommandRegistry`, `BatchHelper`, the chat window and its tests),
and all 30 commits that change Unity-compiled C# list Unity (compile and EditMode) under `NOT RUN` in their messages. Gross change in EditMode tests: 52 deleted (`ChatActivityStateTests` 20, `CancelTurnCleanupTests` 10,
`CancelTurnTests` 6, `ActivityStateMonkeyTests` 16), 12 added (`ChatTurnPortsTests` 8, `ChatTurnWiringTests` 4) and 19 added test cases in `CommandRouterTests`. The pure `dotnet test` lanes
compile the generated C# with the real `CommandRegistry.cs`, `JsonHelper.cs`, `Editor/Decisions/*.cs` and `ChatTurnDriver.cs`; they do not compile `CommandRouter.cs`, `BatchHelper.cs` or
`MCPChatWindow*.cs`. Only the items of section 1 close F-2.

"Measured" below means run on `f5de3626` (the branch tip before the CI edits) on 2026-11-04: macOS arm64, .NET SDK 6.0.421, Python 3.14.6, Node 24.
Tick an item only with a current result; a run that did not happen is not green.

## 0. Make CI run without merging

- [ ] All three OS legs of the Unity workflow, no PR needed: `gh workflow run unity-tests.yml --ref biome-7.1`. A push of `biome-7.1` alone starts nothing: the name matches none of
      `feat/**`, `feature/**`, `fix/**`. A pull request into `master` starts the `pull_request` workflows (Linux leg only, and the path filters apply); a draft that is never merged is enough.
- [ ] Open the job summary of `EditMode Tests (Linux)` and read the table. A run without `UNITY_EMAIL`/`UNITY_PASSWORD` (a fork, Dependabot) is green and runs nothing
      (`Notice - Unity secrets unavailable`); no table means no test ran.

## 1. Unity EditMode (never run on this branch)

Open `unity-test-project` in Unity 6000.0.83f1, wait for the compile, call `sync_unity` and require a clean result, then:

- [ ] The new tests and the expectations the branch flipped (requirement change, owner 2026-10-29), through the durable runner. `--filter` splits on `|` into regex group names; a
      filtered run only proves what it names.

```bash
server/.venv/bin/python run_unity_tests.py EditMode --project unity-test-project --filter \
'UnityMCP.Editor.Chat.Tests.ChatTurnPortsTests|UnityMCP.Editor.Chat.Tests.ChatTurnWiringTests|UnityMCP.Editor.Tests.CommandRouterTests|UnityMCP.Editor.Tests.CommandRouterReadOnlyTests|UnityMCP.Editor.Tests.CommandRouterRegistrationTests|UnityMCP.Editor.Tests.CommandRegistryTests|UnityMCP.Editor.Tests.CommandRegistrySyncOwnerFlagsTests|UnityMCP.Editor.Tests.PluginRegistryTests|UnityMCP.Editor.Tests.BatchHelperParserTests|UnityMCP.Editor.Tests.MCPBatchAtomicTests|UnityMCP.Editor.Tests.PlaytestRunnerEditModeTests|UnityMCP.Editor.Tests.Sprint3FrictionTests|UnityMCP.TestProject.CommandRegistryContractTests|UnityMCP.TestProject.Command.CommandRegistryTests|UnityMCP.TestProject.Batch.BatchTests'
```

  Green: 0 failed, and these were discovered and passed (not skipped):
  - `ChatTurnPortsTests`, 8: `LockReload_takes_the_guard_and_UnlockReload_gives_it_back`, `UndoStart_opens_the_group_of_the_label_and_UndoClose_commits_it`,
    `CancelAsks_cancels_the_asks_of_the_turn_and_leaves_the_others`, `NoteTimedOut_names_the_last_tool_of_the_turn`,
    `ErrorChip_shows_the_text_of_the_event_that_ended_the_turn_or_a_default`, `ResetTurn_clears_every_per_turn_scratch_field`,
    `RestartBackend_stops_the_old_backend_and_takes_a_new_one`, `A_port_that_throws_is_logged_and_does_not_escape`.
  - `ChatTurnWiringTests`, 4: `A_TurnDone_with_no_turn_open_does_nothing_and_with_one_open_ends_it`, `An_Error_with_no_turn_open_is_still_shown`,
    `A_send_over_an_open_turn_sends_nothing_and_takes_the_lock_once`, `Stop_releases_the_ask_that_the_turn_raised`.
  - `CommandRouterTests`: `RepairedCommand_IsMutatingAndKeepsPlayModeAndUndo` (17 cases), `Pr04_EditorPlayAction_IsMutating_True_AgreesWithPython` (renamed, assertion flipped),
    `Registry_IsMutating_Editor_MutationModeDependsOnEnable`, `Registry_IsMutating_UitkFile_DependsOnAction`, `PlayMutationAllowance_IsNarrowAndArgumentAware`,
    `Pr04_IsMutating_BaseFlag_FourCommands` (expectations flipped).
  - `Sprint3FrictionTests.Timeline_ReadAction_NotBlockedByPlayModeGuard` (assertions rewritten).
- [ ] The chat window fixtures that S4 and S5 edited, the whole assemblies (they share the window): `BackendSwitchMidTurnTests`, `RelayFlowWindowTests`, `TranscriptReloadEdgeCaseTests`,
      `DomainRefreshTests`, `ChatWindowSessionMonkeyTests`, `ChatWindowModeMonkeyTests`, `SentTextCacheTests`, `AgentMissDetectorWindowTests`, `ScriptDragDropTests`, `TokenResetTests`,
      `ChatWindowWindowLifecycleTests`. 0 failed; `ScriptDragDropTests` must not be skipped (it skipped silently when it found no sample script).

```bash
server/.venv/bin/python run_unity_tests.py EditMode --project unity-test-project \
  --assembly UnityMCP.Editor.Chat.Tests.View --assembly UnityMCP.Editor.Chat.Tests.CLI
```

- [ ] The whole suite, unfiltered. It enforces `ci/full-baseline.json`; CI enforces the same floor in the `Write job summary` step of `unity-tests.yml`. Never `--allow-empty`, never lower the floor to pass.

```bash
server/.venv/bin/python run_unity_tests.py EditMode --project unity-test-project
```

  Green: 0 failed, total >= 9513 (the floor) and close to the estimate of section 2 (about 9646). A total far from it is a finding, not noise: find which fixtures went missing.
- [ ] The other Unity checks of a pull request, if one is opened or dispatched: `PlayMode Tests (Play-bound corpus)`, `Unity Version Compatibility` (6.0 and 6.5 run the full suite, 6.4 a
      targeted set, the other versions compile only), `Hosted disposable Unity conformance (Linux)`. The branch touches no PlayMode code (`unity-test-project`, `unity-plugin-reload`
      and `unity-plugin/Runtime` are unchanged).

## 2. Test count against the floor

| What | EditMode tests |
|---|---:|
| Floor, `ci/full-baseline.json` (2026-09-05, discovered tests of a real run) | 9513 |
| Last CI run on `master`, all three OS (`docs/quality/data/unity-tests.json`, run of `c96b187f`, the Unity code of `master`) | 9667 |
| Static `[Test]` + `[TestCase(` + `[UnityTest]` in the Unity test assemblies, `master` `86eca971` | 9713 |
| The same, `biome-7.1` `f5de3626` | 9692 (-21) |
| Estimate for `biome-7.1`: static count minus the offset of `master` (46) | about 9646 |
| Estimated margin over the floor | about +133 |

The -21 is -52 +12 +19 +0 elsewhere. The static count is an inventory, not a discovery: these assemblies have no `[TestCaseSource]`, `[Theory]` or `[UnityTest]`, but `#if`, `[Ignore]` and
`[Explicit]` make the real number differ. The offset between the static count and the CI total is 45 or 46 at four commits of `master` (`0e06aa2e` 9569/9524, `2f7968d6` 9622/9577,
`661c6715` and `c96b187f` 9713/9667), so the estimate is good to a few tests; a Unity run replaces it. The README "tests" badge counts `Tests~` as well; do not compare it with the floor.
Reproduce the static count: strip comments and strings as `scripts/readme_facts.py::count_unity_tests` does, count in the assemblies whose asmdef references `nunit.framework.dll`, skip every
`Tests~` folder (Unity ignores it).

## 3. Python

From `server/` the venv comes from `uv sync --locked --extra dev` (Python 3.14).

- [ ] `PYTHONWARNDEFAULTENCODING=1 .venv/bin/python -m pytest tests/ -m "$(.venv/bin/python ../scripts/gen_lane_args.py pytest pr-python-core)" -q --tb=short -n auto --dist load`
      Green: 0 failed (the count is pytest's output, not copied here).
- [ ] From the repository root: `PYTHONWARNDEFAULTENCODING=1 server/.venv/bin/python -m pytest scripts/tests/ -q --tb=short --timeout=30`. Green: 0 failed (the count is pytest's output, not copied here).
- [ ] `PYTHONWARNDEFAULTENCODING=1 server/.venv/bin/python -m pytest install/tests/ -q --tb=short --timeout=30`. Measured: 89 passed, 1 skipped.
- [ ] `server/.venv/bin/python scripts/update_readme.py --check-facts` and `--check`. Measured: `facts OK`, `all generated files are up to date`.

## 4. Pure dotnet (net6.0, no Unity)

- [ ] `dotnet test unity-plugin/Tests~/Decisions/UnityMCP.Decisions.Tests.csproj -c Release`. Measured: 292 passed, 0 failed.
- [ ] `dotnet test unity-plugin/Tests~/ChatTurn/ChatTurn.Tests.csproj -c Release`. Measured: 28 passed, 0 failed.
- [ ] The rest of `ci-pure-dotnet.yml`, for completeness (the branch does not touch them): `Pure/UnityMCP.Playtest.Core.Tests.csproj` 58, `AssemblyFreshness/UnityMCP.AssemblyFreshness.Tests.csproj` 29,
      `SourcePatchReadiness/UnityMCP.SourcePatchReadiness.Tests.csproj` 17. `MutationAdapterContract` fetches pinned sources from the network first (see the workflow); not run.

## 5. DSL

- [ ] `scripts/dsl.sh check` with `BIOME_DSL_ROOT` set to a **git checkout** of biome-dsl at the `TOOLCHAIN` commit of `dsl/biome.project`, after
      `npm ci --ignore-scripts` in its `implementations/typescript` (Node 22 or newer). A copy without `.git` fails at the pin guard with `TOOLCHAIN_ROOT_NOT_GIT`, before Node runs; a checkout at
      another commit fails with `TOOLCHAIN_PIN_MISMATCH`. Green: exit 0 and the content lock reports every file as locked. The last recorded result predates the 2026-11-12 pin correction
      (it was taken at an earlier pin): re-run this item before ticking it.
- [ ] CI does not run `dsl.sh check` (it needs the private biome-dsl). It runs the Node-free tier 0 in the server lane: `test_dsl_fresh.py`, `test_dsl_content_lock.py`, `test_decisions_examples.py`,
      the goldens. The pin guard tests need no Node either: `server/tests/test_dsl_toolchain_pin.py`, and `scripts/tests/test_dsl_sh_toolchain_pin_guard.py`, which the `scripts/tests` step of `ci-python.yml` runs. A change under `dsl/` triggers `unity-tests`, `unity-compat`, `ci-conformance`, `ci-pure-dotnet` and `ci-python` (push; its pull request is unfiltered on purpose) as well.

## 6. Lint

- [ ] `ruff` as pinned by `server/uv.lock`, the command of the `Lint` job:

```bash
RUFF="$(cd server && uv export --locked --extra dev --no-hashes --no-header --no-emit-project | grep -E '^ruff==')"
uvx --from "$RUFF" ruff check --config server/pyproject.toml server/src/ install/ scripts/
```

  Measured: ruff 0.16.8, `All checks passed!`.

## 7. If the decision changes and the branch is merged

- [ ] Redo sections 0 to 6 on the then-current tip (`git fetch origin && git rev-list --left-right --count origin/master...biome-7.1` must print `0  <n>`; otherwise merge or rebase first).
- [ ] The push to `master` runs `EditMode Tests` on Linux, macOS and Windows. Read all three tables: totals are equal on the three OS (9667 each before this branch). Read the log of a red leg
      before calling it a flake; the branch changed code shared by all three.
