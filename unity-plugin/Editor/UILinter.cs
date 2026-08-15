using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityMCP.Editor
{
    // G2: Structural uGUI diagnostics.
    // LintUITK is added in Session 4.
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
    }
}
