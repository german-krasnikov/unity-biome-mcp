using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class LevelUpAnimator
    {
        const int SparkCount  = 5;
        const int DurationMs = 1480;

        internal static VisualElement BuildIdleSignal()
        {
            var signal = CreateLiftSymbol(
                "lvlup-idle-signal",
                out var arrow,
                out var auraInner,
                out var auraOuter);
            ArcadeAnim.SmoothLoop(signal, elapsed =>
            {
                float lift = 0.5f - 0.5f
                    * Mathf.Cos(elapsed * Mathf.PI * 2f / 2.2f);
                ApplyLiftMotion(
                    arrow,
                    auraInner,
                    auraOuter,
                    lift,
                    5f - lift * 12f,
                    elapsed);
            });
            return signal;
        }

        internal static VisualElement Build(
            VisualElement scheduleHost,
            string fromVersion,
            string toVersion,
            Action onComplete)
        {
            var root = new VisualElement();
            root.AddToClassList("lvlup-anim-root");

            var versionLabel = new Label($"v{fromVersion}  →  v{toVersion}");
            versionLabel.AddToClassList("lvlup-version");
            root.Add(versionLabel);

            var liftSymbol = CreateLiftSymbol(
                "lvlup-lift-symbol",
                out var arrow,
                out var auraInner,
                out var auraOuter);
            root.Add(liftSymbol);

            var track = new VisualElement();
            track.AddToClassList("lvlup-xp-track");
            var fill = new VisualElement();
            fill.AddToClassList("lvlup-xp-fill");
            fill.usageHints |= UsageHints.DynamicTransform;
            track.Add(fill);
            root.Add(track);

            var sparkContainer = new VisualElement();
            sparkContainer.AddToClassList("lvlup-spark-container");
            sparkContainer.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < SparkCount; i++)
            {
                var spark = new VisualElement();
                spark.pickingMode = PickingMode.Ignore;
                spark.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                spark.AddToClassList("lvlup-spark");
                spark.AddToClassList($"lvlup-spark--pos-{i + 1}");
                sparkContainer.Add(spark);
            }
            root.Add(sparkContainer);

            bool completed = false;
            bool flashed = false;
            void CompleteOnce()
            {
                if (completed) return;
                completed = true;
                fill.AddToClassList("lvlup-xp-fill--done");
#if UNITY_INCLUDE_TESTS
                _lastOnComplete = null;
#endif
                onComplete?.Invoke();
            }

            ArcadeAnim.SmoothLoop(root, elapsed =>
            {
                float progress = Mathf.Clamp01(elapsed / (DurationMs / 1000f));
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                fill.style.scale = new Scale(new Vector3(eased, 1f, 1f));

                if (!flashed && progress >= 0.38f)
                {
                    versionLabel.AddToClassList("lvlup-version-flash");
                    flashed = true;
                }

                float lift = Mathf.SmoothStep(0f, 1f, progress);
                ApplyLiftMotion(
                    arrow,
                    auraInner,
                    auraOuter,
                    lift,
                    10f - lift * 28f,
                    elapsed);

                for (int i = 0; i < SparkCount; i++)
                {
                    var spark = sparkContainer.ElementAt(i);
                    float life = Mathf.Repeat(progress * 1.45f + i * 0.23f, 1f);
                    float sideways = Mathf.Sin(
                        elapsed * (2.2f + i * 0.17f) + i * 1.7f)
                        * (3f + i);
                    float rise = 10f - life * (20f + i * 2.5f);
                    float sparkScale = 0.55f
                        + Mathf.Sin(life * Mathf.PI) * 0.95f;
                    spark.style.translate = new Translate(sideways, rise);
                    spark.style.scale = new Scale(new Vector3(
                        sparkScale,
                        sparkScale,
                        1f));
                    spark.style.opacity = Mathf.Sin(life * Mathf.PI) * 0.92f;
                }

                if (progress >= 1f)
                    CompleteOnce();
            });

#if UNITY_INCLUDE_TESTS
            // Keep only the caller callback. Retaining CompleteOnce would retain
            // the full visual tree if a test-enabled editor closed mid-animation.
            _lastOnComplete = onComplete;
#endif
            return root;
        }

        private static VisualElement CreateLiftSymbol(
            string modifier,
            out Label arrow,
            out VisualElement auraInner,
            out VisualElement auraOuter)
        {
            var symbol = new VisualElement();
            symbol.pickingMode = PickingMode.Ignore;
            symbol.AddToClassList("lvlup-symbol");
            symbol.AddToClassList(modifier);

            auraOuter = new VisualElement { pickingMode = PickingMode.Ignore };
            auraOuter.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            auraOuter.AddToClassList("lvlup-symbol-aura");
            auraOuter.AddToClassList("lvlup-symbol-aura--outer");
            symbol.Add(auraOuter);

            auraInner = new VisualElement { pickingMode = PickingMode.Ignore };
            auraInner.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            auraInner.AddToClassList("lvlup-symbol-aura");
            auraInner.AddToClassList("lvlup-symbol-aura--inner");
            symbol.Add(auraInner);

            arrow = new Label("↑") { pickingMode = PickingMode.Ignore };
            arrow.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            arrow.AddToClassList("lvlup-symbol-arrow");
            symbol.Add(arrow);
            return symbol;
        }

        private static void ApplyLiftMotion(
            Label arrow,
            VisualElement auraInner,
            VisualElement auraOuter,
            float lift,
            float translateY,
            float elapsed)
        {
            float shimmer = 0.5f + 0.5f * Mathf.Sin(elapsed * 5.4f);
            arrow.style.translate = new Translate(0f, translateY);
            float arrowScale = 0.88f + lift * 0.25f + shimmer * 0.05f;
            arrow.style.scale = new Scale(new Vector3(
                arrowScale,
                arrowScale,
                1f));
            arrow.style.opacity = 0.74f + lift * 0.26f;

            float innerScale = 0.70f + lift * 0.48f;
            auraInner.style.scale = new Scale(new Vector3(
                innerScale,
                innerScale,
                1f));
            auraInner.style.opacity = 0.11f + lift * 0.30f;

            float outerScale = 0.86f + lift * 0.52f;
            auraOuter.style.scale = new Scale(new Vector3(
                outerScale,
                outerScale,
                1f));
            auraOuter.style.opacity = 0.04f + lift * 0.17f;
        }

#if UNITY_INCLUDE_TESTS
        static Action _lastOnComplete;

        /// <summary>Test-only: fire onComplete exactly as the scheduler would at TotalTicks.</summary>
        internal static void SimulateCompletion()
        {
            var cb = _lastOnComplete;
            _lastOnComplete = null;
            cb?.Invoke();
        }
#endif
    }
}
