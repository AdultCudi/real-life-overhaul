using System;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>What a step of the books asks the game side to do with an account.</summary>
    public enum DueStep
    {
        None = 0,
        ChargeLate = 1,     // the bill owed is a day past its due day: the late fee, once
        Cut = 2             // ...and the days of grace have run out: the supply is cut
    }

    /// <summary>What becomes of an account as the player's places are read again (UtilitiesRules.Fate).</summary>
    public enum AccountFate
    {
        Keep = 0,           // a place of the player's, or a last bill still owed
        LastBill = 1,       // the place is no longer the player's: its last bill now, and closed
        WriteOff = 2,       // a place the utilities never supply (an underground car park): nothing owed, taken out
        TakeOut = 3         // closed and settled: off the books
    }

    /// <summary>The rules of the city's power and water, prices as the city's level (1000 at the start: Core/Prices). Checked offline (EconomyCheck).</summary>
    public static class UtilitiesRules
    {
        /// <summary>Thousandths of a unit used in a day: a home by floor and people, a business by floor and kind, by the season.</summary>
        public static long Use(Service service, PlaceKind kind, int sqm, int people, bool open, int season)
        {
            sqm = Math.Max(0, sqm);
            int s = Math.Max(0, Math.Min(3, season));
            long use;
            if (kind == PlaceKind.Home)
            {
                int persons = Math.Max(1, people);
                use = service == Service.Power
                    ? (UtilitiesCatalogue.HomePowerBase + (long)UtilitiesCatalogue.HomePowerPerSqm * sqm) * (1000 + (long)UtilitiesCatalogue.ExtraPersonShare * (persons - 1)) / 1000
                    : (long)UtilitiesCatalogue.HomeWaterPerPerson * persons;
            }
            else
            {
                int k = Math.Max(0, Math.Min(UtilitiesCatalogue.PowerPerSqm.Length - 1, (int)kind));
                use = (long)(service == Service.Power ? UtilitiesCatalogue.PowerPerSqm[k] : UtilitiesCatalogue.WaterPerSqm[k]) * sqm;
                if (!open)
                {
                    use = use * UtilitiesCatalogue.ClosedShare / 1000;
                }
            }
            return use * (service == Service.Power ? UtilitiesCatalogue.PowerSeason[s] : UtilitiesCatalogue.WaterSeason[s]) / 1000;
        }

        /// <summary>A day's use in cents at a price level: the units and the standing charge, with the temporary surcharge.</summary>
        public static int DayCents(Service service, long use, int level, bool temporary)
        {
            int i = (int)service;
            long cents = (Math.Max(0L, use) * UtilitiesCatalogue.UnitCents[i] + 500) / 1000 + UtilitiesCatalogue.StandingCents[i];
            cents = cents * Math.Max(PriceRules.Least, Math.Min(PriceRules.Most, level)) / PriceRules.Start;
            if (temporary)
            {
                cents = cents * (1000 + UtilitiesCatalogue.TemporarySurcharge) / 1000;
            }
            return (int)Math.Min(cents, int.MaxValue / 4);
        }

        /// <summary>Whole money from cents, halves away from nothing.</summary>
        public static int Money(long cents)
        {
            return (int)Math.Min((Math.Max(0L, cents) + 50) / 100, int.MaxValue / 4);
        }

        /// <summary>A month of a day's cost, money.</summary>
        public static int Month(int dayCents, int monthDays)
        {
            return Money((long)Math.Max(0, dayCents) * Math.Max(1, monthDays));
        }

        /// <summary>The days a bill is due in: half a month, within the catalogue's bounds.</summary>
        public static int DueIn(int monthDays)
        {
            return Math.Max(UtilitiesCatalogue.DueLeast, Math.Min(UtilitiesCatalogue.DueMost, monthDays / 2));
        }

        /// <summary>The deposit a new account asks: months of its estimate by credit grade (none for a good one).</summary>
        public static int Deposit(int grade, int monthEstimate)
        {
            int g = Math.Max(0, Math.Min(UtilitiesCatalogue.DepositMonths.Length - 1, grade));
            return UtilitiesCatalogue.DepositMonths[g] * Math.Max(0, monthEstimate);
        }

        public static bool Supplied(Account a, int day)
        {
            if (a == null)
            {
                return false;
            }
            switch (a.State)
            {
                case SupplyState.Active:
                    return true;
                case SupplyState.Temporary:
                case SupplyState.Pending:
                    return day < a.TemporaryUntil;   // a bought place before its account connects
                default:
                    return false;
            }
        }

        /// <summary>A day's use added to the month's, with the temporary surcharge while supplied that way.</summary>
        public static void AddDay(Account a, long use, int level, int day)
        {
            if (a == null || !Supplied(a, day))
            {
                return;
            }
            bool temporary = a.State != SupplyState.Active;
            a.Accrued = (int)Math.Min((long)a.Accrued + DayCents(a.Service, use, level, temporary), int.MaxValue / 4);
            a.Units = (int)Math.Min(a.Units + use, int.MaxValue / 4);
        }

        /// <summary>The month's use billed, due in half a month; the bill, nothing if nothing was used.</summary>
        public static int Bill(Account a, int day, int monthDays)
        {
            if (a == null)
            {
                return 0;
            }
            int bill = Money(a.Accrued);
            a.Accrued = 0;
            a.Units = 0;
            if (bill <= 0)
            {
                return 0;
            }
            a.Owed = (int)Math.Min((long)a.Owed + bill, int.MaxValue / 4);
            a.DueDay = day + DueIn(monthDays);
            a.LateCharged = false;
            a.LastBill = bill;
            a.LastBillDay = day;
            return bill;
        }

        /// <summary>What a bill asks today: the late fee once a day overdue, the cut once the days of grace are gone.</summary>
        public static DueStep Due(Account a, int day)
        {
            if (a == null || a.Owed <= 0 || a.DueDay <= 0 || day <= a.DueDay)
            {
                return DueStep.None;
            }
            if (day >= a.DueDay + UtilitiesCatalogue.CutAfterDays && (a.State == SupplyState.Active || a.State == SupplyState.Temporary || a.State == SupplyState.Pending))
            {
                return DueStep.Cut;
            }
            return a.LateCharged ? DueStep.None : DueStep.ChargeLate;
        }

        /// <summary>What is owed paid; on time it counts towards the deposit's return. The deposit given back.</summary>
        public static int Paid(Account a, int day)
        {
            if (a == null || a.Owed <= 0)
            {
                return 0;
            }
            bool onTime = a.DueDay <= 0 || day <= a.DueDay;
            a.Owed = 0;
            a.DueDay = 0;
            a.LateCharged = false;
            a.OnTime = onTime ? a.OnTime + 1 : 0;
            if (a.Deposit > 0 && a.OnTime >= UtilitiesCatalogue.DepositBackBills)
            {
                int back = a.Deposit;
                a.Deposit = 0;
                return back;
            }
            return 0;
        }

        /// <summary>The supply cut, the day noted, and whether it was for a bill left unpaid.</summary>
        public static void Cut(Account a, int day, bool forDebt = false)
        {
            if (a == null || a.State == SupplyState.Closed)
            {
                return;
            }
            a.State = SupplyState.Cut;
            a.CutForDebt = forDebt;
            a.ConnectDay = day;
            a.TemporaryUntil = 0;
        }

        /// <summary>An account applied for in person, only with nothing owed: it connects after ConnectDays.</summary>
        public static bool Apply(Account a, int day)
        {
            if (a == null || a.Owed > 0 || a.State == SupplyState.Active || a.State == SupplyState.Pending)
            {
                return false;
            }
            a.State = SupplyState.Pending;
            a.CutForDebt = false;
            a.OpenedDay = day;
            a.ConnectDay = day + UtilitiesCatalogue.ConnectDays;
            return true;
        }

        /// <summary>The day's turn: an applied account connects, a run-out temporary supply is cut. True if it changed.</summary>
        public static bool Turn(Account a, int day)
        {
            if (a == null)
            {
                return false;
            }
            if (a.State == SupplyState.Pending && day >= a.ConnectDay)
            {
                a.State = SupplyState.Active;
                a.TemporaryUntil = 0;
                return true;
            }
            if (a.State == SupplyState.Temporary && day >= a.TemporaryUntil)
            {
                Cut(a, day);
                return true;
            }
            return false;
        }

        /// <summary>A bought place with no account: supplied at the surcharge for TemporaryDays.</summary>
        public static Account Temporary(string place, Service service, PlaceKind kind, int day)
        {
            return new Account
            {
                Place = place, Service = service, Kind = kind, State = SupplyState.Temporary, Owned = true, OpenedDay = day,
                TemporaryUntil = day + UtilitiesCatalogue.TemporaryDays
            };
        }

        /// <summary>A rented place: the landlord's account, supplied at once, with no deposit; the player just pays.</summary>
        public static Account Rented(string place, Service service, PlaceKind kind, int day)
        {
            return new Account { Place = place, Service = service, Kind = kind, State = SupplyState.Active, Owned = false, OpenedDay = day, AutoDebit = true };
        }

        public const string CarParkStreet = "ba:street_parking";   // the game's street of its underground car parks (UndergroundParkingEntrance.Address)
        public const string CarParkSize = "ba:buildingsize_parking";   // ...and their size (UndergroundParkingManager.ParkingBuildingSize)

        /// <summary>Every place but an underground car park, which the game gives no premises to light or plumb.</summary>
        public static bool Serves(string street, string size)
        {
            return !string.Equals(street, CarParkStreet, StringComparison.Ordinal) && !string.Equals(size, CarParkSize, StringComparison.Ordinal);
        }

        /// <summary>An account's place (an escaped street, a tilde and a number) is an underground car park's.</summary>
        public static bool CarParkPlace(string place)
        {
            int at = place != null ? place.LastIndexOf('~') : -1;
            return at > 0 && !Serves(Uri.UnescapeDataString(place.Substring(0, at)), null);
        }

        /// <summary>What becomes of an account as the places are read again (see AccountFate).</summary>
        public static AccountFate Fate(Account a, bool held, bool unserved)
        {
            if (a == null)
            {
                return AccountFate.TakeOut;
            }
            if (unserved || CarParkPlace(a.Place))
            {
                return AccountFate.WriteOff;
            }
            if (a.State != SupplyState.Closed && !held)
            {
                return AccountFate.LastBill;
            }
            return a.State == SupplyState.Closed && a.Owed <= 0 && a.Accrued <= 0 ? AccountFate.TakeOut : AccountFate.Keep;
        }

        /// <summary>A never-supplied place's account written off: nothing owed or used, closed, so taken out; the deposit to give back.</summary>
        public static int WriteOff(Account a)
        {
            if (a == null)
            {
                return 0;
            }
            int back = Math.Max(0, a.Deposit);
            a.Deposit = 0;
            a.Owed = 0;
            a.Accrued = 0;
            a.Units = 0;
            a.DueDay = 0;
            a.LateCharged = false;
            a.CutForDebt = false;
            a.State = SupplyState.Closed;
            a.TemporaryUntil = 0;
            a.ContractUntil = 0;
            a.OutageFrom = a.OutageUntil = 0;
            return back;
        }
    }
}
