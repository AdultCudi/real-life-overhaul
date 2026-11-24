using System;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for the builder's licence by floors, the land's monthly asks and the company's worth: the licence level earned on the day's pass from the
    /// builds done and the standing (and told once), one ask a month a district read from the lots for sale, and the figures the Construction screen reads. The
    /// city's eight builders have a level by the same rule from what they have built and their standing. Nothing a frame; the day's pass reads the lots once a month.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private int _asksMonth = -1;

        /// <summary>The floors the company's licence covers.</summary>
        public int FloorsLicensed => LevelRules.FloorsOf(_state != null ? _state.Firm.Level : 0);

        /// <summary>The refusal for a building taller than the licence covers (a project's start).</summary>
        public const string FloorsKey = "reallife_ui_cons_answer_floors";

        /// <summary>The refusal for a bid on a building taller than the licence covers.</summary>
        public const string TenderFloorsKey = "reallife_bld_answer_tender_floors";

        /// <summary>The floors a won tender's building has once it is a project, 0 for the city's own works (they have no building to start): the one rule FromTender uses.</summary>
        public int TenderFloors(TenderFile t)
        {
            if (t == null || t.Source == TenderSource.CityWorks)
            {
                return 0;
            }
            if (t.Source == TenderSource.Landmark && Lot(t.Lot, out LotView site))
            {
                return Math.Max(1, Math.Min(BuildCatalogue.UptownHeightLimit, Math.Max(1, t.Area) / Math.Max(1, site.Width * site.Depth)));
            }
            return Math.Max(1, t.Area / 300);
        }

        /// <summary>The words a refusal for floors fills in: the building's floors, what the licence covers, and what the licence that covers them asks and the company has.</summary>
        private string[] FloorsPairs(int floors)
        {
            int level = LevelRules.LevelFor(floors);
            FirmFile f = _state.Firm;
            return new[]
            {
                "floors", floors.ToString(CultureInfo.InvariantCulture), "have", FloorsLicensed.ToString(CultureInfo.InvariantCulture),
                "builds", LevelRules.BuildsFrom[level].ToString(CultureInfo.InvariantCulture), "done", f.Finished.ToString(CultureInfo.InvariantCulture),
                "standing", LevelRules.StandingFrom[level].ToString(CultureInfo.InvariantCulture), "rep", f.Reputation.ToString(CultureInfo.InvariantCulture)
            };
        }

        /// <summary>The floors refusal's words for a project or a tender with these floors, as the page writes it.</summary>
        public string FloorsWords(string key, int floors) => ConstructionText.Fill(key, FloorsPairs(floors));

        /// <summary>How many of the city's works a builder has on its hands: the quotes' risk reads it.</summary>
        public int BuilderBusy(int rival) => Busy(BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1));

        /// <summary>The licence level a rival builder has now, by the builds it has done and its standing.</summary>
        public int RivalLevel(int rival)
        {
            if (_state == null || rival < 0 || rival >= _state.Rivals.Length)
            {
                return 0;
            }
            return LevelRules.Rival(_state.Rivals[rival].Built, _state.Rivals[rival].Reputation);
        }

        /// <summary>
        /// What the company is worth: its books (earned less spent), the land it holds at its worth now and what it has put into works under way. A company not
        /// founded has none.
        /// </summary>
        public long Worth()
        {
            if (_state == null || _state.Firm.Founded <= 0)
            {
                return 0L;
            }
            long worth = _state.Firm.Earned - _state.Firm.Spent;
            for (int i = 0; i < _state.Lots.Count; i++)
            {
                long value = SiteValue(_state.Lots[i].Id);
                worth += value > 0L ? value : _state.Lots[i].Paid;
            }
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                worth += _state.Projects[i].Live ? _state.Projects[i].Spent : 0L;
            }
            return worth;
        }

        // The day's pass: the licence level the builds and the standing have earned, and the month's asks.
        private void KitDay(int day)
        {
            LevelDay();
            AsksDay(day);
            FinishDay(day);
            VarianceDayPass(day);
            AuctionDay(day);
        }

        private void LevelDay()
        {
            FirmFile f = _state.Firm;
            if (f.Founded <= 0)
            {
                return;
            }
            int level = LevelRules.For(f.Finished, f.Reputation);
            if (level <= f.Level)
            {
                return;
            }
            f.Level = level;
            Logger?.Info("Construction: the company's licence now covers " + LevelRules.FloorsOf(level) + " floors (level " + level + ").");
            Tell(NotificationType.Info, "reallife_ui_cons_level_up", string.Empty, null, TabCompany, 0, LevelRules.FloorsOf(level));
            Changed();
        }

        // One ask a square metre for each district, once a month: the lots for sale, as the Land tab lists them, priced over their area.
        private void AsksDay(int day)
        {
            int month = AskRules.MonthOf(day, ConstructionGame.DaysPerYear);
            if (month == _asksMonth)
            {
                return;
            }
            _asksMonth = month;
            var price = new long[AskRules.DistrictsMost];
            var area = new long[AskRules.DistrictsMost];
            var key = new string[AskRules.DistrictsMost];
            var lots = Lots;
            for (int i = 0; i < lots.Count; i++)
            {
                LotView lot = lots[i];
                if (lot.District < 0 || lot.District >= price.Length || _state.Lot(lot.Id) != null || OnSpaces(lot) || !lot.Open || LotTaken(lot.Id) || DeveloperOn(lot.Id))
                {
                    continue;
                }
                long each = LotPrice(lot);
                if (each > 0L)
                {
                    price[lot.District] += each;
                    area[lot.District] += (long)lot.Width * lot.Depth;
                    key[lot.District] = lot.DistrictKey;
                }
            }
            for (int d = 0; d < price.Length; d++)
            {
                AskRing ring = key[d] != null ? _state.AskOf(key[d], true) : null;
                if (ring != null)
                {
                    ring.Count = AskRules.Put(ring.Months, ring.Asks, ring.Count, month, AskRules.Mean(price[d], area[d]));
                }
            }
            Changed();
        }
    }
}
