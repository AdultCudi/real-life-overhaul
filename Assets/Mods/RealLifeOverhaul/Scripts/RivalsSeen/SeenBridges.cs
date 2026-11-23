using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// What a rival has on the market with the player: a business they put up for sale (they are the seller) or one they will take if the player does not (they are the buyer).
    /// </summary>
    public sealed class SeenOffer
    {
        public int Id;                  // the sale's number in Services
        public string Business = string.Empty, Other = string.Empty;   // the business's name, and the other rival of the sale (the buyer for a seller, the seller for a buyer)
        public string Address = string.Empty;   // where it is, in words
        public int Price, Deadline;     // what is asked, and the last day the player may buy it
        public bool Selling;            // the rival puts it up; false: they would take it over
    }

    /// <summary>
    /// What the rivals seen around the city ask of the rest of the mod, set by Wiring/Wiring.SeenLinks: the rivals' own feature and the tycoons of the rich list as
    /// their file has them, the playthrough's own id, the City Ledger's account on Flare, and what a rival has with the player in Services, Schemes and the rivals' own page. A
    /// bridge left unset gives nothing: no tycoons, no post, nothing said of a sale.
    /// </summary>
    public static class SeenBridges
    {
        /// <summary>
        /// The rivals' own feature now: <see cref="SeenRules.RivalsWait"/> before it has started for this save, <see cref="SeenRules.RivalsOff"/> when it gives no
        /// tycoons (switched off, a fault, not made), <see cref="SeenRules.RivalsOn"/> while it runs. Default: off, so nothing waits for tycoons who cannot come.
        /// </summary>
        public static Func<int> Rivals;

        /// <summary>The rich list's tycoons who have come forward, each as their file has them today (Rivals). Default: none.</summary>
        public static Action<List<SeenSubject>> Tycoons;

        /// <summary>Whether the tycoon at a place of the rich list is still there (come forward, not taken over), asked as the hour turns. Default: yes.</summary>
        public static Func<int, bool> TycoonHere;

        /// <summary>The playthrough's own id, made once with the save (the game's seed of it), for a new file's seed. Default: none.</summary>
        public static Func<string> Playthrough;

        /// <summary>A line on the City Ledger's Flare account at an hour and minute of today. True if it went up. Default: nothing is posted.</summary>
        public static Func<string, int, int, bool> Post;

        /// <summary>A business of a rival's that is up for sale to the player, or one the rival is about to take over (Services), for the rival by the game's own id; null for none.
        /// Default: none.</summary>
        public static Func<string, SeenOffer> Offer;

        /// <summary>Show on map for the sale with this number (Services). Default: nothing.</summary>
        public static Action<int> ShowOffer;

        /// <summary>Whether the fund on offer to the player now is this tycoon's (by their place on the rich list), and stands (Schemes). Default: no.</summary>
        public static Func<int, bool> Fund;

        /// <summary>Opens the fund on offer as its own page (Schemes). Default: nothing.</summary>
        public static Action ShowFund;

        /// <summary>What a tycoon has announced against the player, as a line, or empty for nothing (Rivals). Default: none.</summary>
        public static Func<int, string> Move;

        public static void Reset()
        {
            Offer = null;
            ShowOffer = null;
            Fund = null;
            ShowFund = null;
            Move = null;
            Rivals = null;
            Tycoons = null;
            TycoonHere = null;
            Playthrough = null;
            Post = null;
        }
    }
}
