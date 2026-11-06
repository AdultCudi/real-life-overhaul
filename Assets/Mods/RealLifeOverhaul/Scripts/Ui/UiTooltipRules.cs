using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A shift of a customer-facing employee in one business, as the split of the day's customers needs it. Game-free.
    /// </summary>
    public struct UiTipShift
    {
        public int Who;        // a stable order for the crew (a hash of the employee's id): ties in the split go by it
        public int From, To;   // the hours worked: From up to, not including, To
    }

    /// <summary>
    /// The tooltip card's sums, game-free and checked offline (EconomyCheck, TooltipChecks): where a card stands beside the
    /// pointer (the game's own rule), the mood word for a satisfaction, and a business's customers split among the
    /// employees who stood at the till. Cost: a few sums; nothing allocated.
    /// </summary>
    public static class UiTooltipRules
    {
        /// <summary>px of the layout the card keeps from the pointer when it hangs below it: the game's 40 canvas units at the kit's 2 to a px.</summary>
        public const float Gap = 20f;

        /// <summary>The mood steps, by the game's own colours for an employee's satisfaction: its tables turn red under 30 and yellow under 60.</summary>
        public const int Low = 30, Fair = 60;

        /// <summary>
        /// Where a card of a size stands for a pointer at a place (px of the layout, y downwards), as the game's TooltipSystem places its own:
        /// centred over the pointer with its foot on it, kept inside the screen at the sides; where it would not clear the top by
        /// the gap it hangs below the pointer instead. Kept inside the screen at the foot as well.
        /// </summary>
        public static void Place(float pointerX, float pointerY, float w, float h, float screenW, float screenH, float gap, out float x, out float y)
        {
            x = pointerX - w / 2f;
            if (x < 0f)
            {
                x = 0f;
            }
            else if (x + w > screenW)
            {
                x = screenW - w;
            }
            x = Math.Max(0f, x);
            y = pointerY - h;
            if (y < 0f)
            {
                y = 0f;
            }
            if (pointerY < h - gap)
            {
                y = pointerY + gap;
            }
            if (y + h > screenH)
            {
                y = screenH - h;
            }
            y = Math.Max(0f, y);
        }

        /// <summary>
        /// Where the card over a pill stands: over it, its right edge on the pill's (the money pill sits at the screen's right),
        /// its foot a gap above the pill's top, kept inside the screen. The pill's box is in px of the layout.
        /// </summary>
        public static void OverPill(float pillX, float pillY, float pillW, float w, float h, float screenW, float gap, out float x, out float y)
        {
            x = pillX + pillW - w;
            x = Math.Max(UiHudRules.Margin, Math.Min(x, screenW - UiHudRules.Margin - w));
            y = Math.Max(UiHudRules.Margin, pillY - gap / 2f - h);
        }

        /// <summary>The mood step of a satisfaction: 0 low, 1 fair, 2 good.</summary>
        public static int MoodStep(int satisfaction) => satisfaction < Low ? 0 : satisfaction < Fair ? 1 : 2;

        /// <summary>The colour of a mood step's bar.</summary>
        public static UiRole MoodTone(int satisfaction)
        {
            int step = MoodStep(satisfaction);
            return step == 0 ? UiRole.Danger : step == 1 ? UiRole.Warn : UiRole.Ok;
        }

        /// <summary>
        /// The customers one employee served of a business today. <paramref name="perHour"/> is how many customers the game completed in each
        /// hour so far; each hour's customers are shared among the customer-facing employees whose shift covers that hour, as evenly as
        /// whole customers go, the one who gets the odd customer turning with the hour. Customers of an hour nobody stood at the till are
        /// nobody's. Deterministic: no random draw, nothing saved.
        /// </summary>
        public static int Served(int[] perHour, UiTipShift[] crew, int count, int who)
        {
            int mine = 0;
            for (int h = 0; h < perHour.Length && h < 24; h++)
            {
                if (perHour[h] <= 0)
                {
                    continue;
                }
                int on = 0, before = -1;
                for (int i = 0; i < count; i++)
                {
                    if (h >= crew[i].From && h < crew[i].To)
                    {
                        if (crew[i].Who == who && before < 0)
                        {
                            before = Rank(crew, count, h, crew[i].Who);
                        }
                        on++;
                    }
                }
                if (on == 0 || before < 0)
                {
                    continue;
                }
                int share = perHour[h] / on, odd = perHour[h] % on;
                int turn = ((before - h) % on + on) % on;   // the odd customers go to those at the front of the turn
                mine += share + (turn < odd ? 1 : 0);
            }
            return mine;
        }

        // The place of an employee among those on the till at an hour, by their order.
        private static int Rank(UiTipShift[] crew, int count, int hour, int who)
        {
            int rank = 0;
            for (int i = 0; i < count; i++)
            {
                if (hour >= crew[i].From && hour < crew[i].To && crew[i].Who < who)
                {
                    rank++;
                }
            }
            return rank;
        }
    }
}
