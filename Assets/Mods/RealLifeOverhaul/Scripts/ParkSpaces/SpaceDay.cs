using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>The money lines of the parking spaces, each a line of the player's books of its own.</summary>
    public enum SpaceLine { Buy = 0, Sold, Rent, RentBack, Refund, Gone }

    /// <summary>What the player is told when the day's pass ends a space.</summary>
    public enum SpaceNote { Gone = 0, Ended, Unpaid, OwnBuilding }

    /// <summary>
    /// What the day's pass needs of the rest of the mod: the player's cash through the mod's one booking call, a notice, and a
    /// tycoon's worth. The director answers it with the game; the checks answer it with a ledger of their own, so the pass is run
    /// by the very code that ships.
    /// </summary>
    public interface ISpaceLedger
    {
        /// <summary>Money out of the player's cash; false, and nothing taken, if the cash does not cover it.</summary>
        bool Pay(long amount, SpaceLine line, SpacePark park);

        /// <summary>Money into the player's cash.</summary>
        void Receive(long amount, SpaceLine line, SpacePark park, SpaceHolding h);

        void Tell(SpaceNote note, SpacePark park, SpaceHolding h, long amount);

        /// <summary>A sum added to a tycoon's worth (under nothing: taken from it).</summary>
        void Tycoon(int who, long worth);
    }

    /// <summary>What a day's pass did.</summary>
    public struct SpaceDayResult
    {
        public long Refund;     // the game's charges paid back
        public int Gone;        // spaces settled because their car park is gone
        public int Owned;       // bought spaces settled because the building became the player's
        public int Ended;       // rented spaces that ended
    }

    /// <summary>
    /// The hour and the day of the parking spaces, game-free, so the director calls the very rules the offline checks drive: an hour
    /// counted once, the day's pass in a Going Public session or out of one, a car park that is gone or smaller, a building that
    /// becomes the player's, the rent, and the car park list read late. COST: a pass over the car parks and the spaces held.
    /// </summary>
    public static class SpaceDay
    {
        public const string BuyKey = "reallife_transaction_space_buy";
        public const string SoldKey = "reallife_transaction_space_sold";
        public const string RentKey = "reallife_transaction_space_rent";
        public const string RefundKey = "reallife_transaction_space_refund";
        public const string GoneKey = "reallife_transaction_space_gone";

        /// <summary>
        /// The line of the player's books each money line goes under. A space bought, sold back or settled is capital (not in the day's
        /// result); its rent, a rent paid back for the days a car park was gone, and the charges paid back are costs and count. So a
        /// rent is booked back where it was booked and never as capital.
        /// </summary>
        public static string KeyOf(SpaceLine line)
        {
            switch (line)
            {
                case SpaceLine.Buy: return BuyKey;
                case SpaceLine.Sold: return SoldKey;
                case SpaceLine.Rent:
                case SpaceLine.RentBack: return RentKey;
                case SpaceLine.Refund: return RefundKey;
                default: return GoneKey;
            }
        }

        /// <summary>
        /// An hour of the game, counted just before the game charges it: the cars the game is about to charge in a car park, as far
        /// as the player's spaces there go, are counted for their vehicles. An hour is counted once, whoever raises the event again;
        /// hours that went by unseen are never counted. Returns the car-hours counted, or -1 for an hour counted already.
        /// </summary>
        public static int Hour(SpaceState s, IList<SpacePark> parks, int now, int day, Action<int, List<string>> carsIn, List<string> scratch)
        {
            if (now == s.LastHour)
            {
                return -1;
            }
            s.LastHour = now;
            int counted = 0;
            for (int i = 0; parks != null && i < parks.Count; i++)
            {
                SpacePark p = parks[i];
                if (s.Count(p.Number, SpaceHolding.PlayerIs) == 0 && p.Owner != SpaceOwner.Player)
                {
                    continue;
                }
                scratch.Clear();
                carsIn(p.Number, scratch);
                int covered = scratch.Count > 0 ? SpaceRules.Covered(SpaceRules.Entitled(s, p, day), scratch.Count) : 0;
                for (int c = 0; c < covered; c++)
                {
                    SpaceRules.AddHour(s, day, p.Number, scratch[c]);
                }
                counted += covered;
            }
            return counted;
        }

        /// <summary>A save is loaded, or the feature is switched on: hours that went by without it are never counted.</summary>
        public static void Resume(SpaceState s, int now)
        {
            s.LastHour = now;
        }

        /// <summary>True when the car parks known are not all there is: none are known, or a space of the player's names a car park not among them. Read the city again.</summary>
        public static bool Stale(SpaceState s, IList<SpacePark> parks)
        {
            if (parks == null || parks.Count == 0)
            {
                return true;
            }
            for (int i = 0; i < s.Held.Count; i++)
            {
                if (s.Held[i].Players && SpaceRules.Find(parks, s.Held[i].Park) == null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The day's pass. The game's charges are paid back (the player's own money, so in a session too); the rent of the spaces held is
        /// taken or the space ends (also the player's own money); and outside a Going Public session, where the city's buildings are the
        /// host's and a tycoon's worth is not this machine's to change, the spaces whose car park is gone or smaller are settled and
        /// the bought spaces in a building that has become the player's. Nothing of a tycoon's or of another player's changes in a session.
        /// </summary>
        public static SpaceDayResult Pass(SpaceState s, IList<SpacePark> parks, int day, bool session, Func<string, bool> there, ISpaceLedger book)
        {
            var r = new SpaceDayResult();
            s.LastDay = day;
            r.Refund = SpaceRules.Refund(s, day, there);
            if (r.Refund > 0)
            {
                book.Receive(r.Refund, SpaceLine.Refund, null, null);
                s.PaidTotal -= r.Refund;
            }
            if (!session)
            {
                r.Gone = Settle(s, parks, day, book);
                r.Owned = Owned(s, parks, day, false, book);
            }
            r.Ended = Rents(s, parks, day, book);
            return r;
        }

        /// <summary>The spaces whose car park is no longer in the city, or has fewer spaces than their number: marked, and after the days of grace settled.</summary>
        public static int Settle(SpaceState s, IList<SpacePark> parks, int day, ISpaceLedger book)
        {
            int settled = 0;
            for (int i = s.Held.Count - 1; i >= 0; i--)
            {
                SpaceHolding h = s.Held[i];
                SpacePark p = SpaceRules.Find(parks, h.Park);
                SpaceRules.Looked(h, p, day);
                if (!SpaceRules.Gone(h, day))
                {
                    continue;
                }
                long pay = SpaceRules.Close(h, SpaceEnd.CarParkGone, 0L, day);
                s.Held.RemoveAt(i);
                settled++;
                if (h.Players)
                {
                    book.Receive(pay, h.Rented ? SpaceLine.RentBack : SpaceLine.Gone, p, h);   // an unused rent comes back where the rent was booked, a bought space is capital
                    s.PaidTotal -= pay;
                    book.Tell(SpaceNote.Gone, p, h, pay);
                }
                else
                {
                    book.Tycoon(h.Who, h.Rented ? 0L : pay - h.Paid);
                }
            }
            return settled;
        }

        /// <summary>
        /// The bought spaces in a building that has become the player's own are settled at once, as if sold back (the lower of what was
        /// paid and the price, less the fee): nobody is left to sell them to, and a space kept would come back at its full price when the
        /// building is demolished. Never in a Going Public session. A rented space there runs to the end of its term.
        /// </summary>
        public static int Owned(SpaceState s, IList<SpacePark> parks, int day, bool session, ISpaceLedger book)
        {
            int settled = 0;
            for (int i = s.Held.Count - 1; !session && i >= 0; i--)
            {
                SpaceHolding h = s.Held[i];
                SpacePark p = h.Rented ? null : SpaceRules.Find(parks, h.Park);
                if (p == null || p.Owner != SpaceOwner.Player)
                {
                    continue;
                }
                long pay = SpaceRules.Close(h, SpaceEnd.OwnBuilding, p.Price, day);
                s.Held.RemoveAt(i);
                settled++;
                if (h.Players)
                {
                    book.Receive(pay, SpaceLine.Sold, p, h);
                    s.PaidTotal -= pay;
                    book.Tell(SpaceNote.OwnBuilding, p, h, pay);
                }
                else
                {
                    book.Tycoon(h.Who, pay - h.Paid);
                }
            }
            return settled;
        }

        /// <summary>The player's rented spaces whose term is over: the next term taken, or the space given back (given up, the rent not there, or the building theirs now).</summary>
        public static int Rents(SpaceState s, IList<SpacePark> parks, int day, ISpaceLedger book)
        {
            int ended = 0;
            for (int i = s.Held.Count - 1; i >= 0; i--)
            {
                SpaceHolding h = s.Held[i];
                SpacePark p = SpaceRules.Find(parks, h.Park);
                if (!h.Players || !SpaceRules.RentDue(h, day) || p == null || h.Slot > p.Capacity)
                {
                    continue;   // a space whose car park is missing waits for it to stand again or to be settled
                }
                bool own = p.Owner == SpaceOwner.Player;   // the building has become the player's: nobody is left to pay rent to, and its spaces are theirs
                if (!h.Ending && !own && book.Pay(h.Rent, SpaceLine.Rent, p))
                {
                    SpaceRules.RentPaid(h, day);
                    s.PaidTotal += h.Rent;
                    continue;
                }
                s.Held.RemoveAt(i);
                ended++;
                book.Tell(h.Ending || own ? SpaceNote.Ended : SpaceNote.Unpaid, p, h, h.Rent);
            }
            return ended;
        }
    }
}
