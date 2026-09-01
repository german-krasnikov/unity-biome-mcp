namespace UnityMCP.Editor
{
    /// <summary>
    /// Compact receipt persisted across the one Domain Reload triggered by
    /// disabling Source Patch (§3.3 P0-70). Lives in the main assembly (not
    /// the engine-neutral SourcePatch module) because it is read/written via
    /// SessionState — a Unity API the neutral asmdef cannot reference.
    ///
    /// Immutable, structurally validated on parse: any missing/malformed
    /// field fails closed (TryParse returns false) rather than reconstructing
    /// a partially-trusted receipt — a corrupt receipt must read as
    /// "wrong/ambiguous", never silently as "no receipt".
    /// </summary>
    internal sealed class SourcePatchDisableReceipt
    {
        public string OpId { get; }
        public int Pid { get; }
        public string ProjectPath { get; }
        public int ExpectedEpochAfter { get; }

        public SourcePatchDisableReceipt(string opId, int pid, string projectPath, int expectedEpochAfter)
        {
            OpId = opId;
            Pid = pid;
            ProjectPath = projectPath;
            ExpectedEpochAfter = expectedEpochAfter;
        }

        public string Serialize() =>
            "{\"op_id\":\"" + JsonHelper.EscapeJson(OpId) + "\"," +
            "\"pid\":" + Pid + "," +
            "\"project_path\":\"" + JsonHelper.EscapeJson(ProjectPath) + "\"," +
            "\"expected_epoch_after\":" + ExpectedEpochAfter + "}";

        public static bool TryParse(string raw, out SourcePatchDisableReceipt receipt)
        {
            receipt = null;
            if (string.IsNullOrEmpty(raw)) return false;

            var opId = JsonHelper.ExtractString(raw, "op_id");
            var projectPath = JsonHelper.ExtractString(raw, "project_path");
            if (string.IsNullOrEmpty(opId) || string.IsNullOrEmpty(projectPath)) return false;

            // JsonHelper.ExtractInt has no "field absent" signal (defaults to 0),
            // so absence is detected via the string extractor first — a receipt
            // with a genuinely-missing numeric field must fail closed, not read
            // as pid=0/epoch=0.
            if (JsonHelper.ExtractString(raw, "pid") == null) return false;
            if (JsonHelper.ExtractString(raw, "expected_epoch_after") == null) return false;

            var pid = JsonHelper.ExtractInt(raw, "pid", int.MinValue);
            var epoch = JsonHelper.ExtractInt(raw, "expected_epoch_after", int.MinValue);

            receipt = new SourcePatchDisableReceipt(opId, pid, projectPath, epoch);
            return true;
        }
    }
}
