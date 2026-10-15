using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>How the internet stands at the best of the player's homes, with that home's name (null for none).</summary>
    public delegate NetState NetLook(out string home);

    /// <summary>
    /// What the City Ledger asks of the rest of the mod, set by the wiring (Wiring/LedgerLinks). The paper calls no other
    /// feature: it reads the morning's news, the market's close and the economy's figures through these, and each one left
    /// unset is simply not in the paper. Reset when the wiring is taken down.
    /// </summary>
    public static class LedgerBridges
    {
        /// <summary>The day the city's paper (Social/News) was last written for; 0 none.</summary>
        public static Func<int> PaperDay;

        /// <summary>The city's paper as it stands, as pieces sorted into their sections (the old masthead left out).</summary>
        public static Action<List<LedgerPiece>> Paper;

        /// <summary>The host: what another player's game wrote (a guest's piece's values), as this city's players are shown it, by its author's id (Flare's filter). Null: as it came.</summary>
        public static Func<string, string, string> Filter;

        /// <summary>The market at its last close written into an edition (the index, the sectors, every company), with its news from a day on.</summary>
        public static Action<LedgerEdition, int> Market;

        /// <summary>The economy's figures in an edition: the bank rate, inflation, the state of the cycle.</summary>
        public static Action<LedgerEdition> Economy;

        /// <summary>More pieces for a morning: what is on in the street, a spell of weather.</summary>
        public static Action<int, List<LedgerPiece>> Extra;

        /// <summary>
        /// The council's policies: given how they stood at the last edition (null for none yet), a piece for each that has
        /// come in or ended since; returns how they stand now, for the next edition.
        /// </summary>
        public static Func<string, List<LedgerPiece>, string> Council;

        /// <summary>True if a place is within so many metres of another feature's fixture in the street (a bus stop, a help-wanted board, a bike dock).</summary>
        public static Func<Vector3, float, bool> Taken;

        /// <summary>A message from the City Ledger's contact on the phone: a locale key and the names that fill it.</summary>
        public static Action<string, Dictionary<string, string>> Say;

        /// <summary>
        /// How the internet stands at the best of the player's homes (Utilities), and that home's name: what Newsstand's
        /// editions come over. Unset, or NotKept (the utilities are off), a home counts as connected.
        /// </summary>
        public static NetLook Connection;

        /// <summary>Opens the Utilities app on its internet page, where a connection is bought; false if it is not on the phone.</summary>
        public static Func<bool> OpenUtilities;

        /// <summary>The host of a Going Public session: a guest's report for the city's paper (Social/News: its kind, where, its detail, its amount).</summary>
        public static Action<int, float, float, string, int> Report;

        public static void Reset()
        {
            PaperDay = null;
            Paper = null;
            Filter = null;
            Market = null;
            Economy = null;
            Extra = null;
            Council = null;
            Taken = null;
            Say = null;
            Connection = null;
            OpenUtilities = null;
            Report = null;
        }
    }
}
