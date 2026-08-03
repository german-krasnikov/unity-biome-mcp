// ReloadDiagnoseCommand — read-only snapshot of all reload/compile signals.
// Ported from unity-plugin/Editor/DiagnoseCommand.cs (lines 1-253).
// Deps replaced:
//   SyncHelper.ComputeStamp()       → ReloadDomainStamp.ComputeStamp()
//   SyncHelper.CurrentDomainStamp   → ReloadDomainStamp.MainDomainStamp
//   CompileNotifier.*               → ReloadCompileNotifier.*
//   CompileErrorCapture.GetErrors() → SessionState.GetString("MCP_CompileErrors", "")
//   SyncHelper.GetSyncStatus()      → SessionState read-only (sync=unknown when main absent)
// All types public: CS0122 trap avoided.
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Reload
{
    public static class ReloadDiagnoseCommand
    {
        public static string Execute(string args = "")
        {
            var sb = new StringBuilder();

            // mvid= — from reload assembly stamp
            var stamp = ReloadDomainStamp.ComputeStamp();
            var mvid = string.IsNullOrEmpty(stamp) ? "UNDETERMINED" : stamp.Split(':')[0];
            sb.AppendLine($"mvid={mvid}");

            // stamp= — main package domain stamp (from volatile cache, thread-safe)
            var domainStamp = ReloadCompileNotifier.CachedDomainStamp;
            sb.AppendLine($"stamp={(!string.IsNullOrEmpty(domainStamp) ? domainStamp : "UNDETERMINED")}");

            // main_mvid= — UnityMCP.Editor assembly MVID via reflection (thread-safe)
            sb.AppendLine($"main_mvid={ReloadDomainStamp.MainAsmdefMvid()}");

            // compile= — from volatile cache (thread-safe)
            sb.AppendLine($"compile={ReloadCompileNotifier.CachedStatus}");

            // sync= — from volatile cache (thread-safe)
            sb.AppendLine($"sync={ReloadCompileNotifier.CachedSyncState}  epoch={ReloadCompileNotifier.CachedSyncEpoch}");

            // iscompiling/cn_active/started/stamp_frozen — wedge fingerprint (C2 fields)
            bool isCompiling = ReloadCompileNotifier.CachedIsCompiling;
            bool cnActive    = ReloadCompileNotifier.CachedCnActive;
            bool started     = ReloadCompileNotifier.CachedStarted;
            var  stampAtTrig = ReloadCompileNotifier.CachedStampAtTrigger;
            bool stampFrozen = !string.IsNullOrEmpty(domainStamp) && domainStamp == stampAtTrig;
            sb.AppendLine(
                $"iscompiling={isCompiling.ToString().ToLower()}" +
                $"  cn_active={cnActive.ToString().ToLower()}" +
                $"  started={started.ToString().ToLower()}" +
                $"  stamp_frozen={stampFrozen.ToString().ToLower()}");

            // dlls= — per-asmdef dll mtime + freshness
            sb.AppendLine($"dlls={BuildDllFreshness()}");

            // errors= — from volatile cache (thread-safe)
            var errors = ReloadCompileNotifier.CachedCompileErrors;
            sb.AppendLine($"errors={(!string.IsNullOrEmpty(errors) ? errors : "No compilation errors")}");

            // log= — Editor.log parse for CS codes
            sb.AppendLine($"log={ParseEditorLog()}");

            // reload_failed= — detect reload-failed markers in Editor.log
            sb.AppendLine($"reload_failed={DetectReloadFailed().ToString().ToLower()}");

            return sb.ToString().TrimEnd();
        }

        // G29: enumerate dlls via CompilationPipeline dynamically.
        public static string[] GetKnownDlls()
        {
            try
            {
                var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
                var result = new string[assemblies.Length];
                for (int i = 0; i < assemblies.Length; i++)
                    result[i] = Path.GetFileName(assemblies[i].outputPath);
                return result;
            }
            catch (Exception)
            {
                return new[] { "UnityMCP.Editor.dll", "UnityMCP.Reload.dll" };
            }
        }

        static string BuildDllFreshness()
        {
            // F1/F7: use volatile cache — Application.dataPath can only be called on main thread,
            // but diagnose is dispatched inline on ThreadPool in ReloadMiniServer.
            var projectRoot = ReloadCompileNotifier.CachedProjectRoot;
            var dataPath    = string.IsNullOrEmpty(projectRoot) ? "" : Path.Combine(projectRoot, "Assets");
            var libPath     = Path.Combine(projectRoot, "Library", "ScriptAssemblies");
            var sbDlls = new StringBuilder();

            foreach (var dllName in GetKnownDlls())
            {
                var asmName = Path.GetFileNameWithoutExtension(dllName);
                var dllPath = Path.Combine(libPath, dllName);
                var srcDir  = FindAsmdefDir(dataPath, asmName);
                var token   = GetDllFreshnessToken(dllPath, srcDir);
                var mtime   = File.Exists(dllPath)
                    ? new FileInfo(dllPath).LastWriteTimeUtc.Ticks : 0L;

                if (sbDlls.Length > 0) sbDlls.Append(',');
                sbDlls.Append($"{asmName}:{mtime}:{token}");
            }

            return sbDlls.Length > 0 ? sbDlls.ToString() : "none";
        }

        // Exposed public for NUnit testing with injected temp paths.
        // Returns "fresh" | "stale" | "unknown(missing)" | "unknown(no-src)"
        public static string GetDllFreshnessToken(string dllPath, string srcDir)
        {
            if (!File.Exists(dllPath)) return "unknown(missing)";
            if (string.IsNullOrEmpty(srcDir) || !Directory.Exists(srcDir)) return "unknown(no-src)";

            var dllMtime = new FileInfo(dllPath).LastWriteTimeUtc;
            var maxCsMtime = DateTime.MinValue;
            foreach (var cs in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            {
                var t = new FileInfo(cs).LastWriteTimeUtc;
                if (t > maxCsMtime) maxCsMtime = t;
            }

            if (maxCsMtime == DateTime.MinValue) return "fresh";
            return dllMtime < maxCsMtime ? "stale" : "fresh";
        }

        static string FindAsmdefDir(string dataPath, string asmName)
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
            catch (Exception) { }
            return "";
        }

        // C10: detect a current reload failure in Editor.log.
        // Testable overload with injected logPath.
        public static bool DetectReloadFailed(string logPath)
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

                int lastFailure = Math.Max(
                    text.LastIndexOf(reloadFailed, StringComparison.Ordinal),
                    text.LastIndexOf(reloadAborted, StringComparison.Ordinal));
                if (lastFailure < 0) return false;

                int lastSuccess = Math.Max(
                    text.LastIndexOf(reloadComplete, StringComparison.Ordinal),
                    text.LastIndexOf(monoReloaded, StringComparison.Ordinal));
                return lastSuccess < lastFailure;
            }
            catch (Exception) { return false; }
        }

        public static bool DetectReloadFailed() => DetectReloadFailed(GetEditorLogPath());

        static string ParseEditorLog()
        {
            try
            {
                var logPath = GetEditorLogPath();
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                    return "absent";

                string text;
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                    text = reader.ReadToEnd();

                return ParseEditorLogText(text);
            }
            catch (Exception e)
            {
                return $"error:{e.GetType().Name}";
            }
        }

        internal static string ParseEditorLogText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "clean";

            const string failHeader     = "## Script Compilation Error for:";
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

        static string GetEditorLogPath()
        {
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
            var linuxHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(linuxHome, ".config", "unity3d", "Editor.log");
        }
    }
}
