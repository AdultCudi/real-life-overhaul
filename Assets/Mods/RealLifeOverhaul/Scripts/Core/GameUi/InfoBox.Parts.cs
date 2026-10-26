using System;
using System.Diagnostics;
using System.Globalization;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The box's own parts as a page writes them (Header, Section, Row, Entry, Meter, Check, Note and the rest), each a copy of its
    /// template; after a section's heading the parts stand in its card until the next. Where a part stands and how tall it is
    /// is the layout's (InfoBox.Frame, InfoBox.Look): no part is given a position or a height here. With diagnostics on, each page
    /// logs the box's own time (<see cref="BoxClock"/>, opened by <see cref="Timed"/>).
    /// </summary>
    public sealed partial class InfoBox
    {
        /// <summary>The box's own work on a page over this is said as slow in the self-check.</summary>
        public const double FillBudgetMs = 3.0;   // milliseconds: a page of forty parts

        private TMP_Text _headerText;     // the page's header, while it is the last thing written
        private Transform _headerRow;     // the row the header's title and chips stand in
        private Transform _chips;
        private float _chipsWidth;
        private bool _chipsBelow;
        private bool _footMade;
        private int _parts;
        private bool _begun;
        private readonly BoxClock _clock = new BoxClock();
        private static InfoBox _writing;   // the box whose page is being written, Clear to Done: a row's buttons count to it

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
            _headerRow = null;
            _chips = null;
            _chipsWidth = 0f;
            _chipsBelow = false;
            _group = null;
            _bodyStarted = false;
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

        /// <summary>The page's header: the title under a short bar of the app's colour, to which Subtitle and Chip add; a second one starts anew.</summary>
        public TMP_Text Header(string words)
        {
            using var own = Timed();
            EndSection();
            GameObject header = Add(_headerT, Head());
            header.transform.Find("Accent").GetComponent<Image>().color = _accent;   // by name: a text grows children of its own when it draws
            var title = header.transform.Find("Title").GetComponent<TMP_Text>();
            title.text = words ?? string.Empty;
            if (_pageKey == null)
            {
                _pageKey = title.text;   // the same title is the same page written again: its scroll is kept
            }
            _headerText = title;
            _headerRow = header.transform;
            _chips = null;
            _chipsWidth = 0f;
            _chipsBelow = false;
            return title;
        }

        /// <summary>A line under the title, muted: who and where ("BlueStone Imports, at 4 Pier").</summary>
        public TMP_Text Subtitle(string words)
        {
            using var own = Timed();
            return Text(_subT, words, Head(), false);
        }

        /// <summary>
        /// A status chip right of the title, a short word on a pill of its tone (Plain: the app's colour); under the title when it
        /// would squeeze it; a line of its own without a header.
        /// </summary>
        public TMP_Text Chip(string words, CellTone tone = CellTone.Plain)
        {
            using var own = Timed();
            words = words ?? string.Empty;
            if (_chips == null)
            {
                if (_headerRow != null)
                {
                    _chips = Add(_chipsT, _headerRow).transform;
                }
                else
                {
                    _chips = Add(_chipsT, Body()).transform;
                    var line = _chips.GetComponent<HorizontalLayoutGroup>();
                    line.childAlignment = TextAnchor.UpperLeft;
                    line.padding = new RectOffset(0, 0, 0, 0);
                    _chipsBelow = true;
                }
            }
            GameObject chip = Add(_chipT, _chips);
            Color paint = Paint(tone);
            chip.GetComponent<Image>().color = new Color(paint.r, paint.g, paint.b, 0.16f);
            var text = chip.transform.Find("Text").GetComponent<TMP_Text>();
            text.color = tone == CellTone.Plain ? _accent : Ink(tone);
            text.text = words;
            if (!_chipsBelow)
            {
                _chipsWidth += text.preferredWidth + 36f + (_chipsWidth > 0f ? 10f : 0f);   // the word, the pill's padding, the gap
                var size = _chips.GetComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = _chipsWidth;   // chips keep their width; the title gives way
                if (LeftPaneRules.ChipsBelow(_chipsWidth, Room()))
                {
                    ChipsUnder();
                }
            }
            return text;
        }

        // Too many or too wide to stand at the title's right: a line of their own under it, before the subtitle.
        private void ChipsUnder()
        {
            Transform row = _headerRow;
            _chipsBelow = true;
            if (row == null || row.parent == null)
            {
                return;
            }
            _chips.SetParent(row.parent, false);
            _chips.SetSiblingIndex(row.GetSiblingIndex() + 1);
            var size = _chips.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = -1f;
            var line = _chips.GetComponent<HorizontalLayoutGroup>();
            line.childAlignment = TextAnchor.UpperLeft;
            line.padding = new RectOffset(0, 0, 4, 0);
        }

        /// <summary>A section: its heading (a locale key) in small capitals over a hairline, in a card; what follows stands in the card until the next section.</summary>
        public Transform Section(string key)
        {
            using var own = Timed();
            GameObject card = OpenSection();
            UiClone.Key(card.transform.Find("Heading"), key);
            return card.transform;
        }

        /// <summary>The same with the heading in final words.</summary>
        public Transform SectionWords(string words)
        {
            using var own = Timed();
            GameObject card = OpenSection();
            UiClone.Words(card.transform.Find("Heading"), words);
            return card.transform;
        }

        private GameObject OpenSection()
        {
            EndSection();
            _bodyStarted = true;
            GameObject card = Add(_groupT, _content);
            _group = card.transform;
            return card;
        }

        /// <summary>What follows no longer stands in the last section's card.</summary>
        public void EndSection()
        {
            _group = null;
            _headerText = null;
            _headerRow = null;
        }

        /// <summary>A fact: the label (a locale key, colon dropped) at the left in grey, the value at the right, on one baseline; the value wraps under itself, not under the label.</summary>
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

        /// <summary>The same with the label in final words, in a tone. Returns the value's text.</summary>
        public TMP_Text RowWords(string label, string value, CellTone tone)
        {
            using var own = Timed();
            GameObject row = Add(_rowT);
            return Fill(row.transform, LookRules.Label(label), value ?? string.Empty, tone, Room(), out float labelWidth, out float valueWidth);
        }

        // Writes a fact's two texts and gives the label the width it needs (LeftPaneRules.LabelWidth) in the room: measured from
        // the texts themselves, in whatever language, not guessed from letters. Returns the value's text.
        private TMP_Text Fill(Transform row, string label, string value, CellTone tone, float room, out float labelWidth, out float valueWidth)
        {
            var name = row.GetChild(0).GetComponent<TMP_Text>();
            var figure = row.GetChild(1).GetComponent<TMP_Text>();
            bool named = label.Length > 0;
            name.gameObject.SetActive(named);   // no label, no gap before the value
            name.text = label;
            figure.text = value;
            figure.color = Ink(tone);
            labelWidth = named ? name.preferredWidth + LeftPaneRules.Slack : 0f;
            valueWidth = value.Length > 0 ? figure.preferredWidth + LeftPaneRules.Slack : 0f;
            var size = name.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = LeftPaneRules.LabelWidth(labelWidth, valueWidth, room, LeftPaneRules.RowGap);
            return figure;
        }

        /// <summary>Two facts side by side in half the line each; one under the other where either does not fit its half on one line.</summary>
        public void RowPair(string labelKey, string value, string labelKey2, string value2)
        {
            using var own = Timed();
            RowPairWords(labelKey.GetLocalization(), value, labelKey2.GetLocalization(), value2);
        }

        /// <summary>The same with both labels in final words.</summary>
        public void RowPairWords(string label, string value, string label2, string value2)
        {
            using var own = Timed();
            label = LookRules.Label(label);
            label2 = LookRules.Label(label2);
            value = value ?? string.Empty;
            value2 = value2 ?? string.Empty;
            float half = LeftPaneRules.Half(Room());
            GameObject pair = Add(_pairT);
            Fill(pair.transform.GetChild(0), label, value, CellTone.Plain, half, out float labelA, out float valueA);
            Fill(pair.transform.GetChild(1), label2, value2, CellTone.Plain, half, out float labelB, out float valueB);
            if (LeftPaneRules.OneLine(labelA, valueA, half, LeftPaneRules.RowGap) && LeftPaneRules.OneLine(labelB, valueB, half, LeftPaneRules.RowGap))
            {
                _headerText = null;
                return;
            }
            Unwrite(pair, _pairT);
            RowWords(label, value, CellTone.Plain);
            RowWords(label2, value2, CellTone.Plain);
        }

        // The part just written is taken back: laid aside as if it had not been written.
        private void Unwrite(GameObject part, GameObject template)
        {
            int last = _live.Count - 1;
            if (last >= 0 && _live[last] == part)
            {
                _live.RemoveAt(last);
                _liveFrom.RemoveAt(last);
            }
            _parts--;
            Park(part, template);
        }

        /// <summary>
        /// A list entry, a rung of a ladder: its name and its figure on one line, what it asks under them in small muted words;
        /// <paramref name="current"/> is the one the player is on or aims at, on a tint of the app's colour with a mark at its left.
        /// Returns the figure's text.
        /// </summary>
        public TMP_Text Entry(string title, string figure, string detail, CellTone tone = CellTone.Plain, bool current = false)
        {
            using var own = Timed();
            GameObject entry = Add(_entryT);
            float room = Room() - LeftPaneRules.EntryPadLeft - LeftPaneRules.EntryPadRight;
            TMP_Text value = Fill(entry.transform.GetChild(0), LookRules.Label(title), figure ?? string.Empty, tone, room, out float titleWidth, out float figureWidth);
            var note = entry.transform.GetChild(1).GetComponent<TMP_Text>();
            bool asks = !string.IsNullOrEmpty(detail);
            note.gameObject.SetActive(asks);
            if (asks)
            {
                note.text = detail;
            }
            entry.GetComponent<Image>().color = current ? new Color(_accent.r, _accent.g, _accent.b, 0.12f) : Color.clear;
            entry.transform.GetChild(2).gameObject.SetActive(current);
            _headerText = null;
            return value;
        }

        /// <summary>A meter: a fact of its label and figure over a bar the card's width, filled to <paramref name="share"/> (0 to 1) in a tone, Plain green.</summary>
        public TMP_Text Meter(string labelKey, float share, string figure, CellTone tone)
        {
            using var own = Timed();
            return MeterWords(labelKey.GetLocalization(), share, figure, tone);
        }

        /// <summary>The same with the label in final words.</summary>
        public TMP_Text MeterWords(string label, float share, string figure, CellTone tone)
        {
            using var own = Timed();
            GameObject meter = Add(_meterT);
            TMP_Text value = Fill(meter.transform.GetChild(0), LookRules.Label(label), figure ?? string.Empty, tone, Room(), out float labelWidth, out float valueWidth);
            var fill = (RectTransform)meter.transform.Find("Track/Fill");
            float f = LookRules.Clamp(share);
            fill.anchorMax = new Vector2(f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var paint = fill.GetComponent<Image>();
            paint.color = Paint(tone == CellTone.Plain ? CellTone.Green : tone);
            paint.enabled = f > 0f;
            _headerText = null;
            return value;
        }

        /// <summary>A need and whether it is met: the game's tick in green before it, or a red dot and the words in red.</summary>
        public TMP_Text Check(string words, bool met)
        {
            using var own = Timed();
            GameObject check = Add(_checkT);
            var text = check.transform.Find("Text").GetComponent<TMP_Text>();
            text.text = words ?? string.Empty;
            var mark = check.transform.Find("Mark").GetComponent<Image>();
            _headerText = null;
            if (met && _tick != null)
            {
                mark.color = Ink(CellTone.Green);
                return text;
            }
            // No tick: a dot of the round sprite, half the size.
            mark.sprite = _round;
            mark.color = met ? Ink(CellTone.Green) : _bad;
            mark.rectTransform.localScale = new Vector3(0.5f, 0.5f, 1f);
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
            return Text(_noteT, words);
        }

        /// <summary>The Guide's page for the app at the foot of the card ("Guide: Professional jobs"); a note written after joins the page.</summary>
        public TMP_Text GuideLink(string pageWords)
        {
            using var own = Timed();
            EndSection();
            string label = "reallife_guide_app".GetLocalization();
            string words = label + LookRules.Colon(label) + "<b>" + pageWords + "</b>";
            if (_footMade)
            {
                return Note(words);
            }
            _footMade = true;
            return Text(_guideT, words, _foot);
        }

        // Into the page (or the place given). Anything but a subtitle ends the header.
        private TMP_Text Text(GameObject template, string words, Transform parent = null, bool endsHeader = true)
        {
            if (endsHeader)
            {
                _headerText = null;
                _headerRow = null;
            }
            GameObject made = Add(template, parent);
            var text = made.GetComponent<TMP_Text>();
            text.text = words ?? string.Empty;
            return text;
        }
    }
}
