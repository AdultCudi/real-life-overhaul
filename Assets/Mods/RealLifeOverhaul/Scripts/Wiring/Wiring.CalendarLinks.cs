using System;
using System.Collections.Generic;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The Calendar's sources: the Planner's dated sources (its alerts are left to the Planner), what each night takes
        /// and pays, and each feature's schedule of money and days (CalendarMoney, CalendarWork, CalendarPeople, CalendarSeasons). Each reads
        /// what its feature keeps and changes nothing; asked only when the calendar is shown.
        /// </summary>
        public static class CalendarLinks
        {
            internal static void Connect()
            {
                CalendarDirector calendar = CalendarDirector.Instance;
                if (calendar == null)
                {
                    return;
                }
                calendar.AddSource(Planned);
                calendar.AddSource(Nightly);
                CalendarMoney.Add(calendar);
                CalendarWork.Add(calendar);
                CalendarPeople.Add(calendar);
                CalendarSeasons.Add(calendar);
            }

            internal static void Disconnect()
            {
                // The sources stay with the calendar's director, which goes with the city.
            }

            private static readonly PlannerDays Planned = new PlannerDays();
            private static readonly NightMoney Nightly = new NightMoney();

            /// <summary>The Planner's dated sources for the calendar's month, each entry written as a day of the calendar.</summary>
            private sealed class PlannerDays : ICalendarSource
            {
                // The Planner's sources that are dates, and the app that deals with each; null for none.
                private static readonly string[] Ids =
                {
                    PlannerStore.NoteSourceId, "tax", "work", "profession", "freelance", "study", "court", "cell", "licence", "family", "social", "travel",
                    "event", "civic", "health", "markets", "listing", "utilities", "newsstand", "ventures", "services", "standing"
                };
                private static readonly string[] Apps =
                {
                    "RealLifePlanner", null, null, "RealLifeCareer", "RealLifeCareer", "RealLifeCampus", null, null, null, null, null, null,
                    null, null, null, "RealLifeStocks", "RealLifeStocks", "RealLifeOverhaulUtilities", "RealLifeNewsstand", "RealLifeVentures", "RealLifeServices", "RealLifeFinance"
                };
                private static readonly Func<string, bool> Wanted = id => Array.IndexOf(Ids, id) >= 0;   // made once

                private readonly PlannerList _list = new PlannerList();

                public string Id => "planner";
                public bool Available => PlannerDirector.Instance != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    PlannerDirector.Instance.LookFor(_list, fromDay, toDay, Wanted);
                    List<PlannerEntry> items = _list.Items;
                    for (int i = 0; i < items.Count; i++)
                    {
                        PlannerEntry p = items[i];
                        CalendarGroup group = p.Category == PlannerCategory.Money ? CalendarGroup.Money : p.Category == PlannerCategory.City ? CalendarGroup.City : CalendarGroup.Time;
                        CalendarEntry e = into.Add(Id + ":" + p.Id.Source, p.Id.A, p.Id.B, group, p.Day);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Start = p.Start >= 0 ? p.Start : p.Deadline && p.End >= 0 ? p.End : -1;
                        e.Title = p.Title;
                        if (p.Owes > 0f)
                        {
                            e.Sign = -1;
                            e.Amount = p.Owes;
                        }
                        e.Value = group == CalendarGroup.Money && !string.IsNullOrEmpty(p.Money) ? p.Money : PlannerText.When(p);
                        int at = Array.IndexOf(Ids, p.Id.Source);
                        e.App = at >= 0 ? Apps[at] : null;
                    }
                }
            }

            /// <summary>What the game's night takes and pays, as the Finance app's Overview adds it up (Books.Tomorrow).</summary>
            private sealed class NightMoney : ICalendarSource
            {
                public string Id => "nightly";
                public bool Available => SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    Obligations o = Books.Tomorrow(CreditLedger.File);
                    into.Night(Id, CalendarText.T("reallife_calendar_night_rent"), -1, o.Rent);
                    into.Night(Id, CalendarText.T("reallife_calendar_night_wages"), -1, o.Wages);
                    into.Night(Id, CalendarText.T("reallife_calendar_night_loans"), -1, o.Loans + o.Accounts);
                    into.Night(Id, CalendarText.T("reallife_calendar_night_fines"), -1, o.Other);
                }
            }
        }
    }
}
