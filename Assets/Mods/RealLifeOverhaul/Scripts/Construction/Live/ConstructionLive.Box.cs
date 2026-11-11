using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Construction
{
    public sealed partial class ConstructionLive
    {
        /// <summary>A row of buttons the old pages wrote together: the kit puts them where the page's actions stand.</summary>
        private sealed class KitRow
        {
            public readonly List<KitButton> Buttons = new List<KitButton>(4);
        }

        private struct KitButton
        {
            public int Kind;
            public string Key;
            public Action Click;
        }

        /// <summary>
        /// The page the old app wrote into the game's white box (a title, rows, notes, meters, selectors, buttons), taken as the kit's cards: the same calls with the same words, so a
        /// page keeps every line and every button it had, and the layout is the kit's. A section starts a card; the way back and the map link, which the
        /// page's head holds, are not lines.
        /// Written when a page is opened or pressed, never a frame.
        /// </summary>
        private sealed class KitBox
        {
            public const int Positive = 0, Negative = 1, Additional = 2;
            private const string BackKey = "reallife_bld_back", MapKey = "reallife_bld_button_map";

            private readonly ConstructionRuns _runs;
            private readonly Func<string, string> _words;
            private readonly List<UiFactCardModel> _cards = new List<UiFactCardModel>(8);
            private readonly List<UiFactLine> _lines = new List<UiFactLine>(24);
            private readonly List<KitRow> _rows = new List<KitRow>(4);
            private string _cardTitle;

            public string TitleWords, SubtitleWords, ChipWords, LocateWords;
            public CellTone ChipTone;
            public int LocateRun = -1;
            public bool HasContent => _cards.Count > 0 || _lines.Count > 0 || _rows.Count > 0 || !string.IsNullOrEmpty(TitleWords);

            public KitBox(ConstructionRuns runs, Func<string, string> words)
            {
                _runs = runs;
                _words = words;
            }

            public void Clear()
            {
                _cards.Clear();
                _lines.Clear();
                _rows.Clear();
                _cardTitle = null;
                TitleWords = SubtitleWords = ChipWords = LocateWords = null;
                LocateRun = -1;
                ChipTone = CellTone.Plain;
            }

            public void Title(string words) => TitleWords = words;

            public void Header(string words) => TitleWords = words;

            public void Subtitle(string words) => SubtitleWords = words;

            public void Chip(string words, CellTone tone = CellTone.Plain)
            {
                ChipWords = words;
                ChipTone = tone;
            }

            public void Section(string key) => SectionWords(_words(key));

            public void SectionWords(string words)
            {
                Close();
                _cardTitle = words;
            }

            public void Row(string labelKey, string value, CellTone tone = CellTone.Plain) => RowWords(_words(labelKey), value, tone);

            public void RowWords(string label, string value) => RowWords(label, value, CellTone.Plain);

            public void RowWords(string label, string value, CellTone tone)
            {
                _lines.Add(UiFactLine.Row(label, value, Ink(tone)));
            }

            public void RowPair(string labelKey, string value, string labelKey2, string value2)
            {
                Row(labelKey, value);
                Row(labelKey2, value2);
            }

            public void RowPairWords(string label, string value, string label2, string value2)
            {
                RowWords(label, value);
                RowWords(label2, value2);
            }

            public void Meter(string labelKey, float share, string figure, CellTone tone) => MeterWords(_words(labelKey), share, figure, tone);

            public void MeterWords(string label, float share, string figure, CellTone tone)
            {
                _lines.Add(UiFactLine.Bar(label?.TrimEnd(':'), (int)Math.Round(Math.Max(0f, Math.Min(1f, share)) * 100f), figure));
            }

            public void Check(string words, bool met) => _lines.Add(UiFactLine.Tick(words, met));

            public void Line(string words, CellTone tone) => _lines.Add(UiFactLine.Say(words, Ink(tone)));

            public void Note(string words) => _lines.Add(UiFactLine.Say(words));

            /// <summary>A line the player presses, in cyan with an arrow: it runs <paramref name="click"/> like a button.</summary>
            public void Link(string words, Action click) => _lines.Add(_runs.Link(words, click));

            /// <summary>A choice among a few things as a step: the chosen one's words between a minus and a plus, and in full under them where they are long.</summary>
            public bool Selector(int count, int index, Func<int, string> text, Action<int> changed)
            {
                if (count <= 0)
                {
                    return false;
                }
                index = Math.Max(0, Math.Min(count - 1, index));
                string words = text(index);
                int at = _runs.Do(() => changed(index - 1));
                _runs.Do(() => changed(index + 1));
                UiFactLine line = UiFactLine.Amount(null, words, count > 1 && words != null && words.Length > 22 ? words : null, index > 0,
                    index < count - 1, ConstructionFace.StepLess,
                    ConstructionFace.StepMore, at);
                _lines.Add(line);
                return true;
            }

            public bool Switch(string labelKey, bool on, Action<bool> changed)
            {
                _lines.Add(_runs.Switch(_words(labelKey), on, changed));
                return true;
            }

            public KitRow ButtonRow()
            {
                var row = new KitRow();
                _rows.Add(row);
                return row;
            }

            public static void Button(KitRow row, int kind, string key, Action click)
            {
                row.Buttons.Add(new KitButton { Kind = kind, Key = key, Click = click });
            }

            private static UiRole Ink(CellTone tone)
            {
                switch (tone)
                {
                    case CellTone.Yellow: return UiRole.WarnText;
                    case CellTone.Red: return UiRole.DangerText;
                    case CellTone.Green: return UiRole.OkText;
                    default: return UiRole.None;
                }
            }

            private void Close()
            {
                if (_lines.Count == 0 && _cardTitle == null)
                {
                    return;
                }
                _cards.Add(new UiFactCardModel { Title = _cardTitle, Heading = _cardTitle != null, Lines = _lines.ToArray() });
                _lines.Clear();
                _cardTitle = null;
            }

            /// <summary>The cards and the page's actions into the model, as a detail page: the map link and the way back are the head's, the other
            /// buttons the page's actions.</summary>
            public void Finish(ConstructionModel m)
            {
                Close();
                m.Cards = _cards.ToArray();
                var actions = new List<UiActionModel>(6);
                int main = -1, count = 0;
                for (int r = 0; r < _rows.Count; r++)
                {
                    for (int b = 0; b < _rows[r].Buttons.Count; b++)
                    {
                        KitButton k = _rows[r].Buttons[b];
                        if (k.Key == BackKey)
                        {
                            continue;   // the head's Back does it
                        }
                        if (k.Key == MapKey)
                        {
                            LocateWords = _words(MapKey);
                            LocateRun = _runs.Do(k.Click);
                            continue;
                        }
                        count++;
                        main = k.Kind == Positive ? actions.Count : main;
                        actions.Add(new UiActionModel
                        {
                            Label = _words(k.Key), Kind = UiButtonKind.Secondary, Icon = UiIcon.None, Key = UiAction.Count,
                                Command = ConstructionFace.ActionCommand, Arg = _runs.Do(k.Click)
                        });
                    }
                }
                // The main action is the last positive one, the page's one primary (F); the first of the others takes P. The main stands last, as the kit's buttons do.
                if (main >= 0)
                {
                    UiActionModel a = actions[main];
                    a.Kind = UiButtonKind.Primary;
                    a.Key = UiAction.Alt;
                    actions.RemoveAt(main);
                    actions.Add(a);
                }
                for (int i = 0; i < actions.Count - (main >= 0 ? 1 : 0); i++)
                {
                    if (i == 0)
                    {
                        UiActionModel a = actions[i];
                        a.Key = UiAction.Option;
                        actions[i] = a;
                    }
                }
                if (actions.Count <= 3)
                {
                    m.Actions = actions.ToArray();
                }
                else
                {
                    // Many buttons would crowd the head: they stand as the last card, where the page's other things to do are.
                    var list = new List<UiFactCardModel>(m.Cards)
                    {
                        new UiFactCardModel { Actions = actions.ToArray() }
                    };
                    m.Cards = list.ToArray();
                }
                m.DetailTitle = TitleWords;
                m.DetailSub = SubtitleWords;
                m.DetailChip = ChipWords;
                m.DetailChipTone = ChipTone == CellTone.Green ? UiRole.Ok : ChipTone == CellTone.Red ? UiRole.Danger : ChipTone == CellTone.Yellow ? UiRole.Warn : UiRole.Muted;
                m.DetailLocate = LocateWords;
                m.DetailLocateRun = LocateRun;
            }

            /// <summary>The page as the cards that stand beside a table: its head as the first card, the sections, and the buttons at the foot of the last.</summary>
        public UiFactCardModel[] Side(ConstructionModel m)
        {
            Close();
            var cards = new List<UiFactCardModel>(_cards.Count + 2);
            UiRole tone = ChipTone == CellTone.Green ? UiRole.Ok : ChipTone == CellTone.Red ? UiRole.Danger : ChipTone == CellTone.Yellow ? UiRole.Warn : UiRole.Muted;
            var head = new UiFactCardModel { Title = TitleWords, Sub = SubtitleWords, Chip = ChipWords, ChipTone = tone };
            var actions = new List<UiActionModel>(4);
            var headLines = new List<UiFactLine>(2);
            for (int r = 0; r < _rows.Count; r++)
            {
                for (int b = 0; b < _rows[r].Buttons.Count; b++)
                {
                    KitButton k = _rows[r].Buttons[b];
                    if (k.Key == BackKey)
                    {
                        continue;
                    }
                    if (k.Key == MapKey)
                    {
                        headLines.Add(UiFactLine.Go(_words(MapKey), ConstructionFace.ActionCommand, _runs.Do(k.Click)));
                        continue;
                    }
                    bool main = k.Kind == Positive && actions.Count == 0;
                    actions.Add(new UiActionModel
                    {
                        Label = _words(k.Key), Kind = main ? UiButtonKind.Primary : UiButtonKind.Secondary, Icon = UiIcon.None, Key = main ? UiAction.Alt : UiAction.Count,
                        Command = ConstructionFace.ActionCommand, Arg = _runs.Do(k.Click)
                    });
                }
            }
            head.Lines = headLines.ToArray();
            cards.Add(head);
            cards.AddRange(_cards);
            if (actions.Count > 0)
            {
                cards.Add(new UiFactCardModel { Actions = actions.ToArray() });
            }
            return cards.ToArray();
        }

        /// <summary>The page's card for a side: the lines and the buttons without a head, for what a tab is before anything is picked.</summary>
            public UiFactCardModel Card(string title)
            {
                Close();
                var actions = new List<UiActionModel>(4);
                for (int r = 0; r < _rows.Count; r++)
                {
                    for (int b = 0; b < _rows[r].Buttons.Count; b++)
                    {
                        KitButton k = _rows[r].Buttons[b];
                        if (k.Key == BackKey || k.Key == MapKey)
                        {
                            continue;
                        }
                        bool main = k.Kind == Positive && actions.Count == 0;
                        actions.Add(new UiActionModel
                        {
                            Label = _words(k.Key), Kind = main ? UiButtonKind.Primary : UiButtonKind.Secondary, Icon = UiIcon.None, Key = main ? UiAction.Alt : UiAction.Count,
                            Command = ConstructionFace.ActionCommand, Arg = _runs.Do(k.Click)
                        });
                    }
                }
                var lines = new List<UiFactLine>(8);
                for (int i = 0; i < _cards.Count; i++)
                {
                    lines.AddRange(_cards[i].Lines);
                }
                return new UiFactCardModel { Title = title ?? TitleWords, Sub = SubtitleWords, Lines = lines.ToArray(), Actions = actions.ToArray() };
            }
        }
    }
}
