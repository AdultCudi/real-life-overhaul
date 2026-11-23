using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How the rival owners' buildings are brought to their place in the Rivals app's ranking: not at all, at once in a new game, or also gently in a game under way.</summary>
    public enum RivalHoldingsPace
    {
        None = 0,
        AtOnce = 1,
        Gentle = 2
    }

    /// <summary>One building changing hands: its index in the book, and who held it and holds it now (a place in the ranking, or <see cref="HoldingsBook.Market"/>).</summary>
    public struct HoldingsMove
    {
        public int Building;
        public int From;
        public int To;
    }

    /// <summary>
    /// The buildings the plan may move, as the plan sees them: who holds each (a rival's place in the ranking, 0 the top; on the market; or
    /// not the plan's to touch), what it is worth, its district, and the day it was put on the market. The game side fills it from the
    /// registrations, the rules edit it, and the game side then makes the moves it lists. Game-free; the arrays are kept between runs.
    /// </summary>
    public sealed class HoldingsBook
    {
        public const int Market = -1;       // for sale: the game's list of buildings for sale
        public const int Fixed = -2;        // never the plan's: the player's, rented by the player, a special service, a Hamptons house, a held address, another player's

        public int Count;
        public int Rivals;
        public int Districts;
        public int[] Owner = new int[0];
        public long[] Value = new long[0];
        public int[] District = new int[0];
        public int[] ListedDay = new int[0];   // the day a market building was listed; 0 where it never was, so it counts as listed long ago
        public readonly List<HoldingsMove> Moves = new List<HoldingsMove>(64);

        public void Reset(int rivals, int districts)
        {
            Count = 0;
            Rivals = Math.Max(0, rivals);
            Districts = Math.Max(1, districts);
            Moves.Clear();
        }

        /// <summary>Adds a building and returns its index.</summary>
        public int Add(int owner, long value, int district, int listedDay)
        {
            if (Count == Owner.Length)
            {
                int n = Math.Max(256, Count * 2);
                Array.Resize(ref Owner, n);
                Array.Resize(ref Value, n);
                Array.Resize(ref District, n);
                Array.Resize(ref ListedDay, n);
            }
            Owner[Count] = owner;
            Value[Count] = Math.Max(0L, value);
            District[Count] = district;
            ListedDay[Count] = listedDay;
            return Count++;
        }
    }

    /// <summary>
    /// What the rival owners hold, by their place in the Rivals app's ranking. The game deals a new city's buildings to its rivals by the
    /// order of their ids and in fixed shares (CityGenerator.DistributeBuildingsToRivals: 24 per cent to the four special rivals, half to the
    /// first five of the others, the rest to the rest), and ranks them by their takings, so a rival last in the ranking can hold a hundred
    /// buildings and one near the top a handful. This puts it right: every rival holds a share of the buildings the rivals hold together by
    /// its place (<see cref="Targets"/>: a curve that rises with the place, never under <see cref="Least"/>, summing to the whole), a new city is
    /// dealt that way at once (<see cref="AtOnce"/>) and a game under way is moved towards it a few buildings a day (<see cref="Day"/>): a rival
    /// above its share puts buildings up for sale, a rival below it buys what has been for sale a few days. A rival high in the ranking sheds
    /// its cheapest first and buys its dearest, a rival low in it the other way, so the worth follows the place as the number does and the
    /// small homes the leaders let go reach the market. Game-free; every pick is by value and then by index, so the same city gives the same
    /// moves on every machine. Cost: a few passes over the book once a game day; nothing per frame.
    /// </summary>
    public static class RivalHoldingsRules
    {
        public const int OptionOff = 0, OptionNewGames = 1, OptionAlways = 2;   // the option's steps: the game's deal; a new game dealt at its start (the default); every game
        public const int NewGameDays = 3;      // a game this young is a new game: its first days
        public const int Least = 4;            // buildings: the least the last rival holds ("a handful"), the city having that many to give
        public const int Steepness = 2;        // the curve: a place's weight is the number of places below it, plus one, to this power
        public const int SalesPerDay = 3;      // buildings a day the rivals put up for sale: 21 a week at most
        public const int BuysPerDay = 3;       // buildings a day the rivals buy from the market: 21 a week at most
        public const int ListedDays = 3;       // days a building has been for sale before a rival may buy it: the player has them first
        public const int ListingsLeast = 2;    // buildings for sale a district keeps: a rival never buys the last of them

        /// <summary>
        /// The pace for an option step on a day: nothing, the whole deal at once (a new game's first days), or a few buildings a day. A game
        /// that was dealt at its start (<paramref name="marked"/>) goes on gently under the default step; a game that was not, only under the last.
        /// </summary>
        public static RivalHoldingsPace Pace(int option, int day, bool marked)
        {
            if (option <= OptionOff || option > OptionAlways)
            {
                return RivalHoldingsPace.None;
            }
            if (day <= NewGameDays)
            {
                return RivalHoldingsPace.AtOnce;
            }
            return option == OptionAlways || marked ? RivalHoldingsPace.Gentle : RivalHoldingsPace.None;
        }

        /// <summary>The weight of a place (0 the top) among <paramref name="rivals"/>: the number of places from it to the last, to <see cref="Steepness"/>.</summary>
        public static long Weight(int place, int rivals)
        {
            long k = Math.Max(1, rivals - place);
            long w = 1;
            for (int i = 0; i < Steepness; i++)
            {
                w *= k;
            }
            return w;
        }

        /// <summary>
        /// Each place's share of <paramref name="total"/> buildings, into <paramref name="into"/> (at least <paramref name="rivals"/> long, the top
        /// place first): <see cref="Least"/> to each, the rest by <see cref="Weight"/>, the odd buildings to the largest fractions and then the
        /// better place. Never rises towards the bottom and sums to the total. With fewer buildings than <see cref="Least"/> for each rival
        /// the floor is what each can have (the total over the rivals, rounded down) and the rest follows the same curve.
        /// </summary>
        public static void Targets(int rivals, int total, int[] into)
        {
            if (rivals <= 0 || into == null)
            {
                return;
            }
            total = Math.Max(0, total);
            long floor = Math.Min(Least, total / rivals);
            long rest = total - floor * rivals;
            long sum = 0;
            for (int i = 0; i < rivals; i++)
            {
                sum += Weight(i, rivals);
            }
            var frac = new long[rivals];
            long given = 0;
            for (int i = 0; i < rivals; i++)
            {
                long w = Weight(i, rivals);
                long share = rest * w / sum;
                frac[i] = rest * w % sum;
                into[i] = (int)(floor + share);
                given += share;
            }
            for (long left = rest - given; left > 0; left--)
            {
                int best = 0;
                for (int i = 1; i < rivals; i++)
                {
                    if (frac[i] > frac[best])
                    {
                        best = i;
                    }
                }
                into[best]++;
                frac[best] = -1;
            }
        }

        /// <summary>True for the top half of the places: they shed their cheapest buildings and buy their dearest.</summary>
        public static bool TopHalf(int place, int rivals) => place * 2 < rivals;

        /// <summary>The game's order of its rivals: the dearer takings first, then by id, so the same game gives the same places. Negative when A comes first.</summary>
        public static int Compare(long takingsA, string idA, long takingsB, string idB)
        {
            int c = takingsB.CompareTo(takingsA);
            return c != 0 ? c : string.CompareOrdinal(idA, idB);
        }

        /// <summary>The buildings each rival holds now, by place, into <paramref name="into"/>; returns the total of them.</summary>
        public static int Counts(HoldingsBook b, int[] into)
        {
            Array.Clear(into, 0, Math.Max(0, Math.Min(into.Length, b.Rivals)));
            int total = 0;
            for (int i = 0; i < b.Count; i++)
            {
                int o = b.Owner[i];
                if (o >= 0 && o < b.Rivals)
                {
                    into[o]++;
                    total++;
                }
            }
            return total;
        }

        /// <summary>How far the holdings are from their shares: the buildings that would have to change hands, counted once.</summary>
        public static int Distance(int[] counts, int[] targets, int rivals)
        {
            int over = 0;
            for (int i = 0; i < rivals; i++)
            {
                over += Math.Max(0, counts[i] - targets[i]);
            }
            return over;
        }

        /// <summary>
        /// The whole deal at once, for a new game: each rival above its share lets go the buildings over it (its cheapest in the top half, its
        /// dearest below), and the rivals below their shares take them, the best place first and the dearest first. Buildings on the market stay
        /// on it. Edits the book and lists the moves; returns how many buildings changed hands.
        /// </summary>
        public static int AtOnce(HoldingsBook b)
        {
            b.Moves.Clear();
            int rivals = b.Rivals;
            if (rivals < 2)
            {
                return 0;
            }
            var counts = new int[rivals];
            var targets = new int[rivals];
            int total = Counts(b, counts);
            Targets(rivals, total, targets);
            var pool = new List<int>(64);
            var held = new List<int>(64);
            var was = new int[b.Count];
            Array.Copy(b.Owner, was, b.Count);
            for (int p = 0; p < rivals; p++)
            {
                int surplus = counts[p] - targets[p];
                if (surplus <= 0)
                {
                    continue;
                }
                held.Clear();
                for (int i = 0; i < b.Count; i++)
                {
                    if (b.Owner[i] == p)
                    {
                        held.Add(i);
                    }
                }
                SortByValue(b, held, !TopHalf(p, rivals));   // cheapest first for the top half, dearest first below
                for (int k = 0; k < surplus && k < held.Count; k++)
                {
                    pool.Add(held[k]);
                    b.Owner[held[k]] = HoldingsBook.Fixed;   // out of every rival's hands until a taker has it
                }
            }
            SortByValue(b, pool, true);   // the dearest first
            int next = 0;
            for (int p = 0; p < rivals; p++)
            {
                int deficit = targets[p] - counts[p];
                for (int k = 0; k < deficit && next < pool.Count; k++)
                {
                    b.Owner[pool[next++]] = p;
                }
            }
            for (; next < pool.Count; next++)
            {
                b.Owner[pool[next]] = HoldingsBook.Fixed;   // cannot happen with whole counts; a building with no taker stays with nobody rather than with a rival that is over
            }
            int moved = 0;
            for (int i = 0; i < b.Count; i++)
            {
                if (b.Owner[i] != was[i])
                {
                    if (b.Owner[i] == HoldingsBook.Fixed)
                    {
                        b.Owner[i] = was[i];   // put back
                        continue;
                    }
                    b.Moves.Add(new HoldingsMove { Building = i, From = was[i], To = b.Owner[i] });
                    moved++;
                }
            }
            return moved;
        }

        /// <summary>
        /// One game day's moves for a game under way: up to <paramref name="salesMost"/> buildings put up for sale by the rivals furthest over
        /// their shares, then up to <paramref name="buysMost"/> bought from the market by the rivals furthest under theirs, from buildings that
        /// have been for sale <see cref="ListedDays"/> and never the last <see cref="ListingsLeast"/> of a district. Edits the book and lists the
        /// moves; returns how many buildings changed hands or were put up.
        /// </summary>
        public static int Day(HoldingsBook b, int day, int salesMost, int buysMost, out int sold, out int bought)
        {
            sold = bought = 0;
            b.Moves.Clear();
            int rivals = b.Rivals;
            if (rivals < 2)
            {
                return 0;
            }
            var counts = new int[rivals];
            var targets = new int[rivals];
            int total = Counts(b, counts);
            Targets(rivals, total, targets);
            for (int s = 0; s < salesMost; s++)
            {
                int seller = -1, most = 0;
                for (int p = 0; p < rivals; p++)
                {
                    int over = counts[p] - targets[p];
                    if (over > 0 && over >= most)   // the same surplus: the lower place sells first
                    {
                        seller = p;
                        most = over;
                    }
                }
                int pick = seller < 0 ? -1 : Pick(b, seller, TopHalf(seller, rivals));
                if (pick < 0)
                {
                    break;
                }
                b.Owner[pick] = HoldingsBook.Market;
                b.ListedDay[pick] = day;
                b.Moves.Add(new HoldingsMove { Building = pick, From = seller, To = HoldingsBook.Market });
                counts[seller]--;
                sold++;
            }
            var listed = new int[b.Districts];
            for (int i = 0; i < b.Count; i++)
            {
                if (b.Owner[i] == HoldingsBook.Market)
                {
                    listed[Math.Max(0, Math.Min(b.Districts - 1, b.District[i]))]++;
                }
            }
            for (int s = 0; s < buysMost; s++)
            {
                int buyer = -1, most = 0;
                for (int p = 0; p < rivals; p++)
                {
                    int under = targets[p] - counts[p];
                    if (under > most)   // the same shortfall: the better place buys first
                    {
                        buyer = p;
                        most = under;
                    }
                }
                int pick = buyer < 0 ? -1 : PickListed(b, day, listed, TopHalf(buyer, rivals));
                if (pick < 0)
                {
                    break;
                }
                listed[Math.Max(0, Math.Min(b.Districts - 1, b.District[pick]))]--;
                b.Owner[pick] = buyer;
                b.ListedDay[pick] = 0;
                b.Moves.Add(new HoldingsMove { Building = pick, From = HoldingsBook.Market, To = buyer });
                counts[buyer]++;
                bought++;
            }
            return sold + bought;
        }

        // A rival's building to put up: its cheapest (top half) or dearest, the lowest index among equals; -1 for none it can let go.
        private static int Pick(HoldingsBook b, int owner, bool cheapest)
        {
            int best = -1;
            for (int i = 0; i < b.Count; i++)
            {
                if (b.Owner[i] != owner)
                {
                    continue;
                }
                if (best < 0 || (cheapest ? b.Value[i] < b.Value[best] : b.Value[i] > b.Value[best]))
                {
                    best = i;
                }
            }
            return best;
        }

        // A building a rival may buy: for sale long enough, in a district with more than the least left for sale; the dearest for the top half, the cheapest below.
        private static int PickListed(HoldingsBook b, int day, int[] listed, bool dearest)
        {
            int best = -1;
            for (int i = 0; i < b.Count; i++)
            {
                if (b.Owner[i] != HoldingsBook.Market || b.ListedDay[i] > day - ListedDays)
                {
                    continue;
                }
                if (listed[Math.Max(0, Math.Min(b.Districts - 1, b.District[i]))] <= ListingsLeast)
                {
                    continue;
                }
                if (best < 0 || (dearest ? b.Value[i] > b.Value[best] : b.Value[i] < b.Value[best]))
                {
                    best = i;
                }
            }
            return best;
        }

        private static void SortByValue(HoldingsBook b, List<int> items, bool dearestFirst)
        {
            // Cheapest first, or with the flag the dearest first; equal values by index, so the order is the same on every machine.
            items.Sort((x, y) =>
            {
                int c = b.Value[x].CompareTo(b.Value[y]);
                if (c == 0)
                {
                    c = x.CompareTo(y);
                }
                return dearestFirst ? -c : c;
            });
        }
    }
}
