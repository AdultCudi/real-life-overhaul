using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// What a leftover of the player's land is offered when no design of the city's fits it (the owner, 28 Oct 2026: a block of
    /// five by five squares, a building two spaces wide placed on it, and each single space left over read "no design", with
    /// nothing to do about it). The game's designs for a single space are the shops' alone (an A1 is the only size one space wide), and
    /// every other use starts at two spaces, so a narrow leftover has a way out only by joining the site beside it. This picks the
    /// site to join with and says what to tell the player: a join that gives designs, a join that still gives none, or nothing to join
    /// with. The same rule is the city's: its growth leaves no run that nothing of the city's fills (GrowRules.Counts, Rest, Leave).
    /// Game-free (EconomyCheck's PlotJoinChecks), the same on every machine. COST: nothing per frame; a few comparisons when squares are picked.
    /// </summary>
    public static class PlotJoinRules
    {
        public const int Fits = 0;      // designs fit the squares picked: nothing to offer
        public const int Offer = 1;     // none fits, but joined with the site beside it some do
        public const int Useless = 2;   // none fits and a join is possible, but joined none fits either
        public const int Alone = 3;     // none fits and there is no site of the player's beside it to join with

        /// <summary>The sides a site can be joined on: the site after it along the block's side, the site before it.</summary>
        public const int Before = -1, Neither = 0, After = 1;

        /// <summary>
        /// Which neighbour to join a site with: the one that gives designs (the site after first when both do), else the one that can be joined
        /// at all (the site after first), else neither. <paramref name="afterOk"/> and <paramref name="beforeOk"/> say whether the join is possible,
        /// and the designs the joined shape would have on that side.
        /// </summary>
        public static int Side(bool afterOk, int afterDesigns, bool beforeOk, int beforeDesigns)
        {
            if (afterOk && afterDesigns > 0)
            {
                return After;
            }
            if (beforeOk && beforeDesigns > 0)
            {
                return Before;
            }
            return afterOk ? After : beforeOk ? Before : Neither;
        }

        /// <summary>What the panel says: <see cref="Fits"/> while designs fit already, else by the join on the side chosen and the designs it would give.</summary>
        public static int Kind(int designs, int side, int joinedDesigns)
        {
            if (designs > 0)
            {
                return Fits;
            }
            if (side == Neither)
            {
                return Alone;
            }
            return joinedDesigns > 0 ? Offer : Useless;
        }

        /// <summary>The columns of the shape a join makes: the first column of the pair (the site before's, when it joins on that side) and how many columns both take.</summary>
        public static void Shape(int side, int ownColumn, int ownSpaces, int otherColumn, int otherSpaces, out int firstColumn, out int columns)
        {
            firstColumn = side == Before ? otherColumn : ownColumn;
            columns = ownSpaces + (side == Neither ? 0 : otherSpaces);
        }

        /// <summary>The rows a joined shape is cut to for its designs: the shallowest of the two sites' and of the picked footprint (never less than one).</summary>
        public static int Rows(int picked, int ownRows, int otherRows)
        {
            return Math.Max(1, Math.Min(picked <= 0 ? int.MaxValue : picked, Math.Min(ownRows <= 0 ? int.MaxValue : ownRows, otherRows <= 0 ? int.MaxValue : otherRows)));
        }

        public const string OfferKey = "reallife_bld_join_offer";           // the panel's line for a join that gives designs
        public const string UselessKey = "reallife_bld_join_useless";       // ... a join that gives none
        public const string UseKey = "reallife_bld_join_use";               // ... another use that fits the squares as they are
        public const string ButtonKey = "reallife_bld_join_button";         // the button that joins
        public const string HintKey = "reallife_bld_hint_place_join";       // the hint bar's line while a join would give designs

        /// <summary>The first of the player's zoned uses other than <paramref name="use"/> that has designs on the squares, by the designs each has (-1 for none).</summary>
        public static int OtherUse(int use, int zones, int[] designsByUse)
        {
            for (int u = 0; designsByUse != null && u < designsByUse.Length; u++)
            {
                if (u != use && (zones & (1 << u)) != 0 && designsByUse[u] > 0)
                {
                    return u;
                }
            }
            return -1;
        }
    }
}
