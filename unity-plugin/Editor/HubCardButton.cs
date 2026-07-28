using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class HubCardButton
    {
        public static VisualElement Build(string icon, string title, string subtitle, Action onClick)
        {
            var card = new Button();
            if (onClick != null)
                card.clicked += onClick;
            card.text = string.Empty;
            card.tooltip = $"{title}: {subtitle}";
            card.AddToClassList("hub-card");

            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("hub-card-icon");

            var col = new VisualElement();
            col.AddToClassList("hub-card-col");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("hub-card-title");

            var subLabel = new Label(subtitle);
            subLabel.AddToClassList("hub-card-subtitle");

            col.Add(titleLabel);
            col.Add(subLabel);
            card.Add(iconLabel);
            card.Add(col);

            var chevron = new Label("›");
            chevron.AddToClassList("hub-card-chevron");
            card.Add(chevron);
            return card;
        }
    }
}
