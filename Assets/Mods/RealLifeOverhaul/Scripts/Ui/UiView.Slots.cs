using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The objects each kind of item is drawn with, made once into a pool and bound to item after item.</summary>
    public sealed partial class UiView
    {
        private const float InitialsShare = 0.36f;     // an avatar's initials, of its size
        private const float PadLetterShare = 0.5f;     // a pad button's letter, of its cap

        /// <summary>One pooled object: its rect and the target it belongs to now.</summary>
        internal abstract class Slot
        {
            public RectTransform Rect;
            public Graphic[] Graphics;   // what an arrival fades
            public int Node = -1;
            public int Item = -1;
        }

        private sealed class BoxSlot : Slot
        {
            public Image Fill;
            public Image Edge;
            public Image Rule;       // a framed panel's head rule, made the first time a theme wears one
            public UiHit Hit;
            public UiRole Role;
            public float Alpha;
        }

        private sealed class TextSlot : Slot
        {
            public TextMeshProUGUI Text;
            public UiStyle Style = UiStyle.Count;
        }

        private sealed class ImageSlot : Slot
        {
            public Image Image;
        }

        private sealed class AvatarSlot : Slot
        {
            public Image Circle;
            public TextMeshProUGUI Letters;
        }

        private sealed class SparkSlot : Slot
        {
            public UiSpark Line;
        }

        private sealed class KeyCapSlot : Slot
        {
            public Image Face;
            public Image Edge;
            public Image Glyph;
            public TextMeshProUGUI Label;
        }

        private sealed class BusySlot : Slot
        {
            public readonly Image[] Dots = new Image[3];
        }

        private Slot Make(UiKind kind, RectTransform parent)
        {
            RectTransform r = NewRect(parent, kind.ToString());
            switch (kind)
            {
                case UiKind.Box:
                case UiKind.HueDot:
                {
                    var s = new BoxSlot { Rect = r, Fill = r.gameObject.AddComponent<Image>() };
                    s.Fill.raycastTarget = false;
                    s.Fill.type = Image.Type.Sliced;
                    RectTransform er = NewRect(r, "Edge");
                    Stretch(er);
                    s.Edge = er.gameObject.AddComponent<Image>();
                    s.Edge.raycastTarget = false;
                    s.Edge.type = Image.Type.Sliced;
                    s.Hit = r.gameObject.AddComponent<UiHit>();
                    s.Graphics = new Graphic[] { s.Fill, s.Edge };
                    return s;
                }
                case UiKind.Text:
                {
                    var t = r.gameObject.AddComponent<TextMeshProUGUI>();
                    Plain(t);
                    return new TextSlot { Rect = r, Text = t, Graphics = new Graphic[] { t } };
                }
                case UiKind.Avatar:
                {
                    var s = new AvatarSlot { Rect = r, Circle = r.gameObject.AddComponent<Image>() };
                    s.Circle.raycastTarget = false;
                    s.Circle.type = Image.Type.Sliced;
                    s.Circle.sprite = UiSprites.Circle;
                    RectTransform tr = NewRect(r, "Initials");
                    Stretch(tr);
                    s.Letters = tr.gameObject.AddComponent<TextMeshProUGUI>();
                    Plain(s.Letters);
                    s.Letters.alignment = TextAlignmentOptions.Center;
                    s.Graphics = new Graphic[] { s.Circle, s.Letters };
                    return s;
                }
                case UiKind.Spark:
                {
                    r.gameObject.AddComponent<CanvasRenderer>();   // a graphic of the mod's own: the engine adds the renderer only for its own kinds
                    var line = r.gameObject.AddComponent<UiSpark>();
                    line.raycastTarget = false;
                    return new SparkSlot { Rect = r, Line = line, Graphics = new Graphic[] { line } };
                }
                case UiKind.KeyCap:
                {
                    var s = new KeyCapSlot { Rect = r, Face = r.gameObject.AddComponent<Image>() };
                    s.Face.raycastTarget = false;
                    s.Face.type = Image.Type.Sliced;
                    RectTransform er = NewRect(r, "Edge");
                    Stretch(er);
                    s.Edge = er.gameObject.AddComponent<Image>();
                    s.Edge.raycastTarget = false;
                    s.Edge.type = Image.Type.Sliced;
                    RectTransform gr = NewRect(r, "Glyph");
                    Stretch(gr);
                    s.Glyph = gr.gameObject.AddComponent<Image>();
                    s.Glyph.raycastTarget = false;
                    s.Glyph.preserveAspect = true;
                    RectTransform lr = NewRect(r, "Label");
                    Stretch(lr);
                    s.Label = lr.gameObject.AddComponent<TextMeshProUGUI>();
                    Plain(s.Label);
                    s.Label.alignment = TextAlignmentOptions.Center;
                    s.Graphics = new Graphic[] { s.Face, s.Edge, s.Glyph, s.Label };
                    return s;
                }
                case UiKind.Busy:
                {
                    var s = new BusySlot { Rect = r };
                    for (int i = 0; i < s.Dots.Length; i++)
                    {
                        RectTransform dr = NewRect(r, "Dot" + i);
                        s.Dots[i] = dr.gameObject.AddComponent<Image>();
                        s.Dots[i].raycastTarget = false;
                        s.Dots[i].sprite = UiSprites.Circle;
                        s.Dots[i].type = Image.Type.Sliced;
                    }
                    s.Graphics = new Graphic[] { s.Dots[0], s.Dots[1], s.Dots[2] };
                    return s;
                }
                default:
                {
                    var img = r.gameObject.AddComponent<Image>();
                    img.raycastTarget = false;
                    return new ImageSlot { Rect = r, Image = img, Graphics = new Graphic[] { img } };
                }
            }
        }

        // An engine text set up as the kit draws every text: no rich text, no auto size, no margins, not clickable.
        private static void Plain(TextMeshProUGUI t)
        {
            t.raycastTarget = false;
            t.richText = false;
            t.enableAutoSizing = false;
            t.margin = Vector4.zero;
            t.isOrthographic = true;
            t.extraPadding = false;
        }

        private void Bind(Slot slot, ref UiItem it, int index)
        {
            slot.Item = index;
            slot.Node = it.Node;
            switch (it.Kind)
            {
                case UiKind.Box:
                case UiKind.HueDot:
                    BindBox((BoxSlot)slot, ref it, index);
                    break;
                case UiKind.Text:
                    Place(slot.Rect, it.X, it.Y, it.W, it.H);
                    BindText(((TextSlot)slot).Text, ref it);
                    ((TextSlot)slot).Style = it.Style;
                    break;
                case UiKind.Icon:
                {
                    Place(slot.Rect, it.X, it.Y, it.W, it.H);
                    Image img = ((ImageSlot)slot).Image;
                    Sprite sprite = UiIcons.Get(it.Icon);
                    if (img.sprite != sprite)
                    {
                        img.sprite = sprite;
                    }
                    img.type = Image.Type.Simple;
                    img.color = Colour(it.Fill, it.Alpha);
                    break;
                }
                case UiKind.Avatar:
                {
                    Place(slot.Rect, it.X, it.Y, it.W, it.H);
                    var a = (AvatarSlot)slot;
                    a.Circle.pixelsPerUnitMultiplier = UiSprites.CircleScale(it.H * Scale);
                    a.Circle.color = ToColor(UiColour.AvatarFill(it.Value, _dark), it.Alpha);
                    a.Letters.font = UiFonts.For(UiStyle.Title);
                    a.Letters.fontSize = it.W * InitialsShare * Scale;
                    a.Letters.color = ToColor(UiColour.AvatarInk(it.Value, _dark), it.Alpha);
                    SetText(a.Letters, it.Text);
                    Show(a.Letters, !string.IsNullOrEmpty(it.Text));
                    break;
                }
                case UiKind.Spark:
                {
                    Place(slot.Rect, it.X, it.Y, it.W, it.H);
                    UiSpark line = ((SparkSlot)slot).Line;
                    line.Thickness = UiTokens.Hairline * 2f * Scale;
                    line.SetPoints(it.Points);
                    line.color = Colour(it.Fill, it.Alpha);
                    break;
                }
                case UiKind.KeyCap:
                    BindKeyCap((KeyCapSlot)slot, ref it);
                    break;
                case UiKind.Busy:
                {
                    Place(slot.Rect, it.X, it.Y, it.W, it.H);
                    var b = (BusySlot)slot;
                    float d = it.H, gap = (it.W - d * 3f) / 2f;
                    for (int i = 0; i < b.Dots.Length; i++)
                    {
                        b.Dots[i].pixelsPerUnitMultiplier = UiSprites.CircleScale(d * Scale);
                        Place((RectTransform)b.Dots[i].transform, i * (d + gap), 0f, d, d);
                        b.Dots[i].color = Colour(it.Fill, 1f);
                    }
                    Beat(b.Dots);
                    break;
                }
            }
        }

        private void BindBox(BoxSlot b, ref UiItem it, int index)
        {
            Place(b.Rect, it.X, it.Y, it.W, it.H);
            bool circle = (it.Flags & UiFlags.Circle) != 0 || it.Kind == UiKind.HueDot;
            Sprite fill = circle ? UiSprites.Circle : UiSprites.Fill(it.Radius);
            if (b.Fill.sprite != fill)
            {
                b.Fill.sprite = fill;
            }
            float slice = circle ? UiSprites.CircleScale(Mathf.Min(it.W, it.H) * Scale) : 1f;
            b.Fill.pixelsPerUnitMultiplier = slice;
            b.Edge.pixelsPerUnitMultiplier = slice;
            b.Role = it.Fill;
            b.Alpha = it.Alpha;
            b.Fill.color = it.Kind == UiKind.HueDot ? ToColor(UiColour.HueDot(it.Value, _dark), it.Alpha)
                : it.Fill == UiRole.None ? new Color(0f, 0f, 0f, 0f) : Colour(it.Fill, it.Alpha);
            bool edge = it.Edge != UiRole.None;
            Show(b.Edge, edge);
            if (edge)
            {
                Sprite e = circle ? UiSprites.CircleEdge : UiSprites.Edge(it.Radius);
                if (b.Edge.sprite != e)
                {
                    b.Edge.sprite = e;
                }
                b.Edge.color = Colour(it.Edge, 1f);
            }
            bool face = it.Node >= 0 && _scene.Nodes[it.Node].Face == index;
            b.Fill.raycastTarget = face;
            b.Hit.Face(face ? this : null, it.Node);
            // A framed panel's head rule, where the theme wears one: the column rule of a printed page, solid and square.
            bool rule = (it.Flags & UiFlags.Panel) != 0;
            if (rule && b.Rule == null)
            {
                RectTransform rr = NewRect(b.Rect, "Rule");
                rr.anchorMin = new Vector2(0f, 1f);
                rr.anchorMax = new Vector2(1f, 1f);
                rr.pivot = new Vector2(0.5f, 1f);
                b.Rule = rr.gameObject.AddComponent<Image>();
                b.Rule.raycastTarget = false;
            }
            if (b.Rule != null)
            {
                Show(b.Rule, rule);
                if (rule)
                {
                    b.Rule.rectTransform.sizeDelta = new Vector2(0f, UiTokens.FrameRule * Scale);
                    b.Rule.rectTransform.anchoredPosition = Vector2.zero;
                    b.Rule.color = Colour(UiRole.Rule, 1f);
                }
            }
        }

        private void BindText(TextMeshProUGUI t, ref UiItem it)
        {
            UiType type = UiTokens.Type(it.Style);
            TMP_FontAsset font = UiFonts.For(it.Style);
            if (font != null && t.font != font)
            {
                t.font = font;
            }
            float size = type.Size * Scale;
            if (!Mathf.Approximately(t.fontSize, size))
            {
                t.fontSize = size;
            }
            t.characterSpacing = type.Tracking * 100f;
            t.lineSpacing = (type.Leading - 1f) * UiFontMetrics.Face(type.Family, type.Weight).LineEm * 100f;
            FontStyles style = type.Case == UiCase.Upper ? FontStyles.UpperCase : type.Case == UiCase.Lower ? FontStyles.LowerCase : FontStyles.Normal;
            if (t.fontStyle != style)
            {
                t.fontStyle = style;
            }
            bool wrap = (it.Flags & UiFlags.Wrap) != 0;
            TextAlignmentOptions align = (it.Flags & UiFlags.Right) != 0 ? TextAlignmentOptions.TopRight
                : (it.Flags & UiFlags.Centre) != 0 && TextCentred(ref it) ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
            if (t.alignment != align)
            {
                t.alignment = align;
            }
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = (it.Flags & UiFlags.Truncate) != 0 ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            t.color = Colour(it.Ink, it.Alpha);
            SetText(t, it.Text);
        }

        // A centred flag centres a text's words where its box is wider than they are (an empty state), and every line of a
        // wrapped one, which once sat at the left of its box and ran on past it. A box cut to its words (a button's label)
        // was placed by centring already.
        private static bool TextCentred(ref UiItem it) => (it.Flags & UiFlags.Wrap) != 0 || it.W > UiTextRules.Width(it.Text, it.Style) + UiTokens.S2;

        private void BindKeyCap(KeyCapSlot k, ref UiItem it)
        {
            Place(k.Rect, it.X, it.Y, it.W, it.H);
            string letter = UiIcons.Letter(it.Icon);
            bool round = it.Icon >= UiIcon.PadA && it.Icon <= UiIcon.PadTriangle;
            Sprite face = round ? UiSprites.Circle : UiSprites.Fill(UiTokens.RadiusSmall);
            k.Face.sprite = face;
            k.Face.pixelsPerUnitMultiplier = round ? UiSprites.CircleScale(it.H * Scale) : 1f;
            k.Edge.pixelsPerUnitMultiplier = k.Face.pixelsPerUnitMultiplier;
            k.Face.color = Colour(it.Fill, it.Alpha);
            k.Edge.sprite = round ? UiSprites.CircleEdge : UiSprites.Edge(UiTokens.RadiusSmall);
            k.Edge.color = Colour(it.Edge, 0.55f);
            bool glyph = it.Icon != UiIcon.None && letter == null;
            Show(k.Glyph, glyph);
            if (glyph)
            {
                k.Glyph.sprite = UiIcons.Get(it.Icon);
                k.Glyph.color = Colour(it.Ink, 1f);
                float inset = UiTokens.S1 * Scale;
                RectTransform gr = (RectTransform)k.Glyph.transform;
                gr.offsetMin = new Vector2(inset, inset);
                gr.offsetMax = new Vector2(-inset, -inset);
            }
            string words = letter ?? it.Text;
            Show(k.Label, !string.IsNullOrEmpty(words));
            if (!string.IsNullOrEmpty(words))
            {
                UiType type = UiTokens.Type(UiStyle.KeyCap);
                k.Label.font = UiFonts.For(UiStyle.KeyCap);
                k.Label.fontSize = (letter != null ? it.H * PadLetterShare : type.Size) * Scale;
                k.Label.fontStyle = FontStyles.UpperCase;
                k.Label.textWrappingMode = TextWrappingModes.NoWrap;
                k.Label.alignment = TextAlignmentOptions.Midline;
                k.Label.color = Colour(it.Ink, 1f);
                SetText(k.Label, words);
            }
        }

        /// <summary>A text set only when it changes, as TextMeshPro builds its mesh again on every set.</summary>
        internal static void SetText(TMP_Text t, string text)
        {
            text = text ?? string.Empty;
            if (!string.Equals(t.text, text, System.StringComparison.Ordinal))
            {
                t.text = text;
            }
        }
    }
}
