using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class MCPHubDivider
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var row = new VisualElement();
            row.AddToClassList("hub-divider");

            var lineLeft = new VisualElement();
            lineLeft.usageHints |= UsageHints.DynamicColor;
            lineLeft.AddToClassList("hub-divider-line");

            var spike = new VisualElement();
            spike.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            spike.AddToClassList("hub-divider-spike");
            spike.style.rotate = new StyleRotate(new Rotate(new Angle(45f, AngleUnit.Degree)));

            var lineRight = new VisualElement();
            lineRight.usageHints |= UsageHints.DynamicColor;
            lineRight.AddToClassList("hub-divider-line");

            row.Add(lineLeft);
            row.Add(spike);
            row.Add(lineRight);

            string previousState = null;
            void RefreshState()
            {
                string state;
                if (MCPServer.IsRunning && MCPServer.IsClientConnected)
                    state = "beat-up";
                else if (MCPServer.IsRunning)
                    state = "beat-listen";
                else
                    state = "beat-down";

                if (state != previousState)
                {
                    BiomeUI.SetExclusiveClass(
                        spike,
                        state,
                        "beat-up",
                        "beat-listen",
                        "beat-down");
                    previousState = state;
                }
            }

            RefreshState();
            row.schedule.Execute(RefreshState).Every(600);
            ArcadeAnim.SmoothLoop(row, elapsed =>
            {
                float pulse = 0.5f - 0.5f
                    * Mathf.Cos(elapsed * Mathf.PI * 2f / 1.8f);
                float eased = Mathf.SmoothStep(0f, 1f, pulse);
                float scale = 0.82f + eased * 0.34f;
                spike.style.scale = new Scale(new Vector3(scale, scale, 1f));
                spike.style.opacity = 0.35f + eased * 0.58f;

                lineLeft.style.opacity = 0.42f + eased * 0.22f;
                lineRight.style.opacity = 0.42f
                    + (1f - eased) * 0.22f;
            });

            return row;
        }
    }
}
