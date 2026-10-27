using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The city's growth (plans/growables.md section 2; the owner chose a steady pace, 26 Oct 2026): once a game day, where a
    /// use's demand is over its threshold and fewer than a few grown sites are under way, a developer (a rival builder's arm, or
    /// a rival tycoon's holding at a tier of its own) takes free land the council zones for the use along a street of the block
    /// that is filling, as one site by the site rule, pays the city its price, and puts a building through the player's own
    /// pipeline: its architect's plans, City Hall's permission by the player's rules, a plot site in the city's programme going
    /// up on the schedule of its working hours (build mode's under construction view, the markers, the objectives near the
    /// player's land, the City Ledger), and the district's one registration step (FinishLot, FinishOverLot: whole, checked). Its
    /// unit is let to a tenant within days or weeks by the demand (LifeDirector.LetUnit, as the mix pass lets one), and until then
    /// stands for rent under the map's For rent filters, for the player and the rivals. Each week a standing grown building's
    /// condition moves: let, it is won back; mostly empty past four weeks, it falls, its traffic and so its rent drop, and under
    /// 400 it is marked run down, first in the city's developers' order to redevelop; a refurbishment makes it as new. The old
    /// lot-by-lot developments start nothing more while the growth runs (those under way finish). Outside a Going Public session
    /// alone: nothing of Uptown changes on one machine only. Every pick from the save's seed and the day.
    /// COST: nothing per frame; the day's pass reads the district's columns once, a week's pass looks at each grown building.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly Dictionary<string, GrowFile> _growLots = new Dictionary<string, GrowFile>(64, StringComparer.Ordinal);   // every space a grown building takes
        private int _growLotsStamp = int.MinValue, _growChanged;
        private GrowColumn[] _growCols = new GrowColumn[0];
        private readonly int[] _growDemand = new int[(int)BuildUse.Count];
        private readonly int[] _growFree = new int[(int)BuildUse.Count];
        private readonly List<LotView> _growSpaces = new List<LotView>(8);
        private readonly List<string> _growOver = new List<string>(8);
        private readonly List<LotView> _growIndexSpaces = new List<LotView>(8);   // the index's own: FreeSpace asks it while _growSpaces is in use
        private readonly List<int> _growOrder = new List<int>(32);
        private bool _growDropsDue = true;   // a load makes every record at its own traffic: each grown building's drop is asked again once

        /// <summary>True while the city grows: the construction game, its developers and the growth on, Uptown standing, outside a session.</summary>
        public bool GrowthOn => Running && Settings.ConstructionGrowth && Settings.ConstructionDevelopers && !GoingPublic.InSession && DistrictLives();

        // ------------------------------------------------------------------ the spaces taken

        /// <summary>The grown building (under way or standing) that takes a lot, or null.</summary>
        public GrowFile GrowOf(string lot)
        {
            if (string.IsNullOrEmpty(lot) || _state == null)
            {
                return null;
            }
            int lots = Lots.Count;   // the district's lots read first: their stamp moves when they change
            int stamp = _lotsStamp * 31 + _growChanged;
            if (stamp != _growLotsStamp)
            {
                _growLotsStamp = stamp;
                _growLots.Clear();
                for (int i = 0; i < _state.Grows.Count && lots > 0; i++)
                {
                    GrowFile g = _state.Grows[i];
                    if (g.Stage == GrowStage.Dropped || !SiteSpaces(g.Lot, g.Spaces, _growIndexSpaces))
                    {
                        continue;
                    }
                    for (int k = 0; k < _growIndexSpaces.Count; k++)
                    {
                        _growLots[_growIndexSpaces[k].Id] = g;
                    }
                }
                _growIndexSpaces.Clear();
            }
            return _growLots.TryGetValue(lot, out GrowFile found) ? found : null;
        }

        // A grown site under way (not the player's) takes the lot: nobody else may buy or build there.
        private bool GrowUnderWayOn(string lot)
        {
            GrowFile g = GrowOf(lot);
            return g != null && g.UnderWay && g.Owner != GrowOwner.Player;
        }

        private void GrowsChanged()
        {
            _growChanged++;
            _cellsFactsStamp++;
        }

        // ------------------------------------------------------------------ the day

        // The day's pass: the council's sitting, each grown building moved on, the week's look at the standing ones, a new site begun.
        private void GrowDay(int day)
        {
            if (GoingPublic.InSession || !Running)
            {
                return;
            }
            CouncilZoningDay(day);
            if (!GrowthOn)
            {
                return;
            }
            for (int i = 0; i < _state.Grows.Count; i++)
            {
                GrowFile g = _state.Grows[i];
                if (g.UnderWay || g.Stage == GrowStage.Letting)
                {
                    GrowStep(g, day, false);
                }
            }
            GrowWeek(day);
            if (_state.NextGrowDay <= 0)
            {
                _state.NextGrowDay = day + GrowRules.FirstDay;   // the first look: the market is counted first
                return;
            }
            // Each district grows by its own demand and under its own cap (GrowRules.SitesMost sites at once): the day's turn goes to one
            // first and the other next, so neither is always the one that waits.
            for (int k = 0; k < BuildCatalogue.Districts; k++)
            {
                int district = (day + k) % BuildCatalogue.Districts;
                if (GrowRules.MayStart(GrowingIn(district), day, _state.NextGrowDay) && SiteCount() < BuildCatalogue.SitesMost && GrowBegin(day, -1, district) != null)
                {
                    _state.NextGrowDay = day + GrowRules.GapDays;
                    break;
                }
            }
        }

        /// <summary>Grown sites under way now in a district (the player's own are not the city's to count).</summary>
        public int GrowingIn(int district)
        {
            int n = 0;
            for (int i = 0; i < _state.Grows.Count; i++)
            {
                n += _state.Grows[i].UnderWay && DistrictOfLot(_state.Grows[i].Lot) == district ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// A new grown site in a district (or in the first that has one, for a test that asks for none): the use that grows (or the one
        /// asked, for a test), the land picked, a design that fills it, the land bought from the city. Null when nothing is wanted, no
        /// land is zoned for it, or the city's designs are being read.
        /// </summary>
        internal GrowFile GrowBegin(int day, int asked, int district = -1)
        {
            if (district < 0)
            {
                for (int d = 0; d < BuildCatalogue.Districts; d++)
                {
                    GrowFile one = GrowBegin(day, asked, d);
                    if (one != null)
                    {
                        return one;
                    }
                }
                return null;
            }
            CellGrid grid = Cells;
            if (grid.Columns.Count == 0)
            {
                return null;
            }
            if (_growCols.Length != grid.Columns.Count)
            {
                _growCols = new GrowColumn[grid.Columns.Count];
            }
            for (int u = 0; u < _growDemand.Length; u++)
            {
                _growDemand[u] = DemandFor((BuildUse)u, district);
                _growFree[u] = 0;
            }
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                bool free = IsFreeColumn(c) && DistrictOfColumn(c) == district;   // only the land of the district whose turn it is
                int use = free ? CouncilColumnUse(c) : -1;
                _growCols[c] = new GrowColumn { Free = free, Use = use, Growing = LotOfColumn(c, out LotView lot) && GrowUnderWayOn(lot.Id) };
                if (use >= 0)
                {
                    _growFree[use]++;
                }
            }
            if (asked >= 0)
            {
                for (int u = 0; u < _growDemand.Length; u++)
                {
                    _growDemand[u] = u == asked ? 100 : -1;   // a test's use, wanted whatever the district's count says
                }
            }
            int wanted = GrowRules.UseToGrow(_growDemand, _growFree);
            if (wanted < 0)
            {
                return null;   // no use wanted enough, or none with land zoned for it: the city waits
            }
            if (!GrowRules.Pick(grid, _growCols, _growDemand, _state.Seed, day, out int grow, out int first, out int run, out int want))
            {
                Logger?.Info("Construction: the city's growth wants " + (BuildUse)wanted + " (demand " + _growDemand[wanted] + ") on day " + day + ", but no block has room for a site of it now.");
                return null;
            }
            int id = _state.NewId();
            Holdings(_holdings);
            bool holding = GrowRules.ByHolding(_state.Seed, id, _holdings.Count > 0);
            GrowTier tier = holding ? GrowRules.HoldingTier(_state.Seed, id) : GrowTier.Standard;
            for (int count = Math.Min(want, run); count >= 1; count--)
            {
                int start = GrowRules.Take(grid, first, run, count);
                if (!LotOfColumn(start, out LotView firstLot) || !SiteSpaces(firstLot.Id, count, _growSpaces))
                {
                    continue;
                }
                bool free = true;
                for (int k = 0; k < _growSpaces.Count; k++)
                {
                    free &= FreeSpace(_growSpaces[k]);
                }
                if (!free)
                {
                    continue;
                }
                LotView site = Joined(_growSpaces);
                if (!GrowDesign(site, (BuildUse)grow, tier, holding, out string design))
                {
                    return null;   // the city's designs are being looked through: the next day's pass
                }
                if (design == NoDesign)
                {
                    continue;   // nothing of the city's fills so many spaces: fewer
                }
                var g = new GrowFile
                {
                    Id = id, Stage = GrowStage.Plans, Owner = holding ? GrowOwner.Holding : GrowOwner.Builder, Lot = firstLot.Id, Spaces = count, Use = (BuildUse)grow, Tier = tier,
                    Day = day, Design = design, Price = SitePrice(firstLot.Id, count)
                };
                g.Rival = DevelopmentRules.Rival(g.Lot, g.Use);
                g.Holding = holding ? _holdings[RedevelopRules.PickOf(_state.Seed, day, 0x67, _holdings.Count)] : string.Empty;
                g.Until = day + GrowRules.PlansDays(_state.Seed, g.Id);
                _state.Grows.Add(g);
                _state.CityLand += g.Price;
                TidyGrows();
                GrowsChanged();
                Paper("reallife_grow_paper_bought", GrowName(g), count);
                Logger?.Info("Construction: the city's growth: " + GrowDeveloper(g) + " (builder " + g.Rival + (holding ? ", tier " + tier : string.Empty) + ") bought " + count + " space(s) from "
                             + g.Lot + " for " + g.Price + " to build " + g.Use + " in " + design + " (demand " + _growDemand[grow] + "); plans until day " + g.Until + ".");
                Changed();
                return g;
            }
            Logger?.Info("Construction: the city's growth found " + run + " free space(s) for " + (BuildUse)grow + " from column " + first + ", but no design of the city's fills them.");
            return null;
        }

        // The oldest given up make room: standing ones are always kept.
        private void TidyGrows()
        {
            for (int i = 0; i < _state.Grows.Count && _state.Grows.Count > BuildState.GrowsMost; i++)
            {
                if (_state.Grows[i].Stage == GrowStage.Dropped)
                {
                    _state.Grows.RemoveAt(i);
                    i--;
                }
            }
        }

        // The design a grown site wears: a builder's arm picks one of those that fill it by its lot (as the developers always have), a
        // holding by its tier and the demand among them by floor area; false while the designs are being looked through, NoDesign for none.
        private bool GrowDesign(LotView site, BuildUse use, GrowTier tier, bool byTier, out string design)
        {
            design = null;
            SiteGround(site, out float w, out float d);
            if (!ReadDesigns(w, d, BuildCatalogue.UseTypes[(int)use], DesignsAsked, _choices))
            {
                return false;
            }
            KeepFitting(site, _choices);
            if (!CityCorners(site, use, _choices))
            {
                return false;
            }
            int pick = byTier ? BySize(_choices, GrowRules.TierPick(_choices.Count, tier, DemandFor(use, site.District))) : DevelopmentRules.DesignPick(site.Id, _choices.Count);
            design = pick >= 0 ? KeptName(_choices[pick]) : NoDesign;
            return true;
        }

        // The design at a rank of floor area (0 the smallest) among those given: its place in the list, or -1.
        private int BySize(List<DesignChoice> designs, int rank)
        {
            _growOrder.Clear();
            for (int i = 0; i < designs.Count; i++)
            {
                int k = _growOrder.Count;
                _growOrder.Add(i);
                while (k > 0 && designs[_growOrder[k - 1]].TotalSqm > designs[i].TotalSqm)
                {
                    _growOrder[k] = _growOrder[k - 1];
                    k--;
                }
                _growOrder[k] = i;
            }
            return rank >= 0 && rank < _growOrder.Count ? _growOrder[rank] : -1;
        }

        // A grown site's ground as one lot; false when one of its spaces is not in the district at this load.
        private bool GrowGround(GrowFile g, out LotView site)
        {
            site = default(LotView);
            if (!SiteSpaces(g.Lot, g.Spaces, _growSpaces))
            {
                return false;
            }
            site = Joined(_growSpaces);
            return true;
        }

        /// <summary>
        /// One grown site moved on by a day: its plans to City Hall, City Hall's decision and its works begun, its building handed to
        /// the district, its unit let. A test rushes it: each stage ends now and the permission is granted.
        /// </summary>
        internal void GrowStep(GrowFile g, int day, bool rush)
        {
            if (!rush && day < g.Until)
            {
                return;
            }
            switch (g.Stage)
            {
                case GrowStage.Plans:
                    g.Stage = GrowStage.Planning;
                    g.Until = rush ? day : PlanningRules.AfterWorkingDays(day, PlanningRules.WorkingDays(GrowArea(g)));
                    Paper("reallife_grow_paper_applied", GrowName(g), 0);
                    break;
                case GrowStage.Planning:
                    GrowDecide(g, day, rush);
                    break;
                case GrowStage.Building:
                    GrowBuilt(g, day);
                    break;
                case GrowStage.Letting:
                    GrowLet(g, day);
                    break;
            }
            Changed();
        }

        // Its floor area: its design's, else the usual building of its ground's.
        private int GrowArea(GrowFile g)
        {
            if (!GrowGround(g, out LotView site))
            {
                return 1;
            }
            return DesignOf(site, g.Use, g.Design, out DesignChoice d) && d.TotalSqm > 0 ? d.TotalSqm : Math.Max(1, LandRules.UsualArea(site.Width, site.Depth, site.Class));
        }

        // City Hall's decision by the player's own rules: the neighbours' objections, the council's zoning (the land is zoned for it),
        // the developer's standing and its plans' quality. Granted: its plot site goes up on its working hours' schedule.
        private void GrowDecide(GrowFile g, int day, bool rush)
        {
            if (!GrowGround(g, out LotView site) || !DesignOf(site, g.Use, g.Design, out DesignChoice d))
            {
                if (day > g.Until + GrowRules.PatienceDays)
                {
                    GrowDrop(g, day, "its design no longer fits its ground");
                }
                return;   // the designs are being looked through: the next day's pass
            }
            int storeys = PlansRules.Storeys(d.Height);
            int quality = g.Owner == GrowOwner.Holding ? GrowRules.TierQuality[(int)g.Tier] : GrowRules.Quality;
            int reputation = _state.Rivals[g.Rival].Reputation;
            int objections = PlanningRules.Objections(_state.Seed, g.Id * 13 + 1, storeys, 4, true, 0, reputation, quality);
            bool granted = rush || PlanningRules.Decide(_state.Seed, g.Id, 1, true, 0, quality, reputation, objections, Permits(), out _);
            if (!granted)
            {
                GrowDrop(g, day, "City Hall refused it (" + objections + " objection(s))");
                Paper("reallife_grow_paper_refused", GrowName(g), objections);
                return;
            }
            int hours = PlansRules.Hours(g.Use, Math.Max(1, d.TotalSqm), storeys, ProjectKind.Plot);
            g.Stage = GrowStage.Building;
            g.Until = rush ? day : PlanningRules.AfterWorkingDays(day, TenderRules.StandardDays(hours));
            g.SiteId = SiteCount() < BuildCatalogue.SitesMost
                ? StartPlotSite(new Vector3(site.X, site.Y, site.Z), site.Yaw, site.Width, site.Depth, LandRules.FrameStoreys(site.Width, storeys), g.Rival, hours, out _) : 0;
            Paper("reallife_grow_paper_started", GrowName(g), storeys);
            Logger?.Info("Construction: the grown site at " + g.Lot + " has its permission (" + objections + " objection(s)); " + hours + " working hour(s) until day " + g.Until
                         + (g.SiteId > 0 ? ", site " + g.SiteId : ", no site shown (the city's programme is full)") + ".");
        }

        // Its last day: the district's (the one registration step: it stands now, or when nobody sees its ground), its builder's
        // record, the stock market's news, the building of the year, the land tax the city had while it went up; then its unit to let.
        private void GrowBuilt(GrowFile g, int day)
        {
            if (!GrowGround(g, out LotView site))
            {
                GrowDrop(g, day, "its ground is not in the district at this load");
                return;
            }
            if (!DesignOf(site, g.Use, g.Design, out DesignChoice d))
            {
                if (day > g.Until + GrowRules.PatienceDays)
                {
                    GrowDrop(g, day, "its design no longer fits its ground");
                }
                return;
            }
            string type = BuildCatalogue.UseTypes[(int)g.Use], why;
            bool handed;
            if (g.Spaces > 1)
            {
                _growOver.Clear();
                for (int k = 1; k < _growSpaces.Count; k++)
                {
                    _growOver.Add(_growSpaces[k].Id);
                }
                handed = FinishOverLot(g.Lot, _growOver, d.Name, d.Size, d.Version, type, 0, false, 0.0, d.Look, out why);
                _growOver.Clear();
            }
            else
            {
                handed = FinishLot(g.Lot, d.Name, d.Size, d.Version, type, 0, false, 0.0, d.Look, out why);
            }
            if (!handed)
            {
                if (day > g.Until + GrowRules.PatienceDays)
                {
                    GrowDrop(g, day, "the district would not take its building (" + why + ")");
                    return;
                }
                Logger?.Info("Construction: the grown building on lot " + g.Lot + " is not the district's yet (" + why + "); asked again each day.");
                return;
            }
            if (g.SiteId > 0)
            {
                FinishSite(g.SiteId);   // the frame comes down and its look stands, out of view
                g.SiteId = 0;
            }
            g.Handed = day;
            _state.CityLandTax += LandRules.LandTax(g.Price, Math.Max(0, day - g.Day), ConstructionGame.DaysPerYear, false, Permits());
            _state.Rivals[g.Rival].Built++;
            RivalNews(g.Rival, RivalDeveloped, 1);   // the district grew: its builder, and a little every listed builder
            int quality = g.Owner == GrowOwner.Holding ? GrowRules.TierQuality[(int)g.Tier] : Math.Min(95, _state.Rivals[g.Rival].Reputation + 10);
            NoteFinished(AwardEntry.OfDevelopers, null, g.Lot, site.Street, site.Number, g.Use, g.Rival, quality, 0, day);
            int demand = DemandFor(g.Use, g.Lot);
            g.Stage = GrowStage.Letting;
            g.Until = day + GrowRules.LetDays(demand, _state.Seed, g.Id);
            GrowsChanged();
            Paper("reallife_grow_paper_opened", GrowName(g), 0);
            Logger?.Info("Construction: the grown building on lot " + g.Lot + " stands: " + g.Use + " in " + KeptName(d) + ", the city's, for rent until its tenant moves in on day " + g.Until
                         + " (demand " + demand + ").");
        }

        // Its tenant's day: the unit let as the district's mix lets one; one the player, a rival or the game's own letting took first
        // stays theirs. Then its condition is watched each week.
        private void GrowLet(GrowFile g, int day)
        {
            if (!StandingOnLot(g.Lot, out StandingView b))
            {
                if (day > g.Until + GrowRules.PatienceDays)
                {
                    g.Stage = GrowStage.Let;   // it never stood in view to be let: its condition is watched from now
                    g.Week = day / 7;
                }
                return;   // its ground in view since its keys: it stands at the next black screen
            }
            string why = LetUnitNow(g.Lot);
            g.Stage = GrowStage.Let;
            g.EmptyDays = 0;
            g.Week = day / 7;
            Logger?.Info("Construction: the grown building at " + b.Street + " " + b.Number + (why == null ? " has its tenant" : " was not let by the developer (" + why + ")")
                         + " on day " + day + ".");
        }

        private static string LetUnitNow(string lot)
        {
            try
            {
                return Down("LetUnit") || ConstructionBridges.LetUnit == null ? "the district is not there" : ConstructionBridges.LetUnit(lot);
            }
            catch (Exception e)
            {
                Faulty("LetUnit", e);
                return "a fault";
            }
        }

        private static bool RetrafficNow(string lot, int drop)
        {
            try
            {
                return !Down("Retraffic") && ConstructionBridges.Retraffic != null && ConstructionBridges.Retraffic(lot, drop);
            }
            catch (Exception e)
            {
                Faulty("Retraffic", e);
                return false;
            }
        }

        // Given up: its site taken down, its land left as it was (the developer keeps it for another day).
        private void GrowDrop(GrowFile g, int day, string why)
        {
            if (g.SiteId > 0)
            {
                DropSite(g.SiteId);
                g.SiteId = 0;
            }
            g.Stage = GrowStage.Dropped;
            g.Until = day;
            GrowsChanged();
            Logger?.Info("Construction: the grown site at " + g.Lot + " (" + GrowDeveloper(g) + ") is given up: " + why + ".");
        }

        // ------------------------------------------------------------------ the week: condition, tenants of the player's

        // Each standing grown building once a week: let or mostly empty, its condition won back or lost, its traffic dropped by it,
        // the run down mark; the player's own building's tenants found by the developer in its first weeks. After a load, every drop
        // is asked again at once (the game's records are made at each load at their own traffic).
        private void GrowWeek(int day)
        {
            int week = day / 7;
            for (int i = 0; i < _state.Grows.Count; i++)
            {
                GrowFile g = _state.Grows[i];
                if (g.Stage != GrowStage.Let)
                {
                    continue;
                }
                if (_growDropsDue && GrowRules.Drop(g.Condition) > 0)
                {
                    RetrafficNow(g.Lot, GrowRules.Drop(g.Condition));
                }
                if (g.Week == week)
                {
                    continue;
                }
                g.Week = week;
                GrowLook(g, day);
            }
            _growDropsDue = false;
        }

        // One building's week.
        private void GrowLook(GrowFile g, int day)
        {
            if (!StandingOnLot(g.Lot, out StandingView b))
            {
                return;   // not standing at this load (its ground in view since its keys, or knocked down: GrowGone lets it go)
            }
            bool empty;
            if (g.Owner == GrowOwner.Player)
            {
                if (!ConstructionGame.DeedLet(b.Street, b.Number, out int let, out int most))
                {
                    return;   // no deed of the player's any more: sold, its condition the new owner's
                }
                int weeks = (day - g.Handed) / 7;
                if (weeks < GrowRules.FillWeeks && let < most)
                {
                    let = ConstructionGame.DeedFill(b.Street, b.Number, GrowRules.WeekFill(DemandFor(g.Use, g.Lot), g.Tier));   // the developer's lettings beside the game's own
                }
                empty = let < GrowRules.MostlyEmptyBelow;
            }
            else
            {
                empty = BuildModeGame.Occupant(b.Street, b.Number, out bool vacant, out _) && vacant;
            }
            g.EmptyDays = empty ? g.EmptyDays + 7 : 0;
            bool wasDown = g.RunDown;
            int was = g.Condition;
            g.Condition = GrowRules.Condition(g.Condition, empty, g.EmptyDays);
            g.RunDown = GrowRules.RunDown(g.Condition, g.RunDown);
            if (GrowRules.Drop(g.Condition) != GrowRules.Drop(was) || _growDropsDue)
            {
                RetrafficNow(g.Lot, GrowRules.Drop(g.Condition));
            }
            if (g.RunDown && !wasDown)
            {
                Paper("reallife_grow_paper_rundown", GrowName(g), 0);
                if (g.Owner == GrowOwner.Player)
                {
                    Tell(NotificationType.Warning, "reallife_grow_notice_rundown", ConstructionGame.AddressText(b.Street, b.Number), null, TabProjects, g.Project);
                }
                GrowsChanged();
            }
            if (Verbose)
            {
                Logger?.Info("Construction check (grown building, week " + (day / 7) + "): " + b.Street + " " + b.Number + " " + (empty ? "mostly empty for " + g.EmptyDays + " day(s)" : "let")
                             + ", condition " + was + " to " + g.Condition + (g.RunDown ? ", run down" : string.Empty) + ", traffic " + GrowRules.Drop(g.Condition) + " point(s) down: PASS.");
            }
        }

        /// <summary>A building at an address refurbished (the player's refit, or a business's refit tender handed over): a grown one's condition as new, its traffic its own.</summary>
        internal void GrowRefurbished(string street, int number)
        {
            for (int i = 0; _state != null && i < _state.Grows.Count; i++)
            {
                GrowFile g = _state.Grows[i];
                if (!g.Standing || !Standing(street, number, out StandingView b) || !string.Equals(b.Lot, g.Lot, StringComparison.Ordinal))
                {
                    continue;
                }
                bool was = g.RunDown;
                g.Condition = GrowRules.ConditionFull;
                g.RunDown = false;
                g.EmptyDays = 0;
                RetrafficNow(g.Lot, 0);
                GrowsChanged();
                Logger?.Info("Construction: the grown building at " + street + " " + number + " was refurbished: its condition as new" + (was ? ", no longer run down" : string.Empty) + ".");
                return;
            }
        }

        /// <summary>A building on a lot knocked down: a grown one there is let go (its lots are the redevelopment's now).</summary>
        internal void GrowGone(string lot)
        {
            GrowFile g = GrowOf(lot);
            if (g != null && g.Standing)
            {
                g.Stage = GrowStage.Dropped;
                RetrafficNow(g.Lot, 0);
                GrowsChanged();
                Logger?.Info("Construction: the grown building on lot " + g.Lot + " was knocked down; its record let go.");
            }
        }

        /// <summary>True for a grown site on a block where the player holds land or a building of the district: the objectives say it.</summary>
        public bool GrowNearMine(GrowFile g)
        {
            if (g == null || !Space(g.Lot, out LotView at))
            {
                return false;
            }
            for (int i = 0; i < _state.Lots.Count; i++)
            {
                if (Space(_state.Lots[i].Id, out LotView mine) && mine.Block == at.Block)
                {
                    return true;
                }
            }
            IReadOnlyList<LotView> lots = Lots;
            for (int i = 0; i < lots.Count; i++)
            {
                if (lots[i].Block == at.Block && !string.IsNullOrEmpty(lots[i].Street) && LotTaken(lots[i].Id) && ConstructionGame.Owns(lots[i].Street, lots[i].Number))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the building on a lot is a grown one marked run down: the city's developers take it first.</summary>
        public bool RunDownOn(string lot)
        {
            GrowFile g = GrowOf(lot);
            return g != null && g.Standing && g.RunDown;
        }

        // ------------------------------------------------------------------ the site of the programme

        // The pace of a grown site: its schedule hands it over on its day, never the programme's crews.
        private bool GrowPace(int siteId, double done, double needed, float hours, out float pace)
        {
            pace = 0f;
            if (_state.GrowSite(siteId) == null)
            {
                return false;
            }
            pace = Running ? (float)Math.Max(0.0, Math.Min(hours, needed * 0.995 - done)) : 0f;
            return true;
        }

        // The developer's name for a grown site's board; null for a site of none of the growth's.
        private string GrowBoard(int siteId)
        {
            GrowFile g = _state.GrowSite(siteId);
            return g != null ? GrowDeveloper(g) : null;
        }

        // A grown site handed over by the programme: the schedule ends the rest on its day.
        private void GrowSiteHanded(int siteId)
        {
            GrowFile g = _state.GrowSite(siteId);
            if (g != null)
            {
                g.SiteId = 0;
            }
        }

        // ------------------------------------------------------------------ in words

        /// <summary>Who develops a grown building: a rival tycoon's holding, the builder's own arm, or the player.</summary>
        public static string GrowDeveloper(GrowFile g)
        {
            if (g.Owner == GrowOwner.Player)
            {
                return ConstructionText.T("reallife_grow_developer_you");
            }
            return !string.IsNullOrEmpty(g.Holding) ? g.Holding : ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(g.Rival, 0, BuildCatalogue.RivalCount - 1)]);
        }

        /// <summary>A grown building in words: its use, where, and who develops it.</summary>
        public string GrowName(GrowFile g)
        {
            return ConstructionText.T("reallife_grow_name", "use", ConstructionText.T(BuildCatalogue.UseKeys[(int)g.Use]), "address", LotWords(g.Lot), "developer", GrowDeveloper(g));
        }

        /// <summary>Where a grown building stands, in words.</summary>
        public static string GrowStageWords(GrowFile g)
        {
            return ConstructionText.T(GrowStageKeys[BuildCatalogue.Clamp((int)g.Stage, 0, GrowStageKeys.Length - 1)], "day", ConstructionText.Day(g.Until));
        }

        public static readonly string[] GrowStageKeys =
        {
            "reallife_grow_stage_plans", "reallife_grow_stage_planning", "reallife_grow_stage_building", "reallife_grow_stage_letting", "reallife_grow_stage_let",
            "reallife_grow_stage_dropped"
        };
    }
}
