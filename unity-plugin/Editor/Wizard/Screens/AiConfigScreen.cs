using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>Data-driven AI tool config cards for all 8 backends.</summary>
    public sealed class AiConfigScreen : IWizardScreen
    {
        private readonly Action _onDone;
        private readonly Action _onBack;
        private VisualElement[] _cards;

        public string Title => "AI Tools";

        public AiConfigScreen(Action onDone, Action onBack)
        {
            _onDone = onDone;
            _onBack = onBack;
        }

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("wiz-container");

            var title = new Label("Configure AI Tools");
            title.AddToClassList("wiz-title");
            root.Add(title);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("wiz-scroll");

            int port     = MCPServer.IsRunning ? MCPServer.ServerPort : 9500;
            var allCards = AiToolCardFactory.Build(port);
            var external = allCards.Where(c => c.Action != CardAction.CopyPort).ToArray();
            var chat     = allCards.Where(c => c.Action == CardAction.CopyPort).ToArray();

            var cardElements = new System.Collections.Generic.List<VisualElement>();

            scroll.Add(MakeGroupLabel("External MCP Hosts"));
            foreach (var card in external) { var el = BuildCard(card, port); cardElements.Add(el); scroll.Add(el); }

            scroll.Add(MakeGroupLabel("In-Unity Chat Backends"));
            foreach (var card in chat) { var el = BuildCard(card, port); cardElements.Add(el); scroll.Add(el); }

            _cards = cardElements.ToArray();
            root.Add(scroll);

            root.Add(WizardUI.Navigation(
                WizardUI.Secondary("← Back", _onBack),
                WizardUI.Primary("Done ✓", _onDone)));
            return root;
        }

        public void OnEnter()
        {
            if (_cards == null) return;
            for (int i = 0; i < _cards.Length; i++)
                WizardAnimUtils.SlideInRight(_cards[i], i * 100);
        }

        public void OnExit() { }

        // ── Private ───────────────────────────────────────────────────────────

        private static Label MakeGroupLabel(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("wiz-group-label");
            return lbl;
        }

        private static VisualElement BuildCard(BackendCard data, int port)
        {
            var card = new VisualElement();
            card.AddToClassList("wiz-card");

            var heading = new Label(data.Name);
            heading.AddToClassList("wiz-card-title");

            var body = new Label(data.Body);
            body.AddToClassList("wiz-card-description");

            Button btn = null;
            btn = WizardUI.Secondary(data.BtnLabel, () =>
            {
                Dispatch(data, port);
                WizardAnimUtils.FlashClass(btn, "wiz-btn-copied", 800);
            });

            card.Add(heading);
            card.Add(body);

            if (data.Action == CardAction.WriteConfig)
            {
                var snippet = new TextField
                {
                    value      = WizardConfigWriter.Fresh(port),
                    isReadOnly = true,
                    multiline  = true,
                };
                snippet.AddToClassList("wiz-snippet");
                card.Add(snippet);
            }

            card.Add(btn);

            if (data.Action == CardAction.WriteConfig && WizardConfigWriter.HasBackup(data.Payload))
            {
                var restoreBtn = WizardUI.Secondary("Restore", () =>
                {
                    WizardConfigWriter.RestoreConfig(data.Payload);
                    WizardAnimUtils.FlashClass(btn, "wiz-btn-copied", 800);
                });
                restoreBtn.AddToClassList("wiz-btn-restore");
                card.Add(restoreBtn);
            }

            return card;
        }

        private static void Dispatch(BackendCard card, int port)
        {
            switch (card.Action)
            {
                case CardAction.CopyText:
                case CardAction.CopyPort:
                    GUIUtility.systemCopyBuffer = card.Payload;
                    break;
                case CardAction.WriteConfig:
                    WizardConfigWriter.Write(card.Name, card.Payload, port);
                    break;
            }
        }
    }
}
