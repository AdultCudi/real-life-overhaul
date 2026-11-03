using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Planner;
using Streets;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Planner on the kit: Today and the week on the board, Upcoming and Notes as rows. The old page stays underneath
    /// and is what shows when this screen's switch is off. Cost: a look and a layout when the day, the tab or a note changes.
    /// </summary>
    public sealed class PlannerScreen : IUiScreen
    {
        private readonly PlannerDirector _director;
        private readonly List<PlannerEntry> _rows = new List<PlannerEntry>(32);
        private readonly UiPlanModel _model = new UiPlanModel();
        private int _tab, _chip;
        private int _sig = int.MinValue;
        private bool _force = true;
        private string[] _hours;

        public PlannerScreen(PlannerDirector director)
        {
            _director = director;
        }

        public string Id => "planner";

        public bool Dirty => _force || _sig != Signature();

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_sig != Signature())
            {
                Build();
                _sig = Signature();
            }
            _force = false;
            LifePages.Planner(s, _model, frame, dark, keys);
        }

        public bool Press(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand && arg >= 0 && arg < 4 && arg != _tab)
            {
                _tab = arg;
                _model.Offset = 0f;
                return true;
            }
            if (command == LifePages.Chip && arg >= 0 && arg < 6 && arg != _chip)
            {
                _chip = arg;
                return true;
            }
            if (command == LifePages.Add || command == UiBoard.ActionCommand && arg == AddAction())
            {
                AskNote(null);
                return true;
            }
            if (command == LifePages.Row && arg >= 0 && arg < _rows.Count)
            {
                return Open(_rows[arg]);
            }
            if (command == LifePages.Locate && arg >= 0 && arg < _rows.Count)
            {
                Locate(_rows[arg]);
                return true;
            }
            if (command == UiBoard.ShiftCommand)
            {
                PlannerEntry entry = Shift(arg);
                return entry != null && Open(entry);
            }
            return command == UiBoard.LaneCommand || command == UiBoard.DayCommand || command == LifePages.Second;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;

        public void Tab(int step)
        {
            int next = _tab + (step < 0 ? -1 : 1);
            if (next >= 0 && next < 4)
            {
                _tab = next;
                _model.Offset = 0f;
            }
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                AskNote(null);
                return true;
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            _model.Offset = Math.Max(0, index) * 48f;
        }

        public void Wheel(int clip, float notches)
        {
            _model.Offset = Math.Max(0f, _model.Offset - notches * 48f);
            _force = true;
        }

        private int Signature()
        {
            int notes = PlannerStore.Notes.Count;
            int hour = TimeHelper.CurrentHour;
            int day = TimeHelper.CurrentDay;
            return _tab * 1000003 + _chip * 10007 + hour * 97 + day * 13 + notes;
        }

        private void Build()
        {
            int today = TimeHelper.CurrentDay;
            int from = _tab == 1 ? today + 1 : today;
            int to = _tab == 1 ? today + 14 : _tab == 2 ? today + 6 : today;
            if (_director != null)
            {
                _director.Look(from, to, true);
            }
            _rows.Clear();
            int group = GroupOfChip(_chip);
            List<PlannerEntry> items = _director != null ? _director.List.Items : null;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    PlannerEntry e = items[i];
                    if (!PlannerRules.OnTab(e, _tab == 3 ? PlannerRules.TabToday : _tab, today))
                    {
                        continue;
                    }
                    if (_tab == 3 && !e.Own)
                    {
                        continue;
                    }
                    if (group >= 0 && (int)PlannerRules.GroupOf(e) != group)
                    {
                        continue;
                    }
                    _rows.Add(e);
                }
            }
            if (_tab == 3)
            {
                _rows.Clear();
                for (int i = 0; i < PlannerStore.Notes.Count; i++)
                {
                    PlannerNote note = PlannerStore.Notes[i];
                    PlannerEntry e = new PlannerEntry { Title = note.Title, Day = note.Day, Start = note.Start, Own = true, Category = PlannerCategory.Note };
                    e.Id = new EntryId { Source = "note", A = note.Id };
                    _rows.Add(e);
                }
            }
            Fill(today);
        }

        private void Fill(int today)
        {
            _model.Title = UiText.T("reallife_planner_app");
            _model.Tab = _tab;
            _model.Chip = _chip;
            _model.Week = UiText.Fill("reallife_ui_life_week", "week", CalendarRules.WeekNumber(today).ToString());
            _model.Nav = Nav();
            _model.Chips = Chips();
            _model.LocateWord = UiText.T("reallife_ui_life_locate");
            _model.Move = UiText.T("reallife_ui_prompt_move");
            _model.Select = UiText.T("reallife_ui_prompt_select");
            _model.Back = UiText.T("reallife_ui_prompt_back");
            _model.Tabs = UiText.T("reallife_ui_prompt_page");
            _model.Add = _tab == 0 || _tab == 2 ? null : UiText.T("reallife_ui_life_add");
            _model.EmptyTitle = UiText.T(_tab == 3 ? "reallife_ui_life_empty_notes_title" : _tab == 1 ? "reallife_ui_life_empty_upcoming_title" : "reallife_ui_life_empty_today_title");
            _model.EmptyBody = UiText.T(_tab == 3 ? "reallife_ui_life_empty_notes_body" : _tab == 1 ? "reallife_ui_life_empty_upcoming_body" : "reallife_ui_life_empty_today_body");
            if (_tab == 0 || _tab == 2)
            {
                _model.Board = Board(today);
                _model.RowCount = 0;
            }
            else
            {
                _model.Board = null;
                _model.Rows = new UiPlanRow[_rows.Count];
                for (int i = 0; i < _rows.Count; i++)
                {
                    PlannerEntry e = _rows[i];
                    _model.Rows[i] = new UiPlanRow
                    {
                        Icon = IconOf(PlannerRules.GroupOf(e)),
                        Title = string.IsNullOrEmpty(e.Title) ? UiText.T("reallife_ui_life_a_business") : e.Title,
                        Meta = e.Start >= 0 ? e.Start / 60 + ":" + (e.Start % 60).ToString("00") : UiText.T("reallife_ui_life_all_day"),
                        Locate = e.HasPlace || !string.IsNullOrEmpty(e.Street),
                    };
                }
                _model.RowCount = _rows.Count;
            }
        }

        private UiBoardModel Board(int today)
        {
            if (_hours == null)
            {
                _hours = new string[16];
                for (int i = 0; i < _hours.Length; i++)
                {
                    _hours[i] = (7 + i).ToString("00") + ":00";
                }
            }
            var lanes = new List<UiBoardLane>(8);
            var index = new int[(int)PlannerGroup.Count];
            for (int g = 0; g < index.Length; g++)
            {
                index[g] = -1;
            }
            if (_tab == 2)
            {
                for (int d = 0; d < 7; d++)
                {
                    lanes.Add(new UiBoardLane { Name = PlannerText.Weekday(today + d), Icon = UiIcon.Calendar, Tint = UiRole.Chart1 });
                }
            }
            else
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    int g = (int)PlannerRules.GroupOf(_rows[i]);
                    if (g >= 0 && g < index.Length && index[g] < 0)
                    {
                        index[g] = lanes.Count;
                        lanes.Add(new UiBoardLane { Name = ChipName(g), Icon = IconOf((PlannerGroup)g), Tint = TintOf(g) });
                    }
                }
            }
            var shifts = new List<UiBoardShift>(_rows.Count);
            for (int i = 0; i < _rows.Count; i++)
            {
                PlannerEntry e = _rows[i];
                int lane = _tab == 2 ? e.Day - today : index[(int)PlannerRules.GroupOf(e)];
                if (lane < 0 || lane >= lanes.Count || e.Start < 0)
                {
                    continue;
                }
                int end = e.End >= 0 ? e.End : e.Start + 60;
                shifts.Add(new UiBoardShift
                {
                    Lane = lane, Start = e.Start, End = end, Name = e.Title, Warn = e.Clash || e.State == PlannerState.Missed,
                    Span = (e.Start / 60).ToString("00") + ":" + (e.Start % 60).ToString("00"),
                });
            }
            var actions = new List<UiActionModel>(2);
            if (_tab == 0)
            {
                actions.Add(new UiActionModel { Label = UiText.T("reallife_ui_life_jump"), Kind = UiButtonKind.Secondary, Key = UiAction.Option });
            }
            actions.Add(new UiActionModel { Label = UiText.T("reallife_ui_life_add"), Kind = UiButtonKind.Primary, Key = UiAction.Alt });
            return new UiBoardModel
            {
                FirstHour = 7, LastHour = 23, Hours = _hours, Now = _tab == 0 ? TimeHelper.CurrentHour * 60 + Mathf.FloorToInt(TimeHelper.CurrentMinute) : -1,
                Lanes = lanes.ToArray(), Shifts = shifts.ToArray(), Actions = actions.ToArray(),
                EmptyTitle = _model.EmptyTitle, EmptyBody = _model.EmptyBody,
            };
        }

        private int AddAction() => _model.Board != null && _model.Board.Actions != null ? _model.Board.Actions.Length - 1 : -1;

        private PlannerEntry Shift(int arg)
        {
            int seen = 0;
            int today = TimeHelper.CurrentDay;
            for (int i = 0; i < _rows.Count; i++)
            {
                PlannerEntry e = _rows[i];
                int lane = _tab == 2 ? e.Day - today : 0;
                if (e.Start < 0 || (_tab == 2 && (lane < 0 || lane >= 7)))
                {
                    continue;
                }
                if (seen++ == arg)
                {
                    return e;
                }
            }
            return arg >= 0 && arg < _rows.Count ? _rows[arg] : null;
        }

        private bool Open(PlannerEntry e)
        {
            if (e != null && e.Own)
            {
                AskNote(PlannerStore.Note(e.Id.A));
                return true;
            }
            if (e != null && (e.HasPlace || !string.IsNullOrEmpty(e.Street)))
            {
                Locate(e);
                return true;
            }
            return false;
        }

        private void AskNote(PlannerNote note)
        {
            TextBox.Ask("reallife_planner_note_placeholder", "common_confirm", text =>
            {
                string clean = PlannerStore.Clean(text);
                if (clean.Length == 0)
                {
                    return;
                }
                if (note == null)
                {
                    if (PlannerStore.AddNote(clean, TimeHelper.CurrentDay, (TimeHelper.CurrentHour + 1) * 60) == null)
                    {
                        Notifications.ShowError("reallife_planner_notes_full", null, false);
                    }
                }
                else
                {
                    note.Title = clean;
                }
                _sig = int.MinValue;
            }, null, note != null ? note.Title : string.Empty, false, PlannerStore.TitleLength);
        }

        private static void Locate(PlannerEntry e)
        {
            if (e == null || SaveGameManager.Current == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(e.Street))
            {
                var address = new Address(e.Street, e.Number);
                SaveGameManager.Current.customDestination = address;
                GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);
                Notifications.Show(NotificationType.Success, "notification_destination_set", new Dictionary<string, string> { { "address", address.ToFormattedString() } });
            }
        }

        private static int GroupOfChip(int chip)
        {
            switch (chip)
            {
                case 1: return (int)PlannerGroup.Work;
                case 2: return (int)PlannerGroup.Life;
                case 3: return (int)PlannerGroup.Money;
                case 4: return (int)PlannerGroup.Business;
                case 5: return (int)PlannerGroup.Study;
                default: return -1;
            }
        }

        private static UiNavEntry[] Nav()
        {
            return new[]
            {
                new UiNavEntry { Icon = UiIcon.Clock, Label = UiText.T("reallife_ui_life_tab_today") },
                new UiNavEntry { Icon = UiIcon.Calendar, Label = UiText.T("reallife_planner_tab_upcoming") },
                new UiNavEntry { Icon = UiIcon.Grid, Label = UiText.T("reallife_planner_tab_week") },
                new UiNavEntry { Icon = UiIcon.Info, Label = UiText.T("reallife_ui_life_tab_notes"), Count = PlannerStore.Notes.Count > 0 ? PlannerStore.Notes.Count.ToString() : null },
            };
        }

        private static string[] Chips()
        {
            return new[]
            {
                UiText.T("reallife_planner_filter_all"), UiText.T("reallife_ui_life_group_work"), UiText.T("reallife_ui_life_group_life"),
                UiText.T("reallife_planner_group_money"), UiText.T("reallife_planner_group_business"), UiText.T("reallife_ui_life_group_study"),
            };
        }

        private static string ChipName(int group)
        {
            switch ((PlannerGroup)group)
            {
                case PlannerGroup.Work: return UiText.T("reallife_ui_life_group_work");
                case PlannerGroup.Money: return UiText.T("reallife_planner_group_money");
                case PlannerGroup.Business: return UiText.T("reallife_planner_group_business");
                case PlannerGroup.Study: return UiText.T("reallife_ui_life_group_study");
                default: return UiText.T("reallife_ui_life_group_life");
            }
        }

        private static UiIcon IconOf(PlannerGroup group)
        {
            switch (group)
            {
                case PlannerGroup.Work: return UiIcon.Briefcase;
                case PlannerGroup.Money: return UiIcon.Coin;
                case PlannerGroup.Business: return UiIcon.Store;
                case PlannerGroup.Study: return UiIcon.Cap;
                default: return UiIcon.Heart;
            }
        }

        private static UiRole TintOf(int group)
        {
            switch ((PlannerGroup)group)
            {
                case PlannerGroup.Work: return UiRole.Chart1;
                case PlannerGroup.Study: return UiRole.Chart6;
                case PlannerGroup.Money: return UiRole.Chart2;
                case PlannerGroup.Business: return UiRole.Chart3;
                default: return UiRole.Chart5;
            }
        }
    }
}
