// DiagnoseCommand — C8. Read-only atomic snapshot of all fact-signals.
// MUST NOT call TriggerSync or any mutating API (re-wedges the state machine).
// Registered in IsAllowedDuringCompile + IsAlwaysAllowed (C4).
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor
{
    internal static class DiagnoseCommand
    {
        // G29: enumerate dlls dynamically via CompilationPipeline so newly-added
        // asmdefs (e.g. Chat.Tests) are always included without manual list updates.
        // Returns dll file names (e.g. "UnityMCP.Editor.dll") for freshness check.
        internal static string[] GetKnownDlls()
        {
            try
            {
                var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
                var result = new string[assemblies.Length];
                for (int i = 0; i < assemblies.Length; i++)
                    result[i] = System.IO.Path.GetFileName(assemblies[i].outputPath);
                return result;
            }
            catch (Exception)
            {
                // Fallback to known-critical dlls if CompilationPipeline unavailable
                return new[] { "UnityMCP.Editor.dll", "UnityMCP.Editor.Tests.dll" };
            }
        }

        // NOTE: snapshot is NOT atomic across these reads (two reads of isCompiling possible).
        // Accepted limitation — Unity main thread guarantees no lock needed, but the boundary
        // between reads is open. Document; over-engineering with a snapshot struct would be
        // premature.
        public static string Execute(string args)
        {
            var sb = new StringBuilder();

            // mvid= — MVID half of ComputeStamp (IL identity)
            var stamp = SyncHelper.ComputeStamp();
            var mvid = string.IsNullOrEmpty(stamp) ? "UNDETERMINED"
                : stamp.Split(':')[0];
            sb.AppendLine($"mvid={mvid}");

            // stamp= — SyncHelper.CurrentDomainStamp
            var domainStamp = SyncHelper.CurrentDomainStamp;
            sb.AppendLine($"stamp={(!string.IsNullOrEmpty(domainStamp) ? domainStamp : "UNDETERMINED")}");

            // compile= — CompileNotifier.GetStatus() (e.g. "idle|3.2", "idle-failed|8.1")
            sb.AppendLine($"compile={CompileNotifier.GetStatus()}");

            // sync= — first 2 fields of GetSyncStatus (state + epoch). No TriggerSync call.
            var syncStatus = SyncHelper.GetSyncStatus();
            sb.AppendLine($"sync={ExtractSyncSummary(syncStatus)}");

            // iscompiling/cn_active/started/stamp_frozen — wedge fingerprint (C2 fields)
            // Read these directly for a standalone snapshot (GetSyncStatus only emits them
            // in the compiling state; here we always emit for completeness).
            bool isCompiling = EditorApplication.isCompiling;
            bool cnActive    = CompileNotifier.IsCompiling;
            bool started     = SessionState.GetBool(SyncHelper.CompileStartedKey, false);
            var  stampAtTrig = SessionState.GetString(SyncHelper.StampAtTriggerKey, "");
            bool stampFrozen = !string.IsNullOrEmpty(domainStamp) && domainStamp == stampAtTrig;
            sb.AppendLine(
                $"iscompiling={isCompiling.ToString().ToLower()}" +
                $"  cn_active={cnActive.ToString().ToLower()}" +
                $"  started={started.ToString().ToLower()}" +
                $"  stamp_frozen={stampFrozen.ToString().ToLower()}");

            // isReallyCompiling= — MCPServer event-driven flag (accurate; no stale post-reload latch)
            sb.AppendLine($"isReallyCompiling={MCPServer.IsReallyCompiling.ToString().ToLower()}");

            // dlls= — per-asmdef dll mtime + freshness
            sb.AppendLine($"dlls={BuildDllFreshness()}");

            // errors= — CompileErrorCapture.GetErrors() (survives reload via SessionState after C5)
            sb.AppendLine($"errors={CompileErrorCapture.GetErrors()}");

            // log= — Editor.log parse for CS codes
            sb.AppendLine($"log={ParseEditorLog()}");

            // main_mvid= — MVID of UnityMCP.Editor assembly (this assembly). Thread-safe.
            // Symmetrical with ReloadDiagnoseCommand / ReloadDomainStamp.MainAsmdefMvid().
            sb.AppendLine($"main_mvid={typeof(DiagnoseCommand).Assembly.ManifestModule.ModuleVersionId}");

            // C10: reload_failed= — detects "Reloading assemblies failed." or "Editor compiler errors found.
            // Will not reload assemblies." in the editor log. Python editor_log_parser.py already defines
            // BuildFailure.reload_failed; this C# signal lets Python consume it via wire format.
            sb.AppendLine($"reload_failed={DetectReloadFailed().ToString().ToLower()}");

            // substate= / port= / port_fallback= — WP8 Gap 4: status UX signals for agent diagnosis
            var subState = MCPStatusModel.GetSubState(
                isCompiling:   MCPServer.IsReallyCompiling,
                portMismatch:  MCPServer._portFallback,
                bindFailed:    MCPServer._lastWrittenState == "bind_failed",
                compileFailed: MCPServer._lastWrittenState == "compile_failed");
            sb.AppendLine($"substate={subState}");
            sb.AppendLine($"port={MCPServer.ServerPort}");
            sb.AppendLine($"port_fallback={MCPServer._portFallback.ToString().ToLower()}");

            // all_errors= — FIX-1: cross-asmdef compile errors with explicit CS codes
            // LAST field: multiline block must not be followed by single-line fields (Issue #53 Fix B)
            sb.AppendLine($"all_errors={SessionState.GetString(SyncHelper.AllAsmErrKey, "")}");

            return sb.ToString().TrimEnd();
        }

        // Extract "state  epoch=N" summary from full GetSyncStatus string
        static string ExtractSyncSummary(string syncStatus)
        {
            // format: epoch=N|state=X|... or epoch=N|state=compiling|dur=...|stamp=...|...
            // Return "state  epoch=N" for wire-format
            var parts = syncStatus.Split('|');
            string epochPart = "", statePart = "";
            foreach (var p in parts)
            {
                if (p.StartsWith("epoch=")) epochPart = p;
                if (p.StartsWith("state=")) statePart = p.Substring(6); // just the state value
            }
            return $"{statePart}  {epochPart}";
        }

        static string BuildDllFreshness()
        {
            var projectPath = UnityEngine.Application.dataPath;
            var projectRoot = Path.GetDirectoryName(projectPath) ?? "";
            var libPath     = Path.Combine(projectRoot, "Library", "ScriptAssemblies");

            // Single scan for Assets/ and Packages/ — O(N+M) total, not O(N*M) (Issue #53 Fix C)
            var assetsMap = ScanAssets(projectPath);
            var pkgsMap   = ScanPackages();

            var sbDlls = new StringBuilder();
            foreach (var dllName in GetKnownDlls())
            {
                var asmName = Path.GetFileNameWithoutExtension(dllName);
                var dllPath = Path.Combine(libPath, dllName);

                string srcDir;
                if (!assetsMap.TryGetValue(asmName, out srcDir))
                    pkgsMap.TryGetValue(asmName, out srcDir);

                var token = GetDllFreshnessToken(dllPath, srcDir ?? "");
                var mtime = File.Exists(dllPath)
                    ? new FileInfo(dllPath).LastWriteTimeUtc.Ticks : 0L;

                if (sbDlls.Length > 0) sbDlls.Append(',');
                sbDlls.Append($"{asmName}:{mtime}:{token}");
            }

            return sbDlls.Length > 0 ? sbDlls.ToString() : "none";
        }

        // Exposed internal for NUnit testing with injected temp paths.
        // Returns "fresh" | "stale" | "unknown(missing)" | "unknown(no-src)"
        internal static string GetDllFreshnessToken(string dllPath, string srcDir)
        {
            if (!File.Exists(dllPath)) return "unknown(missing)";
            if (string.IsNullOrEmpty(srcDir) || !Directory.Exists(srcDir)) return "unknown(no-src)";

            var dllMtime = new FileInfo(dllPath).LastWriteTimeUtc;
            var maxCsMtime = DateTime.MinValue;
            foreach (var cs in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            {
                // Unity ignores files/dirs with ~ prefix — exclude from freshness calculation (FIX-3)
                if (Path.GetFileName(cs).StartsWith("~")) continue;
                var t = new FileInfo(cs).LastWriteTimeUtc;
                if (t > maxCsMtime) maxCsMtime = t;
            }

            if (maxCsMtime == DateTime.MinValue) return "fresh"; // no .cs files → dll is up-to-date by definition
            if (dllMtime < maxCsMtime)
            {
                // Before declaring stale, check if Bee decided no recompile was needed.
                // compile="idle|X" (successful compile, not idle-failed) means Bee cache-hit:
                // content unchanged despite mtime bump. idle-never is NOT a cache-hit
                // (compilation never ran this session — cannot trust Bee).
                var status = CompileNotifier.GetStatus();
                if (status.StartsWith("idle|"))
                    return "fresh";
                return "stale";
            }
            return "fresh";
        }

        // Seam: resolves an asmdef filename to its directory via AssetDatabase Packages/ scan.
        // Returns null when not found. Injectable for NUnit — production impl calls AssetDatabase.
        internal static Func<string, string> FindInPackages = (asmdefFile) =>
        {
            var guids = AssetDatabase.FindAssets("t:asmdef", new[] { "Packages" });
            foreach (var guid in guids)
            {
                var virtualPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(virtualPath).Equals(asmdefFile, StringComparison.OrdinalIgnoreCase))
                    return Path.GetDirectoryName(Path.GetFullPath(virtualPath));
            }
            return null;
        };

        // Seam: scan Assets/ for all .asmdef files → name→dir map. Injectable for NUnit.
        // Production impl: one Directory.GetFiles call covering all of Assets/.
        internal static Func<string, System.Collections.Generic.Dictionary<string, string>> ScanAssets =
            (dataPath) =>
            {
                var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var f in Directory.GetFiles(dataPath, "*.asmdef", SearchOption.AllDirectories))
                        map[Path.GetFileNameWithoutExtension(f)] = Path.GetDirectoryName(f) ?? "";
                }
                catch (Exception) { }
                return map;
            };

        // Seam: scan Packages/ for all .asmdef assets → name→dir map. Injectable for NUnit.
        // Production impl: one AssetDatabase.FindAssets call (in-memory index, fast).
        internal static Func<System.Collections.Generic.Dictionary<string, string>> ScanPackages =
            () =>
            {
                var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var guids = AssetDatabase.FindAssets("t:asmdef", new[] { "Packages" });
                foreach (var guid in guids)
                {
                    var vp = AssetDatabase.GUIDToAssetPath(guid);
                    map[Path.GetFileNameWithoutExtension(vp)] = Path.GetDirectoryName(Path.GetFullPath(vp)) ?? "";
                }
                return map;
            };

        // Find the directory containing <asmName>.asmdef by scanning under dataPath.
        // Falls back to FindInPackages for file: UPM packages outside Assets/.
        internal static string FindAsmdefDir(string dataPath, string asmName)
        {
            var asmdefName = asmName + ".asmdef";
            try
            {
                foreach (var f in Directory.GetFiles(dataPath, "*.asmdef", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(f).Equals(asmdefName, StringComparison.OrdinalIgnoreCase))
                        return Path.GetDirectoryName(f) ?? "";
                }
            }
            catch (Exception) { /* permission or IO error — degrade gracefully */ }
            return FindInPackages(asmdefName) ?? "";
        }

        // C10: Detect reload-failed markers in Editor.log.
        // Matches the same substrings as Python editor_log_parser.py _RELOAD_FAILED / _RELOAD_ABORTED.
        // Returns true when the last reload terminal is a failure. Unity 6 logs
        // "Mono: successfully reloaded assembly" on successful reloads; older/editor-specific
        // paths may emit "Reload assemblies complete." instead, so either clears a stale failure.
        // Testable overload — accepts injected logPath for NUnit coverage.
        internal static bool DetectReloadFailed(string logPath)
        {
            const string reloadFailed    = "Reloading assemblies failed.";
            const string reloadAborted   = "Editor compiler errors found. Will not reload assemblies.";
            const string reloadComplete  = "Reload assemblies complete.";
            const string monoReloaded    = "Mono: successfully reloaded assembly";
            try
            {
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                    return false;
                string text;
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                    text = reader.ReadToEnd();

                // Find position of last failure marker
                int lastFailed  = text.LastIndexOf(reloadFailed,  StringComparison.Ordinal);
                int lastAborted = text.LastIndexOf(reloadAborted, StringComparison.Ordinal);
                int lastFailure = Math.Max(lastFailed, lastAborted);
                if (lastFailure < 0) return false;

                // If any supported success terminal appears after the last failure, it is stale.
                int lastSuccess = Math.Max(
                    text.LastIndexOf(reloadComplete, StringComparison.Ordinal),
                    text.LastIndexOf(monoReloaded, StringComparison.Ordinal));
                return lastSuccess < lastFailure;
            }
            catch (Exception) { return false; }
        }

        internal static bool DetectReloadFailed() => DetectReloadFailed(GetEditorLogPath());

        static string _cachedLogResult;
        static long   _cachedLogMtime;
        static string _cachedLogPath;

        static string ParseEditorLog()
        {
            try
            {
                var logPath = GetEditorLogPath();
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                    return "absent";

                // Mtime-guarded cache: skip re-read when file hasn't changed
                var mtime = new FileInfo(logPath).LastWriteTimeUtc.Ticks;
                if (logPath == _cachedLogPath && mtime == _cachedLogMtime && _cachedLogResult != null)
                    return _cachedLogResult;

                // Read full file (not tail — P1 fix teaches full-file is correct)
                string text;
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                    text = reader.ReadToEnd();

                return CacheAndReturn(logPath, mtime, ParseEditorLogText(text));
            }
            catch (Exception e)
            {
                return $"error:{e.GetType().Name}";
            }
        }

        internal static string ParseEditorLogText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "clean";

            const string failHeader    = "## Script Compilation Error for:";
            const string reloadComplete = "Reload assemblies complete.";
            const string monoReloaded   = "Mono: successfully reloaded assembly";

            var idx = text.LastIndexOf(failHeader, StringComparison.Ordinal);
            if (idx < 0) return "clean";

            var lastSuccess = Math.Max(
                text.LastIndexOf(reloadComplete, StringComparison.Ordinal),
                text.LastIndexOf(monoReloaded, StringComparison.Ordinal));
            if (lastSuccess > idx) return "clean";

            var block = text.Substring(idx, Math.Min(8192, text.Length - idx));
            var matches = Regex.Matches(block, @"error (CS\d+)");
            if (matches.Count == 0) return "clean";

            var seen = new System.Collections.Generic.HashSet<string>();
            var codes = new StringBuilder();
            foreach (Match m in matches)
            {
                var code = m.Groups[1].Value;
                if (seen.Add(code))
                {
                    if (codes.Length > 0) codes.Append(' ');
                    codes.Append(code);
                }
            }
            return codes.ToString();
        }

        static string CacheAndReturn(string logPath, long mtime, string result)
        {
            _cachedLogPath   = logPath;
            _cachedLogMtime  = mtime;
            _cachedLogResult = result;
            return result;
        }

        static string GetEditorLogPath()
        {
            // macOS: ~/Library/Logs/Unity/Editor.log
            // Windows: %LOCALAPPDATA%\Unity\Editor\Editor.log
            // Linux: ~/.config/unity3d/Editor.log
            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                return Path.Combine(home, "Library", "Logs", "Unity", "Editor.log");
            }
            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor)
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, "Unity", "Editor", "Editor.log");
            }
            // Linux
            var linuxHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(linuxHome, ".config", "unity3d", "Editor.log");
        }
    }
}
