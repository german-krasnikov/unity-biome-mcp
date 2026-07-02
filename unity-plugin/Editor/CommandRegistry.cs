using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UnityMCP.Editor
{
    public static class CommandRegistry
    {
        private struct Entry
        {
            public Func<string, string> Handler;
            public Action<string, string, TaskCompletionSource<string>> AsyncHandler;
            public bool Mutating;
            public bool Runtime;
            // true = dispatched outside the normal Handler/AsyncHandler path (e.g. screenshot,
            // intercepted in CommandRouter.Process for file-response formatting). Its Handler
            // here is a throwing stub, not a real implementation — must never be batchable.
            public bool SpecialDispatch;
            // Guard-list flags (DRY audit issues-23-29 Cat.1): travel with the registration
            // instead of being re-typed as a separate hardcoded OR-chain in CommandRouter.
            public bool AlwaysAllowed;
            public bool AllowedDuringCompile;
            // null = free-form (e.g. execute_code) — skip validation entirely.
            // non-null (possibly empty) array = structured contract, validated by CommandValidator.
            public string[] Required;
            public string[] Optional;
            public string Description;
            // Per-command soft limit for response truncation (0 = no limit).
            public int MaxResponseChars;
        }

        // All mutations happen on Unity main thread (dispatched by MCPServer).
        private static readonly Dictionary<string, Entry> _commands = new Dictionary<string, Entry>();

        // No static constructor here (M7, ROI reliability sprint) — CommandRegistry used to
        // eagerly RegisterAll() on first touch, creating a cyclic static-init dependency with
        // CommandRouter. Population now happens explicitly via Bootstrap.Init() (see Bootstrap.cs).
        internal static void InitDefaults()
        {
            CommandRouter.RegisterAll();
        }

        // Task 3.1 (ROI reliability sprint): set by PluginRegistry around each plugin's
        // RegisterCommands() call. When true, Register/RegisterAction/RegisterAsync strip
        // alwaysAllowed/allowedDuringCompile — these are core-only trust flags; a 3rd-party
        // plugin claiming them could run mutating code during Unity's compile window, while
        // the C# domain is in flux (stale-assembly read/write risk).
        internal static bool CallerIsPlugin;

        /// <summary>Strips core-only trust flags from a plugin's registration, logging a warning.</summary>
        private static void DenyPluginCoreFlags(string cmd, ref CommandOptions options)
        {
            if (!CallerIsPlugin || (!options.AlwaysAllowed && !options.AllowedDuringCompile)) return;
            UnityEngine.Debug.LogWarning(
                $"[MCP] Plugin command '{cmd}' requested alwaysAllowed/allowedDuringCompile — denied (core-only flags).");
            options.AlwaysAllowed = false;
            options.AllowedDuringCompile = false;
        }

        /// <summary>CSV → string[]. null stays null (free-form marker); "" becomes empty array (zero-param).</summary>
        private static string[] Split(string csv) =>
            csv == null ? null : (csv.Length == 0 ? Array.Empty<string>() : csv.Split(','));

        /// <summary>Guards against double-registration. Returns true (and logs) if `cmd` is already taken.</summary>
        private static bool AlreadyRegistered(string cmd)
        {
            if (!_commands.ContainsKey(cmd)) return false;
            UnityEngine.Debug.LogWarning($"[MCP] Command '{cmd}' already registered, skipping duplicate");
            return true;
        }

        public static void Register(string cmd, Func<string, string> handler, CommandOptions options)
        {
            if (AlreadyRegistered(cmd)) return;
            DenyPluginCoreFlags(cmd, ref options);
            _commands[cmd] = new Entry
            {
                Handler = handler,
                Mutating = options.Mutating,
                Runtime = options.Runtime,
                SpecialDispatch = options.SpecialDispatch,
                AlwaysAllowed = options.AlwaysAllowed,
                AllowedDuringCompile = options.AllowedDuringCompile,
                Required = Split(options.Required),
                Optional = Split(options.Optional),
                Description = options.Description,
                MaxResponseChars = options.MaxResponseChars
            };
        }

        public static void Register(string cmd, Func<string, string> handler, bool mutating = false, bool runtime = false,
            string required = null, string optional = null, bool specialDispatch = false,
            bool alwaysAllowed = false, bool allowedDuringCompile = false, string description = null,
            int maxResponseChars = 0) =>
            Register(cmd, handler, new CommandOptions
            {
                Mutating = mutating,
                Runtime = runtime,
                Required = required,
                Optional = optional,
                SpecialDispatch = specialDispatch,
                AlwaysAllowed = alwaysAllowed,
                AllowedDuringCompile = allowedDuringCompile,
                Description = description,
                MaxResponseChars = maxResponseChars
            });

        // action is always required (enforced by the wrapper below) — callers only
        // declare the REMAINING required params via `required`/`options.Required`.
        public static void RegisterAction(string cmd, Func<string, string, string> handler, CommandOptions options)
        {
            if (AlreadyRegistered(cmd)) return;
            DenyPluginCoreFlags(cmd, ref options);
            var req = new List<string> { "action" };
            var extra = Split(options.Required);
            if (extra != null) req.AddRange(extra);
            _commands[cmd] = new Entry
            {
                Handler = args =>
                {
                    var action = JsonHelper.ExtractString(args, "action");
                    if (action == null)
                        throw new ArgumentException($"'action' is required for command '{cmd}'");
                    return handler(action, args);
                },
                Mutating = options.Mutating,
                Runtime = options.Runtime,
                AlwaysAllowed = options.AlwaysAllowed,
                AllowedDuringCompile = options.AllowedDuringCompile,
                Required = req.ToArray(),
                Optional = Split(options.Optional),
                Description = options.Description,
                MaxResponseChars = options.MaxResponseChars
            };
        }

        public static void RegisterAction(string cmd, Func<string, string, string> handler, bool mutating = false, bool runtime = false,
            string required = null, string optional = null, bool alwaysAllowed = false, bool allowedDuringCompile = false,
            string description = null, int maxResponseChars = 0) =>
            RegisterAction(cmd, handler, new CommandOptions
            {
                Mutating = mutating,
                Runtime = runtime,
                Required = required,
                Optional = optional,
                AlwaysAllowed = alwaysAllowed,
                AllowedDuringCompile = allowedDuringCompile,
                Description = description,
                MaxResponseChars = maxResponseChars
            });

        // handler signature: (id, argsJson, tcs)
        public static void RegisterAsync(string cmd, Action<string, string, TaskCompletionSource<string>> handler,
            CommandOptions options)
        {
            if (AlreadyRegistered(cmd)) return;
            DenyPluginCoreFlags(cmd, ref options);
            _commands[cmd] = new Entry
            {
                AsyncHandler = handler,
                Mutating = options.Mutating,
                Runtime = options.Runtime,
                AlwaysAllowed = options.AlwaysAllowed,
                AllowedDuringCompile = options.AllowedDuringCompile,
                Required = Split(options.Required),
                Optional = Split(options.Optional),
                Description = options.Description,
                MaxResponseChars = options.MaxResponseChars
            };
        }

        public static void RegisterAsync(string cmd, Action<string, string, TaskCompletionSource<string>> handler,
            bool mutating = false, bool runtime = false, string required = null, string optional = null,
            bool alwaysAllowed = false, bool allowedDuringCompile = false, string description = null,
            int maxResponseChars = 0) =>
            RegisterAsync(cmd, handler, new CommandOptions
            {
                Mutating = mutating,
                Runtime = runtime,
                Required = required,
                Optional = optional,
                AlwaysAllowed = alwaysAllowed,
                AllowedDuringCompile = allowedDuringCompile,
                Description = description,
                MaxResponseChars = maxResponseChars
            });

        public static bool IsRegistered(string cmd) => _commands.ContainsKey(cmd);
        internal static bool IsMutating(string cmd) => _commands.TryGetValue(cmd, out var entry) && entry.Mutating;
        internal static bool IsRuntime(string cmd) => _commands.TryGetValue(cmd, out var entry) && entry.Runtime;
        // Guard-list flags (DRY audit issues-23-29 Cat.1) — single source of truth, travels
        // with the registration. CommandRouter.IsAlwaysAllowed/IsAllowedDuringCompile delegate here.
        internal static bool IsAlwaysAllowed(string cmd) => _commands.TryGetValue(cmd, out var entry) && entry.AlwaysAllowed;
        internal static bool IsAllowedDuringCompile(string cmd) => _commands.TryGetValue(cmd, out var entry) && entry.AllowedDuringCompile;
        internal static int GetMaxResponseChars(string cmd) => _commands.TryGetValue(cmd, out var entry) ? entry.MaxResponseChars : 0;

        // Structural batchability check — replaces a hand-maintained name list.
        // Unregistered commands are treated as "batchable" so they fall through to
        // Validate(), which reports "Unknown command" instead of a misleading
        // "not batchable" error. A REGISTERED command is blocked here if it's async-only
        // OR marked SpecialDispatch (e.g. screenshot — its Handler is a throwing stub,
        // the real implementation runs outside CommandRegistry.Execute).
        internal static bool IsBatchable(string cmd) =>
            !_commands.TryGetValue(cmd, out var e) || (e.AsyncHandler == null && !e.SpecialDispatch);

        /// <summary>Read-only view of a command's contract for CommandValidator. Returns false if unregistered.</summary>
        internal static bool TryGetContract(string cmd, out string[] required, out string[] optional, out bool isFreeForm)
        {
            if (!_commands.TryGetValue(cmd, out var e))
            {
                required = null;
                optional = null;
                isFreeForm = false;
                return false;
            }
            isFreeForm = e.Required == null && e.Optional == null;
            required = e.Required ?? Array.Empty<string>();
            optional = e.Optional ?? Array.Empty<string>();
            return true;
        }

        internal static string Execute(string cmd, string args)
        {
            if (!_commands.TryGetValue(cmd, out var entry))
                throw new InvalidOperationException($"Command not registered: {cmd}");
            if (entry.Handler == null)
                throw new InvalidOperationException($"{cmd} requires async dispatch via ProcessAsync");
            return entry.Handler(args);
        }

        internal static bool HasAsyncHandler(string cmd, out Action<string, string, TaskCompletionSource<string>> handler)
        {
            if (_commands.TryGetValue(cmd, out var entry) && entry.AsyncHandler != null)
            {
                handler = entry.AsyncHandler;
                return true;
            }
            handler = null;
            return false;
        }

        internal static IEnumerable<string> GetAllCommands() => _commands.Keys;
        internal static void Clear() => _commands.Clear();

        public static string GetDescription(string cmd) =>
            _commands.TryGetValue(cmd, out var e) ? e.Description : null;

        /// <summary>Build help text for all commands matching a prefix (e.g. "bt_", "am_").</summary>
        public static string BuildHelp(string prefix)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var cmd in GetAllCommands().Where(c => c.StartsWith(prefix)).OrderBy(c => c))
            {
                if (!TryGetContract(cmd, out var req, out var opt, out _)) continue;
                var rw = IsMutating(cmd) ? "RW" : "RO";
                var desc = GetDescription(cmd) ?? "";
                var usage = CommandValidator.AutoUsage(cmd, req, opt);
                sb.AppendLine($"{usage} [{rw}] | {desc}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
