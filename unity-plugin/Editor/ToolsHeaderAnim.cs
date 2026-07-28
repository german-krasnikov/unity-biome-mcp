using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class ToolsHeaderAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var container = new VisualElement();
            container.AddToClassList("anim-tools");

            var tracks = new VisualElement[5];
            var knobs = new VisualElement[5];
            for (int i = 0; i < 5; i++)
            {
                var track = new VisualElement();
                track.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                track.AddToClassList("toggle-track");
                var knob = new VisualElement();
                knob.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                knob.AddToClassList("toggle-knob");
                track.Add(knob);
                container.Add(track);
                tracks[i] = track;
                knobs[i] = knob;
            }

            var particles = BiomeAmbientParticles.Attach(
                container,
                BiomeParticlePattern.Tools);
            int previousEnabled = -1;
            int previousTotal = -1;
            int previousScan = -1;

            void Refresh()
            {
                string[] tools = MCPSettings.GetToolNames();
                int enabled = tools.Count(MCPSettings.IsToolEnabled);
                if (enabled == previousEnabled && tools.Length == previousTotal)
                    return;

                int activeCount = tools.Length == 0
                    ? 0
                    : System.Math.Max(1, (int)System.Math.Round(enabled * 5d / tools.Length));
                for (int i = 0; i < 5; i++)
                {
                    bool isEnabled = i < activeCount && enabled > 0;
                    knobs[i].EnableInClassList("on", isEnabled);
                    BiomeUI.SetExclusiveClass(
                        knobs[i],
                        isEnabled ? "conn-up" : string.Empty,
                        "conn-up",
                        "conn-listen",
                        "conn-down");
                }
                previousEnabled = enabled;
                previousTotal = tools.Length;
                container.tooltip = $"{enabled} of {tools.Length} tools enabled";
                particles.SetState(enabled > 0 ? "up" : "down");
            }

            Refresh();
            container.schedule.Execute(Refresh).Every(900);
            ArcadeAnim.SmoothLoop(container, elapsed =>
            {
                float scanPosition = (0.5f - 0.5f
                    * Mathf.Cos(elapsed * Mathf.PI * 2f / 4.2f))
                    * (tracks.Length - 1);
                int scanIndex = Mathf.RoundToInt(scanPosition);

                for (int i = 0; i < tracks.Length; i++)
                {
                    float influence = Mathf.Clamp01(1f - Mathf.Abs(i - scanPosition));
                    float spark = 0.5f + 0.5f
                        * Mathf.Sin(elapsed * (3.2f + i * 0.11f) + i * 1.3f);
                    tracks[i].style.scale = new Scale(new Vector3(
                        1f + influence * 0.12f,
                        1f + influence * 0.12f,
                        1f));
                    tracks[i].style.translate = new Translate(
                        0f,
                        -influence * (1.5f + spark));
                    float knobScale = 1f + influence
                        * (knobs[i].ClassListContains("on") ? 0.25f : 0.10f);
                    knobs[i].style.scale = new Scale(new Vector3(
                        knobScale,
                        knobScale,
                        1f));
                }

                if (scanIndex != previousScan)
                {
                    for (int i = 0; i < tracks.Length; i++)
                    {
                        bool scanned = i == scanIndex;
                        tracks[i].EnableInClassList("toggle-track--scan", scanned);
                        knobs[i].EnableInClassList(
                            "toggle-knob--pulse",
                            scanned && knobs[i].ClassListContains("on"));
                    }
                    previousScan = scanIndex;
                }
            });

            return container;
        }
    }
}
