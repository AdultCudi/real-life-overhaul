using Localizor;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The game's words for the rivals seen around the city: its language's texts, its cars' and districts' names, handed to the game-free line builder
    /// (<see cref="SeenLines"/>). Every other text is a locale key written whole where it is used.
    /// </summary>
    public static class SeenText
    {
        /// <summary>The game's words, as the line builder and the pages ask for them.</summary>
        public static readonly ISeenWords Words = new GameWords();

        private sealed class GameWords : ISeenWords
        {
            public string T(string key) => key.GetLocalization();

            public string Car(string type) => SeenGame.CarName(type);

            public string District(string key) => SeenGame.DistrictName(key);
        }

        public static string T(string key) => key.GetLocalization();

        /// <summary>A text with up to two {names} filled in.</summary>
        public static string Fill(string key, string name, string value, string name2 = null, string value2 = null) => SeenLines.Fill(T(key), name, value, name2, value2);

        /// <summary>A sighting as words, for Flare, the paper and the notices; empty for one with no line.</summary>
        public static string Line(SeenNote n) => SeenLines.Line(n, Words);

        public static string Spot(SeenSpot spot) => SeenLines.Spot(spot, Words);
    }
}
