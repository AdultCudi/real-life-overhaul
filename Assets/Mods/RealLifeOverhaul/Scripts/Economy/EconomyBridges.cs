using System;
using System.Collections.Generic;

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

        /// <summary>False while the record cannot be read yet (its feature has not loaded this save). Default: ready.</summary>
        public static Func<bool> LegalReady;

        /// <summary>Adds the fines and court debts the mod keeps to the list. Default: none.</summary>
        public static Action<List<DebtLine>> OtherDebts;

        public static void Reset()
        {
            Legal = null;
            LegalReady = null;
            OtherDebts = null;
        }
    }

    /// <summary>The Finance app as the rest of the mod may use it: open it, on a tab.</summary>
    public static class Finance
    {
        public const int Overview = 0, Credit = 1, Borrow = 2, CashFlow = 3;

        /// <summary>Opens the Finance app on a tab. Does nothing while the economy is off or the app is not on the phone.</summary>
        public static void Open(int tab)
        {
            EconomyDirector director = EconomyDirector.Instance;
            if (director != null)
            {
                director.Open(tab);
            }
        }
    }
}
