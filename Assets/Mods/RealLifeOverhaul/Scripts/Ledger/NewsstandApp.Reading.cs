using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>Newsstand's app: the reading page, an edition set as one column of newsprint.</summary>
    public sealed partial class NewsstandApp
    {
        // One title's edition as written for the phone, kept.
        private sealed class Written
        {
            public int Title, Day, Language;
            public LedgerEdition Edition;
            public readonly List<NewsBlock> Blocks = new List<NewsBlock>(40);
        }

        private void WriteReading(InfoBox box, NewsstandState s)
        {
            int today = _director.Today;
            if (_title < 0 || _title >= NewsstandCatalogue.Titles || !Readable(s, _title, today))
            {
                _title = FirstReadable(s, today);
                if (_title >= 0)
                {
                    Readable(s, _title, today);
                }
            }
            if (_title < 0)
            {
                UiClone.Words(_shell.Header, LedgerText.T("reallife_ledger_online_app"));
                box.Title(LedgerText.T("reallife_ledger_online_tab_reading"));
                box.Line(LedgerText.Fill("reallife_ledger_online_nothing", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ledger_online_to_titles", () => UiClone.Safe(() => ShowTab(TabTitles)));
                Meta.Guide.Point(box, GuideTitle);
                _writer.Empty(LedgerText.Fill("reallife_ledger_online_nothing", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)), _scratch);
                Sheet(true);
                Lay(_scratch, true);
                return;
            }
            if (!_days.Contains(_day))
            {
                _day = _days[0];
            }
            int title = _title;
            LedgerEdition e = _director.OnlineEdition(_day);
            string name = LedgerText.T(NewsstandCatalogue.NameKeys[title]);
            UiClone.Words(_shell.Header, LedgerText.Fill("reallife_ledger_online_headline_reading", "title", name, "day", DayWords(_day, today)));
            box.Title(name);
            if (!NewsstandRules.Covered(s, title))
            {
                box.Note(LedgerText.T("reallife_ledger_online_box_kept"));
            }
            box.Row("reallife_ledger_online_box_edition", LedgerPage.Plain(DayWords(_day, today)));
            if (_days.Count > 1)
            {
                int at = _days.IndexOf(_day);
                box.Selector(_days.Count, at, i => DayWords(_days[i], today), i => UiClone.Safe(() =>
                {
                    _day = _days[i];
                    Refresh();
                }));
            }
            Transform buttons = box.ButtonRow();
            if (title == NewsstandCatalogue.Ledger && e != null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_printed", () => UiClone.Safe(() =>
                {
                    if (!_director.ReadPrinted(e))
                    {
                        Say(NotificationType.Info, "reallife_ledger_online_no_printed");
                    }
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ledger_online_to_titles", () => UiClone.Safe(() => ShowTab(TabTitles)));
            box.Line(LedgerText.T("reallife_ledger_online_box_scroll"));
            Meta.Guide.Point(box, GuideTitle);
            Sheet(true);
            List<NewsBlock> blocks = e != null ? Page(title, _day, e) : null;
            if (blocks == null)
            {
                _writer.Empty(LedgerText.T("reallife_ledger_online_gone"), _scratch);
                blocks = _scratch;
            }
            Lay(blocks, ReferenceEquals(blocks, _scratch));
        }

        // A title's edition as written for the phone: the one kept, or written now and kept.
        private List<NewsBlock> Page(int title, int day, LedgerEdition e)
        {
            for (int i = 0; i < _written.Count; i++)
            {
                Written w = _written[i];
                if (w.Title == title && w.Day == day && w.Language == _language && ReferenceEquals(w.Edition, e))
                {
                    return w.Blocks;
                }
            }
            Written made = null;
            for (int i = _written.Count - 1; i >= 0; i--)
            {
                Written w = _written[i];
                if ((w.Title == title && w.Day == day) || w.Day <= _director.Today - NewsstandCatalogue.KeepDays)
                {
                    made = made ?? w;
                    _written.RemoveAt(i);
                }
            }
            if (made == null && _written.Count >= WrittenMost)
            {
                made = _written[0];
                _written.RemoveAt(0);
            }
            made = made ?? new Written();
            if (ReferenceEquals(made.Blocks, _shown))
            {
                _shown = null;   // its blocks are written again: the page is set again too
            }
            made.Title = title;
            made.Day = day;
            made.Language = _language;
            made.Edition = e;
            float started = Time.realtimeSinceStartup;
            _writer.Write(e, title, made.Blocks);
            _written.Add(made);
            if (Meta.Dev.Verbose)
            {
                LedgerDirector.Logger?.Info("Newsstand: " + NewsstandCatalogue.NameKeys[title] + " of day " + day + " written for the phone: " + made.Blocks.Count + " blocks, "
                                            + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
            }
            return made.Blocks;
        }

        private void Sheet(bool on)
        {
            if (_sheet != null && _sheet.gameObject.activeSelf != on)
            {
                _sheet.gameObject.SetActive(on);
                if (on)
                {
                    _sheet.SetAsLastSibling();   // over whatever the table has put in the list since
                }
            }
            if (!on && _glide != null)
            {
                _glide.Stop();
            }
        }

        // The blocks set one under the next in a column of readable width, in the middle of the sheet; the page goes to
        // its top when it is another page than the one shown.
        private void Lay(List<NewsBlock> blocks, bool fresh)
        {
            if (_viewport == null || _content == null)
            {
                return;
            }
            float width = _viewport.rect.width;
            if (width < 200f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_shell.Root.transform);
                width = _viewport.rect.width;
            }
            if (width < 200f)
            {
                LaterOnce();   // the frame has not been laid out yet (the first opening): set once it has
                return;
            }
            bool other = fresh || !ReferenceEquals(blocks, _shown) || Mathf.Abs(width - _shownWidth) > 1f;
            if (!other)
            {
                return;   // the same page at the same width: it is set already, and stays where it was scrolled to
            }
            _shown = blocks;
            _shownWidth = width;
            if (_gameSize <= 0f)
            {
                TextAt(0);   // the first text made reads the game's own size, which the page's type is set against
            }
            float u = Mathf.Clamp(Mathf.Max(_gameSize, 26f), 24f, 40f);
            float column = Mathf.Min(width - 2.4f * u, 30f * u);
            float px = LedgerPage.Hairline(_sheet);
            float y = 1.2f * u;
            int texts = 0, rules = 0;
            for (int i = 0; i < blocks.Count; i++)
            {
                NewsBlock b = blocks[i];
                if (b.Kind == NewsstandPage.Rule || b.Kind == NewsstandPage.Double)
                {
                    y += 0.35f * u;
                    Image rule = RuleAt(rules++);
                    float thick = b.Kind == NewsstandPage.Double ? 3f * px : px;
                    LedgerPage.Put(rule.rectTransform, 0f, y, column, thick);
                    if (b.Kind == NewsstandPage.Double)
                    {
                        LedgerPage.Put(RuleAt(rules++).rectTransform, 0f, y + thick + 3f * px, column, px);
                        y += 4f * px;
                    }
                    y += thick + 0.45f * u;
                    continue;
                }
                TMP_Text t = TextAt(texts++);
                Style(t, b.Kind, u);
                float h = t.GetPreferredValues(b.Text, column, 0f).y;
                UiClone.Words(t, b.Text);
                LedgerPage.Put(t.rectTransform, 0f, y, column, h + 2f);
                y += h + After(b.Kind) * u;
            }
            for (int i = texts; i < _texts.Count; i++)
            {
                if (_texts[i].gameObject.activeSelf)
                {
                    _texts[i].gameObject.SetActive(false);
                }
            }
            for (int i = rules; i < _rules.Count; i++)
            {
                if (_rules[i].gameObject.activeSelf)
                {
                    _rules[i].gameObject.SetActive(false);
                }
            }
            _content.sizeDelta = new Vector2(column, y + 1.5f * u);
            if (_wheel != null)
            {
                _wheel.Step = 4.2f * u;
            }
            if (_glide != null)
            {
                _glide.Top();
            }
        }

        // How each kind of block is set: its size against the body's, its alignment and spacing.
        private void Style(TMP_Text t, byte kind, float u)
        {
            float size = 1f;
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft;
            float paragraph = 0f;
            switch (kind)
            {
                case NewsstandPage.Masthead: size = 2.2f; align = TextAlignmentOptions.Top; break;
                case NewsstandPage.Centre: size = 0.86f; align = TextAlignmentOptions.Top; break;
                case NewsstandPage.Label: size = 0.9f; break;
                case NewsstandPage.Table: size = 0.9f; break;
                case NewsstandPage.Foot: size = 0.76f; align = TextAlignmentOptions.Top; break;
                default: paragraph = 45f; break;
            }
            t.fontSize = size * u;
            t.alignment = align;
            t.paragraphSpacing = paragraph;
            t.lineSpacing = kind == NewsstandPage.Masthead ? -18f : kind == NewsstandPage.Table ? -6f : 0f;
        }

        private static float After(byte kind)
        {
            switch (kind)
            {
                case NewsstandPage.Masthead: return 0.2f;
                case NewsstandPage.Centre: return 0.2f;
                case NewsstandPage.Label: return 0.35f;
                case NewsstandPage.Table: return 0.55f;
                case NewsstandPage.Foot: return 0.4f;
                default: return 0.75f;
            }
        }

        // A text of the page: the white box's line lifted out of its layout, in the paper's face and ink.
        private TMP_Text TextAt(int i)
        {
            while (_texts.Count <= i)
            {
                TMP_Text t = _shell.Box.Line(string.Empty, _content);
                t.gameObject.name = "Block" + _texts.Count;
                LedgerPage.Loose(t.gameObject);
                if (_gameSize <= 0f)
                {
                    _gameSize = t.fontSize;
                }
                if (_face != null)
                {
                    t.font = _face;
                }
                t.color = LedgerPage.Ink;
                t.fontStyle = FontStyles.Normal;
                t.richText = true;
                t.raycastTarget = false;
                t.enableAutoSizing = false;
                t.margin = Vector4.zero;
                t.textWrappingMode = TextWrappingModes.Normal;
                t.overflowMode = TextOverflowModes.Overflow;
                t.characterSpacing = 0f;
                t.wordSpacing = 0f;
                _texts.Add(t);
            }
            TMP_Text text = _texts[i];
            if (!text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(true);
            }
            return text;
        }

        private Image RuleAt(int i)
        {
            while (_rules.Count <= i)
            {
                var go = new GameObject("Rule" + _rules.Count, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_content, false);
                var image = go.GetComponent<Image>();
                image.color = LedgerPage.RuleInk;
                image.raycastTarget = false;
                _rules.Add(image);
            }
            Image rule = _rules[i];
            if (!rule.gameObject.activeSelf)
            {
                rule.gameObject.SetActive(true);
            }
            return rule;
        }

        // The page set again a frame later, once the frame has been laid out (at most one wait at a time).
        private void LaterOnce()
        {
            if (_later || _director == null)
            {
                return;
            }
            _later = true;
            _director.StartCoroutine(Guard.Routine(Later()));
        }

        private IEnumerator Later()
        {
            yield return null;
            _later = false;
            if (Showing && _tab == TabReading)
            {
                UiClone.Safe(Refresh);
            }
        }

        // The days a title can be read on into _days, newest first, those whose edition is still kept. False if none.
        private bool Readable(NewsstandState s, int title, int today)
        {
            NewsstandRules.Days(s, title, today, _days);
            for (int i = _days.Count - 1; i >= 0; i--)
            {
                if (_director.OnlineEdition(_days[i]) == null)
                {
                    _days.RemoveAt(i);
                }
            }
            return _days.Count > 0;
        }

        // The first title with something to read: today's edition first, else the latest that came. -1 for none.
        private int FirstReadable(NewsstandState s, int today)
        {
            int best = -1, bestDay = 0;
            for (int t = 0; t < NewsstandCatalogue.Titles; t++)
            {
                if (Readable(s, t, today) && _days[0] > bestDay)
                {
                    best = t;
                    bestDay = _days[0];
                }
            }
            return best;
        }

        private static string DayWords(int day, int today)
        {
            return day == today
                ? LedgerText.Fill("reallife_ledger_online_day_today", "day", day.ToString())
                : LedgerText.Fill("reallife_ledger_online_day", "weekday", LedgerPage.Weekday(day), "day", day.ToString());
        }
    }
}
