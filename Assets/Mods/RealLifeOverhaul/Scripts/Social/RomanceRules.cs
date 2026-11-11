using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Who the player can fall for: one rule for the people of the social life, the strangers street talk stops and the faces Flare
    /// shows. They are of the sex the player is interested in (the option "Who you are interested in": anyone, women or men) and
    /// within <see cref="AgeGap"/> years of the player's own age, the gap the city's own couples keep when they pair up
    /// (Population/LifeRules.AgeGapMost). Game-free. COST: arithmetic, on a press or when a page is drawn.
    /// </summary>
    public static class RomanceRules
    {
        public const int Anyone = 0, Women = 1, Men = 2;   // what the player is interested in, as the option saves it
        public const int AgeLeast = 18;                    // years: nobody younger is ever somebody to fall for
        public const int AgeMost = 85;                     // years: the oldest anybody is kept (the cast, the faces, the People page)

        // Years either side of the player's age. It is the city's own: its people pair up within it (Population/LifeRules.PairAll),
        // so the player is held to what the city's couples keep.
        public const int AgeGap = RealLifeOverhaul.Population.LifeRules.AgeGapMost;

        // Years inside the gap that somebody is made, never on its edge. Their birthday and the player's fall on different days, so for
        // the weeks between them the two are a year further apart (or nearer) than the rest of the year: somebody made exactly AgeGap
        // away would be out of the window for part of every year, and Ask them out and the People line would go and come back with it.
        public const int AgeMargin = 1;

        /// <summary>Whether somebody of this sex is one the player is interested in. A value that is neither women nor men is anyone.</summary>
        public static bool SexFits(int interest, bool female)
        {
            return interest == Women ? female : interest == Men ? !female : true;
        }

        /// <summary>
        /// Whether an age is near the player's: an adult within <see cref="AgeGap"/> years. A player's age not known (0 or less)
        /// rules nobody out for it. This is the verdict on somebody as they are today; who is made for a player is kept further
        /// inside it (<see cref="AgeSteady"/>).
        /// </summary>
        public static bool AgeFits(int age, int playerAge)
        {
            return playerAge <= 0 || (age >= AgeLeast && Math.Abs(age - playerAge) <= AgeGap);
        }

        /// <summary>
        /// Whether an age stays near the player's through every birthday: an adult within <see cref="AgeGap"/> less
        /// <see cref="AgeMargin"/> years, so the year more or less between the two in the weeks between their birthdays still fits.
        /// Whoever is made for a player is kept to it (<see cref="AgeNear"/>). A player's age not known rules nobody out.
        /// </summary>
        public static bool AgeSteady(int age, int playerAge)
        {
            return playerAge <= 0 || (age >= AgeLeast && Math.Abs(age - playerAge) <= AgeGap - AgeMargin);
        }

        /// <summary>Whether somebody of this sex and age is somebody the player could fall for.</summary>
        public static bool Fits(int interest, bool female, int age, int playerAge)
        {
            return SexFits(interest, female) && AgeFits(age, playerAge);
        }

        /// <summary>
        /// An age for somebody made to fit the player: any whole age from <see cref="AgeGap"/> less <see cref="AgeMargin"/> under
        /// theirs to as much over, kept between <see cref="AgeLeast"/> and <see cref="AgeMost"/> (the margin is taken from the gap's
        /// edge, not from eighteen: for a player under twenty-six the youngest is eighteen). Never on the gap's edge, so the person
        /// keeps fitting through both birthdays of every year (<see cref="AgeSteady"/>). <paramref name="roll"/> is any number; the
        /// same gives the same age.
        /// </summary>
        public static int AgeNear(int playerAge, int roll)
        {
            int low = Math.Max(AgeLeast, playerAge - AgeGap + AgeMargin), high = Math.Min(AgeMost, playerAge + AgeGap - AgeMargin);
            if (high < low)
            {
                return Math.Max(AgeLeast, Math.Min(AgeMost, playerAge));   // a player older than the cast keeps anyone: the nearest age there is
            }
            return low + (roll & int.MaxValue) % (high - low + 1);
        }
    }
}
