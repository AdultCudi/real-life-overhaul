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
        /// Investors with the rest of the mod (Economy/Ventures/VenturesBridges). Ventures calls no other feature and
        /// none calls it: what they have to do with each other is here, and every answer is read from what a feature
        /// already keeps in public. (The stock market is the city's own now: Wiring/MarketLinks.)
        ///
        ///   Climate      the city's economy: its boom, steady and slowdown, and the bank rate
        ///   JobsHeld     Jobs/CareerState: the employers on the CV, the present one included
        ///   Held         custody, prison, a hearing, a driving test or lesson, the police, a bus ride
        ///   the Planner  a source of its own (<see cref="VenturesSource"/>)
        ///
        /// </summary>
        public static class VenturesLinks
        {
            private const int NewsDays = 1;
            private static readonly VenturesSource Source = new VenturesSource();

            internal static void Connect()
            {
                // The city's economy: its boom is the market's boom, its slowdown and its recession the market's slowdown; the
                // bank rate is the steady rate with the central bank's points on it. Steady and unmoved when that phase is off.
                VenturesBridges.Climate = new ClimateFrom(
                    () => CityEconomy.State == CycleState.Boom ? 1 : CityEconomy.State == CycleState.Steady ? 0 : -1,
                    () => SteadyClimate.Rate + CityEconomy.BasePoints);
                VenturesBridges.JobsHeld = JobsHeld;
                VenturesBridges.Held = Held;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                VenturesBridges.Reset();   // (the Planner's director goes with the city, and its sources with it)
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
            /// on the last day its business can be opened, a deal's target day, the last day to answer a demand, and
            /// what a sum called back takes tomorrow. It reads the file and changes nothing. Ids: 1 to 5 with the
            /// offer's, the promise's or the deal's number (6 was the old market's dividend day: no longer used).
            /// </summary>
            private sealed class VenturesSource : IPlannerSource
            {
                public string Id => "ventures";

                public bool Available => Settings.VenturesInvestors && VenturesDirector.Instance != null && VenturesDirector.Instance.File != null
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
