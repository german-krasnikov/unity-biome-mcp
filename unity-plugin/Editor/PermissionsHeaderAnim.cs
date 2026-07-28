using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class PermissionsHeaderAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var root = new VisualElement();
            root.AddToClassList("shield-root");

            var lineL = new VisualElement(); lineL.AddToClassList("shield-line");
            var hub   = new VisualElement(); hub.AddToClassList("shield-hub");
            var lineR = new VisualElement(); lineR.AddToClassList("shield-line");

            var body    = new VisualElement();
            body.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            body.AddToClassList("shield-body");
            var shackle = new VisualElement(); shackle.AddToClassList("lock-shackle");
            var bar     = new VisualElement(); bar.AddToClassList("lock-bar");
            var dot     = new VisualElement();
            dot.usageHints |= UsageHints.DynamicTransform;
            dot.AddToClassList("lock-dot");
            var scan    = new VisualElement();
            scan.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            scan.AddToClassList("shield-scan");

            hub.Add(body); hub.Add(shackle); hub.Add(bar); hub.Add(dot); hub.Add(scan);
            root.Add(lineL); root.Add(hub); root.Add(lineR);

            var particles = BiomeAmbientParticles.Attach(
                root,
                BiomeParticlePattern.Shield);
            string previousState = null;
            var config = new PermissionConfig();

            void Refresh()
            {
                var states = config.GetToolStates();
                int denied = states.FindAll(item => !item.allowed).Count;
                string state = denied > 0 ? "up" : "listen";
                if (state == previousState)
                {
                    root.tooltip = denied == 0
                        ? "All tools are allowed"
                        : $"{denied} tool permissions denied";
                    return;
                }

                foreach (var el in new[] { body, shackle })
                    BiomeUI.SetExclusiveClass(
                        el,
                        "shield--" + state,
                        "shield--up",
                        "shield--listen",
                        "shield--down");
                BiomeUI.SetExclusiveClass(
                    bar,
                    "lock-bar--" + state,
                    "lock-bar--up",
                    "lock-bar--listen",
                    "lock-bar--down");
                shackle.EnableInClassList("lock-shackle--up", denied == 0);
                BiomeUI.SetExclusiveClass(
                    scan,
                    "conn-" + state,
                    "conn-up",
                    "conn-listen",
                    "conn-down");
                particles.SetState(state);
                root.tooltip = denied == 0
                    ? "All tools are allowed"
                    : $"{denied} tool permissions denied";
                previousState = state;
            }

            Refresh();
            root.schedule.Execute(Refresh).Every(900);
            ArcadeAnim.SmoothLoop(root, elapsed =>
            {
                float travel = 0.5f - 0.5f
                    * Mathf.Cos(elapsed * Mathf.PI * 2f / 2.8f);
                scan.style.translate = new Translate(travel * 24f, 0f);
                scan.style.opacity = 0.22f
                    + Mathf.Sin(travel * Mathf.PI) * 0.74f;

                float breath = 0.5f + 0.5f * Mathf.Sin(elapsed * 2.35f);
                float bodyScale = 1f + breath * 0.055f;
                body.style.scale = new Scale(new Vector3(
                    bodyScale,
                    bodyScale,
                    1f));
                body.style.opacity = 0.62f + breath * 0.34f;
                dot.style.scale = new Scale(new Vector3(
                    0.82f + breath * 0.42f,
                    0.82f + breath * 0.42f,
                    1f));
            });

            return root;
        }
    }
}
