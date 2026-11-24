using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    public sealed partial class LogisticsDirector
    {
        private int _gaveDay = -1;                       // the day the evening's gifts were made on
        private int _gavePieces, _gavePlaces;            // what the evening gave, for the overnight notice
        private long _gaveCents;                         // ...and what it cost, cents

        /// <summary>Whether food that would spoil is given to charity in the evening: the option, with food spoilage.</summary>
        public static bool GiveWanted => SpoilWanted && Settings.LivingSpoilageGive;

        // What a place sold of a product: pieces that left it and were not thrown out, given or carried off by a trip (those are taken from the batches by the mod itself).
        // Kept only while the option is on, so with it off nothing is recorded or saved.
        private static void Sold(SpoilPlace p, string item, int pieces, int today)
        {
            if (pieces > 0)
            {
                p.SoldRing(item).Add(today, pieces);
            }
        }

        // The evening before a turn of the day: what would be thrown out at it is given to charity instead, once a day for each place. Exactly those pieces are taken from the
        // stock and from the batches, so the turn finds nothing of them and no piece is counted twice. Only what an ordinary shop would have left over earns anything
        // (FoodGiftRules.Earning); the rest is given all the same.
        private void GiveEvening(BuildingRegistration reg, SpoilPlace p, int today)
        {
            if (!GiveWanted || p.GaveDay == today || LogisticsGame.Now / 60 % 24 < FoodGiftRules.EveningHour || LogisticsBridges.FoodGiftReady == null || !LogisticsBridges.FoodGiftReady())
            {
                return;
            }
            p.GaveDay = today;
            int pieces = 0;
            long cents = 0, earnedCents = 0;
            foreach (KeyValuePair<string, List<Cohort>> kv in p.Batches)
            {
                int at = _spoilItems.IndexOf(kv.Key);
                if (at < 0)
                {
                    continue;
                }
                int life = SpoilRules.Scale(_spoilLives[at], p.Pace), coldLife = SpoilRules.Scale(_spoilColdLives[at], p.Pace);
                int due = FoodGiftRules.DueTomorrow(kv.Value, life, coldLife, _spoilChilled[at], today);
                if (due <= 0)
                {
                    continue;
                }
                int gone = LogisticsGame.Discard(reg, kv.Key, due, out int paid);
                SpoilRules.Take(kv.Value, gone, null);
                if (gone <= 0)
                {
                    continue;
                }
                int sold = p.Sold.TryGetValue(kv.Key, out WeekRing ring) ? ring.Sum(today) : 0;
                pieces += gone;
                cents += paid;
                earnedCents += FoodGiftRules.ValueOf(FoodGiftRules.Earning(gone, sold), gone, paid);
            }
            if (pieces <= 0)
            {
                return;
            }
            LogisticsBridges.FoodGift?.Invoke(cents / 100, earnedCents / 100);
            if (_gaveDay != today)
            {
                _gaveDay = today;
                _gavePieces = _gavePlaces = 0;
                _gaveCents = 0;
            }
            _gavePieces += pieces;
            _gaveCents += cents;
            _gavePlaces++;
            _changes++;
            string gains = string.Empty;
            if (earnedCents >= 100)
            {
                FoodGiftRules.Grant(today, p.GaveStanding, p.GaveStaff, _spoil.GaveStanding, _spoil.GaveMood, out int standing, out bool staff, out bool mood);
                int applied = standing > 0 && LogisticsBridges.Neighbourhood != null ? LogisticsBridges.Neighbourhood(LogisticsGame.DistrictOf(reg), standing) : 0;
                if (staff)
                {
                    LogisticsBridges.StaffLift?.Invoke(reg, FoodGiftRules.StaffLift);
                }
                if (mood)
                {
                    ModState.AddModifier(ModState.MoodUp);
                }
                gains = ", standing +" + applied + (staff ? ", staff lifted" : string.Empty) + (mood ? ", good mood" : string.Empty);
            }
            Logger?.Info("Food gift: " + pieces + " piece(s) that would have spoiled given to charity at " + LogisticsGame.NameOf(reg) + ", cost " + cents + " cents, of which "
                         + earnedCents + " cents earn" + gains + ".");
        }

        // The overnight notice's words when food went to charity the evening before.
        private bool GaveLastEvening(int today) => _gaveDay >= 0 && _gaveDay == today - 1 && _gavePieces > 0;

        private void TellGave(int today)
        {
            if (_spoil.Told >= today)
            {
                return;
            }
            _spoil.Told = today;
            int thrown = _roundDay == today ? _roundSpoiled : 0;
            long thrownCents = _roundDay == today ? _roundCents : 0;
            Notifications.Show(NotificationType.Info, "reallife_spoil_note_gave", new Dictionary<string, string>
            {
                { "given", _gavePieces.ToString(CultureInfo.InvariantCulture) },
                { "places", _gavePlaces.ToString(CultureInfo.InvariantCulture) },
                { "worth", Ui.UiText.Money(_gaveCents / 100f) },
                { "count", thrown.ToString(CultureInfo.InvariantCulture) },
                { "amount", Ui.UiText.Money(thrownCents / 100f) }
            }, 9f, null, null, true, false);
            _gavePieces = _gavePlaces = 0;
            _gaveCents = 0;
        }
    }
}
