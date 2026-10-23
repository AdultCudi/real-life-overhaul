using System;
using System.Collections.Generic;
using RealLifeOverhaul.Floors;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for a show flat of a block sold off plan: one at a time, dressed in one of the show flats' designs
    /// (Scripts/Designs, made from the game's residential designs) on a floor the floors core gives it, which the player walks
    /// into. Buyers come to its viewings; its quality against the district's standard sets how many come, how often one buys and
    /// at what price (ShowFlatRules); a sales agent shows them round when the player is not there. It costs its dressing once and
    /// its days. Asked on the hour, the day, a press and once a second; nothing per frame. Nothing of it is in the game's part of
    /// the save: its floor is asked again at each load from the project's line.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private ShowFlatPlace _showPlace;      // the show flat's floor place while one is open and the option is on
        private int _showPlaceFor;             // the project it stands for
        private readonly List<ShowDesignView> _showChoices = new List<ShowDesignView>(8);

        /// <summary>True while show flats are on: the construction game running and their option.</summary>
        public bool ShowFlatsOn => Running && Settings.ConstructionShowFlats;

        // ------------------------------------------------------------------ the designs

        /// <summary>The show flat designs a block's flats are offered (their structure the size of its flats), into the caller's list; false for none.</summary>
        public bool ShowChoices(ProjectFile p, List<ShowDesignView> into)
        {
            into.Clear();
            int flats = FlatsOf(p);
            int area = ShowFlatRules.FlatArea(p.Area, flats);
            try
            {
                return !Down("ShowDesigns") && ConstructionBridges.ShowDesigns != null && ConstructionBridges.ShowDesigns(area, into);
            }
            catch (Exception e)
            {
                into.Clear();
                Faulty("ShowDesigns", e);
                return false;
            }
        }

        private static bool ShowDesignOf(string id, out ShowDesignView design)
        {
            design = default(ShowDesignView);
            try
            {
                return !Down("ShowDesign") && ConstructionBridges.ShowDesign != null && ConstructionBridges.ShowDesign(id, out design);
            }
            catch (Exception e)
            {
                Faulty("ShowDesign", e);
                return false;
            }
        }

        /// <summary>The standard the project's district expects of an interior (a plot's is the new district's), 0 to 100.</summary>
        internal static int StandardOf(string district)
        {
            try
            {
                int standard = !Down("Standard") && ConstructionBridges.Standard != null ? ConstructionBridges.Standard(district ?? string.Empty) : -1;
                return standard >= 0 ? BuildCatalogue.Clamp(standard, 0, 100) : Interiors.InteriorRules.Standard(Interiors.InteriorRules.IncomeMiddle, 0);
            }
            catch (Exception e)
            {
                Faulty("Standard", e);
                return Interiors.InteriorRules.Standard(Interiors.InteriorRules.IncomeMiddle, 0);
            }
        }

        /// <summary>The show flat's quality now, 0 to 100, from the scores read as it opened.</summary>
        public static int ShowQuality(ProjectFile p) => ShowFlatRules.Quality(p.ShowInterior, p.ShowDecor);

        /// <summary>The plain off plan price of one of a block's flats now.</summary>
        public int OffPlanPrice(ProjectFile p)
        {
            return LandRules.OffPlanPrice(ValueOf(p, out _), Math.Max(1, FlatsOf(p)), p.Quality);
        }

        // ------------------------------------------------------------------ what the player does

        /// <summary>Why a show flat cannot be opened for a project now, as a text key; null when it can.</summary>
        public string ShowBlock(ProjectFile p)
        {
            if (!ShowFlatsOn)
            {
                return "reallife_bld_answer_show_off";
            }
            if (p == null || !ShowFlatRules.Allowed(p, FlatsOf(p)))
            {
                return "reallife_bld_answer_show_not_here";
            }
            if (p.ShowOpen)
            {
                return "reallife_bld_answer_show_open";
            }
            if (_state.ShowFlat() != null)
            {
                return "reallife_bld_answer_show_other";
            }
            return null;
        }

        /// <summary>A show flat opened for a project in a design of its choices, its dressing paid now (free for a test). Returns a text key.</summary>
        public string OpenShow(ProjectFile p, string designId, bool free)
        {
            string block = ShowBlock(p);
            if (block != null)
            {
                return block;
            }
            if (!ShowDesignOf(designId, out ShowDesignView d))
            {
                return "reallife_bld_answer_show_no_design";
            }
            long cost = ShowFlatRules.FitCost(d.Furniture, d.Fee);
            if (!free && !ConstructionGame.Take(cost, "reallife_transaction_bld_show_fit", ProjectName(p), true))
            {
                return "reallife_bld_answer_no_money";
            }
            int day = ConstructionGame.Day;
            p.Flats = FlatsOf(p);
            p.ShowDesign = d.Id;
            p.ShowSince = Math.Max(1, day);
            p.ShowInterior = d.Interior;
            p.ShowDecor = d.Decor;
            p.ShowStandard = StandardOf(p.District);
            p.ShowShown = 0;
            if (!free)
            {
                Spend(p, cost);
                p.ShowSpent += cost;
            }
            int quality = ShowQuality(p);
            Logger?.Info("Construction: project " + p.Id + "'s show flat opened in " + d.Id + " (" + d.Pieces + " pieces, interior " + d.Interior + ", decor " + d.Decor + ", quality " + quality
                         + " against the district's standard " + p.ShowStandard + "), " + (free ? "dressed for a test" : "dressed for " + cost) + "; "
                         + ShowFlatRules.Viewings(quality, p.ShowStandard) + " viewing(s) a day.");
            SyncShowPlace();
            Changed();
            return "reallife_bld_answer_show_opened";
        }

        /// <summary>The show flat closed: its floor let go, its agent's days over. Returns a text key.</summary>
        public string CloseShow(ProjectFile p, string why)
        {
            if (p == null || !p.ShowOpen)
            {
                return "reallife_bld_answer_show_none";
            }
            p.ShowSince = 0;
            p.ShowAgent = false;
            Logger?.Info("Construction: project " + p.Id + "'s show flat closed (" + why + ") after " + p.ShowViewings + " viewing(s) and " + p.ShowSold + " flat(s) sold there, " + p.ShowSpent
                         + " spent on it.");
            SyncShowPlace();
            Changed();
            return "reallife_bld_answer_show_closed";
        }

        /// <summary>A sales agent from the estate agency hired or let go. Returns a text key.</summary>
        public string SetAgent(ProjectFile p, bool agent)
        {
            if (p == null || !p.ShowOpen)
            {
                return "reallife_bld_answer_show_none";
            }
            p.ShowAgent = agent;
            Changed();
            return agent ? "reallife_bld_answer_agent_hired" : "reallife_bld_answer_agent_gone";
        }

        // ------------------------------------------------------------------ the floor

        // The show flat's floor asked or let go as the state says: one open and the option on, the floors core gives it a floor.
        private void SyncShowPlace()
        {
            ProjectFile open = ShowFlatsOn ? _state.ShowFlat() : null;
            if (open == null)
            {
                DropShowPlace(ShowFlatsOn ? "no show flat is open" : "show flats are off");
                return;
            }
            if (_showPlace != null && _showPlaceFor == open.Id && _showPlace.DesignId == open.ShowDesign)
            {
                return;
            }
            DropShowPlace("its design changed");
            if (!ShowDesignOf(open.ShowDesign, out ShowDesignView d))
            {
                Logger?.Warn("Construction: project " + open.Id + "'s show flat design " + open.ShowDesign + " is no longer in the game's files; the show flat is closed.");
                CloseShow(open, "its design is gone");
                Tell(NotificationType.Warning, "reallife_bld_notice_show_gone", ProjectName(open), null, TabProjects, open.Id);
                return;
            }
            _showPlace = new ShowFlatPlace(d, ProjectName(open));
            _showPlaceFor = open.Id;
            FloorsDirector.Ask(_showPlace);
            bool there = FloorsDirector.Where(_showPlace, out FloorWhere at);
            if (Verbose)
            {
                Logger?.Info("Show flats check (floor): project " + open.Id + "'s show flat asked a floor on structure " + d.Size + " " + d.Version + ": "
                             + (there ? "floor " + at.Floor + " of " + ConstructionGame.AddressText(at.Street, at.Number) + ": PASS" : "none free yet, it waits for one: PASS") + ".");
            }
        }

        private void DropShowPlace(string why)
        {
            if (_showPlace != null)
            {
                ShowFlatPlace place = _showPlace;
                _showPlace = null;
                _showPlaceFor = 0;
                try
                {
                    FloorsDirector.Release(place, why);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);   // the floors going with the city: their own unload puts every floor away
                }
            }
            HideViewing("the show flat's floor is let go");
        }

        /// <summary>Where the show flat is (its building and floor), for the app and the route; false while it has none.</summary>
        public bool ShowWhere(out FloorWhere where)
        {
            where = default(FloorWhere);
            return _showPlace != null && FloorsDirector.Where(_showPlace, out where);
        }

        /// <summary>True while the player is on the show flat's floor.</summary>
        public bool InShowFlat => _showPlace != null && _showPlace.Here;

        // ------------------------------------------------------------------ viewings

        private static int HourKey(int day, int hour) => day * 24 + hour;

        // The hour's viewings: the one just over is decided, and a new one is told of as it begins.
        private void ShowHour(int day, int hour)
        {
            ProjectFile p = ShowFlatsOn ? _state.ShowFlat() : null;
            if (p == null)
            {
                return;
            }
            if (!ShowFlatRules.Allowed(p, FlatsOf(p)))
            {
                CloseShow(p, p.Stage == ProjectStage.Finished ? "the block is handed over" : "nothing left to sell");
                return;
            }
            int quality = ShowQuality(p), viewings = ShowFlatRules.Viewings(quality, p.ShowStandard);
            int before = hour == 0 ? 23 : hour - 1, beforeDay = hour == 0 ? day - 1 : day;
            if (ShowFlatRules.ViewingAt(before, viewings) >= 0 && beforeDay >= p.ShowSince)
            {
                Viewing(p, HourKey(beforeDay, before), false);
            }
            if (ShowFlatRules.ViewingAt(hour, viewings) >= 0 && !p.ShowAgent && !InShowFlat)
            {
                Tell(NotificationType.Info, "reallife_bld_notice_viewing", ProjectName(p), null, TabProjects, p.Id);
            }
        }

        /// <summary>Who shows a viewing round: the player if they took it on the show flat's floor, else the agent, else nobody.</summary>
        private static ViewingHost HostOf(ProjectFile p, int key)
        {
            return p.ShowShown == key ? ViewingHost.Player : p.ShowAgent ? ViewingHost.Agent : ViewingHost.Nobody;
        }

        /// <summary>
        /// A viewing decided: the chance by the show flat's quality, the standard, the company's reputation and who showed the buyers
        /// round, drawn from the save's seed, the project and the hour; a buyer takes a flat at the show flat's price. True for a sale.
        /// </summary>
        internal bool Viewing(ProjectFile p, int key, bool forced)
        {
            ViewingHost host = HostOf(p, key);
            int quality = ShowQuality(p);
            int chance = ShowFlatRules.BuyerChance(quality, p.ShowStandard, _state.Firm.Reputation, host);
            bool bought = forced || BuildRandom.Of(_state.Seed, p.Id, key * 7 + 5).Chance(chance);
            p.ShowViewings++;
            int price = 0;
            if (bought)
            {
                price = ShowFlatRules.Price(OffPlanPrice(p), quality, p.ShowStandard);
                bought = price > 0 && OffPlanSale(p, price, 1) == 1;
            }
            if (bought)
            {
                p.ShowSold++;
                Tell(NotificationType.Success, "reallife_bld_notice_show_sold", ProjectName(p), null, TabProjects, p.Id, FlatsOf(p) - p.FlatsSold);
            }
            if (Verbose)
            {
                Logger?.Info("Show flats check (viewing): project " + p.Id + "'s viewing at hour " + key % 24 + " of day " + key / 24 + ", shown round by " + host + ": quality " + quality
                             + " against " + p.ShowStandard + ", a buyer's chance " + chance + " in 1000" + (forced ? " (a test's buyer)" : string.Empty) + ": "
                             + (bought ? "a flat sold at " + price + " (" + ShowFlatRules.PricePermille(quality, p.ShowStandard) + " in 1000 of the plain off plan price)" : "no buyer") + ": PASS.");
            }
            Changed();
            return bought;
        }

        /// <summary>The player takes the viewing that is on now on the show flat's floor. Returns a text key.</summary>
        public string ShowRound()
        {
            ProjectFile p = _state != null ? _state.ShowFlat() : null;
            if (p == null || !InShowFlat)
            {
                return "reallife_bld_answer_show_none";
            }
            int quality = ShowQuality(p);
            if (ShowFlatRules.ViewingAt(ConstructionGame.Hour, ShowFlatRules.Viewings(quality, p.ShowStandard)) < 0)
            {
                return "reallife_bld_answer_no_viewing";
            }
            p.ShowShown = HourKey(ConstructionGame.Day, ConstructionGame.Hour);
            Logger?.Info("Construction: the player shows the buyers round project " + p.Id + "'s show flat at " + ConstructionGame.Hour + ":00.");
            Changed();
            return "reallife_bld_answer_showing";
        }

        // A day of the open show flat: its suite and the agent, as the game's own costs run on.
        private void ShowDay(int day)
        {
            ProjectFile p = ShowFlatsOn ? _state.ShowFlat() : null;
            if (p == null || p.ShowSince >= day)
            {
                return;
            }
            long cost = ShowFlatRules.DayCost(p.ShowAgent, ConstructionGame.Prices);
            ConstructionGame.Force(cost, "reallife_transaction_bld_show", ProjectName(p), true);
            Spend(p, cost);
            p.ShowSpent += cost;
        }

        // ------------------------------------------------------------------ flats sold off plan

        /// <summary>
        /// Flats of a block sold off plan at a price each: a deposit now, the rest at completion. The one way off plan sales are
        /// made, at the plain price (SellOffPlan) or a show flat's (a viewing). Returns how many were sold.
        /// </summary>
        private int OffPlanSale(ProjectFile p, int price, int count)
        {
            p.Flats = FlatsOf(p);
            int n = Math.Min(Math.Max(0, count), p.Flats - p.FlatsSold);
            if (n <= 0 || price <= 0)
            {
                return 0;
            }
            int deposit = LandRules.Deposit(price);
            p.FlatsOffPlan += n;
            p.FlatsSold += n;
            p.After = AfterUse.SellFlats;
            p.Deposits += (long)deposit * n;
            p.OffPlanBalance += (long)(price - deposit) * n;
            ConstructionGame.Give((long)deposit * n, "reallife_transaction_bld_sales", ProjectName(p));
            _state.Firm.Earned += (long)deposit * n;
            Logger?.Info("Construction: project " + p.Id + " sold " + n + " flat(s) off plan at " + price + " each, " + deposit + " down.");
            return n;
        }

        // ------------------------------------------------------------------ once a second

        private void ShowSecond()
        {
            SyncShowPlace();
            ViewingSecond();
        }
    }
}
