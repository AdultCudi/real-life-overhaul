using System;
using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Living with Empire Motors (Steam Workshop 3787690782), the mod that lets the player run a car dealership of its own
    /// business type. This mod builds no dealership: it lets that one be a business of the player's like any other and
    /// keeps its own hands off what is the dealership's. See RealLifeOverhaul.Build/docs/other-mods.md, "Empire Motors".
    ///
    /// What matters here, read in its decompiled code (it makes no Harmony patch: its one patch method is never applied):
    ///  - its dealership is a business type of its own, "empiremotors:businesstype_cardealership", in a retail building,
    ///    tagged as one that takes money and lets the player start it. The game sends it no customers: its own scheduler
    ///    sells the cars to customers it makes up, by the staff on shift at the counter;
    ///  - every car of its stock is a vehicle of the player's to the game (GameInstance.VehicleInstances), parked at the
    ///    dealership's address (VehicleInstance.SetStreetData), and it keeps its own record of each, by the vehicle's id,
    ///    in the save under "empiremotors.save". A car sold is taken out of the save;
    ///  - its money moves through the game's own GameManager.ChangeMoneySafe, but under types the game's statement does
    ///    not count (Core/EmpireMotorsLines): to the game, and so to this mod's books, a dealership took nothing;
    ///  - while the player drives, V switches to a view from the driver's seat that writes the camera's place itself,
    ///    and F6, F7, F9 and F12 are tools of its own for the showroom's cars, always on.
    ///
    /// How it is known, without a reference to it: by its assembly and a type of its own (<see cref="OtherMods"/>).
    /// Nothing of it is called or read by reflection: its dealership is known by the game's own record of the business
    /// type, its stock by the address and by the vehicle's id in its saved text.
    ///
    /// The cost: nothing per frame. <see cref="Stock"/> is a field read while it is not switched on; then a dictionary
    /// lookup of the game's, and once per vehicle while its saved text stays the same, one search of that text.
    /// </summary>
    public static class EmpireMotors
    {
        public const string DealershipType = "empiremotors:businesstype_cardealership";
        private const string SaveKey = "empiremotors.save";   // its own records, by vehicle id among the rest (EmpireMotorsSaveCodec)

        private static string _saved;                                                        // the saved text the answers below are for
        private static readonly Dictionary<string, bool> Held = new Dictionary<string, bool>(32);   // vehicle id: in its records
        private static readonly List<Address> Scratch = new List<Address>(4);
        private static bool _toldKey;   // the camera key's notice has been shown in this run of the game

        /// <summary>True once Empire Motors has been seen among the loaded assemblies. A field read.</summary>
        public static bool On => OtherMods.EmpireMotors.Loaded;

        /// <summary>True for a business of Empire Motors' dealership type.</summary>
        public static bool IsDealership(BuildingRegistration reg)
        {
            return reg != null && reg.businessTypeName == DealershipType;
        }

        /// <summary>
        /// True for a car of a dealership's stock: parked at one of its dealerships, or among its records (a car of the
        /// stock that has been driven off the lot). It is the dealership's to sell, not the player's to run: this mod keeps
        /// no record of it and offers nothing for it. False for everything while Empire Motors is not switched on (a save
        /// it has left keeps its cars as the player's own). Never per frame.
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
                    _saved = saved;   // it writes the whole text again on every change: the answers are for the text they were found in
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

        /// <summary>Puts the addresses of the player's own dealerships into the list (Going Public's other players' left out).</summary>
        public static void Dealerships(List<Address> into)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = On && save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (IsDealership(reg) && GoingPublic.Mine(reg) && reg.HasEstablishedBusiness && reg.HasValidAddress)
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
        /// What a transaction is to the player's dealerships (Core/EmpireMotorsLines). A sold item with an address is the
        /// takings of a dealership only at a dealership's address; one without is Empire Motors' only when the player runs
        /// a dealership. None while Empire Motors is not switched on.
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

        /// <summary>
        /// True while Empire Motors has the camera key: it is V with no modifier, as Empire Motors' view from the driver's
        /// seat is, and the player is driving. The first time in a run of the game it says so. Only when the key went down.
        /// </summary>
        public static bool TakesCameraKey(GameManager gm)
        {
            if (!On || Keys.Key(KeyAction.CameraToggle) != KeyCode.V || Keys.Modifier(KeyAction.CameraToggle) != KeyModifier.None)
            {
                return false;
            }
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            if (vehicle == null || !vehicle.controlledByPlayer)
            {
                return false;
            }
            if (!_toldKey)
            {
                _toldKey = true;
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_empire_camera_key", null, 10f);
            }
            return true;
        }

        /// <summary>The text key that says what Empire Motors does on a key of this mod's, for its row on the options page; null for nothing.</summary>
        public static string KeyUse(KeyCode key, KeyModifier modifier)
        {
            if (!On)
            {
                return null;
            }
            switch (key)
            {
                case KeyCode.V:
                    return modifier == KeyModifier.None ? "reallife_keys_empire_view" : null;
                case KeyCode.F7:
                    return modifier == KeyModifier.Shift ? null : "reallife_keys_empire_tools";   // (its F7 waits for Shift to be up)
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
