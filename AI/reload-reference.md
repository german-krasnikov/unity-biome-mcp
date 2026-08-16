# Unity Compilation and Reload — Research and Current Contract

Sections §1–§12 preserve a research snapshot verified on 2026-06-10 for
Unity 2021.3 through the Unity 6 versions cited inline. They are the dated design
basis for the implementation, not a live task list or a substitute for the
current contract in §13.

Claim format: `[U<ver>–U<ver> | HIGH/MED/LOW | URL]`. HIGH means official
documentation or UnityCsReference; MED means an official-derived inference or
staff statement; LOW means a community source. `NOT FOUND` remains explicitly
unverified. The source trust order is official Unity documentation and source,
then staff-triaged issue reports, Unity staff statements, and community material.

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
- [U2021.3–U6000.x | LOW | https://discussions.unity.com/t/initializeonload-vs-didreloadscripts/572660] Community-only: InitializeOnLoad always before DidReloadScripts. Do not make a handshake depend on this ordering without new empirical evidence; the current contract does not.
- [U6000.5+ | HIGH | https://docs.unity3d.com/6000.5/Documentation/Manual/programming-code-lifecycle.html] Unity 6.5+ adds `[OnCodeDeinitializing]`/`[OnCodeUnloading]`/`[OnCodeLoaded]`/`[OnCodeInitializing]`; these APIs were outside the 6000.3 target evaluated by the research snapshot.
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

## §8 MCP prior-art survey (T8)

The repository links in this dated survey pointed at their default branches when
captured. Re-verify their current source before relying on an implementation
detail.

| Repo | Refresh trigger | Reload survival | Reconnect | Steal |
|---|---|---|---|---|
| CoplayDev/unity-biome-mcp | `Refresh(ForceUpdate\|ForceSynchronousImport)` + optional `RequestScriptCompilation`; returns state, client polls | EditorPrefs resume-flag + heartbeat file "reloading" + 6-step retry ladder (0→1→3…30s) | Python waits ≤20s for session; in-flight → `hint="retry"` | heartbeat-during-reload; resume ladder. Beware their #1173 Windows socket race |
| CoderGamester/mcp-unity | `RequestScriptCompilation()` + respond inside `compilationFinished` (pre-reload, socket still alive) | delayCall-scheduled restart; no state | WS exp backoff 1–30s + jitter, cap 50; play-mode 3s poll; queue+replay commands | reply-before-reload; queue/replay |
| Arodoid/UnityMCP | none | `[InitializeOnLoad]` rebirth + naive 5s loop | in-flight hangs | anti-pattern baseline — skip |
| IvanMurzak/Unity-MCP | `Refresh(ForceSynchronousImport)`; if isCompiling → `Processing`+requestId, push result post-compile | explicit disconnect-before/reconnect-after | SignalR retry; KeepConnected | two-phase Processing/follow-up for reload-crossing ops |
| hatayama/unity-cli-loop (uLoopMCP) | refresh-then-compile via CompileUseCase | session (port/projectRoot/sessionId) persisted; compile result persisted by requestId; **compile lock-FILE on compilationStarted/Finished** = out-of-band signal | TS poller fast→slow; 2s grace on loss; tools-changed notify after recovery | best-in-class: full DomainReloadRecoveryUseCase + result persistence + lock-file |

- Universal pattern [HIGH, all repo sources]: **nobody keeps the socket alive through reload** — stop-before/restart-after + external-side retry, everywhere.
- Differentiator 1: does the in-flight request get a real answer (respond-pre-reload / Processing+push / persist-by-requestId / retry-hint punt)?
- Differentiator 2: is reload state persisted (EditorPrefs flag, session port file) vs re-derived from `[InitializeOnLoad]`?
- NOT FOUND: any `LockReloadAssemblies` use across all 5 repos (uLoopMCP's "CompilationLockService" is a lock *file*).
- [LOW | https://github.com/CoplayDev/unity-biome-mcp/issues/1173] Field lesson: Windows TcpListener leak across reload — 500ms release-wait too short (fix was 2000ms + `ExclusiveAddressUse=true` + `listener?.Server?.Dispose()` in beforeAssemblyReload); silent port-fallback masked it while the client stayed on the old port.
- [LOW | https://github.com/AnkleBreaker-Studio/unity-biome-mcp-server] Sixth candidate located, not investigated.

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
- **Editor.log marker caveat: per-OS log paths + `-logFile` override — see §11.** If the editor was launched with `-logFile`, the default path is NOT written.
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

## §13 Current `sync_unity` Contract

The public agent API is
`sync_unity(resolve=False, bump=False, timeout=SESSION_TIMEOUT)` in
`server/src/unity_mcp/tools/sync.py`. Agents should call it after external C#
or package changes; they should not call internal recovery commands directly.

1. When `bump=True`, Python increments the source checkout's plugin patch
   version at most once per connection session and implies `resolve=True`.
   Standalone installs without the repository package manifest reject this mode.
2. Python reads the current domain stamp when available, then sends the C#
   `sync` command. `resolve=True` asks `SyncHelper` to call
   `Client.Resolve()` before refresh.
3. `SyncHelper.TriggerSync()` allocates an epoch in `SessionState`, marks the
   state compiling, optionally resolves packages, refreshes assets, requests
   script compilation, and starts its tick pump. It returns
   `sync_ack|epoch=N|will_compile=<bool>`.
4. If Unity reports that no compile is needed, Python still checks corroborated
   compile errors and warms the type cache before returning a clean no-op result.
5. Otherwise Python polls `sync_status` until the same epoch is ready or failed.
   Stale epochs are ignored. Connection loss and `DomainReloadError` during the
   expected domain swap are retried within the caller's deadline.
6. A failed state returns compile evidence; it is never reported as synced. A
   timeout returns a stop/manual-recovery result rather than a success.
7. A ready state is checked for compile errors. When an expected compile leaves a
   comparable main-assembly MVID frozen, Python invokes bounded internal recovery
   before it may report success.

`unity-plugin/Editor/SyncHelper.cs` owns the persisted epoch/state/error/stamp
machine and compilation/reload event handlers. A ready state may come from the
verified no-compile path, clean self-heal guards, or a completed domain reload;
a compile failure stays terminal for that epoch.

## §14 Recovery Boundary

`server/src/unity_mcp/tools/reload_ladder.py` is internal recovery plumbing.
It may use the main listener or the independent reload listener to diagnose the
domain, request refresh/recompile, resolve package state, or return an actionable
manual result. Platform focus automation is optional. The play-stop tier requires
explicit consent that the public `sync_unity` wrapper does not expose.

Internal C# commands such as `force_refresh` and `force_play_stop` may be
named when explaining architecture, but must not appear as direct agent workflow
steps. Recovery success requires observed state/evidence; an acknowledged
command alone is not proof that new code is live.

## §15 Maintenance

- Treat §§1–§12 as dated research. Re-verify a cited Unity version or platform
  before extending a claim beyond its stated range.
- Update §13 when `tools/sync.py` or `SyncHelper.cs` changes the public
  handshake. Update §14 when the internal ladder or reload package changes.
- Keep shipped work and release history in `CHANGELOG.md`, tests and evidence in
  `AI/testing.md`, and user recovery instructions in `docs/`.
- Do not add file:line audit tables, future-task backlogs, or duplicated OS/source
  indexes here.
