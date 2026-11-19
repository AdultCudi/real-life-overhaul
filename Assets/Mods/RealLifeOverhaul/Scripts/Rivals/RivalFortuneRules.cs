using System;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>What the mod says one of the game's rivals is worth, in its parts: the buildings it owns, what they are worth, what they would rent for, and its weekly takings.</summary>
    public struct RivalFortune
    {
        public int Buildings;        // buildings it owns
        public long Property;        // their market value, dollars
        public long RentWeek;        // what they would rent for at the market's rate, dollars a week
        public long Trade;           // its weekly takings from its shops and offices, as the Rivals app shows them (the figure the game ranks by), dollars
        public long Worth;           // the fortune by the one rule: <see cref="RivalFortuneRules.Make"/>
    }

    /// <summary>
    /// A game rival's fortune by one rule, for every page of the mod that shows it. The game ranks its rivals by their weekly takings alone and
    /// shows how many buildings each owns without counting them in any figure, so a rival that owns a hundred buildings can read as poorer than a
    /// shop owner. The mod leaves the game's ranking number as it is and shows its breakdown beside it: the market value of the buildings, what
    /// they would rent for in a week, and the fortune, which is the buildings' value plus half a year of the weekly takings and rent (the same
    /// half year a rival can put up when it buys a business, <see cref="Economy.ServicesCatalogue.RivalPurseWeeks"/>). Game-free.
    /// </summary>
    public static class RivalFortuneRules
    {
        public const int RentDays = 7;   // a week of the market's daily rent

        /// <summary>The weeks of takings and rent a fortune counts: the half year a rival can put up when it buys a business.</summary>
        public static int PurseWeeks => Economy.ServicesCatalogue.RivalPurseWeeks;

        /// <summary>The fortune of a rival owning buildings worth <paramref name="property"/>, renting for <paramref name="rentDay"/> a day, with weekly takings <paramref name="trade"/>.</summary>
        public static RivalFortune Make(int buildings, long property, long rentDay, long trade)
        {
            var f = new RivalFortune
            {
                Buildings = Math.Max(0, buildings),
                Property = Math.Max(0L, property),
                RentWeek = Math.Max(0L, rentDay) * RentDays,
                Trade = trade
            };
            f.Worth = f.Property + PurseWeeks * Math.Max(0L, f.Trade + f.RentWeek);
            return f;
        }

        /// <summary>A rival's place by fortune among <paramref name="count"/> rivals given in the game's order (1 the richest); equal fortunes keep the game's order.</summary>
        public static int PlaceByFortune(long[] worth, int count, int index)
        {
            if (worth == null || index < 0 || index >= count || index >= worth.Length)
            {
                return 0;
            }
            int place = 1;
            for (int i = 0; i < count && i < worth.Length; i++)
            {
                if (worth[i] > worth[index] || (worth[i] == worth[index] && i < index))
                {
                    place++;
                }
            }
            return place;
        }
    }
}
