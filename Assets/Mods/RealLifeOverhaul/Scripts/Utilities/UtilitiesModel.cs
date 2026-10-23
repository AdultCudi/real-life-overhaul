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
        // ---- the internet
        public int Provider = -1;        // InternetCatalogue.Providers, or InternetCatalogue.OwnIndex: the player's own company
        public int Plan;
        public bool BusinessPlan;
        public int ContractUntil;        // the first day out of contract (0: month by month)
        public int PriceCents;           // the month's price: held for a contract, set again each month without one
        public int OutageFrom, OutageUntil;   // an outage, in hours from the save's first day (day * 24 + hour)

        public bool IsHome => Kind == PlaceKind.Home;
    }

    /// <summary>Every account the player has, and how far the days have been added up.</summary>
    public sealed class UtilitiesState
    {
        public readonly List<Account> Accounts = new List<Account>();
        public int LastDay;              // the last day whose use was added up (0: none yet)
        public int MonthStart;           // the first day of the month being added up
        public int Seed;                 // the internet's outages and prices are drawn from it (InternetRules.Draw)
        public int Month;                // months turned since the internet began, for its prices
        public readonly int[] Promo = { 1000, 1000, 1000, 1000 };   // each rival's month, thousandths of its list
        public readonly Network Network = new Network();             // the player's own broadband company (Utilities/Broadband)
        public int GraceUntil;           // the internet's grace period (InternetRules.BeginGrace): the first day it no longer covers (0: none)
        public readonly List<string> Grace = new List<string>();     // ...and the places it covers: the businesses that needed a connection when it began

        /// <summary>A provider's month, thousandths of its list (the player's own company keeps to its list).</summary>
        public int PromoOf(int provider)
        {
            return provider >= 0 && provider < Promo.Length ? Promo[provider] : 1000;
        }

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

    /// <summary>
    /// The accounts by place and service, as <see cref="UtilitiesState.Find"/> answers, for passes that add or move none: one
    /// build instead of some 270,000 compares an hour. Game-free.
    /// </summary>
    public sealed class AccountIndex
    {
        private readonly Dictionary<string, Account>[] _by = new Dictionary<string, Account>[UtilitiesCatalogue.AllServices];
        private UtilitiesState _of;

        public AccountIndex()
        {
            for (int i = 0; i < _by.Length; i++)
            {
                _by[i] = new Dictionary<string, Account>(64, StringComparer.Ordinal);
            }
        }

        public void Build(UtilitiesState state)
        {
            _of = state;
            for (int i = 0; i < _by.Length; i++)
            {
                _by[i].Clear();
            }
            for (int i = 0; state != null && i < state.Accounts.Count; i++)
            {
                Account a = state.Accounts[i];
                int s = a != null ? (int)a.Service : -1;
                if (a != null && a.Place != null && s >= 0 && s < _by.Length && !_by[s].ContainsKey(a.Place))
                {
                    _by[s].Add(a.Place, a);   // the first of a place and service, as Find has it
                }
            }
        }

        /// <summary>An account just added in place of its place and service's last: the answer from now on.</summary>
        public void Put(UtilitiesState state, Account a)
        {
            int s = a != null ? (int)a.Service : -1;
            if (!ReferenceEquals(_of, state) || a.Place == null || s < 0 || s >= _by.Length)
            {
                _of = null;   // built again at the next answer
                return;
            }
            _by[s][a.Place] = a;
        }

        /// <summary>The account of a place for a service, as <see cref="UtilitiesState.Find"/> gives it.</summary>
        public Account Find(UtilitiesState state, string place, Service service)
        {
            int s = (int)service;
            if (place == null || s < 0 || s >= _by.Length || state == null)
            {
                return state != null ? state.Find(place, service) : null;
            }
            if (!ReferenceEquals(_of, state))
            {
                Build(state);
            }
            return _by[s].TryGetValue(place, out Account a) && a.Service == service && string.Equals(a.Place, place, StringComparison.Ordinal) ? a : null;
        }
    }
}
