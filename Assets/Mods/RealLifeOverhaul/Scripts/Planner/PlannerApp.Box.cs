using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using Streets;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Planner
{
    /// <summary>The Planner's box part: a day at a glance or one entry, written on opening and on a click.</summary>
    public sealed partial class PlannerApp
    {
        private const string GuideTitle = "reallife_meta_title_planner";   // the Guide's page pointed to at the foot

        private readonly int[] _freeFrom = new int[PlannerRules.MostGaps], _freeTo = new int[PlannerRules.MostGaps];

        private void Write()
        {
            InfoBox box = _shell.Box;
            box.Clear();
            switch (_page)
            {
                case Page.Entry:
                    PlannerEntry entry = Selected;
                    if (entry != null)
                    {
                        WriteEntry(box, entry);
                        break;
                    }
                    _page = Page.Glance;
                    WriteDay(box, _glanceDay);
                    break;
                case Page.Note:
                    WriteNote(box);
                    break;
                case Page.Settings:
                    WriteSettings(box);
                    break;
                default:
                    WriteDay(box, _glanceDay);
                    break;
            }
            box.Done();
        }

        // the hour, with the day too when it is not 'day'
        private static string At(int minutes, int day, int today)
        {
            int on = PlannerTime.DayOf(minutes);
            return on == day ? PlannerText.Clock(PlannerTime.OfDay(minutes)) : PlannerText.Day(on, today) + ", " + PlannerText.Clock(PlannerTime.OfDay(minutes));
        }

        private void WriteDay(InfoBox box, int day)
        {
            int today = TimeHelper.CurrentDay;
            int now = PlannerDirector.Now();
            List<PlannerEntry> items = _director.List.Items;
            box.Title(day == today ? PlannerText.Date(today) : PlannerText.Day(day, today));
            DayFirst(box, items, day, today, now);
            DayFree(box, items, day, today, now);
            DayClashes(box, items, day);
            DayMoney(box, items, day, today);
            DayCounts(box, items, day, today);
            if (day == today)
            {
                DayCalendar(box, today);
            }
            DayButtons(box, day);
            Meta.Guide.Point(box, GuideTitle);
        }

        private void DayFirst(InfoBox box, List<PlannerEntry> items, int day, int today, int now)
        {
            PlannerEntry first = PlannerRules.FirstOf(items, day);
            box.Section("reallife_ibox_sec_next");
            if (first == null)
            {
                box.Row("reallife_planner_glance_first", PlannerText.T("reallife_planner_glance_first_none"));
                return;
            }
            box.Row("reallife_planner_glance_first", first.Title + ", " + PlannerText.When(first));
            if (first.State == PlannerState.Upcoming && Way(first, out int travel, out bool here) && !here)
            {
                int leave = PlannerRules.LeaveBy(first, travel);
                box.Row("reallife_planner_way_leave", leave <= now ? PlannerText.T("reallife_planner_way_now") : At(leave, day, today), leave <= now ? CellTone.Red : CellTone.Plain);
            }
        }

        private void DayFree(InfoBox box, List<PlannerEntry> items, int day, int today, int now)
        {
            int from = day == today ? PlannerTime.OfDay(now) : 0;
            int gaps = PlannerRules.FreeTime(items, day, from, _freeFrom, _freeTo, out _);
            string words;
            if (gaps == 0)
            {
                words = PlannerText.T("reallife_planner_glance_free_none");
            }
            else
            {
                words = string.Empty;
                for (int g = 0; g < gaps; g++)
                {
                    words += (g > 0 ? ", " : string.Empty) + PlannerText.T("reallife_planner_glance_free_span", "from", PlannerText.Clock(_freeFrom[g]), "to", PlannerText.Clock(_freeTo[g]));
                }
            }
            box.Row("reallife_planner_glance_free", words, gaps == 0 ? CellTone.Yellow : CellTone.Plain);
        }

        private void DayClashes(InfoBox box, List<PlannerEntry> items, int day)
        {
            int clashes = PlannerLogic.ClashPairs(items, day, PlannerRules.DrivePerMinute);
            box.Row("reallife_planner_glance_clashes", clashes.ToString(), clashes > 0 ? CellTone.Red : CellTone.Plain);
        }

        // due on the day, and in the next seven days
        private static void DayMoney(InfoBox box, List<PlannerEntry> items, int day, int today)
        {
            float onDay = PlannerRules.OwedOn(items, day, day);
            float week = PlannerRules.OwedOn(items, today + 1, today + UpcomingDays);
            if (onDay > 0f || week > 0f)
            {
                box.Section("reallife_ibox_sec_money");
            }
            if (onDay > 0f)
            {
                box.Row(day == today ? "reallife_planner_glance_due_today" : "reallife_planner_glance_due_day", PlannerText.Money(onDay), CellTone.Yellow);
            }
            if (week > 0f)
            {
                box.Row("reallife_planner_glance_due_week", PlannerText.Money(week));
            }
        }

        private static void DayCounts(InfoBox box, List<PlannerEntry> items, int day, int today)
        {
            int toDo = 0, done = 0, missed = 0, week = 0;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (e.Day != day)
                {
                    week += e.Day > today ? 1 : 0;
                }
                else if (e.State == PlannerState.Done)
                {
                    done++;
                }
                else if (e.State == PlannerState.Missed)
                {
                    missed++;
                }
                else if (e.State != PlannerState.Over)
                {
                    toDo++;
                }
            }
            box.Section("reallife_ibox_sec_totals");
            if (day != today)
            {
                box.Row("reallife_planner_glance_planned", toDo.ToString());
                return;
            }
            box.Row("reallife_planner_glance_todo", toDo.ToString());
            if (done > 0 || missed > 0)
            {
                box.RowPair("reallife_planner_glance_done", done.ToString(), "reallife_planner_glance_missed", missed.ToString());
            }
            box.Row("reallife_planner_glance_week", week.ToString());
        }

        private static void DayCalendar(InfoBox box, int today)
        {
            box.Headline("reallife_planner_calendar");
            box.Row("reallife_planner_calendar_week", PlannerText.T("reallife_planner_calendar_week_value", "day", (PlannerTime.Weekday(today) + 1).ToString()));
            int perYear = SaveGameManager.Current != null ? GameCalendar.DaysPerYear : 0;
            if (perYear > 0)
            {
                int year = CalendarRules.YearOf(today, perYear);
                int next = CalendarRules.NextYearStart(today, perYear);
                box.Row("reallife_planner_calendar_year", PlannerText.T("reallife_planner_calendar_year_value", "year", year.ToString(), "days", perYear.ToString()));
                box.Row("reallife_planner_calendar_new_year", PlannerText.Date(next));
            }
        }

        private void DayButtons(InfoBox box, int day)
        {
            box.EndSection();
            box.Note(PlannerText.T(_tab == WeekTab ? "reallife_planner_glance_hint_week" : "reallife_planner_glance_hint"));
            Transform row = box.ButtonRow();
            InfoBox.Button(row, InfoBox.Positive, "reallife_planner_add_entry", () => EditNote(null, day));
        }

        private void WriteEntry(InfoBox box, PlannerEntry e)
        {
            int today = TimeHelper.CurrentDay;
            EntryHead(box, e);
            EntryFacts(box, e, today);
            EntryWay(box, e, today);
            EntryReminder(box, e);
            EntryButtons(box, e);
            Meta.Guide.Point(box, GuideTitle);
        }

        private void EntryHead(InfoBox box, PlannerEntry e)
        {
            box.Title(e.Title);
            string status = PlannerText.StatusKey(e);
            if (status != null)
            {
                box.Chip(PlannerText.T(status), e.State == PlannerState.Missed ? CellTone.Red : e.State == PlannerState.Done || e.State == PlannerState.Now ? CellTone.Green
                    : e.Clash ? CellTone.Yellow : CellTone.Plain);
            }
            PlannerEntry other = e.Clash && e.State != PlannerState.Done ? e.ClashWith : null;
            if (other != null)
            {
                string with = other.Title + ", " + (other.Day == e.Day ? PlannerText.When(other) : PlannerText.Day(other.Day, TimeHelper.CurrentDay) + ", " + PlannerText.When(other));
                box.Line(PlannerText.T(e.ClashReach ? "reallife_planner_detail_clash_reach" : "reallife_planner_detail_clash", "title", with), CellTone.Red);
            }
        }

        private static void EntryFacts(InfoBox box, PlannerEntry e, int today)
        {
            box.Section("reallife_ibox_sec_when_where");
            box.Row("reallife_planner_detail_when", PlannerText.WhenLong(e, today));
            if (!string.IsNullOrEmpty(e.Where))
            {
                box.Row("reallife_planner_detail_where", e.Where);
            }
            if (!string.IsNullOrEmpty(e.What) || !string.IsNullOrEmpty(e.Money) || !string.IsNullOrEmpty(e.IfMissed))
            {
                box.Section("reallife_ibox_sec_what");
            }
            if (!string.IsNullOrEmpty(e.What))
            {
                box.Line(e.What);
            }
            if (!string.IsNullOrEmpty(e.Money))
            {
                box.Line(e.Money);
            }
            if (!string.IsNullOrEmpty(e.IfMissed))
            {
                box.Line(e.IfMissed, CellTone.Yellow);
            }
        }

        private static void EntryWay(InfoBox box, PlannerEntry e, int today)
        {
            if (e.State != PlannerState.Upcoming || e.AllDay)
            {
                return;
            }
            if (!PlannerGame.MetresTo(e, out float metres))
            {
                return;
            }
            box.Section("reallife_ibox_sec_getting_there");
            if (!(metres > PlannerRules.HereMetres))
            {
                box.Line(PlannerText.T("reallife_planner_way_here"), CellTone.Green);
                return;
            }
            bool car = PlannerGame.ByCar();
            int drive = PlannerRules.TravelMinutes(metres, PlannerRules.DrivePerMinute), walk = PlannerRules.TravelMinutes(metres, PlannerRules.WalkPerMinute);
            int leave = PlannerRules.LeaveBy(e, car ? drive : walk);
            int now = PlannerDirector.Now();
            box.Row("reallife_planner_way_leave", leave <= now ? PlannerText.T("reallife_planner_way_now") : At(leave, e.Day, today), leave <= now ? CellTone.Red : CellTone.Plain);
            if (leave < now)
            {
                box.Line(PlannerText.T("reallife_planner_way_late", "late", PlannerText.Span(now - leave)), CellTone.Red);
            }
            box.Note(PlannerText.T(car ? "reallife_planner_way_about_car" : "reallife_planner_way_about_foot", "drive", PlannerText.Span(drive), "walk", PlannerText.Span(walk)));
        }

        // by car if the player has one, on foot if not; false if the place or the player is not known
        private static bool Way(PlannerEntry e, out int travel, out bool here)
        {
            travel = 0;
            here = false;
            if (!PlannerGame.MetresTo(e, out float metres))
            {
                return false;
            }
            here = !(metres > PlannerRules.HereMetres);
            travel = PlannerRules.TravelMinutes(metres, PlannerGame.ByCar() ? PlannerRules.DrivePerMinute : PlannerRules.WalkPerMinute);
            return true;
        }

        // A repeating note keeps its reminder for every time (PlannerStore.Arm), but "when to leave" for this time only.
        private void EntryReminder(InfoBox box, PlannerEntry e)
        {
            bool open = e.State == PlannerState.Upcoming || e.State == PlannerState.Now;
            if (!open || !Settings.PlannerReminders || !box.HasSelector)
            {
                return;
            }
            EntryId id = e.Id;
            PlannerNote series = e.Own ? PlannerStore.Note(id.A) : null;
            if (series != null && series.Repeat == PlannerRepeat.Once)
            {
                series = null;
            }
            bool leave = !e.AllDay && Way(e, out _, out bool here) && !here;
            ReminderLead set = PlannerStore.LeadOf(id);
            if (series != null && set != ReminderLead.LeaveBy)
            {
                set = series.Lead;
            }
            leave |= set == ReminderLead.LeaveBy;   // one set while the place was known stays shown
            int count = leave ? PlannerText.Leads.Length : PlannerText.Leads.Length - 1;   // "when to leave" is the last
            box.Headline("reallife_planner_reminder_head");
            box.Selector(count, Mathf.Min(PlannerText.LeadIndex(set), count - 1), i => PlannerText.T(PlannerText.LeadKey(PlannerText.Leads[i])), i => SetLead(id, PlannerText.Leads[i]));
        }

        private void SetLead(EntryId id, ReminderLead lead)
        {
            PlannerEntry live = _director.List.Find(id);
            if (live == null)
            {
                return;
            }
            int now = PlannerDirector.Now();
            PlannerNote series = live.Own ? PlannerStore.Note(id.A) : null;
            if (series != null && series.Repeat != PlannerRepeat.Once && lead != ReminderLead.LeaveBy)
            {
                PlannerStore.SetReminder(id, 0, 0, ReminderLead.None, 0);   // a "when to leave" for this time goes
                series.Lead = lead;
                PlannerStore.Arm(series, now);
                return;
            }
            int travel = 0;
            if (lead == ReminderLead.LeaveBy)
            {
                Way(live, out travel, out _);   // from where the player is now; told again when it goes out
            }
            if (!PlannerStore.SetReminder(id, live.Day, live.Deadline && live.End >= 0 ? live.End : live.Start, lead, now, travel))
            {
                Notifications.ShowError("reallife_planner_reminder_too_late", null, false);
                Write();   // the selector goes back to "no reminder"
            }
        }

        private void EntryButtons(InfoBox box, PlannerEntry e)
        {
            EntryId id = e.Id;
            int day = e.Day;
            Transform row = box.ButtonRow();
            if (e.HasPlace || !string.IsNullOrEmpty(e.Street))
            {
                InfoBox.Button(row, InfoBox.Positive, "common_setdestination", () => SetDestination(id));
            }
            if (e.Own)
            {
                InfoBox.Button(row, InfoBox.Additional, e.Done ? "reallife_planner_note_not_done" : "reallife_planner_note_done", () => ToggleDone(id.A, day));
                Transform second = box.ButtonRow();
                InfoBox.Button(second, InfoBox.Additional, "reallife_planner_note_edit", () => EditNote(PlannerStore.Note(id.A), day));
                InfoBox.Button(second, InfoBox.Negative, "filebrowser_delete", () => DeleteNote(id.A));
            }
            else if (!e.HasPlace && string.IsNullOrEmpty(e.Street))
            {
                InfoBox.Button(row, InfoBox.Additional, "common_close", () => Show(Page.Glance));
            }
        }

        // as the game's BizManBusiness.SetDestination: the guider, the notification, the menu closed
        private void SetDestination(EntryId id)
        {
            PlannerEntry e = _director.List.Find(id);
            if (e == null || SaveGameManager.Current == null || InstanceBehavior<GuidersManager>.Instance == null)
            {
                return;
            }
            string name;
            if (!string.IsNullOrEmpty(e.Street))
            {
                var address = new Address(e.Street, e.Number);
                name = address.ToFormattedString();
                SaveGameManager.Current.customDestination = address;
                GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);
            }
            else
            {
                name = !string.IsNullOrEmpty(e.Where) ? e.Where : e.Title;
                Sprite icon = PlannerText.Icon(e.Category) ?? GameSprites.Get("Icon-SetDestination");
                GuidersManager.SetGuiderTarget(new Vector3(e.X, e.Y, e.Z), name, icon, GuidersManager.GetGuiderColor(DirectionGuiderType.Destination), DirectionGuiderType.Destination);
            }
            Notifications.Show(NotificationType.Success, "notification_destination_set", new Dictionary<string, string> { { "address", name } });
            if (_menu != null)
            {
                _menu.Toggle(false);
            }
        }
    }
}
