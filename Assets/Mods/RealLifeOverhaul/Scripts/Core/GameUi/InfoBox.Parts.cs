using System.Diagnostics;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The parts of the box's own look, as a page writes them: Header (Title), Subtitle and Chip; Section and EndSection;
    /// Row, Meter and Check; Line in a tone, Note and GuideLink. A part is one copy of its template (InfoBox.Look.cs)
    /// and its words, written where the page is being written: the rows of the section last opened, or the box. A row
    /// of buttons closes the section. Written on a click; nothing runs per frame. With Developer diagnostics on, each
    /// page says in Player.log how many parts it was and how long it took to write.
    /// </summary>
    public sealed partial class InfoBox
    {
        /// <summary>Over this long to write a page is said as a fault in the self-check (milliseconds).</summary>
        public const double FillBudgetMs = 4.0;   // a big page (40 parts) writes in about 1.5 ms on the build machine's Unity, measured offline

        private Transform _into;          // where the next part goes: the open section's rows, or the box
        private Transform _header;        // the page's header, while nothing but its subtitle and chips follow it
        private Transform _titleRow, _chips;
        private Transform _foot;
        private int _capacity;            // letters a row of the box holds (LookRules.Capacity)
        private int _parts;
        private long _began;

        private void Begin()
        {
            _into = _box;
            _header = _titleRow = _chips = _foot = null;
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

        // ---------------------------------------------------------------- the header

        /// <summary>
        /// The page's header: the app's colour in a short bar, then the title. Subtitle and Chip that follow add to it.
        /// A second header on a page starts a new one, as a sub-page's title does.
        /// </summary>
        public TMP_Text Header(string words)
        {
            EndSection();
            GameObject header = Add(_headerT, _box);
            Transform accent = header.transform.Find("Accent");
            if (accent != null)
            {
                accent.GetComponent<Image>().color = _accent;
            }
            _header = header.transform;
            _titleRow = header.transform.Find("TitleRow");
            _chips = null;
            Transform title = _titleRow.Find("Title");
            UiClone.Words(title, words);
            return title.GetComponent<TMP_Text>();
        }

        /// <summary>A line under the title, muted: who and where ("BlueStone Imports, at 4 Pier"). Without a header, a muted line where the page is.</summary>
        public TMP_Text Subtitle(string words)
        {
            return Text(_subT, words, _header);
        }

        /// <summary>
        /// A status chip beside the title: a short word in capitals on a pill of its tone (Plain is the app's colour).
        /// Without a header, the chip stands on a line of its own where the page is.
        /// </summary>
        public TMP_Text Chip(string words, CellTone tone = CellTone.Plain)
        {
            if (_chips == null)
            {
                _chips = Add(_chipsT, _titleRow != null ? _titleRow : (_into ?? _box)).transform;
            }
            GameObject chip = Add(_chipT, _chips);
            Color paint = Paint(tone);
            chip.GetComponent<Image>().color = new Color(paint.r, paint.g, paint.b, 0.16f);
            var text = chip.transform.Find("Text").GetComponent<TMP_Text>();
            text.color = tone == CellTone.Plain ? _accent : Ink(tone);
            text.text = words ?? string.Empty;
            return text;
        }

        // ---------------------------------------------------------------- sections

        /// <summary>
        /// A section: its heading (a locale key) in small capitals over a hairline, with room above it. Every part that
        /// follows goes into it, indented, until the next section, a row of buttons, the foot or EndSection.
        /// </summary>
        public Transform Section(string key)
        {
            Transform section = OpenSection();
            UiClone.Key(section.Find("Heading"), key);
            return section;
        }

        /// <summary>The same with the heading in final words (a name and a figure).</summary>
        public Transform SectionWords(string words)
        {
            Transform section = OpenSection();
            UiClone.Words(section.Find("Heading"), words);
            return section;
        }

        private Transform OpenSection()
        {
            EndSection();
            GameObject section = Add(_sectionT, _box);
            _into = section.transform.Find("Rows");
            return section.transform;
        }

        /// <summary>Back to the box itself: what follows is not part of the last section.</summary>
        public void EndSection()
        {
            _into = _box;
            _header = null;
        }

        // ---------------------------------------------------------------- rows

        /// <summary>
        /// A row: the label (a locale key; a colon it ends in is left off) at the left in the muted grey, the value at the
        /// right in the body's colour, in a tone if given. A row whose label and value do not fit one line has the
        /// value under its label. Returns the value's text.
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
            _header = null;
            GameObject row = Add(LookRules.Stack(label, value, _capacity) ? _stackT : _rowT);
            Transform t = row.transform;
            t.GetChild(0).GetComponent<TMP_Text>().text = label;
            var shown = t.GetChild(1).GetComponent<TMP_Text>();
            shown.text = value;
            if (tone != CellTone.Plain)
            {
                shown.color = Ink(tone);
            }
            return shown;
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
            _header = null;
            Transform pair = Add(_pairT).transform;
            Half(pair.GetChild(0), label, value);
            Half(pair.GetChild(1), label2, value2);
        }

        private static void Half(Transform half, string label, string value)
        {
            half.GetChild(0).GetComponent<TMP_Text>().text = label;
            half.GetChild(1).GetComponent<TMP_Text>().text = value;
        }

        /// <summary>
        /// A meter: the label at the left, a short bar filled to a share (0 to 1) in a tone, and the figure at the right
        /// ("11 of 10", "45%"). Returns the figure's text.
        /// </summary>
        public TMP_Text Meter(string labelKey, float share, string figure, CellTone tone)
        {
            return MeterWords(labelKey.GetLocalization(), share, figure, tone);
        }

        /// <summary>The same with the label in final words.</summary>
        public TMP_Text MeterWords(string label, float share, string figure, CellTone tone)
        {
            _header = null;
            GameObject meter = Add(_meterT);
            Transform t = meter.transform;
            t.GetChild(0).GetComponent<TMP_Text>().text = LookRules.Label(label);
            Transform bar = t.GetChild(1);
            var fill = (RectTransform)bar.GetChild(0);
            float f = LookRules.Clamp(share);
            fill.anchorMax = new Vector2(f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var paint = fill.GetComponent<Image>();
            paint.color = Paint(tone == CellTone.Plain ? CellTone.Green : tone);
            paint.enabled = f > 0f;
            var shown = t.GetChild(2).GetComponent<TMP_Text>();
            shown.text = figure ?? string.Empty;
            if (tone != CellTone.Plain)
            {
                shown.color = Ink(tone);
            }
            return shown;
        }

        /// <summary>A need and whether it is met: the game's tick in green before it, or a red dot and the words in red.</summary>
        public TMP_Text Check(string words, bool met)
        {
            _header = null;
            GameObject check = Add(_checkT);
            Transform t = check.transform;
            var mark = t.GetChild(0).GetComponent<Image>();
            var text = t.GetChild(1).GetComponent<TMP_Text>();
            text.text = words ?? string.Empty;
            if (met && _tick != null)
            {
                mark.sprite = _tick;
                mark.color = Ink(CellTone.Green);
            }
            else
            {
                // no tick: a dot of the round sprite, small, in the tone that says whether it is met
                mark.sprite = _round;
                mark.color = met ? Ink(CellTone.Green) : _bad;
                var layout = mark.GetComponent<LayoutElement>();
                layout.minWidth = layout.preferredWidth = LookRules.MarkSize;
                layout.minHeight = layout.preferredHeight = LookRules.MarkSize * 0.5f;
                if (!met)
                {
                    text.color = _bad;
                }
            }
            return text;
        }

        // ---------------------------------------------------------------- sentences and notes

        /// <summary>A sentence in a tone: green for good news, orange for a warning, red for what is wrong or refused.</summary>
        public TMP_Text Line(string words, CellTone tone)
        {
            TMP_Text text = Text(_bodyT, words, null);
            if (tone != CellTone.Plain)
            {
                text.color = Ink(tone);
            }
            return text;
        }

        /// <summary>A note: a smaller, muted sentence where the page is (what a figure means, what to do next).</summary>
        public TMP_Text Note(string words)
        {
            return Text(_noteT, words, null);
        }

        /// <summary>
        /// The Guide's page that explains the app, at the foot of the page under a hairline, small and muted:
        /// "Guide: Professional jobs". Notes written after it go into the foot as well.
        /// </summary>
        public TMP_Text GuideLink(string pageWords)
        {
            if (_foot == null)
            {
                _foot = Add(_footT, _box).transform;
            }
            _into = _foot;
            _header = null;
            string label = "reallife_guide_app".GetLocalization();
            return Text(_noteT, label + LookRules.Colon(label) + "<b>" + pageWords + "</b>", _foot);
        }

        // A copy of one of the text templates with its words, under a parent or where the page is being written.
        private TMP_Text Text(GameObject template, string words, Transform parent)
        {
            if (parent == null)
            {
                _header = null;
            }
            GameObject made = Add(template, parent);
            var text = made.GetComponent<TMP_Text>();
            text.text = words ?? string.Empty;
            return text;
        }
    }
}
