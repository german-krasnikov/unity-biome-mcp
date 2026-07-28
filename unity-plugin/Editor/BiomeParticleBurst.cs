using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal enum BiomeParticlePattern
    {
        DataFlow,
        Tools,
        Shield,
        Chat,
        Sampling,
        Updates,
        Ecosystem,
        Timeline
    }

    /// <summary>
    /// Small event-only UI Toolkit particle burst. Particles are pooled VisualElements
    /// and animate through GPU-friendly transforms; there is no permanent update loop.
    /// </summary>
    internal sealed class BiomeParticleBurst : VisualElement
    {
        internal const int ParticleCount = 12;
        private const string ElementName = "biome-particle-burst";

        private readonly VisualElement[] _particles = new VisualElement[ParticleCount];
        private int _generation;

        private BiomeParticleBurst()
        {
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            usageHints |= UsageHints.GroupTransform;
            AddToClassList("biome-particles");

            for (int i = 0; i < ParticleCount; i++)
            {
                var particle = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                particle.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                particle.AddToClassList("biome-particle");
                particle.AddToClassList(i % 3 == 0
                    ? "biome-particle--accent"
                    : i % 3 == 1
                        ? "biome-particle--success"
                        : "biome-particle--warning");
                Add(particle);
                _particles[i] = particle;
            }
        }

        internal static void Emit(VisualElement host)
        {
            if (host == null) return;
            host.AddToClassList("biome-particle-host");
            var burst = host.Q<BiomeParticleBurst>(ElementName);
            if (burst == null)
            {
                burst = new BiomeParticleBurst();
                host.Add(burst);
            }
            burst.Play();
        }

        private void Play()
        {
            int generation = ++_generation;
            for (int i = 0; i < _particles.Length; i++)
            {
                var particle = _particles[i];
                float angle = (360f / ParticleCount) * i - 90f;
                float radius = 30f + (i % 4) * 7f;
                float radians = angle * Mathf.Deg2Rad;
                float x = Mathf.Cos(radians) * radius;
                float y = Mathf.Sin(radians) * radius;
                int delay = i * 10;

                particle.AddToClassList("biome-particle--reset");
                particle.style.translate = new Translate(0f, 0f);
                particle.style.rotate = new Rotate(new Angle(0f));
                particle.style.scale = new Scale(new Vector3(0.65f, 0.65f, 1f));
                particle.style.opacity = 1f;

                particle.schedule.Execute(() =>
                {
                    if (generation != _generation) return;
                    particle.RemoveFromClassList("biome-particle--reset");
                    particle.style.translate = new Translate(x, y);
                    particle.style.rotate = new Rotate(new Angle(angle + 135f));
                    particle.style.scale = new Scale(new Vector3(0.25f, 0.25f, 1f));
                    particle.style.opacity = 0f;
                }).StartingIn(delay + 16);
            }
        }
    }

    /// <summary>
    /// Pooled ambient particles for an active settings header. Each particle has
    /// an independent harmonic profile, while the animation loop pauses on detach.
    /// </summary>
    internal sealed class BiomeAmbientParticles : VisualElement
    {
        internal const int ParticleCount = 9;
        private const string ElementName = "biome-ambient-particles";

        private readonly VisualElement[] _particles = new VisualElement[ParticleCount];
        private readonly MotionProfile[] _motion = new MotionProfile[ParticleCount];
        private readonly BiomeParticlePattern _pattern;
        private bool _entryBurstRegistered;

        private readonly struct MotionProfile
        {
            internal readonly float PhaseA;
            internal readonly float PhaseB;
            internal readonly float PhaseC;
            internal readonly float SpeedA;
            internal readonly float SpeedB;
            internal readonly float AmplitudeX;
            internal readonly float AmplitudeY;
            internal readonly float Spin;

            internal MotionProfile(int seed)
            {
                PhaseA = Unit(seed + 1) * Mathf.PI * 2f;
                PhaseB = Unit(seed + 2) * Mathf.PI * 2f;
                PhaseC = Unit(seed + 3) * Mathf.PI * 2f;
                SpeedA = 0.38f + Unit(seed + 4) * 0.72f;
                SpeedB = 0.72f + Unit(seed + 5) * 0.93f;
                AmplitudeX = 4f + Unit(seed + 6) * 11f;
                AmplitudeY = 3f + Unit(seed + 7) * 9f;
                Spin = (Unit(seed + 8) > 0.5f ? 1f : -1f)
                    * (18f + Unit(seed + 9) * 46f);
            }
        }

        private BiomeAmbientParticles(BiomeParticlePattern pattern)
        {
            _pattern = pattern;
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            usageHints |= UsageHints.GroupTransform;
            AddToClassList("biome-ambient-particles");
            AddToClassList(PatternClass(pattern));

            for (int i = 0; i < ParticleCount; i++)
            {
                var particle = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                particle.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                particle.AddToClassList("biome-ambient-particle");
                particle.AddToClassList(i % 3 == 0
                    ? "biome-ambient-particle--hot"
                    : i % 2 == 0
                        ? "biome-ambient-particle--secondary"
                        : "biome-ambient-particle--primary");
                particle.style.left = Length.Percent(7f + i * 10.75f);
                particle.style.top = Length.Percent(24f + (i * 17f) % 54f);
                Add(particle);
                _particles[i] = particle;
                _motion[i] = new MotionProfile((int)pattern * 101 + i * 17);
            }

            ArcadeAnim.SmoothLoop(this, Tick);
        }

        internal static BiomeAmbientParticles Attach(
            VisualElement host,
            BiomeParticlePattern pattern,
            bool entryBurst = true)
        {
            if (host == null) return null;
            host.AddToClassList("biome-ambient-host");

            var field = host.Q<BiomeAmbientParticles>(ElementName);
            if (field == null)
            {
                field = new BiomeAmbientParticles(pattern);
                host.Add(field);
            }

            if (entryBurst && !field._entryBurstRegistered)
            {
                field._entryBurstRegistered = true;
                ScheduleEntryBurst(host, field);
            }
            return field;
        }

        internal void SetState(string state)
        {
            string active = state.StartsWith("conn-") ? state : "conn-" + state;
            BiomeUI.SetExclusiveClass(
                this,
                active,
                "conn-up",
                "conn-listen",
                "conn-down");
        }

        private static void ScheduleEntryBurst(
            VisualElement host,
            BiomeAmbientParticles field)
        {
            int lifecycle = 0;
            host.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                int generation = ++lifecycle;
                field.schedule.Execute(() =>
                {
                    if (generation == lifecycle && host.panel != null)
                        BiomeParticleBurst.Emit(host);
                }).StartingIn(220);
            });
            host.RegisterCallback<DetachFromPanelEvent>(_ => lifecycle++);
        }

        private void Tick(float elapsed)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                var motion = _motion[i];
                float x;
                float y;
                float scale;
                float opacity;
                ResolveMotion(
                    i,
                    elapsed,
                    motion,
                    out x,
                    out y,
                    out scale,
                    out opacity);

                var particle = _particles[i];
                particle.style.translate = new Translate(x, y);
                particle.style.scale = new Scale(new Vector3(scale, scale, 1f));
                particle.style.rotate = new Rotate(
                    new Angle(elapsed * motion.Spin + motion.PhaseC * Mathf.Rad2Deg));
                particle.style.opacity = opacity;
            }
        }

        private void ResolveMotion(
            int index,
            float elapsed,
            MotionProfile motion,
            out float x,
            out float y,
            out float scale,
            out float opacity)
        {
            float a = elapsed * motion.SpeedA + motion.PhaseA;
            float b = elapsed * motion.SpeedB + motion.PhaseB;
            float c = elapsed * (motion.SpeedA + motion.SpeedB) * 0.37f
                + motion.PhaseC;

            // Incommensurate harmonics keep paths smooth without looking looped.
            x = Mathf.Sin(a) * motion.AmplitudeX
                + Mathf.Cos(b * 0.73f) * motion.AmplitudeX * 0.38f;
            y = Mathf.Cos(b) * motion.AmplitudeY
                + Mathf.Sin(c * 1.31f) * motion.AmplitudeY * 0.42f;
            scale = 0.72f + (0.5f + 0.5f * Mathf.Sin(c * 1.7f)) * 0.52f;
            opacity = 0.30f + (0.5f + 0.5f * Mathf.Sin(b * 1.23f)) * 0.55f;

            switch (_pattern)
            {
                case BiomeParticlePattern.DataFlow:
                    x += Mathf.Sin(elapsed * 0.72f + motion.PhaseC)
                        * (8f + index * 0.8f);
                    y *= 0.45f;
                    break;
                case BiomeParticlePattern.Tools:
                    x *= 0.55f;
                    y += Mathf.Sin(elapsed * (1.4f + index * 0.07f) + motion.PhaseC)
                        * 5f;
                    scale += (0.5f + 0.5f * Mathf.Sin(a * 2.1f)) * 0.20f;
                    break;
                case BiomeParticlePattern.Shield:
                {
                    float angle = elapsed * (0.65f + index * 0.035f) + motion.PhaseA;
                    float radius = 4f + index * 1.45f
                        + Mathf.Sin(b) * 2.5f;
                    x += Mathf.Cos(angle) * radius;
                    y += Mathf.Sin(angle) * radius;
                    break;
                }
                case BiomeParticlePattern.Chat:
                {
                    float angle = elapsed * (0.8f + index * 0.06f) + motion.PhaseB;
                    float radius = 4f + (index % 4) * 3f + Mathf.Sin(c) * 3f;
                    x += Mathf.Cos(angle) * radius;
                    y += Mathf.Sin(angle) * radius;
                    break;
                }
                case BiomeParticlePattern.Sampling:
                    x *= 0.48f;
                    y += Mathf.Sin(elapsed * (1.2f + index * 0.08f) + motion.PhaseA)
                        * (4f + index * 0.7f);
                    break;
                case BiomeParticlePattern.Updates:
                    x *= 0.42f;
                    y += Mathf.Sin(elapsed * 0.68f + motion.PhaseC)
                        * (7f + index);
                    break;
                case BiomeParticlePattern.Ecosystem:
                    x *= 1.08f;
                    y += Mathf.Sin(elapsed * 0.44f + motion.PhaseA)
                        * (3f + index * 0.45f);
                    break;
                case BiomeParticlePattern.Timeline:
                    x += Mathf.Sin(elapsed * 0.58f + motion.PhaseC)
                        * (6f + index);
                    y *= 0.38f;
                    break;
            }
        }

        private static float Unit(int seed)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= value >> 16;
                value *= 0x7feb352d;
                value ^= value >> 15;
                value *= 0x846ca68b;
                value ^= value >> 16;
                return (value & 0x00ffffff) / 16777215f;
            }
        }

        private static string PatternClass(BiomeParticlePattern pattern) =>
            pattern switch
            {
                BiomeParticlePattern.DataFlow => "biome-ambient--data",
                BiomeParticlePattern.Tools => "biome-ambient--tools",
                BiomeParticlePattern.Shield => "biome-ambient--shield",
                BiomeParticlePattern.Chat => "biome-ambient--chat",
                BiomeParticlePattern.Sampling => "biome-ambient--sampling",
                BiomeParticlePattern.Updates => "biome-ambient--updates",
                BiomeParticlePattern.Ecosystem => "biome-ambient--ecosystem",
                _ => "biome-ambient--timeline"
            };
    }
}
