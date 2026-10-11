using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Nav;
using RealLifeOverhaul.Planner;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The online shops with the rest of the mod (Economy/Online/OnlineBridges). The online shops call no other
        /// feature and none calls them: what they have to do with each other is here.
        ///
        ///   Campaign      a campaign of the company's that runs at a business today (CampaignRules.Footfall on the
        ///                 company's own file of the business): half of its customers also find the site. Nothing
        ///                 while the company's marketing is off
        ///   Season        the season of a day, as the city's economy is told it (CityBridges.Day, the mod's calendar)
        ///   Road          the metres of road between two places over the road graph the GPS has made
        ///                 (Navigation.NavDirector.Roads), searched with a search of its own a slice at a time so that
        ///                 no call takes more than a few thousand points; nothing while the GPS has no graph
        ///   the Planner   a source of its own (<see cref="OnlineSource"/>): the rows the online shops have made
        ///
        /// Everything is read from what those features already keep in public or asked through an entry they already
        /// have; nothing of theirs is changed. The online sales reach the company's quarterly return and the game's
        /// tax through the game's own statements (OnlineGame.Book), not through anything here.
        /// </summary>
        public static class OnlineLinks
        {
            /// <summary>
            /// The online shops' rows in the Planner, as a source of its own: orders that wait with nobody on shift to
            /// pick them, a rating that is falling or poor, a site that is offline for unpaid hosting, and hosting that
            /// is taken tomorrow. The rows are made by the online shops when what they depend on changes
            /// (OnlineDirector.Alerts); this only hands them over, and changes nothing.
            /// </summary>
            private sealed class OnlineSource : IPlannerSource
            {
                public string Id => "online";

                public bool Available => Settings.Economy && Settings.EconomyOnline && OnlineDirector.Instance != null && OnlineDirector.Instance.Running;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    IReadOnlyList<OnlineAlert> alerts = OnlineDirector.Instance.Alerts;
                    for (int i = 0; i < alerts.Count; i++)
                    {
                        OnlineAlert alert = alerts[i];
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

            private const float Reach = 70f;        // metres from a place within which a lane point starts or ends its road
            private const float ReachWide = 180f;   // ...and for a place with no lane that near
            private const int EndsMost = 24;        // lane points at either end
            private const int Slice = 4000;         // points a call goes on from: a fraction of a millisecond
            private const int SlicesMost = 40;      // a search that has not ended by then has no road worth the name
            private const int WaitsMost = 180;      // calls made while the GPS has no graph yet, after which the guess is kept

            private static readonly OnlineSource Source = new OnlineSource();
            private static readonly List<int> Starts = new List<int>(EndsMost);
            private static readonly List<int> Goals = new List<int>(EndsMost);
            private static readonly List<int> Path = new List<int>(512);
            private static RouteSearch _made, _search;
            private static Vector3 _from, _to;
            private static bool _begun;
            private static int _slices, _waits;

            internal static void Connect()
            {
                OnlineBridges.Campaign = Campaign;
                OnlineBridges.Season = Season;
                OnlineBridges.Road = Road;
                PlannerDirector.Instance?.AddSource(Source);
                _made = _search = null;
                _begun = false;
                _slices = _waits = 0;
            }

            internal static void Disconnect()
            {
                OnlineBridges.Reset();
                _made = _search = null;
                _begun = false;
            }

            private static int Campaign(BuildingRegistration reg)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    GameInstance save = SaveGameManager.Current;
                    if (company == null || save == null || reg == null || !Settings.EconomyCompany || !company.On(CompanyPart.Marketing) || company.State == null)
                    {
                        return 0;
                    }
                    ShopFile file = company.State.Shop(CompanyGame.Place(reg));
                    return file != null ? Math.Max(0, CampaignRules.Footfall(file, save.Day)) : 0;
                }
                catch (Exception e)
                {
                    Fault("OnlineLinks.Campaign", e);
                    return 0;
                }
            }

            private static int Season(int day)
            {
                try
                {
                    return CityBridges.Day != null ? Mathf.Clamp(CityBridges.Day(day).Season, 0, 3) : 1;
                }
                catch (Exception e)
                {
                    Fault("OnlineLinks.Season", e);
                    return 1;
                }
            }

            // A slice of the search for the road between two places: -1 still looking, 0 no road to be had, otherwise its metres.
            private static int Road(Vector3 from, Vector3 to)
            {
                try
                {
                    RouteSearch made = NavDirector.Roads;
                    if (made == null || made.EdgeStart == null)
                    {
                        return NavDirector.Instance != null && ++_waits < WaitsMost ? -1 : 0;   // (the GPS makes its graph in the first moments of a city; with it off there is none)
                    }
                    if (!ReferenceEquals(made, _made))
                    {
                        _made = made;
                        _search = new RouteSearch(made);   // the same points, a search of its own: the GPS's own search is never disturbed
                        _search.SetEdges(made.EdgeStart, made.EdgeTo, made.EdgeExtra);
                        _begun = false;
                    }
                    if (!_begun || from != _from || to != _to)
                    {
                        Near(from, Starts);
                        Near(to, Goals);
                        _from = from;
                        _to = to;
                        _slices = 0;
                        _begun = Starts.Count > 0 && Goals.Count > 0 && _search.Begin(Starts, Goals, to.x, to.y, to.z, ReachWide);
                        if (!_begun)
                        {
                            return 0;
                        }
                    }
                    RouteSearch.Outcome outcome = _search.Advance(_search.Expanded + Slice, Path);
                    if (outcome == RouteSearch.Outcome.Searching)
                    {
                        if (++_slices < SlicesMost)
                        {
                            return -1;
                        }
                        _begun = false;
                        return 0;
                    }
                    _begun = false;
                    if (outcome != RouteSearch.Outcome.Found)
                    {
                        return 0;
                    }
                    float metres = 0f;
                    for (int i = 1; i < Path.Count; i++)
                    {
                        float x = _search.X[Path[i]] - _search.X[Path[i - 1]], z = _search.Z[Path[i]] - _search.Z[Path[i - 1]];
                        metres += Mathf.Sqrt(x * x + z * z);
                    }
                    Path.Clear();
                    return Mathf.Max(1, Mathf.RoundToInt(metres));
                }
                catch (Exception e)
                {
                    Fault("OnlineLinks.Road", e);
                    _begun = false;
                    return 0;
                }
            }

            // The lane points nearest a place, a couple of dozen at most: one pass over the graph's points, when a search begins.
            private static void Near(Vector3 place, List<int> into)
            {
                into.Clear();
                float[] x = _search.X, z = _search.Z;
                int[] edges = _search.EdgeStart;
                float reach = Reach * Reach;
                for (int round = 0; round < 2 && into.Count == 0; round++, reach = ReachWide * ReachWide)
                {
                    for (int i = 0; i < _search.Count && into.Count < EndsMost; i++)
                    {
                        float dx = x[i] - place.x, dz = z[i] - place.z;
                        if (dx * dx + dz * dz <= reach && edges[i + 1] > edges[i])
                        {
                            into.Add(i);
                        }
                    }
                }
            }
        }
    }
}
