using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Entities;
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
                CompanyBridges.Close = Close;
                CompanyBridges.Dispute = Dispute;
                CompanyBridges.Rival = Rival;
                CompanyBridges.Leave = Leave;
                BusinessGame.Ambient = Ambient;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                CompanyBridges.Reset();
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
