using System;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The templates of the box's own look, made once in From from what the game's white box is built of: its line of
    /// text, title, headline font, grey and rule, the round sprite and the satisfaction bar's green, the switch's tick,
    /// and the game's red, orange and green (GlobalReferences.colors). Nothing is shipped but the apps' accents (LookRules).
    /// Each part is one text where the old box had one (a row's label and value share a text; a heading carries its
    /// hairline, a meter its bar), so a page is as many objects as before. Costs nothing a frame.
    /// </summary>
    public sealed partial class InfoBox
    {
        // Read from the game's box before its parts are taken away.
        private Sprite _round, _tick;
        private TMP_FontAsset _headingFont;
        private Color _ink = new Color(0.067f, 0.082f, 0.098f, 1f);    // the box's own text (MyEmployees' EmployeeName and Age)
        private Color _muted = new Color(0.349f, 0.412f, 0.459f, 1f);  // the box's own headline grey
        private Color _rule = new Color(0.741f, 0.78f, 0.812f, 1f);    // the box's own grey rule (SplitterGray)
        private Color _good = new Color(0.369f, 0.651f, 0.376f, 1f);   // the satisfaction bar's green
        private Color _warn = new Color(0.949f, 0.553f, 0.153f, 1f);   // the game's orange, as its tables show yellow on a light row
        private Color _bad = new Color(0.929f, 0.251f, 0.341f, 1f);    // the box's own red (CalledInSickWarning)
        private Color _accent = new Color(0.349f, 0.412f, 0.459f, 1f);
        private bool _medium;   // the body font has a medium weight: values in it, else in bold

        // A row's mark-up, made once: the label's size and grey, the value's weight, each tone's colour.
        private string _labelOpen, _valueOpen, _valueClose;
        private readonly string[] _toneOpen = new string[4];

        private GameObject _bodyT, _subT, _noteT, _headerT, _chipsT, _chipT, _headingT, _pairT, _meterT, _checkT, _footT;

        /// <summary>The app's colour: the bar over each page's title and its plain chips (LookRules.Accent).</summary>
        public void SetAccent(int rgb)
        {
            _accent = Rgb(rgb);
        }

        private static Color Rgb(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        // Read where the box shows them; each keeps its stand-in if the game moved it.
        private void ReadLook(Transform whiteBox)
        {
            var line = _line != null ? _line.GetComponent<TMP_Text>() : null;
            if (line != null)
            {
                _ink = line.color;
                _medium = HasMedium(line.font);
            }
            if (_headline != null)
            {
                var heading = _headline.GetComponentInChildren<TMP_Text>(true);
                if (heading != null)
                {
                    _muted = heading.color;
                    _headingFont = heading.font;
                }
                Transform splitter = _headline.transform.Find("SplitterGray");
                var rule = splitter != null ? splitter.GetComponent<Image>() : null;
                if (rule != null)
                {
                    _rule = rule.color;
                }
            }
            Transform bar = whiteBox.Find("SatisfactionPanel/SatisfactionProgressBar/BarIndicator");
            Transform back = bar != null ? bar.Find("Background") : null, fill = bar != null ? bar.Find("Fill Area/Fill") : null;
            var backImage = back != null ? back.GetComponent<Image>() : null;
            var fillImage = fill != null ? fill.GetComponent<Image>() : null;
            _round = backImage != null ? backImage.sprite : null;
            if (fillImage != null)
            {
                _good = fillImage.color;
            }
            if (_round == null)
            {
                _round = GameSprites.Get("Circle-20") ?? GameSprites.Get("circle");
            }
            if (_switch != null)
            {
                var toggle = _switch.GetComponentInChildren<Toggle>(true);
                var mark = toggle != null ? toggle.graphic as Image : null;
                _tick = mark != null ? mark.sprite : null;
            }
            ReadGameColours();
        }

        // A method of its own: a game without GlobalReferences' colours costs the look its tones, not the box.
        private void ReadGameColours()
        {
            try
            {
                var references = InstanceBehavior<GlobalReferences>.Instance;
                if (references != null && references.colors != null)
                {
                    _bad = references.colors.red;
                    _warn = references.colors.orange;
                    _good = references.colors.green;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Game UI: the game's colours could not be read for the apps' boxes (" + e.GetType().Name + "); the box keeps its own.");
            }
        }

        private static bool HasMedium(TMP_FontAsset font)
        {
            TMP_FontWeightPair[] weights = font != null ? font.fontWeightTable : null;
            return weights != null && weights.Length > 5 && weights[5].regularTypeface != null;
        }

        /// <summary>A tone's colour for words on white: the game's own, part way to the ink so that green and orange read.</summary>
        private Color Ink(CellTone tone)
        {
            switch (tone)
            {
                case CellTone.Green: return Color.Lerp(_good, _ink, 0.3f);
                case CellTone.Yellow: return Color.Lerp(_warn, _ink, 0.25f);
                case CellTone.Red: return _bad;
                default: return _ink;
            }
        }

        /// <summary>A tone's own colour, for a meter's fill and a chip's ground.</summary>
        private Color Paint(CellTone tone)
        {
            switch (tone)
            {
                case CellTone.Green: return _good;
                case CellTone.Yellow: return _warn;
                case CellTone.Red: return _bad;
                default: return _accent;
            }
        }

        private static string Hex(Color colour)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(colour) + ">";
        }

        // One small gap between parts; a heading, a header and the foot make their own room.
        private void Space(Transform box)
        {
            var layout = box != null ? box.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null)
            {
                layout.spacing = LookRules.BoxSpacing;
            }
            var rect = box as RectTransform;
            var padding = layout != null ? layout.padding : null;
            float width = rect != null ? rect.rect.width - (padding != null ? padding.left + padding.right : 0) - LookRules.Indent : 0f;
            _capacity = LookRules.Capacity(width > 200f ? width : LookRules.DefaultWidth, LookRules.BodySize);   // a box not laid out yet: the phone's width
        }

        private void MakeLook(Transform templates)
        {
            _labelOpen = "<size=" + LookRules.LabelSize.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ">" + Hex(_muted);
            _valueOpen = _medium ? "<font-weight=500>" : "<b>";
            _valueClose = _medium ? "</font-weight>" : "</b>";
            _toneOpen[(int)CellTone.Plain] = string.Empty;
            _toneOpen[(int)CellTone.Green] = Hex(Ink(CellTone.Green));
            _toneOpen[(int)CellTone.Yellow] = Hex(Ink(CellTone.Yellow));
            _toneOpen[(int)CellTone.Red] = Hex(Ink(CellTone.Red));

            _bodyT = Words(templates, "Line", LookRules.BodySize, _ink, FontStyles.Normal, false);
            _subT = Words(templates, "Subtitle", LookRules.SubtitleSize, _muted, FontStyles.Normal, false);
            _subT.GetComponent<TMP_Text>().margin = new Vector4(0f, 0f, 0f, 6f);
            _noteT = Words(templates, "Note", LookRules.NoteSize, _muted, FontStyles.Normal, false);

            // The header: the title under a short bar of the app's colour; chips at its right.
            _headerT = UnityEngine.Object.Instantiate(_title, templates, false);
            _headerT.name = "Header";
            Plain(_headerT);
            var titleLayout = _headerT.GetComponent<LayoutElement>();
            if (titleLayout != null)
            {
                titleLayout.minHeight = -1f;   // the game's name keeps room for one line of 90; a title is as tall as its lines
            }
            _headerT.GetComponent<TMP_Text>().margin = new Vector4(0f, LookRules.AccentHeight + 16f, 0f, 0f);
            Image accent = Picture(_headerT.transform, "Accent", _round, _accent);
            Pin((RectTransform)accent.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(LookRules.AccentWidth, LookRules.AccentHeight));
            Round(accent, LookRules.AccentHeight);

            _chipsT = Holder(templates, "Chips", 10f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperRight);
            var fit = _chipsT.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _chipsT.AddComponent<LayoutElement>().ignoreLayout = true;
            _chipT = Holder(templates, "Chip", 0f, new RectOffset(18, 18, 6, 6), TextAnchor.MiddleCenter);
            Image ground = _chipT.AddComponent<Image>();
            ground.sprite = _round;
            ground.type = Image.Type.Sliced;
            ground.raycastTarget = false;
            Round(ground, LookRules.ChipSize * 1.25f + 12f);
            GameObject chipWords = Words(_chipT.transform, "Text", LookRules.ChipSize, _accent, FontStyles.Bold | FontStyles.UpperCase, false);
            var chipText = chipWords.GetComponent<TMP_Text>();
            chipText.characterSpacing = LookRules.ChipTracking;
            chipText.textWrappingMode = TextWrappingModes.NoWrap;
            chipText.alignment = TextAlignmentOptions.Center;

            // A section's heading: small capitals in grey, room above, the hairline under it.
            _headingT = Words(templates, "Heading", LookRules.HeadingSize, _muted, FontStyles.SmallCaps | FontStyles.Bold, true);
            var headingText = _headingT.GetComponent<TMP_Text>();
            headingText.characterSpacing = LookRules.HeadingTracking;
            headingText.margin = new Vector4(0f, LookRules.SectionGap, 0f, 10f);
            if (_headingFont != null)
            {
                headingText.font = _headingFont;
            }
            Rule(_headingT.transform, false);

            // Two rows side by side, each half the line.
            _pairT = Holder(templates, "RowPair", 40f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
            _pairT.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            for (int i = 0; i < 2; i++)
            {
                GameObject half = Words(_pairT.transform, "Half", LookRules.BodySize, _ink, FontStyles.Normal, false);
                var share = half.AddComponent<LayoutElement>();
                share.minWidth = share.preferredWidth = 0f;   // the halves share the line equally, whatever their words
                share.flexibleWidth = 1f;
            }

            // A meter: a row with a short bar of the round sprite under the figure.
            _meterT = Words(templates, "Meter", LookRules.BodySize, _ink, FontStyles.Normal, false);
            _meterT.GetComponent<TMP_Text>().margin = new Vector4(0f, 0f, 0f, LookRules.MeterHeight + 10f);
            Image track = Picture(_meterT.transform, "Bar", _round, Color.Lerp(_rule, Color.white, 0.35f));
            Pin((RectTransform)track.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(LookRules.MeterWidth, LookRules.MeterHeight));
            Round(track, LookRules.MeterHeight);
            Image fill = Picture(track.transform, "Fill", _round, _good);
            Round(fill, LookRules.MeterHeight);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

            // A need: the game's tick before the words, or a dot in its tone.
            _checkT = Words(templates, "Check", LookRules.BodySize, _ink, FontStyles.Normal, false);
            _checkT.GetComponent<TMP_Text>().margin = new Vector4(LookRules.MarkSize + 14f, 0f, 0f, 0f);
            Image mark = Picture(_checkT.transform, "Mark", _tick ?? _round, _good);
            mark.preserveAspect = true;
            mark.type = Image.Type.Simple;
            Pin((RectTransform)mark.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 2f), new Vector2(LookRules.MarkSize, LookRules.MarkSize));

            // The foot: a hairline, then the Guide's page in the note's style.
            _footT = Words(templates, "Foot", LookRules.NoteSize, _muted, FontStyles.Normal, false);
            _footT.GetComponent<TMP_Text>().margin = new Vector4(0f, 18f, 0f, 0f);
            Rule(_footT.transform, true);
        }

        // A copy of the box's line of text set for one part. Its localisation component goes (parts are written in final
        // words) unless the part is written by key.
        private GameObject Words(Transform parent, string name, float size, Color colour, FontStyles style, bool byKey)
        {
            GameObject made = UnityEngine.Object.Instantiate(_line, parent, false);
            made.name = name;
            if (!byKey)
            {
                Plain(made);
            }
            var text = made.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.color = colour;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            text.richText = true;
            return made;
        }

        private static void Plain(GameObject made)
        {
            var localised = made.GetComponent<TextLocalizationComponent>();
            if (localised != null)
            {
                UnityEngine.Object.DestroyImmediate(localised);
            }
        }

        private static GameObject Holder(Transform parent, string name, float spacing, RectOffset padding, TextAnchor align)
        {
            var made = new GameObject(name, typeof(RectTransform));
            made.transform.SetParent(parent, false);
            var group = made.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding;
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return made;
        }

        private static Image Picture(Transform parent, string name, Sprite sprite, Color colour)
        {
            var made = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            made.transform.SetParent(parent, false);
            made.GetComponent<LayoutElement>().ignoreLayout = true;   // on a text, placed by its anchors, not by the box
            var image = made.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        // A sliced round sprite as a pill of a height: its corners scaled to half the height.
        private static void Round(Image image, float height)
        {
            if (image.sprite == null || image.sprite.border.x <= 0f)
            {
                return;
            }
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, image.sprite.border.x * 2f / height);
        }

        // A solid hairline in the rule grey, at a text's foot (a heading) or top (the foot).
        private void Rule(Transform text, bool top)
        {
            Image line = Picture(text, "Rule", null, _rule);
            var rect = (RectTransform)line.transform;
            float y = top ? 1f : 0f;
            rect.anchorMin = new Vector2(0f, y);
            rect.anchorMax = new Vector2(1f, y);
            rect.pivot = new Vector2(0.5f, y);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, LookRules.RuleHeight);
        }
    }
}
