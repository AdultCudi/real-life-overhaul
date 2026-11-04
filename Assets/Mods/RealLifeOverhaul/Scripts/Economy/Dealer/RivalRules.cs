using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>A car a rival dealer has on its lot: which dealer, the game's vehicle type, the price it asks and the day it was put out.</summary>
    public struct RivalCar
    {
        public int Rival;
        public string Type;
        public int Ask;
        public int Day;
    }

    /// <summary>
    /// The other dealers of the city as sellers: each of the <see cref="DealerCatalogue.RivalNames"/> keeps a few cars of the game's own
    /// vehicle types on its lot with prices of its own, restocked a little each morning, and a buyer looking at the player's car of the
    /// same model compares the asking prices. Seeded from the save and the day, so the same on every machine. Game-free. COST: a few
    /// dozen cars a morning, a scan of at most <see cref="Most"/> cars when a buyer looks at one.
    /// </summary>
    public static class RivalRules
    {
        public const int PerDealerLeast = 3, PerDealerMost = 6;
        public const int Most = 6 * PerDealerMost;
        public const int SoldChance = 120;                      // a day, in thousandths, that a rival's car is sold and replaced
        public const int BiasLeast = 900, BiasMost = 1180;      // a dealer's usual price against the worth, in thousandths
        public const int Spread = 60;                           // a car's price against its dealer's usual, plus or minus
        public const int TypicalKm = 40000, TypicalCondition = 880;
        public const int DearFloor = 600, CheapMost = 1150;     // what comparing prices does to a buyer's offer chance, in thousandths

        /// <summary>A dealer's usual price against a car's worth, in thousandths: its own, the same all game.</summary>
        public static int BiasOf(int seed, int rival)
        {
            var dice = new DealerDice(seed, rival, 5, 77);
            return dice.Range(BiasLeast, BiasMost);
        }

        /// <summary>The morning's restock: sold cars go, each dealer is topped up to its number, from the game's vehicle types at today's prices.</summary>
        public static void Morning(DealerState s, int day, IList<MarketModel> models, int level)
        {
            int dealers = DealerCatalogue.RivalNames.Length;
            if (models == null || models.Count == 0)
            {
                s.Rivals.Clear();
                return;
            }
            for (int i = s.Rivals.Count - 1; i >= 0; i--)
            {
                RivalCar c = s.Rivals[i];
                bool known = false;
                for (int m = 0; m < models.Count && !known; m++)
                {
                    known = models[m].Type == c.Type;
                }
                var dice = new DealerDice(s.Seed, day, c.Rival * 131 + c.Day * 7 + (DealerRules.Hash(c.Type) % 9973) + c.Ask, 83);   // each car its own roll
                if (!known || c.Rival < 0 || c.Rival >= dealers || dice.Chance(SoldChance))
                {
                    s.Rivals.RemoveAt(i);
                }
            }
            for (int r = 0; r < dealers; r++)
            {
                var want = new DealerDice(s.Seed, r, 3, 79);
                int target = want.Range(PerDealerLeast, PerDealerMost);
                int have = 0;
                for (int i = 0; i < s.Rivals.Count; i++)
                {
                    have += s.Rivals[i].Rival == r ? 1 : 0;
                }
                int bias = BiasOf(s.Seed, r);
                var dice = new DealerDice(s.Seed, day, r, 89);
                for (int n = have; n < target && s.Rivals.Count < Most; n++)
                {
                    MarketModel m = models[dice.Range(0, models.Count - 1)];
                    int worth = DealerRules.Worth(m.Price, level, TypicalKm, TypicalCondition);
                    int ask = DealerCatalogue.Named(DealerCatalogue.Share(worth, bias + dice.Range(-Spread, Spread)));
                    if (ask > 0)
                    {
                        s.Rivals.Add(new RivalCar { Rival = r, Type = m.Type, Ask = ask, Day = day });
                    }
                }
            }
        }

        /// <summary>The lowest price any rival asks for a type of car, and who; 0 when none has one.</summary>
        public static int Cheapest(DealerState s, string type, out int rival)
        {
            rival = -1;
            int best = 0;
            for (int i = 0; i < s.Rivals.Count; i++)
            {
                RivalCar c = s.Rivals[i];
                if (c.Type == type && c.Ask > 0 && (best == 0 || c.Ask < best))
                {
                    best = c.Ask;
                    rival = c.Rival;
                }
            }
            return best;
        }

        /// <summary>
        /// What a rival's price for the same model does to a buyer's offer chance, in thousandths of it: dearer than the rival and they
        /// look elsewhere (down to <see cref="DearFloor"/>), cheaper and they are drawn (up to <see cref="CheapMost"/>); alike, nothing.
        /// </summary>
        public static int Compare(int ask, int rivalAsk)
        {
            if (rivalAsk <= 0 || ask <= 0)
            {
                return 1000;
            }
            long ratio = (long)ask * 1000 / rivalAsk;
            long dear = ratio - 1000;
            return dear > 0 ? (int)Math.Max(DearFloor, 1000 - dear * 2) : (int)Math.Min(CheapMost, 1000 + (-dear) / 2);
        }
    }
}
