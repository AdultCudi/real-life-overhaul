using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.Careers;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The building trades (BuildTrades/TradesBridges) with education, civic life's construction programme and the jobs:
        ///
        ///   Passed, Points, Learn   the player's courses and skills (Careers): what a trade asks, and what its work teaches
        ///   Nearest, Site           the works sites of the city's programme (Civic/CivicWorks), as the trades see them
        ///   ReportUnsafe            an inspector fails an unsafe site: the city's own report, as at its notice
        ///   SiteWork                "Work here" in the site notice's conversation (Civic/CityHallPhone)
        ///   Workplace               the builder's, building control's and the practice's places and their ranks' needs (Jobs/CareerLinks)
        ///   AtOffice                an office trade is taken on the practice's floor (Jobs/JobDirector)
        ///
        /// Asked when a shift is taken or settled, a page turned, a review held; never per frame. A fault is said once
        /// (<see cref="Fault"/>) and the answer is the bridge's own default.
        /// </summary>
        public static class TradesLinks
        {
            internal static void Connect()
            {
                TradesBridges.Passed = Passed;
                TradesBridges.Points = Points;
                TradesBridges.Learn = Learn;
                TradesBridges.Nearest = Nearest;
                TradesBridges.Site = Site;
                TradesBridges.ReportUnsafe = ReportUnsafe;
                CivicBridges.SiteWork = SiteWork.Page;
                CareerLinks.Workplace = TradesDirector.Workplace;
                JobDirector.AtOffice = AtOffice;
            }

            internal static void Disconnect()
            {
                TradesBridges.Passed = null;
                TradesBridges.Points = null;
                TradesBridges.Learn = null;
                TradesBridges.Nearest = null;
                TradesBridges.Site = null;
                TradesBridges.ReportUnsafe = null;
                CivicBridges.SiteWork = null;
                CareerLinks.Workplace = null;
                JobDirector.AtOffice = null;
            }

            private static bool Passed(int course)
            {
                try
                {
                    StudyFile study = CareersDirector.Instance != null ? CareersDirector.Instance.Study : null;
                    return study != null && study.Holds(course);
                }
                catch (System.Exception e)
                {
                    Fault("TradesLinks.Passed", e);
                    return false;
                }
            }

            private static int Points(int skill)
            {
                try
                {
                    StudyFile study = CareersDirector.Instance != null ? CareersDirector.Instance.Study : null;
                    return study != null ? StudyRules.Points(study, skill) : 0;
                }
                catch (System.Exception e)
                {
                    Fault("TradesLinks.Points", e);
                    return 0;
                }
            }

            private static void Learn(int skill, int thousandths)
            {
                try
                {
                    StudyFile study = CareersDirector.Instance != null ? CareersDirector.Instance.Study : null;
                    if (study != null)
                    {
                        StudyRules.Learn(study, skill, thousandths, TimeHelper.CurrentDay);
                    }
                }
                catch (System.Exception e)
                {
                    Fault("TradesLinks.Learn", e);
                }
            }

            private static bool Nearest(Vector3 at, float within, TradeSite into)
            {
                try
                {
                    CivicDirector civic = CivicDirector.Instance;
                    if (civic == null || civic.Works == null || !Settings.CivicConstruction)
                    {
                        return false;
                    }
                    SiteState site = civic.Works.Nearest(at, within);
                    return site != null && Fill(civic.Works, site, into);
                }
                catch (System.Exception e)
                {
                    Fault("TradesLinks.Nearest", e);
                    return false;
                }
            }

            private static bool Site(int id, TradeSite into)
            {
                try
                {
                    CivicDirector civic = CivicDirector.Instance;
                    SiteState site = civic != null && civic.Works != null ? civic.Works.Find(id) : null;
                    return site != null && Fill(civic.Works, site, into);
                }
                catch (System.Exception e)
                {
                    Fault("TradesLinks.Site", e);
                    return false;
                }
            }

            private static bool Fill(CivicWorks works, SiteState site, TradeSite into)
            {
                into.Id = site.Id;
                into.Kind = (int)site.Kind;
                into.Phase = site.Phase;
                into.Working = works.Working(site, TimeHelper.CurrentDay, TimeHelper.CurrentHour);
                into.Unsafe = site.Unsafe;
                into.At = new Vector3(site.X, site.Y, site.Z);
                into.Plot = PlotRules.TryRead(site.Street, out into.Width, out into.Depth, out into.Yaw, out _);
                into.Project = CivicWorks.ProjectName(site);
                into.Contractor = CivicWorks.ContractorName(site);
                into.Address = CivicWorks.PlaceText(site);
                return true;
            }

            private static bool ReportUnsafe(int id)
            {
                try
                {
                    CivicDirector civic = CivicDirector.Instance;
                    SiteState site = civic != null && civic.Works != null ? civic.Works.Find(id) : null;
                    return site != null && civic.Works.ReportUnsafe(site, TimeHelper.CurrentDay);
                }
                catch (System.Exception e)
                {
                    Fault("TradesLinks.ReportUnsafe", e);
                    return false;
                }
            }

            private static bool AtOffice() => Settings.Trades && Settings.TradesOffice && TradeOffice.Here;
        }
    }
}
