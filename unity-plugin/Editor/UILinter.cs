using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityMCP.Editor
{
    // G2+S4: uGUI structural diagnostics + UITK structural lint.
    internal static class UILinter
    {
        // Called by ExecLintUGUI.
        // root: scene path to root GO to scan, or null = scan all loaded scenes.
        // Returns compact text: "ok: 0 issues" or newline-separated warnings.
        internal static string LintUGUI(string root)
        {
            var issues = new List<string>();

            // Resolve optional root scope.
            GameObject rootGO = root != null ? GameObject.Find(root) : null;

            // Check EventSystem presence (required for uGUI click dispatch).
            // EventSystem is always scene-level; root scoping never applies here.
            bool hasEventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null;
            if (!hasEventSystem)
                issues.Add("warn: no EventSystem in scene — uGUI clicks will not work; " +
                           "add GameObject > UI > Event System");

            // Check GraphicRaycaster on every Canvas within scope.
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (rootGO != null && !canvas.transform.IsChildOf(rootGO.transform)) continue;
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    issues.Add($"warn: Canvas '{canvas.name}' missing GraphicRaycaster — " +
                               "add it for raycast-based interaction");
            }

            if (issues.Count == 0) return "ok: 0 issues";
            return string.Join("\n", issues);
        }

        // Called by ExecLintUITK.
        // path: absolute path to a .uxml or .uss file.
        // fix: accepted for future use; currently no auto-fix is applied.
        // Returns: "ok: 0 issues" or "warn: N issues\n[AX] ..." text.
        internal static string LintUITK(string path, bool fix)
        {
            if (string.IsNullOrEmpty(path))
                return "err: path is required";
            if (!File.Exists(path))
                return $"err: file not found: {path}";

            bool isUxml = path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase);
            bool isUss  = path.EndsWith(".uss",  StringComparison.OrdinalIgnoreCase);
            if (!isUxml && !isUss)
                return $"err: unsupported extension (expected .uxml or .uss): {path}";

            string text = File.ReadAllText(path);
            var issues = new List<string>();

            if (isUxml) LintUxmlContent(text, path, issues);
            else        LintUssContent(text, issues);

            if (issues.Count == 0) return "ok: 0 issues";
            return $"warn: {issues.Count} issues\n" + string.Join("\n", issues);
        }

        // UXML checks: A1 (malformed XML), A2 (broken Style src), A3 (missing Template src), A4 (unnamed interactives).
        private static void LintUxmlContent(string text, string uxmlPath, List<string> issues)
        {
            XDocument doc;
            try { doc = XDocument.Parse(text); }
            catch (Exception ex)
            {
                issues.Add($"[A1] {Path.GetFileName(uxmlPath)}: malformed XML — {ex.Message}");
                return;  // Can't continue parsing if XML is invalid.
            }

            string dir = Path.GetDirectoryName(uxmlPath) ?? "";

            // A2: broken <Style src="..."> — referenced file does not exist on disk.
            foreach (var el in doc.Descendants())
            {
                if (!el.Name.LocalName.Equals("Style", StringComparison.OrdinalIgnoreCase)) continue;
                string src = (string)el.Attribute("src");
                if (src == null) continue;
                string resolved = Path.GetFullPath(Path.Combine(dir, src));
                if (!File.Exists(resolved))
                    issues.Add($"[A2] {Path.GetFileName(uxmlPath)}: broken <Style src=\"{src}\">");
            }

            // A3: missing <Template src="..."> — referenced file does not exist on disk.
            foreach (var el in doc.Descendants())
            {
                if (!el.Name.LocalName.Equals("Template", StringComparison.OrdinalIgnoreCase)) continue;
                string src = (string)el.Attribute("src");
                if (src == null) continue;
                string resolved = Path.GetFullPath(Path.Combine(dir, src));
                if (!File.Exists(resolved))
                    issues.Add($"[A3] {Path.GetFileName(uxmlPath)}: missing <Template src=\"{src}\">");
            }

            // A4: interactive elements without a name attribute (or empty name).
            var interactiveTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Button", "Toggle", "Slider", "TextField" };
            foreach (var el in doc.Descendants())
            {
                if (!interactiveTypes.Contains(el.Name.LocalName)) continue;
                string name = (string)el.Attribute("name");
                if (string.IsNullOrEmpty(name))
                    issues.Add($"[A4] {Path.GetFileName(uxmlPath)}: <{el.Name.LocalName}> has no name — add name=\"...\" for accessibility and scripting");
            }
        }

        // USS checks: A5 (duplicate selectors), A6 (empty rules).
        private static void LintUssContent(string text, List<string> issues)
        {
            // Strip CSS block comments so selectors inside comments are not matched.
            text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);

            // A5: find duplicate selectors.
            // Match selector blocks: "selector { ... }" — simplified: capture text before {.
            var selectorPattern = new Regex(@"([^{}]+?)\s*\{", RegexOptions.Multiline);
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in selectorPattern.Matches(text))
            {
                string sel = m.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(sel)) continue;
                if (seen.ContainsKey(sel))
                    issues.Add($"[A5] duplicate selector \"{sel}\"");
                else
                    seen[sel] = 1;
            }

            // A6: empty rules — selector followed by block with only whitespace.
            var emptyRulePattern = new Regex(@"([^{}]+?)\s*\{\s*\}", RegexOptions.Multiline);
            foreach (Match m in emptyRulePattern.Matches(text))
            {
                string sel = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(sel))
                    issues.Add($"[A6] empty rule for selector \"{sel}\"");
            }
        }
    }
}
