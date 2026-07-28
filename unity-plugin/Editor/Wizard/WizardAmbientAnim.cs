using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>A persistent ecosystem route shared by every setup step.</summary>
    internal sealed class WizardJourneyAnim : VisualElement
    {
        internal const int NodeCount = 4;

        private readonly VisualElement[] _nodes = new VisualElement[NodeCount];
        private readonly VisualElement _packet;
        private readonly VisualElement _aura;
        private readonly BiomeAmbientParticles _particles;
        private int _currentStep;

        internal WizardJourneyAnim()
        {
            pickingMode = PickingMode.Ignore;
            usageHints |= UsageHints.GroupTransform;
            AddToClassList("wiz-journey");

            _particles = BiomeAmbientParticles.Attach(
                this,
                BiomeParticlePattern.Ecosystem);
            _particles?.SetState("up");

            var route = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            route.AddToClassList("wiz-journey__route");

            for (int i = 0; i < NodeCount; i++)
            {
                if (i > 0)
                {
                    var segment = new VisualElement
                    {
                        pickingMode = PickingMode.Ignore
                    };
                    segment.AddToClassList("wiz-journey__segment");
                    route.Add(segment);
                }

                var node = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    tooltip = $"Setup step {i + 1}"
                };
                node.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                node.AddToClassList("wiz-journey__node");

                var core = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                core.AddToClassList("wiz-journey__node-core");
                node.Add(core);
                route.Add(node);
                _nodes[i] = node;
            }
            Add(route);

            _aura = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _aura.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            _aura.AddToClassList("wiz-journey__aura");
            Add(_aura);

            _packet = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _packet.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            _packet.AddToClassList("wiz-journey__packet");
            Add(_packet);

            SetStep(0, NodeCount);
            ArcadeAnim.SmoothLoop(this, Tick);
        }

        internal void SetStep(int step, int screenCount)
        {
            int max = Mathf.Max(1, screenCount - 1);
            float normalized = Mathf.Clamp01((float)step / max);
            _currentStep = Mathf.RoundToInt(normalized * (NodeCount - 1));

            for (int i = 0; i < _nodes.Length; i++)
            {
                _nodes[i].EnableInClassList("wiz-journey__node--complete", i < _currentStep);
                _nodes[i].EnableInClassList("wiz-journey__node--active", i == _currentStep);
                _nodes[i].EnableInClassList("wiz-journey__node--pending", i > _currentStep);
            }
        }

        private void Tick(float elapsed)
        {
            float width = resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f)
                width = 1f;

            float current = (float)_currentStep / (NodeCount - 1);
            float drift = Mathf.Sin(elapsed * 0.83f) * 0.014f
                + Mathf.Sin(elapsed * 1.71f + 0.7f) * 0.006f;
            float position = Mathf.Clamp01(current + drift);
            float travel = Mathf.Max(0f, width - 28f);
            float x = 14f + travel * position;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 3.4f);
            float orbit = Mathf.Sin(elapsed * 1.37f) * 2.5f;

            _packet.style.translate = new Translate(x, orbit);
            float packetScale = 0.84f + pulse * 0.32f;
            _packet.style.scale = new Scale(new Vector3(
                packetScale,
                packetScale,
                1f));
            _packet.style.opacity = 0.72f + pulse * 0.28f;

            _aura.style.translate = new Translate(x, -1f - orbit * 0.35f);
            float auraScale = 0.86f + pulse * 0.42f;
            _aura.style.scale = new Scale(new Vector3(
                auraScale,
                auraScale,
                1f));
            _aura.style.opacity = 0.08f + pulse * 0.24f;

            for (int i = 0; i < _nodes.Length; i++)
            {
                float nodeWave = 0.5f + 0.5f
                    * Mathf.Sin(elapsed * (1.45f + i * 0.09f) + i * 1.31f);
                float emphasis = i == _currentStep ? 0.18f : i < _currentStep ? 0.07f : 0.03f;
                float scale = 0.96f + nodeWave * emphasis;
                _nodes[i].style.scale = new Scale(new Vector3(scale, scale, 1f));
            }
        }
    }

    /// <summary>Living module stream for the standalone and wizard skills installer.</summary>
    internal sealed class SkillsInstallAnim : VisualElement
    {
        private readonly VisualElement[] _modules = new VisualElement[3];
        private readonly VisualElement _packet;
        private readonly VisualElement _aura;
        private readonly BiomeAmbientParticles _particles;
        private bool _working;

        internal SkillsInstallAnim()
        {
            pickingMode = PickingMode.Ignore;
            usageHints |= UsageHints.GroupTransform;
            AddToClassList("wiz-skills-anim");

            _particles = BiomeAmbientParticles.Attach(
                this,
                BiomeParticlePattern.Tools);
            _particles?.SetState("up");

            var route = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            route.AddToClassList("wiz-skills-anim__route");

            string[] glyphs = { "S", "A", "<>" };
            string[] tooltips = { "Skills", "Agents", "Scripts" };
            for (int i = 0; i < _modules.Length; i++)
            {
                if (i > 0)
                {
                    var segment = new VisualElement
                    {
                        pickingMode = PickingMode.Ignore
                    };
                    segment.AddToClassList("wiz-skills-anim__segment");
                    route.Add(segment);
                }

                var module = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    tooltip = tooltips[i]
                };
                module.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                module.AddToClassList("wiz-skills-anim__module");
                var glyph = new Label(glyphs[i])
                {
                    pickingMode = PickingMode.Ignore
                };
                glyph.AddToClassList("wiz-skills-anim__glyph");
                module.Add(glyph);
                route.Add(module);
                _modules[i] = module;
            }
            Add(route);

            _aura = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _aura.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            _aura.AddToClassList("wiz-skills-anim__aura");
            Add(_aura);

            _packet = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _packet.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            _packet.AddToClassList("wiz-skills-anim__packet");
            Add(_packet);

            ArcadeAnim.SmoothLoop(this, Tick);
        }

        internal void SetWorking(bool working)
        {
            _working = working;
            EnableInClassList("wiz-skills-anim--working", working);
        }

        private void Tick(float elapsed)
        {
            float width = resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f)
                width = 1f;

            float speed = _working ? 1.62f : 0.78f;
            float yoyo = 0.5f - 0.5f * Mathf.Cos(elapsed * Mathf.PI * speed);
            float travel = Mathf.Max(0f, width - 30f);
            float x = 15f + travel * yoyo;
            float pulse = 0.5f + 0.5f
                * Mathf.Sin(elapsed * (_working ? 5.2f : 2.6f));
            float floatY = Mathf.Sin(elapsed * 1.83f + yoyo * 4f) * 3f;

            _packet.style.translate = new Translate(x, floatY);
            float packetScale = 0.78f + pulse * (_working ? 0.46f : 0.28f);
            _packet.style.scale = new Scale(new Vector3(
                packetScale,
                packetScale,
                1f));
            _packet.style.opacity = 0.72f + pulse * 0.28f;

            _aura.style.translate = new Translate(x, floatY * 0.35f);
            float auraScale = 0.92f + pulse * 0.44f;
            _aura.style.scale = new Scale(new Vector3(
                auraScale,
                auraScale,
                1f));
            _aura.style.opacity = 0.08f + pulse * (_working ? 0.30f : 0.18f);

            for (int i = 0; i < _modules.Length; i++)
            {
                float modulePosition = i / 2f;
                float proximity = 1f - Mathf.Clamp01(Mathf.Abs(modulePosition - yoyo) * 2.4f);
                float life = 0.5f + 0.5f
                    * Mathf.Sin(elapsed * (1.3f + i * 0.17f) + i * 1.8f);
                float scale = 0.96f + life * 0.05f + proximity * 0.16f;
                _modules[i].style.scale = new Scale(new Vector3(scale, scale, 1f));
                _modules[i].style.opacity = 0.72f + proximity * 0.28f;
            }
        }
    }
}
