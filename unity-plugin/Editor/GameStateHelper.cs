using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class GameStateHelper
    {
        // queries = comma-separated "path|component|field" triplets
        public static string Snapshot(string queries)
        {
            var sb = new StringBuilder();
            var items = queries.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in items)
            {
                var parts = item.Trim().Split('|');
                if (parts.Length < 3)
                {
                    sb.AppendLine($"ERR: need path|component|field, got '{item.Trim()}'");
                    continue;
                }

                var path = parts[0].Trim();
                var compName = parts[1].Trim();
                var fieldName = parts[2].Trim();

                try
                {
                    var go = ComponentSerializer.FindObject(path);
                    if (go == null) { sb.AppendLine($"{compName}.{fieldName}=ERR:object not found"); continue; }

                    var comp = RuntimeHelper.FindComponentInternal(go, compName);
                    if (comp == null) { sb.AppendLine($"{compName}.{fieldName}=ERR:component not found"); continue; }

                    string result;
                    try { result = RuntimeHelper.ReadFieldInternal(comp, fieldName); }
                    catch
                    {
                        // Fall back to method invoke (no args)
                        result = RuntimeHelper.InvokeMethod(path, compName, fieldName, "");
                    }
                    sb.AppendLine($"{compName}.{fieldName}={result}");
                }
                catch (System.Exception e)
                {
                    var msg = e.Message.Length > 200 ? e.Message.Substring(0, 200) : e.Message;
                    sb.AppendLine($"{compName}.{fieldName}=ERR:{msg}");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
