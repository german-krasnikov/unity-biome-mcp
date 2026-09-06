using System;
using System.Text;

namespace UnityMCP.Editor
{
    internal static class WatchCommandHandler
    {
        internal static void RegisterAll()
        {
            // watch_add: runtime-only (polling only makes sense in Play Mode).
            // PR-04: notBatchable — batch runs are sequential/deterministic; a watch spans
            // real wall-clock Play-Mode time, so "did it fire during the batch" is undefined.
            CommandRegistry.Register("watch_add", ExecWatchAdd, runtime: true,
                required: "path,component,field", optional: "condition,action,interval_ms",
                notBatchable: true);
            // read/management: available outside Play Mode too
            CommandRegistry.Register("get_watches",  _ => ExecGetWatches(),
                required: "", optional: "");
            CommandRegistry.Register("watch_remove", args =>
            {
                var id = JsonHelper.ExtractString(args, "id");
                return WatchRegistry.Remove(id ?? "") ? $"removed {id}" : $"not found: {id}";
            }, required: "id", optional: "");
            CommandRegistry.Register("watch_clear",  _ => { WatchRegistry.Clear(); WatchRegistry.Save(); return "cleared"; },
                required: "", optional: "");
            CommandRegistry.Register("watch_reset",  args =>
            {
                var id = JsonHelper.ExtractString(args, "id");
                if (id != null && WatchRegistry.All.TryGetValue(id, out var entry))
                {
                    entry.Triggered = false;
                    return $"reset {id}";
                }
                return $"not found: {id}";
            }, required: "id", optional: "");
        }

        private static string ExecWatchAdd(string args)
        {
            var path      = JsonHelper.ExtractString(args, "path");
            var component = JsonHelper.ExtractString(args, "component");
            var field     = JsonHelper.ExtractString(args, "field");
            if (string.IsNullOrEmpty(path))      throw new ArgumentException("'path' is required");
            if (string.IsNullOrEmpty(component)) throw new ArgumentException("'component' is required");
            if (string.IsNullOrEmpty(field))     throw new ArgumentException("'field' is required");
            var condition = JsonHelper.ExtractString(args, "condition") ?? "";
            var action    = JsonHelper.ExtractString(args, "action") ?? "log";
            var interval  = CommandRouter.ExtractFloat(args, "interval_ms", 500f);

            var id = WatchRegistry.Add(path, component, field, condition, action, interval);
            if (id == null) throw new System.Exception("MaxWatches (20) reached — remove some first");
            return id;
        }

        private static string ExecGetWatches()
        {
            var watches = WatchRegistry.All;
            var log = WatchRegistry.DrainLog();
            var sb = new StringBuilder();
            sb.AppendLine($"watches: {watches.Count}");
            foreach (var (id, e) in watches)
            {
                var cond = string.IsNullOrEmpty(e.Condition) ? "" : $" {e.Condition}";
                var triggered = e.Triggered ? " TRIGGERED" : "";
                sb.AppendLine(
                    $"{id}: {e.Path} {e.Component} {e.Field}{cond}" +
                    $" | interval={e.IntervalMs}ms | changes={e.ChangeCount}{triggered}");
            }
            if (log.Length > 0)
            {
                sb.AppendLine($"\nlog ({log.Length} entries):");
                foreach (var entry in log) sb.AppendLine(entry);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
