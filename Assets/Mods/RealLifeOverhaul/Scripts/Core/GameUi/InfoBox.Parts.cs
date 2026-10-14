using System.Diagnostics;
using System.Text;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The parts of the box's own look, as a page writes them: Header (Title), Subtitle and Chip; Section and EndSection;
    /// Row, RowPair, Meter and Check; Line in a tone, Note and GuideLink. A part is one copy of its template
    /// (InfoBox.Look.cs) and its words, written in order into the box; what follows a section's heading stands in from it
    /// until the next heading, a row of buttons, the foot or EndSection. Written on a click; nothing runs per frame. With
    /// Developer diagnostics on, each page says in Player.log how many parts it was and how long it took to write.
    /// </summary>
    public sealed partial class InfoBox
    {
        /// <summary>Over this long to write a page is said as slow in the self-check (milliseconds).</summary>
        public const double FillBudgetMs = 3.0;   // a page of the old box with as many parts took about as long: the look adds no objects of its own

        private TMP_Text _headerText;     // the page's header, while it is the last thing written
        private Transform _chips;
        private float _chipsWidth;
        private bool _inSection;          // what is written now stands in under a heading
        private bool _footMade;
        private int _capacity;            // letters a row of the box holds (LookRules.Capacity)
        private int _parts;
        private long _began;
        private readonly StringBuilder _words = new StringBuilder(160);

        private void Begin()
        {
            _headerText = null;
            _chips = null;
            _chipsWidth = 0f;
            _inSection = false;
            _footMade = false;
            _parts = 0;
            _began = Stopwatch.GetTimestamp();
        }

        private void Finish()
        {
            if (!Meta.Dev.Verbose || _began == 0)
            {
                return;
            }
            double ms = (Stopwatch.GetTimestamp() - _began) * 1000.0 / Stopwatch.Frequency;
            Logger?.Info("Info box check (" + _box.name + "): a page of " + _parts + " parts written in " + ms.ToString("0.00") + " ms, wanted under "
                         + FillBudgetMs.ToString("0.#") + " ms: " + (ms < FillBudgetMs ? "PASS" : "SLOW") + ".");
            _began = 0;
        }

        // How far what is written now stands in: a section's indent, or nothing.
        private float Indent => _inSection ? LookRules.Indent : 0f;

        // ---------------------------------------------------------------- the header

        /// <summary>
        /// The page's header: the title, with the app's colour in a short bar over it. Subtitle and Chip that follow add
        /// to it. A second header on a page starts a new one, as a sub-page's title does.
        /// </summary>
        public TMP_Text Header(string words)
        {
            EndSection();
            GameObject header = Add(_headerT);
            header.transform.Find("Accent").GetComponent<Image>().color = _accent;   // (by name: a text grows children of its own when it draws)
            var title = header.GetComponent<TMP_Text>();
            title.text = words ?? string.Empty;
            _headerText = title;
            _chips = null;
            _chipsWidth = 0f;
            return title;
        }

        /// <summary>A line under the title, muted: who and where ("BlueStone Imports, at 4 Pier").</summary>
        public TMP_Text Subtitle(string words)
        {
            TMP_Text text = Text(_subT, words, false);
            text.margin = new Vector4(Indent, 0f, 0f, 6f);
            return text;
        }

        /// <summary>
        /// A status chip at the right of the title: a short word in capitals on a pill of its tone (Plain is the app's
        /// colour). The title wraps short of the chips. Without a header the chip stands on a line of its own.
        /// </summary>
        public TMP_Text Chip(string words, CellTone tone = CellTone.Plain)
        {
            words = words ?? string.Empty;
            if (_chips == null)
            {
                Transform under = _headerText != null ? _headerText.transform : null;
                GameObject chips = Add(_chipsT, under);
                if (under != null)
                {
                    var rect = (RectTransform)chips.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                    rect.anchoredPosition = new Vector2(0f, -(LookRules.AccentHeight + 16f) - 11f);   // level with the title's first line
                }
                else
                {
                    chips.GetComponent<LayoutElement>().ignoreLayout = false;
                    chips.GetComponent<ContentSizeFitter>().enabled = false;
                    chips.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                }
                _chips = chips.transform;
            }
            GameObject chip = Add(_chipT, _chips);
            Color paint = Paint(tone);
            chip.GetComponent<Image>().color = new Color(paint.r, paint.g, paint.b, 0.16f);
            var text = chip.transform.Find("Text").GetComponent<TMP_Text>();
            text.color = tone == CellTone.Plain ? _accent : Ink(tone);
            text.text = words;
            if (_headerText != null && _chips.parent == _headerText.transform)
            {
                _chipsWidth += LookRules.ChipWidth(words) + (_chipsWidth > 0f ? 10f : 0f);
                Vector4 margin = _headerText.margin;
                margin.z = _chipsWidth + 24f;
                _headerText.margin = margin;
            }
            return text;
        }

        // ---------------------------------------------------------------- sections

        /// <summary>
        /// A section: its heading (a locale key) in small capitals over a hairline, with room above it. What follows stands
        /// in under it until the next section, a row of buttons, the foot or EndSection.
        /// </summary>
        public Transform Section(string key)
        {
            GameObject heading = OpenSection();
            UiClone.Key(heading.transform, key);
            return heading.transform;
        }

        /// <summary>The same with the heading in final words (a name and a figure).</summary>
        public Transform SectionWords(string words)
        {
            GameObject heading = OpenSection();
            UiClone.Words(heading.transform, words);
            return heading.transform;
        }

        private GameObject OpenSection()
        {
            EndSection();
            GameObject heading = Add(_headingT);
            _inSection = true;
            return heading;
        }

        /// <summary>Back to the box itself: what follows does not stand in under the last section's heading.</summary>
        public void EndSection()
        {
            _inSection = false;
            _headerText = null;
        }

        // ---------------------------------------------------------------- rows

        /// <summary>
        /// A row: the label (a locale key; a colon it ends in is left off) at the left in the muted grey, the value at the
        /// right in the body's colour, in a tone if given. A row whose label and value do not fit one line has the value
        /// under its label. Returns the row's text.
        /// </summary>
        public TMP_Text Row(string labelKey, string value, CellTone tone = CellTone.Plain)
        {
            return RowWords(labelKey.GetLocalization(), value, tone);
        }

        /// <summary>The same with the label in final words.</summary>
        public TMP_Text RowWords(string label, string value)
        {
            return RowWords(label, value, CellTone.Plain);
        }

        /// <summary>The same with the label in final words, in a tone.</summary>
        public TMP_Text RowWords(string label, string value, CellTone tone)
        {
            label = LookRules.Label(label);
            value = value ?? string.Empty;
            TMP_Text text = Text(_bodyT, RowMarkup(label, value, tone, LookRules.Stack(label, value, _capacity)));
            text.margin = new Vector4(Indent, 0f, 0f, 0f);
            return text;
        }

        // A row's words in one text: the label in its size and grey, then the value; beside it, the value is set on the
        // label's own line (a line height of nought) and aligned right; stacked, it is the next line.
        private string RowMarkup(string label, string value, CellTone tone, bool stacked)
        {
            _words.Length = 0;
            _words.Append(_labelOpen).Append(label).Append("</color></size>");
            _words.Append(stacked ? "\n" : "<line-height=0>\n</line-height><align=right>");
            _words.Append(_toneOpen[(int)tone]).Append(_valueOpen).Append(value).Append(_valueClose);
            if (tone != CellTone.Plain)
            {
                _words.Append("</color>");
            }
            return _words.ToString();
        }

        /// <summary>
        /// Two rows side by side, each in half the line: what the old pages wrote as "Label value  Label value". Where
        /// either does not fit its half, they are two rows, one under the other.
        /// </summary>
        public void RowPair(string labelKey, string value, string labelKey2, string value2)
        {
            string label = LookRules.Label(labelKey.GetLocalization()), label2 = LookRules.Label(labelKey2.GetLocalization());
            value = value ?? string.Empty;
            value2 = value2 ?? string.Empty;
            int half = _capacity / 2 - 2;
            if (_capacity <= 0 || LookRules.Stack(label, value, half) || LookRules.Stack(label2, value2, half))
            {
                RowWords(label, value, CellTone.Plain);
                RowWords(label2, value2, CellTone.Plain);
                return;
            }
            GameObject pair = Add(_pairT);
            var group = pair.GetComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset((int)Indent, 0, 0, 0);
            pair.transform.GetChild(0).GetComponent<TMP_Text>().text = RowMarkup(label, value, CellTone.Plain, false);
            pair.transform.GetChild(1).GetComponent<TMP_Text>().text = RowMarkup(label2, value2, CellTone.Plain, false);
            _headerText = null;
        }

        /// <summary>
        /// A meter: a row of its label and figure ("11 of 10", "about 45%"), with a short bar under the figure filled to a
        /// share (0 to 1) in a tone (Plain fills it green). Returns the row's text.
        /// </summary>
        public TMP_Text Meter(string labelKey, float share, string figure, CellTone tone)
        {
            return MeterWords(labelKey.GetLocalization(), share, figure, tone);
        }

        /// <summary>The same with the label in final words.</summary>
        public TMP_Text MeterWords(string label, float share, string figure, CellTone tone)
        {
            TMP_Text text = Text(_meterT, RowMarkup(LookRules.Label(label), figure ?? string.Empty, tone, false));
            Vector4 margin = text.margin;
            margin.x = Indent;
            text.margin = margin;
            var fill = (RectTransform)text.transform.Find("Bar/Fill");
            float f = LookRules.Clamp(share);
            fill.anchorMax = new Vector2(f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var paint = fill.GetComponent<Image>();
            paint.color = Paint(tone == CellTone.Plain ? CellTone.Green : tone);
            paint.enabled = f > 0f;
            return text;
        }

        /// <summary>A need and whether it is met: the game's tick in green before it, or a red dot and the words in red.</summary>
        public TMP_Text Check(string words, bool met)
        {
            TMP_Text text = Text(_checkT, words);
            float indent = Indent;
            Vector4 margin = text.margin;
            margin.x = indent + LookRules.MarkSize + 14f;
            text.margin = margin;
            var mark = text.transform.Find("Mark").GetComponent<Image>();
            var rect = (RectTransform)mark.transform;
            rect.anchoredPosition = new Vector2(indent, rect.anchoredPosition.y);
            if (met && _tick != null)
            {
                mark.color = Ink(CellTone.Green);
                return text;
            }
            // no tick: a dot of the round sprite, half the size, in the tone that says whether it is met
            mark.sprite = _round;
            mark.color = met ? Ink(CellTone.Green) : _bad;
            rect.sizeDelta = new Vector2(LookRules.MarkSize * 0.5f, LookRules.MarkSize * 0.5f);
            rect.anchoredPosition = new Vector2(indent + LookRules.MarkSize * 0.25f, -LookRules.MarkSize * 0.35f);
            if (!met)
            {
                text.color = _bad;
            }
            return text;
        }

        // ---------------------------------------------------------------- sentences and notes

        /// <summary>A sentence in a tone: green for good news, orange for a warning, red for what is wrong or refused.</summary>
        public TMP_Text Line(string words, CellTone tone)
        {
            TMP_Text text = Text(_bodyT, words);
            text.margin = new Vector4(Indent, 0f, 0f, 0f);
            if (tone != CellTone.Plain)
            {
                text.color = Ink(tone);
            }
            return text;
        }

        /// <summary>A note: a smaller, muted sentence (what a figure means, what to do next).</summary>
        public TMP_Text Note(string words)
        {
            TMP_Text text = Text(_noteT, words);
            text.margin = new Vector4(Indent, 0f, 0f, 0f);
            return text;
        }

        /// <summary>
        /// The Guide's page that explains the app, at the foot of the page under a hairline, small and muted:
        /// "Guide: Professional jobs". A note written after it stands with it.
        /// </summary>
        public TMP_Text GuideLink(string pageWords)
        {
            EndSection();
            string label = "reallife_guide_app".GetLocalization();
            if (_footMade)
            {
                return Note(label + LookRules.Colon(label) + "<b>" + pageWords + "</b>");
            }
            _footMade = true;
            return Text(_footT, label + LookRules.Colon(label) + "<b>" + pageWords + "</b>");
        }

        // A copy of one of the text templates with its words, in the box. Anything but a subtitle ends the header.
        private TMP_Text Text(GameObject template, string words, bool endsHeader = true)
        {
            if (endsHeader)
            {
                _headerText = null;
            }
            GameObject made = Add(template);
            var text = made.GetComponent<TMP_Text>();
            text.text = words ?? string.Empty;
            return text;
        }
    }
}
