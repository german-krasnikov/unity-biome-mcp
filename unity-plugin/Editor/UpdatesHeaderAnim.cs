using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class UpdatesHeaderAnim
    {
        private const int BarCount = 5;
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var container = new VisualElement();
            container.AddToClassList("anim-updates");

            var bars = new VisualElement[BarCount];
            for (int i = 0; i < BarCount; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("upload-bar");
                bar.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                container.Add(bar);
                bars[i] = bar;
            }

            var levelUp = new VisualElement();
            levelUp.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            levelUp.AddToClassList("levelup-symbol");
            var auraOuter = new VisualElement();
            auraOuter.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            auraOuter.AddToClassList("levelup-aura");
            auraOuter.AddToClassList("levelup-aura--outer");
            var auraInner = new VisualElement();
            auraInner.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            auraInner.AddToClassList("levelup-aura");
            auraInner.AddToClassList("levelup-aura--inner");
            var arrow = new Label("↑");
            arrow.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            arrow.AddToClassList("levelup-arrow");
            levelUp.Add(auraOuter);
            levelUp.Add(auraInner);
            levelUp.Add(arrow);
            container.Add(levelUp);

            var particles = BiomeAmbientParticles.Attach(
                container,
                BiomeParticlePattern.Updates);
            bool checking = false;
            bool hasUpdate = false;
            string previousState = null;
            int previousScan = -1;

            void RefreshState()
            {
                checking = container.ClassListContains("updates-checking");
                hasUpdate = UpdateChecker.HasUpdate;
                string state = checking || hasUpdate ? "listen" : "up";
                if (state != previousState)
                {
                    foreach (var bar in bars)
                        BiomeUI.SetExclusiveClass(
                            bar,
                            "conn-" + state,
                            "conn-up",
                            "conn-listen",
                            "conn-down");
                    BiomeUI.SetExclusiveClass(
                        levelUp,
                        "conn-" + state,
                        "conn-up",
                        "conn-listen",
                        "conn-down");
                    particles.SetState(state);
                    previousState = state;
                }
            }

            RefreshState();
            container.schedule.Execute(RefreshState).Every(600);
            ArcadeAnim.SmoothLoop(container, elapsed =>
            {
                float scanPosition = (0.5f - 0.5f
                    * Mathf.Cos(elapsed * Mathf.PI * 2f / 3.8f))
                    * (BarCount - 1);
                int scanIndex = Mathf.RoundToInt(scanPosition);

                for (int i = 0; i < BarCount; i++)
                {
                    float distance = Mathf.Abs(i - scanPosition);
                    float wave = Mathf.Exp(-distance * distance * 0.85f);
                    float ratio = checking
                        ? Mathf.Lerp(0.16f, 1f, wave)
                        : hasUpdate
                            ? Mathf.Clamp01(0.30f + i * 0.12f + wave * 0.28f)
                            : Mathf.Clamp(0.16f + i * 0.07f + wave * 0.22f, 0.14f, 0.78f);
                    bars[i].style.scale = new Scale(new Vector3(1f, ratio, 1f));
                    bars[i].style.opacity = 0.52f + wave * 0.48f;
                }

                if (scanIndex != previousScan)
                {
                    for (int i = 0; i < BarCount; i++)
                        bars[i].EnableInClassList("upload-bar--active", i == scanIndex);
                    previousScan = scanIndex;
                }

                float lift = 0.5f - 0.5f
                    * Mathf.Cos(elapsed * Mathf.PI * 2f / 2.4f);
                float shimmer = 0.5f + 0.5f * Mathf.Sin(elapsed * 5.1f);
                arrow.style.translate = new Translate(0f, 4f - lift * 13f);
                float arrowScale = 0.88f + lift * 0.23f + shimmer * 0.05f;
                arrow.style.scale = new Scale(new Vector3(
                    arrowScale,
                    arrowScale,
                    1f));
                arrow.style.opacity = 0.72f + lift * 0.28f;

                float innerScale = 0.72f + lift * 0.42f;
                auraInner.style.scale = new Scale(new Vector3(
                    innerScale,
                    innerScale,
                    1f));
                auraInner.style.opacity = 0.10f + lift * 0.27f;
                float outerScale = 0.86f + lift * 0.46f;
                auraOuter.style.scale = new Scale(new Vector3(
                    outerScale,
                    outerScale,
                    1f));
                auraOuter.style.opacity = 0.04f + lift * 0.14f;
            });

            return container;
        }

        internal static void SetChecking(VisualElement header, bool checking) =>
            header.EnableInClassList("updates-checking", checking);
    }
}
