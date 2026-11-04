using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Calendar on the kit: its skin over the game. Everything the screen does is <see cref="CalendarFlow"/>'s (game-free, so
    /// the checks press every target); this lays its model out and gives it the game's day, entries, weather and apps. The old
    /// page stays for when this screen's switch is off. Cost: a look and a layout when the month, the chosen day or the hour changes.
    /// </summary>
    public sealed class CalendarScreen : IUiScreen
    {
        private readonly CalendarFlow _flow;

        public CalendarScreen(CalendarDirector director)
        {
            _flow = new CalendarFlow(new Host(director), UiText.Words);
        }

        public string Id => "calendar";

        public bool Dirty => _flow.Dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_flow.Dirty)
            {
                _flow.Build();
            }
            LifePages.Calendar(s, _flow.Model, frame, dark, keys);
        }

        public bool Press(int command, int arg) => _flow.Press(command, arg);

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;

        public void Tab(int step) => _flow.Tab(step);

        public bool Act(UiAction action) => _flow.Act(action);

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        /// <summary>The game as the Calendar's flow asks for it.</summary>
        private sealed class Host : ICalendarHost
        {
            private readonly CalendarDirector _director;

            public Host(CalendarDirector director)
            {
                _director = director;
            }

            public int Today => TimeHelper.CurrentDay;

            public int Hour => TimeHelper.CurrentHour;

            public int YearDays => GameCalendar.DaysPerYear;

            public void Look(int month, List<CalendarEntry> into)
            {
                if (_director == null)
                {
                    return;
                }
                _director.Look(month);
                into.AddRange(_director.List.Items);
            }

            public void Plan(int day, List<PlannerEntry> into)
            {
                PlannerDirector planner = PlannerDirector.Instance;
                if (planner == null)
                {
                    return;
                }
                planner.Look(day, day, true);
                into.AddRange(planner.List.Items);
            }

            public string Clock(int minuteOfDay) => PlannerText.Clock(minuteOfDay);

            public string Money(float amount) => PlannerText.Money(amount);

            // the sky as the camera reads it, the air's degrees as the HUD works them out, in the unit the game is set to
            public bool Weather(out string skyKey, out string skyEnglish, out int celsius, out bool imperial)
            {
                string token = Photos.PhotoShot.Token();
                skyKey = Photos.PhotoShot.Words(token);
                skyEnglish = null;
                celsius = Photos.PhotoShot.Degrees(token);
                imperial = UnitHelper.useImperial;
                return true;
            }

            public string AppName(string appId) => PhoneApp.TitleOf(appId);

            public bool OpenApp(string appId) => PhoneApp.OpenById(appId);

            public void OpenPlanner()
            {
                PlannerDirector.Instance?.Open();
            }

            public void AskTitle(string initial, Action<string> done)
            {
                TextBox.Ask("reallife_planner_note_placeholder", "common_confirm", text => done(text), null, initial ?? string.Empty, false, PlannerStore.TitleLength);
            }

            public void Notice(string key)
            {
                UI.Notification.Notifications.ShowError(key, null, false);
            }
        }
    }
}
