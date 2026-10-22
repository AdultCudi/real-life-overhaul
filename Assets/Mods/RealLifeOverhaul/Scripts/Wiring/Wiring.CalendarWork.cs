using System;
using System.Collections.Generic;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The calendar's days of the player's work and businesses, each read from the feature's own schedule and changed
        /// in nothing:
        ///
        ///   the company     the day the tax quarter closes (TaxFile.QuarterFrom, three of the city's months a quarter), the
        ///                   last day of a return waiting, the accountant's monthly fee (AccountantNext), each cover's
        ///                   renewal (Policy.Until), a claim's payout (Claim.PayDay) and each licence's last day; the daily
        ///                   premiums among what moves every night
        ///   the tech firms  each managed service's monthly invoice, thirty days from the last (Contract.LastBilled, as
        ///                   FirmsRules.Work bills it); the subscriptions to the player's products, paid in a thirtieth of the
        ///                   month's each day, among what moves every night
        ///   a profession    the next performance review (Job.ReviewDay and LifeRules.ReviewEvery), the weekly profit
        ///                   share, benefit each week and a pension each night
        ///   the market      each trading bot's monthly statement and fee: the last trading day of the market's month
        ///   the city        the day each of the central bank's rate decisions takes effect (CityModel.DaysToTurn)
        ///
        /// Asked only when the calendar is shown.
        /// </summary>
        internal static class CalendarWork
        {
            internal static void Add(CalendarDirector calendar)
            {
                calendar.AddSource(Company);
                calendar.AddSource(Firms);
                calendar.AddSource(Profession);
                calendar.AddSource(Bots);
                calendar.AddSource(Rates);
            }

            private static readonly CompanyDays Company = new CompanyDays();
            private static readonly FirmDays Firms = new FirmDays();
            private static readonly ProfessionDays Profession = new ProfessionDays();
            private static readonly BotDays Bots = new BotDays();
            private static readonly RateDays Rates = new RateDays();

            private static int Today => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

            /// <summary>The tax quarter, a return due, the accountant, business cover, claims and licences (Economy/Company).</summary>
            private sealed class CompanyDays : ICalendarSource
            {
                public string Id => "company";

                public bool Available => Settings.Economy && Settings.EconomyCompany && CompanyDirector.Instance != null && CompanyDirector.Instance.Running
                                         && CompanyDirector.Instance.State != null && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    CompanyDirector d = CompanyDirector.Instance;
                    CompanyState state = d.State;
                    int today = Today;
                    IReadOnlyList<CompanyShop> shops = d.Shops;
                    if (d.On(CompanyPart.Tax) && state.Tax != null && shops.Count > 0)
                    {
                        Tax(d, state.Tax, fromDay, toDay, today, into);
                    }
                    float premiums = 0f;
                    for (int s = 0; s < shops.Count; s++)
                    {
                        CompanyShop shop = shops[s];
                        if (shop == null || shop.File == null)
                        {
                            continue;
                        }
                        for (int k = 0; d.On(CompanyPart.Cover) && k < shop.File.Cover.Length; k++)
                        {
                            Policy p = shop.File.Cover[k];
                            if (!p.Active)
                            {
                                continue;
                            }
                            premiums += p.From <= today ? p.Premium : 0f;
                            CalendarEntry e = into.Add(Id, 10 + k, s, CalendarGroup.Money, Math.Max(today, p.Until + 1));
                            if (e != null)
                            {
                                e.Title = CalendarText.T("reallife_calendar_cover_title", "cover", CompanyText.Cover(k), "business", shop.Name);
                                e.Value = CalendarText.T("reallife_calendar_cover_value", "amount", CalendarText.Money(p.Premium));
                                e.App = CompanyDirector.AppId;
                            }
                        }
                        for (int k = 0; d.On(CompanyPart.Licences) && k < shop.File.Permits.Length; k++)
                        {
                            Licence l = shop.File.Permits[k];
                            if (l.Until <= 0 || !LicenceRules.Needed(shop.Needs, k))
                            {
                                continue;
                            }
                            CalendarEntry e = into.Add(Id, 20 + k, s, CalendarGroup.Time, l.Until);
                            if (e != null)
                            {
                                e.Title = CalendarText.T(l.Auto ? "reallife_calendar_licence_renews_title" : "reallife_calendar_licence_ends_title", "licence", CompanyText.Licence(k),
                                    "business", shop.Name);
                                e.App = CompanyDirector.AppId;
                            }
                        }
                    }
                    for (int i = 0; d.On(CompanyPart.Cover) && i < state.Claims.Count; i++)
                    {
                        Claim c = state.Claims[i];
                        if (c.Stage != ClaimStage.Filed || c.PayDay <= 0)
                        {
                            continue;
                        }
                        CalendarEntry e = into.Add(Id, 30, c.Id, CalendarGroup.Money, Math.Max(today, c.PayDay));
                        if (e != null)
                        {
                            e.Sign = 1;
                            e.Amount = Math.Max(0, c.Payout);
                            e.Title = CalendarText.T("reallife_calendar_claim_title", "business", c.Name);
                            e.App = CompanyDirector.AppId;
                        }
                    }
                    into.Night(Id, CalendarText.T("reallife_calendar_night_cover"), -1, premiums);
                }

                // The quarter's close, every quarter; a return waiting; the accountant's fee each month.
                private void Tax(CompanyDirector d, TaxFile t, int fromDay, int toDay, int today, CalendarList into)
                {
                    int quarter = Math.Max(1, d.QuarterDays);
                    for (int day = MonthViewRules.FirstFrom(Math.Max(today, t.QuarterFrom + quarter), quarter, fromDay); day <= toDay; day += quarter)
                    {
                        CalendarEntry e = into.Add(Id, 1, day, CalendarGroup.Money, day);
                        if (e != null)
                        {
                            e.Title = CalendarText.T("reallife_calendar_quarter_title");
                            e.Value = CalendarText.T("reallife_calendar_quarter_value");
                            e.App = CompanyDirector.AppId;
                        }
                    }
                    if (t.ReturnWaiting)
                    {
                        CalendarEntry e = into.Add(Id, 2, t.ReturnQuarter, CalendarGroup.Money, Math.Max(today, t.ReturnDue));
                        if (e != null)
                        {
                            e.Title = CalendarText.T("reallife_calendar_return_title", "quarter", t.ReturnQuarter.ToString());
                            e.App = CompanyDirector.AppId;
                        }
                    }
                    if (!d.HasAccountant)
                    {
                        return;
                    }
                    int month = GameCalendar.MonthDays, fee = TaxRules.Fee(t.Accountant, d.Index, d.Hidden() > 0);
                    for (int day = MonthViewRules.FirstFrom(Math.Max(today, t.AccountantNext), month, fromDay); day <= toDay; day += month)
                    {
                        CalendarEntry e = into.Add(Id, 3, day, CalendarGroup.Money, day);
                        if (e != null)
                        {
                            e.Sign = -1;
                            e.Amount = fee;
                            e.Title = CalendarText.T("reallife_calendar_accountant_title", "name", CompanyText.Accountant(t.Accountant));
                            e.App = CompanyDirector.AppId;
                        }
                    }
                }
            }

            /// <summary>The tech firms' monthly invoices for managed services, and the subscriptions to their products each night (Tech/Firms).</summary>
            private sealed class FirmDays : ICalendarSource
            {
                public string Id => "firms";

                public bool Available => Settings.TechSector && TechDirector.Instance != null && TechDirector.Instance.Firms != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    FirmsFile file = TechDirector.Instance.Firms;
                    int level = Prices.Level, today = Today, every = FirmsCatalogue.MonthDays;
                    for (int i = 0; i < file.Contracts.Count; i++)
                    {
                        Tech.Contract w = file.Contracts[i];
                        if (!w.Monthly || w.Stage != (int)WorkStage.Active || file.FirmAt(w.Place) == null)
                        {
                            continue;
                        }
                        // FirmsRules.Work bills a managed service when thirty days have gone since its last bill, its last day included.
                        for (int day = MonthViewRules.FirstFrom(Math.Max(today, w.LastBilled + every), every, fromDay); day <= toDay && day <= w.End; day += every)
                        {
                            CalendarEntry e = into.Add(Id, w.Id, day, CalendarGroup.Money, day);
                            if (e != null)
                            {
                                e.Sign = 1;
                                e.Amount = w.Value * level / 1000 / 100f;
                                e.Title = CalendarText.T("reallife_calendar_invoice_title", "firm", file.FirmAt(w.Place).Name, "client", w.Client);
                                e.App = TechDirector.TechAppId;
                            }
                        }
                    }
                    long cents = 0;
                    for (int i = 0; i < file.Products.Count; i++)
                    {
                        Product p = file.Products[i];
                        if (p.Live && file.FirmAt(p.Place) != null)
                        {
                            cents += FirmsRules.MonthlyRevenue(p) / FirmsCatalogue.MonthDays;   // as FirmsRules.Sell takes it each day
                        }
                    }
                    into.Night(Id, CalendarText.T("reallife_calendar_night_subscriptions"), 1, cents * level / 1000 / 100f);
                }
            }

            /// <summary>A profession's review, its weekly profit share, benefit each week and the pension (Careers: CareersDirector.Life).</summary>
            private sealed class ProfessionDays : ICalendarSource
            {
                public string Id => "profession";

                public bool Available => Settings.CareersJobs && Settings.CareersLife && CareersDirector.Instance != null && CareersDirector.Instance.Work != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    WorkFile w = CareersDirector.Instance.Work;
                    int today = Today, year = GameCalendar.DaysPerYear;
                    Careers.Job j = w.Job;
                    if (j != null)
                    {
                        int last = j.NoticeEnd > 0 ? j.NoticeEnd : toDay;
                        string employer = CareersText.Employer(j.Post.Employer);
                        int review = Math.Max(today, Math.Max(j.ReviewDay + LifeRules.ReviewEvery(year), j.Hired + 1));
                        CalendarEntry e = review <= last ? into.Add(Id, 1, 0, CalendarGroup.Time, review) : null;
                        if (e != null)
                        {
                            e.Title = CalendarText.T("reallife_calendar_review_title", "employer", employer);
                            e.Value = CalendarText.T("reallife_calendar_review_value");
                            e.App = CareersDirector.CareerAppId;
                        }
                        if (LifeRules.ProfitShareBp(j) > 0)
                        {
                            // CareersDirector.WorkDayMore: the share is paid when a whole week more since the hiring day is done.
                            for (int day = MonthViewRules.FirstFrom(j.Hired + 6, 7, Math.Max(today, fromDay)); day <= toDay && day <= last; day += 7)
                            {
                                CalendarEntry s = into.Add(Id, 2, day, CalendarGroup.Money, day);
                                if (s != null)
                                {
                                    s.Sign = 1;
                                    s.Title = CalendarText.T("reallife_calendar_profit_share_title", "employer", employer);
                                    s.App = CareersDirector.CareerAppId;
                                }
                            }
                        }
                    }
                    else if (w.BenefitWeek > 0 && w.BenefitUntil >= fromDay)
                    {
                        for (int day = MonthViewRules.FirstFrom(Math.Max(today, w.BenefitPaid + 7), 7, fromDay); day <= toDay && day <= w.BenefitUntil; day += 7)
                        {
                            CalendarEntry e = into.Add(Id, 3, day, CalendarGroup.Money, day);
                            if (e != null)
                            {
                                e.Sign = 1;
                                e.Amount = Prices.At(w.BenefitWeek);
                                e.Title = CalendarText.T("reallife_calendar_benefit_title");
                                e.App = CareersDirector.CareerAppId;
                            }
                        }
                    }
                    if (w.Retired > 0 && w.Pension > 0)
                    {
                        into.Night(Id, CalendarText.T("reallife_calendar_night_pension"), 1, Math.Min(w.Pension, LifeRules.PensionDay(w.Pension, year, w.RetiredEarly)) / 100f);
                    }
                }
            }

            /// <summary>Each trading bot's monthly statement and fee: at the close of the market month's last trading day (Markets: MarketEngine.BotClose).</summary>
            private sealed class BotDays : ICalendarSource
            {
                public string Id => "bots";

                public bool Available => Settings.Markets && MarketDirector.Instance != null && MarketDirector.Instance.State != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    List<Bot> bots = MarketDirector.Instance.State.Bots;
                    if (bots.Count == 0)
                    {
                        return;
                    }
                    int year = GameCalendar.DaysPerYear;
                    for (int day = fromDay; day <= toDay; day++)
                    {
                        if (!MarketCatalogue.TradingDay(day) || MarketCatalogue.MonthOf(MarketTrading.NextTradingDay(day), year) == MarketCatalogue.MonthOf(day, year))
                        {
                            continue;
                        }
                        for (int b = 0; b < bots.Count; b++)
                        {
                            BotFirm firm = bots[b].Def;
                            CalendarEntry e = firm != null ? into.Add(Id, firm.Id, day, CalendarGroup.Money, day) : null;
                            if (e != null)
                            {
                                e.Start = MarketCatalogue.CloseMinute;
                                e.Sign = -1;
                                e.Title = CalendarText.T("reallife_calendar_bot_statement_title", "firm", firm.Name);
                                e.App = MarketDirector.AppId;
                            }
                        }
                    }
                }
            }

            /// <summary>The day each of the central bank's decisions takes effect, a month apart (Economy/City: CityModel.DaysToTurn).</summary>
            private sealed class RateDays : ICalendarSource
            {
                public string Id => "rates";

                public bool Available => Settings.Economy && Settings.EconomyCity && CityEconomyDirector.Instance != null && CityEconomyDirector.Instance.State != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    CityEconomyDirector director = CityEconomyDirector.Instance;
                    int month = Math.Max(MacroCatalogue.MonthDaysLeast, director.MonthDays);
                    int first = Today + CityModel.DaysToTurn(director.State, month);
                    for (int day = MonthViewRules.FirstFrom(first, month, fromDay); day <= toDay; day += month)
                    {
                        CalendarEntry e = into.Add(Id, 1, day, CalendarGroup.City, day);
                        if (e != null)
                        {
                            e.Title = CalendarText.T("reallife_calendar_rate_title");
                            e.App = EconomyDirector.AppId;
                        }
                    }
                }
            }
        }
    }
}
