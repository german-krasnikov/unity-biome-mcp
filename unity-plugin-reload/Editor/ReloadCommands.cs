// ReloadCommands — dict-dispatch for reload package commands.
// No network code (increment 3 adds TCP server).
// force_refresh/recompile require Unity main thread — in increment 3 server will queue them.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Reload
{
    public static class ReloadCommands
    {
        internal static Func<Func<bool, string>> AdmissionResolver = ResolveMainAdmission;
        internal static Action RefreshForReload = () => AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        internal static Action CompileScripts = () => CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        internal static Action RefreshOnly = () => AssetDatabase.Refresh();

        // The optional main assembly owns mode policy. Read its neutral delegate at
        // dispatch; an unavailable owner cannot authorize a mutation via this port.
        private static Func<bool, string> ResolveMainAdmission()
        {
            var main = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == "UnityMCP.Editor");
            return main?.GetType("UnityMCP.Editor.SyncHelper")?.GetField("ReloadBlockReason",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Func<bool, string>;
        }

        internal static string GetReloadBlockReason()
        {
            try
            {
                var admission = AdmissionResolver();
                return admission == null ? "reload_admission_unavailable" : admission(false);
            }
            catch { return "reload_admission_unavailable"; }
        }

        // Lazy-init so tests can call individual methods without triggering full build
        static readonly Dictionary<string, Func<string, string>> _commands =
            new Dictionary<string, Func<string, string>>
            {
                ["ping"]         = _ => "pong",
                ["get_version"]  = _ => ReloadDomainStamp.ComputeStamp(),
                ["diagnose"]     = args => ReloadDiagnoseCommand.Execute(args),
                ["sync_status"]  = _ => ReadSyncStatus(),
                ["force_refresh"] = _ => ForceRefresh(),
                ["recompile"]    = _ => Recompile(),
            };

        public static IReadOnlyDictionary<string, Func<string, string>> Commands => _commands;

        // Dispatch command by name. Returns error string for unknown commands.
        public static string Dispatch(string cmd, string args = "")
        {
            if (_commands.TryGetValue(cmd, out var handler))
                return handler(args);
            return $"error=unknown command: {cmd}";
        }

        // Read-only sync status from SessionState — no TriggerSync call (diagnose contract).
        static string ReadSyncStatus()
        {
            var state = SessionState.GetString("MCP_SyncState", "unknown");
            var epoch = SessionState.GetInt("MCP_SyncEpoch", 0);
            return $"state={state}|epoch={epoch}";
        }

        // Main-thread required. In increment 3 TCP server will queue this via EditorApplication.update.
        static string ForceRefresh()
        {
            var blocked = GetReloadBlockReason();
            if (!string.IsNullOrEmpty(blocked)) return "blocked|reason=" + blocked;
            RefreshForReload();
            CompileScripts();
            return "force_refresh triggered";
        }

        // Main-thread required.
        static string Recompile()
        {
            var blocked = GetReloadBlockReason();
            if (!string.IsNullOrEmpty(blocked)) return "blocked|reason=" + blocked;
            RefreshOnly();
            return "recompile triggered";
        }
    }
}
