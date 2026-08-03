using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Scene hierarchy/health audit — F4.
    /// Entry point: Analyze(focus). All Check* methods are internal static for testability.
    /// </summary>
    internal static class SceneHealthAnalyzer
    {
        const int ItemCap = 20;

        static readonly HashSet<string> _allChecks = new HashSet<string>
            { "hierarchy", "naming", "duplicates", "origins", "missing", "empty", "disabled" };

        static readonly Dictionary<string, HashSet<string>> _focusMap =
            new Dictionary<string, HashSet<string>>
            {
                { "hierarchy",  new HashSet<string> { "hierarchy" } },
                { "naming",     new HashSet<string> { "naming", "duplicates" } },
                { "duplicates", new HashSet<string> { "duplicates" } },
                { "origins",    new HashSet<string> { "origins" } },
                { "missing",    new HashSet<string> { "missing" } },
                { "empty",      new HashSet<string> { "empty" } },
                { "disabled",   new HashSet<string> { "disabled" } },
            };

        internal static string Analyze(string focus)
        {
            var checks = ParseFocus(focus ?? "all");
            var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            var sb = new StringBuilder();
            sb.AppendLine($"SCENE HEALTH (focus={focus ?? "all"})");

            var findings = new List<string>();
            if (checks.Contains("missing"))    AddFinding(findings, CheckMissingScripts(all));
            if (checks.Contains("hierarchy"))  AddFinding(findings, CheckDeepHierarchy(all));
            if (checks.Contains("naming"))     AddFinding(findings, CheckBadNaming(all));
            if (checks.Contains("duplicates")) AddFinding(findings, CheckDuplicateSiblings(all));
            if (checks.Contains("empty"))      AddFinding(findings, CheckEmptyObjects(all));
            if (checks.Contains("origins"))    AddFinding(findings, CheckWorldOrigin(all));
            if (checks.Contains("disabled"))   AddFinding(findings, CheckDisabledRoots(all));

            if (findings.Count == 0)
                sb.Append("OK: no issues found");
            else
                foreach (var f in findings)
                    sb.AppendLine(f);

            return sb.ToString().TrimEnd();
        }

        static void AddFinding(List<string> list, string finding)
        {
            if (finding != null) list.Add(finding);
        }

        internal static HashSet<string> ParseFocus(string focus)
        {
            if (focus == null || focus == "all" || !_focusMap.ContainsKey(focus))
                return new HashSet<string>(_allChecks);
            return new HashSet<string>(_focusMap[focus]);
        }

        internal static string CheckDeepHierarchy(GameObject[] all)
        {
            var deep = new List<string>();
            foreach (var go in all)
            {
                int depth = GetDepth(go.transform);
                if (depth > 5)
                    deep.Add($"  depth={depth} — {ComponentSerializer.GetPath(go)}");
            }
            if (deep.Count == 0) return null;
            var sb = new StringBuilder("WARNING: hierarchy depth>5\n");
            for (int i = 0; i < deep.Count && i < ItemCap; i++) sb.AppendLine(deep[i]);
            if (deep.Count > ItemCap) sb.AppendLine($"  ... and {deep.Count - ItemCap} more");
            return sb.ToString().TrimEnd();
        }

        internal static string CheckBadNaming(GameObject[] all)
        {
            var bad = new List<string>();
            foreach (var go in all)
                if (go.name == "New GameObject")
                    bad.Add(ComponentSerializer.GetPath(go));
            if (bad.Count == 0) return null;
            var sb = new StringBuilder($"WARNING: \"New GameObject\" naming — {bad.Count} objects\n");
            int shown = 0;
            foreach (var p in bad)
            {
                if (shown++ >= 3) break;
                sb.AppendLine($"  {p}");
            }
            return sb.ToString().TrimEnd();
        }

        internal static string CheckEmptyObjects(GameObject[] all)
        {
            int count = 0;
            foreach (var go in all)
                if (go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1)
                    count++;
            if (count < 5) return null;
            return $"WARNING: empty objects ≥5 — {count} objects (no children, 1 component)";
        }

        internal static string CheckWorldOrigin(GameObject[] all)
        {
            int zeroCount = 0;
            foreach (var go in all)
            {
                if (go.transform.position != Vector3.zero) continue;
                if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null) continue;
                zeroCount++;
            }
            if (zeroCount == 0) return null;
            return $"INFO: {zeroCount} objects at world origin (excludes Camera/Light)";
        }

        internal static string CheckDisabledRoots(GameObject[] all)
        {
            int total = 0, disabled = 0;
            foreach (var go in all)
                if (go.transform.parent == null)
                {
                    total++;
                    if (!go.activeInHierarchy) disabled++;
                }
            if (total == 0 || disabled == 0) return null;
            float pct = 100f * disabled / total;
            if (pct <= 10f) return null;
            return $"DISABLED_ROOTS: {disabled}/{total} root objects disabled ({pct:F0}%)";
        }

        internal static string CheckDuplicateSiblings(GameObject[] all)
        {
            var parentGroups = new Dictionary<Transform, List<GameObject>>();
            var rootObjects = new List<GameObject>();
            foreach (var go in all)
            {
                var parent = go.transform.parent;
                if (parent == null)
                {
                    rootObjects.Add(go);
                    continue;
                }
                if (!parentGroups.ContainsKey(parent))
                    parentGroups[parent] = new List<GameObject>();
                parentGroups[parent].Add(go);
            }

            var findings = new List<string>();
            AppendDuplicateSiblingFindings(rootObjects, findings);
            foreach (var siblings in parentGroups.Values)
                AppendDuplicateSiblingFindings(siblings, findings);
            if (findings.Count == 0) return null;
            var sb = new StringBuilder("WARNING: duplicate sibling names\n");
            for (int i = 0; i < findings.Count && i < ItemCap; i++) sb.AppendLine(findings[i]);
            if (findings.Count > ItemCap) sb.AppendLine($"  ... and {findings.Count - ItemCap} more");
            return sb.ToString().TrimEnd();
        }

        private static void AppendDuplicateSiblingFindings(List<GameObject> siblings, List<string> findings)
        {
            if (siblings.Count == 0) return;
            var nameCounts = new Dictionary<string, int>();
            foreach (var go in siblings)
            {
                if (!nameCounts.ContainsKey(go.name)) nameCounts[go.name] = 0;
                nameCounts[go.name]++;
            }
            foreach (var nc in nameCounts)
            {
                if (nc.Value <= 1) continue;
                var parent = siblings[0].transform.parent;
                string parentPath = parent != null
                    ? ComponentSerializer.GetPath(parent.gameObject)
                    : "/";
                findings.Add($"  duplicate siblings \"{nc.Key}\" under {parentPath} (x{nc.Value})");
            }
        }

        internal static string CheckMissingScripts(GameObject[] all)
        {
            var missing = new List<string>();
            foreach (var go in all)
            {
                int count = 0;
                foreach (var c in go.GetComponents<Component>())
                    if (c == null) count++;
                if (count > 0)
                    missing.Add($"  {ComponentSerializer.GetPath(go)} (x{count})");
            }
            if (missing.Count == 0) return null;
            var sb = new StringBuilder("CRITICAL: MissingScript\n");
            for (int i = 0; i < missing.Count && i < ItemCap; i++) sb.AppendLine(missing[i]);
            if (missing.Count > ItemCap) sb.AppendLine($"  ... and {missing.Count - ItemCap} more");
            return sb.ToString().TrimEnd();
        }

        static int GetDepth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { d++; t = t.parent; }
            return d;
        }
    }
}
