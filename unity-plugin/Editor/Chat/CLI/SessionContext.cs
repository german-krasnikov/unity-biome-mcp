// 256-bit session token: CSPRNG generate, persist/restore from Library/UnityMCP/chat_session.json.
// Token is NEVER logged — only sha256(token)[:16] appears in Python context files.
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class SessionContext
    {
#if UNITY_INCLUDE_TESTS
        // Test seam: override the storage directory so tests never touch Library/UnityMCP.
        internal static string SessionDirOverride;
#endif

        private static string SessionDir
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (!string.IsNullOrEmpty(SessionDirOverride))
                    return SessionDirOverride;
#endif
                return Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "Library", "UnityMCP"));
            }
        }

        private static string SessionFilePath => Path.Combine(SessionDir, "chat_session.json");

        /// <summary>Generate a 256-bit cryptographically random token as 64 lowercase hex chars.</summary>
        internal static string GenerateToken()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Persist token to Library/UnityMCP/chat_session.json (UTF-8, no BOM).</summary>
        internal static void SaveSession(string token)
        {
            Directory.CreateDirectory(SessionDir);
            var tmp = SessionFilePath + ".tmp";
            File.WriteAllText(tmp,
                $"{{\"session_token\":\"{JsonHelper.EscapeJson(token)}\"}}",
                JsonHelper.Utf8NoBom);
            if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath);
            File.Move(tmp, SessionFilePath);
        }

        /// <summary>Load token from file. Returns false when missing, empty, or unparseable.</summary>
        internal static bool TryLoadSession(out string token)
        {
            token = null;
            try
            {
                var path = SessionFilePath;
                if (!File.Exists(path)) return false;
                var json = File.ReadAllText(path);
                token = JsonHelper.ExtractString(json, "session_token");
                return !string.IsNullOrEmpty(token);
            }
            catch { return false; }
        }

        /// <summary>Remove the persisted session file (e.g. on explicit sign-out).</summary>
        internal static void DeleteSession()
        {
            try { File.Delete(SessionFilePath); } catch { }
        }
    }
}
