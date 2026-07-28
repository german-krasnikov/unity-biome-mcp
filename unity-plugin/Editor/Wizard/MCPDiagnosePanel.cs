using System.IO;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor
{
    /// <summary>Health dashboard panel — animated diagnostic rows.</summary>
    public static class MCPDiagnosePanel
    {
        public static VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("wiz-container");

            var ss = MCPEditorUtils.LoadStyleSheet("Wizard/SetupWizard.uss");
            if (ss != null) root.styleSheets.Add(ss);

            var title = new Label("Health Check");
            title.AddToClassList("wiz-title");
            root.Add(title);

            var (dots, dotsTimer) = BuildScanDots();
            root.Add(dots);

            EditorApplication.delayCall += () => RunDiagnostics(root, dots, dotsTimer);

            return root;
        }

        static void RunDiagnostics(VisualElement root, VisualElement dots, IVisualElementScheduledItem dotsTimer)
        {
            dotsTimer.Pause();
            dots.style.display = DisplayStyle.None;

            var pkgPath   = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MCPDiagnosePanel).Assembly)?.resolvedPath ?? "";
            var serverDir = Path.Combine(pkgPath, "..", "server");

            var (pyOk, pyDetail)      = SetupDiagnostics.CheckPython(serverDir);
            var (srvOk, srvDetail)    = SetupDiagnostics.CheckServer();
            var compileOk             = !CompileErrorCapture.HasErrors();
            var compileDetail         = compileOk ? "no errors" : "compile errors present";
            var (uvOk, uvDetail)      = SetupDiagnostics.CheckUv();

            var results = new[]
            {
                ("Python",  pyOk,      pyDetail),
                ("Server",  srvOk,     srvDetail),
                ("Compile", compileOk, compileDetail),
                ("uv",      uvOk,      uvDetail),
            };

            for (int i = 0; i < results.Length; i++)
            {
                var (label, ok, detail) = results[i];
                var row = BuildStatusRow(label, ok, detail);
                root.Add(row);
                WizardAnimUtils.FadeIn(row, i * 80);
            }
        }

        public static VisualElement BuildStatusRow(string label, bool ok, string detail)
        {
            var row = new VisualElement();
            row.AddToClassList("wiz-status-row");

            var icon = new Label(ok ? "✓" : "✗");
            icon.AddToClassList("wiz-status-icon");
            icon.AddToClassList(ok ? "wiz-status-ok" : "wiz-status-fail");
            row.Add(icon);

            var text = new Label($"{label}    {detail}");
            row.Add(text);

            return row;
        }

        public static (VisualElement container, IVisualElementScheduledItem timer) BuildScanDots()
        {
            var container = new VisualElement();
            container.AddToClassList("wiz-dots");

            int lit = 0;
            for (int i = 0; i < 3; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("wiz-scan-dot");
                container.Add(dot);
            }

            // Pulse each dot in turn at 250ms intervals
            var timer = container.schedule.Execute(() =>
            {
                var dots = container.Query<VisualElement>(className: "wiz-scan-dot").ToList();
                for (int i = 0; i < dots.Count; i++)
                {
                    if (i == lit) dots[i].AddToClassList("wiz-scan-dot--lit");
                    else          dots[i].RemoveFromClassList("wiz-scan-dot--lit");
                }
                lit = (lit + 1) % dots.Count;
            }).Every(250);

            return (container, timer);
        }
    }
}
