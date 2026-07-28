using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class StatusAmbientAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var container = new VisualElement();
            container.AddToClassList("status-ambient");
            container.style.position = Position.Absolute;
            container.style.top  = 0; container.style.left   = 0;
            container.style.right = 0; container.style.bottom = 0;

            var scanline = new VisualElement();
            scanline.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            scanline.AddToClassList("status-scanline");
            container.Add(scanline);

            var grid = new VisualElement();
            grid.usageHints |= UsageHints.DynamicColor;
            grid.AddToClassList("status-grid");
            var dots = new VisualElement[16];
            for (int i = 0; i < 16; i++)
            {
                var dot = new VisualElement();
                dot.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                dot.AddToClassList("status-grid-dot");
                grid.Add(dot);
                dots[i] = dot;
            }
            container.Add(grid);

            var sonar = new VisualElement();
            sonar.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            sonar.AddToClassList("status-sonar");
            container.Add(sonar);

            string previousState = null;
            void RefreshState()
            {
                string conn = ArcadePalette.StateClass;
                if (conn == previousState)
                    return;
                BiomeUI.SetExclusiveClass(
                    grid,
                    conn,
                    "conn-up",
                    "conn-listen",
                    "conn-down");
                previousState = conn;
            }

            RefreshState();
            container.schedule.Execute(RefreshState).Every(700);
            ArcadeAnim.SmoothLoop(container, elapsed =>
            {
                float sweep = Mathf.Repeat(elapsed / 2.8f, 1f);
                float height = container.resolvedStyle.height;
                float travel = float.IsNaN(height) ? 0f : height + 2f;
                scanline.style.translate = new Translate(0f, sweep * travel);
                scanline.style.opacity = Mathf.Sin(sweep * Mathf.PI) * 0.72f;

                float sonarLife = Mathf.Repeat(elapsed / 2.2f, 1f);
                float sonarScale = 0.72f + sonarLife * 2.55f;
                sonar.style.scale = new Scale(new Vector3(
                    sonarScale,
                    sonarScale,
                    1f));
                sonar.style.opacity = Mathf.Sin(sonarLife * Mathf.PI) * 0.35f;

                for (int i = 0; i < dots.Length; i++)
                {
                    float wave = 0.5f + 0.5f
                        * Mathf.Sin(elapsed * (1.1f + i * 0.018f) + i * 1.31f);
                    float scale = 0.72f + wave * 0.48f;
                    dots[i].style.scale = new Scale(new Vector3(
                        scale,
                        scale,
                        1f));
                    dots[i].style.opacity = 0.24f + wave * 0.72f;
                }
            });

            return container;
        }
    }
}
