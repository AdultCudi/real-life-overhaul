using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// What RLO Texts asks of the rest of the mod for the city's new talk of data v6 and for what people remember, filled by
    /// Wiring/Wiring.TextsCity and Wiring/Wiring.TextsMemory; each is null while nobody fills it in, and is then not asked (nothing
    /// is said, nothing is remembered from there). Nothing here decides anything: each reads what its feature keeps, or
    /// sends through its feature's own way.
    /// </summary>
    public static class TextsBridges
    {
        /// <summary>The player's own staff as running a business has them (into the list, cleared first); the game's own facts of each are TextsGame's to add.</summary>
        public static Action<List<TextStaff>> Staff;

        /// <summary>The player's life as its people could hear of it (into the view, every field set).</summary>
        public static Action<TextLife> Life;

        /// <summary>The people of the player's social life (into the list, cleared first), as gossip and the life's texts read them.</summary>
        public static Action<List<TextPerson>> People;

        /// <summary>
        /// A text from a person of the social life (their place in it, the written line's key, its values) through the social
        /// life's own texts (its daily cap and quiet hours, and RLO Texts' words through the phone's hold); true if sent.
        /// </summary>
        public static Func<int, string, Dictionary<string, string>, bool> PersonSay;

        /// <summary>The least sum a day's move of the net worth must be to count as a big win or loss, at today's prices.</summary>
        public static Func<long> BigFloor;

        /// <summary>Every bridge back to nobody.</summary>
        public static void Reset()
        {
            Staff = null;
            Life = null;
            People = null;
            PersonSay = null;
            BigFloor = null;
        }
    }
}
