using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// What an earlier version kept for places whose plan has changed since, let go before anything of it goes back: Uptown's
    /// lots were planned again (the owner's "Like Manhattan", 24 Oct 2026), so a save made with the first plan holds
    /// businesses at addresses that are now other buildings, of other sizes, whose interiors theirs would not fit. A feature
    /// that knows its save is of an earlier plan names the streets of its own whose records are retired, for that save
    /// alone; at the next pass over what is kept (the main menu's unload, or the city load for a load that did not pass
    /// it) every kept record naming one of them is let go, and so is never written again. A stand-in of such a place keeps
    /// the stand-in the save holds (a contact with no address, the player where the save put them). COST: nothing per
    /// frame; one pass over what is kept at that load.
    /// </summary>
    public static partial class DistrictSaveDirector
    {
        private static object _retireFor;     // the save whose kept records are retired at its next pass
        private static string _retirePrefix;  // the start of every street id retired
        private static string _retireWhy;

        /// <summary>
        /// Retires, for this save alone, every record it keeps that names a street whose id starts with
        /// <paramref name="streetPrefix"/>: a feature's places of an earlier plan. Called by the feature before its records go
        /// back (its early step, or its city step before the district save's).
        /// </summary>
        public static void Retire(object save, string streetPrefix, string why)
        {
            if (save == null || string.IsNullOrEmpty(streetPrefix) || !DistrictSaveRules.IsModId(streetPrefix))
            {
                return;   // the game's own streets are never retired
            }
            _retireFor = save;
            _retirePrefix = streetPrefix;
            _retireWhy = why;
        }

        // Before a pass: what is kept for the retired streets of this save let go, with one line.
        private static void Retired(object save)
        {
            if (_retirePrefix == null || !ReferenceEquals(save, _retireFor))
            {
                return;
            }
            int gone = DistrictSaveStore.Retire(KeptRecords, KeptItems, _retirePrefix);
            Logger?.Info("District save: " + gone + " record(s) kept for " + _retirePrefix + "* let go (" + _retireWhy + "); " + KeptRecords.Count + " kept still.");
            _retirePrefix = null;
            _retireFor = null;
            _retireWhy = null;
        }
    }
}
