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
                var path = parts[0].Trim();

                // 2-part shorthand: path|virtualField (name, tag, layer, activeSelf, activeInHierarchy)
                if (parts.Length == 2)
                {
                    var shortField = parts[1].Trim();
                    try
                    {
                        var go2 = ComponentSerializer.FindObject(path);
                        if (go2 == null) { sb.AppendLine($"{shortField}=ERR:object not found"); continue; }
                        string shortResult = shortField switch
                        {
                            "name"              => go2.name,
                            "tag"               => go2.tag,
                            "layer"             => go2.layer.ToString(),
                            "activeSelf"        => go2.activeSelf.ToString().ToLowerInvariant(),
                            "active"            => go2.activeSelf.ToString().ToLowerInvariant(),
                            "activeInHierarchy" => go2.activeInHierarchy.ToString().ToLowerInvariant(),
                            _ => null
                        };
                        if (shortResult != null) { sb.AppendLine($"{shortField}={shortResult}"); continue; }
                    }
                    catch (System.Exception e2)
                    {
                        var m2 = e2.Message.Length > 200 ? e2.Message.Substring(0, 200) : e2.Message;
                        sb.AppendLine($"{parts[1].Trim()}=ERR:{m2}");
                        continue;
                    }
                    sb.AppendLine($"ERR: unknown shorthand '{parts[1].Trim()}'; use path|name|tag|layer|activeSelf|activeInHierarchy or path|component|field");
                    continue;
                }

                if (parts.Length < 3)
                {
                    sb.AppendLine($"ERR: need path|component|field, got '{item.Trim()}'");
                    continue;
                }

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
