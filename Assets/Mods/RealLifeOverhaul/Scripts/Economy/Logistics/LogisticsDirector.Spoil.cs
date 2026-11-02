using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    public sealed partial class LogisticsDirector
    {
        private readonly List<BuildingRegistration> _owned = new List<BuildingRegistration>(32);
        private readonly List<string> _spoilItems = new List<string>(8);
        private readonly List<int> _spoilLives = new List<int>(8);
        private readonly List<string> _gone = new List<string>(8);
        private readonly List<Cohort> _taken = new List<Cohort>(SpoilRules.CohortsMost);
        private int _ownedHour = -1;          // the absolute game hour the list of places was read in
        private int _spoilAt;                 // the next place looked at
        private int _roundSpoiled, _roundCents, _roundPlaces, _roundDay = -1;   // what the turn of the day threw out, for its one notice

        private void SpoilStart()
        {
            _ownedHour = -1;
            _spoilAt = 0;
            _roundDay = -1;
        }

        private void SpoilStop()
        {
            _owned.Clear();
            _ownedHour = -1;
        }

        // One of the player's places a second: its goods that spoil matched to what it holds, and at the turn of a day what is
        // past its life thrown out. The list of places is read again each game hour.
        private void SpoilSecond(int today)
        {
            int hour = LogisticsGame.Now / 60;
            if (hour != _ownedHour)
            {
                _ownedHour = hour;
                LogisticsGame.Owned(_owned);
                if (_spoilAt >= _owned.Count)
                {
                    _spoilAt = 0;
                }
            }
            if (_owned.Count == 0)
            {
                return;
            }
            if (_spoilAt >= _owned.Count)
            {
                _spoilAt = 0;
                Told(today);   // a whole round of the places: the turn's notice, once
            }
            Visit(_owned[_spoilAt++], today);
        }

        private void Visit(BuildingRegistration reg, int today)
        {
            if (reg == null || reg.Address == null)
            {
                return;
            }
            string key = LogisticsGame.Key(reg.Address);
            LogisticsGame.Spoilable(reg, _spoilItems, _spoilLives);
            if (_spoilItems.Count == 0 && !_spoil.Places.ContainsKey(key))
            {
                return;   // nothing here spoils: no record is made
            }
            SpoilPlace p = _spoil.Place(key);
            if (p.Turned < 0)
            {
                p.Turned = today;   // first seen (a save from before, or new stock): everything held is taken as stocked today
            }
            if (p.Turned < today)
            {
                Turn(reg, p, today);
            }
            for (int i = 0; i < _spoilItems.Count; i++)
            {
                p.Lives[_spoilItems[i]] = _spoilLives[i];
                int came = SpoilRules.Match(p.Of(_spoilItems[i]), LogisticsGame.InStock(reg, _spoilItems[i]), today);
                p.Note(today, 0, came, 0);
            }
            _gone.Clear();
            foreach (KeyValuePair<string, List<Cohort>> kv in p.Batches)
            {
                if (!_spoilItems.Contains(kv.Key))
                {
                    _gone.Add(kv.Key);   // none of it here now: sold, used or moved
                }
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                p.Batches.Remove(_gone[i]);
                p.Lives.Remove(_gone[i]);
            }
        }

        // The turn of a day at a place: each product's pieces past their life thrown out by the game's own calls.
        private void Turn(BuildingRegistration reg, SpoilPlace p, int today)
        {
            int spoiled = 0, cents = 0;
            foreach (KeyValuePair<string, List<Cohort>> kv in p.Batches)
            {
                int at = _spoilItems.IndexOf(kv.Key);
                int life = at >= 0 ? _spoilLives[at] : SpoilRules.Life(kv.Key, LogisticsGame.Weighed(kv.Key), false);
                int due = SpoilRules.Expired(kv.Value, life, today);
                if (due <= 0)
                {
                    continue;
                }
                int gone = LogisticsGame.Discard(reg, kv.Key, due, out int paid);
                SpoilRules.Take(kv.Value, gone, null);
                spoiled += gone;
                cents += paid;
            }
            p.Turned = today;
            if (spoiled <= 0)
            {
                return;
            }
            p.Note(today, spoiled, 0, cents);
            _changes++;
            if (_roundDay != today)
            {
                _roundDay = today;
                _roundSpoiled = _roundCents = _roundPlaces = 0;
            }
            _roundSpoiled += spoiled;
            _roundCents += cents;
            _roundPlaces++;
            Logger?.Info("Food spoilage: " + spoiled + " piece(s) past their shelf life thrown out at " + LogisticsGame.NameOf(reg) + " (cost " + cents + " cents).");
        }

        // The day's one notice of stock thrown out, after a whole round of the places.
        private void Told(int today)
        {
            if (_roundDay != today || _roundSpoiled <= 0 || _spoil.Told >= today)
            {
                return;
            }
            _spoil.Told = today;
            Notifications.Show(NotificationType.Warning, "reallife_spoil_note", new Dictionary<string, string>
            {
                { "count", _roundSpoiled.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "places", _roundPlaces.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "amount", Ui.UiText.Money(_roundCents / 100f) }
            }, 9f, null, null, true, false);
        }

        // Goods moved by a trip: their batches go with them, oldest first, so they keep their age at the stop.
        private void Moved(string fromKey, string toKey, string item, int count)
        {
            if (!SpoilOn || count <= 0 || SpoilRules.Life(item, LogisticsGame.Weighed(item), true) <= 0)
            {
                return;
            }
            _taken.Clear();
            if (_spoil.Places.TryGetValue(fromKey, out SpoilPlace from) && from.Batches.TryGetValue(item, out List<Cohort> b))
            {
                SpoilRules.Take(b, count, _taken);
            }
            SpoilPlace to = _spoil.Place(toKey);
            if (to.Turned < 0)
            {
                to.Turned = LogisticsGame.Day;
            }
            List<Cohort> into = to.Of(item);
            for (int i = 0; i < _taken.Count; i++)
            {
                SpoilRules.Add(into, _taken[i].Day, _taken[i].Count);
            }
            to.Note(LogisticsGame.Day, 0, count, 0);
        }

        // ------------------------------------------------------------------------------------------------- what the pages read

        /// <summary>
        /// Turns of the day until the oldest of a product at a place spoils (1: tonight), as the place keeps it now; -1 when it never
        /// spoils, the place holds none, or spoilage is off. For the stock pages and the buy page.
        /// </summary>
        public int SpoilsIn(BuildingRegistration place, string item)
        {
            if (!SpoilOn || place == null || place.Address == null || string.IsNullOrEmpty(item)
                || !_spoil.Places.TryGetValue(LogisticsGame.Key(place.Address), out SpoilPlace p) || !p.Batches.TryGetValue(item, out List<Cohort> b))
            {
                return -1;
            }
            int life = p.Lives.TryGetValue(item, out int kept) ? kept : SpoilRules.Life(item, LogisticsGame.Weighed(item), false);
            return SpoilRules.SpoilsIn(b, life, LogisticsGame.Day);
        }

        /// <summary>
        /// Days a product bought now keeps (out of or in cold storage); -1 when it never spoils or spoilage is off. Static, so the
        /// buy page may ask before the feature has started: it reads only the switch and the rules.
        /// </summary>
        public static int ShelfLife(string item, bool cold)
        {
            if (!SpoilWanted || string.IsNullOrEmpty(item))
            {
                return -1;
            }
            int life = SpoilRules.Life(item, LogisticsGame.Weighed(item), cold);
            return life > 0 ? life : -1;
        }

        /// <summary>A place's pieces spoiled and their cost (cents) today or over the last seven days, and the share of what came in, per cent (-1 none).</summary>
        public bool Spoiled(BuildingRegistration place, bool today, out int pieces, out int cents, out int percent)
        {
            pieces = cents = 0;
            percent = -1;
            if (!SpoilOn || place == null || place.Address == null || !_spoil.Places.TryGetValue(LogisticsGame.Key(place.Address), out SpoilPlace p))
            {
                return false;
            }
            p.Sum(LogisticsGame.Day, today, out pieces, out int came, out cents);
            percent = SpoilRules.Percent(pieces, came);
            return true;
        }
    }
}
