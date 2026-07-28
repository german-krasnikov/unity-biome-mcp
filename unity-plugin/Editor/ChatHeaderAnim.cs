using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class ChatHeaderAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var root = new VisualElement();
            root.AddToClassList("wave-root");

            var lineL = new VisualElement();
            lineL.usageHints |= UsageHints.DynamicColor;
            lineL.AddToClassList("wave-line");
            var hub   = new VisualElement(); hub.AddToClassList("wave-hub");
            var lineR = new VisualElement();
            lineR.usageHints |= UsageHints.DynamicColor;
            lineR.AddToClassList("wave-line");

            var arcs = new VisualElement[3];
            for (int i = 0; i < 3; i++)
            {
                arcs[i] = new VisualElement();
                arcs[i].usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                arcs[i].AddToClassList("wave-arc");
                arcs[i].AddToClassList("wave-arc-" + (i + 1));
                hub.Add(arcs[i]);
            }

            var dot = new VisualElement();
            dot.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            dot.AddToClassList("wave-dot");
            hub.Add(dot);

            var orbit = new VisualElement();
            orbit.usageHints |= UsageHints.DynamicTransform;
            orbit.AddToClassList("wave-orbit");
            var orbitDot = new VisualElement();
            orbitDot.AddToClassList("wave-orbit-dot");
            orbit.Add(orbitDot);
            hub.Add(orbit);

            root.Add(lineL); root.Add(hub); root.Add(lineR);

            var particles = BiomeAmbientParticles.Attach(
                root,
                BiomeParticlePattern.Chat);
            string previousState = null;

            string ResolveState()
            {
                if (ChatBackendProbe.IsChatBackendRunning())
                    return "up";
                if (!ChatSettingsHook.IsChatBinaryAvailable())
                    return "down";
                return EditorPrefs.GetString(PrefKeys.ChatAuthStatus, "") == "fail"
                    ? "down"
                    : "listen";
            }

            void RefreshState()
            {
                string state = ResolveState();
                if (state != previousState)
                {
                    foreach (var arc in arcs)
                        BiomeUI.SetExclusiveClass(
                            arc,
                            "wave--" + state,
                            "wave--up",
                            "wave--listen",
                            "wave--down");
                    foreach (var line in new[] { lineL, lineR })
                        BiomeUI.SetExclusiveClass(
                            line,
                            "wave--" + state,
                            "wave--up",
                            "wave--listen",
                            "wave--down");
                    BiomeUI.SetExclusiveClass(
                        dot,
                        "wave-dot--" + state,
                        "wave-dot--up",
                        "wave-dot--listen",
                        "wave-dot--down");
                    BiomeUI.SetExclusiveClass(
                        orbit,
                        "conn-" + state,
                        "conn-up",
                        "conn-listen",
                        "conn-down");
                    particles.SetState(state);
                    previousState = state;
                }

                root.tooltip = state == "up"
                    ? "Chat backend is running"
                    : state == "listen"
                        ? "Chat CLI is ready"
                        : "Chat CLI needs attention";
            }

            void Animate(float elapsed)
            {
                for (int i = 0; i < arcs.Length; i++)
                {
                    float wave = 0.5f + 0.5f
                        * Mathf.Sin(elapsed * 3.15f - i * 0.92f);
                    float energy = wave * wave;
                    arcs[i].style.opacity = 0.25f + energy * 0.75f;
                    float arcScale = 0.94f + wave * 0.19f;
                    arcs[i].style.scale = new Scale(new Vector3(
                        arcScale,
                        arcScale,
                        1f));
                }

                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 4.4f);
                float dotScale = 0.88f + pulse * 0.68f;
                dot.style.scale = new Scale(new Vector3(
                    dotScale,
                    dotScale,
                    1f));
                dot.style.opacity = 0.72f + pulse * 0.28f;

                float orbitAngle = Mathf.Sin(elapsed * 0.95f) * 154f
                    + Mathf.Sin(elapsed * 0.37f + 1.2f) * 18f;
                orbit.style.rotate = new Rotate(new Angle(orbitAngle));
                lineL.style.opacity = 0.48f
                    + (0.5f + 0.5f * Mathf.Sin(elapsed * 2.1f)) * 0.28f;
                lineR.style.opacity = 0.48f
                    + (0.5f + 0.5f * Mathf.Sin(elapsed * 2.1f + Mathf.PI)) * 0.28f;
            }

            RefreshState();
            root.schedule.Execute(RefreshState).Every(600);
            ArcadeAnim.SmoothLoop(root, Animate);

            return root;
        }
    }
}
