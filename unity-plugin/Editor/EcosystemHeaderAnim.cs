using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>Shared semantic header visual for plugin modules and version history.</summary>
    internal static class EcosystemHeaderAnim
    {
        private const int NodeCount = 7;

        internal static VisualElement BuildPlugins()
        {
            var root = BuildBase(
                "eco-root--plugins",
                BiomeParticlePattern.Ecosystem);
            var nodes = root.ElementAt(1);
            int activeCount = 0;
            int previousCount = -1;
            int previousPulse = -1;

            void Refresh()
            {
                int count = PluginRegistry.All.Count(plugin => plugin.HasSettingsUI);
                activeCount = Math.Min(NodeCount, count);
                if (count != previousCount)
                {
                    for (int i = 0; i < NodeCount; i++)
                        nodes.ElementAt(i)
                            .EnableInClassList("eco-node--active", i < activeCount);
                    previousCount = count;
                    root.tooltip = $"{count} plugin settings modules";
                }
            }

            Refresh();
            root.schedule.Execute(Refresh).Every(900);
            ArcadeAnim.SmoothLoop(root, elapsed =>
            {
                float pulsePosition = activeCount <= 1
                    ? 0f
                    : (0.5f - 0.5f
                        * Mathf.Cos(elapsed * Mathf.PI * 2f / 4.6f))
                        * (activeCount - 1);
                int pulse = activeCount > 0 ? Mathf.RoundToInt(pulsePosition) : -1;

                for (int i = 0; i < NodeCount; i++)
                {
                    float influence = i < activeCount
                        ? Mathf.Clamp01(1f - Mathf.Abs(i - pulsePosition))
                        : 0f;
                    float breathing = i < activeCount
                        ? 0.5f + 0.5f * Mathf.Sin(elapsed * 1.2f + i * 0.8f)
                        : 0f;
                    float scale = 1f + influence * 0.30f + breathing * 0.035f;
                    nodes.ElementAt(i).style.scale = new Scale(new Vector3(
                        scale,
                        scale,
                        1f));
                }

                if (pulse != previousPulse)
                {
                    for (int i = 0; i < NodeCount; i++)
                        nodes.ElementAt(i).EnableInClassList(
                            "eco-node--pulse",
                            i == pulse);
                    previousPulse = pulse;
                }
            });
            return root;
        }

        internal static VisualElement BuildVersions()
        {
            var root = BuildBase(
                "eco-root--versions",
                BiomeParticlePattern.Timeline);
            root.tooltip = "Release history";

            var nodes = root.ElementAt(1);
            int previousScan = -1;
            ArcadeAnim.SmoothLoop(root, elapsed =>
            {
                float scanPosition = (0.5f - 0.5f
                    * Mathf.Cos(elapsed * Mathf.PI * 2f / 5.2f))
                    * (NodeCount - 1);
                int scan = Mathf.RoundToInt(scanPosition);

                for (int i = 0; i < NodeCount; i++)
                {
                    float influence = Mathf.Clamp01(1f - Mathf.Abs(i - scanPosition));
                    float scale = 1f + influence * 0.28f;
                    nodes.ElementAt(i).style.scale = new Scale(new Vector3(
                        scale,
                        scale,
                        1f));
                }

                if (scan != previousScan)
                {
                    for (int i = 0; i < NodeCount; i++)
                        nodes.ElementAt(i)
                            .EnableInClassList("eco-node--scan", i == scan);
                    previousScan = scan;
                }
            });
            return root;
        }

        internal static void SetVersionIndex(VisualElement root, int index, int total)
        {
            int normalized = total <= 1
                ? 0
                : (int)Math.Round(index * (NodeCount - 1d) / (total - 1d));
            for (int i = 0; i < NodeCount; i++)
                root.ElementAt(1).ElementAt(i)
                    .EnableInClassList("eco-node--selected", i == normalized);
            ArcadeAnim.PulseOnce(root.ElementAt(1).ElementAt(normalized));
        }

        private static VisualElement BuildBase(
            string modifier,
            BiomeParticlePattern particlePattern)
        {
            var root = new VisualElement();
            root.AddToClassList("eco-root");
            root.AddToClassList(modifier);

            var left = new VisualElement();
            left.AddToClassList("eco-line");
            root.Add(left);

            var nodes = new VisualElement();
            nodes.AddToClassList("eco-nodes");
            for (int i = 0; i < NodeCount; i++)
            {
                var node = new VisualElement();
                node.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                node.AddToClassList("eco-node");
                nodes.Add(node);
            }
            root.Add(nodes);

            var right = new VisualElement();
            right.AddToClassList("eco-line");
            root.Add(right);
            var particles = BiomeAmbientParticles.Attach(root, particlePattern);
            particles.SetState("up");
            return root;
        }
    }
}
