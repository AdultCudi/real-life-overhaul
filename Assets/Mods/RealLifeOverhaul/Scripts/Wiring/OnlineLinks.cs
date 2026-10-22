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
        /// The online shops (OnlineBridges) with the company's campaigns, the season, the GPS's roads (searched a slice of a
        /// few thousand points per call, on a search of its own) and the Planner. Their costs go into the company's quarter
        /// as marketing costs, the company having no kind for selling online, so the accountant's rebate counts them once.
        /// </summary>
        public static class OnlineLinks
        {
            /// <summary>The online shops' rows in the Planner, made by OnlineDirector.Alerts; this only hands them over.</summary>
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
                OnlineBridges.Cost = Cost;
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

            private static void Cost(int cents)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    int amount = (cents + 50) / 100;
                    if (company != null && amount > 0 && Settings.EconomyCompany && company.On(CompanyPart.Tax) && company.State != null)
                    {
                        TaxRules.Note(company.State.Tax, CostKind.Marketing, amount);
                    }
                }
                catch (Exception e)
                {
                    Fault("OnlineLinks.Cost", e);
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
                        return NavDirector.Instance != null && ++_waits < WaitsMost ? -1 : 0;   // the GPS makes its graph in a city's first moments
                    }
                    if (!ReferenceEquals(made, _made))
                    {
                        _made = made;
                        _search = new RouteSearch(made);   // a search of its own, so the GPS's is never disturbed
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

            // The lane points nearest a place, a couple of dozen at most: one pass over the graph when a search begins.
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
