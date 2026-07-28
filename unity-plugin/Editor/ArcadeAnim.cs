using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>Shared arcade animation primitives using USS class toggles.</summary>
    internal static class ArcadeAnim
    {
        internal const int SmoothFrameMs = 16;

        internal sealed class MotionHandle
        {
            private readonly VisualElement _owner;
            private readonly Action<float> _animate;
            private readonly IVisualElementScheduledItem _item;
            private float _epoch;
            private bool _active;

            internal MotionHandle(
                VisualElement owner,
                Action<float> animate,
                int frameMs)
            {
                _owner = owner;
                _animate = animate;
                _item = owner.schedule.Execute(Tick).Every(frameMs);
                _item.Pause();

                owner.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    if (!_active) return;
                    Restart();
                });
                owner.RegisterCallback<DetachFromPanelEvent>(_ => _item.Pause());
            }

            internal bool IsActive => _active;

            internal void SetActive(bool active)
            {
                if (_active == active)
                    return;

                _active = active;
                if (!active)
                {
                    _item.Pause();
                    return;
                }

                _epoch = Time.realtimeSinceStartup;
                _animate(0f);
                if (_owner.panel != null)
                    _item.Resume();
            }

            private void Restart()
            {
                _epoch = Time.realtimeSinceStartup;
                _animate(0f);
                _item.Resume();
            }

            private void Tick() =>
                _animate(Time.realtimeSinceStartup - _epoch);
        }

        /// <summary>
        /// Runs a time-based animation only while <paramref name="owner"/> is attached.
        /// The epoch resets after reattach, avoiding a large catch-up jump.
        /// </summary>
        internal static IVisualElementScheduledItem SmoothLoop(
            VisualElement owner,
            Action<float> animate,
            int frameMs = SmoothFrameMs)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (animate == null) throw new ArgumentNullException(nameof(animate));

            float epoch = Time.realtimeSinceStartup;
            animate(0f);

            var item = owner.schedule.Execute(() =>
                animate(Time.realtimeSinceStartup - epoch)).Every(frameMs);
            item.Pause();

            owner.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                epoch = Time.realtimeSinceStartup;
                animate(0f);
                item.Resume();
            });
            owner.RegisterCallback<DetachFromPanelEvent>(_ => item.Pause());

            if (owner.panel != null)
                item.Resume();
            return item;
        }

        /// <summary>
        /// Creates a loop that remains paused until explicitly activated. It also
        /// pauses on detach and only resumes after reattach when still active.
        /// </summary>
        internal static MotionHandle ControlledSmoothLoop(
            VisualElement owner,
            Action<float> animate,
            int frameMs = SmoothFrameMs)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (animate == null) throw new ArgumentNullException(nameof(animate));
            return new MotionHandle(owner, animate, frameMs);
        }

        // ── Generic Class Toggle ──────────────────────────────────────────────

        /// <summary>Adds <paramref name="hiddenClass"/>, then after <paramref name="delayMs"/> swaps to <paramref name="visibleClass"/>.</summary>
        public static void AnimateClass(VisualElement el, string hiddenClass, string visibleClass, int delayMs = 0)
        {
            el.AddToClassList(hiddenClass);
            el.schedule.Execute(() =>
            {
                el.RemoveFromClassList(hiddenClass);
                el.AddToClassList(visibleClass);
            }).StartingIn(delayMs);
        }

        // ── Fade ─────────────────────────────────────────────────────────────

        public static void FadeIn(VisualElement el, int delayMs = 0) =>
            AnimateClass(el, "arcade-fade-hidden", "arcade-fade-visible", delayMs);

        // ── Slide ─────────────────────────────────────────────────────────────

        public static void SlideInRight(VisualElement el, int delayMs = 0) =>
            AnimateClass(el, "arcade-slide-hidden", "arcade-slide-visible", delayMs);

        // ── Shake ─────────────────────────────────────────────────────────────

        public static void ShakeX(VisualElement el)
        {
            BiomeUI.ShakeX(el);
        }

        // ── Pulse ─────────────────────────────────────────────────────────────

        public static void PulseOnce(VisualElement el)
        {
            el.AddToClassList("arcade-pulse");
            el.schedule.Execute(() => el.RemoveFromClassList("arcade-pulse")).StartingIn(400);
        }

        // ── Flash ─────────────────────────────────────────────────────────────

        public static void FlashClass(VisualElement el, string cls, int ms)
        {
            el.AddToClassList(cls);
            el.schedule.Execute(() => el.RemoveFromClassList(cls)).StartingIn(ms);
        }

        // ── Glow Pulse ────────────────────────────────────────────────────────

        /// <summary>Toggles "arcade-glow" and state class every <paramref name="intervalMs"/> ms.</summary>
        public static void GlowPulse(VisualElement el, string stateKey, int intervalMs = 900)
        {
            el.AddToClassList("arcade-glow");
            el.AddToClassList(StateClassFor(stateKey));

            bool on = true;
            el.schedule.Execute(() =>
            {
                el.EnableInClassList("arcade-glow", on);
                on = !on;
            }).Every(intervalMs);
        }

        // ── Count Up ──────────────────────────────────────────────────────────

        /// <summary>Animates label text from <paramref name="from"/> to <paramref name="to"/> over <paramref name="durationMs"/>.</summary>
        public static void CountUp(Label el, int from, int to, int durationMs = 600)
        {
            el.text = from.ToString();
            int steps = System.Math.Abs(to - from);
            if (steps == 0) return;

            int stepMs = durationMs / steps;
            int current = from;
            el.schedule.Execute(() =>
            {
                current += current < to ? 1 : -1;
                el.text = current.ToString();
            }).Every(stepMs).Until(() => current == to);
        }

        // ── Stagger Fade ──────────────────────────────────────────────────────

        /// <summary>Fades in each element with staggered delay.</summary>
        public static void StaggerFadeIn(IList<VisualElement> els, int stepMs = 80)
        {
            for (int i = 0; i < els.Count; i++)
                FadeIn(els[i], i * stepMs);
        }

        // ── Typewriter ────────────────────────────────────────────────────────

        /// <summary>Reveals <paramref name="text"/> character by character.</summary>
        public static void Typewriter(Label el, string text, int msPerChar = 35)
        {
            el.text = "";
            int idx = 0;
            el.schedule.Execute(() =>
            {
                idx++;
                el.text = text[..idx];
            }).Every(msPerChar).Until(() => idx >= text.Length);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string StateClassFor(string stateKey) => stateKey switch
        {
            "up"     => "conn-up",
            "listen" => "conn-listen",
            _        => "conn-down"
        };
    }
}
