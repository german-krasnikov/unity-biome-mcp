// T15: Receipt value struct emitted by mutation handlers into BuildResponse.
namespace UnityMCP.Editor
{
    internal struct MutationReceipt
    {
        public string Path;       // scene path of mutated object
        public string Op;         // "create" | "modify" | "delete"
        public string TargetType; // "scene_object" | "component" | "property" | "asset"
        public string Prop;       // property name (null if N/A)
        public string Before;     // before value, capped to 512 chars (null for create)
        public string After;      // after value, capped to 512 chars (null for delete)
        public bool   Reversible;

        // Returns JSON fragment: "receipt":{...}  (no leading comma)
        public string ToJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("\"receipt\":{");
            Append(sb, "path", Cap(Path));
            sb.Append(",");
            Append(sb, "op", Op);
            sb.Append(",");
            Append(sb, "t", TargetType);
            if (Prop   != null) { sb.Append(","); Append(sb, "prop",   Cap(Prop)); }
            if (Before != null) { sb.Append(","); Append(sb, "before", Cap(Before)); }
            if (After  != null) { sb.Append(","); Append(sb, "after",  Cap(After)); }
            sb.Append(",\"rev\":").Append(Reversible ? "true" : "false");
            sb.Append("}");
            return sb.ToString();
        }

        private static void Append(System.Text.StringBuilder sb, string k, string v)
            => sb.Append("\"").Append(k).Append("\":\"")
                 .Append(JsonHelper.EscapeJson(v ?? "")).Append("\"");

        private static string Cap(string s) =>
            s != null && s.Length > 512 ? s.Substring(0, 512) : s;
    }
}
