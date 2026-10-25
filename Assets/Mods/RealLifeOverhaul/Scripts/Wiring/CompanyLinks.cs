using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Entities;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Running a business (CompanyBridges) with business pressure (footfall, closures, disputes, rivals, audits and
        /// inspections), the game's resignation (once, StaffLeave), fires, break-ins and fines for its claims, Justice's tax
        /// cases, the paper and the Planner. One rival's offer to an employee at a time: the company and the tech sector
        /// each ask the other's (<see cref="Holds"/>, TechLinks.Holds), and business pressure's poaching asks both.
        /// </summary>
        public static class CompanyLinks
        {
            /// <summary>The company's rows in the Planner, made by CompanyDirector.Alerts; this only hands them over.</summary>
            private sealed class CompanySource : IPlannerSource
            {
                public string Id => "company";

                public bool Available => Settings.Economy && Settings.EconomyCompany && CompanyDirector.Instance != null && CompanyDirector.Instance.Running;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    IReadOnlyList<CompanyAlert> alerts = CompanyDirector.Instance.Alerts;
                    for (int i = 0; i < alerts.Count; i++)
                    {
                        CompanyAlert alert = alerts[i];
                        PlannerEntry e = into.Add(Id, alert.A, alert.B, PlannerCategory.Business, alert.Day);
                        if (e != null && into.WantText)
                        {
                            e.Title = alert.Title;
                            e.What = alert.What;
                            e.Money = alert.Money;
                            e.IfMissed = alert.IfMissed;
                        }
                    }
                }
            }

            private static readonly List<string> Names = new List<string>(4);
            private static readonly CompanySource Source = new CompanySource();
            private static Action<int, List<NewsItem>> _paperBefore;

            internal static void Connect()
            {
                CompanyBridges.Footfall = Footfall;
                CompanyBridges.Promoted = BusinessGame.Promoted;
                CompanyBridges.Close = Close;
                CompanyBridges.Dispute = Dispute;
                CompanyBridges.Rival = Rival;
                CompanyBridges.Leave = Leave;
                CompanyBridges.Held = TechLinks.Holds;
                BusinessDirector.Held = Offered;
                CompanyBridges.Audit = Audit;
                CompanyBridges.TaxCase = TaxCases.File;
                CompanyBridges.Inspect = Inspect;
                CompanyBridges.IssueFootfall = IssueFootfall;
                HierarchyLinks.Connect();   // the professionals the executives are, from the professions
                BusinessEvents.Closed += IssueClosed;
                BusinessGame.Ambient = Ambient;
                _paperBefore = News.Extra;
                News.Extra = Paper;
                PlannerDirector.Instance?.AddSource(Source);
                FireLosses.Billed += FireLoss;
                BreakIns.Happened += BreakIn;
                BusinessEvents.Fined += Fined;
            }

            internal static void Disconnect()
            {
                CompanyBridges.Reset();
                BusinessDirector.Held = null;
                FireLosses.Billed -= FireLoss;
                BreakIns.Happened -= BreakIn;
                BusinessEvents.Fined -= Fined;
                BusinessEvents.Closed -= IssueClosed;
                BusinessGame.Ambient = CityEconomyDirector.FootfallOf;   // the city's economy's pairing takes it off next
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            // The city's economy's and the company's parts of a business's footfall; business pressure bounds the sum.
            private static int Ambient(BuildingRegistration reg)
            {
                try
                {
                    return CityEconomyDirector.FootfallOf(reg) + CompanyDirector.FootfallOf(reg);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Ambient", e);
                    return 0;
                }
            }

            private static bool Footfall(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written)
            {
                try
                {
                    return BusinessDirector.Footfall(reg, redraw && !(newDay && CityEconomyDirector.FootfallOf(reg) != 0), newDay, out own, out written);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Footfall", e);
                    own = 0;
                    written = 0;
                    return false;
                }
            }

            private static bool Close(BuildingRegistration reg, int hours)
            {
                try
                {
                    BusinessDirector director = BusinessDirector.Instance;
                    return director != null && Settings.Business && BusinessState.Loaded && BusinessGame.IsBusiness(reg) && director.CloseFor(reg, hours);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Close", e);
                    return false;
                }
            }

            private static bool Dispute(BuildingRegistration reg)
            {
                try
                {
                    BusinessIssue issue = Settings.Business && BusinessState.Loaded && reg != null ? BusinessState.IssueAt(BusinessGame.Place(reg)) : null;
                    return issue != null && issue.Kind == BusinessCatalogue.Strike;
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Dispute", e);
                    return false;
                }
            }

            // What one of business pressure's issues changes a business's footfall by on a day, until the day it ends.
            private static int IssueFootfall(BuildingRegistration reg, int day)
            {
                try
                {
                    int at = BusinessState.Loaded && reg != null ? BusinessState.EffectAt(BusinessGame.Place(reg)) : -1;
                    return at >= 0 && day < BusinessState.Effects[at].UntilDay ? BusinessState.Effects[at].Percent : 0;
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.IssueFootfall", e);
                    return 0;
                }
            }

            private static string Rival(BuildingRegistration reg)
            {
                try
                {
                    RivalData rival = reg != null ? BusinessGame.AnyRival(reg) : null;
                    return rival != null ? rival.rivalName : null;
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Rival", e);
                    return null;
                }
            }

            private static bool Leave(EmployeeInstance employee)
            {
                try
                {
                    return StaffLeave.Resign(employee, "the company, for a rival");
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Leave", e);
                    return false;
                }
            }

            /// <summary>
            /// The company's own rival's offer waits for an employee (the game's id), or they work their notice after one:
            /// the tech sector and business pressure make them no offer then.
            /// </summary>
            internal static bool Holds(string id)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    StaffFile f = company != null && company.Running && company.State != null ? company.State.Employee(id) : null;
                    return f != null && (f.PoachDay > 0 || f.LeaveDay > 0);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Holds", e);
                    return false;
                }
            }

            // Business pressure's poaching: never somebody the company or the tech sector has an offer or a departure in hand for.
            private static bool Offered(string id)
            {
                try
                {
                    return Holds(id) || TechLinks.Holds(id);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Offered", e);
                    return false;
                }
            }

            // A fire's repair bill at a business of the player's: fire cover may pay some of it back.
            private static void FireLoss(BuildingRegistration reg, int bill)
            {
                try
                {
                    CompanyDirector.Instance?.Loss(reg, CoverKind.Fire, bill);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.FireLoss", e);
                }
            }

            // A break-in: theft cover counts the stock taken and the repair together.
            private static void BreakIn(BuildingRegistration reg, int stock, int repair)
            {
                try
                {
                    CompanyDirector.Instance?.Loss(reg, CoverKind.Theft, Math.Max(0, stock) + Math.Max(0, repair));
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.BreakIn", e);
                }
            }

            // A fine from a health or fire safety inspection: liability cover may pay some of it back.
            private static void Fined(string kind, string place, int amount)
            {
                try
                {
                    if (kind == BusinessCatalogue.Health || kind == BusinessCatalogue.Fire)
                    {
                        CompanyDirector.Instance?.Loss(BusinessGame.Find(place), CoverKind.Liability, amount);
                    }
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Fined", e);
                }
            }

            // Business pressure's own tax audit, by its own rules: it may say no.
            private static bool Audit(BuildingRegistration reg)
            {
                try
                {
                    BusinessDirector director = BusinessDirector.Instance;
                    return director != null && reg != null && director.StartIssue(BusinessCatalogue.Audit, reg);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Audit", e);
                    return false;
                }
            }

            // Health where food is sold, fire safety otherwise, or whichever of the two business pressure will start.
            private static bool Inspect(BuildingRegistration reg)
            {
                try
                {
                    BusinessDirector director = BusinessDirector.Instance;
                    if (director == null || reg == null)
                    {
                        return false;
                    }
                    bool food = BusinessGame.IsFood(reg);
                    return director.StartIssue(food ? BusinessCatalogue.Health : BusinessCatalogue.Fire, reg) || director.StartIssue(food ? BusinessCatalogue.Fire : BusinessCatalogue.Health, reg);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Inspect", e);
                    return false;
                }
            }

            // A health or fire safety inspection has ended at a business: the company looks at its licences.
            private static void IssueClosed(string kind, string place, string outcome)
            {
                try
                {
                    if (kind == BusinessCatalogue.Health || kind == BusinessCatalogue.Fire)
                    {
                        CompanyDirector.Instance?.Inspected(place);
                    }
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.IssueClosed", e);
                }
            }

            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Paper (the pieces before)", e);
                }
                try
                {
                    CompanyDirector director = CompanyDirector.Instance;
                    if (director == null || !director.Running)
                    {
                        return;
                    }
                    Names.Clear();
                    director.StrikesFrom(day, Names);
                    for (int i = 0; i < Names.Count; i++)
                    {
                        into.Add(new NewsItem { Key = "reallife_co_news_strike", Data = new Dictionary<string, string>(1) { { "business", Names[i] } } });
                    }
                    Names.Clear();
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Paper", e);
                }
            }
        }
    }
}
