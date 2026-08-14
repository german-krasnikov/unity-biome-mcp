// T16: Pure static parser — text format → ChangeSetViewModel. No Unity deps.
//
// Format:
//   cs:<id8> status:<status> ops:<n> nc:<n> nm:<n> nd:<n>
//   <kind> <ttype> <path> [prop] [bh:<hash>] [ah:<hash>] rev:<bool>
namespace UnityMCP.Editor
{
    internal static class ChangeSetParser
    {
        // Returns null when text is null/empty/"no_changeset".
        internal static ChangeSetViewModel Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text == "no_changeset") return null;
            var lines = text.Split('\n');
            if (lines.Length == 0) return null;

            var id     = ExtractToken(lines[0], "cs:");
            var status = ExtractToken(lines[0], "status:");
            if (id == null || status == null) return null;

            var ops = new System.Collections.Generic.List<OperationViewModel>();
            for (int i = 1; i < lines.Length; i++)
            {
                var op = ParseOpLine(lines[i]);
                if (op != null) ops.Add(op);
            }
            return new ChangeSetViewModel(id, status, ops.ToArray());
        }

        // "modify property /Player Health bh:aabb1122 ah:ccdd3344 rev:true"
        private static OperationViewModel ParseOpLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var parts = line.Split(' ');
            if (parts.Length < 4) return null;

            string kind  = parts[0];
            string ttype = parts[1];
            string path  = parts[2];
            string prop  = null;
            string bh = null, ah = null;
            bool rev = true;

            int i = 3;
            if (i < parts.Length && !parts[i].Contains(':'))
                prop = parts[i++];

            for (; i < parts.Length; i++)
            {
                if      (parts[i].StartsWith("bh:"))  bh  = parts[i].Substring(3);
                else if (parts[i].StartsWith("ah:"))  ah  = parts[i].Substring(3);
                else if (parts[i].StartsWith("rev:")) rev = parts[i] == "rev:true";
            }
            return new OperationViewModel(kind, ttype, path, prop, bh, ah, rev);
        }

        // Extract "value" from "key:value" in space-delimited header line.
        private static string ExtractToken(string line, string prefix)
        {
            int idx = line.IndexOf(prefix, System.StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + prefix.Length;
            int end   = line.IndexOf(' ', start);
            return end < 0 ? line.Substring(start) : line.Substring(start, end - start);
        }
    }
}
