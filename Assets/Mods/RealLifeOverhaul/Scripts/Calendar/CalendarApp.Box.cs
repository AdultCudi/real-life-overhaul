using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>
    /// The Calendar's white box (GameUi.InfoBox): the chosen day, or one entry of it. Each page is written in sections in
    /// the box's own look: a header (Header, Subtitle, Chip), sections of rows, notes and buttons. The box does not scroll,
    /// so a day shows its first few things of each kind and says how many more the Day tab has. A page is written when the
    /// app opens and when the player clicks; nothing here runs per frame.
    /// </summary>
    public sealed partial class CalendarApp
    {
        private const int RowsOfAKind = 3;   // rows of one kind a day's page shows: the Day tab has them all

        private void WriteBox(int today)
        {
            InfoBox box = _shell.Box;
            box.Clear();
            CalendarEntry picked = Picked();
            if (picked != null)
            {
                WriteEntry(box, picked, today);
            }
            else
            {
                _pick = null;
                WriteDay(box, _chosen, today);
            }
            box.Done();
        }

        // ================================================================ a day

        private void WriteDay(InfoBox box, int day, int today)
        {
            int year = GameCalendar.DaysPerYear;
            int first = CalendarRules.MonthStart(_month, year), last = CalendarRules.MonthEnd(_month, year);
            box.Header(Date(day, year));
            box.Subtitle(CalendarText.T("reallife_calendar_subtitle", "day", day.ToString(), "month", (CalendarRules.MonthInYear(_month, year) + 1).ToString(),
                "months", CalendarRules.MonthsIn(year).ToString()));
            if (day == today)
            {
                box.Chip(CalendarText.T("common_today"), CellTone.Yellow);
            }
            if (day == last)
            {
                box.Chip(CalendarText.T("reallife_calendar_chip_month_end"));
            }
            List<CalendarEntry> items = _director.List.Items;
            int shown = Kind(box, items, day, CalendarGroup.Money, "reallife_calendar_sec_money")
                        + Kind(box, items, day, CalendarGroup.Time, "reallife_calendar_sec_time")
                        + Kind(box, items, day, CalendarGroup.City, "reallife_calendar_sec_city");
            int all = MonthViewRules.CountOn(items, day);
            if (all == 0)
            {
                box.Line(CalendarText.T("reallife_calendar_day_none"), CellTone.Plain);
            }
            else if (all > shown)
            {
                box.Note(CalendarText.T("reallife_calendar_day_more", "count", (all - shown).ToString()));
            }
            DayButtons(box, all, today, year);
            MonthSection(box, first, last, today);
            Nights(box);
            Meta.Guide.Point(box, GuideTitle);
        }

        // "Wednesday 4 March": the game's weekday, the day of the month and its name.
        private string Date(int day, int year)
        {
            int month = CalendarRules.MonthOf(day, year);
            return CalendarText.T("reallife_calendar_date", "weekday", CalendarText.Weekday(day), "day", (day - CalendarRules.MonthStart(month, year) + 1).ToString(),
                "month", CalendarText.Month(CalendarRules.MonthInYear(month, year)));
        }

        // One kind of the day's entries under its heading, the first few in time order. Returns how many were written.
        private static int Kind(InfoBox box, List<CalendarEntry> items, int day, CalendarGroup group, string headingKey)
        {
            int written = 0;
            for (int i = 0; i < items.Count && written < RowsOfAKind; i++)
            {
                CalendarEntry e = items[i];
                if (e.Day != day || e.Group != group)
                {
                    continue;
                }
                if (written == 0)
                {
                    box.Section(headingKey);
                }
                written++;
                box.RowWords(e.Title ?? string.Empty, Value(e), e.Group == CalendarGroup.Money && e.Sign > 0 ? CellTone.Green : CellTone.Plain);
            }
            return written;
        }

        // What a row of the box says at its right: the amount for money with one, else the source's words, else the hour.
        private static string Value(CalendarEntry e)
        {
            if (e.Group == CalendarGroup.Money && e.Amount > 0f && e.Sign != 0)
            {
                return Amount(e);
            }
            if (!string.IsNullOrEmpty(e.Value))
            {
                return e.Value;
            }
            return e.Start >= 0 ? CalendarText.Clock(e.Start) : string.Empty;
        }

        // The whole day on the Day tab, and back to today's month from another.
        private void DayButtons(InfoBox box, int count, int today, int year)
        {
            bool other = CalendarRules.MonthOf(today, year) != _month;
            if ((count == 0 || _tab == DayTab) && !other)
            {
                return;
            }
            var row = box.ButtonRow();
            if (count > 0 && _tab == MonthTab)
            {
                InfoBox.Button(row, InfoBox.Positive, "reallife_calendar_see_day", () => ShowTab(DayTab));
            }
            if (other)
            {
                InfoBox.Button(row, InfoBox.Additional, "reallife_calendar_this_month", ToToday);
            }
        }

        // The month: the day it ends on, how many are left, and the money set for it with a known amount.
        private void MonthSection(InfoBox box, int first, int last, int today)
        {
            box.Section("reallife_calendar_sec_month");
            int left = MonthViewRules.DaysLeft(today, last);
            string ends = CalendarText.T("reallife_calendar_ends_value", "day", last.ToString(), "weekday", CalendarText.Weekday(last));
            if (today >= first && left > 0)
            {
                ends = CalendarText.T("reallife_calendar_ends_left", "ends", ends, "days", left.ToString());
            }
            box.Row("reallife_calendar_row_ends", ends, today >= first && left == 0 ? CellTone.Yellow : CellTone.Plain);
            float moneyIn = 0f, moneyOut = 0f;
            List<CalendarEntry> items = _director.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                CalendarEntry e = items[i];
                if (e.Group == CalendarGroup.Money && e.Day >= System.Math.Max(first, today))
                {
                    moneyIn += e.Sign > 0 ? e.Amount : 0f;
                    moneyOut += e.Sign < 0 ? e.Amount : 0f;
                }
            }
            if (moneyIn > 0f || moneyOut > 0f)
            {
                box.Row("reallife_calendar_row_still_set", CalendarText.T("reallife_calendar_in_out", "in", CalendarText.Money(moneyIn), "out", CalendarText.Money(moneyOut)));
            }
        }

        // What moves every night, whatever the day: not marked on the days, said once here.
        private void Nights(InfoBox box)
        {
            List<NightLine> nights = _director.List.Nights;
            if (nights.Count == 0)
            {
                return;
            }
            box.Section("reallife_calendar_sec_night");
            for (int i = 0; i < nights.Count; i++)
            {
                NightLine n = nights[i];
                box.RowWords(n.Title ?? string.Empty, (n.Sign > 0 ? "+" : string.Empty) + CalendarText.Money(n.Amount), n.Sign > 0 ? CellTone.Green : CellTone.Plain);
            }
            box.Note(CalendarText.T("reallife_calendar_night_note"));
        }

        // ================================================================ one entry

        private void WriteEntry(InfoBox box, CalendarEntry e, int today)
        {
            int year = GameCalendar.DaysPerYear;
            box.Header(e.Title ?? string.Empty);
            box.Subtitle(Date(e.Day, year) + (e.Start >= 0 ? ", " + CalendarText.Clock(e.Start) : string.Empty));
            if (e.Day == today)
            {
                box.Chip(CalendarText.T("common_today"), CellTone.Yellow);
            }
            box.Section("reallife_calendar_sec_entry");
            box.Row("reallife_calendar_row_kind", CalendarText.T(KindKey(e)));
            string value = Value(e);
            if (!string.IsNullOrEmpty(value))
            {
                box.Row("reallife_calendar_row_value", value, e.Group == CalendarGroup.Money && e.Sign > 0 ? CellTone.Green : CellTone.Plain);
            }
            string app = AppName(e.App);
            if (app != null)
            {
                box.Row("reallife_calendar_row_app", app);
            }
            var row = box.ButtonRow();
            if (app != null)
            {
                string id = e.App;
                var button = InfoBox.Button(row, InfoBox.Positive, "reallife_calendar_open", () => OpenApp(id));
                var words = button != null ? button.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
                UiClone.Words(words, CalendarText.T("reallife_calendar_open_app", "app", app));
            }
            InfoBox.Button(row, InfoBox.Additional, "reallife_calendar_back", Back);
            Meta.Guide.Point(box, GuideTitle);
        }

        // Money in, money out, money (neither known), your time, the city.
        private static string KindKey(CalendarEntry e)
        {
            switch (e.Group)
            {
                case CalendarGroup.Money: return e.Sign > 0 ? "reallife_calendar_kind_in" : e.Sign < 0 ? "reallife_calendar_kind_out" : "reallife_calendar_kind_money";
                case CalendarGroup.City: return "reallife_calendar_kind_city";
                default: return "reallife_calendar_kind_time";
            }
        }

        // The entry's app, opened as its own button opens it. A notice says so when it is not on the phone.
        private static void OpenApp(string id)
        {
            if (!PhoneApp.OpenById(id))
            {
                CalendarDirector.Notice("reallife_calendar_app_gone");
            }
        }
    }
}
