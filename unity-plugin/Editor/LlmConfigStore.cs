using System.IO;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    [System.Serializable]
    internal sealed class LlmConfigStore
    {
        public LlmConfig Config = new LlmConfig();

        private static string DefaultPath =>
            Path.Combine(Application.dataPath, "..", "Library", "MCP_LlmConfig.json");

        internal static LlmConfigStore Load(string path = null)
        {
            path ??= DefaultPath;
            if (!File.Exists(path)) return new LlmConfigStore();
            try
            {
                var store = JsonUtility.FromJson<LlmConfigStore>(File.ReadAllText(path));
                return store ?? new LlmConfigStore();
            }
            catch { return new LlmConfigStore(); }
        }

        internal void Save(string path = null)
        {
            path ??= DefaultPath;
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        internal string ToTcpPayload()
        {
            var sb = new StringBuilder();
            AppendLine(sb, "visual_verify",       Config.VisualVerify);
            AppendLine(sb, "screenshot_describe", Config.ScreenshotDescribe);
            AppendLine(sb, "visual_diff",         Config.VisualDiff);
            AppendLine(sb, "summarize",           Config.Summarize);
            AppendLine(sb, "do_intent",           Config.DoIntent);
            AppendLine(sb, "distiller",           Config.Distiller);
            AppendLine(sb, "nl_composer",         Config.NlComposer);
            return sb.ToString().TrimEnd();
        }

        private static void AppendLine(StringBuilder sb, string key, SamplingConfig c)
        {
            var model   = string.IsNullOrEmpty(c.Model)   ? "haiku"  : c.Model;
            var backend = string.IsNullOrEmpty(c.Backend) ? "claude" : c.Backend;
            sb.AppendLine($"{key}:{model},{c.MaxTurns},{c.Timeout},{c.MaxTokens},{backend}");
        }
    }
}
