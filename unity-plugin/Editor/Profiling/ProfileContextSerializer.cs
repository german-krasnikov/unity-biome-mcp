using System;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>Converts latest ProfileSession to compact text for TCP and chip payloads.</summary>
    internal static class ProfileContextSerializer
    {
#if UNITY_INCLUDE_TESTS
        // Override Get(sid) entirely for isolated chip-provider tests.
        internal static Func<string, string> GetOverride;
#endif

        internal static string GetLatest()
        {
            string list = ProfileRecorder.Dispatch("list_sessions", "{}");
            if (list == "no sessions") return "no sessions";
            string sid = ExtractLatestSid(list);
            return sid == null ? "no sessions" : Get(sid);
        }

        internal static string GetLatestSessionId()
        {
            string list = ProfileRecorder.Dispatch("list_sessions", "{}");
            return list == "no sessions" ? null : ExtractLatestSid(list);
        }

        internal static string Get(string sid)
        {
#if UNITY_INCLUDE_TESTS
            if (GetOverride != null) return GetOverride(sid);
#endif
            return ProfileRecorder.Dispatch("analyze", $"{{\"session\":\"{sid}\"}}");
        }

        private static string ExtractLatestSid(string listOutput)
        {
            string latestSid = null;
            foreach (string line in listOutput.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string sid = line.Trim().Split(' ')[0];
                if (!string.IsNullOrEmpty(sid)) latestSid = sid;
            }
            return latestSid;
        }
    }
}
