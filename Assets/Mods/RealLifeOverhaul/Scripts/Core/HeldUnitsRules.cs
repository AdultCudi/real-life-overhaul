using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>Which feature holds an address, for the self-check's count; the order of the check line.</summary>
    public enum HeldKind
    {
        Police,        // a police station
        Towers,        // a tech tower (SpecialKind.Other: the only such place today)
        University,    // the university and its reserved building
        Hotels,        // a hotel
        Utilities,     // City Utilities and its reserved building
        Courthouse,    // the courthouse
        Reserved,      // one of the two offices kept for the player
        Conversions,   // an empty shop that is an office while the mod runs
        Count
    }

    /// <summary>One claim on an address: the holder's own id (one address at a time) and what it is.</summary>
    public struct HeldClaim
    {
        public string Holder;   // "book:police_2", "reserve:utilities_office", "offices:street#12"
        public string Street;
        public int Number;
        public HeldKind Kind;
    }

    /// <summary>An address held by one or more claims, and what the mod has done with it on the game's side.</summary>
    public sealed class HeldSpot
    {
        public string Street;
        public int Number;
        public int Holders;      // claims on it now; 0 only until its entry is off the game's list
        public HeldKind Kind;    // the first claim's
        public bool Ours;        // the mod put it on the game's list of addresses its rivals leave alone: only then taken off
        public object Place;     // the game side's own handle for it (its Address), made once by the Game file
        internal string Key;
    }

    /// <summary>
    /// Every address the mod holds, counted by claim (<see cref="Hold"/>, <see cref="Release"/>, <see cref="Sync"/>), so
    /// two features holding one address never take off each other's entry: an address is held while any claim is on it,
    /// and its entry comes off only when the last goes. Addresses and ids only, nothing of the game's or the save's; the
    /// caller puts the game's side right after each change (<see cref="HeldUnits"/>) and then <see cref="Prune"/>s.
    /// Game-free, checked by EconomyCheck. COST: dictionary work on a claim's change only.
    /// </summary>
    public sealed class HeldLedger
    {
        private readonly Dictionary<string, HeldClaim> _claims = new Dictionary<string, HeldClaim>(64, StringComparer.Ordinal);   // by holder
        private readonly Dictionary<string, HeldSpot> _spots = new Dictionary<string, HeldSpot>(64, StringComparer.Ordinal);      // by address key
        private readonly List<string> _gone = new List<string>(16);

        /// <summary>Changes each time a claim does.</summary>
        public int Stamp { get; private set; }

        /// <summary>Addresses held now (an address whose last claim went counts until it is pruned).</summary>
        public int Count => _spots.Count;

        public int Claims => _claims.Count;

        /// <summary>A claim made, or moved to another address; false if it was there already.</summary>
        public bool Hold(string holder, string street, int number, HeldKind kind)
        {
            if (string.IsNullOrEmpty(holder) || string.IsNullOrEmpty(street))
            {
                return false;
            }
            string key = HeldUnitsRules.Key(street, number);
            if (_claims.TryGetValue(holder, out HeldClaim was))
            {
                if (HeldUnitsRules.Key(was.Street, was.Number) == key)
                {
                    return false;
                }
                Drop(was);
            }
            _claims[holder] = new HeldClaim { Holder = holder, Street = street, Number = number, Kind = kind };
            if (!_spots.TryGetValue(key, out HeldSpot spot))
            {
                spot = new HeldSpot { Street = street, Number = number, Kind = kind, Key = key };
                _spots[key] = spot;
            }
            else if (spot.Holders == 0)
            {
                spot.Kind = kind;
            }
            spot.Holders++;
            Stamp++;
            return true;
        }

        /// <summary>A claim let go; its address stays held while another claim is on it. False if the holder held nothing.</summary>
        public bool Release(string holder)
        {
            if (string.IsNullOrEmpty(holder) || !_claims.TryGetValue(holder, out HeldClaim was))
            {
                return false;
            }
            _claims.Remove(holder);
            Drop(was);
            Stamp++;
            return true;
        }

        private void Drop(HeldClaim claim)
        {
            if (_spots.TryGetValue(HeldUnitsRules.Key(claim.Street, claim.Number), out HeldSpot spot) && spot.Holders > 0)
            {
                spot.Holders--;
            }
        }

        /// <summary>
        /// Every claim whose holder starts with <paramref name="prefix"/> made as <paramref name="wanted"/> says: the others
        /// of that prefix let go. Returns how many claims changed.
        /// </summary>
        public int Sync(string prefix, List<HeldClaim> wanted)
        {
            int changed = 0;
            _gone.Clear();
            foreach (string holder in _claims.Keys)
            {
                if (holder.StartsWith(prefix, StringComparison.Ordinal) && !Wants(wanted, holder))
                {
                    _gone.Add(holder);
                }
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                changed += Release(_gone[i]) ? 1 : 0;
            }
            for (int i = 0; wanted != null && i < wanted.Count; i++)
            {
                HeldClaim c = wanted[i];
                if (c.Holder != null && c.Holder.StartsWith(prefix, StringComparison.Ordinal))
                {
                    changed += Hold(c.Holder, c.Street, c.Number, c.Kind) ? 1 : 0;
                }
            }
            _gone.Clear();
            return changed;
        }

        private static bool Wants(List<HeldClaim> wanted, string holder)
        {
            for (int i = 0; wanted != null && i < wanted.Count; i++)
            {
                if (string.Equals(wanted[i].Holder, holder, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Every claim let go (the city unloading); the spots stay until their entries are off and they are pruned.</summary>
        public int ReleaseAll()
        {
            int n = _claims.Count;
            _claims.Clear();
            foreach (HeldSpot spot in _spots.Values)
            {
                spot.Holders = 0;
            }
            if (n > 0)
            {
                Stamp++;
            }
            return n;
        }

        /// <summary>
        /// Every address's entry on the game's ignore list made as the rule says now, then the addresses let go forgotten:
        /// wanted while <paramref name="ready"/>, held and <paramref name="mayList"/>; <paramref name="ignore"/> makes the
        /// change by <see cref="HeldUnitsRules.IgnoreChange"/> and says whether the entry is the mod's after;
        /// <paramref name="listed"/> says whether the list has it at all (the mod's entry taken off by somebody else is put
        /// back). Returns how many of the mod's entries changed.
        /// </summary>
        public int Apply(bool ready, Func<HeldSpot, bool> mayList, Func<HeldSpot, bool> listed, Func<HeldSpot, bool, bool> ignore)
        {
            int changed = 0;
            foreach (HeldSpot spot in _spots.Values)
            {
                bool want = ready && spot.Holders > 0 && mayList(spot);
                if (want != spot.Ours || (want && !listed(spot)))
                {
                    bool was = spot.Ours;
                    spot.Ours = ignore(spot, want);
                    changed += was != spot.Ours ? 1 : 0;
                }
            }
            Prune();
            return changed;
        }

        /// <summary>The addresses no claim holds and the mod has no entry for are forgotten.</summary>
        public int Prune()
        {
            _gone.Clear();
            foreach (KeyValuePair<string, HeldSpot> pair in _spots)
            {
                if (pair.Value.Holders <= 0 && !pair.Value.Ours)
                {
                    _gone.Add(pair.Key);
                }
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                _spots.Remove(_gone[i]);
            }
            int n = _gone.Count;
            _gone.Clear();
            return n;
        }

        /// <summary>True while a claim is on an address.</summary>
        public bool Holds(string street, int number)
        {
            return _spots.Count > 0 && !string.IsNullOrEmpty(street) && _spots.TryGetValue(HeldUnitsRules.Key(street, number), out HeldSpot spot) && spot.Holders > 0;
        }

        public HeldSpot At(string street, int number)
        {
            return !string.IsNullOrEmpty(street) && _spots.TryGetValue(HeldUnitsRules.Key(street, number), out HeldSpot spot) ? spot : null;
        }

        /// <summary>Every address the ledger keeps, held or waiting to be pruned, into the caller's list (cleared first).</summary>
        public void Spots(List<HeldSpot> into)
        {
            into.Clear();
            foreach (HeldSpot spot in _spots.Values)
            {
                into.Add(spot);
            }
        }

        /// <summary>The addresses held now by the first claim's kind, into <paramref name="byKind"/> (HeldKind.Count long).</summary>
        public void Tally(int[] byKind)
        {
            Array.Clear(byKind, 0, byKind.Length);
            foreach (HeldSpot spot in _spots.Values)
            {
                if (spot.Holders > 0 && (int)spot.Kind < byKind.Length)
                {
                    byKind[(int)spot.Kind]++;
                }
            }
        }
    }

    /// <summary>
    /// The rules of the addresses the mod holds against the game's own letting. The game's night lets its empty units
    /// (CompetitionHelper.RunDaily: StartNewBusiness and the residential and warehouse swaps), and it takes them from
    /// BuildingHelper.AllNeighbourhoodBuildings, which nothing else of the game reads: so a held unit standing empty is
    /// out of that list for the night's pass only (<see cref="OutOfPass"/>) and back as it was right after. The game's
    /// list of addresses its rivals leave alone (RivalsHelper.IgnoredAddresses) also keeps the game's businesses off, but
    /// its rivals' step (RivalsHelper.RefreshRivals, every game hour) blanks a rival's landlord and business at every
    /// address on it, and the save keeps that: so a held address goes on that list only while it has neither
    /// (<see cref="MayList"/>), which is also how the game keeps its own entry. Game-free, checked by EconomyCheck.
    /// </summary>
    public static class HeldUnitsRules
    {
        public const string BookPrefix = "book:";          // a building a feature put in the book of special buildings
        public const string ReservePrefix = "reserve:";    // a fixed address a feature reserves
        public const string OfficesPrefix = "offices:";    // an empty shop that is an office while the mod runs

        public static string Key(string street, int number) => (street ?? string.Empty) + "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// What becomes of an address on the game's list of addresses its rivals leave alone, a shared list: +1 the mod adds
        /// it, -1 the mod takes its own entry off, 0 nothing. <paramref name="ours"/> is set to whether the entry there is
        /// the mod's. One already there that the mod did not add (the game's own, another mod's) is never taken off, and is
        /// not the mod's after.
        /// </summary>
        public static int IgnoreChange(bool want, bool listed, ref bool ours)
        {
            if (want)
            {
                if (!listed)
                {
                    ours = true;
                    return 1;
                }
                return 0;   // there already: the mod's if it put it there, else somebody else's, left as it is
            }
            bool mine = ours;
            ours = false;
            return mine && listed ? -1 : 0;
        }

        /// <summary>
        /// True if a held address may be on the game's ignore list: no rival is its landlord or runs a business in it, it is
        /// not for sale (a rival may buy it at midnight, and the same night's rivals' step would blank the purchase), and
        /// this machine is not a guest in a Going Public session (the rivals there are the host's). The rivals' step then
        /// has nothing to write for it.
        /// </summary>
        public static bool MayList(bool held, bool rivalLandlord, bool rivalBusiness, bool forSale, bool guest)
        {
            return held && !rivalLandlord && !rivalBusiness && !forSale && !guest;
        }

        /// <summary>
        /// True if a held unit sits out of the game's night of letting: it stands empty, no player rents or owns it (a
        /// player's own empty building is theirs to let), and it is not a special service (the game never lets one). A
        /// held unit with a business in it stays in, so the game may still close that business as it would.
        /// </summary>
        public static bool OutOfPass(bool held, bool empty, bool players, bool special)
        {
            return held && empty && !players && !special;
        }

        /// <summary>The kind of a building put in the book, by what the book says it is.</summary>
        public static HeldKind KindOf(SpecialKind kind)
        {
            switch (kind)
            {
                case SpecialKind.Police:
                    return HeldKind.Police;
                case SpecialKind.Court:
                    return HeldKind.Courthouse;
                case SpecialKind.Hotel:
                    return HeldKind.Hotels;
                case SpecialKind.Campus:
                    return HeldKind.University;
                case SpecialKind.Civic:
                    return HeldKind.Utilities;
                default:
                    return HeldKind.Towers;
            }
        }

        /// <summary>The kind of a reservation: the player's offices by their id, any other by the kind its feature reserved it as.</summary>
        public static HeldKind ReservedKind(string id, SpecialKind kind)
        {
            return id != null && id.StartsWith(CityReserves.PlayerOfficeId, StringComparison.Ordinal) ? HeldKind.Reserved : KindOf(kind);
        }

        /// <summary>"police 6, towers 3, university 1, hotels 7, utilities 1, courthouse 1, reserved 2, conversions 0".</summary>
        public static string Counts(int[] byKind)
        {
            var text = new System.Text.StringBuilder(128);
            for (int i = 0; i < (int)HeldKind.Count && i < byKind.Length; i++)
            {
                text.Append(i > 0 ? ", " : string.Empty).Append(Names[i]).Append(' ').Append(byKind[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        private static readonly string[] Names = { "police", "towers", "university", "hotels", "utilities", "courthouse", "reserved", "conversions" };
    }
}
