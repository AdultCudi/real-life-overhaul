using System;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Investors (VenturesBridges) with the city's economy, the employers on the CV, whether the player is held, and the
        /// Planner.
        /// </summary>
        public static class VenturesLinks
        {
            private static readonly VenturesSource Source = new VenturesSource();

            internal static void Connect()
            {
                // A slowdown and a recession are both the market's slowdown; the bank rate is the steady rate with the
                // central bank's points on it.
                VenturesBridges.Climate = new ClimateFrom(
                    () => CityEconomy.State == CycleState.Boom ? 1 : CityEconomy.State == CycleState.Steady ? 0 : -1,
                    () => SteadyClimate.Rate + CityEconomy.BasePoints);
                VenturesBridges.JobsHeld = JobsHeld;
                VenturesBridges.Held = Held;
                VenturesBridges.Morale = Morale;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                VenturesBridges.Reset();   // the Planner's director goes with the city, and its sources with it
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

            // The staff's standing across the player's businesses, weighted by the people at each: what a promise to keep morale up is judged on.
            private static int Morale()
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    if (company == null)
                    {
                        return -1;
                    }
                    var list = new System.Collections.Generic.List<BuildingRegistration>(16);
                    Books.Businesses(list);
                    long standing = 0;
                    int staff = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        int count;
                        int each = company.StandingAt(Books.Key(list[i].Address), out count);
                        standing += (long)each * count;
                        staff += count;
                    }
                    return staff > 0 ? (int)(standing / staff) : -1;
                }
                catch (Exception e)
                {
                    Fault("VenturesLinks.Morale", e);
                    return -1;
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
            /// The Ventures rows of the Planner: an offer's lapse, a promise's last day, a deal's target, a demand's answer and
            /// a call's payment tomorrow. Read only. Ids 1 to 5 with the offer's, promise's or deal's number; 6 is not used.
            /// </summary>
            private sealed class VenturesSource : IPlannerSource
            {
                public string Id => "ventures";

                // with Investors off the file stays open while deals, promises or offers carry on, and their days are still due
                public bool Available => VenturesDirector.Instance != null && VenturesDirector.Instance.File != null && SaveGameManager.Current != null;

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
                                float owed = Math.Min(d.CallPerDay, d.CallOwed);
                                PlannerText.Costs(e, owed, FinanceText.Money(owed));
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
