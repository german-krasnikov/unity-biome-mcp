using System.Globalization;
using System.IO;
using System.Text;

namespace UnityMCP.Playtest
{
    public sealed partial class PlayerPlaytestRunner
    {
        private static readonly UTF8Encoding ReceiptEncoding = new(false);

        private void WriteReceipts(int failed, double duration)
        {
            if (!string.IsNullOrEmpty(_jsonPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_jsonPath) ?? ".");
                File.WriteAllText(_jsonPath, ToJson(failed, duration), ReceiptEncoding);
            }
            if (!string.IsNullOrEmpty(_junitPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_junitPath) ?? ".");
                File.WriteAllText(_junitPath, ToJunit(failed, duration), ReceiptEncoding);
            }
        }

        private string ToJson(int failed, double duration)
        {
            var sb = new StringBuilder();
            sb.Append("{\"schema_version\":1,");
            sb.Append("\"passed\":").Append(_results.Count - failed).Append(',');
            sb.Append("\"failed\":").Append(failed).Append(',');
            sb.Append("\"duration_seconds\":").Append(duration.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"steps\":[");
            for (var i = 0; i < _results.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(_results[i].ToJson());
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private string ToJunit(int failed, double duration)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<testsuite name=\"UnityMCP.PlayerPlaytest\" tests=\"").Append(_results.Count);
            sb.Append("\" failures=\"").Append(failed).Append("\" time=\"");
            sb.Append(duration.ToString("F3", CultureInfo.InvariantCulture)).Append("\">");
            foreach (var result in _results)
                sb.Append(result.ToJunit());
            sb.Append("</testsuite>");
            return sb.ToString();
        }

        internal readonly struct StepResult
        {
            public readonly string Raw;
            public readonly bool Passed;
            public readonly string Message;

            private StepResult(string raw, bool passed, string message)
            {
                Raw = raw;
                Passed = passed;
                Message = message;
            }

            public static StepResult Pass(string raw, string message) => new(raw, true, message);
            public static StepResult Fail(string raw, string message) => new(raw, false, message);
            public string ToJson() => "{\"raw\":\"" + Escape(Raw) + "\",\"passed\":" +
                (Passed ? "true" : "false") + ",\"message\":\"" + Escape(Message) + "\"}";
            public string ToJunit()
            {
                var sb = new StringBuilder();
                sb.Append("<testcase name=\"").Append(EscapeXml(Raw)).Append("\">");
                if (!Passed)
                    sb.Append("<failure message=\"").Append(EscapeXml(Message)).Append("\" />");
                sb.Append("</testcase>");
                return sb.ToString();
            }
        }

        private static string Escape(string value)
        {
            return (value ?? "")
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        private static string EscapeXml(string value)
        {
            return (value ?? "")
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
