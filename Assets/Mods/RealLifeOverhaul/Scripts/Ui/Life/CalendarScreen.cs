using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Planner;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Calendar on the kit: the month as a diary, today's weather, and tomorrow under the chosen day. The old page
    /// stays for when this screen's switch is off. Cost: a look and a layout when the month or the chosen day changes.
    /// </summary>
    public sealed class CalendarScreen : IUiScreen
    {
        private readonly CalendarDirector _director;
        private readonly UiCalModel _model = new UiCalModel();
        private readonly List<UiDiaryEntry> _entries = new List<UiDiaryEntry>(32);
        private int _month = -1;
        private int _chosen = 1;
        private int _sig = int.MinValue;

        public CalendarScreen(CalendarDirector director)
        {
            _director = director;
        }

        public string Id => "calendar";

        public bool Dirty => _sig != Signature();

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            _sig = Signature();
            LifePages.Calendar(s, _model, frame, dark, keys);
        }

        public bool Press(int command, int arg)
        {
            if (command == UiDiary.DayCommand && arg >= 1)
            {
                _chosen = arg;
                return true;
            }
            if (command == UiDiary.MonthCommand)
            {
                _month += arg < 0 ? -1 : 1;
                _chosen = 1;
                return true;
            }
            if (command == LifePages.Add)
            {
                TextBox.Ask("reallife_planner_note_placeholder", "common_confirm", text =>
                {
                    string clean = PlannerStore.Clean(text);
                    if (clean.Length > 0)
                    {
                        PlannerStore.AddNote(clean, ChosenDay(), 9 * 60);
                    }
                    _sig = int.MinValue;
                }, null, string.Empty, false, PlannerStore.TitleLength);
                return true;
            }
            if (command == LifePages.Second)
            {
                PlannerDirector.Instance?.Open();
                return true;
            }
            return command == UiDiary.EntryCommand;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;

        public void Tab(int step)
        {
            _month += step < 0 ? -1 : 1;
            _chosen = 1;
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                return Press(LifePages.Add, 0);
            }
            if (action == UiAction.Option)
            {
                return Press(LifePages.Second, 0);
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private int Signature() => _month * 1000 + _chosen * 17 + TimeHelper.CurrentDay + TimeHelper.CurrentHour;

        private void Build()
        {
            int today = TimeHelper.CurrentDay;
            int year = GameCalendar.DaysPerYear;
            if (_month < 0)
            {
                _month = CalendarRules.MonthOf(today, year);
            }
            int from = CalendarRules.MonthStart(_month, year);
            int length = CalendarRules.MonthLength(_month, year);
            if (_chosen > length)
            {
                _chosen = length;
            }
            _director?.Look(_month);
            _entries.Clear();
            int chosenDay = from + _chosen - 1;
            int count = 0, done = 0;
            if (_director != null)
            {
                for (int i = 0; i < _director.List.Items.Count; i++)
                {
                    CalendarEntry e = _director.List.Items[i];
                    int day = e.Day - from + 1;
                    if (day < 1 || day > length)
                    {
                        continue;
                    }
                    if (day == _chosen || day == _chosen + 1)
                    {
                        _entries.Add(new UiDiaryEntry
                        {
                            Day = day, Icon = e.Group == CalendarGroup.Money ? UiIcon.Coin : e.Group == CalendarGroup.City ? UiIcon.Building : UiIcon.Calendar,
                            Title = string.IsNullOrEmpty(e.Title) ? UiText.T("reallife_ui_life_a_plan") : e.Title, Value = e.Value,
                        });
                    }
                    if (day == _chosen)
                    {
                        count++;
                    }
                }
            }
            PlannerDirector planner = PlannerDirector.Instance;
            if (planner != null)
            {
                planner.Look(chosenDay, chosenDay, false);
                for (int i = 0; i < planner.List.Items.Count; i++)
                {
                    if (planner.List.Items[i].Day == chosenDay && planner.List.Items[i].Done)
                    {
                        done++;
                    }
                }
            }
            string[] days =
            {
                UiText.T("common_monday"), UiText.T("common_tuesday"), UiText.T("common_wednesday"), UiText.T("common_thursday"),
                UiText.T("common_friday"), UiText.T("common_saturday"), UiText.T("common_sunday"),
            };
            _model.Title = UiText.T("reallife_calendar_app");
            _model.Add = UiText.T("reallife_ui_life_add");
            _model.Open = UiText.T("reallife_ui_life_open_planner");
            _model.Move = UiText.T("reallife_ui_prompt_move");
            _model.Select = UiText.T("reallife_ui_prompt_select");
            _model.Back = UiText.T("reallife_ui_prompt_back");
            _model.Weather = chosenDay == today ? WeatherLine() : null;
            _model.Diary = new UiDiaryModel
            {
                Month = MonthName(_month, year) + ", " + UiText.Fill("reallife_ui_life_week", "week", CalendarRules.WeekNumber(from).ToString()),
                Weekdays = days, Lead = MonthViewRules.Lead(from), Length = length,
                Today = today >= from && today < from + length ? today - from + 1 : 0,
                Chosen = _chosen, PastBefore = today >= from && today < from + length ? today - from + 1 : 0,
                Entries = _entries.ToArray(),
                ChosenTitle = UiText.Fill("reallife_ui_life_day_line", "count", count.ToString(), "done", done.ToString()),
                ChosenEmpty = UiText.T("reallife_ui_life_nothing_day"),
                AheadTitle = UiText.T("reallife_ui_life_tomorrow"),
                More = UiText.T("reallife_ui_life_more"),
            };
        }

        private int ChosenDay()
        {
            int year = GameCalendar.DaysPerYear;
            return CalendarRules.MonthStart(_month < 0 ? CalendarRules.MonthOf(TimeHelper.CurrentDay, year) : _month, year) + _chosen - 1;
        }

        private static readonly string[] MonthKeys =
        {
            "reallife_calendar_month_1", "reallife_calendar_month_2", "reallife_calendar_month_3", "reallife_calendar_month_4",
            "reallife_calendar_month_5", "reallife_calendar_month_6", "reallife_calendar_month_7", "reallife_calendar_month_8",
            "reallife_calendar_month_9", "reallife_calendar_month_10", "reallife_calendar_month_11", "reallife_calendar_month_12",
        };

        private static string MonthName(int month, int year)
        {
            int inYear = CalendarRules.MonthInYear(month, year);
            int at = inYear >= 1 && inYear <= MonthKeys.Length ? inYear - 1 : 0;
            return UiText.T(MonthKeys[at]);
        }

        private static string WeatherLine()
        {
            string token = Photos.PhotoShot.Token();
            int celsius = Photos.PhotoShot.Degrees(token);
            return UiText.Fill("reallife_ui_life_weather", "sky", UiText.T(Photos.PhotoShot.Words(token)), "temp", celsius.ToString() + " C");
        }
    }
}
