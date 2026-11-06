using System;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The templates of the box's own look, made once from what the game's white box is built of (its texts, sprites and
    /// colours): nothing is shipped but the apps' accents. Each part is one text where the old box had one.
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

        // The parts a page is written with, each a template asleep among the box's own; copied once, laid aside between pages.
        private GameObject _bodyT, _subT, _noteT, _headerT, _chipsT, _chipT, _groupT, _rowT, _pairT, _meterT, _entryT, _checkT, _guideT;

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

        // Every part is a layout the box's vertical layout sizes: a text asks for the height its words take at the width it is
        // given, a fact is a horizontal layout of its label and its value, and no part has a position or a height of its own.
        private void MakeLook(Transform templates)
        {
            _bodyT = Words(templates, "Line", LookRules.BodySize, _ink, FontStyles.Normal, false);
            _subT = Words(templates, "Subtitle", LookRules.SubtitleSize, _muted, FontStyles.Normal, false);
            _noteT = Words(templates, "Note", LookRules.NoteSize, _muted, FontStyles.Normal, false);
            _guideT = Words(templates, "Guide", LookRules.NoteSize, _muted, FontStyles.Normal, false);

            // The header: a title that wraps in the room the chips leave it, under a short bar of the app's colour.
            _headerT = Holder(templates, "Header", 16f, new RectOffset(0, 0, (int)(LookRules.AccentHeight + 14f), 0), TextAnchor.UpperLeft);
            GameObject title = UnityEngine.Object.Instantiate(_title, _headerT.transform, false);
            title.name = "Title";
            Plain(title);
            var titleText = title.GetComponent<TMP_Text>();
            titleText.margin = Vector4.zero;
            titleText.raycastTarget = false;
            titleText.alignment = TextAlignmentOptions.TopLeft;
            var titleSize = title.GetComponent<LayoutElement>() ?? title.AddComponent<LayoutElement>();
            titleSize.minHeight = titleSize.preferredHeight = -1f;   // the game's name keeps room for one line of 90; a title is as tall as its lines
            titleSize.minWidth = 0f;
            titleSize.flexibleWidth = 1f;
            Image accent = Picture(_headerT.transform, "Accent", _round, _accent);
            Pin((RectTransform)accent.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(LookRules.AccentWidth, LookRules.AccentHeight));
            Round(accent, LookRules.AccentHeight);

            // Chips: pills in a row, at the title's right, or under it when they are too many or too wide (LeftPaneRules.ChipsBelow).
            _chipsT = Holder(templates, "Chips", 10f, new RectOffset(0, 0, 10, 0), TextAnchor.UpperRight);
            var chipsSize = _chipsT.AddComponent<LayoutElement>();
            chipsSize.flexibleWidth = 0f;
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

            // A section: a card in a tint of the rule's grey, its heading in small capitals over a hairline, its parts under them.
            _groupT = Column(templates, "Group", new RectOffset((int)LeftPaneRules.GroupPadX, (int)LeftPaneRules.GroupPadX, (int)LeftPaneRules.GroupPadTop, (int)LeftPaneRules.GroupPadBottom),
                LeftPaneRules.GroupSpacing);
            Image card = _groupT.AddComponent<Image>();
            card.sprite = _round;
            card.type = Image.Type.Sliced;
            card.color = Color.Lerp(_rule, Color.white, 0.8f);
            card.raycastTarget = false;
            Round(card, 48f);
            GameObject heading = Words(_groupT.transform, "Heading", LookRules.HeadingSize, _muted, FontStyles.SmallCaps | FontStyles.Bold, true);
            var headingText = heading.GetComponent<TMP_Text>();
            headingText.characterSpacing = LookRules.HeadingTracking;
            headingText.lineSpacing = 0f;
            if (_headingFont != null)
            {
                headingText.font = _headingFont;
            }
            Bar(_groupT.transform, "Rule", null, _rule, LookRules.RuleHeight);

            // A fact: the label at the left in grey, the value at the right in the box's ink, one size so that they share a baseline;
            // the label keeps what it needs, the value takes the rest and wraps under itself, right aligned.
            _rowT = Holder(templates, "Row", LeftPaneRules.RowGap, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
            Words(_rowT.transform, "Label", LookRules.LabelSize, _muted, FontStyles.Normal, false);
            GameObject value = Words(_rowT.transform, "Value", LookRules.BodySize, _ink, _medium ? FontStyles.Normal : FontStyles.Bold, false);
            var valueText = value.GetComponent<TMP_Text>();
            valueText.alignment = TextAlignmentOptions.TopRight;
            if (_medium)
            {
                valueText.fontWeight = FontWeight.Medium;
            }
            var labelSize = _rowT.transform.GetChild(0).gameObject.AddComponent<LayoutElement>();
            labelSize.flexibleWidth = 0f;   // its width is set for each fact (LeftPaneRules.LabelWidth)
            var valueSize = value.AddComponent<LayoutElement>();
            valueSize.minWidth = 0f;
            valueSize.flexibleWidth = 1f;

            // Two facts side by side, each half the line.
            _pairT = Holder(templates, "RowPair", LeftPaneRules.PairGap, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
            for (int i = 0; i < 2; i++)
            {
                GameObject half = UnityEngine.Object.Instantiate(_rowT, _pairT.transform, false);
                half.name = "Half";
                var share = half.AddComponent<LayoutElement>();
                share.minWidth = share.preferredWidth = 0f;   // the halves share the line equally, whatever their words
                share.flexibleWidth = 1f;
            }

            // A meter: a fact, and under it a bar of the round sprite the card's whole width.
            _meterT = Column(templates, "Meter", new RectOffset(0, 0, 0, 0), 8f);
            UnityEngine.Object.Instantiate(_rowT, _meterT.transform, false).name = "Row";
            Image track = Bar(_meterT.transform, "Track", _round, Color.Lerp(_rule, Color.white, 0.35f), LookRules.MeterHeight);
            Round(track, LookRules.MeterHeight);
            Image fill = Picture(track.transform, "Fill", _round, _good);
            Round(fill, LookRules.MeterHeight);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

            // A list entry (a rung of a ladder): its name and its figure on one line, what it asks under them in small grey; the
            // current one on a tint of the app's colour with a mark at its left.
            _entryT = Column(templates, "Entry", new RectOffset((int)LeftPaneRules.EntryPadLeft, (int)LeftPaneRules.EntryPadRight, (int)LeftPaneRules.EntryPadY, (int)LeftPaneRules.EntryPadY), 4f);
            Image back = _entryT.AddComponent<Image>();
            back.sprite = _round;
            back.type = Image.Type.Sliced;
            back.color = Color.clear;
            back.raycastTarget = false;
            Round(back, 36f);
            GameObject top = UnityEngine.Object.Instantiate(_rowT, _entryT.transform, false);
            top.name = "Top";
            var entryName = top.transform.GetChild(0).GetComponent<TMP_Text>();
            entryName.color = _ink;
            entryName.fontStyle = _medium ? FontStyles.Normal : FontStyles.Bold;
            if (_medium)
            {
                entryName.fontWeight = FontWeight.Medium;
            }
            Words(_entryT.transform, "Detail", LookRules.NoteSize, _muted, FontStyles.Normal, false);
            Image mark = Picture(_entryT.transform, "Mark", _round, _accent);
            var markRect = (RectTransform)mark.transform;
            markRect.anchorMin = new Vector2(0f, 0f);
            markRect.anchorMax = new Vector2(0f, 1f);
            markRect.pivot = new Vector2(0f, 0.5f);
            markRect.offsetMin = new Vector2(2f, 8f);
            markRect.offsetMax = new Vector2(2f + LeftPaneRules.MarkWidth, -8f);
            Round(mark, LeftPaneRules.MarkWidth * 2f);
            mark.gameObject.SetActive(false);

            // A need: the game's tick before the words, or a dot in its tone.
            _checkT = Holder(templates, "Check", 14f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
            Image tick = Picture(_checkT.transform, "Mark", _tick ?? _round, _good);
            tick.preserveAspect = true;
            tick.type = Image.Type.Simple;
            var tickSize = tick.GetComponent<LayoutElement>();
            tickSize.ignoreLayout = false;
            tickSize.minWidth = tickSize.preferredWidth = LookRules.MarkSize;
            tickSize.minHeight = tickSize.preferredHeight = LookRules.BodySize * 1.3f;   // as tall as a line, so the tick stands on it
            tickSize.flexibleWidth = 0f;
            GameObject words = Words(_checkT.transform, "Text", LookRules.BodySize, _ink, FontStyles.Normal, false);
            var wordsSize = words.AddComponent<LayoutElement>();
            wordsSize.minWidth = 0f;
            wordsSize.flexibleWidth = 1f;
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
            text.lineSpacing = LookRules.LineSpacing;   // the game's line of the box has far more, which the layout counts but a long note then fills
            text.paragraphSpacing = 0f;
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

        // A bar that the layout gives the whole width and a height: a section's rule, a meter's track.
        private static Image Bar(Transform parent, string name, Sprite sprite, Color colour, float height)
        {
            Image made = Picture(parent, name, sprite, colour);
            var size = made.GetComponent<LayoutElement>();
            size.ignoreLayout = false;
            size.minHeight = size.preferredHeight = height;
            size.flexibleWidth = 1f;
            return made;
        }
    }
}
