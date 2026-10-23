using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Uptown's landmarks (Landmarks/LandmarkBridges) with the rest of the mod:
        ///
        ///   OpenTender ... RivalName   the construction game (Construction/ConstructionDirector.Landmarks): a landmark's
        ///                              tender, its state, put back or awarded by a test, the claims on the district's lots
        ///   StartPlot ... DropSite     the city's construction programme (Civic/CivicWorks) for a rival's or the city's works
        ///   Standing                   the press's Standing (Fame) for joining an opening
        ///   News.Extra                 the morning's City Ledger: a landmark's news leads the front page
        ///   Sites, Design              what ConstructionLinks hands the construction game of the landmarks' sites and designs
        ///
        /// Asked on a day's pass, a page, a press or a site's working hour, never per frame; a fault is said once
        /// (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class LandmarkLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<LandmarkStory> Stories = new List<LandmarkStory>(6);

            internal static void Connect()
            {
                LandmarkBridges.OpenTender = OpenTender;
                LandmarkBridges.TenderState = id => ConstructionDirector.Instance != null ? (int)ConstructionDirector.Instance.LandmarkTenderState(id, out _) : -1;
                LandmarkBridges.DropTender = id => ConstructionDirector.Instance != null && ConstructionDirector.Instance.DropLandmarkTender(id);
                LandmarkBridges.AwardTender = id => ConstructionDirector.Instance != null && ConstructionDirector.Instance.AwardLandmarkTender(id);
                LandmarkBridges.Claimed = lot => ConstructionDirector.Instance != null && ConstructionDirector.Instance.Claimed(lot);
                LandmarkBridges.RivalName = rival => rival >= 0 && rival < BuildCatalogue.RivalCount ? ConstructionText.T(BuildCatalogue.RivalKeys[rival]) : null;
                LandmarkBridges.StartPlot = StartPlot;
                LandmarkBridges.Site = Site;
                LandmarkBridges.FinishSite = id => Works() is CivicWorks w && w.Finish(w.Find(id), TimeHelper.CurrentDay);
                LandmarkBridges.DropSite = id => Works() is CivicWorks w && w.Drop(w.Find(id));
                LandmarkBridges.Standing = points => FameDirector.Instance?.Moved(FamePart.Neighbourhood, FameCause.Press, points);
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                LandmarkBridges.Reset();
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            private static CivicWorks Works()
            {
                CivicDirector civic = CivicDirector.Instance;
                return civic != null ? civic.Works : null;
            }

            // ------------------------------------------------------------------ the construction game

            private static int OpenTender(string site, int use, int area, int storeys, out string why)
            {
                why = null;
                try
                {
                    ConstructionDirector d = ConstructionDirector.Instance;
                    if (d == null)
                    {
                        why = "the construction game is not there";
                        return 0;
                    }
                    return d.LandmarkTender(site, use, area, storeys, out why);
                }
                catch (Exception e)
                {
                    Fault("LandmarkLinks.OpenTender", e);
                    why = "a fault";
                    return 0;
                }
            }

            /// <summary>Each landmark's site as one of the construction game's lots: its id the site's, its address its avenue lot's, its ground the works' plot.</summary>
            internal static void Sites(List<LotView> into)
            {
                into.Clear();
                try
                {
                    for (int k = 0; k < (int)LandmarkKind.Count; k++)
                    {
                        if (!LandmarksDirector.SiteOf((LandmarkKind)k, out LandmarkSite site, out _) || string.IsNullOrEmpty(site.Address))
                        {
                            continue;
                        }
                        Lot address = UptownLots.Get(site.Address);
                        if (address == null)
                        {
                            continue;
                        }
                        into.Add(new LotView
                        {
                            Id = LandmarkRules.SiteId((LandmarkKind)k), Street = address.Street, Number = address.Number, X = site.Plot.X, Y = UptownLots.GroundY, Z = site.Plot.Z,
                            Yaw = site.Plot.Yaw, Width = site.Plot.Width, Depth = site.Plot.Depth, Class = (int)address.Class, First = true, Open = UptownLots.Open(address),
                            Traffic = Math.Max(0, DistrictBuildings.TrafficOf(address.Id))
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("LandmarkLinks.Sites", e);
                    into.Clear();
                }
            }

            /// <summary>The design a landmark's main building wears, as the construction game reads one; false for the stadium or one not picked yet.</summary>
            internal static bool Design(string site, out DesignChoice design)
            {
                design = default(DesignChoice);
                try
                {
                    if (!LandmarksDirector.DesignOf(site, out string name, out string size, out int version, out string type, out float height, out float width, out float depth, out int area))
                    {
                        return false;
                    }
                    design = new DesignChoice { Name = name, Size = size, Version = version, Type = type, Height = height, Width = width, Depth = depth, TotalSqm = area };
                    return true;
                }
                catch (Exception e)
                {
                    Fault("LandmarkLinks.Design", e);
                    return false;
                }
            }

            // ------------------------------------------------------------------ the city's programme

            private static int StartPlot(Vector3 middle, int yaw, int width, int depth, int storeys, int contractor, int hours, out string why)
            {
                why = null;
                try
                {
                    CivicWorks works = Works();
                    if (works == null || !Settings.Civic || !Settings.CivicConstruction)
                    {
                        why = "the city's construction sites are off";
                        return 0;
                    }
                    SiteState site = works.StartPlot(middle, yaw, width, depth, storeys, TimeHelper.CurrentDay, out why);
                    if (site == null)
                    {
                        return 0;
                    }
                    if (hours > 0)
                    {
                        site.Needed = hours;
                    }
                    site.Contractor = Mathf.Clamp(contractor, 0, CivicCatalogue.Contractors.Length - 1);
                    return site.Id;
                }
                catch (Exception e)
                {
                    Fault("LandmarkLinks.StartPlot", e);
                    why = "a fault";
                    return 0;
                }
            }

            private static bool Site(int id, out float x, out float z)
            {
                x = z = 0f;
                try
                {
                    SiteState site = Works()?.Find(id);
                    if (site == null)
                    {
                        return false;
                    }
                    x = site.X;
                    z = site.Z;
                    return true;
                }
                catch (Exception e)
                {
                    Fault("LandmarkLinks.Site", e);
                    return false;
                }
            }

            // ------------------------------------------------------------------ the paper

            // The landmarks' news in the morning's City Ledger: the day's biggest leads the front page, so the Ledger's account on
            // Flare posts it first and the city's people answer it; the rest after the others' pieces.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("LandmarkLinks.Paper (the pieces before)", e);
                }
                try
                {
                    Stories.Clear();
                    LandmarksDirector.Stories(day, Stories);
                    bool led = false;
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        var item = new NewsItem { Key = Stories[i].Key, Data = Stories[i].Data };
                        if (Stories[i].Lead && !led && into.Count > 0)
                        {
                            into.Insert(1, item);   // after the masthead
                            led = true;
                        }
                        else
                        {
                            into.Add(item);
                        }
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("LandmarkLinks.Paper", e);
                }
            }
        }
    }
}
