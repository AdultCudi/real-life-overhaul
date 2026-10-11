using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Helpers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Planner;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Investors and the market with the rest of the mod (Economy/Ventures/VenturesBridges). Ventures calls no
        /// other feature and none calls it: what they have to do with each other is here, and every answer is read
        /// from what a feature already keeps in public.
        ///
        ///   DayNews      what the market answers to at the day's end:
        ///                  the weather      Events/Weather: the sky as the day ends, the spell rolled for the day
        ///                                   that ended, and the game's own rain
        ///                  city events      Events/EventCalendar: what was on the day that ended
        ///                  the paper        Social/News: a crime, a break-in or a road accident since yesterday
        ///                                   (News.Recent says whether there was one, not how many)
        ///                  a rival's move   Business/BusinessState: a price war or a smear begun yesterday, with the
        ///                                   rival's name where the issue carries one
        ///   Lever        Civic/CivicPolicy.Position: Ventures keeps what it last saw and takes a change as news
        ///   Elections    the number of elections counted (Civic's government state): one more is news
        ///   JobsHeld     Jobs/CareerState: the employers on the CV, the present one included
        ///   Held         custody, prison, a hearing, a driving test or lesson, the police, a bus ride
        ///   the Planner  a source of its own (<see cref="VenturesSource"/>)
        ///
        /// Left at its default on purpose: Climate (steady, a bank rate that never moves). The city's economy
        /// (Scripts/Economy/City) is not in this tree; where it is, one line here joins it on:
        ///     VenturesBridges.Climate = new ClimateFrom(() => (int)CityEconomy.State - 1, () => CityEconomy.BankRate);
        /// (with whatever CityEconomy calls its slowdown, steady and boom, and its bank rate in per cent a year).
        ///
        /// Not linked, because the features show nothing usable in public: how many crimes or accidents a day had
        /// (the paper only says whether there was one of a kind), the paper's own items by kind (News.Today has
        /// finished texts with no kind), and a campaign's polls.
        /// </summary>
        public static class VenturesLinks
        {
            private const int NewsDays = 1;
            private static readonly VenturesSource Source = new VenturesSource();

            internal static void Connect()
            {
                VenturesBridges.DayNews = DayNews;
                VenturesBridges.Lever = Lever;
                VenturesBridges.Elections = Elections;
                VenturesBridges.JobsHeld = JobsHeld;
                VenturesBridges.Held = Held;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                VenturesBridges.Reset();   // (the Planner's director goes with the city, and its sources with it)
            }

            private static void DayNews(List<MarketNews> into)
            {
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    if (save == null)
                    {
                        return;
                    }
                    int ended = save.Day - 1;
                    switch (Events.Weather.Sky)
                    {
                        case Events.Sky.Storm: Add(into, MarketCause.Storm); break;
                        case Events.Sky.Rain: Add(into, MarketCause.Rain); break;
                        default:
                            if (RainHelper.isRaining)
                            {
                                Add(into, MarketCause.Rain);
                            }
                            break;
                    }
                    switch (Events.Weather.SpellOf(ended))
                    {
                        case Events.Spell.HeatWave: Add(into, MarketCause.HeatWave); break;
                        case Events.Spell.ColdSnap: Add(into, MarketCause.ColdSnap); break;
                        case Events.Spell.Ice: Add(into, MarketCause.Ice); break;
                    }
                    switch (Events.EventCalendar.For(ended).Kind)
                    {
                        case Events.EventKind.Market:
                        case Events.EventKind.FarmersMarket: Add(into, MarketCause.StreetMarket); break;
                        case Events.EventKind.Parade:
                        case Events.EventKind.FunRun:
                        case Events.EventKind.Fireworks: Add(into, MarketCause.Parade); break;
                        case Events.EventKind.Protest: Add(into, MarketCause.Protest); break;
                        case Events.EventKind.FilmShoot: Add(into, MarketCause.FilmShoot); break;
                    }
                    Vector3 where;
                    string detail;
                    if (News.Recent(NewsKind.Crime, NewsDays, out where, out detail))
                    {
                        Add(into, MarketCause.Crime);
                    }
                    if (News.Recent(NewsKind.BreakIn, NewsDays, out where, out detail) || News.Recent(NewsKind.CarBreakIn, NewsDays, out where, out detail))
                    {
                        Add(into, MarketCause.BreakIn);
                    }
                    if (News.Recent(NewsKind.Accident, NewsDays, out where, out detail) || News.Recent(NewsKind.PedestrianHit, NewsDays, out where, out detail)
                        || News.Recent(NewsKind.HitAndRun, NewsDays, out where, out detail))
                    {
                        Add(into, MarketCause.Accident);
                    }
                    for (int i = 0; BusinessState.Loaded && i < BusinessState.Issues.Count; i++)
                    {
                        BusinessIssue issue = BusinessState.Issues[i];
                        if (issue.StartDay == ended && (issue.Kind == BusinessCatalogue.Undercut || issue.Kind == BusinessCatalogue.Smear))
                        {
                            into.Add(new MarketNews { Cause = MarketCause.RivalMove, Company = issue.Kind == BusinessCatalogue.Undercut ? issue.Ref : null });
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("VenturesLinks.DayNews", e);
                }
            }

            private static void Add(List<MarketNews> into, MarketCause cause)
            {
                into.Add(new MarketNews { Cause = cause });
            }

            private static int Lever(int number)
            {
                try
                {
                    return number >= 0 && number < CivicCatalogue.Levers ? CivicPolicy.Position((Lever)number) : 0;
                }
                catch (Exception e)
                {
                    Fault("VenturesLinks.Lever", e);
                    return 0;
                }
            }

            private static int Elections()
            {
                try
                {
                    CivicDirector civic = CivicDirector.Instance;
                    return civic != null && civic.Running && civic.Government != null ? civic.Government.State.Number : -1;
                }
                catch (Exception e)
                {
                    Fault("VenturesLinks.Elections", e);
                    return -1;
                }
            }

            private static int JobsHeld()
            {
                try
                {
                    return CareerState.Loaded ? CareerState.History.Count + (CareerState.Employed ? 1 : 0) : 0;
                }
                catch (Exception e)
                {
                    Fault("VenturesLinks.JobsHeld", e);
                    return 0;
                }
            }

            private static bool Held()
            {
                try
                {
                    return Law.Custody.Active || JusticePanel.IsOpen || LicenceDirector.IsActive || PoliceAttention.IsBusy || Justice.InPrison || TransitDirector.Riding;
                }
                catch (Exception e)
                {
                    Fault("VenturesLinks.Held", e);
                    return true;   // not known: no meeting
                }
            }

            /// <summary>
            /// The Ventures rows of the Planner: an offer waiting for an answer on the day it lapses, money promised
            /// on the last day its business can be opened, a deal's target day, the last day to answer a demand, what
            /// a sum called back takes tomorrow, and the next dividend day while shares are held. It reads the file
            /// and changes nothing. Ids: 1 to 5 with the offer's, the promise's or the deal's number; 6 with nothing.
            /// </summary>
            private sealed class VenturesSource : IPlannerSource
            {
                public string Id => "ventures";

                public bool Available => (Settings.VenturesInvestors || Settings.VenturesMarket) && VenturesDirector.Instance != null && VenturesDirector.Instance.File != null
                                         && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    VenturesDirector director = VenturesDirector.Instance;
                    VenturesFile file = director.File;
                    int today = SaveGameManager.Current.Day;
                    for (int i = 0; i < file.Offers.Count; i++)
                    {
                        Offer o = file.Offers[i];
                        PlannerEntry e = into.Add(Id, 1, o.Id, PlannerCategory.Money, Math.Max(today, o.LapseDay));
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_ven_plan_offer_title", "name", Name(file, o.InvestorId));
                            e.What = PlannerText.T("reallife_ven_plan_offer_what", "share", VenturesText.Share(o.ShareBp));
                            e.Money = PlannerText.T("reallife_planner_pays", "amount", FinanceText.Money(o.Amount));
                            e.IfMissed = PlannerText.T("reallife_ven_plan_offer_missed");
                        }
                    }
                    for (int i = 0; i < file.Pledges.Count; i++)
                    {
                        Pledge p = file.Pledges[i];
                        PlannerEntry e = into.Add(Id, 2, p.Id, PlannerCategory.Money, Math.Max(today, p.OpenBy));
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_ven_plan_pledge_title", "type", VenturesGame.TypeName(p.TypeName));
                            e.What = PlannerText.T("reallife_ven_plan_pledge_what", "name", Name(file, p.InvestorId));
                            e.Money = PlannerText.T("reallife_planner_pays", "amount", FinanceText.Money(p.Amount));
                            e.IfMissed = PlannerText.T("reallife_ven_plan_pledge_missed");
                        }
                    }
                    for (int i = 0; i < file.Deals.Count; i++)
                    {
                        Deal d = file.Deals[i];
                        if (d.Stage == DealStage.Running && d.TargetDay > 0)
                        {
                            PlannerEntry e = into.Add(Id, 3, d.Id, PlannerCategory.Money, Math.Max(today, d.TargetDay));
                            if (e != null && into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_ven_plan_target_title", "name", Name(file, d.InvestorId));
                                e.What = PlannerText.T("reallife_ven_plan_target_what", "business", d.Business, "amount", FinanceText.Money(d.TargetProfit));
                                e.IfMissed = PlannerText.T("reallife_ven_plan_target_missed");
                            }
                        }
                        if (d.Stage == DealStage.Demand)
                        {
                            PlannerEntry e = into.Add(Id, 4, d.Id, PlannerCategory.Money, Math.Max(today, d.DemandDay));
                            if (e != null && into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_ven_plan_demand_title", "name", Name(file, d.InvestorId));
                                e.What = PlannerText.T("reallife_ven_plan_demand_what", "business", d.Business);
                                e.IfMissed = PlannerText.T("reallife_ven_plan_demand_missed");
                            }
                        }
                        if (d.Stage == DealStage.Called && d.CallOwed > 0)
                        {
                            PlannerEntry e = into.Add(Id, 5, d.Id, PlannerCategory.Money, today + 1);
                            if (e != null && into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_ven_plan_call_title", "name", Name(file, d.InvestorId));
                                e.What = PlannerText.T("reallife_ven_plan_call_what", "amount", FinanceText.Money(d.CallOwed));
                                e.Money = PlannerText.T("reallife_planner_costs", "amount", FinanceText.Money(Math.Min(d.CallPerDay, d.CallOwed)));
                                e.IfMissed = PlannerText.T("reallife_ven_plan_call_missed", "count", VenturesCatalogue.CallMissesMost.ToString());
                            }
                        }
                    }
                    if (Settings.VenturesMarket && file.Holdings.Count > 0)
                    {
                        PlannerEntry e = into.Add(Id, 6, 0, PlannerCategory.Money, today + Market.DaysToDividend(file));
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_ven_plan_dividend_title");
                            e.What = PlannerText.T("reallife_ven_plan_dividend_what", "count", file.Holdings.Count.ToString());
                        }
                    }
                }

                private static string Name(VenturesFile file, int investorId)
                {
                    Investor investor = file.Investor(investorId);
                    return investor != null ? investor.Name : string.Empty;
                }
            }
        }
    }
}
