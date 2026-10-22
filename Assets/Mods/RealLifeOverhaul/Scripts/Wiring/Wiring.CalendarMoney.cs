using System;
using Localizor;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Utilities;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The calendar's days of the player's own bills, each read from the feature's own schedule and changed in nothing:
        ///
        ///   power and water   the day the month's bills are sent (Utilities: a month of the calendar's length from the
        ///                     utilities' own first day, UtilitiesState.MonthStart), each due some days later; a bill
        ///                     already sent has its due day from the Planner's source
        ///   cards and loans   a card's statement every so many days from its next one (CreditAccount.NextDay) and the
        ///                     day its minimum is taken (DueDay); the day the private lender wants its money
        ///   cars              the day each car's insurance renews (CarRecord.InsuredUntil, a month of cover at a time)
        ///                     and the day its registration runs out (a year from RegisteredDay)
        ///   web shops         the day each shop's hosting is taken (WebShop.HostDue, then every month)
        ///   the paper         the morning each week of the City Ledger delivered is paid (LedgerState.PaidUntil)
        ///
        /// Asked only when the calendar is shown.
        /// </summary>
        internal static class CalendarMoney
        {
            internal static void Add(CalendarDirector calendar)
            {
                calendar.AddSource(Bills);
                calendar.AddSource(Cards);
                calendar.AddSource(Cars);
                calendar.AddSource(Hosting);
                calendar.AddSource(Paper);
            }

            private static readonly UtilityBills Bills = new UtilityBills();
            private static readonly CardDays Cards = new CardDays();
            private static readonly CarDays Cars = new CarDays();
            private static readonly WebHosting Hosting = new WebHosting();
            private static readonly PaperDays Paper = new PaperDays();

            /// <summary>The day each month's power, water and internet bills are sent (UtilitiesDirector.Day: a month from the last).</summary>
            private sealed class UtilityBills : ICalendarSource
            {
                public string Id => "bills";

                public bool Available => Settings.Utilities && !UtilitiesDirector.Failed && UtilitiesDirector.Instance != null && UtilitiesDirector.Instance.State != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    UtilitiesState state = UtilitiesDirector.Instance.State;
                    bool open = false;
                    for (int i = 0; i < state.Accounts.Count && !open; i++)
                    {
                        open = state.Accounts[i].State != SupplyState.Closed;
                    }
                    if (!open)
                    {
                        return;
                    }
                    int month = GameCalendar.MonthDays;
                    for (int day = MonthViewRules.FirstFrom(state.MonthStart + month, month, fromDay); day <= toDay; day += month)
                    {
                        CalendarEntry e = into.Add(Id, 1, day, CalendarGroup.Money, day);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Sign = -1;
                        e.Title = CalendarText.T("reallife_calendar_bills_title");
                        e.Value = CalendarText.T("reallife_calendar_bills_value", "day", (day + UtilitiesRules.DueIn(month)).ToString());
                        e.App = UtilitiesDirector.AppId;
                    }
                }
            }

            /// <summary>A card's statements and the day its minimum is taken; the private lender's due day (Economy: AccountRules, SharkRules).</summary>
            private sealed class CardDays : ICalendarSource
            {
                public string Id => "credit";

                public bool Available => Settings.Economy && CreditLedger.Ready && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    CreditFile file = CreditLedger.File;
                    int today = SaveGameManager.Current.Day;
                    for (int i = 0; i < file.Accounts.Count; i++)
                    {
                        CreditAccount a = file.Accounts[i];
                        if (a.Closed || a.Frozen || a.Test || a.Kind != ProductKind.CreditCard)
                        {
                            continue;
                        }
                        int next = a.NextDay > 0 ? a.NextDay : today + ProductCatalogue.CardStatementDays;
                        for (int day = MonthViewRules.FirstFrom(next, ProductCatalogue.CardStatementDays, fromDay); day <= toDay; day += ProductCatalogue.CardStatementDays)
                        {
                            CalendarEntry e = into.Add(Id, 1, a.Id, CalendarGroup.Money, day);
                            if (e != null)
                            {
                                e.Title = CalendarText.T("reallife_calendar_card_statement_title", "card", CreditDesk.Name(a));
                                e.Value = CalendarText.T("reallife_calendar_card_statement_value", "days", ProductCatalogue.CardGraceDays.ToString());
                                e.App = BanksDirector.AppId;
                            }
                        }
                        if (a.DueDay >= today && a.MinimumLeft > 0)
                        {
                            CalendarEntry e = into.Add(Id, 2, a.Id, CalendarGroup.Money, a.DueDay);
                            if (e != null)
                            {
                                e.Sign = -1;
                                e.Amount = a.MinimumLeft;
                                e.Title = CalendarText.T("reallife_calendar_card_minimum_title", "card", CreditDesk.Name(a));
                                e.App = BanksDirector.AppId;
                            }
                        }
                    }
                    SharkDebt shark = file.Shark;
                    if (shark.Active && !shark.Test)
                    {
                        int due = Math.Max(today, shark.DueDay);
                        CalendarEntry e = into.Add(Id, 3, 0, CalendarGroup.Money, due);
                        if (e != null)
                        {
                            e.Sign = -1;
                            e.Amount = SharkRules.OwedOnDueDay(shark.Borrowed) > shark.Owed && today < shark.DueDay
                                ? shark.Owed + SharkRules.ADay(shark.Borrowed) * (shark.DueDay - today) : shark.Owed;
                            e.Title = CalendarText.T("reallife_calendar_lender_title");
                            e.App = EconomyDirector.AppId;
                        }
                    }
                }
            }

            /// <summary>Each car's insurance renewal and the day its registration runs out (Transport: CarOwnership's day, CarRules).</summary>
            private sealed class CarDays : ICalendarSource
            {
                public string Id => "cars";

                public bool Available => Settings.CarOwnership && CarGarage.Loaded && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    GameInstance save = SaveGameManager.Current;
                    int today = save.Day, year = GameCalendar.DaysPerYear, policy = CarRules.PolicyDays;
                    for (int i = 0; save.VehicleInstances != null && i < save.VehicleInstances.Count; i++)
                    {
                        VehicleInstance car = save.VehicleInstances[i];
                        if (car == null || car.VehicleType == null || !car.VehicleType.IsMotorVehicle || !CarGarage.Has(car.id) || Dealerships.Stock(car) || Family.PartnerCar.Is(car))
                        {
                            continue;   // as CarOwnership's day reads them: a car of the player's with a record of its own
                        }
                        CarRecord record = CarGarage.For(car.id, today);   // (it has one: nothing is made)
                        string name = car.vehicleTypeName.GetLocalization();
                        if (record.InsuredUntil > 0 && policy > 0)
                        {
                            float premium = Mathf.Round(CarRules.Premium(car.VehicleType.price, record.Claims) * (1f + GarageBridges.InsuranceOf(car.id)));
                            for (int day = MonthViewRules.FirstFrom(Math.Max(today, record.InsuredUntil + 1), policy, fromDay); day <= toDay; day += policy)
                            {
                                CalendarEntry e = into.Add(Id, 1, i, CalendarGroup.Money, day);
                                if (e != null)
                                {
                                    e.Sign = -1;
                                    e.Amount = premium;
                                    e.Title = CalendarText.T("reallife_calendar_car_insurance_title", "car", name);
                                }
                            }
                        }
                        if (!Settings.CarEasy && record.RegisteredDay > 0)
                        {
                            int runsOut = record.RegisteredDay + year;
                            CalendarEntry e = into.Add(Id, 2, i, CalendarGroup.Money, Math.Max(today, runsOut));
                            if (e != null)
                            {
                                e.Sign = -1;
                                e.Amount = CarRules.Registration(car.VehicleType.price);
                                e.Title = CalendarText.T(runsOut < today ? "reallife_calendar_car_registration_late_title" : "reallife_calendar_car_registration_title", "car", name);
                            }
                        }
                    }
                }
            }

            /// <summary>The day each web shop's hosting is taken, a month at a time (Economy/Online: WebShop.HostDue).</summary>
            private sealed class WebHosting : ICalendarSource
            {
                public string Id => "hosting";

                public bool Available => Settings.Economy && Settings.EconomyOnline && OnlineDirector.Instance != null && OnlineDirector.Instance.Running && OnlineDirector.Instance.State != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    OnlineDirector director = OnlineDirector.Instance;
                    OnlineState state = director.State;
                    int month = Math.Max(1, director.MonthDays), today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : fromDay;
                    for (int i = 0; i < state.Shops.Count; i++)
                    {
                        WebShop shop = state.Shops[i];
                        int cost = OnlineRules.HostingCost(shop.Quality, Prices.Level);
                        for (int day = MonthViewRules.FirstFrom(Math.Max(today, shop.HostDue), month, fromDay); day <= toDay; day += month)
                        {
                            CalendarEntry e = into.Add(Id, 1, i, CalendarGroup.Money, day);
                            if (e != null)
                            {
                                e.Sign = -1;
                                e.Amount = cost;
                                e.Title = CalendarText.T("reallife_calendar_hosting_title", "shop", shop.Name);
                                e.App = OnlineDirector.AppId;
                            }
                        }
                    }
                }
            }

            /// <summary>The morning each week of the City Ledger delivered is paid, and the last morning of one cancelled (Ledger: LedgerRules.Morning).</summary>
            private sealed class PaperDays : ICalendarSource
            {
                public string Id => "paper";

                public bool Available => Settings.SocialNewspaper && LedgerDirector.Instance != null && LedgerDirector.Instance.State != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    LedgerDirector director = LedgerDirector.Instance;
                    LedgerState state = director.State;
                    if (!state.Subscribed || state.PaidUntil <= 0)
                    {
                        return;
                    }
                    if (state.Cancelled)
                    {
                        CalendarEntry last = into.Add(Id, 2, 0, CalendarGroup.Time, state.PaidUntil);
                        if (last != null)
                        {
                            last.Start = LedgerCatalogue.DeliveryHour * 60;
                            last.Title = CalendarText.T("reallife_calendar_paper_last_title");
                            last.Value = CalendarText.Clock(last.Start);
                        }
                        return;
                    }
                    int week = LedgerCatalogue.WeekDays;
                    for (int day = MonthViewRules.FirstFrom(state.PaidUntil + 1, week, fromDay); day <= toDay; day += week)
                    {
                        CalendarEntry e = into.Add(Id, 1, day, CalendarGroup.Money, day);
                        if (e != null)
                        {
                            e.Start = LedgerCatalogue.DeliveryHour * 60;
                            e.Sign = -1;
                            e.Amount = director.WeekPrice;
                            e.Title = CalendarText.T("reallife_calendar_paper_title");
                        }
                    }
                }
            }
        }
    }
}
