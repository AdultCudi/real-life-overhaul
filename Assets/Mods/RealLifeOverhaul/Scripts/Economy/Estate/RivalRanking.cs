using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The game's rivals in the order its Rivals app ranks them (RivalLeaderboard: the dearer weekly takings first; the mod breaks a tie by id so the
    /// same game gives the same places on every machine), without the rivals the game counts as defeated (no shop or office left). One place
    /// for the rivals' holdings and for their fortunes to read it. COST: a sort of about twenty rivals; asked once a game day or on a page opening.
    /// </summary>
    internal static class RivalRanking
    {
        private static readonly List<KeyValuePair<long, RivalData>> Kept = new List<KeyValuePair<long, RivalData>>(32);

        /// <summary>The ranked rivals, the top first, into <paramref name="into"/>; the number of them.</summary>
        public static int Read(List<RivalData> into)
        {
            into.Clear();
            Kept.Clear();
            List<RivalData> all = RivalsHelper.GetAllRivalData();
            for (int i = 0; all != null && i < all.Count; i++)
            {
                RivalData rival = all[i];
                if (rival == null || string.IsNullOrEmpty(rival.id) || RivalsHelper.IsRivalDefeated(rival.id) || rival.ownedRetailOfficeBusinesses == null
                    || rival.ownedRetailOfficeBusinesses.Count < 1)
                {
                    continue;
                }
                Kept.Add(new KeyValuePair<long, RivalData>(Takings(rival), rival));
            }
            Kept.Sort((a, b) => RivalHoldingsRules.Compare(a.Key, a.Value.id, b.Key, b.Value.id));
            for (int i = 0; i < Kept.Count; i++)
            {
                into.Add(Kept[i].Value);
            }
            Kept.Clear();
            return into.Count;
        }

        /// <summary>A rival's weekly takings in cents, as the Rivals app sorts them; 0 where the game cannot say.</summary>
        public static long Takings(RivalData rival)
        {
            try
            {
                return (long)Math.Round(rival.WeeklyIncome * 100.0);
            }
            catch (Exception)
            {
                return 0L;
            }
        }
    }
}
