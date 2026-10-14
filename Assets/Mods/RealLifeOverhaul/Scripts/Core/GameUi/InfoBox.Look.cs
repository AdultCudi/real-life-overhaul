using System;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The templates of the box's own look, made once when the box is taken (From), from what the game's white box is
    /// built of: its line of text (font, size, colour), its title, its headline's font, grey and rule, the round sprite
    /// and the green of its satisfaction bar, the tick of its switch, and the game's red, orange and green
    /// (GlobalReferences.colors). Nothing here is shipped: no picture, font or colour of the mod's own but the apps'
    /// accents (LookRules). Costs nothing per frame; a page copies these templates (InfoBox.Parts.cs).
    /// </summary>
    public sealed partial class InfoBox
    {
        // What the look is made of, read from the game's box before its parts are taken away.
        private Sprite _round, _tick;
        private TMP_FontAsset _headingFont;
        private Color _ink = new Color(0.067f, 0.082f, 0.098f, 1f);    // the box's own text (MyEmployees' EmployeeName and Age)
        private Color _muted = new Color(0.349f, 0.412f, 0.459f, 1f);  // the box's own headline grey
        private Color _rule = new Color(0.741f, 0.78f, 0.812f, 1f);    // the box's own grey rule (SplitterGray)
        private Color _good = new Color(0.369f, 0.651f, 0.376f, 1f);   // the satisfaction bar's green
        private Color _warn = new Color(0.949f, 0.553f, 0.153f, 1f);   // the game's orange, as its tables use it for yellow on a light row
        private Color _bad = new Color(0.929f, 0.251f, 0.341f, 1f);    // the box's own red (CalledInSickWarning)
        private Color _accent = new Color(0.349f, 0.412f, 0.459f, 1f);
        private bool _medium;   // the body font has a medium weight of its own: values are set in it, otherwise in bold

        // The templates, asleep among the box's.
        private GameObject _bodyT, _subT, _noteT, _headerT, _chipsT, _chipT, _sectionT, _rowT, _stackT, _pairT, _meterT, _checkT, _footT;

        /// <summary>The colour of the app the box belongs to: the bar over each page's title and its plain chips (LookRules.Accent).</summary>
        public void SetAccent(int rgb)
        {
            _accent = Rgb(rgb);
        }

        private static Color Rgb(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        // The game's own colours and pictures, read where the box shows them; each keeps its stand-in if the game moved it.
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
            var back = bar != null ? bar.Find("Background")?.GetComponent<Image>() : null;
            var fill = bar != null ? bar.Find("Fill Area/Fill")?.GetComponent<Image>() : null;
            _round = back != null ? back.sprite : null;
            if (fill != null)
            {
                _good = fill.color;
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

        // Kept in a method of its own: a game without GlobalReferences' colours costs the look its tones, not the box.
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

        /// <summary>A tone's colour for words on the white box: the game's own, a quarter of the way to the ink, so that green and orange read on white.</summary>
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

        // The box's own spacing: one small gap between its parts; a section, a header and the foot make their own room.
        private void Space(Transform box)
        {
            var layout = box != null ? box.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null)
            {
                layout.spacing = LookRules.BoxSpacing;
            }
            var rect = box as RectTransform;
            var padding = layout != null ? layout.padding : null;
            float width = rect != null ? rect.rect.width - (padding != null ? padding.left + padding.right : 0) : 0f;
            _capacity = LookRules.Capacity(width - LookRules.Indent, LookRules.BodySize);
        }

        // ---------------------------------------------------------------- the templates

        private void MakeLook(Transform templates)
        {
            _bodyT = Words(templates, "Body", LookRules.BodySize, _ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _subT = Words(templates, "Subtitle", LookRules.SubtitleSize, _muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Flexible(_subT);
            _noteT = Words(templates, "Note", LookRules.NoteSize, _muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);

            // The header: the app's colour, then the title and any chips beside it; a subtitle goes under them.
            _headerT = Holder(templates, "Header", false, 6f, new RectOffset(0, 0, 0, 6), TextAnchor.UpperLeft, false);
            Image accent = Picture(_headerT.transform, "Accent", _round, _accent);
            Size(accent.gameObject, LookRules.AccentWidth, LookRules.AccentHeight, false);
            Round(accent, LookRules.AccentHeight);
            GameObject titleRow = Holder(_headerT.transform, "TitleRow", true, 24f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, false);
            Flexible(titleRow);
            GameObject title = UnityEngine.Object.Instantiate(_title, titleRow.transform, false);
            title.name = "Title";
            var titleLayout = title.GetComponent<LayoutElement>() ?? title.AddComponent<LayoutElement>();
            titleLayout.minHeight = -1f;   // the game's name has room for one line of 90; a title here is as tall as its lines
            titleLayout.flexibleWidth = 1f;
            Plain(title);

            // Chips: a row of them beside the title, set down to stand level with its first line.
            _chipsT = Holder(templates, "Chips", true, 10f, new RectOffset(0, 0, 12, 0), TextAnchor.UpperRight, false);
            _chipT = Holder(templates, "Chip", true, 0f, new RectOffset(18, 18, 6, 6), TextAnchor.MiddleCenter, false);
            Image ground = _chipT.AddComponent<Image>();
            ground.sprite = _round;
            ground.type = Image.Type.Sliced;
            ground.raycastTarget = false;
            Round(ground, LookRules.ChipSize * 1.2f + 12f);
            GameObject chipWords = Words(_chipT.transform, "Text", LookRules.ChipSize, _accent, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.Center);
            chipWords.GetComponent<TMP_Text>().characterSpacing = LookRules.ChipTracking;
            NoWrap(chipWords);

            // A section: its heading in small capitals, a hairline, and its rows, indented.
            _sectionT = Holder(templates, "Section", false, 8f, new RectOffset(0, 0, (int)LookRules.SectionGap, 0), TextAnchor.UpperLeft, true);
            GameObject heading = Words(_sectionT.transform, "Heading", LookRules.HeadingSize, _muted, FontStyles.SmallCaps | FontStyles.Bold, TextAlignmentOptions.TopLeft, true);
            var headingText = heading.GetComponent<TMP_Text>();
            headingText.characterSpacing = LookRules.HeadingTracking;
            if (_headingFont != null)
            {
                headingText.font = _headingFont;
            }
            Rule(_sectionT.transform);
            Holder(_sectionT.transform, "Rows", false, LookRules.SectionSpacing, new RectOffset((int)LookRules.Indent, 0, 4, 0), TextAnchor.UpperLeft, true);

            // A row: the label at the left, muted; the value at the right, in the ink. Stacked: the value under its label.
            _rowT = Holder(templates, "Row", true, LookRules.RowGap, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, false);
            GameObject label = Words(_rowT.transform, "Label", LookRules.LabelSize, _muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Flexible(label);
            Value(Words(_rowT.transform, "Value", LookRules.BodySize, _ink, FontStyles.Normal, TextAlignmentOptions.TopRight));
            _stackT = Holder(templates, "Row", false, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, true);
            Words(_stackT.transform, "Label", LookRules.LabelSize, _muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Value(Words(_stackT.transform, "Value", LookRules.BodySize, _ink, FontStyles.Normal, TextAlignmentOptions.TopLeft));

            // Two rows side by side, each half of the line (the old pages' "Label value  Label value").
            _pairT = Holder(templates, "RowPair", true, 40f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, true);
            for (int i = 0; i < 2; i++)
            {
                GameObject half = Holder(_pairT.transform, "Half", true, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, false);
                var share = half.AddComponent<LayoutElement>();
                share.minWidth = share.preferredWidth = 0f;   // the two halves share the line equally, whatever their words
                share.flexibleWidth = 1f;
                Flexible(Words(half.transform, "Label", LookRules.LabelSize, _muted, FontStyles.Normal, TextAlignmentOptions.TopLeft));
                Value(Words(half.transform, "Value", LookRules.BodySize, _ink, FontStyles.Normal, TextAlignmentOptions.TopRight));
            }

            // A meter: the label, a short bar of the game's round sprite, the figure.
            _meterT = Holder(templates, "Meter", true, 20f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft, false);
            Flexible(Words(_meterT.transform, "Label", LookRules.LabelSize, _muted, FontStyles.Normal, TextAlignmentOptions.Left));
            Image track = Picture(_meterT.transform, "Bar", _round, Color.Lerp(_rule, Color.white, 0.35f));
            Size(track.gameObject, LookRules.MeterWidth, LookRules.MeterHeight, false);
            Round(track, LookRules.MeterHeight);
            Image fill = Picture(track.transform, "Fill", _round, _good);
            Round(fill, LookRules.MeterHeight);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            Value(Words(_meterT.transform, "Value", LookRules.BodySize, _ink, FontStyles.Normal, TextAlignmentOptions.Right));

            // A need and whether it is met: the game's tick, or a dot in the tone that says it is not.
            _checkT = Holder(templates, "Check", true, 14f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, false);
            Image mark = Picture(_checkT.transform, "Mark", _tick ?? _round, _good);
            mark.preserveAspect = true;
            Size(mark.gameObject, LookRules.MarkSize, LookRules.MarkSize, false);
            Flexible(Words(_checkT.transform, "Text", LookRules.BodySize, _ink, FontStyles.Normal, TextAlignmentOptions.TopLeft));

            // The foot: a hairline, then the notes and the Guide's page.
            _footT = Holder(templates, "Foot", false, 8f, new RectOffset(0, 0, 14, 0), TextAnchor.UpperLeft, true);
            Rule(_footT.transform);
        }

        // A copy of the box's own line of text, set for one part of the look. Its localisation component is taken off
        // (a part is written with final words, page by page) unless the part is written by key.
        private GameObject Words(Transform parent, string name, float size, Color colour, FontStyles style, TextAlignmentOptions align, bool byKey = false)
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
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            return made;
        }

        // A value: in the font's medium weight where it has one, in bold where not (the game's own "Age: <b>31</b>").
        private void Value(GameObject made)
        {
            var text = made.GetComponent<TMP_Text>();
            if (_medium)
            {
                text.fontWeight = FontWeight.Medium;
            }
            else
            {
                text.fontStyle |= FontStyles.Bold;
            }
        }

        private static void Plain(GameObject made)
        {
            var localised = made.GetComponent<TextLocalizationComponent>();
            if (localised != null)
            {
                UnityEngine.Object.DestroyImmediate(localised);
            }
        }

        private static void NoWrap(GameObject made)
        {
            var text = made.GetComponent<TMP_Text>();
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static void Flexible(GameObject made)
        {
            var layout = made.GetComponent<LayoutElement>() ?? made.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
        }

        private static GameObject Holder(Transform parent, string name, bool across, float spacing, RectOffset padding, TextAnchor align, bool fill)
        {
            var made = new GameObject(name, typeof(RectTransform));
            made.transform.SetParent(parent, false);
            HorizontalOrVerticalLayoutGroup group = across ? (HorizontalOrVerticalLayoutGroup)made.AddComponent<HorizontalLayoutGroup>() : made.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding;
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = fill;
            group.childForceExpandHeight = false;
            return made;
        }

        private static Image Picture(Transform parent, string name, Sprite sprite, Color colour)
        {
            var made = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            made.transform.SetParent(parent, false);
            var image = made.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static void Size(GameObject made, float width, float height, bool flexible)
        {
            var layout = made.GetComponent<LayoutElement>() ?? made.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleWidth = flexible ? 1f : 0f;
        }

        // A sliced round sprite drawn as a pill of a height: its corners scaled to half the height.
        private static void Round(Image image, float height)
        {
            if (image.sprite == null || image.sprite.border.x <= 0f)
            {
                return;
            }
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, image.sprite.border.x * 2f / height);
        }

        // A hairline across a holder: a plain Image (no sprite: a solid line) in the game's rule grey.
        private void Rule(Transform parent)
        {
            Image line = Picture(parent, "Rule", null, _rule);
            var layout = line.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = LookRules.RuleHeight;
            layout.flexibleWidth = 1f;
        }
    }
}
