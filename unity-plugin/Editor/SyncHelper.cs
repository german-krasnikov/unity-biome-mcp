// SyncHelper — epoch, trigger, events, ISyncOps seam, IsCompileClean, domain stamp. (v0.23)
// public everywhere: Tests.dll must access all of this (CS0122 trap).
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class SyncHelper
    {
        private const string EpochKey          = "MCP_SyncEpoch";
        private const string CleanKey          = "MCP_SyncClean";
        private const string StateKey          = "MCP_SyncState";  // idle|compiling|ready|failed
        private const string ErrKey            = "MCP_SyncError";
        private const string TriggerTimeKey    = "MCP_SyncTriggerTime";
        // internal (not private): DiagnoseCommand.cs reads these directly instead
        // of re-typing the literal (DRY audit issues-23-29 Cat.3), same pattern as AllAsmErrKey below.
        internal const string CompileStartedKey = "MCP_SyncCompileStarted";
        private const string StampKey          = "MCP_DomainStamp";
        internal const string StampAtTriggerKey = "MCP_StampAtTrigger";
        internal const string AllAsmErrKey     = "MCP_AllCompileErrors";

        // RC-2 fix: lowered from 10s to 3s — self-heal is only needed for the
        // genuine no-op path (RC-8); masking a real failure after 10s caused false-greens.
        // D5: isCompiling checked AFTER RequestScriptCompilation (not same-frame as Refresh).
        public const double SelfHealGraceSeconds = 3.0;

        // Injectable clock (timeSinceStartup survives domain reload, dies with the
        // editor — same lifetime as SessionState, so the pair stays consistent).
        public static Func<double> NowSeconds = () => EditorApplication.timeSinceStartup;

        // --- State ---
        public static int    CurrentEpoch       => SessionState.GetInt(EpochKey, 0);
        public static bool   IsCompileClean     => SessionState.GetBool(CleanKey, true);
        // RC-1/RC-5: build fingerprint = MVID:mtime, captured only in afterAssemblyReload.
        // Empty string means "no reload has happened in this Unity session yet".
        public static string CurrentDomainStamp => SessionState.GetString(StampKey, "");

        // --- Events ---
        public static event Action         OnSyncComplete;
        public static event Action<string> OnSyncFailed;

        // --- Injectable seam ---
        public static ISyncOps Ops { get; set; } = new UnitySyncOps();

        static SyncHelper()
        {
            CompilationPipeline.compilationStarted          += _ => OnCompileStarted();
            CompilationPipeline.compilationFinished         += _ => OnCompileFinished();
            CompilationPipeline.assemblyCompilationFinished += OnAsmCompilationFinished;
            AssemblyReloadEvents.afterAssemblyReload        += OnAfterReload;

            // Bootstrap: seed stamp on first load — afterAssemblyReload does not fire on Unity startup.
            if (string.IsNullOrEmpty(SessionState.GetString(StampKey, "")))
            {
                var s = ComputeStamp();
                if (!string.IsNullOrEmpty(s)) SessionState.SetString(StampKey, s);
            }
        }

        // --- Called from CommandRouter ---
        public static string TriggerSync(bool resolve)
        {
            // C3: re-wedge guard — if already in compiling state with no new compile activity,
            // do NOT bump epoch (that would re-wedge the state machine).
            // Conditions: state==compiling AND compile actually started AND stamp frozen AND NOT IsCompiling
            var curState = SessionState.GetString(StateKey, "idle");
            if (curState == "compiling"
                && SessionState.GetBool(CompileStartedKey, false)
                && CurrentDomainStamp == SessionState.GetString(StampAtTriggerKey, "___")
                && !Ops.IsCompiling)
            {
                return $"wedged|epoch={CurrentEpoch}";
            }

            var epoch = CurrentEpoch + 1;
            SessionState.SetInt(EpochKey, epoch);
            SessionState.SetString(StateKey, "compiling");
            SessionState.SetBool(CleanKey, false);
            SessionState.SetFloat(TriggerTimeKey, (float)NowSeconds());
            SessionState.SetBool(CompileStartedKey, false);

            if (resolve) Ops.Resolve();
            Ops.Refresh();

            // RC-6 fix: RequestScriptCompilation forces the compile even when Unity
            // is backgrounded (dur=0 bug on macOS).
            // Tier-0: self-re-arming tick-pump keeps nudging the editor loop until
            // compilation actually starts (fixes the backgrounded-editor stall).
            Ops.RequestScriptCompilation(RequestScriptCompilationOptions.None);
            Ops.StartTickPump();

            // Stamp self-heal: snapshot current stamp so GetSyncStatus can detect
            // a domain reload that happened without OnAfterReload firing for us.
            SessionState.SetString(StampAtTriggerKey, CurrentDomainStamp);

            // RC-8 fix: read isCompiling AFTER the above calls, not same-frame as Refresh.
            var willCompile = Ops.IsCompiling || Ops.IsUpdating;
            if (!willCompile && !Ops.ScriptCompilationFailed)
            {
                // Refresh+RequestScriptCompilation was a no-op — no compile will happen.
                // G7: only force-green when build is actually clean; a FAILED build must
                // never be reported as ready/green (force-green hole).
                SessionState.SetString(StateKey, "ready");
                SessionState.SetBool(CleanKey, true);
                CompileNotifier.ClearFailed();
            }
            return $"sync_ack|epoch={epoch}|will_compile={willCompile.ToString().ToLower()}";
        }

        public static string GetSyncStatus()
        {
            var epoch = CurrentEpoch;
            var state = SessionState.GetString(StateKey, "idle");
            var stamp = CurrentDomainStamp;
            if (state == "failed")
            {
                var err = SessionState.GetString(ErrKey, "");
                return $"epoch={epoch}|state=failed|err={err}|stamp={stamp}";
            }
            if (state == "compiling")
            {
                var started = SessionState.GetBool(CompileStartedKey, false);
                var elapsed = NowSeconds() - SessionState.GetFloat(TriggerTimeKey, 0f);
                // RC-2 fix: self-heal only on genuine no-compile path (grace=3s).
                // If compile started, we wait for reload/failed — never force-green.
                if (!started && !MCPServer.IsReallyCompiling && !Ops.IsUpdating
                    && elapsed > SelfHealGraceSeconds
                    && !Ops.ScriptCompilationFailed)
                {
                    // G7: grace-period self-heal only allowed on clean build.
                    // A FAILED build must not be self-healed to ready.
                    SessionState.SetString(StateKey, "ready");
                    SessionState.SetBool(CleanKey, true);
                    CompileNotifier.ClearFailed();
                    return $"epoch={epoch}|state=ready|stamp={stamp}";
                }
                // Stamp self-heal: if domain stamp changed since TriggerSync AND no real
                // compile is in progress, a reload happened without our epoch callback firing.
                // Guard: only heal when a sync was actually started (snapshot is non-empty).
                var stampAtTrigger = SessionState.GetString(StampAtTriggerKey, "");
                if (started && !string.IsNullOrEmpty(stampAtTrigger)
                    && CurrentDomainStamp != stampAtTrigger && !Ops.IsCompiling
                    && !Ops.ScriptCompilationFailed)
                {
                    SessionState.SetString(StateKey, "ready");
                    SessionState.SetBool(CleanKey, true);
                    CompileNotifier.ClearFailed();
                    return $"epoch={epoch}|state=ready|stamp={stamp}";
                }
                var dur = CompileNotifier.ElapsedSeconds;
                // C2: wedge fingerprint — four discriminators for Python 3-way classifier + diagnose
                bool isCompiling  = Ops.IsCompiling;
                bool cnActive     = CompileNotifier.IsCompiling;
                bool stampFrozen  = (CurrentDomainStamp == stampAtTrigger);
                return $"epoch={epoch}|state=compiling|dur={dur.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}|stamp={stamp}" +
                       $"|iscompiling={isCompiling.ToString().ToLower()}|cn_active={cnActive.ToString().ToLower()}" +
                       $"|started={started.ToString().ToLower()}|stamp_frozen={stampFrozen.ToString().ToLower()}";
            }
            return $"epoch={epoch}|state={state}|stamp={stamp}";
        }

        // --- Test seams (called from tests; in production, called via Unity events) ---
        public static void SimulateCompilationStarted() => OnCompileStarted();
        public static void SimulateCompilationFinished() => OnCompileFinished();
        public static void SimulateAfterAssemblyReload() => OnAfterReload();

        public static void ResetForTest()
        {
            SessionState.EraseInt(EpochKey);
            SessionState.EraseBool(CleanKey);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ErrKey);
            SessionState.EraseFloat(TriggerTimeKey);
            SessionState.EraseBool(CompileStartedKey);
            SessionState.EraseString(StampAtTriggerKey);
            SessionState.EraseString(AllAsmErrKey);
            // StampKey intentionally NOT erased: stamp is written only by OnAfterReload
            // (real domain reload). Erasing it here wipes the live stamp between test runs
            // and breaks get_version until the next reload (HOLE-1b). Tests that need a
            // specific stamp value call SimulateAfterAssemblyReload() explicitly.
            NowSeconds = () => EditorApplication.timeSinceStartup;
            OnSyncComplete = null;
            OnSyncFailed   = null;
        }

        // --- Private handlers ---
        private static void OnCompileStarted()
        {
            SessionState.SetBool(CleanKey, false);
            SessionState.SetString(StateKey, "compiling");
            SessionState.SetBool(CompileStartedKey, true);
            SessionState.EraseString(AllAsmErrKey);
            // C3: seed stamp baseline so Play-initiated reloads self-heal via stamp-heal.
            // Only seed if no TriggerSync has already written it (non-empty = TriggerSync was called).
            if (string.IsNullOrEmpty(SessionState.GetString(StampAtTriggerKey, "")))
                SessionState.SetString(StampAtTriggerKey, CurrentDomainStamp);
        }

        private static void OnCompileFinished()
        {
            if (Ops.ScriptCompilationFailed)
            {
                SessionState.SetString(StateKey, "failed");
                var err = "script compilation failed";
                SessionState.SetString(ErrKey, err);
                OnSyncFailed?.Invoke(err);
            }
            // Success: don't write "ready" here — only afterAssemblyReload may (R-4 fix).
        }

        // FIX-1: accumulate compile errors per UnityMCP.* assembly with explicit CS codes.
        private static void OnAsmCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            var asmName = System.IO.Path.GetFileNameWithoutExtension(assemblyPath);
            if (!asmName.StartsWith("UnityMCP.")) return;

            var sb = new System.Text.StringBuilder(SessionState.GetString(AllAsmErrKey, ""));
            foreach (var msg in messages)
            {
                if (msg.type != CompilerMessageType.Error) continue;
                var csCode = ExtractCsCode(msg.message);
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"{asmName}:{csCode}:{msg.file}:{msg.line}: {msg.message}");
            }
            if (sb.Length > 0)
                SessionState.SetString(AllAsmErrKey, sb.ToString());
        }

        private static string ExtractCsCode(string message)
        {
            var m = System.Text.RegularExpressions.Regex.Match(message, @"CS\d+");
            return m.Success ? m.Value : "CS0000";
        }

#if UNITY_INCLUDE_TESTS
        public static void SimulateAsmCompilationFinished(string assemblyPath, CompilerMessage[] msgs)
            => OnAsmCompilationFinished(assemblyPath, msgs);
#endif

        private static void OnAfterReload()
        {
            // RC-1 fix: capture build fingerprint = MVID:mtime.
            // MVID changes on every recompile; mtime provides wall-clock ordering.
            // Stored in SessionState (survives domain reloads; NOT EditorPrefs/static field).
            var stamp = ComputeStamp();
            if (!string.IsNullOrEmpty(stamp))
                SessionState.SetString(StampKey, stamp);

            // C1 fix: don't write ready/clean if script compilation actually failed.
            // scriptCompilationFailed can be true even in afterAssemblyReload (stale IL loaded).
            bool ok = !Ops.ScriptCompilationFailed;
            SessionState.SetBool(CleanKey, ok);
            SessionState.SetString(StateKey, ok ? "ready" : "failed");
            if (ok) OnSyncComplete?.Invoke();
            else    OnSyncFailed?.Invoke("script compilation failed");
        }

        internal static string ComputeStamp()
        {
            var sb = new System.Text.StringBuilder();
            long maxMtime = 0;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = asm.GetName().Name ?? "";
                if (!name.StartsWith("UnityMCP.")) continue;
                sb.Append(asm.ManifestModule.ModuleVersionId.ToString("N")).Append(';');
                var loc = asm.Location;
                if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                {
                    var t = new FileInfo(loc).LastWriteTimeUtc.Ticks;
                    if (t > maxMtime) maxMtime = t;
                }
            }
            return sb.Length == 0 ? "" : $"{sb}:{maxMtime}";
        }
    }

    // ── Interface (public for CS0122) ─────────────────────────────────────────

    public interface ISyncOps
    {
        void Refresh();
        void Resolve();
        void ImportPackageSources();      // CLASS-A: targeted ImportAsset per .cs, bypasses dead watcher
        void RequestScriptCompilation(RequestScriptCompilationOptions opts);  // RC-6: force compile even when backgrounded
        void StartTickPump();             // Tier-0: self-re-arming update-loop until IsCompiling||budget
        bool IsCompiling { get; }
        bool IsUpdating  { get; }
        bool ScriptCompilationFailed { get; }
    }

    // ── Production impl (public for CS0122) ──────────────────────────────────

    public sealed class UnitySyncOps : ISyncOps
    {
        // Anti-runaway: unsubscribe after this many editor ticks if compile never starts.
        public const int TickBudget = 300;

        // Bee mvfrm nuke: delete .mvfrm marker files so Bee unconditionally recompiles.
        // API approaches (ImportAsset, CleanBuildCache) don't propagate to Bee's dirty-tracking.
        public void ImportPackageSources()
        {
            var beePath = Path.Combine(Application.dataPath, "../Library/Bee");

            // Step 1: Delete UnityMCP .mvfrm files → Bee unconditionally re-runs Csc
            var artifactsPath = Path.Combine(beePath, "artifacts");
            if (Directory.Exists(artifactsPath))
                foreach (var dag in Directory.GetDirectories(artifactsPath))
                    foreach (var f in Directory.GetFiles(dag, "UnityMCP*.mvfrm"))
                        File.Delete(f);

            // (digestcache deletion removed — corrupts Bee artifact graph; ForceUpdate flag is sufficient)

            // Step 2: Refresh tells Unity to invoke Bee
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        // Tier-0: ForceUpdate defeats Bee "inputs unchanged" gate; ForceSynchronousImport blocks until done.
        public void Refresh()                  => AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        public void Resolve()                  => UnityEditor.PackageManager.Client.Resolve();
        // None instead of CleanBuildCache: Unity 6.x regression — CleanBuildCache fires
        // assemblyCompilationNotRequired instead of recompiling. Per-file ForceUpdate
        // (in ImportPackageSources) already defeats Bee's "inputs unchanged" gate.
        public void RequestScriptCompilation(RequestScriptCompilationOptions opts) => CompilationPipeline.RequestScriptCompilation(opts);

        // Self-re-arming tick-pump: keeps nudging QueuePlayerLoopUpdate every editor tick
        // until IsCompiling becomes true (backgrounded editor started compiling) OR the
        // tick budget is exhausted (anti-runaway). Unsubscribes itself on stop condition.
        public void StartTickPump()
        {
            int remaining = TickBudget;
            EditorApplication.CallbackFunction pump = null;
            pump = () =>
            {
                EditorApplication.QueuePlayerLoopUpdate();
                remaining--;
                if (EditorApplication.isCompiling || remaining <= 0)
                    EditorApplication.update -= pump;
            };
            EditorApplication.update += pump;
        }

        public bool IsCompiling           => EditorApplication.isCompiling;
        public bool IsUpdating            => EditorApplication.isUpdating;
        public bool ScriptCompilationFailed => EditorUtility.scriptCompilationFailed;
    }
}
