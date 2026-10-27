using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Something the player owes that is not credit: a fine, a court debt. Shown in the Finance app, counted in "owed in all".</summary>
    public struct DebtLine
    {
        public string NameKey;  // a locale key
        public int Owed;
        public int PerDay;      // taken each day; 0 when it is due in one sum
        public int LateDays;    // days running on which it could not be taken
    }

    /// <summary>What the economy asks of the rest of the mod, each with a default so it works alone; Wiring/EconomyLinks and Wiring/DebtLinks fill them in.</summary>
    public static class EconomyBridges
    {
        /// <summary>The legal and public record. Default: spotless.</summary>
        public static Func<LegalStanding> Legal;

        /// <summary>Whether a transaction of the mod's pays back a game charge the player did not owe, for the city's cost of living.</summary>
        public static Func<string, bool> GivenBack;

        /// <summary>Whether another feature's transaction is a business's takings, for the game's statement (<see cref="GameBooks"/>). Default: none.</summary>
        public static Func<string, bool> Takings;

        /// <summary>The text key of the row among a business's goods another feature's transaction goes in (the broadband network); null for none. Default: none.</summary>
        public static Func<string, string> CostRow;

        /// <summary>False while the record cannot be read yet (its feature has not loaded this save). Default: ready.</summary>
        public static Func<bool> LegalReady;

        /// <summary>Adds the fines and court debts the mod keeps to the list. Default: none.</summary>
        public static Action<List<DebtLine>> OtherDebts;

        /// <summary>Adds the credit accounts other features keep (the student loan) to the list: registered on the rating each time the credit side starts. Default: none.</summary>
        public static Action<List<ICreditAccount>> OtherAccounts;

        /// <summary>A lender files a civil claim for this much. Returns the court's number of the claim, or 0 with no court to file with (the debt stays owed). Default: no court.</summary>
        public static Func<int, int> FileClaim;

        /// <summary>True while a claim with this number is still before the court. Default: no.</summary>
        public static Func<int, bool> ClaimOpen;

        /// <summary>A claim is withdrawn (the debt behind it has been written off). Default: nothing.</summary>
        public static Action<int> WithdrawClaim;

        /// <summary>True while something has hold of the player that the private lender keeps away from: custody, prison, hospital, a trip, a scene, a panel. Default: never.</summary>
        public static Func<bool> Held;

        /// <summary>The player is set upon in the street: a mugging's injury (hours hurt, energy lost), with the notification named. False if it cannot be done now. Default: cannot.</summary>
        public static Func<int, float, string, bool> Assault;

        /// <summary>True if the player could see a thing at this point: it is near and the camera looks its way. Default: yes, so nothing is taken away in sight.</summary>
        public static Func<Vector3, bool> InView;

        /// <summary>A free stretch of kerb near a point, for the test tools' car. Default: none.</summary>
        public static SpotFinder KerbSpot;

        /// <summary>Opens the Economy app on a tab (0 the Overview, 1 Prices and rates); false while the app is not on the phone. Default: no app.</summary>
        public static Func<int, bool> OpenEconomyApp;

        /// <summary>True while the Economy app is on the phone, so the Finance app's button to it shows. Default: no.</summary>
        public static Func<bool> EconomyAppOn;

        /// <summary>The Economy app's one-word verdict on the economy, in the game's language; null while it has none. Default: none.</summary>
        public static Func<string> EconomyVerdict;

        /// <summary>Makes the picture of a phone contact with this id, in this colour. Default: the game's blank picture.</summary>
        public static Action<string, Color> ContactIcon;

        public delegate bool SpotFinder(Vector3 near, out Vector3 spot, out Quaternion heading);

        /// <summary>The player's standing (Scripts/Fame) as a page of the Finance app, opened from the Overview with a button. Default: no page and no button.</summary>
        public static StandingPage Standing;

        /// <summary>The rival tycoons and the city's rich list (Scripts/Rivals) as a page of the Finance app. Default: no page and no button.</summary>
        public static StandingPage Rivals;

        public static void Reset()
        {
            Standing = null;
            Rivals = null;
            Legal = null;
            GivenBack = null;
            Takings = null;
            CostRow = null;
            LegalReady = null;
            OtherDebts = null;
            OtherAccounts = null;
            FileClaim = null;
            ClaimOpen = null;
            WithdrawClaim = null;
            Held = null;
            Assault = null;
            InView = null;
            KerbSpot = null;
            OpenEconomyApp = null;
            EconomyAppOn = null;
            EconomyVerdict = null;
            ContactIcon = null;
        }
    }

    /// <summary>
    /// A page another feature writes into the Finance app's frame. Its writer is given the white box, a row for an id, what is
    /// picked (null for nothing), a way to pick another (null puts the pick away) and a way back to the Overview.
    /// </summary>
    public sealed class StandingPage
    {
        public Func<bool> On;
        public TableSpec Spec;
        public Action<InfoBox, Func<string, TableRow>, string, Action<string>, Action> Write;
    }

    /// <summary>The Finance app as the rest of the mod may use it: open it on a tab. Each does nothing while the economy is off or the app is not on the phone.</summary>
    public static class Finance
    {
        public const int Overview = 0, Credit = 1, Borrow = 2, CashFlow = 3, Accounts = 4;

        public static void Open(int tab)
        {
            EconomyDirector director = EconomyDirector.Instance;
            if (director != null)
            {
                director.Open(tab);
            }
        }

        /// <summary>Opens the Finance app on the Standing page (the Overview while there is none).</summary>
        public static void OpenStanding()
        {
            EconomyDirector director = EconomyDirector.Instance;
            if (director != null)
            {
                director.OpenStanding();
            }
        }

        /// <summary>Opens the Finance app on the rivals' page (the Overview while there is none).</summary>
        public static void OpenRivals()
        {
            EconomyDirector director = EconomyDirector.Instance;
            if (director != null)
            {
                director.OpenRivals();
            }
        }

        /// <summary>Opens the Economy app on its Prices and rates tab, where the city's notices lead; the Finance app's Overview while the Economy app is not on the phone.</summary>
        public static void OpenCity()
        {
            Func<int, bool> app = EconomyBridges.OpenEconomyApp;
            if (app != null && app(1))
            {
                return;
            }
            EconomyDirector director = EconomyDirector.Instance;
            if (director != null)
            {
                director.OpenCity();
            }
        }
    }
}
