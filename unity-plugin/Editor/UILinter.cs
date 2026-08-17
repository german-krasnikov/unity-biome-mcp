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

            CheckScrollRects(issues, rootGO);
            CheckGeneralLayout(issues, rootGO);

            if (issues.Count == 0) return "ok: 0 issues";
            return string.Join("\n", issues);
        }

        // SR1-SR5: ScrollRect structural checks.
        private static void CheckScrollRects(List<string> issues, GameObject rootGO)
        {
            var scrollRects = UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None);
            foreach (var sr in scrollRects)
            {
                if (!InScope(sr, rootGO)) continue;
                string n = sr.gameObject.name;

                if (sr.content == null)
                    issues.Add($"[S5] ScrollRect '{n}': content is null — assign a RectTransform to ScrollRect.content");

                if (sr.viewport == null)
                {
                    issues.Add($"[S1] ScrollRect '{n}': viewport is null — wire viewport before checking anchors");
                }
                else
                {
                    var vp = sr.viewport;
                    if (vp.anchorMin != Vector2.zero || vp.anchorMax != Vector2.one)
                        issues.Add($"[S1] ScrollRect '{n}': Viewport anchors are not full-stretch " +
                                   $"(anchorMin={vp.anchorMin}, anchorMax={vp.anchorMax}) — set to stretch (0,0)→(1,1)");
                }

                bool contentCanGrow = sr.content != null &&
                    (sr.content.GetComponent<ContentSizeFitter>() != null
                     || sr.content.GetComponent<LayoutGroup>() != null
                     || sr.content.sizeDelta.x > 0.1f
                     || sr.content.sizeDelta.y > 0.1f);
                if (sr.content != null && !contentCanGrow)
                    issues.Add($"[S2] ScrollRect '{n}': Content has no ContentSizeFitter/LayoutGroup " +
                               "and sizeDelta is zero — content cannot grow");

                bool rootHasMask = sr.GetComponent<Mask>() != null;
                bool vpHasMask   = sr.viewport != null && sr.viewport.GetComponent<Mask>() != null;
                // S3a: Mask on root is always wrong (clipping belongs on Viewport only)
                if (rootHasMask)
                    issues.Add($"[S3] ScrollRect '{n}': Mask on root — remove it; Mask belongs on Viewport only");
                // S3b: Viewport missing Mask → content won't be clipped
                if (sr.viewport != null && !vpHasMask)
                    issues.Add($"[S3b] ScrollRect '{n}': Viewport missing Mask — content will not be clipped");

                // S6: horizontal=true but Content has no effective width
                if (sr.content != null && sr.viewport != null && sr.horizontal)
                {
                    var ct = sr.content;
                    bool horizontalStretch = UnityEngine.Mathf.Approximately(ct.anchorMin.x, 0f)
                                         && UnityEngine.Mathf.Approximately(ct.anchorMax.x, 1f);
                    if (!horizontalStretch && ct.sizeDelta.x <= 0.1f)
                        issues.Add($"[S6] ScrollRect '{n}': horizontal=true but Content has no width " +
                                   "(anchorMax.x=0 and sizeDelta.x=0) — set anchorMax.x=1 or sizeDelta.x>0");
                }

                bool hasScrollbarObj = sr.GetComponentInChildren<Scrollbar>() != null;
                if (hasScrollbarObj && sr.horizontalScrollbar == null && sr.verticalScrollbar == null)
                    issues.Add($"[S4] ScrollRect '{n}': Scrollbar found in children but not wired — " +
                               "set ScrollRect.horizontalScrollbar or verticalScrollbar");
            }
        }

        // G1-G3: General uGUI layout checks.
        private static void CheckGeneralLayout(List<string> issues, GameObject rootGO)
        {
            // G1: Active RectTransform with point anchor and zero size.
            foreach (var rt in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                if (!InScope(rt, rootGO)) continue;
                if (!rt.gameObject.activeSelf) continue;
                bool isPointAnchor = rt.anchorMin == rt.anchorMax;
                if (isPointAnchor && rt.sizeDelta.x <= 0f && rt.sizeDelta.y <= 0f)
                    issues.Add($"[G1] '{ComponentSerializer.GetPath(rt.gameObject)}': " +
                               "active RectTransform has zero size — set size or use stretch anchor");
            }

            // G2: Image without sprite + raycastTarget=true + no interactable ancestor.
            foreach (var img in UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                if (!InScope(img, rootGO)) continue;
                if (img.sprite != null || !img.raycastTarget) continue;
                if (img.GetComponentInParent<Selectable>() == null)
                    issues.Add($"[G2] Image '{ComponentSerializer.GetPath(img.gameObject)}': " +
                               "no sprite + raycastTarget=true with no interactable ancestor — blocks raycasts invisibly");
            }

            // G3: LayoutGroup with no active children.
            foreach (var lg in UnityEngine.Object.FindObjectsByType<LayoutGroup>(FindObjectsSortMode.None))
            {
                if (!InScope(lg, rootGO)) continue;
                int activeChildren = 0;
                for (int i = 0; i < lg.transform.childCount; i++)
                    if (lg.transform.GetChild(i).gameObject.activeSelf) activeChildren++;
                if (activeChildren == 0)
                    issues.Add($"[G3] LayoutGroup '{ComponentSerializer.GetPath(lg.gameObject)}': " +
                               "no active children — remove or populate the layout group");
            }
        }

        // Returns true when component c is within rootGO scope (or scope is null = all scenes).
        private static bool InScope(Component c, GameObject rootGO) =>
            rootGO == null || c.transform.IsChildOf(rootGO.transform);

        // Called by ExecLintUITK.
        // path: absolute path to a .uxml or .uss file.
        // fix: retained for API compatibility; auto-fix is not implemented.
        // Returns: "ok: 0 issues" or "warn: N issues\n[AX] ..." text.
        internal static string LintUITK(string path, bool fix)
        {
            // Fail before probing or reading the path. Silently accepting fix=true would
            // make callers believe a mutation happened even though this tool is read-only.
            if (fix)
                return "err: fix=true is not supported; lint_uitk is read-only. Call with fix=false.";
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
