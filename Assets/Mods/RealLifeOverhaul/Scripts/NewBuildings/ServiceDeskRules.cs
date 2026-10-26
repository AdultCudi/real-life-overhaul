using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>What a special place's desk is to the dialog its service answers with (<see cref="ServiceDeskRules.Verdict"/>).</summary>
    public enum DeskVerdict
    {
        Fine,          // the service has the dialog the desk's person speaks
        NoDialog,      // the service has no dialog at all: the desk would open the game's panel on nothing
        WrongDialog    // the service has another kind's dialog
    }

    /// <summary>
    /// A special place's desks and what they need of the service copied onto a made building, game-free (EconomyCheck's
    /// UptownDealerChecks). The game seats the player at a special employee's desk (SpecialEmployeeController.Interact), pauses
    /// the game a moment later and opens the dialog of the building's SPECIAL SERVICE, not of the desk: a service with no dialog
    /// (dialogType NotImplemented, as Manhattan Luxury Cars, the clinic and the supermarket carry) leaves the game's panel up on
    /// nothing, the player seated and the game paused. A made building copies a service as the game has it, so the copy is
    /// completed for the desks its layout has: every desk's service has the dialog the desk's person speaks, and a vehicle
    /// store's the settings its dialog reads. The dialog and employee names are the game's enum names, so nothing here names a
    /// game type. COST: nothing per frame; a few string comparisons a load.
    /// </summary>
    public static class ServiceDeskRules
    {
        /// <summary>The start of the names of the game's special employee desks (the small one follows it).</summary>
        public const string DeskItem = "ba:itemname_specialemployeedesk";

        public const string NoDialog = "NotImplemented";       // CallDialogType's first value: GetDialog answers null for it
        public const string StoreEmployee = "VehicleStore";    // SpecialEmployeeController.SpecialEmployeeType
        public const string StoreDialog = "VehicleStoreDialog";
        public const string DoctorEmployee = "LongevityDoctor"; // the one desk whose dialog does not come from the service (always the doctor's)

        // SpecialEmployeeController.SpecialEmployeeType and the CallDialogType its desk answers with, in step.
        private static readonly string[] Employees =
        {
            "Wholesale", "Furniture", "Import", "Bank", "MarketingAgency", "RecruitmentAgency", "InstallationFirm", "MovingCompany",
            "HealthInsurance", StoreEmployee, "PrivateDriverService"
        };

        private static readonly string[] Dialogs =
        {
            "WholesaleStoreManagerDialog", "FurnitureStoreManagerDialog", "ImportManagerDialog", "BankDialog", "MarketingAgencyDialog",
            "RecruitmentAgencyDialog", "InteriorInstallationFirm", "MovingServiceDialog", "HealthInsuranceManagerDialog", StoreDialog,
            "PrivateDriverServiceDialog"
        };

        /// <summary>True for one of the game's special employee desks (an item name of a layout).</summary>
        public static bool IsDesk(string itemName)
        {
            return !string.IsNullOrEmpty(itemName) && itemName.StartsWith(DeskItem, StringComparison.Ordinal);
        }

        /// <summary>The kind of person at a desk, from the desk's custom value (its first piece, as the game reads it); empty for none.</summary>
        public static string EmployeeOf(string customValue)
        {
            if (string.IsNullOrWhiteSpace(customValue))
            {
                return string.Empty;
            }
            int comma = customValue.IndexOf(',');
            return (comma < 0 ? customValue : customValue.Substring(0, comma)).Trim();
        }

        /// <summary>The dialog a kind of desk's person speaks (a CallDialogType name), or null for a kind with none of its own.</summary>
        public static string DialogOf(string employee)
        {
            for (int i = 0; i < Employees.Length; i++)
            {
                if (string.Equals(Employees[i], employee, StringComparison.Ordinal))
                {
                    return Dialogs[i];
                }
            }
            return null;
        }

        /// <summary>True when the desk's dialog reads a vehicle store's settings (the delivery fee, the hours) off its service.</summary>
        public static bool NeedsStore(string employee)
        {
            return string.Equals(employee, StoreEmployee, StringComparison.Ordinal);
        }

        /// <summary>
        /// What a desk makes of the dialog its service carries (a CallDialogType name, empty for none). The doctor's desk is the
        /// game's own doctor whatever the service; a desk with no kind (an empty custom value) speaks whatever dialog there is.
        /// </summary>
        public static DeskVerdict Verdict(string employee, string dialog)
        {
            if (string.Equals(employee, DoctorEmployee, StringComparison.Ordinal))
            {
                return DeskVerdict.Fine;
            }
            if (string.IsNullOrEmpty(dialog) || string.Equals(dialog, NoDialog, StringComparison.Ordinal))
            {
                return DeskVerdict.NoDialog;
            }
            string wanted = DialogOf(employee);
            return wanted == null || string.Equals(wanted, dialog, StringComparison.Ordinal) ? DeskVerdict.Fine : DeskVerdict.WrongDialog;
        }

        /// <summary>
        /// The dialog a service should carry for a desk, or null when it is right already (or the desk gives no kind to read one
        /// from): only a service with NO dialog is completed, as another kind's dialog is the game's own choice for that place.
        /// </summary>
        public static string Complete(string employee, string dialog)
        {
            return Verdict(employee, dialog) == DeskVerdict.NoDialog ? DialogOf(employee) : null;
        }

        /// <summary>The words of a verdict, for the census (English, Player.log).</summary>
        public static string Why(string employee, string dialog, DeskVerdict verdict)
        {
            string kind = string.IsNullOrEmpty(employee) ? "a desk" : "a " + employee + " desk";
            return verdict == DeskVerdict.NoDialog ? "its layout has " + kind + " but its service has no dialog for it"
                : verdict == DeskVerdict.WrongDialog ? "its layout has " + kind + " but its service answers with " + dialog + " (wanted " + DialogOf(employee) + ")"
                : null;
        }
    }

    /// <summary>
    /// A dealership's cars for its purchase panel, game-free. The game's vehicle store panel lists a dealership's cars from a
    /// list kept by contact or by address (Services.ContractItemsForSaleService), or else from the showcases of its registration's
    /// layout; the mod gives a dealership of its own the first, so the panel never depends on a lookup of the layout from the
    /// registration. COST: nothing per frame; a pass over a layout's items a load.
    /// </summary>
    public static class StockRules
    {
        /// <summary>
        /// The names in order, each once, that <paramref name="known"/> accepts (a vehicle type the game has), into
        /// <paramref name="into"/> (not cleared). How many were added.
        /// </summary>
        public static int Distinct(IList<string> raw, Func<string, bool> known, List<string> into)
        {
            int added = 0;
            for (int i = 0; raw != null && into != null && i < raw.Count; i++)
            {
                string name = raw[i];
                if (string.IsNullOrEmpty(name) || into.Contains(name) || (known != null && !known(name)))
                {
                    continue;
                }
                into.Add(name);
                added++;
            }
            return added;
        }

        /// <summary>True when a panel's reading is a real one: at least one car, and a price above nothing for the cheapest.</summary>
        public static bool Real(int cars, float cheapest)
        {
            return cars > 0 && cheapest > 0f;
        }
    }

    /// <summary>
    /// The look at a desk's dialog gone wrong, game-free. The game opens its dialog panel before it makes the dialog and pauses
    /// a second after the desk sits the player down; a panel up on no dialog never closes of itself, and the pause with it. Asked
    /// once a second while the player is in one of the mod's buildings: the panel up on no dialog for <see cref="Grace"/> seconds
    /// is closed the game's own way (DialogUI.FinishDialog stands the player up, takes the pause off and lifts the hold), and a
    /// player seated with nothing holding them for <see cref="SeatedLooks"/> looks in a row is stood up (Character.Reset).
    /// COST: nothing per frame; two field reads a second.
    /// </summary>
    public static class DeskWatchRules
    {
        public const float Grace = 2f;       // s a panel may stand open with no dialog before it is closed (the game's pause starts a second after the desk sits the player)
        public const int SeatedLooks = 3;    // looks, a second apart, a seated player may be held by nothing before they are stood up

        /// <summary>
        /// True when the panel has stood open on no dialog for the grace. <paramref name="since"/> is the time it was first seen
        /// so, kept by the caller (negative for not seen).
        /// </summary>
        public static bool Broken(bool panelOpen, bool hasDialog, ref float since, float now, float grace)
        {
            if (!panelOpen || hasDialog)
            {
                since = -1f;
                return false;
            }
            if (since < 0f)
            {
                since = now;
            }
            return now - since >= grace;
        }

        /// <summary>
        /// True when a seated player has been held by nothing (no navigation hold, no panel) for enough looks in a row.
        /// <paramref name="looks"/> is the count so far, kept by the caller.
        /// </summary>
        public static bool Stuck(bool seated, bool held, ref int looks, int needed)
        {
            if (!seated || held)
            {
                looks = 0;
                return false;
            }
            looks++;
            return looks >= needed;
        }
    }
}
