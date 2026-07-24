using UnityEditor;
using UnityEngine.Networking;

namespace UnityMCP.Editor
{
    public static class UpdateChecker
    {
        const string CacheKey     = "UnityMCP.UpdateCache";
        const string CacheTimeKey = "UnityMCP.UpdateCacheTime";
        const string SkipKey      = "UnityMCP.SkippedVersion";
        const int    CacheTtlHours = 24;
        const string RepoSlug    = "german-krasnikov/unity-biome-mcp";
        const string ReleasesUrl = "https://api.github.com/repos/" + RepoSlug + "/releases/latest";
        internal const string RepoGitUrl = "https://github.com/" + RepoSlug + ".git";

        public static string AvailableVersion { get; private set; }
        public static bool   HasUpdate        => !string.IsNullOrEmpty(AvailableVersion);

        /// <summary>Check for updates respecting 24h cache. Safe to call from button.</summary>
        public static void CheckAsync()
        {
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
                    ApplyVersion(cached);
                    return;
                }
            }

            FetchFromNetwork();
        }

        /// <summary>Force network fetch, ignoring cache. Use from "Check for Updates" button.</summary>
        public static void ForceCheckAsync()
        {
            AvailableVersion = null;
            FetchFromNetwork();
        }

        static void FetchFromNetwork()
        {
            var req = UnityWebRequest.Get(ReleasesUrl);
            req.SetRequestHeader("User-Agent", "unity-biome-mcp-update-checker");
            req.SendWebRequest().completed += _ => OnResponse(req);
        }

        static void OnResponse(UnityWebRequest req)
        {
            if (req.result != UnityWebRequest.Result.Success) return;

            var tag = ParseTagName(req.downloadHandler.text);
            if (string.IsNullOrEmpty(tag)) return;

            var nowEpoch = (System.DateTime.UtcNow -
                new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
            EditorPrefs.SetString(CacheKey,     tag);
            EditorPrefs.SetString(CacheTimeKey, nowEpoch.ToString("F0", System.Globalization.CultureInfo.InvariantCulture));

            ApplyVersion(tag);
        }

        static void ApplyVersion(string tag)
        {
            var version = tag.TrimStart('v');
            var skipped = EditorPrefs.GetString(SkipKey, "");
            if (version == skipped) return;

            var current = GetCurrentVersion();
            if (IsNewer(version, current))
                AvailableVersion = version;
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
        }

        public static void SkipVersion()
        {
            if (string.IsNullOrEmpty(AvailableVersion)) return;
            EditorPrefs.SetString(SkipKey, AvailableVersion);
            AvailableVersion = null;
        }

#if UNITY_INCLUDE_TESTS
        public static void ResetForTest()
        {
            AvailableVersion = null;
        }

        public static void SetAvailableVersionForTest(string version)
        {
            AvailableVersion = version;
        }
#endif
    }
}
