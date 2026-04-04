using UnityEngine;
using Verse;

namespace CorvusSurgeryUI
{
    [StaticConstructorOnStartup]
    public static class CorvusStyle
    {
        public static readonly Color Background = new Color(0.07f, 0.08f, 0.09f, 1f);
        public static readonly Color BackgroundAlt = new Color(0.10f, 0.11f, 0.13f, 1f);
        public static readonly Color Surface = new Color(0.12f, 0.14f, 0.16f, 0.98f);
        public static readonly Color SurfaceAlt = new Color(0.09f, 0.11f, 0.13f, 0.98f);
        public static readonly Color Inset = new Color(0.05f, 0.06f, 0.08f, 1f);
        public static readonly Color Border = new Color(0.22f, 0.28f, 0.32f, 1f);
        public static readonly Color BorderBright = new Color(0.28f, 0.40f, 0.48f, 1f);
        public static readonly Color Accent = new Color(0.11f, 0.64f, 0.86f, 1f);
        public static readonly Color AccentMuted = new Color(0.11f, 0.64f, 0.86f, 0.35f);
        public static readonly Color TextPrimary = new Color(0.84f, 0.87f, 0.90f, 1f);
        public static readonly Color TextSecondary = new Color(0.56f, 0.62f, 0.67f, 1f);
        public static readonly Color TextMuted = new Color(0.43f, 0.48f, 0.53f, 1f);
        public static readonly Color Warning = new Color(0.81f, 0.62f, 0.24f, 1f);
        public static readonly Color Danger = new Color(0.72f, 0.28f, 0.26f, 1f);
        public static readonly Color Success = new Color(0.20f, 0.72f, 0.51f, 1f);

        private static Texture2D solidTexture;
        private static Texture2D scrollbarTrackTexture;
        private static Texture2D scrollbarThumbTexture;
        private static Texture2D scrollbarThumbHoverTexture;
        private static GUIStyle labelTiny;
        private static GUIStyle labelSmall;
        private static GUIStyle labelHeader;
        private static GUIStyle labelTitle;
        private static GUIStyle labelMono;
        private static GUIStyle corvusVerticalScrollbar;
        private static GUIStyle corvusVerticalScrollbarThumb;

        private static Texture2D SolidTexture
        {
            get
            {
                if (solidTexture == null)
                {
                    solidTexture = new Texture2D(1, 1);
                    solidTexture.SetPixel(0, 0, Color.white);
                    solidTexture.Apply();
                }

                return solidTexture;
            }
        }

        private static void EnsureStyles()
        {
            if (labelTiny != null)
            {
                return;
            }

            labelTiny = new GUIStyle(Text.CurFontStyle)
            {
                fontSize = 10,
                wordWrap = false
            };
            labelTiny.normal.textColor = TextSecondary;

            labelSmall = new GUIStyle(Text.CurFontStyle)
            {
                fontSize = 12,
                wordWrap = false
            };
            labelSmall.normal.textColor = TextPrimary;

            labelHeader = new GUIStyle(Text.CurFontStyle)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            labelHeader.normal.textColor = TextPrimary;

            labelTitle = new GUIStyle(Text.CurFontStyle)
            {
                fontSize = 18,
                wordWrap = false
            };
            labelTitle.normal.textColor = TextPrimary;

            labelMono = new GUIStyle(Text.CurFontStyle)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            labelMono.normal.textColor = TextPrimary;
        }

        public static void DrawWindowBackground(Rect rect)
        {
            DrawRect(rect, Background);
            DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), BackgroundAlt);

            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), BorderBright);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Danger * 0.65f);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), Border);
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Border);
        }

        public static void DrawWindowHeader(Rect rect, string title, string utilityText)
        {
            EnsureStyles();
            DrawRect(rect, SurfaceAlt);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BorderBright);

            Label(new Rect(rect.x + 12f, rect.y, rect.width - 160f, rect.height), title, labelTitle, TextPrimary, TextAnchor.MiddleLeft);

            if (!utilityText.NullOrEmpty())
            {
                Rect chipRect = new Rect(rect.xMax - 108f, rect.y + 5f, 72f, rect.height - 10f);
                DrawChip(chipRect, utilityText, true);
            }
        }

        public static bool DrawHeaderCloseButton(Rect rect)
        {
            EnsureStyles();
            bool hover = Mouse.IsOver(rect);
            DrawRect(rect, hover ? SurfaceAlt : Inset);
            Color borderColor = hover ? BorderBright : Border;
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderColor);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), borderColor);
            Label(rect, "X", labelHeader, TextSecondary, TextAnchor.MiddleCenter);
            return Widgets.ButtonInvisible(rect);
        }

        public static void DrawTabStrip(Rect rect)
        {
            DrawRect(rect, SurfaceAlt);
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), Border);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BorderBright);
        }

        public static bool DrawTab(Rect rect, string text, bool selected)
        {
            EnsureStyles();
            bool hover = Mouse.IsOver(rect);
            Color fill = selected ? new Color(0.18f, 0.19f, 0.21f, 1f) : new Color(0.15f, 0.16f, 0.18f, 1f);
            if (hover && !selected)
            {
                fill = new Color(0.18f, 0.19f, 0.22f, 1f);
            }

            DrawRect(rect, fill);
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), selected ? BorderBright : Border);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), selected ? AccentMuted : Border);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), selected ? BorderBright : Border);
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), selected ? BorderBright : Border);
            if (selected)
            {
                DrawLine(new Rect(rect.x + 1f, rect.yMax - 2f, rect.width - 2f, 2f), Accent);
            }

            Label(rect, text, labelHeader, selected ? TextPrimary : TextSecondary, TextAnchor.MiddleCenter);
            return Widgets.ButtonInvisible(rect);
        }

        public static void DrawPanel(Rect rect)
        {
            DrawRect(rect, Surface);
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), BorderBright);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Border);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), Border);
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Border);
        }

        public static void DrawInset(Rect rect)
        {
            DrawRect(rect, Inset);
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0f, 0f, 0f, 0.45f));
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Border);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.35f));
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Border);
        }

        public static void DrawSectionHeader(Rect rect, string title)
        {
            EnsureStyles();
            DrawLine(new Rect(rect.x, rect.center.y, rect.width, 1f), AccentMuted);
            Label(new Rect(rect.x, rect.y, rect.width, rect.height), title.ToUpperInvariant(), labelHeader, TextSecondary, TextAnchor.MiddleLeft);
        }

        public static void DrawChip(Rect rect, string text, bool accent = false)
        {
            EnsureStyles();
            DrawRect(rect, accent ? new Color(0.06f, 0.20f, 0.25f, 1f) : Surface);
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), accent ? Accent : Border);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), accent ? AccentMuted : Border);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), accent ? AccentMuted : Border);
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), accent ? AccentMuted : Border);
            Label(rect, text.ToUpperInvariant(), labelTiny, accent ? Accent : TextSecondary, TextAnchor.MiddleCenter);
        }

        public static bool DrawCompactButton(Rect rect, string text, bool accent = false)
        {
            DrawCompactButtonVisual(rect, text, accent);
            return Widgets.ButtonInvisible(rect);
        }

        public static void DrawCompactButtonVisual(Rect rect, string text, bool accent = false)
        {
            EnsureStyles();
            bool hover = Mouse.IsOver(rect);
            Color fill = accent ? new Color(0.06f, 0.20f, 0.25f, 1f) : SurfaceAlt;
            if (hover)
            {
                fill = accent ? new Color(0.08f, 0.26f, 0.33f, 1f) : new Color(0.15f, 0.18f, 0.21f, 1f);
            }

            DrawRect(rect, fill);
            Color borderColor = accent ? Accent : (hover ? BorderBright : Border);
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderColor);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), borderColor);
            Label(rect, text, labelSmall, accent ? Accent : TextPrimary, TextAnchor.MiddleCenter);
        }

        public static void DrawRow(Rect rect, bool hover = false, bool selected = false, bool warning = false)
        {
            Color fill = hover ? new Color(0.12f, 0.15f, 0.18f, 1f) : SurfaceAlt;
            if (warning)
            {
                fill = new Color(0.11f, 0.13f, 0.15f, 1f);
            }

            DrawRect(rect, fill);

            Color borderColor = selected ? Accent : (warning ? Warning * 0.8f : (hover ? BorderBright : Border));
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Border);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), selected ? AccentMuted : Border);
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Border);
        }

        public static void DrawStatusPill(Rect rect, string text, Color color)
        {
            DrawRect(rect, new Color(color.r * 0.18f, color.g * 0.18f, color.b * 0.18f, 1f));
            DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), color);
            DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color * 0.8f);
            DrawLine(new Rect(rect.x, rect.y, 1f, rect.height), color * 0.8f);
            DrawLine(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color * 0.8f);
            Label(rect, text.ToUpperInvariant(), labelTiny, color, TextAnchor.MiddleCenter);
        }

        public static void DrawSubtleText(Rect rect, string text, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Label(rect, text, labelTiny, TextSecondary, anchor);
        }

        public static void DrawPrimaryText(Rect rect, string text, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Label(rect, text, labelSmall, TextPrimary, anchor);
        }

        public static void DrawWrappedText(Rect rect, string text, Color color, int fontSize = 12)
        {
            EnsureStyles();
            GUIStyle wrappedStyle = new GUIStyle(labelSmall)
            {
                wordWrap = true,
                fontSize = fontSize,
                clipping = TextClipping.Clip
            };
            wrappedStyle.normal.textColor = color;
            GUI.Label(rect, text, wrappedStyle);
        }

        public static void DrawMonoText(Rect rect, string text, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Label(rect, text, labelMono, color, anchor);
        }

        public static void ApplyScrollStyles()
        {
            EnsureStyles();
            EnsureScrollbarStyles();
            GUI.skin.verticalScrollbar = corvusVerticalScrollbar;
            GUI.skin.verticalScrollbarThumb = corvusVerticalScrollbarThumb;
        }

        public static void Label(Rect rect, string text, GUIStyle style, Color color, TextAnchor anchor)
        {
            EnsureStyles();

            GUIStyle activeStyle = style ?? labelSmall;
            Color previousGuiColor = GUI.color;
            Color previousTextColor = activeStyle.normal.textColor;
            TextAnchor previousAnchor = activeStyle.alignment;

            GUI.color = Color.white;
            activeStyle.normal.textColor = color;
            activeStyle.alignment = anchor;
            GUI.Label(rect, text, activeStyle);

            activeStyle.normal.textColor = previousTextColor;
            activeStyle.alignment = previousAnchor;
            GUI.color = previousGuiColor;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, SolidTexture);
            GUI.color = Color.white;
        }

        private static void DrawLine(Rect rect, Color color)
        {
            DrawRect(rect, color);
        }

        private static void EnsureScrollbarStyles()
        {
            if (corvusVerticalScrollbar != null && corvusVerticalScrollbarThumb != null)
            {
                return;
            }

            scrollbarTrackTexture = CreateTexture(new Color(0.07f, 0.08f, 0.10f, 1f));
            scrollbarThumbTexture = CreateTexture(new Color(0.20f, 0.25f, 0.29f, 1f));
            scrollbarThumbHoverTexture = CreateTexture(new Color(0.24f, 0.34f, 0.40f, 1f));

            corvusVerticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar)
            {
                fixedWidth = 12f,
                border = new RectOffset(1, 1, 1, 1),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(1, 1, 1, 1)
            };
            corvusVerticalScrollbar.normal.background = scrollbarTrackTexture;
            corvusVerticalScrollbar.hover.background = scrollbarTrackTexture;
            corvusVerticalScrollbar.active.background = scrollbarTrackTexture;

            corvusVerticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb)
            {
                fixedWidth = 10f,
                border = new RectOffset(1, 1, 1, 1),
                margin = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(0, 0, 0, 0)
            };
            corvusVerticalScrollbarThumb.normal.background = scrollbarThumbTexture;
            corvusVerticalScrollbarThumb.hover.background = scrollbarThumbHoverTexture;
            corvusVerticalScrollbarThumb.active.background = scrollbarThumbHoverTexture;
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
