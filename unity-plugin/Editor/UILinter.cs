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

            // Check EventSystem presence (required for uGUI click dispatch).
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                issues.Add("warn: no EventSystem in scene — uGUI clicks will not work; " +
                           "add GameObject > UI > Event System");

            // Check GraphicRaycaster on every Canvas.
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    issues.Add($"warn: Canvas '{canvas.name}' missing GraphicRaycaster — " +
                               "add it for raycast-based interaction");
            }

            if (issues.Count == 0) return "ok: 0 issues";
            return string.Join("\n", issues);
        }
    }
}
