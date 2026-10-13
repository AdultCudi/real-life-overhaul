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

    /// <summary>
    /// What the economy asks of the rest of the mod. Each is optional and has a default, so Scripts/Economy works
    /// alone; Scripts/Wiring/EconomyLinks.cs fills them in. Economy never calls another feature directly, and another
    /// feature reaches the economy only through <see cref="CreditLedger"/> and <see cref="Finance"/>.
    /// </summary>
    public static class EconomyBridges
    {
        /// <summary>The legal and public record. Default: spotless.</summary>
        public static Func<LegalStanding> Legal;

        /// <summary>
        /// Whether a transaction of the mod's pays back a game charge the player did not owe (a ride the day pass covered,
        /// a ticket on a vehicle lent to them): the city's cost of living counts the game's charges of the day less these.
        /// </summary>
        public static Func<string, bool> GivenBack;

        /// <summary>
        /// Whether a transaction of another feature's is a business's takings (the broadband company's customers' bills,
        /// at its headquarters): the game's statement counts it among the takings of the business at its address
        /// (GameBooks), so the game's tax and bank do. Default: none.
        /// </summary>
        public static Func<string, bool> Takings;

        /// <summary>
        /// The row among a business's goods (the cost of what it sold) that a transaction of another feature's is entered
        /// in, by the row's text key (the broadband company's network); null for none. Default: none.
        /// </summary>
        public static Func<string, string> CostRow;

        /// <summary>False while the record cannot be read yet (its feature has not loaded this save). Default: ready.</summary>
        public static Func<bool> LegalReady;

        /// <summary>Adds the fines and court debts the mod keeps to the list. Default: none.</summary>
        public static Action<List<DebtLine>> OtherDebts;

        /// <summary>
        /// A lender files a civil claim for this much with the courts. Returns the court's number of the claim, or 0 if
        /// there is no court to file with (then the debt simply stays owed). Default: no court.
        /// </summary>
        public static Func<int, int> FileClaim;

        /// <summary>True while a claim with this number is still before the court. Default: no.</summary>
        public static Func<int, bool> ClaimOpen;

        /// <summary>A claim is withdrawn (the debt behind it has been written off). Default: nothing.</summary>
        public static Action<int> WithdrawClaim;

        /// <summary>
        /// True while something has hold of the player that the lender of last resort keeps away from: custody, prison,
        /// hospital, a trip, a scene of the mod's, a panel on the screen. Default: never.
        /// </summary>
        public static Func<bool> Held;

        /// <summary>
        /// The player is set upon in the street: the injury a mugging gives (hours hurt, energy lost), with the
        /// notification named. False if it cannot be done now. Default: cannot.
        /// </summary>
        public static Func<int, float, string, bool> Assault;

        /// <summary>True if the player could see a thing at this point: it is near and the camera looks its way. Default: yes (so nothing is taken away in sight).</summary>
        public static Func<Vector3, bool> InView;

        /// <summary>A free stretch of kerb near a point, for the test tools' car. Default: none.</summary>
        public static SpotFinder KerbSpot;

        /// <summary>Makes the picture of a phone contact with this id, in this colour. Default: the contact has the game's blank picture.</summary>
        public static Action<string, Color> ContactIcon;

        public delegate bool SpotFinder(Vector3 near, out Vector3 spot, out Quaternion heading);

        /// <summary>
        /// The player's standing (Scripts/Fame) as a page of the Finance app, opened from the Overview with a button as the
        /// city's page is: its white box and its table's rows are written by its own writer, in the app's frame. Default:
        /// no such page and no button.
        /// </summary>
        public static StandingPage Standing;

        public static void Reset()
        {
            Standing = null;
            Legal = null;
            GivenBack = null;
            Takings = null;
            CostRow = null;
            LegalReady = null;
            OtherDebts = null;
            FileClaim = null;
            ClaimOpen = null;
            WithdrawClaim = null;
            Held = null;
            Assault = null;
            InView = null;
            KerbSpot = null;
            ContactIcon = null;
        }
    }

    /// <summary>
    /// A page another feature writes into the Finance app's frame (the player's standing): whether it is there now, its
    /// table, and its writer, which is given the white box, a row for an id, what is picked (a row's id, or null), a way to
    /// pick another (null puts the pick away) and a way back to the Overview. Asked when the page is shown, never otherwise.
    /// </summary>
    public sealed class StandingPage
    {
        public Func<bool> On;
        public TableSpec Spec;
        public Action<InfoBox, Func<string, TableRow>, string, Action<string>, Action> Write;
    }

    /// <summary>The Finance app as the rest of the mod may use it: open it, on a tab.</summary>
    public static class Finance
    {
        public const int Overview = 0, Credit = 1, Borrow = 2, CashFlow = 3, Accounts = 4;

        /// <summary>Opens the Finance app on a tab. Does nothing while the economy is off or the app is not on the phone.</summary>
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

        /// <summary>Opens the Finance app on the city's page (Overview's own page while the city's economy is off).</summary>
        public static void OpenCity()
        {
            EconomyDirector director = EconomyDirector.Instance;
            if (director != null)
            {
                director.OpenCity();
            }
        }
    }
}
