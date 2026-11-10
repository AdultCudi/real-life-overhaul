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
            h.PriceSeen = h.Rented ? 0L : h.Paid;   // the price it was bought at is the last price seen until the next look
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
            return Close(h, SpaceEnd.SoldBack, priceNow, 0);
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

        /// <summary>
        /// A holding looked at against its car park as the city has it now: missing if the car park is not there or has fewer spaces
        /// than its number (a building rebuilt smaller), and for a bought one the price and the owner as last seen, which is what a car
        /// park that then goes is settled by.
        /// </summary>
        public static void Looked(SpaceHolding h, SpacePark p, int day)
        {
            Seen(h, p != null && h.Slot <= p.Capacity, day);
            Noted(h, p);
        }

        /// <summary>The price and the owner of a bought space's car park, noted whenever the city is read.</summary>
        public static void Noted(SpaceHolding h, SpacePark p)
        {
            if (h != null && p != null && !h.Rented && p.Price > 0)
            {
                h.PriceSeen = p.Price;
                h.OwnSeen = p.Owner == SpaceOwner.Player;
            }
        }

        /// <summary>True when the price or the space a question box asked about is not what the city has now: the purchase is refused and asked again. -1 and 0: not asked.</summary>
        public static bool Moved(long shown, long now, int slotShown, int slotNow)
        {
            return (shown >= 0 && shown != now) || (slotShown > 0 && slotShown != slotNow);
        }

        /// <summary>True when a holding's car park has been missing long enough to be taken as gone (demolished, or rebuilt as another).</summary>
        public static bool Gone(SpaceHolding h, int day)
        {
            return h != null && h.Missing > 0 && day - h.Missing >= SpaceCatalogue.GoneDays;
        }

        /// <summary>
        /// What the holder of a space gets when it ends, one rule for every way. Bought, sold back or settled because the building is
        /// the player's now (their doing): the lower of what was paid and the price, less the fee. Bought, car park gone (somebody
        /// else's doing): what was paid, never more; if the building was the player's when last seen, the lower of paid and the last
        /// price seen, less the fee. Rented: the unused days of the term for a car park gone, nothing otherwise.
        /// </summary>
        public static long Close(SpaceHolding h, SpaceEnd end, long priceNow, int day)
        {
            if (h == null)
            {
                return 0L;
            }
            if (h.Rented)
            {
                if (end != SpaceEnd.CarParkGone)
                {
                    return 0L;
                }
                int left = SpaceCatalogue.Clamp(h.PaidTo - (h.Missing > 0 ? h.Missing : day) + 1, 0, SpaceCatalogue.RentDays);
                return h.Rent * left / SpaceCatalogue.RentDays;
            }
            bool theirs = end != SpaceEnd.CarParkGone || h.OwnSeen;
            long price = end == SpaceEnd.CarParkGone ? (h.PriceSeen > 0 ? h.PriceSeen : h.Paid) : priceNow;
            return theirs ? Back(h.Paid, price) : h.Paid;
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
            bool own = p.Owner == SpaceOwner.Player;
            int n = 0;
            for (int slot = 1; slot <= p.Capacity; slot++)
            {
                SpaceHolding h = s.At(p.Number, slot);
                if (h != null)
                {
                    n += h.Players && (own || !RentDue(h, day)) ? 1 : 0;   // a rented space covers the days it is paid for and not one hour more
                }
                else if (own && !CityHolds(s.Seed, p.Number, slot, day, p.CityShare))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>The cars of an hour whose charge is paid back: those standing there, as far as the spaces go.</summary>
        public static int Covered(int entitled, int cars)
        {
            return Math.Max(0, Math.Min(entitled, cars));
        }

        /// <summary>One car-hour counted for a vehicle in a car park on a day: at most a day's worth for a vehicle, and a bounded list.</summary>
        public static void AddHour(SpaceState s, int day, int park, string vehicle)
        {
            if (string.IsNullOrEmpty(vehicle) || park <= 0)
            {
                return;
            }
            for (int i = 0; i < s.Pending.Count; i++)
            {
                SpaceHours h = s.Pending[i];
                if (h.Day == day && h.Park == park && h.Vehicle == vehicle)
                {
                    h.Hours = Math.Min(SpaceCatalogue.HoursMost, h.Hours + 1);
                    return;
                }
            }
            if (s.Pending.Count < SpaceCatalogue.PendingMost)
            {
                s.Pending.Add(new SpaceHours { Day = day, Park = park, Vehicle = vehicle, Hours = 1 });
            }
        }

        /// <summary>
        /// What the game has really charged for the car-hours counted, in money: the hours of days before <paramref name="day"/> (the game
        /// takes a day's charges on the morning after it, in ParkingSimulator.RunDaily) for vehicles that are still in the save, since
        /// one sold or scrapped before that morning was never charged. Nothing is changed.
        /// </summary>
        public static long RefundDue(SpaceState s, int day, Func<string, bool> there)
        {
            long hours = 0;
            for (int i = 0; i < s.Pending.Count; i++)
            {
                SpaceHours h = s.Pending[i];
                if (h.Day < day && h.Hours > 0 && there != null && there(h.Vehicle))
                {
                    hours += Math.Min(h.Hours, SpaceCatalogue.HoursMost);
                }
            }
            return hours * SpaceCatalogue.HourFee;
        }

        /// <summary>The car-hours the game has charged are paid back, once: what <see cref="RefundDue"/> says, and the records of those days gone, charged or not.</summary>
        public static long Refund(SpaceState s, int day, Func<string, bool> there)
        {
            long refund = RefundDue(s, day, there);
            for (int i = s.Pending.Count - 1; i >= 0; i--)
            {
                if (s.Pending[i].Day < day)
                {
                    s.Pending.RemoveAt(i);
                }
            }
            s.RefundedTotal += refund;
            return refund;
        }

        // ------------------------------------------------------------------ the rival tycoons

        /// <summary>
        /// The tycoons' look of a day, by the player's rules: every <see cref="SpaceCatalogue.RivalEvery"/> days, each on a day of
        /// their own, a tycoon without a space may buy the lowest free one in a car park drawn for them (never in a building of the
        /// player's) at its price, if their worth stays over their floor; one who has held a space long enough may sell it back for
        /// what the owner pays. A space bought is worth what was paid, so their worth does not move and the daily growth has nothing
        /// to refill; a sale loses the fee. <paramref name="worth"/> is under nothing for a tycoon not in play. Moves go to <paramref name="into"/>.
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
                long paidFor = has.Paid, back = Sell(s, has, at.Price);
                into.Add(new SpaceMove { Tycoon = t, Park = has.Park, Slot = has.Slot, Amount = back, Worth = back - paidFor, Sold = true });
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
                h.PriceSeen = h.Paid > 0 ? Math.Max(0L, h.PriceSeen) : 0L;
                h.OwnSeen = h.Paid > 0 && h.OwnSeen;
            }
            for (int i = s.Pending.Count - 1; i >= 0; i--)
            {
                SpaceHours h = s.Pending[i];
                h.Hours = SpaceCatalogue.Clamp(h.Hours, 0, SpaceCatalogue.HoursMost);
                if (h.Hours <= 0 || h.Day < 0 || h.Park <= 0 || string.IsNullOrEmpty(h.Vehicle))
                {
                    s.Pending.RemoveAt(i);   // what cannot be read is not paid back
                }
            }
            for (int i = s.Pending.Count - 1; i >= 0; i--)
            {
                SpaceHours h = s.Pending[i];
                bool twice = false;
                for (int k = 0; k < i && !twice; k++)
                {
                    twice = s.Pending[k].Day == h.Day && s.Pending[k].Park == h.Park && s.Pending[k].Vehicle == h.Vehicle;
                }
                if (twice || i >= SpaceCatalogue.PendingMost)
                {
                    s.Pending.RemoveAt(i);   // an hour written twice for a vehicle is kept once
                }
            }
            s.RefundedTotal = Math.Max(0L, s.RefundedTotal);
        }
    }
}
