using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for the city's own redevelopment of Uptown (docs/features/construction.md, "The city redevelops").
    /// The owner: "Whatever I do, the rivals and NPCs must be able to do." The city's developers (the eight builders' development
    /// arms, or a rival tycoon's holding while one is in play, its works done by the builder of the use) do what the player does,
    /// by the same rules and the same calls to the district: they make an offer for a building for sale (the player sees it in
    /// the Land tab and may buy the building first in BizMan before its day), then knock it down after notice and a permit and
    /// build anew on its ground, join two neighbours' ground under one bigger building, split a building over several lots into
    /// smaller ones, put storeys on a building, or make an empty one another use. Only the city's own buildings: never the
    /// player's, nothing on their land or under their projects, nothing in a Going Public session (they rest there). A move
    /// every so many days by the option, drawn from the save's seed and the day. On the day's pass and a second's look at a
    /// demolition's wrap; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<StandingView> _standing = new List<StandingView>(256);
        private readonly List<long> _ranked = new List<long>(64);
        private readonly List<int> _candidates = new List<int>(64);
        private readonly List<string> _holdings = new List<string>(3);
        private readonly bool[] _canMove = new bool[(int)CityMove.Count];

        /// <summary>True while the city's developers redevelop: the construction game and both options on, Uptown standing, outside a session.</summary>
        public bool CityRedevelopOn => Running && Settings.ConstructionRedevelop && Settings.ConstructionCityRedevelop > 0 && !GoingPublic.InSession && DistrictLives();

        // The day: each redevelopment of the city's moved on; a new one begun when its day has come.
        private void CityRedevelopDay(int day)
        {
            if (!CityRedevelopOn)
            {
                return;
            }
            for (int i = 0; i < _state.CityRedevs.Count; i++)
            {
                if (_state.CityRedevs[i].Live)
                {
                    CityStep(_state.CityRedevs[i], day, false);
                }
            }
            int rate = Settings.ConstructionCityRedevelop;
            if (_state.NextCityRedevDay <= 0)
            {
                _state.NextCityRedevDay = day + RedevelopRules.CityGap(rate, _state.Seed, day);   // the first look: they begin counting now
                return;
            }
            if (day < _state.NextCityRedevDay)
            {
                return;
            }
            _state.NextCityRedevDay = day + RedevelopRules.CityGap(rate, _state.Seed, day);
            if (LiveCityRedevs() < RedevelopRules.CityRedevsMost)
            {
                CityBegin(day, CityMove.Count);
            }
            Changed();
        }

        private int LiveCityRedevs()
        {
            int live = 0;
            for (int i = 0; i < _state.CityRedevs.Count; i++)
            {
                live += _state.CityRedevs[i].Live ? 1 : 0;
            }
            return live;
        }

        // ------------------------------------------------------------------ what they choose

        // Every one of the district's buildings standing at this load.
        private void ReadStanding()
        {
            _standing.Clear();
            try
            {
                if (!Down("StandingAll") && ConstructionBridges.StandingAll != null)
                {
                    ConstructionBridges.StandingAll(_standing);
                }
            }
            catch (Exception e)
            {
                Faulty("StandingAll", e);
            }
        }

        /// <summary>
        /// True for a building the city's developers may take: one of the city's own (no deed or unit of the player's, nothing of
        /// another player's), not a place the district keeps, with nothing of the player's on its lots (their land, a project) and
        /// no other redevelopment of the city's on them.
        /// </summary>
        public bool CityTakes(StandingView b)
        {
            if (b.Kept || string.IsNullOrEmpty(b.Lot) || !LotFree(b.Lot) || Busy(b.Street, b.Number, b.Lot))
            {
                return false;
            }
            for (int i = 0; b.Over != null && i < b.Over.Length; i++)
            {
                if (!LotFree(b.Over[i]))
                {
                    return false;
                }
            }
            return !ConstructionGame.Owns(b.Street, b.Number) && ConstructionGame.CityHeld(b.Street, b.Number);
        }

        // A lot with nothing of the player's on it and no redevelopment of the city's.
        private bool LotFree(string lot)
        {
            return _state.Lot(lot) == null && _state.OnLot(lot) == null && _state.CityRedevOn(lot) == null;
        }

        // The building standing on the lot after a building's own along its block's side, when an assembly may take both.
        private bool Neighbour(StandingView b, out StandingView next)
        {
            next = default(StandingView);
            if (!Space(b.Lot, out LotView own) || !OnSpaces(own) || !After(own, out LotView after) || own.Spaces + Math.Max(1, after.Spaces) > BuildCatalogue.SiteSpacesMost)
            {
                return false;
            }
            for (int i = 0; i < _standing.Count; i++)
            {
                StandingView s = _standing[i];
                if (string.Equals(s.Lot, after.Id, StringComparison.Ordinal))
                {
                    next = s;
                    return (s.Over == null || s.Over.Length == 0) && CityTakes(s);
                }
            }
            return false;
        }

        /// <summary>
        /// The buildings a move may take, into <paramref name="into"/> as places in the standing list, in the developers' order:
        /// those for sale first, then those with no business trading, then the rest (RedevelopRules.Rank). Their count.
        /// </summary>
        public int CityCandidates(CityMove move, List<int> into)
        {
            into.Clear();
            _ranked.Clear();
            int limit = UptownStoreys();
            for (int i = 0; i < _standing.Count && i < 0xFFFF; i++)
            {
                StandingView b = _standing[i];
                if (!CityTakes(b))
                {
                    continue;
                }
                bool over = b.Over != null && b.Over.Length > 0;
                bool trading = ConstructionGame.Trading(b.Street, b.Number);
                switch (move)
                {
                    case CityMove.Redevelop when over:
                    case CityMove.Assemble when over || !Neighbour(b, out _):
                    case CityMove.Split when !over:
                    case CityMove.Convert when trading:
                        continue;
                    case CityMove.Extend when RedevelopRules.ExtensionRoom(b.Storeys, limit) <= 0:
                        continue;
                }
                if (move == CityMove.Convert)
                {
                    ConversionUses(b.Size, b.Version, b.Type, _convertTo);
                    if (_convertTo.Count == 0)
                    {
                        continue;
                    }
                }
                long rank = RedevelopRules.Rank(ConstructionGame.ForSale(b.Street, b.Number, out _), trading, _state.Seed, b.Lot);
                _ranked.Add((rank << 16) | (long)i);   // the place rides under its rank, so one sort of plain numbers orders them
            }
            _ranked.Sort();
            for (int i = 0; i < _ranked.Count; i++)
            {
                into.Add((int)(_ranked[i] & 0xFFFF));
            }
            _ranked.Clear();
            return into.Count;
        }

        /// <summary>
        /// A new move of the city's developers: what they do drawn from the save's seed and the day among the moves the district
        /// holds a building for (or the move asked, for a test), and the building among the first few in their order. Begun with
        /// an offer when the building is for sale, else with its permission asked and its notice given. Null when nothing fits.
        /// </summary>
        internal RedevFile CityBegin(int day, CityMove asked)
        {
            ReadStanding();
            CityMove move = asked;
            if (move == CityMove.Count)
            {
                for (int m = 0; m < (int)CityMove.Count; m++)
                {
                    _canMove[m] = CityCandidates((CityMove)m, _candidates) > 0;
                }
                move = RedevelopRules.MoveOf(_state.Seed, day, _canMove);
            }
            int count = move == CityMove.Count ? 0 : CityCandidates(move, _candidates);
            if (count == 0)
            {
                Logger?.Info("Construction: the city's developers find nothing of Uptown's to take on day " + day + (asked != CityMove.Count ? " for " + asked : string.Empty) + ".");
                return null;
            }
            StandingView b = _standing[_candidates[RedevelopRules.PickOf(_state.Seed, day, (int)move, Math.Min(count, RedevelopRules.CityShortlist))]];
            int use = BuildCatalogue.UseOf(b.Type);
            var r = new RedevFile
            {
                Id = _state.NewId(), Move = move, Lot = b.Lot, Street = b.Street, Number = b.Number, Day = day, Area = Math.Max(1, b.Sqm),
                Use = use >= 0 ? (BuildUse)use : BuildUse.Offices
            };
            if (move == CityMove.Assemble && Neighbour(b, out StandingView next))
            {
                r.Lots = new[] { next.Lot };
                r.Area += Math.Max(1, next.Sqm);
            }
            else if (b.Over != null && b.Over.Length > 0)
            {
                r.Lots = (string[])b.Over.Clone();
            }
            if (move == CityMove.Extend)
            {
                int room = RedevelopRules.ExtensionRoom(b.Storeys, UptownStoreys());
                r.Storeys = Math.Max(1, b.Storeys) + 1 + RedevelopRules.PickOf(_state.Seed, day, 0x51, Math.Min(3, room));   // one to three storeys, within the code
            }
            else if (move == CityMove.Convert)
            {
                ConversionUses(b.Size, b.Version, b.Type, _convertTo);
                r.Use = _convertTo[RedevelopRules.PickOf(_state.Seed, day, 0xC0, _convertTo.Count)];
            }
            r.Rival = DevelopmentRules.Rival(b.Lot, r.Use);
            Holdings(_holdings);
            if (_holdings.Count > 0 && RedevelopRules.ByHolding(_state.Seed, day))
            {
                r.Holding = _holdings[RedevelopRules.PickOf(_state.Seed, day, 0x40, _holdings.Count)];
            }
            _state.CityRedevs.Add(r);
            if (_state.CityRedevs.Count > RedevelopRules.CityRedevsKept)
            {
                TidyCityRedevs();
            }
            if (ConstructionGame.ForSale(b.Street, b.Number, out long price))
            {
                // For sale: their offer goes in, and completes on its day unless the player buys the building first.
                r.Stage = CityStage.Offer;
                r.Price = price;
                r.Until = day + RedevelopRules.OfferDays;
                Tell(NotificationType.Info, "reallife_bld_notice_city_offer", OfferName(r), null, TabLand, 0, RedevelopRules.OfferDays);
                Paper("reallife_bld_paper_city_offer", OfferName(r), RedevelopRules.OfferDays);
            }
            else
            {
                CityApply(r, day);
            }
            Logger?.Info("Construction: the city's developers (" + DeveloperName(r) + ", builder " + r.Rival + ") take " + b.Street + " " + b.Number + " (lot " + b.Lot
                         + (r.Lots != null ? " and " + r.Lots.Length + " more" : string.Empty) + ") for " + move + ", from " + r.Stage + (r.Stage == CityStage.Offer ? " at " + r.Price : string.Empty)
                         + ", until day " + r.Until + ".");
            Changed();
            return r;
        }

        // The oldest finished ones make room: those under way are always kept.
        private void TidyCityRedevs()
        {
            for (int i = 0; i < _state.CityRedevs.Count && _state.CityRedevs.Count > RedevelopRules.CityRedevsKept; i++)
            {
                if (!_state.CityRedevs[i].Live)
                {
                    _state.CityRedevs.RemoveAt(i);
                    i--;
                }
            }
        }

        // ------------------------------------------------------------------ its stages

        // The permission asked of City Hall (a permit for knocking down, planning permission for the rest), and the notice to the
        // building's tenants and business given with it where the works need it empty (all but an extension's): the player's rules.
        private void CityApply(RedevFile r, int day)
        {
            bool down = Demolishes(r.Move);
            int decide = PlanningRules.AfterWorkingDays(day, down ? RedevelopRules.PermitDays(r.Area) : PlanningRules.WorkingDays(r.Area));
            r.Stage = CityStage.Notice;
            r.Until = r.Move == CityMove.Extend ? decide : Math.Max(decide, RedevelopRules.NoticeEnds(day));
            Paper("reallife_bld_paper_city_applied", CityName(r), 0);
        }

        private static bool Demolishes(CityMove move)
        {
            return move == CityMove.Redevelop || move == CityMove.Assemble || move == CityMove.Split;
        }

        /// <summary>
        /// One redevelopment moved on by a day: its offer completed (or given up when the player bought the building first), its
        /// permission decided and its works begun, its works handed over to the district, its new building finished. A test
        /// rushes it: the offer completes, the permission is granted and each stage ends at once.
        /// </summary>
        internal void CityStep(RedevFile r, int day, bool rush)
        {
            if (!rush && day < r.Until)
            {
                if (r.Stage == CityStage.Building && string.IsNullOrEmpty(r.Design))
                {
                    CityBuildStart(r);   // the new building's design while the city's designs are looked through
                }
                return;
            }
            if (r.Stage != CityStage.Building && !StillTheCitys(r))
            {
                bool outbid = r.Stage == CityStage.Offer && ConstructionGame.Owns(r.Street, r.Number);
                CityDrop(r, day, outbid ? "the player bought the building first" : "the building is no longer the city's");
                if (outbid)
                {
                    Tell(NotificationType.Success, "reallife_bld_notice_city_outbid", OfferName(r), null, TabLand, 0);
                    Paper("reallife_bld_paper_city_outbid", OfferName(r), 0);
                }
                return;
            }
            switch (r.Stage)
            {
                case CityStage.Offer:
                    ConstructionGame.TakeOffMarket(r.Street, r.Number);   // theirs now: off the game's listing, as BizMan takes a sold one off
                    Paper("reallife_bld_paper_city_bought", OfferName(r), 0);
                    CityApply(r, day);
                    break;
                case CityStage.Notice:
                    CityDecide(r, day, rush);
                    break;
                case CityStage.Works:
                    CityWorksDone(r, day);
                    break;
                case CityStage.Building:
                    CityBuilt(r, day);
                    break;
            }
            Changed();
        }

        // Every building it takes is still the city's own: nothing bought by the player in the meantime. One its own works knocked
        // down already (an assembly's first, its second refused that day) counts as still theirs.
        private bool StillTheCitys(RedevFile r)
        {
            if (!StandingOnLot(r.Lot, out _))
            {
                return r.Stage == CityStage.Works && Demolishes(r.Move);
            }
            if (ConstructionGame.Owns(r.Street, r.Number) || !ConstructionGame.CityHeld(r.Street, r.Number))
            {
                return false;
            }
            if (r.Move == CityMove.Assemble && r.Lots != null && r.Lots.Length > 0 && StandingOnLot(r.Lots[0], out StandingView n))
            {
                return !ConstructionGame.Owns(n.Street, n.Number) && ConstructionGame.CityHeld(n.Street, n.Number);
            }
            return true;
        }

        // City Hall's decision on its day, by the player's own rules: the neighbours' objections, the council's odds on the use,
        // the developer's standing. Granted: the building emptied where its works need it, and its works begun.
        private void CityDecide(RedevFile r, int day, bool rush)
        {
            Standing(r.Street, r.Number, out StandingView b);
            if (r.Move == CityMove.Extend && r.Storeys <= b.Storeys)
            {
                CityDrop(r, day, "it stands as tall already");
                return;
            }
            bool zoned = r.Move != CityMove.Convert || (Space(r.Lot, out LotView lot) && LandRules.Zoned(LandRules.ZonesOf(lot.Class), r.Use));
            int storeys = r.Move == CityMove.Extend ? r.Storeys : 4, norm = r.Move == CityMove.Extend ? Math.Max(2, b.Storeys) : 4;
            int reputation = _state.Rivals[r.Rival].Reputation;
            int objections = PlanningRules.Objections(_state.Seed, r.Id * 13 + 1, storeys, norm, zoned, 0, reputation, RedevelopRules.CityPlansQuality);
            bool granted = rush || PlanningRules.Decide(_state.Seed, r.Id, 1, zoned, 0, RedevelopRules.CityPlansQuality, reputation, objections, Permits(), out _);
            if (!granted)
            {
                CityDrop(r, day, "City Hall refused it (" + objections + " objection(s))");
                Paper("reallife_bld_paper_city_refused", CityName(r), objections);
                return;
            }
            if (r.Move != CityMove.Extend)
            {
                ConstructionGame.Evict(r.Street, r.Number);   // the notice has run: a business of the city's closes by the game's own call
                if (r.Move == CityMove.Assemble && r.Lots != null && r.Lots.Length > 0 && StandingOnLot(r.Lots[0], out StandingView n))
                {
                    ConstructionGame.Evict(n.Street, n.Number);
                }
            }
            int kind = Demolishes(r.Move) ? RedevelopRules.DemolitionWorks : r.Move == CityMove.Extend ? 4 : RedevelopRules.ConversionWorks;
            int hours = Demolishes(r.Move) ? RedevelopRules.DemolitionHours(r.Area) : r.Move == CityMove.Extend
                ? PlansRules.Hours(r.Use, RedevelopRules.AddedArea(b.SizeSqm, r.Storeys, b.Sqm), r.Storeys, ProjectKind.Extension) : RedevelopRules.ConversionHours(r.Area);
            r.SiteId = SiteCount() < BuildCatalogue.SitesMost ? StartWorksSite(r.Street, r.Number, kind, false, r.Rival, hours, out string refused) : 0;
            r.Stage = CityStage.Works;
            r.Until = rush ? day : PlanningRules.AfterWorkingDays(day, TenderRules.StandardDays(hours));
            Logger?.Info("Construction: the city's " + r.Move + " at " + r.Street + " " + r.Number + " has its permission (" + objections + " objection(s)); works of " + hours
                         + " hour(s) until day " + r.Until + (r.SiteId > 0 ? ", site " + r.SiteId : ", no site shown") + ".");
        }

        // The works' last day: the district takes what they did, by the player's own calls; a demolition's ground then gets its new building.
        private void CityWorksDone(RedevFile r, int day)
        {
            string why = null;
            if (Demolishes(r.Move))
            {
                why = StandingOnLot(r.Lot, out _) ? CityDemolish(r.Lot) : null;   // down already on an earlier day: its neighbour's turn
                if (why == null && r.Move == CityMove.Assemble && r.Lots != null && r.Lots.Length > 0 && StandingOnLot(r.Lots[0], out _))
                {
                    why = CityDemolish(r.Lots[0]);
                }
            }
            else
            {
                try
                {
                    why = r.Move == CityMove.Extend
                        ? Down("Extend") || ConstructionBridges.Extend == null ? "the district is not there to take it" : ConstructionBridges.Extend(r.Lot, r.Storeys)
                        : Down("Convert") || ConstructionBridges.Convert == null ? "the district is not there to take it" : ConstructionBridges.Convert(r.Lot, BuildCatalogue.UseTypes[(int)r.Use]);
                }
                catch (Exception e)
                {
                    Faulty(r.Move == CityMove.Extend ? "Extend" : "Convert", e);
                    why = "a fault";
                }
            }
            if (why != null)
            {
                if (day > r.Until + RedevelopRules.CityPatienceDays)
                {
                    CityDrop(r, day, "the district would not take it (" + why + ")");
                    return;
                }
                Logger?.Info("Construction: the city's " + r.Move + " at " + r.Street + " " + r.Number + " is not the district's yet (" + why + "); asked again each day.");
                return;
            }
            if (r.SiteId > 0)
            {
                FinishSite(r.SiteId);   // the wrap and the hoarding come down
                r.SiteId = 0;
            }
            if (r.Move == CityMove.Convert)
            {
                ConstructionGame.LetAgain(r.Street, r.Number);   // an empty unit of the new use, to let to the city's businesses as the game lets one
            }
            ShowLooks(r, false);
            if (!Demolishes(r.Move))
            {
                r.Stage = CityStage.Done;
                r.Until = day;
                Paper(r.Move == CityMove.Extend ? "reallife_bld_paper_city_extended" : "reallife_bld_paper_city_converted", CityName(r), 0);
                Logger?.Info("Construction: the city's " + r.Move + " at " + r.Street + " " + r.Number + " is done, now and from every load.");
                return;
            }
            Paper("reallife_bld_paper_city_demolished", CityName(r), 0);
            r.Stage = CityStage.Building;
            r.Until = day + RedevelopRules.NewBuildDays(r.Area);
            CityBuildStart(r);
        }

        // A building of the city's knocked down in the district: null when done, else why not.
        private static string CityDemolish(string lot)
        {
            try
            {
                return Down("Demolish") || ConstructionBridges.Demolish == null ? "the district is not there to take it" : ConstructionBridges.Demolish(lot);
            }
            catch (Exception e)
            {
                Faulty("Demolish", e);
                return "a fault";
            }
        }

        // The ground the new building goes up on, as one lot: an assembly's two lots joined, a building's own lot otherwise (a
        // split's first; its others each get one of their own).
        private bool CityGround(RedevFile r, out LotView site)
        {
            if (!Space(r.Lot, out site))
            {
                return false;
            }
            if (r.Move != CityMove.Assemble || r.Lots == null || r.Lots.Length == 0)
            {
                return true;
            }
            _joining.Clear();
            _joining.Add(site);
            if (!Space(r.Lots[0], out LotView next))
            {
                return false;
            }
            _joining.Add(next);
            site = Joined(_joining);
            _joining.Clear();
            return true;
        }

        // The new building's design (the developers' own pick of those of the city's that fill the ground) and its site going up.
        private void CityBuildStart(RedevFile r)
        {
            if (!string.IsNullOrEmpty(r.Design) || !CityGround(r, out LotView site))
            {
                return;
            }
            r.Use = DevelopmentRules.Use(site.Id, site.Class);
            if (!CityDesign(site, r.Use, out string design))
            {
                return;   // the city's designs are being looked through: the next day's pass
            }
            r.Design = design;
            if (design == NoDesign || r.SiteId > 0 || SiteCount() >= BuildCatalogue.SitesMost)
            {
                return;
            }
            int storeys = DesignOf(site, r.Use, design, out DesignChoice d) ? LandRules.FrameStoreys(site.Width, PlansRules.Storeys(d.Height)) : 4;
            int days = Math.Max(1, r.Until - ConstructionGame.Day);
            r.SiteId = StartPlotSite(new Vector3(site.X, site.Y, site.Z), site.Yaw, site.Width, site.Depth, storeys, r.Rival, Math.Max(10, days * 50 / 7), out _);
        }

        // The design a developer gives cleared ground: one of those of the city's that fill it, picked by its lot; false while
        // the designs are being looked through, NoDesign when none fits.
        private bool CityDesign(LotView site, BuildUse use, out string design)
        {
            design = null;
            SiteGround(site, out float w, out float d);
            if (!ReadDesigns(w, d, BuildCatalogue.UseTypes[(int)use], DesignsAsked, _choices))
            {
                return false;
            }
            KeepFitting(site, _choices);
            int pick = DevelopmentRules.DesignPick(site.Id, _choices.Count);
            design = pick >= 0 ? _choices[pick].Name : NoDesign;
            return true;
        }

        // The new building's last day: the district's (it stands now, or when nobody sees its ground), the city's (let to its businesses by the game's own
        // generator), its builder's record, the stock market's builder news, the building of the year.
        private void CityBuilt(RedevFile r, int day)
        {
            CityBuildStart(r);
            if (string.IsNullOrEmpty(r.Design))
            {
                return;   // the designs are being looked through: the next day's pass
            }
            if (r.Design == NoDesign || !CityGround(r, out LotView site))
            {
                CityDrop(r, day, "nothing of the city's designs fits its ground");
                return;
            }
            if (!DesignOf(site, r.Use, r.Design, out DesignChoice design))
            {
                return;
            }
            string type = BuildCatalogue.UseTypes[(int)r.Use], why;
            bool handed;
            if (r.Move == CityMove.Assemble && r.Lots != null && r.Lots.Length > 0)
            {
                _over.Clear();
                _over.Add(r.Lots[0]);
                handed = FinishOverLot(r.Lot, _over, design.Name, design.Size, design.Version, type, 0, false, 0.0, out why);
                _over.Clear();
            }
            else
            {
                handed = FinishLot(r.Lot, design.Name, design.Size, design.Version, type, 0, false, 0.0, out why);
            }
            if (!handed)
            {
                if (day > r.Until + RedevelopRules.CityPatienceDays)
                {
                    CityDrop(r, day, "the district would not take its new building (" + why + ")");
                    return;
                }
                Logger?.Info("Construction: the city's new building on lot " + r.Lot + " is not the district's yet (" + why + ").");
                return;
            }
            int extra = r.Move == CityMove.Split ? CitySplitRest(r) : 0;
            r.Stage = CityStage.Done;
            r.Until = day;
            _state.Rivals[r.Rival].Built += 1 + extra;
            RivalNews(r.Rival, RivalDeveloped, 1);   // the district grew: its builder, and a little every listed builder
            NoteFinished(AwardEntry.OfDevelopers, null, r.Lot, site.Street, site.Number, r.Use, r.Rival, Math.Min(95, _state.Rivals[r.Rival].Reputation + 10), 0, day);
            if (r.SiteId > 0)
            {
                FinishSite(r.SiteId);   // the frame comes down
                r.SiteId = 0;
            }
            Paper("reallife_bld_paper_city_built", CityName(r), 1 + extra);
            Logger?.Info("Construction: the city's " + r.Move + " on lot " + r.Lot + " is finished: " + r.Use + " in " + design.Name + (extra > 0 ? " and " + extra + " more" : string.Empty)
                         + ", the city's.");
        }

        // A split's other lots, each a smaller building of its own; how many the district took.
        private int CitySplitRest(RedevFile r)
        {
            int done = 0;
            for (int i = 0; r.Lots != null && i < r.Lots.Length; i++)
            {
                if (!Space(r.Lots[i], out LotView lot))
                {
                    continue;
                }
                BuildUse use = DevelopmentRules.Use(lot.Id, lot.Class);
                if (CityDesign(lot, use, out string name) && name != NoDesign && DesignOf(lot, use, name, out DesignChoice d)
                    && FinishLot(lot.Id, d.Name, d.Size, d.Version, BuildCatalogue.UseTypes[(int)use], 0, false, 0.0, out _))
                {
                    done++;
                }
            }
            return done;
        }

        // Given up: its site taken down, its buildings' looks shown again.
        private void CityDrop(RedevFile r, int day, string why)
        {
            if (r.SiteId > 0)
            {
                DropSite(r.SiteId);
                r.SiteId = 0;
            }
            ShowLooks(r, true);
            r.Stage = CityStage.Dropped;
            r.Until = day;
            Logger?.Info("Construction: the city's " + r.Move + " at " + r.Street + " " + r.Number + " is given up: " + why + ".");
        }

        // ------------------------------------------------------------------ the second's look, the site's board

        // Once a second: a building of the city's coming down behind its wrap has its look hidden from the works' fourth stage.
        private void CitySecond()
        {
            for (int i = 0; i < _state.CityRedevs.Count; i++)
            {
                RedevFile r = _state.CityRedevs[i];
                if (r.Stage != CityStage.Works || !Demolishes(r.Move) || r.SiteId <= 0 || _downLooks.Contains(r.Lot))
                {
                    continue;
                }
                SiteFacts site = SiteOf(r.SiteId);
                if (site.Known && site.Hours > 0f && site.Done / site.Hours >= RedevelopRules.LookDownShare && HideLook(r.Lot, true))
                {
                    _downLooks.Add(r.Lot);
                    if (r.Move == CityMove.Assemble && r.Lots != null && r.Lots.Length > 0 && HideLook(r.Lots[0], true))
                    {
                        _downLooks.Add(r.Lots[0]);
                    }
                }
            }
        }

        // Its buildings' looks shown again where they were hidden (given up), or forgotten (knocked down).
        private void ShowLooks(RedevFile r, bool show)
        {
            if (_downLooks.Remove(r.Lot) && show)
            {
                HideLook(r.Lot, false);
            }
            for (int i = 0; r.Lots != null && i < r.Lots.Length; i++)
            {
                if (_downLooks.Remove(r.Lots[i]) && show)
                {
                    HideLook(r.Lots[i], false);
                }
            }
        }

        // The pace of a site of the city's redevelopment: its schedule hands it over on its day, never the programme's crews.
        private bool CityPace(int siteId, double done, double needed, float hours, out float pace)
        {
            pace = 0f;
            if (_state.CityRedevSite(siteId) == null)
            {
                return false;
            }
            pace = Running ? (float)Math.Max(0.0, Math.Min(hours, needed * 0.995 - done)) : 0f;
            return true;
        }

        // The developer's name for a site's board; null for a site of none of theirs.
        private string CityBoard(int siteId)
        {
            RedevFile r = _state.CityRedevSite(siteId);
            return r != null ? DeveloperName(r) : null;
        }

        // A site of the city's redevelopment handed over by the programme: the schedule ends the rest on its day.
        private void CityHanded(int siteId)
        {
            RedevFile r = _state.CityRedevSite(siteId);
            if (r != null)
            {
                r.SiteId = 0;
            }
        }

        // ------------------------------------------------------------------ in words

        /// <summary>Who develops it: a rival tycoon's holding, or the builder's own development arm.</summary>
        public static string DeveloperName(RedevFile r)
        {
            return !string.IsNullOrEmpty(r.Holding) ? r.Holding : ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(r.Rival, 0, BuildCatalogue.RivalCount - 1)]);
        }

        /// <summary>A redevelopment of the city's in words: its move, where, and who makes it.</summary>
        public static string CityName(RedevFile r)
        {
            return ConstructionText.T("reallife_bld_city_name", "move", ConstructionText.T(CityMoveKeys[(int)r.Move]), "address", ConstructionGame.AddressText(r.Street, r.Number),
                "developer", DeveloperName(r));
        }

        /// <summary>A developer's offer for a building for sale, in words.</summary>
        public static string OfferName(RedevFile r)
        {
            return ConstructionText.T("reallife_bld_city_offer_name", "developer", DeveloperName(r), "address", ConstructionGame.AddressText(r.Street, r.Number));
        }

        /// <summary>Where a redevelopment of the city's stands, in words.</summary>
        public static string CityStageWords(RedevFile r)
        {
            return ConstructionText.T(CityStageKeys[(int)r.Stage], "day", ConstructionText.Day(r.Until));
        }

        public static readonly string[] CityMoveKeys =
        {
            "reallife_bld_city_move_redevelop", "reallife_bld_city_move_assemble", "reallife_bld_city_move_split", "reallife_bld_city_move_extend", "reallife_bld_city_move_convert"
        };

        public static readonly string[] CityStageKeys =
        {
            "reallife_bld_city_stage_offer", "reallife_bld_city_stage_notice", "reallife_bld_city_stage_works", "reallife_bld_city_stage_building", "reallife_bld_city_stage_done",
            "reallife_bld_city_stage_dropped"
        };

        /// <summary>The developers' offers for buildings for sale still to complete, into the caller's list (cleared).</summary>
        public void CityOffers(List<RedevFile> into)
        {
            into.Clear();
            for (int i = 0; _state != null && i < _state.CityRedevs.Count; i++)
            {
                if (_state.CityRedevs[i].Stage == CityStage.Offer)
                {
                    into.Add(_state.CityRedevs[i]);
                }
            }
        }

        // The rival tycoons' holdings in play.
        private static void Holdings(List<string> into)
        {
            into.Clear();
            try
            {
                if (!Down("Holdings") && ConstructionBridges.Holdings != null)
                {
                    ConstructionBridges.Holdings(into);
                }
            }
            catch (Exception e)
            {
                Faulty("Holdings", e);
            }
        }
    }
}
