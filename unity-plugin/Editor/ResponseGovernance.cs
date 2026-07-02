namespace UnityMCP.Editor
{
    /// <summary>
    /// Per-command response truncation to reduce LLM context waste.
    /// Applied before file-output check in BuildResponse.
    /// </summary>
    internal static class ResponseGovernance
    {
        internal static string Truncate(string data, int maxChars)
        {
            if (maxChars <= 0 || data == null || data.Length <= maxChars)
                return data;
            return data.Substring(0, maxChars)
                + $"\n[TRUNCATED: {data.Length} chars, showing first {maxChars}]";
        }
    }
}
