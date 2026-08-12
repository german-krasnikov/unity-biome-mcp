// T2.5: Abstract base for typed tool card renderers.
//
// PROTECTION CONTRACT (structurally enforced):
//   • _renderedClass is private — subclass cannot access it, so cannot call
//     chip.AddToClassList(_renderedClass) by accident.
//   • OnUpdate is not virtual — subclass cannot override the marker-setting sequence.
//   • Marker is set AFTER TryBuildContent returns true AND without an exception.
//   • If TryBuildContent throws, the catch swallows it and the marker is NOT set,
//     so the next OnUpdate call can retry.
//
// SECONDARY RENDER HOOK (OnAdditionalRender):
//   Called on every OnUpdate once the primary marker is set.
//   Use RunSecondaryPass for secondary idempotency guards — it enforces marker-last
//   so a subclass cannot accidentally set a secondary marker before content is added.
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal abstract class ToolCardBase : IToolCardRenderer
    {
        private readonly string _renderedClass;

        protected ToolCardBase(string renderedClass) => _renderedClass = renderedClass;

        public void OnStart(VisualElement chip, ToolCallRecord rec) { }

        public void OnUpdate(VisualElement chip, ToolCallRecord rec)
        {
            if (!chip.ClassListContains(_renderedClass))
            {
                try
                {
                    if (TryBuildContent(chip, rec))
                        chip.AddToClassList(_renderedClass); // ALWAYS last; base owns this line
                }
                catch (System.IO.IOException) { } // expected I/O race (file gone) — silent, retry
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ToolCardBase] {_renderedClass}: TryBuildContent threw " +
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }

            if (chip.ClassListContains(_renderedClass))
                OnAdditionalRender(chip, rec);
        }

        /// <summary>
        /// Build primary content.
        /// Return true  = built successfully → base sets the rendered marker.
        /// Return false = not ready yet     → no marker, called again next OnUpdate.
        /// Throw        = build failed      → no marker, called again next OnUpdate (retry).
        /// Never call chip.AddToClassList(renderedClass) — the base owns the marker.
        /// </summary>
        protected abstract bool TryBuildContent(VisualElement chip, ToolCallRecord rec);

        /// <summary>
        /// Called on every OnUpdate after the primary marker is set.
        /// Override for multi-pass rendering (e.g. BashCard output fill).
        /// Use <see cref="RunSecondaryPass"/> for idempotency guards — it enforces marker-last.
        /// </summary>
        protected virtual void OnAdditionalRender(VisualElement chip, ToolCallRecord rec) { }

        /// <summary>
        /// Enforce marker-last and retry semantics for a secondary pass.
        /// Call inside <see cref="OnAdditionalRender"/>.
        /// <para>
        /// Guard is checked first. If not set, <paramref name="build"/> is called inside a
        /// try/catch. On success (<c>true</c>), marker is set LAST. On failure (exception or
        /// <c>false</c>), marker is not set — next OnUpdate call can retry.
        /// </para>
        /// </summary>
        /// <param name="target">Element whose class list owns the guard (may differ from chip).</param>
        /// <param name="markerClass">CSS class used as the idempotency guard.</param>
        /// <param name="build">Returns true when content was successfully added.</param>
        protected static void RunSecondaryPass(VisualElement target, string markerClass, Func<bool> build)
        {
            if (target.ClassListContains(markerClass)) return;
            try { if (build()) target.AddToClassList(markerClass); }
            catch { } // no marker on failure → retry on next OnUpdate
        }
    }
}
