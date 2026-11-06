using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>
    /// The director's part for each landmark's life: its site on the first block, held from the first load for nobody else;
    /// its announcement on its day; its tender in the construction game, or the city's own works when nobody can bid; the
    /// works of a rival or the city kept to their timetable on a site of the programme; the handover to the district (the
    /// tower over its block's lots, the mall's main building and a shop on each lot behind it, both the city's to let), and
    /// the lift its crowds give the lots round it. Same calls, same answers on every machine with the same save and mod.
    /// On a day's pass, an award, a handover or a page; never a frame.
    /// </summary>
    public sealed partial class LandmarksDirector
    {
        private const string OfficeType = "ba:buildingtype_office";
        private const string ShopType = "ba:buildingtype_retail";
        private const int DesignsAsked = 32;      // designs read before the tallest or the largest is taken

        private static readonly List<SiteLot> Lots = new List<SiteLot>(128);   // the first block's lots as the rules read them, made once a run
        private static readonly LandmarkSite[] Sites = { new LandmarkSite(), new LandmarkSite(), new LandmarkSite() };
        private static readonly bool[] SiteKnown = new bool[(int)LandmarkKind.Count];
        private static readonly HashSet<string> HeldLots = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<int> HeldBlocks = new List<int>(3);
        private static readonly List<DesignFit> Fits = new List<DesignFit>(DesignsAsked);
        private static readonly List<float> Heights = new List<float>(DesignsAsked);
        private static readonly List<string> Over = new List<string>(16);
        private static readonly Func<string, bool> ClaimedCall = Claimed;
        private static int _sitesFor = -1;   // the day the sites were last worked out for

        // ---------------------------------------------------------------- the sites

        // The first block's lots, from the plan (fixed data): each one's block, whether it is built from the first day, its place.
        private static void ReadLots()
        {
            if (Lots.Count > 0)
            {
                return;
            }
            IReadOnlyList<Lot> first = UptownLots.FirstBlock;
            for (int i = 0; first != null && i < first.Count; i++)
            {
                Lot l = first[i];
                Lots.Add(new SiteLot
                {
                    Id = l.Id, Block = l.Block, First = l.First, FirstDay = DistrictBuildings.FirstDay(l.Id), Avenue = l.Facing < 1f || l.Facing > 359f,
                    X = l.X, Z = l.Z, Yaw = l.Yaw, Width = l.Width, Depth = l.Depth, Class = (int)l.Class
                });
            }
        }

        // A lot somebody else has: a building of the district on it or finished there, the player's land, a project, a development.
        private static bool Claimed(string lot)
        {
            if (DistrictBuildings.Occupied(lot))
            {
                return true;
            }
            Func<string, bool> claimed = LandmarkBridges.Claimed;
            return claimed != null && claimed(lot);
        }

        /// <summary>
        /// Each landmark's site worked out afresh: a settled one's from its block, whatever is on it now (its lots are held);
        /// one still to be announced on the first of its blocks that is free, the settled ones' blocks and those before it in
        /// order left out. The lots of every known site are held.
        /// </summary>
        private static void RefreshSites()
        {
            ReadLots();
            HeldBlocks.Clear();
            HeldLots.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                for (int k = 0; k < Sites.Length; k++)
                {
                    LandmarkFile f = _state.Files[k];
                    bool settled = f.Block > 0;
                    if (settled != (pass == 0))
                    {
                        continue;   // settled ones first, so one still to come never takes a settled block
                    }
                    SiteKnown[k] = settled ? LandmarkRules.SiteOn(f.Kind, f.Block, Lots, null, Sites[k])
                        : Settings.Landmarks && LandmarkRules.Site(f.Kind, Lots, ClaimedCall, HeldBlocks, Sites[k]);
                    if (!SiteKnown[k])
                    {
                        continue;
                    }
                    HeldBlocks.Add(Sites[k].Block);
                    if (settled && f.Stage >= LandmarkStage.Building || Settings.Landmarks)
                    {
                        for (int i = 0; i < Sites[k].Lots.Count; i++)
                        {
                            HeldLots.Add(Sites[k].Lots[i]);   // works going on or done stay held with the option off: what stands there is theirs
                        }
                    }
                }
            }
            _sitesFor = SitesKey();
        }

        // Worked out once a game day, and once a frame while the city loads (the district save's fill, the lots' first reading).
        private static void SitesToday()
        {
            if (_sitesFor != SitesKey())
            {
                RefreshSites();
            }
        }

        private static int SitesKey() => LandmarksGame.Loading ? -1 - Time.frameCount : LandmarksGame.Day;

        /// <summary>True for a lot of the district a landmark's site holds: not for sale, never a development's.</summary>
        public static bool Held(string lot)
        {
            if (!_loaded || string.IsNullOrEmpty(lot))
            {
                return false;
            }
            SitesToday();
            return HeldLots.Contains(lot) || LandmarkRules.IsSite(lot);
        }

        /// <summary>
        /// The site id of the landmark whose site holds the lot ("landmark:tower"), or null for a lot no landmark holds: what build mode
        /// names a held square by. COST: three sites' lots, asked once for each held square when build mode colours and on a hover.
        /// </summary>
        public static string SiteIdOfLot(string lot)
        {
            if (!_loaded || string.IsNullOrEmpty(lot))
            {
                return null;
            }
            SitesToday();
            for (int k = 0; k < Sites.Length; k++)
            {
                if (SiteKnown[k] && HeldLots.Contains(lot) && Sites[k].Lots.Contains(lot))
                {
                    return LandmarkRules.SiteId((LandmarkKind)k);
                }
            }
            return null;
        }

        /// <summary>A landmark's site as it is now, and its file; false while it has none.</summary>
        public static bool SiteOf(LandmarkKind kind, out LandmarkSite site, out LandmarkFile file)
        {
            int k = (int)kind;
            file = _loaded ? _state.Files[k] : null;
            site = null;
            if (!_loaded || k < 0 || k >= Sites.Length)
            {
                return false;
            }
            SitesToday();
            site = Sites[k];
            return SiteKnown[k];
        }

        /// <summary>The landmark a site id names, with its site and file; false for any other id or one with no site yet.</summary>
        public static bool SiteOf(string siteId, out LandmarkSite site, out LandmarkFile file)
        {
            int k = LandmarkRules.KindOf(siteId);
            if (k < 0)
            {
                site = null;
                file = null;
                return false;
            }
            return SiteOf((LandmarkKind)k, out site, out file);
        }

        // ---------------------------------------------------------------- the day

        private void DayWork(int day)
        {
            if (!DistrictBuildings.Lives)
            {
                RefreshSites();
                return;   // nothing moves while the district is not a district of the game at this load
            }
            if (_state.Anchor <= 0)
            {
                _state.Anchor = day;
            }
            RefreshSites();
            bool session = GoingPublic.InSession;
            for (int k = 0; k < _state.Files.Length; k++)
            {
                LandmarkFile f = _state.Files[k];
                switch (f.Stage)
                {
                    case LandmarkStage.Planned:
                        if (day >= LandmarkRules.AnnounceOn(f.Kind, _state.Anchor, session) && SiteKnown[k])
                        {
                            Announce(f, Sites[k], day);
                        }
                        break;
                    case LandmarkStage.Announced:
                        PutOut(f, Sites[k], day);
                        break;
                    case LandmarkStage.Tendered:
                        TenderDay(f, day);
                        break;
                    case LandmarkStage.Building:
                        WorksDay(f, Sites[k], day);
                        break;
                    case LandmarkStage.Finished:
                        if (!f.InDistrict)
                        {
                            ToDistrict(f, Sites[k]);
                        }
                        if (day > f.Opening)
                        {
                            Open(f, false);   // its day has passed: it opened without the player
                        }
                        break;
                    default:
                        if (!f.InDistrict)
                        {
                            ToDistrict(f, Sites[k]);
                        }
                        break;
                }
            }
            if (Verbose)
            {
                Logger?.Info("Landmarks check (day " + day + "): " + Describe() + "; " + HeldLots.Count + " lot(s) held: PASS.");
            }
        }

        // The landmark in the City Ledger, its site settled for good; its tender put out, or the city's own works planned.
        private void Announce(LandmarkFile f, LandmarkSite site, int day)
        {
            f.Stage = LandmarkStage.Announced;
            f.Block = site.Block;
            f.Announced = day;
            Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + " announced on day " + day + ", on block " + site.Block + " (" + site.Lots.Count + " lots, "
                         + Mathf.RoundToInt(site.Ground.Width) + " by " + Mathf.RoundToInt(site.Ground.Depth) + " m).");
            _builtStamp++;
            PutOut(f, site, day);
        }

        // Out to tender when the construction game can take it; the city's own works otherwise, or in a Going Public session.
        private void PutOut(LandmarkFile f, LandmarkSite site, int day)
        {
            if (f.Kind != LandmarkKind.Stadium && string.IsNullOrEmpty(f.Design) && !PickDesign(f, site) && BuildingCopies.Available)
            {
                if (day - f.Announced < LandmarkRules.LapseDays)
                {
                    return;   // the city's designs are still being measured: the next day's pass
                }
            }
            if (GoingPublic.InSession || LandmarkBridges.OpenTender == null)
            {
                CityWorks(f, day, GoingPublic.InSession ? "a Going Public session keeps the city's own timetable" : "the construction game is not there");
                return;
            }
            int tender = LandmarkBridges.OpenTender(LandmarkRules.SiteId(f.Kind), LandmarkRules.UseOf(f.Kind), LandmarkRules.AreaOf(f.Kind), LandmarkRules.StoreysOf(f.Kind), out string why);
            if (tender <= 0)
            {
                CityWorks(f, day, why ?? "no tender could be put out");
                return;
            }
            f.Tender = tender;
            f.Stage = LandmarkStage.Tendered;
            Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + " out to tender (tender " + tender + ").");
        }

        // A tender's day: awarded meanwhile (the award tells us), still open, or lapsed and taken on by the city.
        private void TenderDay(LandmarkFile f, int day)
        {
            int state = LandmarkBridges.TenderState != null ? LandmarkBridges.TenderState(f.Tender) : -1;
            if (state == 0 && day - f.Announced <= LandmarkRules.LapseDays + 4)
            {
                return;   // open: its bids close on its hour
            }
            if (f.Stage == LandmarkStage.Tendered)
            {
                CityWorks(f, day, state == 0 ? "its tender ran past its time" : "its tender is no longer in the construction game");
            }
        }

        // The city's own works department builds it, on the city's timetable.
        private void CityWorks(LandmarkFile f, int day, string why)
        {
            f.Builder = LandmarkBuilder.City;
            f.Tender = 0;
            f.Started = Math.Max(day, f.Announced + LandmarkRules.CityLead);
            f.Due = LandmarkRules.CityDue(f.Kind, f.Started);
            f.Stage = LandmarkStage.Building;
            f.Phase = -1;
            Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + " built by the city's own works (" + why + "), from day " + f.Started + " to day " + f.Due + ".");
        }

        /// <summary>A landmark's tender awarded in the construction game: the player's company or a rival builds it now.</summary>
        public static void Awarded(string siteId, int winner, long price, int days)
        {
            if (!_loaded || !SiteOf(siteId, out _, out LandmarkFile f) || f.Stage > LandmarkStage.Tendered)
            {
                return;
            }
            int day = LandmarksGame.Day;
            f.Builder = winner < 0 ? LandmarkBuilder.Player : Math.Min(winner, 31);
            f.Price = Math.Max(0L, price);
            f.Started = day + 1;
            f.Due = winner < 0 ? 0 : LandmarkRules.AfterWorkingDays(f.Started, Math.Max(1, days));
            f.Stage = LandmarkStage.Building;
            f.Phase = -1;
            Written();
            Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + "'s tender won by " + (winner < 0 ? "the player's company" : "rival " + winner) + " for " + price
                         + (winner < 0 ? "; their project builds it." : ", due on day " + f.Due + "."));
        }

        // A day of a rival's or the city's works: their site shown going up, the City Ledger told of each phase, the handover on its day.
        private void WorksDay(LandmarkFile f, LandmarkSite site, int day)
        {
            if (f.Builder == LandmarkBuilder.Player)
            {
                return;   // the player's company's project: the construction game hands it over to us
            }
            if (day < f.Started)
            {
                return;
            }
            if (f.SiteId > 0 && (LandmarkBridges.Site == null || !LandmarkBridges.Site(f.SiteId, out _, out _)))
            {
                f.SiteId = 0;   // the programme no longer has it (taken away, or handed over by a test): the timetable still hands it over
            }
            if (f.SiteId == 0 && !GoingPublic.IsGuest && LandmarkBridges.StartPlot != null)
            {
                int storeys = Mathf.Clamp(f.Height > 0f ? Mathf.RoundToInt(f.Height / 3.6f) : LandmarkRules.StoreysOf(f.Kind), 1, 10);
                f.SiteId = LandmarkBridges.StartPlot(new Vector3(site.Plot.X, UptownLots.GroundY, site.Plot.Z), site.Plot.Yaw, site.Plot.Width, site.Plot.Depth, storeys,
                    f.Builder >= 0 ? f.Builder : LandmarkRules.CityContractor(f.Kind), Math.Max(40, LandmarkRules.CityDays(f.Kind) * 10), out string why);
                if (f.SiteId <= 0)
                {
                    f.SiteId = 0;
                    Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + "'s site could not be set out today (" + why + "); its works keep their timetable all the same.");
                }
            }
            PhaseDay(f, day);
            if (day >= f.Due)
            {
                if (f.SiteId > 0 && LandmarkBridges.FinishSite != null)
                {
                    LandmarkBridges.FinishSite(f.SiteId);   // the frame comes down out of view
                }
                f.SiteId = 0;
                Finish(f, site, day);
            }
        }

        // The phase the works are in, told once to the City Ledger as each begins.
        private void PhaseDay(LandmarkFile f, int day)
        {
            int span = Math.Max(1, f.Due - f.Started);
            int phase = LandmarkRules.Phase((day - f.Started) / (double)span);
            if (phase != f.Phase && phase > 0)
            {
                f.Phase = phase;
                f.PhaseDay = day;
            }
        }

        /// <summary>The player's company handed a landmark over (its project's handover): it is built, and the district's from the next load.</summary>
        public static bool Handed(string siteId)
        {
            if (!_loaded || !SiteOf(siteId, out LandmarkSite site, out LandmarkFile f))
            {
                return false;
            }
            if (f.Built)
            {
                return f.InDistrict;
            }
            if (Instance == null)
            {
                return false;
            }
            Instance.Finish(f, site, LandmarksGame.Day);
            Written();
            return f.InDistrict;
        }

        // Handed over: its buildings to the district, its opening the next day, its crowds' lift on the lots round it.
        private void Finish(LandmarkFile f, LandmarkSite site, int day)
        {
            f.Stage = LandmarkStage.Finished;
            f.Finished = day;
            f.Opening = day + 1;
            _builtStamp++;
            ToDistrict(f, site);
            DistrictBuildings.LiftsChanged();
            _trafficFor = -1;
            _streetStale = true;
            Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + " handed over on day " + day + (f.InDistrict ? ", the district's from the next load" : string.Empty)
                         + "; it opens on day " + f.Opening + ".");
        }

        // The tower's and the mall's buildings handed to the district as the city's, to let; nothing for the stadium. Asked again each day until done.
        private static void ToDistrict(LandmarkFile f, LandmarkSite site)
        {
            if (f.Kind == LandmarkKind.Stadium)
            {
                f.InDistrict = true;   // the mod's own pieces: no building of the game's
                return;
            }
            if (!DistrictBuildings.Lives)
            {
                return;
            }
            if (string.IsNullOrEmpty(f.Size) && !PickDesign(f, site))
            {
                if (BuildingCopies.Available)
                {
                    return;   // the city's designs are being measured: the next day's pass
                }
                Unmeasured(f);
            }
            bool anchored = DistrictBuildings.Occupied(site.Address);
            if (!anchored)
            {
                Over.Clear();
                for (int i = 0; i < site.Anchor.Count; i++)
                {
                    if (site.Anchor[i] != site.Address)
                    {
                        Over.Add(site.Anchor[i]);
                    }
                }
                anchored = DistrictBuildings.FinishOver(site.Address, Over, string.IsNullOrEmpty(f.Design) ? null : f.Design, f.Size, f.Version, f.Type, 0, FinishOwner.City, 0,
                    out string why);
                if (!anchored)
                {
                    Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + "'s building is not the district's yet (" + why + "); asked again each day.");
                }
            }
            bool shops = true;
            for (int i = 0; i < site.Units.Count; i++)
            {
                if (!DistrictBuildings.Occupied(site.Units[i]) && !Shop(site.Units[i]))
                {
                    shops = false;
                }
            }
            f.InDistrict = anchored && shops;
        }

        // One of the mall's shops on its own lot: the largest shop design of the city's that fits it and is of a size its class takes, to let.
        private static bool Shop(string lotId)
        {
            Lot lot = UptownLots.Get(lotId);
            if (lot == null)
            {
                return false;
            }
            string design = null, size = null, type = ShopType;
            int version = 1;
            if (BuildingCopies.Fitting(lot.Width, lot.Depth, ShopType, DesignsAsked, Fits))
            {
                for (int i = 0; i < Fits.Count && design == null; i++)
                {
                    if (Takes(lot, Fits[i].Size))
                    {
                        design = Fits[i].Name;
                        size = Fits[i].Size;
                        version = Fits[i].Version;
                        type = Fits[i].Type;
                    }
                }
            }
            else if (BuildingCopies.Available)
            {
                return false;   // being measured
            }
            if (size == null)
            {
                size = lot.Sizes.Count > 0 ? lot.Sizes[lot.Sizes.Count > 2 ? 2 : 0] : null;   // a small lot's shop: the game's size C
            }
            bool done = DistrictBuildings.Finish(lotId, design, size, version, type, 0, FinishOwner.City, 0, out string why);
            if (!done)
            {
                Logger?.Info("Landmarks: the mall's shop on lot " + lotId + " is not the district's yet (" + why + ").");
            }
            return done;
        }

        private static bool Takes(Lot lot, string size)
        {
            for (int i = 0; i < lot.Sizes.Count; i++)
            {
                if (string.Equals(lot.Sizes[i], size, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the design

        /// <summary>
        /// The main building's design, picked once and kept: of the city's own designs that fit both the works' frame and the
        /// ground the building stands on, the tallest office for the tower and the largest shop (the best covering, the copy
        /// engine's order) for the mall. False while the city's designs are being measured or none fits.
        /// </summary>
        private static bool PickDesign(LandmarkFile f, LandmarkSite site)
        {
            if (f.Kind == LandmarkKind.Stadium)
            {
                return false;
            }
            PlotRules.Frame(site.Plot.Width, site.Plot.Depth, out float minX, out float maxX, out float minZ, out float maxZ);
            bool alongX = site.Plot.Yaw % 180 == 0;   // the works' land faces the avenue: its width runs along X
            float groundWidth = alongX ? site.AnchorGround.Width : site.AnchorGround.Depth, groundDepth = alongX ? site.AnchorGround.Depth : site.AnchorGround.Width;
            float width = Mathf.Min(maxX - minX, groundWidth), depth = Mathf.Min(maxZ - minZ, groundDepth);
            if (!BuildingCopies.Fitting(width, depth, f.Kind == LandmarkKind.Tower ? OfficeType : ShopType, DesignsAsked, Fits) || Fits.Count == 0)
            {
                return false;
            }
            int pick = 0;
            if (f.Kind == LandmarkKind.Tower)
            {
                Heights.Clear();
                for (int i = 0; i < Fits.Count; i++)
                {
                    Heights.Add(Fits[i].Height);
                }
                pick = Math.Max(0, LandmarkRules.Tallest(Heights));
            }
            DesignFit d = Fits[pick];
            f.Design = d.Name ?? string.Empty;
            f.Size = d.Size ?? string.Empty;
            f.Version = d.Version;
            f.Type = d.Type ?? string.Empty;
            f.Width = d.Width;
            f.Depth = d.Depth;
            f.Height = d.Height;
            f.Area = d.TotalSqm;
            Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + " wears " + f.Design + " (" + f.Size + ", " + Mathf.RoundToInt(f.Height) + " m high, " + Mathf.RoundToInt(f.Width)
                         + " by " + Mathf.RoundToInt(f.Depth) + " m), of " + Fits.Count + " design(s) of the city's that fit " + Mathf.RoundToInt(width) + " by " + Mathf.RoundToInt(depth) + " m.");
            return true;
        }

        // With the copy engine switched off nothing can be measured: the game's largest office and shop structures, any design of their size at the load.
        private static void Unmeasured(LandmarkFile f)
        {
            f.Design = string.Empty;
            f.Size = f.Kind == LandmarkKind.Tower ? "ba:buildingsize_k" : "ba:buildingsize_m";
            f.Version = 1;
            f.Type = f.Kind == LandmarkKind.Tower ? OfficeType : ShopType;
            f.Width = f.Kind == LandmarkKind.Tower ? 28f : 26f;
            f.Depth = f.Kind == LandmarkKind.Tower ? 38f : 33f;
            f.Area = LandmarkRules.AreaOf(f.Kind);
            Logger?.Info("Landmarks: the copy engine is off, so " + LandmarkRules.Name(f.Kind) + " is built in the game's " + f.Size + " with a design of that size.");
        }

        /// <summary>The design a landmark's main building wears, by its site's id: its name, structure, footprint and floor area; false for the stadium or one not picked yet.</summary>
        public static bool DesignOf(string siteId, out string name, out string size, out int version, out string type, out float height, out float width, out float depth, out int area)
        {
            name = size = type = null;
            version = area = 0;
            height = width = depth = 0f;
            if (!SiteOf(siteId, out LandmarkSite site, out LandmarkFile f) || f.Kind == LandmarkKind.Stadium)
            {
                return false;
            }
            if (string.IsNullOrEmpty(f.Size) && !PickDesign(f, site))
            {
                return false;
            }
            name = f.Design;
            size = f.Size;
            version = f.Version;
            type = f.Type;
            height = f.Height;
            width = f.Width;
            depth = f.Depth;
            area = f.Area > 0 ? f.Area : LandmarkRules.AreaOf(f.Kind);
            return true;
        }

        // ---------------------------------------------------------------- the programme's questions

        /// <summary>The landmark's site id whose rival's or city's works a site of the programme shows; null for any other site.</summary>
        public static string Works(int siteId)
        {
            if (!_loaded || siteId <= 0)
            {
                return null;
            }
            LandmarkFile f = _state.OnSite(siteId);
            if (f != null)
            {
                return LandmarkRules.SiteId(f.Kind);
            }
            if (GoingPublic.IsGuest && LandmarkBridges.Site != null && LandmarkBridges.Site(siteId, out float x, out float z))
            {
                // A guest follows the host's sites: the host's landmark works are known by where they stand.
                SitesToday();
                for (int k = 0; k < Sites.Length; k++)
                {
                    if (SiteKnown[k] && _state.Files[k].Stage == LandmarkStage.Building && Mathf.Abs(Sites[k].Plot.X - x) < 1f && Mathf.Abs(Sites[k].Plot.Z - z) < 1f)
                    {
                        return LandmarkRules.SiteId(_state.Files[k].Kind);
                    }
                }
            }
            return null;
        }

        /// <summary>The hours a landmark's own works site does in a working hour: the timetable's pace, its handover on its due day.</summary>
        public static float Pace(int siteId, double done, double needed, float hours)
        {
            LandmarkFile f = _loaded ? _state.OnSite(siteId) : null;
            if (f == null || f.Due <= 0)
            {
                return hours;
            }
            return LandmarkRules.Pace(done, needed, hours, LandmarksGame.Day, f.Started, f.Due);
        }

        /// <summary>True when a landmark's site needs no look of the programme's now: the stadium always (its pieces are its own), the others once the district's building stands.</summary>
        public static bool Built(string siteId)
        {
            if (!SiteOf(siteId, out LandmarkSite site, out LandmarkFile f))
            {
                return false;
            }
            return f.Kind == LandmarkKind.Stadium || DistrictBuildings.Finished(site.Address);
        }

        // ---------------------------------------------------------------- the lift

        private static LandmarkState _liftState;          // the state the lift reads: the director's, or the save's line read while the city loads
        private static string _liftText;                  // ...the line it was read from
        private static int _liftStamp = int.MinValue;
        private static readonly List<LandmarkKind> LiftKinds = new List<LandmarkKind>(3);
        private static readonly List<SiteRect> LiftGrounds = new List<SiteRect>(3);
        private static readonly LandmarkSite LiftSite = new LandmarkSite();

        /// <summary>
        /// The traffic points a lot of the district gets from the landmarks built near it (DistrictBuildings.TrafficLift), from
        /// this save's line alone: asked by Uptown while the city loads, before this director has read it. 0 with the option off.
        /// </summary>
        public static int LiftOf(string lotId)
        {
            if (!Settings.Landmarks)
            {
                return 0;
            }
            Lot lot = UptownLots.Get(lotId);
            if (lot == null || !LiftReady())
            {
                return 0;
            }
            return LandmarkRules.Lift(lot.X, lot.Z, LiftKinds, LiftGrounds, LiftKinds.Count);
        }

        // The built landmarks' grounds, worked out again only when the state they come from changes.
        private static bool LiftReady()
        {
            GameInstance save = SaveGameManager.Current;
            LandmarkState state;
            if (_loaded && ReferenceEquals(_save, save))
            {
                state = _state;
            }
            else
            {
                string text = save != null && save.modData != null && save.modData.TryGetValue(LandmarkStore.Key, out string line) ? line : null;
                if (!ReferenceEquals(text, _liftText) || _liftState == null)
                {
                    _liftText = text;
                    _liftState = string.IsNullOrEmpty(text) ? new LandmarkState() : LandmarkStore.Read(text, out _) ?? new LandmarkState();
                    _liftStamp = int.MinValue;
                }
                state = _liftState;
            }
            int stamp = ReferenceEquals(state, _state) ? _builtStamp : int.MinValue + 1;
            if (stamp == _liftStamp && ReferenceEquals(state, _liftFrom))
            {
                return true;
            }
            _liftFrom = state;
            _liftStamp = stamp;
            ReadLots();
            LiftKinds.Clear();
            LiftGrounds.Clear();
            for (int k = 0; k < state.Files.Length; k++)
            {
                LandmarkFile f = state.Files[k];
                if (f.Built && f.Block > 0 && LandmarkRules.SiteOn(f.Kind, f.Block, Lots, null, LiftSite))
                {
                    LiftKinds.Add(f.Kind);
                    LiftGrounds.Add(LiftSite.Ground);
                }
            }
            return true;
        }

        private static LandmarkState _liftFrom;
    }
}
