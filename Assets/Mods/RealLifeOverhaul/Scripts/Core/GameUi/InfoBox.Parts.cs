using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The box's own parts as a page writes them (Header, Section, Row, Meter, Check, Note and the rest), each a copy of its
    /// template; after a section's heading the parts stand in until the next. With diagnostics on, each page logs the box's
    /// own time (<see cref="BoxClock"/>, opened by <see cref="Timed"/>).
    /// </summary>
    public sealed partial class InfoBox
    {
        /// <summary>The box's own work on a page over this is said as slow in the self-check.</summary>
        public const double FillBudgetMs = 3.0;   // milliseconds: a page of forty parts

        private TMP_Text _headerText;     // the page's header, while it is the last thing written
        private Transform _chips;
        private float _chipsWidth;
        private bool _inSection;          // what is written now stands in under a heading
        private bool _footMade;
        private int _capacity;            // letters a row of the box holds (LookRules.Capacity)
        private int _parts;
        private bool _begun;
        private readonly BoxClock _clock = new BoxClock();
        private static InfoBox _writing;   // the box whose page is being written, Clear to Done: a row's buttons count to it
        private readonly StringBuilder _words = new StringBuilder(160);

        /// <summary>One of the box's own calls, timed while it runs; a struct, so nothing is allocated.</summary>
        private struct Own : IDisposable
        {
            private readonly BoxClock _clock;

            public Own(BoxClock clock)
            {
                _clock = clock;
                clock?.Enter(Stopwatch.GetTimestamp());
            }

            public void Dispose()
            {
                _clock?.Leave(Stopwatch.GetTimestamp());
            }
        }

        private Own Timed()
        {
            return new Own(_clock);
        }

        // For a call with no box of its own (InfoBox.Button on a row): nothing timed while no page is being written.
        private static Own TimedWriting()
        {
            return new Own(_writing != null ? _writing._clock : null);
        }

        private void Begin()
        {
            _headerText = null;
            _chips = null;
            _chipsWidth = 0f;
            _inSection = false;
            _footMade = false;
            _parts = 0;
            _begun = true;
            _writing = this;
            _clock.Begin(Stopwatch.GetTimestamp());
        }

        private void Finish()
        {
            if (_writing == this)
            {
                _writing = null;
            }
            if (!Meta.Dev.Verbose || !_begun)
            {
                return;
            }
            long now = Stopwatch.GetTimestamp();
            double ms = _clock.Own(now) * 1000.0 / Stopwatch.Frequency, page = _clock.Page(now) * 1000.0 / Stopwatch.Frequency;
            Logger?.Info("Info box check (" + _box.name + "): a page of " + _parts + " parts written in " + ms.ToString("0.00", CultureInfo.InvariantCulture) + " ms of the box's own work ("
                         + page.ToString("0.00", CultureInfo.InvariantCulture) + " ms from Clear to Done with the app's own work between), wanted under "
                         + FillBudgetMs.ToString("0.#", CultureInfo.InvariantCulture) + " ms: "
                         + (ms < FillBudgetMs ? "PASS" : "SLOW") + ".");
            _begun = false;
        }

        // How far what is written now stands in: a section's indent, or nothing.
        private float Indent => _inSection ? LookRules.Indent : 0f;

        /// <summary>The page's header: the title under a short bar of the app's colour, to which Subtitle and Chip add; a second one starts anew.</summary>
        public TMP_Text Header(string words)
        {
            using var own = Timed();
            EndSection();
            GameObject header = Add(_headerT);
            header.transform.Find("Accent").GetComponent<Image>().color = _accent;   // by name: a text grows children of its own when it draws
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
            using var own = Timed();
            TMP_Text text = Text(_subT, words, false);
            text.margin = new Vector4(Indent, 0f, 0f, 6f);
            return text;
        }

        /// <summary>A status chip right of the title, a short word on a pill of its tone (Plain: the app's colour); a line of its own without a header.</summary>
        public TMP_Text Chip(string words, CellTone tone = CellTone.Plain)
        {
            using var own = Timed();
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

        /// <summary>A section: its heading (a locale key) in small capitals over a hairline; what follows stands in under it.</summary>
        public Transform Section(string key)
        {
            using var own = Timed();
            GameObject heading = OpenSection();
            UiClone.Key(heading.transform, key);
            return heading.transform;
        }

        /// <summary>The same with the heading in final words.</summary>
        public Transform SectionWords(string words)
        {
            using var own = Timed();
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

        /// <summary>What follows no longer stands in under the last heading.</summary>
        public void EndSection()
        {
            _inSection = false;
            _headerText = null;
        }

        /// <summary>A row: the label (a locale key, colon dropped) left in grey, the value right, under the label when the two do not fit one line.</summary>
        public TMP_Text Row(string labelKey, string value, CellTone tone = CellTone.Plain)
        {
            using var own = Timed();
            return RowWords(labelKey.GetLocalization(), value, tone);
        }

        /// <summary>The same with the label in final words.</summary>
        public TMP_Text RowWords(string label, string value)
        {
            using var own = Timed();
            return RowWords(label, value, CellTone.Plain);
        }

        /// <summary>The same with the label in final words, in a tone.</summary>
        public TMP_Text RowWords(string label, string value, CellTone tone)
        {
            using var own = Timed();
            label = LookRules.Label(label);
            value = value ?? string.Empty;
            TMP_Text text = Text(_bodyT, RowMarkup(label, value, tone, LookRules.Stack(label, value, _capacity)));
            text.margin = new Vector4(Indent, 0f, 0f, 0f);
            return text;
        }

        // Beside: the value on the label's own line (a line height of nought), aligned right. Stacked: the next line.
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

        /// <summary>Two rows side by side in half the line each; one under the other where either does not fit its half.</summary>
        public void RowPair(string labelKey, string value, string labelKey2, string value2)
        {
            using var own = Timed();
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

        /// <summary>A meter: a row of its label and figure over a short bar filled to <paramref name="share"/> (0 to 1) in a tone, Plain green.</summary>
        public TMP_Text Meter(string labelKey, float share, string figure, CellTone tone)
        {
            using var own = Timed();
            return MeterWords(labelKey.GetLocalization(), share, figure, tone);
        }

        /// <summary>The same with the label in final words.</summary>
        public TMP_Text MeterWords(string label, float share, string figure, CellTone tone)
        {
            using var own = Timed();
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
            using var own = Timed();
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
            // No tick: a dot of the round sprite, half the size.
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

        /// <summary>A sentence in a tone: green for good news, orange for a warning, red for what is wrong or refused.</summary>
        public TMP_Text Line(string words, CellTone tone)
        {
            using var own = Timed();
            TMP_Text text = Text(_bodyT, words);
            text.margin = new Vector4(Indent, 0f, 0f, 0f);
            if (tone != CellTone.Plain)
            {
                text.color = Ink(tone);
            }
            return text;
        }

        /// <summary>A smaller, muted sentence: what a figure means, what to do next.</summary>
        public TMP_Text Note(string words)
        {
            using var own = Timed();
            TMP_Text text = Text(_noteT, words);
            text.margin = new Vector4(Indent, 0f, 0f, 0f);
            return text;
        }

        /// <summary>The Guide's page for the app at the foot under a hairline ("Guide: Professional jobs"); a note written after joins it.</summary>
        public TMP_Text GuideLink(string pageWords)
        {
            using var own = Timed();
            EndSection();
            string label = "reallife_guide_app".GetLocalization();
            if (_footMade)
            {
                return Note(label + LookRules.Colon(label) + "<b>" + pageWords + "</b>");
            }
            _footMade = true;
            return Text(_footT, label + LookRules.Colon(label) + "<b>" + pageWords + "</b>");
        }

        // Into the box. Anything but a subtitle ends the header.
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
