using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Entities;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Running a business with the rest of the mod (Economy/Company/CompanyBridges). The company calls no other
        /// feature and none calls it: what they have to do with each other is here.
        ///
        ///   Footfall      a business's promotion figure, written by business pressure's own mechanism
        ///                 (BusinessDirector.Footfall). <see cref="BusinessGame.Ambient"/> is asked for what the city's
        ///                 economy and the company add together: one writer of that figure, whoever asks. On a new day
        ///                 the city's economy has already had the day's customers drawn for a business it changes, so
        ///                 they are not drawn a second time for the company
        ///   Close         business pressure's own closure for some hours, which it reopens itself (only while
        ///                 business pressure is on: off, it keeps no closure)
        ///   Dispute       business pressure's own "staff dispute" is open at a business: the company's strike waits
        ///   Rival         the name of one of the game's rivals, as business pressure picks one
        ///   Leave         the game's own resignation, as business pressure has an employee leave
        ///   losses        a fire's repair bill (Events/FireLosses), a break-in's stock and repair (Crime/BreakIns) and
        ///                 an inspector's fine (Business/BusinessEvents) are told to the company, which opens a claim
        ///                 if a policy covers it. The loss is the other feature's own and is not changed
        ///   Audit         business pressure's own tax audit, started through its own rules (BusinessDirector.StartIssue)
        ///   TaxCase       a criminal case for tax kept back, opened as one of Justice's own (Justice/TaxCases)
        ///   Inspect       one of business pressure's own inspections, started through its own rules where a licence is missing
        ///   inspections   when a health or fire safety inspection ends (Business/BusinessEvents), the company fines a
        ///                 business that trades without a licence it needs
        ///   the paper     a strike that starts today, in the City Ledger (News.Extra, after the city's economy's pieces)
        ///
        /// Everything is read from what those features already keep in public or asked through an entry they already
        /// have; nothing of theirs is changed.
        /// </summary>
        public static class CompanyLinks
        {
            private static readonly List<string> Names = new List<string>(4);
            private static Action<int, List<NewsItem>> _paperBefore;

            internal static void Connect()
            {
                CompanyBridges.Footfall = Footfall;
                CompanyBridges.Promoted = BusinessGame.Promoted;
                CompanyBridges.Close = Close;
                CompanyBridges.Dispute = Dispute;
                CompanyBridges.Rival = Rival;
                CompanyBridges.Leave = Leave;
                CompanyBridges.Audit = Audit;
                CompanyBridges.TaxCase = TaxCases.File;
                CompanyBridges.Inspect = Inspect;
                BusinessEvents.Closed += IssueClosed;
                BusinessGame.Ambient = Ambient;
                _paperBefore = News.Extra;
                News.Extra = Paper;
                FireLosses.Billed += FireLoss;
                BreakIns.Happened += BreakIn;
                BusinessEvents.Fined += Fined;
            }

            internal static void Disconnect()
            {
                CompanyBridges.Reset();
                FireLosses.Billed -= FireLoss;
                BreakIns.Happened -= BreakIn;
                BusinessEvents.Fined -= Fined;
                BusinessEvents.Closed -= IssueClosed;
                BusinessGame.Ambient = CityEconomyDirector.FootfallOf;   // (the city's economy's own pairing takes it off in a moment)
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            // What the city's economy and the company add to a business's footfall, together. Business pressure keeps the sum inside its own bounds.
            private static int Ambient(BuildingRegistration reg)
            {
                return CityEconomyDirector.FootfallOf(reg) + CompanyDirector.FootfallOf(reg);
            }

            private static bool Footfall(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written)
            {
                return BusinessDirector.Footfall(reg, redraw && !(newDay && CityEconomyDirector.FootfallOf(reg) != 0), newDay, out own, out written);
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

            private static void Leave(EmployeeInstance employee)
            {
                try
                {
                    BusinessGame.Leave(employee);
                }
                catch (Exception e)
                {
                    Fault("CompanyLinks.Leave", e);
                }
            }


            // A fire's repair bill at a business of the player's: fire cover may pay some of it back (the bill itself is the fire's own).
            private static void FireLoss(BuildingRegistration reg, int bill)
            {
                CompanyDirector.Instance?.Loss(reg, CoverKind.Fire, bill);
            }

            // A break-in: theft cover counts the stock taken and the repair together.
            private static void BreakIn(BuildingRegistration reg, int stock, int repair)
            {
                CompanyDirector.Instance?.Loss(reg, CoverKind.Theft, Math.Max(0, stock) + Math.Max(0, repair));
            }

            // A fine from one of business pressure's inspections (health, fire safety): liability cover may pay some of it back.
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

            // Business pressure's own tax audit, by its own rules (it may say no: a rest, an open issue, its switch).
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
            // One of business pressure's inspections, started through its own rules: health where food is sold, fire safety otherwise, or whichever of the two it will start.
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
                if (kind == BusinessCatalogue.Health || kind == BusinessCatalogue.Fire)
                {
                    CompanyDirector.Instance?.Inspected(place);
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
