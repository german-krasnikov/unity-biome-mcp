// The one door of a chat turn (step 7.3): the reducer biome prints from dsl/chat/chatturn.feature (ChatTurnHost) plus a queue, so that an exit
// runs all its effects before the next intent starts. Main thread only. ZERO UnityEngine deps: it compiles in Tests~/ChatTurn (plain dotnet),
// which is the boundary test of this assembly.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat.Turn
{
    internal sealed class ChatTurnDriver : IChatTurnView, IDisposable
    {
        public event Action<ChatTurnIntent> Intent;          // IChatTurnView: only the host subscribes
        private readonly IChatTurnDecisions _decisions = new ChatTurnLogicDecisions();
        private readonly Action<ChatTurnViewModel> _redraw;
        private readonly Action<Exception> _onError;
        private readonly Queue<ChatTurnIntent> _queue = new Queue<ChatTurnIntent>();
        private readonly IDisposable _host;
        private bool _busy;

        public ChatTurnViewModel View { get; private set; }
        public ChatTurnPhase Phase => View.Phase;
        public bool CanSend => _decisions.CanSend(new ChatTurnCanSendInput(View.Phase));   // the DECISION canSend: no second copy of "idle"
        public bool Asking => View.Asks > 0;

        public ChatTurnDriver(IChatTurnPorts ports, Action<ChatTurnViewModel> redraw, Action<Exception> onError)
        {
            _redraw = redraw;
            _onError = onError ?? throw new ArgumentNullException(nameof(onError));
            _host = ChatTurnFeature.Mount(this, ports, _decisions);   // renders once, idle
        }

        // The host renders BEFORE it runs the effects of the same exit: a redraw that throws must not be able to skip them.
        void IChatTurnView.Render(ChatTurnViewModel viewModel)
        {
            View = viewModel;
            try { _redraw?.Invoke(viewModel); } catch (Exception e) { _onError(e); }
        }

        // A dispatch made while an exit runs (from a redraw, a port, a callback) waits: the exit finishes first, in the order of the protocol.
        public void Dispatch(ChatTurnIntent intent)
        {
            _queue.Enqueue(intent);
            if (_busy) return;
            _busy = true;
            try { while (_queue.Count > 0) { var next = _queue.Dequeue(); Intent?.Invoke(next); } }   // dequeue first: `?.` skips its arguments when nobody listens
            finally { _busy = false; _queue.Clear(); }
        }

        public void Dispose() => _host.Dispose();            // later intents reach nobody: inert
    }
}
