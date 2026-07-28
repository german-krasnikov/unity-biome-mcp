using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class HubHeaderAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var root = new VisualElement();
            root.AddToClassList("hub-anim-root");

            var nodeL1 = MakeNode("han-node--sm"); var lineL1 = MakeLine();
            var nodeL2 = MakeNode("han-node--md"); var lineL2 = MakeLine();
            var hub    = MakeHub(out var statusLabel);
            var lineR1 = MakeLine();               var nodeR1 = MakeNode("han-node--md");
            var lineR2 = MakeLine();               var nodeR2 = MakeNode("han-node--sm");

            root.Add(nodeL1); root.Add(lineL1);
            root.Add(nodeL2); root.Add(lineL2);
            root.Add(hub);
            root.Add(lineR1); root.Add(nodeR1);
            root.Add(lineR2); root.Add(nodeR2);

            var packet = new VisualElement();
            packet.AddToClassList("han-packet");
            packet.usageHints |= UsageHints.DynamicTransform;
            root.Add(packet);

            var stateEls = new VisualElement[]
                { nodeL1, nodeL2, lineL1, lineL2, hub, lineR1, lineR2, nodeR1, nodeR2, packet, statusLabel };
            var routeNodes = new[] { nodeL1, nodeL2, hub, nodeR1, nodeR2 };
            var particles = BiomeAmbientParticles.Attach(
                root,
                BiomeParticlePattern.DataFlow);

            int previousHotNode = -1;
            string previousKey = null;

            void RefreshState()
            {
                bool run  = MCPServer.IsRunning;
                bool cli  = MCPServer.IsClientConnected;
                bool chat = ChatBackendProbe.IsChatBackendRunning();
                var  state = MCPStatusModel.GetState(run, cli, chat);
                string key = MCPStatusModel.GetCssKey(state);

                if (key != previousKey)
                {
                    foreach (var el in stateEls)
                        BiomeUI.SetExclusiveClass(
                            el,
                            "han--" + key,
                            "han--up",
                            "han--listen",
                            "han--down");
                    particles.SetState(key);
                    previousKey = key;
                }

                statusLabel.text = MCPStatusModel.GetLabel(state, MCPServer.ServerPort);
            }

            RefreshState();
            root.schedule.Execute(RefreshState).Every(600);

            ArcadeAnim.SmoothLoop(root, elapsed =>
            {
                float travel = 0.5f - 0.5f
                    * Mathf.Cos(elapsed * Mathf.PI * 2f / 3.6f);
                float rootWidth = root.resolvedStyle.width;
                float packetWidth = packet.resolvedStyle.width;
                float trackWidth = float.IsNaN(rootWidth) || float.IsNaN(packetWidth)
                    ? 0f
                    : System.Math.Max(0f, rootWidth - packetWidth);
                packet.style.translate = new Translate(trackWidth * travel, 0f);
                float packetPulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 8.4f);
                packet.style.scale = new Scale(new Vector3(
                    0.82f + packetPulse * 0.34f,
                    0.82f + packetPulse * 0.34f,
                    1f));
                packet.style.opacity = 0.58f + packetPulse * 0.42f;

                float routePosition = travel * (routeNodes.Length - 1);
                int hotNode = Mathf.RoundToInt(routePosition);
                for (int i = 0; i < routeNodes.Length; i++)
                {
                    float influence = Mathf.Clamp01(1f - Mathf.Abs(i - routePosition));
                    routeNodes[i].style.scale = new Scale(new Vector3(
                        1f + influence * 0.30f,
                        1f + influence * 0.30f,
                        1f));
                }

                if (hotNode != previousHotNode)
                {
                    for (int i = 0; i < routeNodes.Length; i++)
                        routeNodes[i].EnableInClassList("han-node--hot", i == hotNode);
                    previousHotNode = hotNode;
                }
                float hubInfluence = Mathf.Clamp01(1f - Mathf.Abs(2f - routePosition));
                hub.style.scale = new Scale(new Vector3(
                    1f + hubInfluence * 0.06f,
                    1f + hubInfluence * 0.06f,
                    1f));
                hub.EnableInClassList("han-hub--pulse", hubInfluence > 0.55f);
            });

            return root;
        }

        private static VisualElement MakeNode(string sizeClass)
        {
            var n = new VisualElement();
            n.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            n.AddToClassList("han-node");
            n.AddToClassList(sizeClass);
            return n;
        }

        private static VisualElement MakeLine()
        {
            var l = new VisualElement();
            l.AddToClassList("han-line");
            return l;
        }

        private static VisualElement MakeHub(out Label statusLabel)
        {
            var hub = new VisualElement();
            hub.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            hub.AddToClassList("han-hub");
            statusLabel = new Label();
            statusLabel.AddToClassList("han-status");
            hub.Add(statusLabel);
            return hub;
        }
    }
}
