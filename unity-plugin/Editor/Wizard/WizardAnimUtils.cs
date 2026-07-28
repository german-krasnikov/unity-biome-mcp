using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Shared animation primitives using USS class toggles.</summary>
    public static class WizardAnimUtils
    {
        // ── Fade ─────────────────────────────────────────────────────────────

        public static void FadeIn(VisualElement el, int delayMs = 0) =>
            ArcadeAnim.AnimateClass(el, "wiz-fade-hidden", "wiz-fade-visible", delayMs);

        // ── Slide ─────────────────────────────────────────────────────────────

        public static void SlideInRight(VisualElement el, int delayMs = 0) =>
            ArcadeAnim.AnimateClass(el, "wiz-slide-hidden", "wiz-slide-visible", delayMs);

        // ── Shake ─────────────────────────────────────────────────────────────

        /// <summary>Horizontal shake for error feedback.</summary>
        public static void ShakeX(VisualElement el)
        {
            BiomeUI.ShakeX(el);
        }

        // ── Pulse ─────────────────────────────────────────────────────────────

        /// <summary>Single pulse using border-width expansion (proven Unity 6 pattern).</summary>
        public static void PulseOnce(VisualElement el)
        {
            el.AddToClassList("wiz-pulse");
            el.schedule.Execute(() => el.RemoveFromClassList("wiz-pulse")).StartingIn(400);
        }

        // ── Flash ─────────────────────────────────────────────────────────────

        /// <summary>Adds a USS class for <paramref name="ms"/> milliseconds, then removes it.</summary>
        public static void FlashClass(VisualElement el, string cls, int ms) =>
            ArcadeAnim.FlashClass(el, cls, ms);
    }
}
