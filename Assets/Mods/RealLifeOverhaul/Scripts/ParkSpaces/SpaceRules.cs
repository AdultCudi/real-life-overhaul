using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>
    /// The rules of parking spaces in a building's car park, game-free: how many a car park has, which the city's people hold, what
    /// one costs to buy or rent and what the owner pays to take it back, who may take one, the rent's terms, the car-hours a holder
    /// is paid back for, what happens when a car park goes, and the rival tycoons' own spaces. Everything follows from the save's
    /// seed, the day and the arguments, so a reload changes nothing and two machines agree. COST: a pass over a car park's spaces
    /// (60 at most) for a count; nothing kept between calls.
    /// </summary>
    public static class SpaceRules
    {
        public static uint Hash(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u ^ (uint)c * 668265263u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        // ------------------------------------------------------------------ a car park

        /// <summary>The spaces of a car park: what the building code asks of a building of this use (BuildUse as a number) and floor area, within the least and most.</summary>
        public static int Capacity(int use, int sqm)
        {
            int asked = PlansRules.ParkingNeeded((BuildUse)SpaceCatalogue.Clamp(use, 0, (int)BuildUse.Count - 1), Math.Max(0, sqm));
            return SpaceCatalogue.Clamp(asked, SpaceCatalogue.CapacityLeast, SpaceCatalogue.CapacityMost);
        }

        /// <summary>The thousandths of a car park's spaces the city's people hold, by what an hour at the kerb costs in its district.</summary>
        public static int CityShare(double streetDollars)
        {
            double street = double.IsNaN(streetDollars) ? 0.0 : Math.Max(0.0, Math.Min(100.0, streetDollars));
            return SpaceCatalogue.Clamp(SpaceCatalogue.CityShareBase + (int)Math.Round(street * SpaceCatalogue.CitySharePerDollar), SpaceCatalogue.CityShareLeast,
                SpaceCatalogue.CityShareMost);
        }

        /// <summary>True when one of the city's people holds a space on a day: drawn afresh for each space every <see cref="SpaceCatalogue.CityTurnDays"/> days, each on its own day.</summary>
        public static bool CityHolds(int seed, int park, int slot, int day, int share)
        {
            int phase = (int)(Hash(seed, park, slot, 1) % SpaceCatalogue.CityTurnDays);
            int turn = (Math.Max(0, day) + phase) / SpaceCatalogue.CityTurnDays;
            return Hash(seed, park, slot, 100 + turn) % 1000u < (uint)SpaceCatalogue.Clamp(share, 0, 1000);
        }

        /// <summary>Who holds a car park's spaces today: the player, the rival tycoons, the city's people, and those nobody holds.</summary>
        public static void Counts(SpaceState s, SpacePark p, int day, out int player, out int rivals, out int city, out int free)
        {
            player = rivals = city = free = 0;
            for (int slot = 1; p != null && slot <= p.Capacity; slot++)
            {
                SpaceHolding h = s.At(p.Number, slot);
                if (h != null)
                {
                    player += h.Players ? 1 : 0;
                    rivals += h.Players ? 0 : 1;
                }
                else if (CityHolds(s.Seed, p.Number, slot, day, p.CityShare))
                {
                    city++;
                }
                else
                {
                    free++;
                }
            }
        }

        /// <summary>The lowest numbered space nobody holds today; 0 when the car park is full.</summary>
        public static int FreeSlot(SpaceState s, SpacePark p, int day)
        {
            for (int slot = 1; p != null && slot <= p.Capacity; slot++)
            {
                if (s.At(p.Number, slot) == null && !CityHolds(s.Seed, p.Number, slot, day, p.CityShare))
                {
                    return slot;
                }
            }
            return 0;
        }

        // ------------------------------------------------------------------ what a space costs

        /// <summary>
        /// A term's rent of a space: the share of the game's hourly charge round the clock that the mod's car parks let a bay for, by
        /// what the district does to a property's value (thousandths, 840 to 1160), named in tens.
        /// </summary>
        public static int RentMonth(int districtValue)
        {
            long month = (long)SpaceCatalogue.HourFee * 24L * SpaceCatalogue.RentDays * SpaceCatalogue.RentShare / 1000L;
            long rent = month * SpaceCatalogue.Clamp(districtValue, 500, 2000) / 1000L;
            return (int)Math.Max(SpaceCatalogue.RoundRent, (rent + SpaceCatalogue.RoundRent / 2) / SpaceCatalogue.RoundRent * SpaceCatalogue.RoundRent);
        }

        /// <summary>
        /// A space's price: its rent over a year of the save's length at the property market's yield, by the market's level
        /// (thousandths), named in hundreds.
        /// </summary>
        public static int Price(int rentMonth, int daysPerYear, int market)
        {
            long year = (long)Math.Max(0, rentMonth) * SpaceCatalogue.Clamp(daysPerYear, 8, 400) / SpaceCatalogue.RentDays;
            long price = year * 1000L / SpaceCatalogue.Yield * SpaceCatalogue.Clamp(market, 400, 2500) / 1000L;
            price = (price + SpaceCatalogue.RoundPrice / 2) / SpaceCatalogue.RoundPrice * SpaceCatalogue.RoundPrice;
            return (int)Math.Max(SpaceCatalogue.RoundPrice, Math.Min(price, int.MaxValue / 4));
        }

        /// <summary>
        /// What the building's owner pays to take a bought space back: the lower of what was paid for it and its price today, less
        /// the agent's fee. Never as much as was paid, so buying and selling makes nothing whatever the market does between.
        /// </summary>
        public static long Back(long paid, long priceNow)
        {
            long basis = Math.Max(0L, Math.Min(paid, priceNow));
            return basis * (1000L - SpaceCatalogue.BackFee) / 1000L;
        }

        // ------------------------------------------------------------------ taking and giving up

        /// <summary>
        /// Whether the player may take a space in a car park for a sum, ruled before any money moves: the feature on, not in a shared
        /// city, a car park that stands and is not their own building's, room under both limits, a space nobody holds, and the cash.
        /// </summary>
        public static SpaceRefusal MayTake(SpaceState s, SpacePark p, int day, bool on, bool session, long cash, long cost, out int slot)
        {
            slot = 0;
            if (!on || s == null)
            {
                return SpaceRefusal.Off;
            }
            if (session)
            {
                return SpaceRefusal.Session;
            }
            if (p == null || p.Capacity <= 0)
            {
                return SpaceRefusal.NoPark;
            }
            if (p.Owner == SpaceOwner.Player)
            {
                return SpaceRefusal.OwnBuilding;
            }
            if (s.Count(0, SpaceHolding.PlayerIs) >= SpaceCatalogue.HeldMost)
            {
                return SpaceRefusal.Limit;
            }
            if (s.Count(p.Number, SpaceHolding.PlayerIs) >= SpaceCatalogue.HeldPerPark)
            {
                return SpaceRefusal.ParkLimit;
            }
            slot = FreeSlot(s, p, day);
            if (slot <= 0)
            {
                return SpaceRefusal.Full;
            }
            return cost <= 0 || cost > cash ? SpaceRefusal.Cash : SpaceRefusal.None;
        }

        /// <summary>
        /// A space becomes somebody's: bought for <paramref name="paid"/>, or rented at <paramref name="rent"/> a term with the first
        /// term paid. Null, and nothing changed, if the space is not in the car park or is held already: no space is ever held twice.
        /// </summary>
        public static SpaceHolding Take(SpaceState s, SpacePark p, int slot, int who, int day, long paid, long rent)
        {
            if (p == null || slot < 1 || slot > p.Capacity || s.At(p.Number, slot) != null || CityHolds(s.Seed, p.Number, slot, day, p.CityShare)
                || (paid > 0) == (rent > 0))
            {
                return null;
            }
            var h = new SpaceHolding { Park = p.Number, Slot = slot, Who = who, Paid = Math.Max(0L, paid), Rent = Math.Max(0L, rent), Day = day };
            h.PaidTo = h.Rented ? day + SpaceCatalogue.RentDays - 1 : 0;
            s.Held.Add(h);
            return h;
        }

        /// <summary>Whether a bought space may be sold back to the building's owner now.</summary>
        public static SpaceRefusal MaySell(SpaceState s, SpaceHolding h, SpacePark p, bool on, bool session)
        {
            if (!on || s == null)
            {
                return SpaceRefusal.Off;
            }
            if (session)
            {
                return SpaceRefusal.Session;
            }
            if (h == null || !s.Held.Contains(h))
            {
                return SpaceRefusal.NotHeld;
            }
            if (h.Rented)
            {
                return SpaceRefusal.Rented;
            }
            if (p == null)
            {
                return SpaceRefusal.NoPark;
            }
            return p.Owner == SpaceOwner.Player ? SpaceRefusal.OwnBuilding : SpaceRefusal.None;
        }

        /// <summary>A bought space goes back to the building's owner: the holding is gone and what the owner pays is returned.</summary>
        public static long Sell(SpaceState s, SpaceHolding h, long priceNow)
        {
            if (h == null || h.Rented || !s.Held.Remove(h))
            {
                return 0L;
            }
            return Back(h.Paid, priceNow);
        }

        /// <summary>A rented space is given up (or kept after all): it ends with the term that is paid for, and nothing is paid back.</summary>
        public static SpaceRefusal SetEnding(SpaceState s, SpaceHolding h, bool ending, bool on)
        {
            if (!on || s == null)
            {
                return SpaceRefusal.Off;
            }
            if (h == null || !s.Held.Contains(h))
            {
                return SpaceRefusal.NotHeld;
            }
            if (!h.Rented)
            {
                return SpaceRefusal.Owned;
            }
            h.Ending = ending;
            return SpaceRefusal.None;
        }

        // ------------------------------------------------------------------ the day

        /// <summary>True on a day a rented space's paid term is over: the next term is due, or the space ends.</summary>
        public static bool RentDue(SpaceHolding h, int day)
        {
            return h != null && h.Rented && day > h.PaidTo;
        }

        /// <summary>A term's rent has been paid: the space is paid for a term from the day its last one ended, or from today after time away (none of it is asked for).</summary>
        public static void RentPaid(SpaceHolding h, int day)
        {
            h.PaidTo = Math.Max(h.PaidTo, day - 1) + SpaceCatalogue.RentDays;
        }

        /// <summary>A holding's car park was looked for: the day it was first missing is kept, and let go when it stands again.</summary>
        public static void Seen(SpaceHolding h, bool there, int day)
        {
            h.Missing = there ? 0 : h.Missing > 0 ? h.Missing : Math.Max(1, day);
        }

        /// <summary>True when a holding's car park has been missing long enough to be taken as gone (demolished, or rebuilt as another).</summary>
        public static bool Gone(SpaceHolding h, int day)
        {
            return h != null && h.Missing > 0 && day - h.Missing >= SpaceCatalogue.GoneDays;
        }

        /// <summary>
        /// What the holder of a space gets when its car park has gone: what a bought one was bought for (no fee: it was not their
        /// doing, and never more than they paid), and the days of a rented one's term not yet used.
        /// </summary>
        public static long GonePay(SpaceHolding h, int day)
        {
            if (h == null)
            {
                return 0L;
            }
            if (!h.Rented)
            {
                return h.Paid;
            }
            int left = SpaceCatalogue.Clamp(h.PaidTo - Math.Max(day, h.Missing) + 1, 0, SpaceCatalogue.RentDays);
            return h.Rent * left / SpaceCatalogue.RentDays;
        }

        // ------------------------------------------------------------------ the hour

        /// <summary>
        /// The cars a holder may leave in a car park without the hourly charge: one for each space of theirs there; in a building of
        /// their own, every space nobody else holds.
        /// </summary>
        public static int Entitled(SpaceState s, SpacePark p, int day)
        {
            if (s == null || p == null)
            {
                return 0;
            }
            Counts(s, p, day, out int player, out int _, out int _, out int free);
            return p.Owner == SpaceOwner.Player ? player + free : player;
        }

        /// <summary>The cars of an hour whose charge is paid back: those standing there, as far as the spaces go.</summary>
        public static int Covered(int entitled, int cars)
        {
            return Math.Max(0, Math.Min(entitled, cars));
        }

        public static void AddHours(SpaceState s, int park, int hours)
        {
            if (hours <= 0)
            {
                return;
            }
            int at = s.HourPark.IndexOf(park);
            if (at < 0)
            {
                s.HourPark.Add(park);
                s.HourCount.Add(hours);
            }
            else
            {
                s.HourCount[at] = Math.Min(s.HourCount[at] + hours, 24 * SpaceCatalogue.CapacityMost);
            }
        }

        /// <summary>The day's car-hours settled: the game's charge for each, as it took it, and the list emptied. Counted once, paid once.</summary>
        public static long Refund(SpaceState s)
        {
            long hours = 0;
            for (int i = 0; i < s.HourCount.Count; i++)
            {
                hours += Math.Max(0, s.HourCount[i]);
            }
            s.HourPark.Clear();
            s.HourCount.Clear();
            long refund = hours * SpaceCatalogue.HourFee;
            s.RefundedTotal += refund;
            return refund;
        }

        // ------------------------------------------------------------------ the rival tycoons

        /// <summary>
        /// The tycoons' look of a day, by the rules the player buys by: every <see cref="SpaceCatalogue.RivalEvery"/> days, each on a
        /// day of their own, a tycoon in play without a space may buy the lowest free one in a car park drawn for them (never in a
        /// building of the player's, whose spaces are not sold without the player) at its price, if their worth stays over their
        /// floor; one who has held a space long enough may sell it back for what the owner pays. <paramref name="worth"/> is each
        /// tycoon's worth, under nothing for one not in play. The moves are added to <paramref name="into"/> for the caller to book.
        /// </summary>
        public static void RivalLook(SpaceState s, IList<SpacePark> parks, int day, long[] worth, long[] floor, List<SpaceMove> into)
        {
            for (int t = 0; worth != null && t < worth.Length; t++)
            {
                if (worth[t] < 0 || parks == null || parks.Count == 0 || (day + (int)(Hash(s.Seed, t, 7, 0) % SpaceCatalogue.RivalEvery)) % SpaceCatalogue.RivalEvery != 0)
                {
                    continue;
                }
                SpaceHolding has = null;
                for (int i = 0; i < s.Held.Count && has == null; i++)
                {
                    has = s.Held[i].Who == t ? s.Held[i] : null;
                }
                if (has == null)
                {
                    if (Hash(s.Seed, t, day, 1) % 1000u >= SpaceCatalogue.RivalBuyChance)
                    {
                        continue;
                    }
                    int start = (int)(Hash(s.Seed, t, day, 2) % (uint)parks.Count);
                    for (int n = 0; n < parks.Count; n++)
                    {
                        SpacePark p = parks[(start + n) % parks.Count];
                        long least = floor != null && t < floor.Length ? floor[t] : 0L;
                        int slot = p.Owner == SpaceOwner.Player || p.Price <= 0 || worth[t] - p.Price < least ? 0 : FreeSlot(s, p, day);
                        if (slot > 0 && Take(s, p, slot, t, day, p.Price, 0) != null)
                        {
                            into.Add(new SpaceMove { Tycoon = t, Park = p.Number, Slot = slot, Amount = p.Price });
                            break;
                        }
                    }
                    continue;
                }
                SpacePark at = Find(parks, has.Park);
                if (at == null || at.Owner == SpaceOwner.Player || day - has.Day < SpaceCatalogue.RivalHoldDays || Hash(s.Seed, t, day, 3) % 1000u >= SpaceCatalogue.RivalSellChance)
                {
                    continue;
                }
                long back = Sell(s, has, at.Price);
                into.Add(new SpaceMove { Tycoon = t, Park = has.Park, Slot = has.Slot, Amount = back, Sold = true });
            }
        }

        public static SpacePark Find(IList<SpacePark> parks, int number)
        {
            for (int i = 0; parks != null && i < parks.Count; i++)
            {
                if (parks[i].Number == number)
                {
                    return parks[i];
                }
            }
            return null;
        }

        /// <summary>Every figure of a line read from a save put inside its bounds, and a space held twice kept once (the first).</summary>
        public static void Bound(SpaceState s)
        {
            for (int i = s.Held.Count - 1; i >= 0; i--)
            {
                SpaceHolding h = s.Held[i];
                bool twice = false;
                for (int k = 0; k < i && !twice; k++)
                {
                    twice = s.Held[k].Park == h.Park && s.Held[k].Slot == h.Slot;
                }
                if (twice || h.Park <= 0 || h.Slot < 1 || h.Slot > SpaceCatalogue.CapacityMost || h.Who < SpaceHolding.PlayerIs || (h.Paid <= 0 && h.Rent <= 0))
                {
                    s.Held.RemoveAt(i);
                    continue;
                }
                h.Paid = Math.Max(0L, h.Paid);
                h.Rent = h.Paid > 0 ? 0L : Math.Max(0L, h.Rent);
                h.PaidTo = h.Paid > 0 ? 0 : Math.Max(0, h.PaidTo);
                h.Missing = Math.Max(0, h.Missing);
            }
            for (int i = s.HourCount.Count - 1; i >= 0; i--)
            {
                s.HourCount[i] = SpaceCatalogue.Clamp(s.HourCount[i], 0, 24 * SpaceCatalogue.CapacityMost);
            }
            s.RefundedTotal = Math.Max(0L, s.RefundedTotal);
        }
    }
}
