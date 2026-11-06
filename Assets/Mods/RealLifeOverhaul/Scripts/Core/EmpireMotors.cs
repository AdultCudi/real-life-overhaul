using System;
using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Living with Empire Motors (Workshop 3787690782), the mod whose car dealership is a business type of its own, beside
    /// this mod's (Economy/Dealer). Its stock are the player's vehicles to the game, parked at the dealership and listed
    /// by id in its save text; its money moves under transaction types the game's statement does not count
    /// (Core/EmpireMotorsLines). It is known by its assembly (<see cref="OtherMods"/>), never referenced; nothing of it
    /// is read by reflection here. See RealLifeOverhaul.Build/docs/other-mods.md, "Empire Motors".
    /// COST: nothing per frame; <see cref="Stock"/> is a field read while it is not loaded, else a dictionary look-up
    /// and one search of its save text per vehicle while that text stays the same.
    /// </summary>
    public static class EmpireMotors
    {
        public const string DealershipType = "empiremotors:businesstype_cardealership";
        private const string SaveKey = "empiremotors.save";   // its own records, vehicle ids among them (EmpireMotorsSaveCodec)

        private static string _saved;                                                        // the text the answers below are for
        private static readonly Dictionary<string, bool> Held = new Dictionary<string, bool>(32);   // vehicle id: in its records
        private static readonly List<Address> Scratch = new List<Address>(4);

        /// <summary>True once Empire Motors has been seen among the loaded assemblies. A field read.</summary>
        public static bool On => OtherMods.EmpireMotors.Loaded;

        public static bool IsDealership(BuildingRegistration reg)
        {
            return reg != null && reg.businessTypeName == DealershipType;
        }

        /// <summary>
        /// True for a car of its stock: parked at one of its dealerships, or in its records (driven off the lot). False
        /// while it is not loaded: a save it has left keeps the cars as the player's own. Never per frame.
        /// </summary>
        public static bool Stock(VehicleInstance vehicle)
        {
            if (!On || vehicle == null || string.IsNullOrEmpty(vehicle.id))
            {
                return false;
            }
            try
            {
                if (!string.IsNullOrEmpty(vehicle.streetName) && IsDealership(BuildingHelper.GetBuildingRegistration(vehicle.Address)))
                {
                    return true;
                }
                GameInstance save = SaveGameManager.Current;
                string saved = null;
                if (save == null || save.modData == null || !save.modData.TryGetValue(SaveKey, out saved) || string.IsNullOrEmpty(saved))
                {
                    return false;
                }
                if (!ReferenceEquals(saved, _saved))
                {
                    _saved = saved;   // it writes the whole text again on every change
                    Held.Clear();
                }
                if (!Held.TryGetValue(vehicle.id, out bool held))
                {
                    held = EmpireMotorsLines.Holds(saved, vehicle.id);
                    Held[vehicle.id] = held;
                }
                return held;
            }
            catch (Exception)
            {
                return false;   // a vehicle that cannot be read is the player's, as it is without Empire Motors
            }
        }

        /// <summary>Fills the list with the addresses of the player's own dealerships, other Going Public players' left out.</summary>
        public static void Dealerships(List<Address> into)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = On && save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (IsDealership(reg) && GoingPublic.Mine(reg) && reg.HasEstablishedBusiness && reg.HasValidAddress && GameRegistrations.Found(reg))
                {
                    into.Add(reg.Address);
                }
            }
        }

        /// <summary>How many dealerships the player runs. Not per frame.</summary>
        public static int DealershipCount()
        {
            Dealerships(Scratch);
            int count = Scratch.Count;
            Scratch.Clear();
            return count;
        }

        /// <summary>
        /// What a transaction is to the player's dealerships: a sale with an address counts only at a dealership's
        /// address. None while Empire Motors is not loaded.
        /// </summary>
        public static DealerLine LineOf(Transaction t, int dealerships)
        {
            if (!On || t == null || dealerships <= 0)
            {
                return DealerLine.None;
            }
            bool here = !ReferenceEquals(t.address, null);
            DealerLine line = EmpireMotorsLines.Of(t.transactionType, here, t.transactionData != null ? t.transactionData.Count : 0);
            if (line == DealerLine.Takings && here && t.transactionType == EmpireMotorsLines.SoldType && !IsDealership(BuildingHelper.GetBuildingRegistration(t.address)))
            {
                return DealerLine.None;   // something of the player's sold at another business of theirs
            }
            return line;
        }

        /// <summary>The text key saying what Empire Motors does on one of this mod's keys, for the options page; null for nothing.</summary>
        public static string KeyUse(KeyCode key, KeyModifier modifier)
        {
            if (!On)
            {
                return null;
            }
            switch (key)
            {
                case KeyCode.V:
                    return "reallife_keys_empire_view";   // with any modifier; kept off while this key is V (Camera/EmpireView)
                case KeyCode.F7:
                    return modifier == KeyModifier.Shift ? null : "reallife_keys_empire_tools";   // its F7 waits for Shift to be up
                case KeyCode.F6:
                case KeyCode.F9:
                case KeyCode.F12:
                    return "reallife_keys_empire_tools";
                default:
                    return null;
            }
        }
    }
}
