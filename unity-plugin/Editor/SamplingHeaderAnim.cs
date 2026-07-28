using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class SamplingHeaderAnim
    {
        // Pre-baked height ratios (0.2–1.0) per bar — organic feel, no Math.Random
        private static readonly float[][] _patterns =
        {
            new[] { 0.3f, 0.6f, 0.9f, 0.5f, 0.2f, 0.7f, 0.4f, 0.8f },
            new[] { 0.7f, 0.4f, 0.2f, 0.8f, 0.6f, 0.3f, 1.0f, 0.5f },
            new[] { 0.5f, 0.9f, 0.6f, 0.3f, 1.0f, 0.4f, 0.7f, 0.2f },
            new[] { 1.0f, 0.3f, 0.7f, 0.5f, 0.2f, 0.9f, 0.4f, 0.6f },
            new[] { 0.4f, 0.8f, 0.3f, 1.0f, 0.6f, 0.2f, 0.9f, 0.5f },
            new[] { 0.6f, 0.2f, 1.0f, 0.4f, 0.8f, 0.5f, 0.3f, 0.7f },
            new[] { 0.8f, 0.5f, 0.4f, 0.7f, 0.3f, 1.0f, 0.6f, 0.2f },
        };

        public static VisualElement Build(VisualElement scheduleHost)
        {
            var container = new VisualElement();
            container.AddToClassList("freq-root");

            var bars = new VisualElement[7];
            for (int i = 0; i < 7; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("freq-bar");
                bar.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                container.Add(bar);
                bars[i] = bar;
            }

            var particles = BiomeAmbientParticles.Attach(
                container,
                BiomeParticlePattern.Sampling);
            string previousState = null;

            void RefreshState()
            {
                string conn = ArcadePalette.StateClass;
                for (int i = 0; i < 7; i++)
                {
                    if (conn == previousState)
                        continue;
                    BiomeUI.SetExclusiveClass(
                        bars[i],
                        conn,
                        "conn-up",
                        "conn-listen",
                        "conn-down");
                }
                if (conn != previousState)
                    particles.SetState(conn);
                previousState = conn;
            }

            RefreshState();
            container.schedule.Execute(RefreshState).Every(600);
            ArcadeAnim.SmoothLoop(container, elapsed =>
            {
                float sequence = Mathf.PingPong(
                    elapsed * 1.55f,
                    _patterns[0].Length - 1f);
                int from = Mathf.FloorToInt(sequence);
                int to = System.Math.Min(from + 1, _patterns[0].Length - 1);
                float blend = Mathf.SmoothStep(0f, 1f, sequence - from);

                for (int i = 0; i < bars.Length; i++)
                {
                    float ratio = Mathf.Lerp(
                        _patterns[i][from],
                        _patterns[i][to],
                        blend);
                    ratio += Mathf.Sin(elapsed * 0.72f + i * 1.17f) * 0.035f;
                    ratio = Mathf.Clamp(ratio, 0.14f, 1f);
                    bars[i].style.scale = new Scale(new Vector3(1f, ratio, 1f));
                    bars[i].style.opacity = 0.58f + ratio * 0.42f;
                }
            });

            return container;
        }
    }
}
