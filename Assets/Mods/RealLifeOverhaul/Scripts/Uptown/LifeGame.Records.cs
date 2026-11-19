using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    internal static partial class LifeGame
    {
        /// <summary>
        /// The records of the declared buildings that get one before the city loads put in the game's lookups by address (MadeGame.RegisterEarly): how many were
        /// made, and in <paramref name="found"/> how many the game's own look-ups then give back. The make claims them behind the loading screen.
        /// </summary>
        public static int RecordsEarly(IList<MadeSpec> specs, out int found) => MadeGame.RegisterEarly(specs, out found);

        /// <summary>The records no make claimed taken back (the district stood down, the director never started).</summary>
        public static int RecordsBack(string why) => MadeGame.DropEarly(why);
    }
}
