using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>One service at one place: where it stands, what it has used this month, and what is owed.</summary>
    public sealed class Account
    {
        public string Place;             // the address, written as business pressure writes a place (CompanyGame.Place)
        public Service Service;
        public PlaceKind Kind;
        public SupplyState State;
        public bool Owned;               // a place the player bought (a rented one just pays: its account is the landlord's)
        public bool AutoDebit;           // the bill is taken on its due day (the Utilities app)
        public int OpenedDay;            // the day it was applied for, or began
        public int ConnectDay;           // Pending: the day it connects; Cut: the day it was cut
        public int TemporaryUntil;       // the first day a temporary supply no longer covers (0: none)
        public int Deposit;              // money held, as it was paid
        public int Accrued;              // cents used this month, at each day's prices
        public int Units;                // thousandths of a unit used this month
        public int Owed;                 // money billed and not paid
        public int DueDay;               // the day the bill owed falls due (0 with nothing owed)
        public bool LateCharged;         // the late fee has been charged on the bill owed
        public int LastBill;             // the last bill, money
        public int LastBillDay;
        public int OnTime;               // bills paid by their due day in a row
        public bool CutForDebt;          // cut because a bill went unpaid: reconnecting costs the fee

        public bool IsHome => Kind == PlaceKind.Home;
    }

    /// <summary>Every account the player has, and how far the days have been added up.</summary>
    public sealed class UtilitiesState
    {
        public readonly List<Account> Accounts = new List<Account>();
        public int LastDay;              // the last day whose use was added up (0: none yet)
        public int MonthStart;           // the first day of the month being added up

        public Account Find(string place, Service service)
        {
            for (int i = 0; i < Accounts.Count; i++)
            {
                Account a = Accounts[i];
                if (a != null && a.Service == service && string.Equals(a.Place, place, StringComparison.Ordinal))
                {
                    return a;
                }
            }
            return null;
        }
    }
}
