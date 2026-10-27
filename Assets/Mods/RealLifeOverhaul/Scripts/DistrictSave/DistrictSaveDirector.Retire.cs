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
    /// frame; one pass over what is kept at that load. A feature may also retire single addresses for a save (<see cref="RetireAddresses"/>:
    /// Uptown's rebalance, which builds some of its stores as offices on the same lots).
    /// </summary>
    public static partial class DistrictSaveDirector
    {
        private static object _retireFor;     // the save whose kept records are retired at its next pass
        private static string _retirePrefix;  // the start of every street id retired
        private static string _retireWhy;
        private static object _addressesFor;                                                   // the save whose kept records of single addresses are retired at its next pass
        private static readonly List<KeyValuePair<string, int>> RetireList = new List<KeyValuePair<string, int>>(32);
        private static string _addressesWhy;

        /// <summary>
        /// Retires, for this save alone, every record it keeps that names one of the addresses (street of the mod's own, house number):
        /// buildings of a plan that stand there no more, whose businesses would not fit what stands now. Called by the feature before
        /// its records go back, as <see cref="Retire"/>; the game's own streets are never retired. How many addresses are queued.
        /// </summary>
        public static int RetireAddresses(object save, IList<KeyValuePair<string, int>> addresses, string why)
        {
            if (save == null || addresses == null)
            {
                return 0;
            }
            if (!ReferenceEquals(save, _addressesFor))
            {
                RetireList.Clear();
            }
            _addressesFor = save;
            _addressesWhy = why;
            for (int i = 0; i < addresses.Count; i++)
            {
                if (DistrictSaveRules.IsModId(addresses[i].Key) && !RetireList.Contains(addresses[i]))
                {
                    RetireList.Add(addresses[i]);
                }
            }
            return RetireList.Count;
        }

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
            if (RetireList.Count > 0 && ReferenceEquals(save, _addressesFor))
            {
                int addressed = 0;
                for (int i = 0; i < RetireList.Count; i++)
                {
                    addressed += DistrictSaveStore.RetireAddress(KeptRecords, KeptItems, RetireList[i].Key, RetireList[i].Value);
                }
                Logger?.Info("District save: " + addressed + " record(s) kept for " + RetireList.Count + " address(es) let go (" + _addressesWhy + "); " + KeptRecords.Count + " kept still.");
                RetireList.Clear();
                _addressesFor = null;
                _addressesWhy = null;
            }
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
