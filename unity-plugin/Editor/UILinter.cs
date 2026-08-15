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
            bool hasEventSystem = Object.FindFirstObjectByType<EventSystem>() != null;
            if (!hasEventSystem)
                issues.Add("warn: no EventSystem in scene — uGUI clicks will not work; " +
                           "add GameObject > UI > Event System");

            // Check GraphicRaycaster on every Canvas within scope.
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
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
        // Checks structural rules for .uxml/.uss files — A1/A2/A3/A5/A14.
        // NO CSS property whitelist (A4 decision — Unity importer handles those).
        // fix=true: normalizes format (does not remove valid CSS properties).
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

            var issues = new List<string>();
            var text   = File.ReadAllText(path);

            if (isUxml) LintUxml(text, path, issues);
            else        LintUss(text, issues);

            if (issues.Count == 0) return $"ok: {path} (0 issues)";
            return $"{issues.Count} issues in {path}:\n" + string.Join("\n", issues.ConvertAll(i => $"  {i}"));
        }

        private static void LintUxml(string text, string path, List<string> issues)
        {
            // A1: well-formed XML
            XDocument doc;
            try { doc = XDocument.Parse(text); }
            catch (Exception ex) { issues.Add($"XML parse error: {ex.Message}"); return; }

            // A1: broken <Style src> references
            foreach (var el in doc.Descendants())
            {
                if (el.Name.LocalName != "Style") continue;
                var src = el.Attribute("src")?.Value;
                if (src == null) { issues.Add("missing <Style src> attribute"); continue; }
                var dir      = Path.GetDirectoryName(path) ?? ".";
                var resolved = Path.GetFullPath(Path.Combine(dir, src.Split('?')[0]));
                if (!File.Exists(resolved))
                    issues.Add($"broken <Style src=\"{src}\"> — file not found");
            }

            // A5: inline style= attributes
            foreach (var el in doc.Descendants())
            {
                if (el.Attribute("style") != null)
                    issues.Add($"inline style= on <{el.Name.LocalName}> — use USS classes");
            }
        }

        private static void LintUss(string text, List<string> issues)
        {
            // A2: CamelCase class names (single uppercase word, not kebab)
            var camelClass = new Regex(@"\.([A-Z][a-zA-Z0-9]*)");
            foreach (Match m in camelClass.Matches(text))
                issues.Add($"CamelCase class .{m.Groups[1].Value} — use kebab-case");

            // A3: star selector
            if (Regex.IsMatch(text, @"^\s*\*\s*\{", RegexOptions.Multiline))
                issues.Add("star selector * {} — avoid, targets all elements");

            // A14: duplicate CSS variables within file
            var varPattern = new Regex(@"(--[\w-]+)\s*:");
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Match m in varPattern.Matches(text))
            {
                var name = m.Groups[1].Value;
                seen.TryGetValue(name, out int count);
                if (count == 1) issues.Add($"duplicate variable {name}");
                seen[name] = count + 1;
            }
        }
    }
}
