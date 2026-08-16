using UnityEngine;

namespace UnityMCP.Editor
{
    public static class MCPStatusBarPalette
    {
        public struct Entry
        {
            public Color Text;
            public Color ChipBg;
            public Color ChipBorder;
            public Color Dot;
            public Color HaloRgb; // alpha = intended presence (Down=0); widget drives actual visibility via style.opacity
        }

        public static Entry Get(MCPStatusModel.State state, bool pro)
        {
            switch (state)
            {
                case MCPStatusModel.State.Up:
                    return pro
                        ? new Entry {
                            Text       = new Color(0.55f, 0.95f, 0.72f, 1f),
                            ChipBg     = new Color(0.13f, 0.30f, 0.22f, 1f),
                            ChipBorder = new Color(0.30f, 0.78f, 0.56f, 0.55f),
                            Dot        = new Color(0.40f, 1.00f, 0.70f, 1f),
                            HaloRgb    = new Color(0.40f, 1.00f, 0.70f, 1f) }
                        : new Entry {
                            Text       = new Color(0.05f, 0.42f, 0.27f, 1f),
                            ChipBg     = new Color(0.85f, 0.96f, 0.90f, 1f),
                            ChipBorder = new Color(0.16f, 0.60f, 0.40f, 0.55f),
                            Dot        = new Color(0.10f, 0.62f, 0.40f, 1f),
                            HaloRgb    = new Color(0.10f, 0.62f, 0.40f, 1f) };

                case MCPStatusModel.State.Listen:
                    return pro
                        ? new Entry {
                            Text       = new Color(0.99f, 0.80f, 0.45f, 1f),
                            ChipBg     = new Color(0.32f, 0.25f, 0.10f, 1f),
                            ChipBorder = new Color(0.93f, 0.66f, 0.24f, 0.55f),
                            Dot        = new Color(1.00f, 0.78f, 0.30f, 1f),
                            HaloRgb    = new Color(1.00f, 0.78f, 0.30f, 1f) }
                        : new Entry {
                            Text       = new Color(0.46f, 0.32f, 0.02f, 1f),
                            ChipBg     = new Color(0.99f, 0.94f, 0.80f, 1f),
                            ChipBorder = new Color(0.72f, 0.52f, 0.08f, 0.55f),
                            Dot        = new Color(0.80f, 0.55f, 0.05f, 1f),
                            HaloRgb    = new Color(0.80f, 0.55f, 0.05f, 1f) };

                case MCPStatusModel.State.ChatActive:
                    return pro
                        ? new Entry {
                            Text       = new Color(0.45f, 0.75f, 1.00f, 1f),
                            ChipBg     = new Color(0.10f, 0.22f, 0.38f, 1f),
                            ChipBorder = new Color(0.30f, 0.60f, 0.95f, 0.55f),
                            Dot        = new Color(0.40f, 0.70f, 1.00f, 1f),
                            HaloRgb    = new Color(0.40f, 0.70f, 1.00f, 1f) }
                        : new Entry {
                            Text       = new Color(0.04f, 0.32f, 0.65f, 1f),
                            ChipBg     = new Color(0.85f, 0.92f, 1.00f, 1f),
                            ChipBorder = new Color(0.15f, 0.50f, 0.90f, 0.55f),
                            Dot        = new Color(0.10f, 0.45f, 0.85f, 1f),
                            HaloRgb    = new Color(0.10f, 0.45f, 0.85f, 1f) };

                default: // Down
                    return pro
                        ? new Entry {
                            Text       = new Color(1.00f, 0.62f, 0.66f, 1f),
                            ChipBg     = new Color(0.32f, 0.14f, 0.16f, 1f),
                            ChipBorder = new Color(0.93f, 0.30f, 0.40f, 0.55f),
                            Dot        = new Color(1.00f, 0.42f, 0.50f, 1f),
                            HaloRgb    = new Color(1.00f, 0.42f, 0.50f, 0f) }
                        : new Entry {
                            Text       = new Color(0.55f, 0.06f, 0.12f, 1f),
                            ChipBg     = new Color(0.99f, 0.88f, 0.89f, 1f),
                            ChipBorder = new Color(0.80f, 0.18f, 0.26f, 0.55f),
                            Dot        = new Color(0.75f, 0.14f, 0.22f, 1f),
                            HaloRgb    = new Color(0.75f, 0.14f, 0.22f, 0f) };
            }
        }

        /// <summary>Amber palette for CompileFailed/BindFailed error sub-states.</summary>
        public static Entry GetError(bool pro) => pro
            ? new Entry {
                Text       = new Color(1.00f, 0.65f, 0.30f, 1f),
                ChipBg     = new Color(0.32f, 0.22f, 0.08f, 1f),
                ChipBorder = new Color(0.90f, 0.55f, 0.20f, 0.55f),
                Dot        = new Color(1.00f, 0.55f, 0.20f, 1f),
                HaloRgb    = new Color(1.00f, 0.55f, 0.20f, 1f) }
            : new Entry {
                Text       = new Color(0.52f, 0.28f, 0.02f, 1f),
                ChipBg     = new Color(0.99f, 0.92f, 0.80f, 1f),
                ChipBorder = new Color(0.70f, 0.42f, 0.08f, 0.55f),
                Dot        = new Color(0.75f, 0.40f, 0.08f, 1f),
                HaloRgb    = new Color(0.75f, 0.40f, 0.08f, 1f) };
    }
}
