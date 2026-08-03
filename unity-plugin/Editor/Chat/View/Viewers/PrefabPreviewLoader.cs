// Polls AssetPreview.GetAssetPreview via EditorApplication.update until texture arrives or timeout.
// Injectable hooks (subscribe/unsubscribe/getPreview) allow headless unit testing.
using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class PrefabPreviewLoader
    {
        private readonly UnityEngine.Object _asset;
        private readonly Action<Texture2D>  _onReady;
        private readonly Action<Action>     _subscribe;
        private readonly Action<Action>     _unsubscribe;
        private readonly Func<UnityEngine.Object, Texture2D> _getPreview;
        private readonly int  _timeoutFrames;
        private int  _frame;
        private bool _done;

        // Production constructor — uses real EditorApplication.update + AssetPreview.
        internal PrefabPreviewLoader(UnityEngine.Object asset, Action<Texture2D> onReady,
            int timeoutFrames = 180)
        {
            _asset = asset;
            _onReady = onReady;
            _getPreview = a => a != null ? AssetPreview.GetAssetPreview(a) : null;
            _timeoutFrames = timeoutFrames;
            _subscribe = _ => EditorApplication.update += Poll;
            _unsubscribe = _ => EditorApplication.update -= Poll;
            _subscribe(null);
        }

        // Testable constructor — all side-effects injected.
        internal PrefabPreviewLoader(UnityEngine.Object asset, Action<Texture2D> onReady,
            Action<Action> subscribe, Action<Action> unsubscribe,
            Func<UnityEngine.Object, Texture2D> getPreview = null,
            int timeoutFrames = 180)
        {
            _asset         = asset;
            _onReady       = onReady;
            _subscribe     = subscribe;
            _unsubscribe   = unsubscribe;
            _getPreview    = getPreview ?? (a => a != null ? AssetPreview.GetAssetPreview(a) : null);
            _timeoutFrames = timeoutFrames;
            _subscribe(Poll);
        }

        internal void Cancel()
        {
            if (_done) return;
            _done = true;
            _unsubscribe(Poll);
        }

        private void Poll()
        {
            if (_done) return;

            var tex = _getPreview(_asset);
            if (tex != null)
            {
                Finish(tex);
                return;
            }

            // Only advance the timeout counter when Unity has stopped loading.
            // If still loading, the preview is coming — don't count this frame.
            if (_asset != null && AssetPreview.IsLoadingAssetPreviews())
                return;

            if (++_frame >= _timeoutFrames)
                Finish(null);
        }

        private void Finish(Texture2D tex)
        {
            _done = true;
            _unsubscribe(Poll);
            _onReady(tex);
        }
    }
}
