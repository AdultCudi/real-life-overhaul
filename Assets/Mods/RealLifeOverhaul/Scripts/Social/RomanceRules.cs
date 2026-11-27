using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Who the player can fall for: one rule for the people of the social life, the strangers street talk stops and the faces Flare
    /// shows. They are of the sex the player is interested in (the option "Who you are interested in": anyone, women or men) and
    /// inside the player's age window: at most <see cref="Younger"/> years under the player's age and at most <see cref="Older"/>
    /// over it (the two options "Partners younger than you, at most" and "Partners older than you, at most"). Each is
    /// <see cref="AgeGap"/> years until the player moves it: the gap the city's own couples keep when they pair up
    /// (Population/LifeRules.AgeGapMost), which the city's couples keep whatever the options say. Game-free. COST: arithmetic, on a
    /// press or when a page is drawn.
    /// </summary>
    public static class RomanceRules
    {
        public const int Anyone = 0, Women = 1, Men = 2;   // what the player is interested in, as the option saves it
        public const int AgeLeast = 18;                    // years: nobody younger is ever somebody to fall for
        public const int AgeMost = 85;                     // years: the oldest anybody is kept (the cast, the faces, the People page)

        // Years either side of the player's age before the player moves it. It is the city's own: its people pair up within it
        // (Population/LifeRules.PairAll), so the player is held to what the city's couples keep.
        public const int AgeGap = RealLifeOverhaul.Population.LifeRules.AgeGapMost;

        public const int WindowLeast = 2;    // years: the narrowest either side of the window can be set (the options' range)
        public const int WindowMost = 25;    // ...and the widest

        // Years inside the window that somebody is made, never on its edge. Their birthday and the player's fall on different days, so for
        // the weeks between them the two are a year further apart (or nearer) than the rest of the year: somebody made exactly on an edge
        // would be out of the window for part of every year, and Ask them out and the People line would go and come back with it.
        public const int AgeMargin = 1;

        /// <summary>Years under the player's own age that somebody may be: the option, <see cref="AgeGap"/> unless it was moved. Set through <see cref="Window"/>.</summary>
        public static int Younger { get; private set; } = AgeGap;

        /// <summary>Years over the player's own age that somebody may be: the option, <see cref="AgeGap"/> unless it was moved. Set through <see cref="Window"/>.</summary>
        public static int Older { get; private set; } = AgeGap;

        /// <summary>
        /// The window the options give: each side kept between <see cref="WindowLeast"/> and <see cref="WindowMost"/>, so a saved value
        /// from nowhere is never a window that rules everybody out. Called by the social life before it asks the rules (SocialLife.ApplyWindow).
        /// </summary>
        public static void Window(int younger, int older)
        {
            Younger = Math.Max(WindowLeast, Math.Min(WindowMost, younger));
            Older = Math.Max(WindowLeast, Math.Min(WindowMost, older));
        }

        /// <summary>Whether somebody of this sex is one the player is interested in. A value that is neither women nor men is anyone.</summary>
        public static bool SexFits(int interest, bool female)
        {
            return interest == Women ? female : interest == Men ? !female : true;
        }

        /// <summary>
        /// Whether an age is inside the player's window: an adult at most <see cref="Younger"/> years under and <see cref="Older"/> over.
        /// A player's age not known (0 or less) rules nobody out for it. This is the verdict on somebody as they are today; who is made
        /// for a player is kept further inside it (<see cref="AgeSteady"/>).
        /// </summary>
        public static bool AgeFits(int age, int playerAge)
        {
            return AgeFits(age, playerAge, Younger, Older);
        }

        /// <summary>As <see cref="AgeFits(int, int)"/> for the window given instead of the options'.</summary>
        public static bool AgeFits(int age, int playerAge, int younger, int older)
        {
            return playerAge <= 0 || (age >= AgeLeast && age >= playerAge - younger && age <= playerAge + older);
        }

        /// <summary>
        /// Whether an age stays inside the player's window through every birthday: an adult at most <see cref="Younger"/> less
        /// <see cref="AgeMargin"/> years under and <see cref="Older"/> less <see cref="AgeMargin"/> over, so the year more or less
        /// between the two in the weeks between their birthdays still fits. Whoever is made for a player is kept to it
        /// (<see cref="AgeNear"/>). A player's age not known rules nobody out.
        /// </summary>
        public static bool AgeSteady(int age, int playerAge)
        {
            return AgeSteady(age, playerAge, Younger, Older);
        }

        /// <summary>As <see cref="AgeSteady(int, int)"/> for the window given instead of the options'.</summary>
        public static bool AgeSteady(int age, int playerAge, int younger, int older)
        {
            return playerAge <= 0 || (age >= AgeLeast && age >= playerAge - younger + AgeMargin && age <= playerAge + older - AgeMargin);
        }

        /// <summary>Whether somebody of this sex and age is somebody the player could fall for.</summary>
        public static bool Fits(int interest, bool female, int age, int playerAge)
        {
            return SexFits(interest, female) && AgeFits(age, playerAge);
        }

        /// <summary>
        /// An age for somebody made to fit the player: any whole age from <see cref="Younger"/> less <see cref="AgeMargin"/> under
        /// theirs to <see cref="Older"/> less <see cref="AgeMargin"/> over, kept between <see cref="AgeLeast"/> and <see cref="AgeMost"/>
        /// (the margin is taken from the window's edge, not from eighteen: for a player under twenty-six the youngest is eighteen).
        /// Never on the window's edge, so the person keeps fitting through both birthdays of every year (<see cref="AgeSteady"/>).
        /// <paramref name="roll"/> is any number; the same gives the same age.
        /// </summary>
        public static int AgeNear(int playerAge, int roll)
        {
            return AgeNear(playerAge, roll, Younger, Older);
        }

        /// <summary>As <see cref="AgeNear(int, int)"/> for the window given instead of the options'.</summary>
        public static int AgeNear(int playerAge, int roll, int younger, int older)
        {
            int low = Math.Max(AgeLeast, playerAge - younger + AgeMargin), high = Math.Min(AgeMost, playerAge + older - AgeMargin);
            if (high < low)
            {
                return Math.Max(AgeLeast, Math.Min(AgeMost, playerAge));   // a player older than the cast keeps anyone: the nearest age there is
            }
            return low + (roll & int.MaxValue) % (high - low + 1);
        }
    }
}
