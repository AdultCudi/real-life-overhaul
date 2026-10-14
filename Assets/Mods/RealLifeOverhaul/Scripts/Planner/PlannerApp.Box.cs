using System.Collections.Generic;
using Extensions;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Planner
{
    /// <summary>
    /// The Planner's white box (GameUi.InfoBox): a day at a glance, or one entry. Each page is written in sections,
    /// one method each, in the box's own look: a header (Title, Chip), sections of rows (Section, Row, RowPair), sentences
    /// in a tone and notes, the reminder's selector and the buttons. A page is written when the app opens and
    /// when the player clicks; nothing here runs per frame.
    /// </summary>
    public sealed partial class PlannerApp
    {
        private const string GuideTitle = "reallife_meta_title_planner";   // the Guide's page the day and an entry point to at their foot

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

        // A moment as the box writes it: the hour on the day that is talked about, the day and the hour on another.
        private static string At(int minutes, int day, int today)
        {
            int on = PlannerTime.DayOf(minutes);
            return on == day ? PlannerText.Clock(PlannerTime.OfDay(minutes)) : PlannerText.Day(on, today) + ", " + PlannerText.Clock(PlannerTime.OfDay(minutes));
        }

        // ================================================================ a day at a glance

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

        // The first entry of the day still to come, and when to leave for it.
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
            int travel;
            bool here;
            if (first.State == PlannerState.Upcoming && Way(first, out travel, out here) && !here)
            {
                int leave = PlannerRules.LeaveBy(first, travel);
                box.Row("reallife_planner_way_leave", leave <= now ? PlannerText.T("reallife_planner_way_now") : At(leave, day, today), leave <= now ? CellTone.Red : CellTone.Plain);
            }
        }

        // The stretches of free time between the day's entries.
        private void DayFree(InfoBox box, List<PlannerEntry> items, int day, int today, int now)
        {
            int from = day == today ? PlannerTime.OfDay(now) : 0;
            int total;
            int gaps = PlannerRules.FreeTime(items, day, from, _freeFrom, _freeTo, out total);
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

        // How many pairs of the day's entries cannot both be kept.
        private void DayClashes(InfoBox box, List<PlannerEntry> items, int day)
        {
            int clashes = PlannerLogic.ClashPairs(items, day, PlannerRules.DrivePerMinute);
            box.Row("reallife_planner_glance_clashes", clashes.ToString(), clashes > 0 ? CellTone.Red : CellTone.Plain);
        }

        // The money the sources say falls due on the day, and in the next seven days.
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

        // What is still to do, done and missed on the day, and how much the next seven days hold.
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

        // The game's calendar: the day of the week, the year and the next one's first day.
        private static void DayCalendar(InfoBox box, int today)
        {
            box.Headline("reallife_planner_calendar");
            box.Row("reallife_planner_calendar_week", PlannerText.T("reallife_planner_calendar_week_value", "day", (PlannerTime.Weekday(today) + 1).ToString()));
            int perYear = SaveGameManager.Current != null ? GameCalendar.DaysPerYear : 0;
            if (perYear > 0)
            {
                int year = PlannerTime.Year(today, perYear);
                int next = PlannerTime.NextYearStart(today, perYear);
                box.Row("reallife_planner_calendar_year", PlannerText.T("reallife_planner_calendar_year_value", "year", (year + 1).ToString(), "days", perYear.ToString()));
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

        // ================================================================ one entry

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

        // Its title, where it stands, and what it clashes with.
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

        // When and where it is, what it is, what it costs or pays and what follows if it is missed.
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

        // When to leave for its place, by the way the player goes, and the way both by road and on foot.
        private static void EntryWay(InfoBox box, PlannerEntry e, int today)
        {
            if (e.State != PlannerState.Upcoming || e.AllDay)
            {
                return;
            }
            float metres;
            if (!PlannerGame.MetresTo(e, out metres))
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

        // The way to an entry's place by the player's own way (by car if they have one, on foot if not), and whether
        // they are there already. False if the place or the player is not known.
        private static bool Way(PlannerEntry e, out int travel, out bool here)
        {
            travel = 0;
            here = false;
            float metres;
            if (!PlannerGame.MetresTo(e, out metres))
            {
                return false;
            }
            here = !(metres > PlannerRules.HereMetres);
            travel = PlannerRules.TravelMinutes(metres, PlannerGame.ByCar() ? PlannerRules.DrivePerMinute : PlannerRules.WalkPerMinute);
            return true;
        }

        // The one reminder it can have. One of the player's own entries that repeats keeps its reminder for every time
        // it comes (PlannerStore.Arm), but "when to leave" for this time only, from where the player is now.
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
            int travel;
            bool here;
            bool leave = !e.AllDay && Way(e, out travel, out here) && !here;
            ReminderLead set = PlannerStore.LeadOf(id);
            if (series != null && set != ReminderLead.LeaveBy)
            {
                set = series.Lead;
            }
            leave |= set == ReminderLead.LeaveBy;   // (one set while the place was known stays shown as it is)
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
                PlannerStore.SetReminder(id, 0, 0, ReminderLead.None, 0);   // (a "when to leave" set for this time goes)
                series.Lead = lead;
                PlannerStore.Arm(series, now);
                return;
            }
            int travel = 0;
            bool here;
            if (lead == ReminderLead.LeaveBy)
            {
                Way(live, out travel, out here);   // from where the player is as they set it; told again when it goes out
            }
            if (!PlannerStore.SetReminder(id, live.Day, live.Deadline && live.End >= 0 ? live.End : live.Start, lead, now, travel))
            {
                Notifications.ShowError("reallife_planner_reminder_too_late", null, false);
                Write();   // the selector goes back to "no reminder"
            }
        }

        // Set destination, and for one of the player's own entries: done, edit, delete.
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

        // As the game's own "Set destination" buttons do (BizManBusiness.SetDestination): the destination guider is
        // pointed at the place, the game's notification says so, and the menu closes.
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
