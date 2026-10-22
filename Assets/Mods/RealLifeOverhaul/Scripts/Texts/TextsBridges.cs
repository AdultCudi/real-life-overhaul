using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// What RLO Texts asks of the rest of the mod for the city's new talk of data v6 and for what people remember, set by
    /// Wiring/Wiring.TextsCity and Wiring/Wiring.TextsMemory; one not set is not asked. Each reads what its feature keeps,
    /// or sends through its feature's own way.
    /// </summary>
    public static class TextsBridges
    {
        /// <summary>Fills the list with the player's own staff as running a business has them; the game's own facts are TextsGame's to add.</summary>
        public static Action<List<TextStaff>> Staff;

        /// <summary>Fills the view of the player's life as its people could hear of it, every field set.</summary>
        public static Action<TextLife> Life;

        /// <summary>Fills the list with the people of the player's social life, as gossip and the life's texts read them.</summary>
        public static Action<List<TextPerson>> People;

        /// <summary>
        /// Sends a text from a person of the social life (their place, the written line's key, its values) through the social
        /// life's own texts, its daily cap and quiet hours included; true if sent.
        /// </summary>
        public static Func<int, string, Dictionary<string, string>, bool> PersonSay;

        /// <summary>The least sum a day's move of the net worth must be to count as a big win or loss, at today's prices.</summary>
        public static Func<long> BigFloor;

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
