using UnityEditor;
using UnityEngine.Networking;
using System;

namespace UnityMCP.Editor
{
    public static class UpdateChecker
    {
        private sealed class RuntimeContext
        {
            internal string AvailableVersion;
            internal bool IsChecking;
            internal string LastError;
            internal Action CheckCompleted;
            internal UnityWebRequest ActiveRequest;
        }

        const string CacheKey     = "UnityMCP.UpdateCache";
        const string CacheTimeKey = "UnityMCP.UpdateCacheTime";
        const string SkipKey      = "UnityMCP.SkippedVersion";
        const int    CacheTtlHours = 24;
        const string RepoSlug    = "german-krasnikov/unity-biome-mcp";
        const string ReleasesUrl = "https://api.github.com/repos/" + RepoSlug + "/releases/latest";
        internal const string RepoGitUrl = "https://github.com/" + RepoSlug + ".git";

        private static readonly RuntimeContext ProductionContext = new RuntimeContext();
        private static RuntimeContext _currentContext = ProductionContext;

        private static RuntimeContext CurrentContext => _currentContext;

        public static string AvailableVersion
        {
            get => CurrentContext.AvailableVersion;
            private set => CurrentContext.AvailableVersion = value;
        }

        public static bool   HasUpdate        => !string.IsNullOrEmpty(AvailableVersion);
        public static bool IsChecking
        {
            get => CurrentContext.IsChecking;
            private set => CurrentContext.IsChecking = value;
        }

        public static string LastError
        {
            get => CurrentContext.LastError;
            private set => CurrentContext.LastError = value;
        }

        public static event Action CheckCompleted
        {
            add => CurrentContext.CheckCompleted += value;
            remove => CurrentContext.CheckCompleted -= value;
        }

        /// <summary>Check for updates respecting 24h cache. Safe to call from button.</summary>
        public static void CheckAsync()
        {
            var context = CurrentContext;
            // Populate from cache first
            var cached  = EditorPrefs.GetString(CacheKey, "");
            var rawTime = EditorPrefs.GetString(CacheTimeKey, "");
            if (!string.IsNullOrEmpty(cached) && !string.IsNullOrEmpty(rawTime)
                && double.TryParse(rawTime, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var savedAt))
            {
                var hours = (System.DateTime.UtcNow -
                    new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
                        .AddSeconds(savedAt)).TotalHours;
                if (hours < CacheTtlHours)
                {
                    ApplyVersion(context, cached);
                    return;
                }
            }

            FetchFromNetwork(context);
        }

        /// <summary>Force network fetch, ignoring cache. Use from "Check for Updates" button.</summary>
        public static void ForceCheckAsync()
        {
            var context = CurrentContext;
            context.AvailableVersion = null;
            context.LastError = null;
            FetchFromNetwork(context);
        }

        static void FetchFromNetwork(RuntimeContext context)
        {
            if (context.IsChecking) return;
            context.IsChecking = true;
            var req = UnityWebRequest.Get(ReleasesUrl);
            context.ActiveRequest = req;
            req.SetRequestHeader("User-Agent", "unity-biome-mcp-update-checker");
            req.SendWebRequest().completed += _ => OnResponse(context, req);
        }

        static void OnResponse(RuntimeContext context, UnityWebRequest req)
        {
            if (!ReferenceEquals(req, context.ActiveRequest))
                return;
            context.ActiveRequest = null;
            context.IsChecking = false;
            if (req.result != UnityWebRequest.Result.Success)
            {
                context.LastError = string.IsNullOrEmpty(req.error)
                    ? "Update check failed."
                    : req.error;
                InvokeCheckCompleted(context);
                req.Dispose();
                return;
            }

            var tag = ParseTagName(req.downloadHandler.text);
            if (string.IsNullOrEmpty(tag))
            {
                context.LastError = "The release response did not contain a version tag.";
                InvokeCheckCompleted(context);
                req.Dispose();
                return;
            }

            var nowEpoch = (System.DateTime.UtcNow -
                new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
            EditorPrefs.SetString(CacheKey,     tag);
            EditorPrefs.SetString(CacheTimeKey, nowEpoch.ToString("F0", System.Globalization.CultureInfo.InvariantCulture));

            ApplyVersion(context, tag);
            context.LastError = null;
            InvokeCheckCompleted(context);
            req.Dispose();
        }

        internal static void CancelActiveCheck()
        {
            CancelActiveCheck(CurrentContext);
        }

        private static void CancelActiveCheck(RuntimeContext context)
        {
            var request = context.ActiveRequest;
            context.ActiveRequest = null;
            context.IsChecking = false;
            if (request == null) return;
            try { request.Abort(); } catch { }
            request.Dispose();
        }

        private static void InvokeCheckCompleted(RuntimeContext context)
        {
            var previous = _currentContext;
            _currentContext = context;
            try
            {
                context.CheckCompleted?.Invoke();
            }
            finally
            {
                _currentContext = previous;
            }
        }

        static void ApplyVersion(RuntimeContext context, string tag)
        {
            var version = tag.TrimStart('v');
            var skipped = EditorPrefs.GetString(SkipKey, "");
            if (version == skipped) return;

            var current = GetCurrentVersion();
            if (IsNewer(version, current))
                context.AvailableVersion = version;
        }

        static bool IsNewer(string candidate, string current)
        {
            if (!System.Version.TryParse(candidate, out var a)) return false;
            if (!System.Version.TryParse(current,   out var b)) return false;
            return a > b;
        }

        internal static string GetCurrentVersion()
        {
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UpdateChecker).Assembly);
                return (info?.version ?? "0.0.0").TrimStart('v');
            }
            catch { return "0.0.0"; }
        }

        static string ParseTagName(string json)
        {
            const string key = "\"tag_name\"";
            var idx = json.IndexOf(key);
            if (idx < 0) return null;
            var colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return null;
            var q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return null;
            var q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        public static void ClearCache()
        {
            EditorPrefs.DeleteKey(CacheKey);
            EditorPrefs.DeleteKey(CacheTimeKey);
            AvailableVersion = null;
            LastError = null;
        }

        public static void SkipVersion()
        {
            if (string.IsNullOrEmpty(AvailableVersion)) return;
            EditorPrefs.SetString(SkipKey, AvailableVersion);
            AvailableVersion = null;
        }

#if UNITY_INCLUDE_TESTS
        private sealed class TestIsolationScope : IDisposable
        {
            private readonly RuntimeContext _context;
            private readonly RuntimeContext _previous;
            private bool _disposed;

            internal TestIsolationScope(RuntimeContext context, RuntimeContext previous)
            {
                _context = context;
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed) return;
                if (!ReferenceEquals(_currentContext, _context))
                    throw new InvalidOperationException(
                        "UpdateChecker test-isolation scopes must be disposed in LIFO order.");

                try
                {
                    CancelActiveCheck(_context);
                }
                finally
                {
                    // A request Abort/Dispose failure must never strand the isolated
                    // context as the process-wide UpdateChecker state.
                    _context.CheckCompleted = null;
                    _currentContext = _previous;
                    _disposed = true;
                }
            }
        }

        internal static IDisposable BeginTestIsolation()
        {
            var previous = CurrentContext;
            var isolated = new RuntimeContext();
            _currentContext = isolated;
            return new TestIsolationScope(isolated, previous);
        }

        internal static void SetAvailableVersionForTest(string version)
        {
            AvailableVersion = version;
        }

        internal static void SetStateForTest(
            string availableVersion,
            bool isChecking,
            string lastError,
            UnityWebRequest activeRequest = null)
        {
            var context = CurrentContext;
            context.AvailableVersion = availableVersion;
            context.IsChecking = isChecking;
            context.LastError = lastError;
            context.ActiveRequest = activeRequest;
        }

        internal static UnityWebRequest ActiveRequestForTest => CurrentContext.ActiveRequest;

        internal static void RaiseCheckCompletedForTest()
        {
            InvokeCheckCompleted(CurrentContext);
        }
#endif
    }
}
