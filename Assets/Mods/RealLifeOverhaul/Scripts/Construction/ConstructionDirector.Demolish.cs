using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for demolitions (docs/features/construction.md, "Demolition"): an Uptown building of the player's
    /// knocked down to its ground. A project of its own: notice to its tenants and to a business of the city's in it, given with
    /// the demolition permit's application at City Hall (the planning application's path, a permit's fee and days, the
    /// neighbours' objections to the dust and noise); at the notice's end the business closes by the game's own call and the
    /// tenants are bought out; with the permit and an empty building the works start, by the company's crews as the programme's
    /// demolition works or by the city's best demolition contractor at its quote; the building comes down behind its wrap (its
    /// look hidden from the works' fourth stage), and at the handover it is knocked down in the district (its records let go for
    /// good) and its ground is the player's land, a site of all the lots it stood on, to build on again. Never in a Going Public
    /// session. On a press, the day's pass and a second's look at the works; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly HashSet<string> _downLooks = new HashSet<string>(StringComparer.Ordinal);   // lots whose building's look is hidden at this load

        /// <summary>True while redevelopment in Uptown may run: the construction game, its option, and outside a Going Public session.</summary>
        public bool RedevelopOn => Running && Settings.ConstructionRedevelop && !GoingPublic.InSession;

        /// <summary>The district's building standing at an address at this load, as redevelopment reads it; false for none.</summary>
        public static bool Standing(string street, int number, out StandingView building)
        {
            building = default(StandingView);
            try
            {
                return !Down("StandingAt") && ConstructionBridges.StandingAt != null && ConstructionBridges.StandingAt(street, number, out building);
            }
            catch (Exception e)
            {
                Faulty("StandingAt", e);
                return false;
            }
        }

        // The district's building standing on a lot (its own) at this load; false for none.
        private static bool StandingOnLot(string lot, out StandingView building)
        {
            building = default(StandingView);
            try
            {
                return !Down("StandingOn") && ConstructionBridges.StandingOn != null && ConstructionBridges.StandingOn(lot, out building);
            }
            catch (Exception e)
            {
                Faulty("StandingOn", e);
                return false;
            }
        }

        /// <summary>Why the player's building at an address cannot be planned for demolition now, as a text key; null when it can.</summary>
        public string DemolishBlock(string street, int number)
        {
            return RedevelopRefusal(street, number, out _) ?? ConstructionGame.Occupied(street, number);   // empty of their own things first
        }

        /// <summary>
        /// Why a building at an address cannot be redeveloped by the player at all now, as a text key; null when it may: one of
        /// Uptown's standing at this load, not a place the district keeps, the player's own (the game's deed), with no project of
        /// theirs on it, the option on and outside a Going Public session. Every redevelopment of a building asks this first.
        /// </summary>
        public string RedevelopRefusal(string street, int number, out StandingView b)
        {
            b = default(StandingView);
            if (!Running || !Settings.ConstructionRedevelop)
            {
                return "reallife_bld_answer_redevelop_off";
            }
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_redevelop";
            }
            if (!Standing(street, number, out b))
            {
                return "reallife_bld_answer_not_district";
            }
            if (b.Kept)
            {
                return "reallife_bld_answer_kept_place";
            }
            if (!ConstructionGame.Owns(street, number))
            {
                return "reallife_bld_answer_not_yours";
            }
            return Busy(street, number, b.Lot) ? "reallife_bld_answer_building_busy" : null;
        }

        // A live project of the player's at a building or on its lot.
        private bool Busy(string street, int number, string lot)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Live && ((p.Number == number && string.Equals(p.Street, street, StringComparison.Ordinal)) || (!string.IsNullOrEmpty(lot) && p.Lot == lot)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A demolition of the player's building in Uptown, its permit and notice still to ask for. Its id, or 0 with why not.</summary>
        public int NewDemolition(string street, int number, out string why)
        {
            why = DemolishBlock(street, number);
            if (why != null || !Room(out why))
            {
                return 0;
            }
            Standing(street, number, out StandingView b);
            ConstructionGame.Building(street, number, out BuildingView view);
            int use = BuildCatalogue.UseOf(b.Type);
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Demolition, Stage = ProjectStage.Design, Created = ConstructionGame.Day, Street = street, Number = number, Lot = b.Lot,
                District = view.District ?? string.Empty, Use = use >= 0 ? (BuildUse)use : BuildUse.Shops, Size = b.Size, Version = b.Version, Area = Math.Max(1, b.Sqm),
                Storeys = Math.Max(1, b.Storeys), Height = b.Height, Design = b.Design ?? string.Empty, Hours = RedevelopRules.DemolitionHours(b.Sqm),
                CivicKind = RedevelopRules.DemolitionWorks, Source = TenderSource.Client, Quality = 70, PlansDone = true, Engineered = true
            };
            p.Estimate = DemolitionEstimate(p);
            _state.Projects.Add(p);
            Logger?.Info("Construction: project " + p.Id + " made to knock down " + street + " " + number + " (lot " + b.Lot + (b.Over != null ? " and " + b.Over.Length + " more" : string.Empty)
                         + ", " + b.Sqm + " m2): " + p.Hours + " working hours, estimated at " + p.Estimate + ".");
            Changed();
            return p.Id;
        }

        // What knocking a building down costs its builder: the programme's demolition works for its hours, and its rubble hauled away.
        private long DemolitionEstimate(ProjectFile p)
        {
            Exchange(_exchange);
            return PlansRules.EstimateWorks(RedevelopRules.DemolitionWorks, p.Hours, ConstructionGame.Prices, _exchange).Total + RedevelopRules.RubbleCost(p.Area, ConstructionGame.Prices);
        }

        /// <summary>
        /// The city's best contractor's quote today for works that need their building empty: a demolition's (the demolition
        /// house's trade), or a change of use's (the renovators').
        /// </summary>
        public RefitQuote VacantQuote(ProjectFile p)
        {
            return p.Kind == ProjectKind.Conversion ? RedevelopRules.ConversionQuote(_state.Seed, ConstructionGame.Day, p.Area, ConstructionGame.Prices)
                : RedevelopRules.Quote(_state.Seed, ConstructionGame.Day, p.Area, ConstructionGame.Prices);
        }

        /// <summary>True for a project whose works need its building empty: notice to its tenants and business with its application, the works after it has run.</summary>
        public static bool NeedsNotice(ProjectFile p)
        {
            return p.Kind == ProjectKind.Demolition || p.Kind == ProjectKind.Conversion;
        }

        /// <summary>True for the player's land where a building was knocked down and nothing has been finished since: ground cleared for a new building.</summary>
        public bool ClearedGround(string lot)
        {
            return _state != null && _state.Lot(lot) != null && AskCleared(lot);
        }

        /// <summary>True when a project's building is empty for its works: its notice has run and its tenants and business are gone.</summary>
        public static bool Emptied(ProjectFile p)
        {
            return p.BuyOut != 0L;
        }

        /// <summary>
        /// A demolition or a change of use with its permission and an empty building done by the city's best contractor for it at
        /// today's quote, paid now: its site set out at once, at the city's own pace. Returns a text key.
        /// </summary>
        public string ContractorVacant(ProjectFile p, bool free)
        {
            if (p == null || !NeedsNotice(p) || p.Stage != ProjectStage.Ready)
            {
                return "reallife_bld_answer_no_permission";
            }
            if (!Emptied(p))
            {
                return "reallife_bld_answer_notice_running";
            }
            if (SiteCount() >= BuildCatalogue.SitesMost)
            {
                return "reallife_bld_answer_city_full";
            }
            RefitQuote q = VacantQuote(p);
            string name = ProjectName(p);
            bool down = p.Kind == ProjectKind.Demolition;
            if (!free && !ConstructionGame.Take(q.Price, down ? "reallife_transaction_bld_demolition" : "reallife_transaction_bld_conversion", name, true))
            {
                return "reallife_bld_answer_no_money";
            }
            int site = StartWorksSite(p.Street, p.Number, p.CivicKind, true, q.Rival, q.Hours, out string refused);
            if (site <= 0)
            {
                if (!free)
                {
                    // Nothing started: the money comes back.
                    ConstructionGame.Give(q.Price, down ? "reallife_transaction_bld_demolition_back" : "reallife_transaction_bld_conversion_back", name);
                }
                Logger?.Info("Construction: the contractor's works on " + name + " could not start: " + refused + ".");
                return "reallife_bld_answer_site_refused";
            }
            int day = ConstructionGame.Day;
            p.Contractor = q.Rival;
            p.ContractorPrice = free ? 0L : q.Price;
            p.Hours = q.Hours;
            p.SiteId = site;
            p.Stage = ProjectStage.Building;
            p.StartDay = day;
            p.DueDay = PlanningRules.AfterWorkingDays(day, q.Days);
            Spend(p, free ? 0L : q.Price);
            Logger?.Info("Construction: project " + p.Id + ": " + name + " by contractor " + q.Rival + " for " + q.Price + " in about " + q.Days + " working day(s), site " + site + ".");
            Changed();
            return down ? "reallife_bld_answer_demolition_ordered" : "reallife_bld_answer_conversion_ordered";
        }

        // The notice given with the permit's application: the building's tenants and business leave when it runs out.
        private void GiveNotice(ProjectFile p, int day)
        {
            if (p.NoticeUntil > 0 || Emptied(p))
            {
                return;
            }
            p.NoticeUntil = RedevelopRules.NoticeEnds(day);
            Tell(NotificationType.Info, "reallife_bld_notice_notice_given", ProjectName(p), null, TabProjects, p.Id, RedevelopRules.NoticeDays);
            Logger?.Info("Construction: project " + p.Id + " gave its building's tenants and business notice until day " + p.NoticeUntil + ".");
        }

        // ------------------------------------------------------------------ the day and the second

        // The day: a notice that has run settled (the business closed, the tenants bought out), the building kept empty while its
        // works go on, and a handover the district could not take yet tried again.
        private void RedevelopDay(int day)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (NeedsNotice(p) && p.Live && p.NoticeUntil > 0 && day > p.NoticeUntil && !Emptied(p))
                {
                    SettleNotice(p);
                }
                else if (NeedsNotice(p) && p.Live && Emptied(p))
                {
                    ConstructionGame.HoldEmpty(p.Street, p.Number);
                }
                if (p.Stage != ProjectStage.Finished || p.InDistrict)
                {
                    continue;
                }
                if (p.Kind == ProjectKind.Demolition)
                {
                    Demolished(p);
                }
                else if (p.Kind == ProjectKind.Extension || p.Kind == ProjectKind.Conversion)
                {
                    Redeveloped(p);   // ConstructionDirector.Extend.cs
                }
            }
        }

        // The notice has run: the business closes, the tenants are bought out at the rule's days, and the building is empty.
        private void SettleNotice(ProjectFile p)
        {
            ConstructionGame.Tenants(p.Street, p.Number, out double tenants, out double rent, out bool business);
            long cost = RedevelopRules.BuyOut(tenants, rent, business);
            ConstructionGame.Evict(p.Street, p.Number);
            if (cost > 0L)
            {
                ConstructionGame.Force(cost, "reallife_transaction_bld_buyout", ProjectName(p), true);
                Spend(p, cost);
            }
            p.BuyOut = cost > 0L ? cost : -1L;
            Tell(NotificationType.Info, "reallife_bld_notice_emptied", ProjectName(p), null, TabProjects, p.Id);
            Logger?.Info("Construction: project " + p.Id + "'s notice ran out: " + (business ? "the business there closed, " : string.Empty) + "its tenants bought out for " + cost
                         + " (they paid " + tenants.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " a day); the building is empty.");
            Changed();
        }

        // Once a second: a building coming down behind its wrap has its look hidden from the works' fourth stage, again after a load.
        private void RedevelopSecond()
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Kind != ProjectKind.Demolition || p.Stage != ProjectStage.Building || p.SiteId <= 0 || _downLooks.Contains(p.Lot))
                {
                    continue;
                }
                SiteFacts site = SiteOf(p.SiteId);
                if (site.Known && site.Hours > 0f && site.Done / site.Hours >= RedevelopRules.LookDownShare && HideLook(p.Lot, true))
                {
                    _downLooks.Add(p.Lot);
                    Logger?.Info("Construction: project " + p.Id + "'s building comes down behind its wrap: its look hidden.");
                }
            }
            CitySecond();   // the city's own demolitions alike
        }

        private static bool HideLook(string lot, bool hide)
        {
            try
            {
                return !Down("HideLook") && ConstructionBridges.HideLook != null && ConstructionBridges.HideLook(lot, hide);
            }
            catch (Exception e)
            {
                Faulty("HideLook", e);
                return false;
            }
        }

        // A demolition given up: its building's look shown again if it was coming down.
        private void DemolitionGivenUp(ProjectFile p)
        {
            if (_downLooks.Remove(p.Lot))
            {
                HideLook(p.Lot, false);
            }
        }

        // ------------------------------------------------------------------ the handover

        /// <summary>
        /// A demolition handed over: the building knocked down in the district (its records let go for good, its lots cleared)
        /// and its ground the player's land, one site of all the lots it stood on. False while the district cannot take it yet
        /// (the player inside, the district not standing at this load): asked again each day.
        /// </summary>
        private bool Demolished(ProjectFile p)
        {
            if (p.InDistrict)
            {
                return true;
            }
            int lots = 1;
            if (StandingOnLot(p.Lot, out StandingView b) && b.Over != null)
            {
                lots += b.Over.Length;
            }
            string why;
            try
            {
                why = Down("Demolish") || ConstructionBridges.Demolish == null ? "the district is not there to take it" : ConstructionBridges.Demolish(p.Lot);
            }
            catch (Exception e)
            {
                Faulty("Demolish", e);
                why = "a fault";
            }
            if (why != null)
            {
                Logger?.Info("Construction: project " + p.Id + "'s building is not down in the district yet (" + why + "); asked again each day.");
                return false;
            }
            p.InDistrict = true;
            _downLooks.Remove(p.Lot);
            int day = ConstructionGame.Day;
            LotFile land = _state.Lot(p.Lot);
            if (land == null && _state.Lots.Count < BuildCatalogue.LotsMost && Lot(p.Lot, out LotView lot))
            {
                land = new LotFile { Id = p.Lot, Bought = day, Zones = LandRules.ZonesOf(lot.Class), TaxedTo = day, EmptySince = day };
                _state.Lots.Add(land);
            }
            if (land != null)
            {
                land.Spaces = BuildCatalogue.Clamp(lots, 1, BuildCatalogue.SiteSpacesMost);
                land.EmptySince = day;
                long price = SitePrice(p.Lot, land.Spaces);
                land.Paid = price > 0L ? price : Lot(p.Lot, out LotView first) ? LotPrice(first) : 0L;   // the ground's worth as land: what its tax is on
            }
            Paper("reallife_bld_paper_demolished", ProjectName(p), 0);
            Tell(NotificationType.Success, "reallife_bld_notice_demolished", ProjectName(p), null, TabLand, 0);
            Logger?.Info("Construction: project " + p.Id + "'s building is down: its ground is the player's land, a site of " + lots + " lot(s) from " + p.Lot
                         + (land != null ? " worth " + land.Paid + " as land" : " (no more land may be kept)") + ".");
            return true;
        }
    }
}
