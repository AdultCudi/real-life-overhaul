using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The city's developers: the rival builders' developments on the district's lots, as a pure function of the lot plan
    /// and the game's day, so every machine of a Going Public session comes to the same buildings without anything being
    /// sent. Half the lots (by a hash of the id) are theirs; they are taken in one order (the first block first), one
    /// every few days from <see cref="BuildCatalogue.DevFirstDay"/>, each taking 9 to 16 days. A lot the player owns, or one
    /// the district already holds a building on when its turn comes, is passed over. Game-free.
    /// </summary>
    public static class DevelopmentRules
    {
        /// <summary>True for a lot the city's developers build on in their turn.</summary>
        public static bool Theirs(string lotId)
        {
            return Fold(BuildRandom.Hash(lotId), 1000) < BuildCatalogue.DevSharePermille;
        }

        /// <summary>
        /// The developers' lots in the order they are built: of the lots given, those that are theirs, the first block's first,
        /// then by their id's hash, then by the id: the same on every machine. Into the caller's list (cleared): indices of
        /// <paramref name="lots"/>.
        /// </summary>
        public static void Order(IList<LotView> lots, List<int> into)
        {
            into.Clear();
            for (int i = 0; lots != null && i < lots.Count; i++)
            {
                if (lots[i].Id != null && Theirs(lots[i].Id))
                {
                    into.Add(i);
                }
            }
            // An insertion sort keeps it free of delegates; a district has a few hundred lots and this runs once a load.
            for (int i = 1; i < into.Count; i++)
            {
                int k = into[i];
                int j = i - 1;
                while (j >= 0 && Before(lots[k], lots[into[j]]))
                {
                    into[j + 1] = into[j];
                    j--;
                }
                into[j + 1] = k;
            }
        }

        private static bool Before(LotView a, LotView b)
        {
            if (a.First != b.First)
            {
                return a.First;
            }
            int ha = BuildRandom.Hash(a.Id), hb = BuildRandom.Hash(b.Id);
            if (ha != hb)
            {
                return ha < hb;
            }
            return string.CompareOrdinal(a.Id, b.Id) < 0;
        }

        /// <summary>The day the development in this turn (from 0) starts.</summary>
        public static int Start(int turn)
        {
            return BuildCatalogue.DevFirstDay + turn * BuildCatalogue.DevSpacing;
        }

        /// <summary>How many days a lot's development takes.</summary>
        public static int Days(string lotId)
        {
            return BuildCatalogue.DevDaysLeast + Fold(BuildRandom.Mix(BuildRandom.Hash(lotId), 7), BuildCatalogue.DevDaysSpread);
        }

        /// <summary>The turns that have started by a day: those from 0 up to, not with, this.</summary>
        public static int Started(int day)
        {
            return day < BuildCatalogue.DevFirstDay ? 0 : (day - BuildCatalogue.DevFirstDay) / BuildCatalogue.DevSpacing + 1;
        }

        /// <summary>What a lot of a class becomes: homes or shops on a small or big lot, offices on an office lot, a warehouse on a warehouse lot.</summary>
        public static BuildUse Use(string lotId, int lotClass)
        {
            int h = Fold(BuildRandom.Mix(BuildRandom.Hash(lotId), 3), 6);
            switch (lotClass)
            {
                case 0: return h % 2 == 0 ? BuildUse.Homes : BuildUse.Shops;
                case 1: return BuildUse.Offices;
                case 2: return h % 3 == 0 ? BuildUse.Homes : BuildUse.Shops;
                default: return BuildUse.Warehouses;
            }
        }

        private static readonly int[][] Builders =
        {
            new[] { 3, 7 },      // homes: Ostrander, Fenwick and Daughters
            new[] { 5, 2, 3 },   // shops: Mercer, Calloway Brothers, Ostrander
            new[] { 6, 3 },      // offices: Ironbridge, Ostrander
            new[] { 1, 4, 0 }    // warehouses: Hartwell, Tri-Street, Dunmore and Pike
        };

        /// <summary>The rival builder (an index of the city's contractors) who develops a lot for a use.</summary>
        public static int Rival(string lotId, BuildUse use)
        {
            int[] who = Builders[BuildCatalogue.Clamp((int)use, 0, Builders.Length - 1)];
            return who[Fold(BuildRandom.Mix(BuildRandom.Hash(lotId), 11), who.Length)];
        }

        /// <summary>Which of the designs that fit (from 0) a lot's development wears.</summary>
        public static int DesignPick(string lotId, int count)
        {
            return count <= 0 ? -1 : Fold(BuildRandom.Mix(BuildRandom.Hash(lotId), 13), count);
        }

        /// <summary>Where a development stands on a day: 0 not started (or passed over), 1 going up, 2 finished.</summary>
        public static int State(int turn, string lotId, int day)
        {
            int start = Start(turn);
            if (day < start)
            {
                return 0;
            }
            return day < start + Days(lotId) ? 1 : 2;
        }

        // A hash folded into 0 up to, not with, n, the same for a negative hash.
        private static int Fold(int hash, int n)
        {
            return n <= 0 ? 0 : (int)((uint)hash % (uint)n);
        }
    }
}
