using System;
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
