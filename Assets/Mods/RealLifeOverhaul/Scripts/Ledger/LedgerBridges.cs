using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>How the internet stands at the best of the player's homes, with that home's name (null for none).</summary>
    public delegate NetState NetLook(out string home);

    /// <summary>What the City Ledger asks of the rest of the mod, set by the wiring (Wiring/LedgerLinks); unset, it is not in the paper.</summary>
    public static class LedgerBridges
    {
        /// <summary>The day the city's paper (Social/News) was last written for; 0 none.</summary>
        public static Func<int> PaperDay;

        /// <summary>RLO Texts: an edition was written, so the briefs it can word are asked for now.</summary>
        public static Action<LedgerEdition> TextsEdition;

        /// <summary>RLO Texts: the model's words for a piece's story, or null for its written one.</summary>
        public static Func<LedgerPiece, string> TextsBody;

        public static Action<List<LedgerPiece>> Paper;

        /// <summary>The host: a guest's piece's value as this city's players see it, by its author's id (Flare's filter).</summary>
        public static Func<string, string, string> Filter;

        /// <summary>The market at its last close into an edition, with its news from a day on.</summary>
        public static Action<LedgerEdition, int> Market;

        /// <summary>The economy's figures in an edition: the bank rate, inflation, the state of the cycle.</summary>
        public static Action<LedgerEdition> Economy;

        /// <summary>More pieces for a morning: what is on in the street, a spell of weather.</summary>
        public static Action<int, List<LedgerPiece>> Extra;

        /// <summary>From how the policies stood at the last edition (null: none yet), a piece for each change; returns how they stand.</summary>
        public static Func<string, List<LedgerPiece>, string> Council;

        /// <summary>True if a place is within so many metres of another feature's fixture in the street.</summary>
        public static Func<Vector3, float, bool> Taken;

        /// <summary>A message from the City Ledger's contact on the phone: a locale key and the names that fill it.</summary>
        public static Action<string, Dictionary<string, string>> Say;

        /// <summary>The internet at the best of the player's homes (Utilities); unset, or NotKept, counts as connected.</summary>
        public static NetLook Connection;

        public static Func<bool> OpenUtilities;

        /// <summary>The host of a Going Public session: a guest's report for the city's paper (Social/News).</summary>
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
