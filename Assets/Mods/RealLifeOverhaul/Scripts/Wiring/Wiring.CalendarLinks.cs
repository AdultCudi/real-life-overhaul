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
        /// The Calendar (Scripts/Calendar) with the rest of the mod. The calendar asks its sources only when it is shown;
        /// every source here reads what its feature already keeps, from its real schedule, and changes nothing:
        ///
        ///   the Planner   the Planner's own sources of dated things (CalendarLinks.PlannerDays): classes and exams, shifts
        ///                 and working days, interviews, court dates, licence dates, family appointments, plans with
        ///                 friends, a hotel checkout, the city's events and election, the game's tax bill, dividends and
        ///                 bonds, bills due, investors' days, the paper online, and the player's own entries. Its alerts
        ///                 (what was missed yesterday, what waits on the player at a business) are left to the Planner.
        ///   every night   what the game takes and pays each night: rent, wages, its loans, the mod's accounts and fines
        ///                 (Economy/Books), the businesses' insurance, the subscriptions to the player's products and a
        ///                 pension
        ///   money         bills sent, card statements, the private lender, car insurance and registration, web hosting,
        ///                 the paper delivered, the tax quarter, the accountant, business cover, licences and claims, a
        ///                 firm's monthly invoices, reviews, profit shares and benefit, the trading bots' statements and
        ///                 the central bank's decisions (Wiring.CalendarMoney.cs, Wiring.CalendarWork.cs)
        ///   people        the birthdays of the people the player knows and of their children, and the week the children
        ///                 cost (Wiring.CalendarPeople.cs)
        ///
        /// The sources are given to the calendar once a city (it keeps them with its director, which goes with the city).
        /// Nothing here has an Update.
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
            }

            internal static void Disconnect()
            {
                // The sources stay with the calendar's director, which goes with the city.
            }

            private static readonly PlannerDays Planned = new PlannerDays();
            private static readonly NightMoney Nightly = new NightMoney();

            /// <summary>
            /// The Planner's sources of dated things, asked for the calendar's month (PlannerDirector.LookFor), each entry
            /// written as a day of the calendar: money with what it costs, the player's time with its hour, the city's
            /// events. Which of the Planner's sources are dates (rather than alerts) and the app each belongs to is the
            /// table below.
            /// </summary>
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

            /// <summary>
            /// What the game's night takes and pays, as the Finance app's Overview adds it up (Economy/Books.Tomorrow): rent,
            /// wages as last night, the game's loans, the mod's own accounts, fines and court debts.
            /// </summary>
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
