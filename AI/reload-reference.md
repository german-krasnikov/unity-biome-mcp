# Unified Unity Reload API — Canonical Reference

Purpose: single source of truth for designing unity-kiss-mcp's future `sync_unity` (external file edit → import → compile → domain reload → reconnect) on Unity 2021.3→6000.x, macOS/Windows/Linux.
Claim format: `[U<ver>–U<ver> | HIGH/MED/LOW | URL]` — HIGH = official docs/UnityCsReference verbatim; MED = official-derived inference or staff statement; LOW = community (hard cap for community claims). NOT FOUND = no source located; never upgraded.
Verified by V1 (API existence: PASS, 0 hallucinations), V2 (version/OS audit: PASS-WITH-CORRECTIONS — all corrections applied below), V3 (contradiction matrix: resolutions baked in) on 2026-06-10.
Trust hierarchy: docs.unity3d.com / UnityCsReference source > issuetracker (staff-triaged) > Unity-staff forum replies > community threads/blogs.

## §1 AssetDatabase.Refresh / ImportAsset semantics (T1)

- [U6000.0–U6000.3 | HIGH | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.Refresh.html] Unity 6 docs verbatim: Refresh "happens synchronously for asset imports, and asynchronously for script compilation" — returns after imports, BEFORE compile/reload finish.
- [U2021.3–U2022.3 | HIGH | https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AssetDatabase.Refresh.html] **That wording is U6000.0+ docs only**: sentence absent from 2021.3 and 2022.3 pages (both verified); pre-6000 same behavior = inference (V2 #2 / V3 C3).
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.0/Documentation/Manual/AssetDatabaseRefreshing.html] Refresh pipeline order: 1) scan Assets+Packages → 2) import+compile code files (.dll/.asmdef/.asmref/.rsp/.cs) → 3) "Reload the scripting domain, **if Refresh was not invoked from a script**" → 4) post-process → 5) import non-code assets → 6) hot reload. Same steps in 2021.3 manual.
- [U2021.3–U6000.3 | HIGH | same Manual page] Consequence of step 3: script-invoked `Refresh()` NEVER domain-reloads inside the call — reload happens after your C# returns; caller's frames always run the OLD domain.
- [U2022.3 only | HIGH | https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.ScheduleRefresh.html] `ScheduleRefresh` defers to next editor tick (avoids double-import of script+dependent-asset edits); page 404s in 2021.3, 2023.2, 6000.0 — 2022-only API, don't build on it.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ImportAssetOptions.html] Exactly 6 `ImportAssetOptions`, names stable 2021.3→6000.3: `Default`, `ForceUpdate` (force reimport of mtime-unchanged file), `ForceSynchronousImport` (compile-before-dependent-serialize ordering, NOT inline reload), `ImportRecursive`, `DontDownloadFromCacheServer`, `ForceUncompressedImport`.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.ImportAsset.html] `ImportAsset` imports only the given path (siblings NOT imported) and queues a refresh whose pipeline compiles code files.
- [U2021.3–U6000 | MED | https://issuetracker.unity3d.com/issues/assemblies-not-being-reloaded-when-reimporting-c-number-script-asset] **BUT (V3 C1, docs-vs-bug):** known issue — assemblies sometimes NOT reloaded on script reimport.
- **C1 RESOLVED — canonical guidance: never rely on ImportAsset-only recompile; use `Refresh()` + event/error-gated confirmation** (defensive design wins over doc wording).
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.StartAssetEditing.html] Inside Start/StopAssetEditing all imports (incl. Refresh) are deferred until the nest counter balances.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/2021.3/Documentation/Manual/AssetDatabaseBatching.html] Unbalanced StopAssetEditing = editor unresponsive to all asset operations until restart.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/Manual/programming-best-practices.html] AssetDatabase is main-thread only (UnityEditor namespace rule).
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.0/Documentation/Manual/AssetDatabaseRefreshing.html] Refresh scans BOTH `Assets/` and `Packages/` — local `file:`/embedded packages covered.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/Manual/upm-concepts.html] Only Local and Embedded package sources are mutable; registry/built-in/tarball are immutable.
- [U2021.3–U6000.3 | HIGH | Refresh page] Refresh "implicitly triggers an asset garbage collection" (Resources.UnloadUnusedAssets).
- NOT FOUND: behavior of `Refresh()` called while another refresh is in progress (queue vs merge).
- NOT FOUND: ForceUpdate-style reimport behavior inside immutable registry packages.

## §2 RequestScriptCompilation & forced compilation (T2)

- [U2019.3–U6000.3 | HIGH | https://docs.unity3d.com/2019.3/Documentation/ScriptReference/Compilation.CompilationPipeline.RequestScriptCompilation.html] `RequestScriptCompilation()` exists since 2019.3 (2019.2 page 404s).
- [U2021.1–U6000.3 | HIGH | https://docs.unity3d.com/2021.1/Documentation/ScriptReference/Compilation.RequestScriptCompilationOptions.html] Options overload + `RequestScriptCompilationOptions{None, CleanBuildCache}` since 2021.1 (2020.3 page 404s).
- [U2021.1–U6000.3 | HIGH | https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Compilation.RequestScriptCompilationOptions.html] From 2021.1, default `None` = recompile only changed scripts/settings.
- [U2021.1–U6000.3 | MED | same page, inference] **With zero dirty scripts, Request(None) is a silent no-op: no compile → no reload.**
- [U2021.1–U6000.4 | HIGH | https://docs.unity3d.com/ScriptReference/Compilation.RequestScriptCompilationOptions.CleanBuildCache.html] `CleanBuildCache` = "full rebuild of all scripts", recompiles "even if there are no changes" → compile occurs → reload on success.
- [U2021.1–U6000.3 | HIGH | https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Scripting/ScriptCompilation/EditorCompilation.cs] Source: Request does NOT call Refresh, import assets, or scan disk.
- [U2021.3–U6000.3 | MED | same source, combined inference] **Request alone never sees externally-edited un-imported .cs files.** For external edits, `Refresh()` alone imports AND queues compilation; Request adds nothing.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Compilation.CompilationPipeline.RequestScriptCompilation.html] "When compilation is successful, the Unity Editor reloads all assemblies" — reload conditional on success (verbatim on both 2021.3 and 6000.3 pages, V2-verified).
- [U6000.0 | MED | https://discussions.unity.com/t/requestscriptcompilationoptions-cleanbuildcache-not-working/1589996] **IN-93874**: Unity 6.0 report of `CleanBuildCache` firing `assemblyCompilationNotRequired` instead of recompiling; workaround = hand-delete Library/Bee caches. No public issuetracker entry found. → always confirm `assemblyCompilationFinished` actually fired.
- [U2022.3–U6000.3 | HIGH | https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Compilation.CompilationPipeline-assemblyCompilationNotRequired.html] `assemblyCompilationNotRequired` = observable no-op signal; 404s in 2021.3 (added 2022.x) — **no no-op signal exists on 2021.3**.
- [U2021.1–U6000.3 | HIGH | UnityCsReference EditorCompilation.cs] `CleanBuildCache` cost: `ClearBeeBuildArtifacts()` → from-scratch rebuild of every script assembly.
- [U2020.1–U6000.3 | HIGH | https://docs.unity3d.com/2020.1/Documentation/ScriptReference/Compilation.CompilationPipeline-codeOptimization.html] `codeOptimization` toggle = full recompile+reload lever, but mutates user-visible debug state; toggle-and-restore = TWO full rebuilds. Avoid.
- [U2023.1–U6000.3 | HIGH | https://docs.unity3d.com/2023.1/Documentation/ScriptReference/Compilation.AssemblyBuilder.html] `AssemblyBuilder` Obsolete from 2023.1 (not backported to 2021/2022 LTS); `Build()` refuses to start while editor compiles. Don't build on it for Unity 6 tooling.
- NOT FOUND: exact 2022.x minor introducing `assemblyCompilationNotRequired`.
- NOT FOUND: analyzer/source-generator support in AssemblyBuilder — treat as unsupported-by-contract.

## §3 Domain reload mechanics & editor state lifecycle (T3)

- [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html] Domain = isolated memory with compiled assemblies + app state; reload tears down and recreates it.
- [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/2022.3/Documentation/Manual/ConfigurableEnterPlayModeDetails.html] Docs-backed in-reload order — before unload: `beforeAssemblyReload` → `OnDisable()` → `OnBeforeSerialize()` → Mono domain unload; after load: `OnAfterDeserialize()` → `OnValidate()` → `[ExecuteInEditMode]` lifecycle → `[InitializeOnLoad]`/`[InitializeOnLoadMethod]` → `afterAssemblyReload`.
- **Ordering caveat (V3 C2):** docs-backed order is only `InitializeOnLoad → afterAssemblyReload`. `[DidReloadScripts]` position relative to `afterAssemblyReload` is NOT officially documented.
- [U2021.3–U6000.x | LOW | https://discussions.unity.com/t/initializeonload-vs-didreloadscripts/572660] Community-only: InitializeOnLoad always before DidReloadScripts. **Needs empirical test if any handshake depends on it (§15).**
- [U6000.5+ | HIGH | https://docs.unity3d.com/6000.5/Documentation/Manual/programming-code-lifecycle.html] Unity 6.5+ adds `[OnCodeDeinitializing]`/`[OnCodeUnloading]`/`[OnCodeLoaded]`/`[OnCodeInitializing]` — NOT on 6000.3 target (provisional, §15).
- [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/ScriptReference/InitializeOnLoadAttribute.html] `[InitializeOnLoad]` static ctors run on every recompile, on launch, on play-enter only if domain reload enabled; they run BEFORE asset import completes (asset loads may return null — use OnPostprocessAllAssets for asset work).
- **isCompiling/isUpdating "false gap" is REAL — 3 sourced mechanisms:**
  - [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/Manual/AssetDatabaseRefreshing.html] (1) scripted Refresh defers the reload past the refresh — flags read false while reload still pending.
  - [U2019.3–U6000.x | HIGH | https://issuetracker.unity3d.com/issues/editorapplication-dot-iscompiling-is-not-called-when-the-scripts-are-recompiled-upon-refocusing-the-editor] (2) "By Design": refocus-triggered ADBv2 compile runs synchronously, `isCompiling` never reflects it — official advice is compilation events, not polling.
  - [U2021.3–U6000.x | LOW | https://discussions.unity.com/t/how-to-wait-for-unity-to-compile-generated-script-while-running-editor-script/231945] (3) compile starts on a later editor tick after Refresh, killing in-flight coroutines/continuations mid-wait.
  - Verdict: polling can never prove "editor idle"; event-driven handshake only.
- **Threads/sockets at unload:**
  - [HIGH | https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unload?view=net-5.0] AppDomain unload → `Thread.Abort`/`ThreadAbortException` per domain thread; finally blocks run, can delay unload.
  - [U2021.3–U6000.2 | HIGH | https://issuetracker.unity3d.com/issues/editor-is-frozen-on-reloading-domain-when-entering-play-mode-for-the-second-time-using-socket-dot-poll-1-dot-dot-dot] Thread blocked in native `Socket.Poll(-1)` is unkillable → editor freezes on "Reloading Domain" — Unity WON'T FIX, repro through 6000.2. Same class for unclosed named pipes [HIGH | issuetracker editor-gets-stuck…named-pipe].
  - [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/ScriptReference/AssemblyReloadEvents-beforeAssemblyReload.html] **Cooperative shutdown in `beforeAssemblyReload` is mandatory**; rebind in `[InitializeOnLoad]`/`afterAssemblyReload`.
- [U6000.0+ | HIGH | https://docs.unity3d.com/6000.3/Documentation/Manual/async-awaitable-continuations.html] Unity overwrites default SynchronizationContext with `UnitySynchronizationContext`; continuations run on next main-thread Update tick (citation is Unity-6-only Manual page — V2 correction #3).
- [U2021.3–U6000.x | MED | https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Scripting/UnitySynchronizationContext.cs] SyncContext work queue is per-domain managed state — posts queued in the old domain die with it.
- **Cross-reload state:**
  - [HIGH | https://docs.unity3d.com/ScriptReference/SessionState.html] `SessionState` = survives assembly reload, cleared on editor exit — right tool for reload handshake tokens.
  - [HIGH | https://docs.unity3d.com/ScriptReference/EditorPrefs.html] `EditorPrefs` = per-machine, cross-session AND cross-project — wrong for per-run flags (leaks across projects).
  - [MED | Manual/AssetDatabaseRefreshing.html] Disk = cross-process, but writes inside Assets/ re-trigger refresh — write handshake files to `Library/` or `Temp/`.
- [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html] Enter-Play-Mode "Reload Domain off" affects ONLY play-enter reloads; script-change edit-mode reloads still happen.

## §4 UPM local `file:` package update mechanics (T4)

- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/Manual/upm-concepts.html] `file:` folder packages are mutable and used IN PLACE (no cache copy) — edits modify the files Unity loads.
- [U2021.3–U6000.3 | HIGH | same page] Local tarballs (`file:*.tgz`) ARE extracted to the cache and immutable — go stale on rebuild; folders cannot.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/Manual/AssetDatabaseRefreshing.html] Refresh triggers are exactly: (1) editor regains focus IF Auto Refresh pref enabled, (2) Assets > Refresh menu, (3) scripted `AssetDatabase.Refresh()`. No independent package file-watcher exists.
- [U2021.3–U6000.3 | HIGH | same page] `.cs` edits in `file:` packages travel the normal refresh path — the `Packages/` mount is scanned like `Assets/`.
- On macOS there is no Directory Monitoring (Windows-only feature, §5) → between focus events the editor is blind to disk changes. ("macOS has no OS-level file watching" is **inference** from the Windows-only doc scope, not a stated doc fact — V2 correction #5.)
- [U2020.2–U6000.3 | HIGH | https://issuetracker.unity3d.com/issues/packman-isnt-refreshed-when-calling-assetdatabase-dot-refresh-after-making-changes-to-a-pacakge] Official "By Design" (issue 1248326): `Refresh()` does not update package registration/metadata; "if only package metadata changed, replace Refresh() with Client.Resolve(); if the scope of changes is unknown, call **both**." Canonical division: Refresh = file content, Resolve = manifest/metadata.
- [U2021.3–U6000.2 | HIGH | https://docs.unity3d.com/6000.2/Documentation/ScriptReference/PackageManager.Client.Resolve.html] `Client.Resolve()` is fire-and-forget void; results via `Events.registeringPackages/registeredPackages`; "if packages are already up-to-date, no event is raised".
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PackageManager.Events-registeredPackages.html] `registeredPackages` fires AFTER refresh+compile+domain reload; handlers wiped by reload — register in `[InitializeOnLoadMethod]`.
- **Version-bump trick = folklore** (NOT FOUND in any official source): works only because package.json change → resolver sees "altered" package → registration change → full refresh+reload. Heavyweight substitute for `Client.Resolve()`.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/Manual/upm-conflicts-auto.html] packages-lock.json stores resolution results; delete to force indirect/git re-resolution; never hand-edit.
- NOT FOUND: any content-hash pinning for local `file:` folders — lock deletion is irrelevant to stale `file:` .cs content.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/Manual/upm-embed.html + cus-location.html] Embedded (in `Packages/`) vs `file:` reference: both mutable, both same AssetDB scan, **no documented refresh-behavior difference**.
- NOT FOUND: any claim that embedding fixes "edits not picked up" vs `file:` reference.
- [U2019.1–U6000.3 | LOW | https://discussions.unity.com/threads/solved-force-reload-package.629140/] Unity staff: catch-22 — if the consuming project has ANY compile errors, no mechanism (Refresh/Resolve/bump) loads your fixed package code; editor stays on stale assemblies until errors clear.

## §5 Auto Refresh prefs & focus-based refresh (T5)

- [U2021.3–U6000.x | HIGH | https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2021.3/Editor/Mono/PreferencesWindow/AssetPipelinePreferences.cs] Pref keys (internal, source-verified): `kAutoRefresh` (bool) through 2021.2; `kAutoRefreshMode` (int: 0=Disabled, 1=Enabled, 2=EnabledOutsidePlaymode) from 2021.3 (~.10f1 backport, MED) with legacy-bool fallback.
- [U2022.1–U6000.x | HIGH | master branch, same file] Unity 6 uses the same `kAutoRefreshMode` key — no new key.
- [U6000.0–U6000.x | HIGH | https://docs.unity3d.com/6000.0/Documentation/Manual/preferences-asset-pipeline.html] UI: Edit > Preferences > Asset Pipeline (macOS: Unity > Settings); dropdown values exactly "Disabled" / "Enabled" / "Enabled Outside Playmode".
- [U2021.3–U6000.x | HIGH | same Manual page + CsReference AssetPipelinePreferences.cs] **Directory Monitoring = Windows-only**, detection optimization only (not a background importer); pref `DirectoryMonitoring` (bool, default true); UI hard-disabled off-Windows.
- NOT FOUND: any doc that Directory Monitoring imports without focus.
- [U2019.3–U6000.x | HIGH | https://docs.unity3d.com/6000.0/Documentation/Manual/AssetDatabaseRefreshing.html] Auto-refresh is FOCUS-gated through Unity 6; no 6000.x release note announces background refresh [MED] — treat "Unity 6 refreshes in background" as false.
- [U2022.2–U6000.x | HIGH | https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorApplication-focusChanged.html] `EditorApplication.focusChanged` exists **from U2022.2** (2021.3 AND 2022.1 pages 404 — V2 correction #1).
- **CRITICAL: scripted `Refresh()` is NOT gated by prefs/focus.**
  - [U2019.3–U6000.x | HIGH | Manual/AssetDatabaseRefreshing.html] Listed as an independent trigger, not conditioned on the focus/Auto-Refresh clause.
  - [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.DisallowAutoRefresh.html] Strongest wording: "The Asset Database always performs a refresh if AssetDatabase.Refresh is called, regardless of this method and its internal counter."
  - [U2021.3–U6000.x | HIGH | https://www.jetbrains.com/help/rider/Refreshing_Unity_Assets.html] Proof-by-product: Rider refreshes an unfocused background Unity via in-process plugin RPC; only documented exception: "Rider does not refresh assets if Unity is in the play mode."
- Caveat: the call must run on the editor main loop (remote command → main-thread dispatch); unfocused editor ticks enough to service it (that IS the Rider mechanism). NOT FOUND: any doc claiming Refresh is deferred-until-focus when invoked from code.
- NOT FOUND: any authoritative source documenting `osascript 'tell app Unity to activate'` / Win32 SetForegroundWindow as a refresh workaround — folk practice; fails when Auto Refresh is Disabled or Play-gated. Canonical replacement: in-process `AssetDatabase.Refresh()` over TCP.
- [U2021.3–U6000.x | MED | https://issuetracker.unity3d.com/issues/unity-editor-gets-focused-on-mac-when-recompiling-scripts-finishes-after-switching-windows-with-mac-mission-control] macOS: reload completion can steal focus back to the Editor (Mission Control case) — focus juggling around reload is fragile on Mac.

## §6 LockReloadAssemblies / DisallowAutoRefresh (T6)

- [U2021–U6000 | HIGH | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorApplication.LockReloadAssemblies.html] `LockReloadAssemblies` blocks **assembly (domain) reload only**. "Each LockReloadAssemblies must be matched by UnlockReloadAssemblies, otherwise scripts will never unload."
- [U2021–U6000 | HIGH | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.DisallowAutoRefresh.html] `DisallowAutoRefresh` blocks **automatic refresh** (scan+import) via ref-counted native counter; explicit `Refresh()` always runs regardless.
- [U2021–U6000 | HIGH | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.AllowAutoRefresh.html] Disallow/Allow are explicitly ref-counted and nest-safe; over-release asserts AND keeps decrementing — permanently wedged until rebalanced.
- [U2021–U6000 | MED | composition of three HIGH doc statements] Net behavior of Refresh-under-Lock: import completes immediately, compile proceeds async, **only the reload is queued until Unlock**. (Sync-import/async-compile doc wording is U6000.0-only; pre-6000 = inference — V2 correction #2.)
- NOT FOUND: official guidance to pair Lock+Disallow — community pattern only [LOW | https://discussions.unity.com/t/can-i-stop-auto-compile-after-edit-create-or-remove-a-script/878449].
- NOT FOUND: exact moment the queued reload fires after Unlock. Community: next editor pump, sometimes needs an explicit `Refresh()` kick after `AllowAutoRefresh()` [LOW | https://discussions.unity.com/t/assetdatabase-allowautorefresh-not-working/927783].
- [U2021–U6000 | MED | https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorApplication.bindings.cs/] Counters live in NATIVE editor state (`m_DisallowAutoRefresh` assert text; `StaticAccessor("GetApplication()")` bindings) → **survive domain reload while your managed guard dies**.
- [U2021–U6000 | MED | same bindings file] No public query API; only internal `EditorApplication.CanReloadAssemblies()` (reflection, bool, no depth). NOT FOUND: DisallowAutoRefresh counter reader; crash-survival statement (logically process-local — flagged speculation).
- Known bugs:
  - [U2021–U2022 | HIGH | https://issuetracker.unity3d.com/issues/domain-reload-missing-when-entering-play-mode] Held lock silently skipped the play-mode domain reload (2021.2/2022.1, fixed).
  - [U2021–U6000 | HIGH | https://issuetracker.unity3d.com/issues/auto-refresh-is-still-active-when-its-set-to-to-disable-in-the-preferences] **UUM-40547**: Auto Refresh ran despite "Disabled" pref (repro 2021.3.27f1/2022.3.3f1/6000.0.0b11; fixed 2021.3.44f1/2022.3.X/6000.0.X).
  - [U2020–U2021 | MED | https://issuetracker.unity3d.com/issues/code-coverage-windows-adding-new-included-slash-excluded-paths-row-locks-assembly-reload-in-the-editor] Windows UI flows (OpenFolderPanel/Code Coverage) left the lock held; recovery was "drag a window" — Unity's own flows can wedge the same native lock.
- [U2021–U6000 | MED | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUtility.RequestScriptReload.html] Recovery lever once balanced: `EditorUtility.RequestScriptReload()` — async forced reload next frame, no recompile.
- Safe pattern [HIGH | AllowAutoRefresh counter contract]: `DisallowAutoRefresh(); LockReloadAssemblies(); try {…} finally { UnlockReloadAssemblies(); AllowAutoRefresh(); }` + one `Refresh()` after release [LOW] + SessionState lock-marker rebalanced in `[InitializeOnLoad]` (managed owner dies across reload, native counter doesn't) [MED].
- **Prior-art note (T8/V3): ZERO of 5 surveyed MCP bridges use LockReloadAssemblies — our ReloadGuard is a design outlier; all competitors do stop-before/restart-after.**

## §7 Batch-mode / headless / out-of-process compile (T7)

- [U2021.3–U6000 | HIGH | https://docs.unity3d.com/Manual/EditorCommandLineArguments.html] HARD BLOCKER: "You can't open a project in batch mode while the Editor has the same project open" — wording identical 2021.3→6000.x.
- [U2017–U6000 | MED | https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project/607546] Lock = `Temp/UnityLockfile`; in batch mode this is a HARD FAIL ("Aborting batchmode due to fatal error"), never a wait. Only escape: full project COPY with its own Library/Temp (Unity Support article exists for separate-directory multi-instance [MED]).
- Headless compile-gate recipe: `<UnityBinary> -batchmode -nographics -accept-apiupdate -quit -logFile - -projectPath <copy> -executeMethod CI.NoOp` → exit 0 = compiles [HIGH | CLI docs + MED | game.ci].
- Per-OS binaries (merged per V2 #6) [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/6000.0/Documentation/Manual/EditorCommandLineArguments.html]:
  - macOS: `/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/MacOS/Unity` (the binary inside the .app, not `open -a`).
  - Windows: `C:\Program Files\Unity\Hub\Editor\<ver>\Editor\Unity.exe`.
  - Linux: docs say `/opt/Unity/Hub/Editor/<ver>/Editor/Unity`; Hub actually defaults to `~/Unity/Hub/Editor`, user-configurable [LOW] — resolve via Hub config.
- [U2021.3–U6000 | HIGH | https://docs.unity3d.com/2021.3/Documentation/Manual/EditorCommandLineArguments.html] Without `-accept-apiupdate` the APIUpdater doesn't run in batch mode → phantom compile errors the interactive editor wouldn't show.
- Exit contract: exceptions/failures → exit 1 [HIGH | CLI page]; compile failure logs "Scripts have compiler errors" [MED | https://game.ci/docs/troubleshooting/common-issues/].
- [U2019.4–U2022.1 | MED | https://issuetracker.unity3d.com/issues/unity-terminates-with-error-code-0-when-an-exception-occurs-while-importing-a-package-in-bach-mode] **Don't trust exit code alone**: known exit-0-despite-failure bug class — grep the log for the compiler-errors marker too.
- NOT FOUND: any "compile scripts only, then exit" CLI flag in 6000.x; any compile-only job type in game-ci/Unity Build Automation.
- Cheapest CI gate in practice = EditMode test run (forces full compile before tests) [MED | https://game.ci/docs/github/test-runner/]; test-framework exit codes explicitly undocumented [HIGH | com.unity.test-framework@1.4 reference-command-line].
- External Roslyn/csc/MSBuild vs generated csproj [MED | https://discussions.unity.com/t/expose-sln-and-csproj-generation/892301]: staff — "Csproj's today are only for the IDE experience"; requires populated Library; misses per-asmdef defines [HIGH], source generators (`RoslynAnalyzer` label) [HIGH], ILPostProcessor/Burst codegen [LOW/MED]. Fast pre-filter only, never a verdict.
- `Library/Bee` (dag.json/rsp) is parsable but internal/unsupported — NOT FOUND: any supported standalone bee_backend invocation. `Library/ScriptAssemblies` = last-GOOD compile output, stale the moment code changes [MED].

## §8 MCP prior-art survey — 5 bridges (T8) [all cited @main — pin SHAs when canonizing, §15]

| Repo | Refresh trigger | Reload survival | Reconnect | Steal |
|---|---|---|---|---|
| CoplayDev/unity-mcp | `Refresh(ForceUpdate\|ForceSynchronousImport)` + optional `RequestScriptCompilation`; returns state, client polls | EditorPrefs resume-flag + heartbeat file "reloading" + 6-step retry ladder (0→1→3…30s) | Python waits ≤20s for session; in-flight → `hint="retry"` | heartbeat-during-reload; resume ladder. Beware their #1173 Windows socket race |
| CoderGamester/mcp-unity | `RequestScriptCompilation()` + respond inside `compilationFinished` (pre-reload, socket still alive) | delayCall-scheduled restart; no state | WS exp backoff 1–30s + jitter, cap 50; play-mode 3s poll; queue+replay commands | reply-before-reload; queue/replay |
| Arodoid/UnityMCP | none | `[InitializeOnLoad]` rebirth + naive 5s loop | in-flight hangs | anti-pattern baseline — skip |
| IvanMurzak/Unity-MCP | `Refresh(ForceSynchronousImport)`; if isCompiling → `Processing`+requestId, push result post-compile | explicit disconnect-before/reconnect-after | SignalR retry; KeepConnected | two-phase Processing/follow-up for reload-crossing ops |
| hatayama/unity-cli-loop (uLoopMCP) | refresh-then-compile via CompileUseCase | session (port/projectRoot/sessionId) persisted; compile result persisted by requestId; **compile lock-FILE on compilationStarted/Finished** = out-of-band signal | TS poller fast→slow; 2s grace on loss; tools-changed notify after recovery | best-in-class: full DomainReloadRecoveryUseCase + result persistence + lock-file |

- Universal pattern [HIGH, all repo sources]: **nobody keeps the socket alive through reload** — stop-before/restart-after + external-side retry, everywhere.
- Differentiator 1: does the in-flight request get a real answer (respond-pre-reload / Processing+push / persist-by-requestId / retry-hint punt)?
- Differentiator 2: is reload state persisted (EditorPrefs flag, session port file) vs re-derived from `[InitializeOnLoad]`?
- NOT FOUND: any `LockReloadAssemblies` use across all 5 repos (uLoopMCP's "CompilationLockService" is a lock *file*).
- [LOW | https://github.com/CoplayDev/unity-mcp/issues/1173] Field lesson: Windows TcpListener leak across reload — 500ms release-wait too short (fix was 2000ms + `ExclusiveAddressUse=true` + `listener?.Server?.Dispose()` in beforeAssemblyReload); silent port-fallback masked it while the client stayed on the old port.
- [LOW | https://github.com/AnkleBreaker-Studio/unity-mcp-server] Sixth candidate located, not investigated.

## §9 Compile-finished signals & state machine (T9)

- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Compilation.CompilationPipeline-compilationFinished.html] `compilationStarted` fires before the first per-assembly event; `compilationFinished` after the last `assemblyCompilationFinished` — main thread, OLD domain, BEFORE any reload.
- **THE critical truth** [U2021.3–U6000.4 | HIGH | RequestScriptCompilation page, verbatim on 2021.3 AND 6000.3]: domain reload is CONDITIONAL on success — "if the compilation was successful, the Editor reloads all assemblies." On compile errors: NO reload, OLD assemblies keep running, no before/afterAssemblyReload/DidReloadScripts fire.
- [U2018–U2021 | MED | https://discussions.unity.com/t/custom-assemblies-are-not-reloaded-if-there-is-a-compile-error/705717] Even successfully-compiled asmdef assemblies are NOT loaded until ALL errors clear ("Begin MonoManager ReloadAssembly" never raised).
- [U2021.3–U6000.x | MED | https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Scripting/ScriptCompilation/CompilationPipeline.cs] **`compilationFinished` fires on FAILED compiles too** (forwarded unconditionally in source) — a handler flipping "done" on it runs while old code still executes.
- [U2021.3–U6000.4 | HIGH | https://docs.unity3d.com/ScriptReference/EditorUtility-scriptCompilationFailed.html] Discriminator: `EditorUtility.scriptCompilationFailed` — "True if there are any compilation error messages in the log."
- [U2021.3–U6000.x | MED | https://issuetracker.unity3d.com/issues/editorutility-dot-scriptcompilationfailed-not-flagging-package-compilation-errors-during-editor-startup] Known bug: it missed PACKAGE compile errors during editor startup — don't trust it solo at boot.
- [U2021.3–U6000.3 | HIGH | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Compilation.CompilationPipeline-assemblyCompilationFinished.html] Errors travel ONLY in `assemblyCompilationFinished(string path, CompilerMessage[])` — fires per assembly even on failure.
- [U2021.3–U6000.3 | HIGH | compilationFinished page] `compilationFinished`'s parameter is an opaque compile-cycle token — NO error info; aggregate from assemblyCompilationFinished or read scriptCompilationFailed.
- [U2021.1–U6000.x | HIGH | UnityCsReference CompilationPipeline.cs] `assemblyCompilationStarted` is `[Obsolete]`; obsolete message itself warns these events "run async to actual compilation" — bad for time measurement.
- `!isCompiling` ≠ "new code live": false-"done" windows after failed compile AND between compile-end and reload-start [HIGH composition, §3 gap]; historical always-true/always-false wobbles [LOW | discussions 739571, 763929].
- [U2021.3–U6000.x | MED | https://discussions.unity.com/t/how-to-tell-that-compilation-exceptions-are-resolved-when-no-recompiling-occurred/1576369] Trap: reverting a file to last-compiled content can SKIP recompilation entirely (content hash) — no events fire at all.
- NOT FOUND: any official externally-observable "reload complete" signal.
- De-facto substitutes [MED | IvanMurzak wiki + CoplayDev #1173]: TCP listener drop (beforeAssemblyReload) + reconnect (new domain) IS the external signal; Editor.log markers `Begin MonoManager ReloadAssembly` + `Domain Reload Profiling` block [MED | issuetracker].
- **Editor.log marker caveat: per-OS log paths + `-logFile` override — see §11/§16.** If the editor was launched with `-logFile`, the default path is NOT written.
- State machine (each transition + observable):

```
EDIT ──Refresh()/RequestScriptCompilation──▶ COMPILING        signal: compilationStarted (old domain) [HIGH]
COMPILING ─▶ COMPILED-OK | COMPILED-ERROR                     signal: N× assemblyCompilationFinished(path, msgs)
                                                              then compilationFinished(ctx) — BOTH outcomes [HIGH+MED]
                                                              discriminator: scriptCompilationFailed / any Error msg [HIGH]
COMPILED-ERROR ─▶ terminal: NO reload; OLD assemblies live; no reload events fire [HIGH]
COMPILED-OK ─▶ RELOADING                                      signal: beforeAssemblyReload (close sockets HERE) [HIGH]
RELOADING ─▶ RELOADED (new code live)                         in-process: InitializeOnLoad → afterAssemblyReload (docs)
                                                                [DidReloadScripts position: community-only, LOW — §3]
                                                              external: TCP drop+reconnect / Editor.log markers [MED]
```

## §10 Pitfall catalog (T10)

| Pitfall | Cause | Detection | Mitigation |
|---|---|---|---|
| Old code runs after "successful" scripted Refresh [HIGH \| Manual/AssetDatabaseRefreshing] | step 3: script-invoked Refresh never reloads inline | no afterAssemblyReload since your Refresh | treat scripted Refresh as async; report done only after reload event |
| External write never compiles (unfocused editor) [HIGH \| same] | auto-refresh is focus+pref gated | isCompiling stays false | always scripted `Refresh()` from plugin |
| Errors in ONE asmdef block ALL assembly reloads [MED \| discussions/705717] | no reload while any assembly fails | error scan + missing reload event | gate "code updated" on ZERO total errors |
| .cs reimport → no fresh assembly [MED \| issuetracker assemblies-not-being-reloaded] | known issue (V3 C1) | behavior vs expected code path | `Refresh()` + `RequestScriptCompilation()` fallback; never ImportAsset-only |
| Editor hangs on "Reloading Script Assemblies" [MED \| discussions/907803] | failed/deadlocked reload (only reported fix: kill editor) | out-of-process watchdog timeout on TCP heartbeat | external watchdog + restart; never wait unbounded |
| Safe Mode: plugin silently absent [HIGH \| Manual/SafeMode] | "Safe Mode never allows managed code to run from your project, or its packages" — TCP server never loads | external only: port 9500 never opens | watchdog interprets "editor alive + port closed N min" as Safe Mode/compile failure |
| Play-mode refresh surprises [HIGH \| Manual/Preferences] | "Script Changes While Playing": Recompile-And-Continue (default; mid-play reload) / After-Finished / Stop-And-Recompile | unexpected reload event during play | follow Rider precedent: queue refresh until play exits [MED]; bug UUM-20409 mid-play recompile despite pref, fixed 2021.3.25f1 [HIGH] |
| AssetDatabase off main thread throws [HIGH \| Manual/job-system-overview] | main-thread-only APIs | exception in socket thread (often swallowed → looks silent) | marshal first: captured SyncContext.Post / `EditorApplication.update` queue / `delayCall` [MED]; `Awaitable.MainThreadAsync()` is U6000-only [HIGH] |
| Recursive import loops [HIGH \| OnPostprocessAllAssets page] | writing into Assets/ during import restarts refresh by design | console marker "An infinite import loop has been detected…" [MED] | write outside Assets/, copy in one guarded pass; check writability (Perforce read-only loop [MED]) |
| Burst reload stalls [HIGH \| burst@1.8 changelog] | pre-1.8.1 paid 250ms/reload; sync-compile + play-enter promote Burst to blocking foreground [MED] | "stuck on Domain Reload" with Burst threads [LOW] | pin Burst ≥1.8.4; CI: `UNITY_BURST_DISABLE_COMPILATION` |

- Safe Mode extras [U2021.3–U6000 | HIGH | https://docs.unity3d.com/6000.1/Documentation/Manual/SafeMode.html]: entered when opening a project with compile errors; auto-exits when errors resolved; exit-with-errors risks bad cached Library artifacts; batch mode auto-quits instead (unless `-ignoreCompilerErrors`).
- [U2021.3–U6000 | HIGH | https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorUtility.bindings.cs] `EditorUtility.isInSafeMode` is internal/reflection-only — and moot in-process (your code doesn't run in Safe Mode).
- NOT FOUND: programmatic Safe Mode exit (GUI button only).
- NOT FOUND: documented API/recipe to compare on-disk `Library/ScriptAssemblies` DLL freshness vs loaded assembly — community practice only, heuristic tier.

## §11 Per-OS deltas (T11)

- **Editor.log paths** [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/Manual/log-files.html]:
  - macOS: `~/Library/Logs/Unity/Editor.log`
  - Windows: `%LOCALAPPDATA%\Unity\Editor\Editor.log` (SYSTEM-account CI writes upm.log to `%ALLUSERSPROFILE%\Unity\Editor\`)
  - Linux: `~/.config/unity3d/Editor.log` (upm.log sits next to it on all OSes)
- [U2021.3–U6000.x | HIGH | https://docs.unity3d.com/6000.0/Documentation/Manual/EditorCommandLineArguments.html] `-logFile <path>` redirects the log — the default per-OS path is then NOT written; `-logFile -` → stdout. Resolve the override before tailing.
- [LOW | https://discussions.unity.com/t/my-editor-prev-log-is-over-8gb-can-i-safely-remove-it/99369] Rotation to `Editor-prev.log` on editor start is de-facto only (NOT FOUND in current docs; no size cap, community 8–40GB files) — when tailing, reopen by path, don't hold the fd (inode swap).
- **Linux:**
  - [U2021.3+U6000.4 | HIGH | https://docs.unity3d.com/6000.4/Documentation/Manual/system-requirements.html] "File systems are case sensitive" (verbatim, both doc lines); Wayland support is **experimental** with GPU-vendor caveats (V2 softening #4); X11 is the baseline.
  - [U6000.x | LOW | https://issuetracker.unity3d.com/issues/linux-having-same-case-insensitive-named-assets-causes-infinite-import-looping] Case-twin assets → infinite import loop.
  - [U6000.3.6–6000.3.8 | HIGH | https://issuetracker.unity3d.com/issues/linux-auto-refresh-fails-to-reimport-and-compile-script-changes-when-editing-files-outside-the-editor] **UUM-133944**: Auto Refresh stops reimporting external edits (Linux-only; fixed 6000.3.10f1/6000.4.0b10/6000.5.0a8).
  - [U2022.3.44–U6000.0.17 | HIGH | https://issuetracker.unity3d.com/issues/linux-editor-freezes-for-1-2-minutes-when-asset-database-is-refreshed] **UUM-79033**: 1–2min refresh freeze with huge `ulimit -n` hard limit (fixed 2022.3.55f1/6000.0.36f1) — pin sane `ulimit -n` (e.g. 4096), don't max it.
  - [U2020.1+ | MED | https://unity.com/releases/2020-1/editor-team-workflows] No Directory Monitoring (inference — only Windows ever mentioned, flagged).
- **Windows:**
  - [HIGH | https://learn.microsoft.com/en-us/windows/win32/winsock/using-so-reuseaddr-and-so-exclusiveaddruse] SO_REUSEADDR on Windows = port-hijack semantics (≠ BSD TIME_WAIT rebind); hardening = `SO_EXCLUSIVEADDRUSE`.
  - [HIGH | https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.exclusiveaddressuse] `Socket.ExclusiveAddressUse` default true on modern Windows; set before Bind; exclusive ports NOT immediately rebindable after close → budget ≥2s release-wait or retry-bind after reload (CoplayDev #1173 field evidence: 500ms too short [LOW]).
  - NOT FOUND: ExclusiveAddressUse semantics on Unix/Mono — treat as unspecified; retry-bind with backoff instead.
  - [U2021–U6000 | HIGH | https://docs.unity3d.com/Packages/com.unity.asset-store-validation@0.1/manual/path-length-validation.html] Package paths capped 140 chars because PackageCache expansion blows MAX_PATH(260) — keep Windows project roots short.
  - [HIGH | https://learn.microsoft.com/en-us/defender-endpoint/microsoft-defender-endpoint-antivirus-performance-mode] Defender mitigation = Dev Drive + performance mode. NOT FOUND: official Unity AV-exclusion guidance.
- **FS case-sensitivity** [HIGH | Apple Disk Utility doc + Unity sysreq]: default APFS (macOS) and NTFS = case-INsensitive; Linux ext4 = sensitive. Never generate case-twin files or do case-only renames via raw I/O (Windows "Moving file failed" regression 2020.3–2021.2 [HIGH]; macOS/Win "inconsistent casing" meta breakage [LOW]).
- **Symlinks**: unsupported/at-your-own-risk [LOW | support.unity.com]; documented hard consequence: Directory Monitoring auto-disabled when symlinks detected [HIGH | https://docs.unity3d.com/2021.1/Documentation/ScriptReference/AssetDatabase.IsDirectoryMonitoringEnabled.html].
- **`file:` paths** [U2021–U6000 | HIGH | https://docs.unity3d.com/Manual/upm-localpath.html]: absolute or relative-to-`Packages/`; forward slashes *preferred* (escaped backslashes legal on Windows — V2 softening #4); Windows absolute form `file:C:/...`. NOT FOUND: UNC/symlinked-target stance.
- **Process control** [HIGH | CLI docs]: graceful exit = `-quit`/`EditorApplication.Exit(code)`; `-quitTimeout` default 300s. NOT FOUND: official editor SIGTERM/SIGINT handling on any OS — don't rely on signals; after SIGKILL expect `Temp/UnityLockfile` cleanup [MED].
- **EditorPrefs storage** [HIGH | EditorPrefs page]: macOS `~/Library/Preferences/com.unity3d.UnityEditor5.x.plist`; Windows `HKCU\Software\Unity Technologies\Unity Editor 5.x`; Linux `~/.local/share/unity3d/prefs`.
- NOT FOUND: per-OS differences in Editor.log compile-marker *content* (Bee markers appear platform-neutral); mtime-granularity relevance to change detection (don't build on sub-second deltas).

## §12 Decision table — situation → correct reload action

| Situation | Action | Confirmation gate |
|---|---|---|
| Edited .cs inside `Assets/` | scripted `AssetDatabase.Refresh()` (works unfocused; ignores Auto Refresh pref AND DisallowAutoRefresh) | `compilationFinished` + `scriptCompilationFailed==false` + afterAssemblyReload/reconnect |
| Edited .cs inside `file:` UPM package | same — `Refresh()`; Packages/ mount is scanned (§4). Resolve/version-bump/lock-delete add NOTHING for content | same as above |
| Edited `package.json` of `file:` package (or .asmdef add/remove, deps) | `Client.Resolve()` **+** `Refresh()` — official "unknown scope → call both" (issue 1248326) | `Events.registeredPackages` (fires after refresh+compile+reload) |
| Added/deleted asset files (non-code or unknown set) | `Refresh()` (full scan adds+removes); `ImportAsset(path)` only when path known AND not load-bearing for compile (C1: never ImportAsset-only for .cs) | `isUpdating` settles + no new console errors |
| Need recompile with ZERO dirty scripts | `RequestScriptCompilation(CleanBuildCache)` — `None` is a silent no-op | confirm `assemblyCompilationFinished` actually fired, not `assemblyCompilationNotRequired` (IN-93874 no-op caveat; the no-op event doesn't exist on 2021.3) |
| Verify compile finished from OUTSIDE Unity process | no official signal (NOT FOUND, §9) — use TCP drop+reconnect handshake, state file, Editor.log markers (`Begin MonoManager ReloadAssembly`, `Domain Reload Profiling`, Bee/Csc lines), DLL freshness heuristic | at least two independent signals (both-signals gate) |
| Unity in Play mode | DON'T refresh — queue until play exits (Rider precedent); otherwise governed by "Script Changes While Playing" pref; default = mid-play domain reload | playmode state check before Refresh |
| Unity in Safe Mode | nothing works in-process (plugin never loaded); no programmatic exit exists | external watchdog: editor process alive + port closed → report Safe Mode/compile failure to user |
| Auto Refresh disabled by user | irrelevant for us: scripted `Refresh()` is unconditional (§5) | normal gates |
| Compile FAILED | terminal: NO reload, old assemblies live; `compilationFinished` still fired | `scriptCompilationFailed==true` / CompilerMessage errors → surface errors, do NOT report "synced"; next fix → new `Refresh()` |

## §13 Canonical `sync_unity` handshake (numbered, with races)

Hard constraint: C# command handlers are synchronous → **the wait-loop lives Python-side**; C# only triggers, signals, and persists.

1. External tool finishes file edits (all writes flushed; never mid-batch).
2. Python sends `sync` over TCP → C# main-thread dispatch → `AssetDatabase.Refresh()` (+ `Client.Resolve()` first if package metadata touched). Works unfocused, ignores Auto Refresh prefs AND DisallowAutoRefresh on all 3 OSes (§5). C# returns immediately: "refresh queued, epoch=N".
3. C# `CompilationPipeline.compilationStarted` → persist epoch/token in SessionState + state file "compiling".
4. C# `compilationFinished` → **fires on FAIL too** — must check `EditorUtility.scriptCompilationFailed` + aggregated `assemblyCompilationFinished` errors.
   - FAILED → write state "compile_failed" + errors; NO reload will happen; Python reports errors. Terminal.
   - OK → reload is coming (conditional-on-success, §9).
5. Conditional domain reload begins → `beforeAssemblyReload` (last code in old domain): send `going_away`, write state file "reloading", TCP listener teardown (cooperative thread exit — never rely on ThreadAbort reaching native `select`, §3).
6. [domain swap — managed state, SyncContext queue, and event handlers die]
7. New domain: `[InitializeOnLoad]` → rebind listener → `afterAssemblyReload` path writes state "ready". Python's reconnect = the external "reload complete" signal.
8. Out-of-band corroboration (both-signals gate): Editor.log `Begin MonoManager ReloadAssembly` + `Domain Reload Profiling` + Bee/Csc markers + `Library/ScriptAssemblies` DLL mtime heuristic (no official API — §10 NOT FOUND). Per-OS log paths + `-logFile` override: §11.
9. Python declares synced ONLY when: epoch matches AND state=ready AND reconnect succeeded AND zero compile errors.

**Known races (mark in implementation):**

- isCompiling false-gap — T3's 3 mechanisms (scripted-Refresh deferred reload; By-Design sync refocus compile invisible to flag; next-tick compile start) + T9's compile-end→reload-start window. Polling flags can NEVER prove done; events + epoch only.
- Failed-compile-no-reload: waiting for reconnect after a failed compile waits forever — step 4 gate is mandatory before any reload wait.
- Epoch/token need: a Refresh may be a no-op (already imported / content-hash revert skips compile entirely, §9) — without an epoch, Python can confuse a stale "ready" with the new cycle. Persist token in SessionState (survives reload, dies with editor — correct scope).
- False-"ready" window: writing "ready" from `compilationFinished`+delayCall can land BEFORE beforeAssemblyReload writes "reloading" (V3 row MCPServer.cs:80-88) — success-path "ready" must come from the post-reload path only.
- Unbounded reload hang (stuck "Reloading Script Assemblies", Burst stalls, Safe Mode) → Python-side timeout + watchdog, never infinite wait.

## §14 Implications for current codebase (V3, file:line)

| File:line | Research finding | Risk | Required change |
|---|---|---|---|
| `unity-plugin/Editor/CommandRouter.cs:267` | `recompile` = bare `Refresh()` (Default), returns `"ok"` synchronously; T1/T10: scripted Refresh queues compile async, never reloads inline; Default = no-op if already imported | LLM reads "ok" as "recompiled" while compile/reload hasn't started | Return state ("refresh queued — poll compile_status"); consider `ForceUpdate` |
| `server/src/unity_mcp/tools/scene.py:126` | Docstring: "recompile … and wait for completion (up to 60s)" — false; C# returns instantly, 60s is only socket timeout | Agent trusts completion that never happened | Fix docstring or chain `await_compile` internally |
| `server/src/unity_mcp/tools/code_intel.py:60-107` | `await_compile` polls `compile_status` only; T9: "idle" reads true in the compile-end→reload-start window and after FAILED compiles; docstring promises "compiling + reloading" but reload is never verified | False "compile clean" while old code live; next command eats the reload disconnect | Both-signals: also require state-file ≠ "reloading"/`going_away`→reconnect handshake before declaring done |
| `unity-plugin/Editor/CompileNotifier.cs:18-24` | `compilationFinished` fires on failure too (T9 CsRef); status "idle\|dur" after failed compile is indistinguishable from success-with-reload | Status alone can't discriminate; mitigated only because callers also fetch errors | Add fail marker (e.g. `EditorUtility.scriptCompilationFailed` → "idle-failed\|dur") |
| `unity-plugin/Editor/MCPServer.cs:80-88` | `compilationFinished`→`delayCall`→`WriteStateFile("ready")` can run in the T9 gap before `beforeAssemblyReload` writes "reloading" | Transient false-"ready" in state file → Python sends straight into reload | Defer success-path "ready" until after reload (write from afterAssemblyReload path / next StartAsync) |
| `unity-plugin/Editor/MCPServer.cs:130` | `ReuseAddress=true` on all platforms; T11: Windows SO_REUSEADDR = port-hijack semantics, hardening is `ExclusiveAddressUse`; CoplayDev#1173 fix used it | Foreign process can steal :9500 on Windows | `#if UNITY_EDITOR_WIN` → `ExclusiveAddressUse=true` + keep retry-bind (existing 500–2500ms ladder ≈7.5s satisfies T11 ≥2s budget); keep ReuseAddress on Unix |
| `unity-plugin/Editor/MCPServer.cs:474-485` | `OnBeforeReload`: going_away → state "reloading" → TeardownCore (listener closed) | — | None — matches T3/T8 best practice (confirmed OK) |
| `unity-plugin/Editor/MCPServer.cs:502-505` | "rename(2) atomic" comment — `File.Delete`+`File.Move` is NOT atomic on Windows | Reader can hit missing state file in delete-move window | Use `File.Replace`/`File.Move(overwrite:true)` on Windows |
| `unity-plugin/Editor/Chat/MCPChatWindow.Drain.cs:61-131` | `TryResumePendingTurn` has no isCompiling/isUpdating gate; fires from afterAssemblyReload while follow-up import/compile may be queued (T1 double-import) | Resumed turn races a second reload; ReloadGuard then defers it up to 120s watchdog | Gate on `!isCompiling && !isUpdating`, reschedule via delayCall (imperfect per T3, strictly better) |
| `unity-plugin/Editor/Chat/MCPChatWindow.Drain.cs:164-167` | `_needsRefresh` → `Refresh(ForceUpdate)` after EVERY code-edit result, mid-turn under lock; T6: import+compile proceed under lock → half-finished multi-file edits compile → phantom CS errors arm auto-fix at TurnDone | Spurious auto-fix turns; wasted compiles | Debounce: single Refresh at turn end (after unlock), or only on last edit of batch |
| `unity-plugin/Editor/Chat/ReloadGuard.cs:31,50` | Lock WITHOUT `DisallowAutoRefresh` (T6 risk 3: mid-turn focus-refresh imports under AI's feet); no SessionState marker / no `[InitializeOnLoad]` rebalance (T6 risk 1: native counter survives reload, managed `_lockDepth` + update-watchdog die with domain) | Wedged reload-lock with no in-process recovery after any lock-crossing reload | T6 safe pattern: `Disallow→Lock→try/finally{Unlock→Allow}` + `Refresh()` kick + SessionState lock-marker rebalanced in InitializeOnLoad |
| `unity-plugin/Editor/Chat/ChatProcess.cs:34-35` | Comment "unlock is handled by the guard itself on reload; the watchdog will also fire" — false across reload (both are managed state in the dying domain) | Misleads future maintainers into trusting nonexistent recovery | Fix comment; tie to ReloadGuard rebalance above |
| `server/src/unity_mcp/bridge_heartbeat.py` + `bridge.py` | **v0.78.5 RESOLVED** (3 fixes): **(1) `_reload_gate` (asyncio.Event):** `_send_with_retry()` no longer sleeps a fixed duration on `domain_reload` retries; clears gate when disconnected, then `asyncio.wait_for(gate.wait(), timeout=delay+jitter)` — wakes immediately when `_reconnect()` sets the gate. Race guard: gate only cleared `if not self.connected`. **(2) `_ping_stall_failures` counter:** When ping fails ≥3× but `is_process_dead()` returns False (Unity alive, heavy compile or App Nap), increments stall counter instead of force-closing. After 6 stall windows (~6 min) → force-close. Resets on DomainReloadError, ProtocolDesyncError, successful ping, confirmed-dead close, or reconnect. **(3) `connected` property simplified:** `self._writer is not None and not self._writer.is_closing()` — removed `select.select + MSG_PEEK` which caused false negatives and triggered unnecessary reconnects. | Low (resolved — behavior now matches T8 best-in-class ladders) | None — implemented in v0.78.5 |
| `server/src/unity_mcp/editor_log_parser.py:22-46` | Per-OS paths present and match T11 official list (darwin/win32/linux) — NOT macOS-hardcoded; reopen-per-call survives Editor-prev.log inode swap | Gap: `-logFile`-launched editors write nowhere near default path; only manual env override exists | Detect/document `-logFile` override (warn when log mtime ancient); optional Editor-prev.log fallback |

**Cross-platform (R23) gaps in current code (V3):**

- Python code is clean: no `osascript` / `open -a` / hardcoded `Library/Logs` outside the correct darwin branch of `editor_log_parser.py:37`; `compile_state.py:104` Bee lock path and `unity_state.py:32` state path use portable `Path` joins.
- C# keepalive (`MCPServer.cs:205-224`): per-OS `#if` branches for macOS/Win/Linux present — OK.
- Windows deltas to fix: `ReuseAddress` → `ExclusiveAddressUse`; `File.Delete+Move` non-atomic state-file write; no MAX_PATH guard for project paths (T11 140-char package cap — doc-level concern only).
- Linux deltas unhandled anywhere: case-sensitive FS (case-twin infinite-import loop), 6000.3.6–.8 Auto-Refresh regression (UUM-133944), ulimit refresh freeze (UUM-79033) — relevant only for docs/watchdog heuristics.
- Workflow docs are macOS-only: project `CLAUDE.md` `osascript … activate` recipe is unsupported folklore per T5 — the in-process `recompile` command (once semantics fixed) is the portable replacement.

## §15 Open questions & provisional-beta empirical tests (target: 6000.3.0b7)

1. `[OnCode*]` lifecycle attributes (T3) — 6000.5-docs-only; 6000.3 page 404s. Test: reference attribute in throwaway editor script on b7 → expect CS0246; keep `beforeAssemblyReload`/`[InitializeOnLoad]` canonical.
2. UUM-133944 b7 status (T11) — repro list starts 6000.3.6f1; beta status unknown (Linux-only). Test (Linux): 10× external-edit → focus (Auto Refresh=Enabled) → assert recompile each cycle.
3. IN-93874 CleanBuildCache no-op (T2) — single community report, no public tracker entry. Test on b7: `RequestScriptCompilation(CleanBuildCache)` with zero dirty scripts; assert `assemblyCompilationFinished` fires, not `assemblyCompilationNotRequired`.
4. UUM-40547 fix presence on b7 (T6) — "fixed in 6000.0.X" unspecific; repro included 6000.0.0b11. Test: Auto Refresh=Disabled, touch .cs, refocus, assert no compile starts.
5. End-to-end b7 smoke of the §13 handshake (external edit → scripted Refresh → compilationFinished → beforeAssemblyReload → reconnect after afterAssemblyReload) with Editor.log `Begin MonoManager ReloadAssembly` corroboration — 6000.3 docs describe final 6000.3.x; b7 may predate late fixes.
6. Re-pin T8 repo citations (§8, §17) from `@main` to commit SHAs when canonizing.
7. C2 ordering test (V3): if any handshake ever depends on `[DidReloadScripts]` vs `afterAssemblyReload` order, measure it empirically — not officially documented (§3).
8. Design-blocking NOT FOUNDs carried from T-reports:
   - `Refresh()` re-entrancy (refresh-during-refresh: queue vs merge, T1) — affects debounce design.
   - Exact post-Unlock reload timing (same stack vs next pump, T6) — affects ReloadGuard release sequencing.
   - No public reader for Lock/DisallowAutoRefresh counters (T6) — recovery must be marker-based (SessionState), not query-based.
   - No official external reload-complete signal (T9) — the both-signals gate is mandatory, not optional.
   - No compile-only CLI flag (T7) — headless gate stays `-executeMethod NoOp` + log grep.
   - No DLL-freshness comparison recipe (T10) — heuristic tier only, never the sole gate.

## §16 Per-OS matrix (R23)

**IDENTICAL on macOS / Windows / Linux** (the sync core is OS-independent because everything happens in-process via TCP — no focus hacks, no osascript):

- Scripted `Refresh()` semantics: unconditional on prefs/focus/DisallowAutoRefresh (§5).
- Event order: compilationStarted → assemblyCompilationFinished× → compilationFinished → [success] → beforeAssemblyReload → reload → InitializeOnLoad → afterAssemblyReload (§9).
- TCP handshake design (§13), SessionState/EditorPrefs APIs, conditional-reload-on-success rule, Safe Mode behavior, single-instance project lock.

**DIFFERS:**

| Axis | macOS | Windows | Linux |
|---|---|---|---|
| Editor.log | `~/Library/Logs/Unity/Editor.log` | `%LOCALAPPDATA%\Unity\Editor\Editor.log` | `~/.config/unity3d/Editor.log` |
| (all OS) | `-logFile` override → default path NOT written; `Editor-prev.log` rotation (de-facto, undocumented) — reopen by path when tailing | | |
| Directory Monitoring | absent (full-scan) | **only OS with it** (detection-only; auto-disabled by symlinks) | absent (inference) |
| Socket rebind after reload | immediate | `SO_EXCLUSIVEADDRUSE` + port not immediately rebindable → ≥2s wait / retry-bind; SO_REUSEADDR = hijack semantics | immediate; ulimit UUM-79033 freeze (fixed 2022.3.55f1/6000.0.36f1) |
| FS case | APFS default insensitive | NTFS insensitive | **sensitive** (case-twin import loop) |
| OS-specific bugs | reload-completion focus-steal (Mission Control) | case-only rename "Moving file failed" (2020.3–2021.2); MAX_PATH/140-char package cap; Defender scan cost | Auto-Refresh regression 6000.3.6–3.8 (fixed 6000.3.10f1); Wayland **experimental** |
| Headless binary | `/Applications/Unity/Hub/Editor/<v>/Unity.app/Contents/MacOS/Unity` | `C:\Program Files\Unity\Hub\Editor\<v>\Editor\Unity.exe` | docs `/opt/Unity/Hub/Editor/<v>/Editor/Unity`; real default `~/Unity/Hub/Editor` (configurable) |
| EditorPrefs storage | `~/Library/Preferences/com.unity3d.UnityEditor5.x.plist` | `HKCU\Software\Unity Technologies\Unity Editor 5.x` | `~/.local/share/unity3d/prefs` |

**PARAMETRIZE in our code** (vs handled in-process):

- Editor.log path table + `-logFile` override detection (already per-OS in `editor_log_parser.py`).
- `ExclusiveAddressUse` under `#if UNITY_EDITOR_WIN`; rebind backoff budget (Windows ≥2s).
- State-file atomic write (`File.Replace` on Windows).
- Headless binary path table (Linux via Hub config, not hardcoded).
- ulimit guidance + case-sensitivity warnings — docs/watchdog tier (Linux).
- Everything else rides the in-process TCP path and needs NO per-OS branches.

## §17 Source index (deduplicated, grouped by domain)

**docs.unity3d.com — Manual** (base `https://docs.unity3d.com/<ver>/Documentation/Manual/`):
AssetDatabaseRefreshing.html (2021.3, 6000.0/.1/.3) · AssetDatabaseBatching.html (2021.3) · domain-reloading.html (6000.0) · ConfigurableEnterPlayModeDetails.html (2021.3, 2022.3) · configurable-enter-play-mode-details.html (6000.6) · programming-code-lifecycle.html (6000.5) · preferences-asset-pipeline.html (2021.3, 6000.0/.3) · Preferences.html (2021.3, 6000.1) · SafeMode.html (6000.1/.4) · log-files.html / LogFiles.html (2021.3, current) · EditorCommandLineArguments.html (2021.3, 6000.0, current) · upm-concepts.html (6000.3) · upm-embed.html (6000.3) · cus-location.html (6000.3) · upm-conflicts-auto.html (6000.3) · upm-localpath.html · system-requirements.html (2021.3, 6000.4) · managed-code-debugging.html (6000.0) · async-awaitable-continuations.html (6000.0/.3) · job-system-overview.html (6000.2) · programming-best-practices.html (6000.3) · ImporterConsistency.html · ScriptCompilationAssemblyDefinitionFiles.html (2021.3) · roslyn-analyzers.html (2021.3)

**docs.unity3d.com — ScriptReference** (base `https://docs.unity3d.com/<ver>/Documentation/ScriptReference/`):
AssetDatabase.Refresh (2021.3, 6000.0/.4) · AssetDatabase.ImportAsset · AssetDatabase.ScheduleRefresh (2022.3 only) · AssetDatabase.StartAssetEditing · AssetDatabase.DisallowAutoRefresh (2021.3, 6000.0) · AssetDatabase.AllowAutoRefresh · AssetDatabase.IsDirectoryMonitoringEnabled (2021.1) · ImportAssetOptions (+ .ForceUpdate, .ForceSynchronousImport) · Compilation.CompilationPipeline.RequestScriptCompilation (2019.3, 2020.3, 2021.3, 6000.0/.3) · Compilation.RequestScriptCompilationOptions (2021.1, 2021.3) + .CleanBuildCache · Compilation.CompilationPipeline-assemblyCompilationNotRequired (2022.3) · -compilationStarted · -compilationFinished (6000.3) · -assemblyCompilationFinished (6000.3) · -codeOptimization (2020.1) · Compilation.AssemblyBuilder (2021.3, 2022.3, 2023.1) + .Build · Compilation.CompilerMessage · EditorApplication-isCompiling · -isUpdating · -focusChanged (2022.2/.3) · -delayCall · -update · EditorApplication.LockReloadAssemblies · EditorUtility-scriptCompilationFailed · EditorUtility.RequestScriptReload · AssemblyReloadEvents-beforeAssemblyReload · -afterAssemblyReload · InitializeOnLoadAttribute · Callbacks.DidReloadScripts (+ -ctor, 6000.3) · SessionState · EditorPrefs · PackageManager.Client.Resolve (2021.3, 6000.2) · PackageManager.Client.Add · PackageManager.Events-registeredPackages (6000.3) · AssetPostprocessor.OnPostprocessAllAssets (6000.1) · Awaitable.MainThreadAsync (6000.0)

**docs.unity3d.com — Packages**: com.unity.test-framework@1.4 manual/reference-command-line.html · com.unity.burst@1.8 changelog/CHANGELOG.html · com.unity.asset-store-validation@0.1 manual/path-length-validation.html

**github.com/Unity-Technologies/UnityCsReference**: Editor/Mono/Scripting/ScriptCompilation/CompilationPipeline.cs · Editor/Mono/Scripting/ScriptCompilation/EditorCompilation.cs · Editor/Mono/PreferencesWindow/AssetPipelinePreferences.cs (branches 2021.2, 2021.3, 2022.1, master) · Editor/Mono/EditorApplication.bindings.cs · Editor/Mono/EditorUtility.bindings.cs · Runtime/Export/Scripting/UnitySynchronizationContext.cs

**issuetracker.unity3d.com**: editorapplication-dot-iscompiling-is-not-called…refocusing-the-editor (By Design) · editor-is-frozen-on-reloading-domain…socket-dot-poll-1 (Won't Fix) · editor-gets-stuck…named-pipe · domain-reload-missing-when-entering-play-mode · auto-refresh-is-still-active…disable-in-the-preferences (UUM-40547) · code-coverage-windows…locks-assembly-reload-in-the-editor · lockreloadassemblies-is-not-working-correctly (U5) · packman-isnt-refreshed-when-calling-assetdatabase-dot-refresh (1248326, By Design) · assemblies-not-being-reloaded-when-reimporting-c-number-script-asset · script-recompiles-in-play-mode…recompile-after-finished-playing (UUM-20409) · recompile-after-finish-playmode-option-is-gone · editorutility-dot-scriptcompilationfailed-not-flagging-package…startup · editor-freezes-at-begin-monomanager-reloadassembly · linux-auto-refresh-fails-to-reimport (UUM-133944) · linux-editor-freezes-for-1-2-minutes…refreshed (UUM-79033) · linux-having-same-case-insensitive-named-assets · moving-file-failed…case (Windows) · long-visualscripting-path · unity-terminates-with-error-code-0…batch-mode · unity-server-run-time-does-not-respond-to-sigterm · an-infinite-import-loop-has-been-detected · a-polybrushmesh-with-an-assetpostprocessor…infinite-import-loop · unity-editor-gets-focused-on-mac…mission-control

**Repos (T8) — flagged: pin to commit SHA when canonizing (currently @main)**:
github.com/CoplayDev/unity-mcp (RefreshUnity.cs, StdioBridgeReloadHandler.cs, plugin_hub.py, unity_transport.py; issues #1173, #672; discussions #241) · github.com/CoderGamester/mcp-unity (RecompileScriptsTool.cs, McpUnityServer.cs, unityConnection.ts, mcpUnity.ts) · github.com/Arodoid/UnityMCP (UnityMCPConnection.cs, index.ts) · github.com/IvanMurzak/Unity-MCP (Assets.Refresh.cs, Startup.Editor.cs, wiki/Troubleshooting) · github.com/hatayama/unity-cli-loop (McpServerController.cs, DomainReloadRecoveryUseCase.cs, CompileUseCase.cs, CompilationLockService.cs, unity-discovery.ts) · github.com/AnkleBreaker-Studio/unity-mcp-server (not investigated) · github.com/inkle/ink-unity-integration (issues #137, #145) · github.com/microsoft/MSBuildForUnity · github.com/baba-s/unity-compile-in-background (archived) · github.com/needle-mirror/com.unity.burst (BurstILPostProcessor.cs)

**learn.microsoft.com**: system.appdomain.unload · winsock using-so-reuseaddr-and-so-exclusiveaddruse · system.net.sockets.socket.exclusiveaddressuse · fileio maximum-file-path-limitation · defender-endpoint antivirus-performance-mode (+ devblogs.microsoft.com/visualstudio/devdrive)

**Other official-adjacent**: jetbrains.com/help/rider/Refreshing_Unity_Assets.html · support.unity.com (115003118426 multiple-instances · project-open lockfile 40828087523092 · 208456906 exclude-scripts-symlinks · 46719618344852 build-automation failures) · support.apple.com Disk Utility file-system formats · unity.com/releases (2020-1 editor-team-workflows · 6000.3.0f1 notes) · game.ci docs (github/builder · github/test-runner · troubleshooting/common-issues)

**Community (LOW cap)**: discussions.unity.com t/572660 (InitializeOnLoad vs DidReloadScripts) · t/231945 · t/929827 · t/705717 (asmdef no-reload on error) · t/707907 · t/920018 · t/919319 · t/739571 · t/763929 · t/878449 · t/927783 · t/938010 · t/1649233 · t/903346 · t/1589996 (IN-93874) · t/1576369 · t/859164 · t/892301 (csproj staff) · t/607546 + t/248925 (lockfile) · t/715610 · threads/629140 (force-reload staff) · t/809214 · t/823112 · t/739406 · t/99369 (Editor-prev.log) · threads/912602 (packages-lock staff) · t/1692576 (Burst domain stall) · t/898620 · t/1688645 · t/680321 · t/632359 (EditorPrefs magic strings) · forum.unity.com threads/627952, /1368849, /446610 · answers.unity.com q/52013 · feedback.vrchat.com (isCompiling+isUpdating poll pattern) · blogs: hextantstudios.com/unity-log-compile-times · johnaustin.io domain-reloads · blog.s-schoener.com (Burst) · medium.com/openupm 2020.1 round-up · codedojo.com (project copies) · gio.blue (ILPP) · sdumetz.github.io (unix signals) · gitlab.com/game-ci issue 116 · git.cardiff.ac.uk unity-pong Bee artifacts · blog.unity.com incremental-build-pipeline
