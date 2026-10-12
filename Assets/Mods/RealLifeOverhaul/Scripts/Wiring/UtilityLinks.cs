using System;
using System.Collections.Generic;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Power and water (Scripts/Utilities) with the rest of the mod:
        ///
        ///   Grade        the credit rating (Economy): the deposit a new account asks for
        ///   Household    who lives at the player's home besides them (Family: a partner who moved in, children at
        ///                home; the roommate at the home they share): the home's power and water
        ///   CloseFor     a business closed for want of power, water or a connection (Business, the game's own switch),
        ///                as the utilities' own closure: asked again each hour, it lasts while the want does, whether or
        ///                not business events are on
        ///   Release      ...and ended as soon as the business has what it lacked
        ///   UtilitiesOn  their off switch, read by the closures' keeper (BusinessDirector): off, or stopped by a fault,
        ///                every business they closed opens again
        ///   Record       why, in the Business desk's messages (BusinessDesk.Text), to read back after the notification
        ///   Taken        the courthouse's building (Justice), which the office is not to be
        ///   Life         the electricity company's events need a power account of the player's (LifeAccounts.Power)
        ///   Home         a home without water has dry taps, one without power dark screens and no cooker (HomeTidy)
        ///   shares       the partner's share and the roommate's are of the rent and the home's power and water
        ///   Finance      power and water owed are among "what you owe" (EconomyLinks.AddDebts)
        ///   the Planner  a source of its own: a bill's due day, the day an unpaid one is cut, the end of a temporary
        ///                supply, the morning an account connects
        /// </summary>
        public static class UtilityLinks
        {
            internal static void Connect()
            {
                UtilitiesBridges.Grade = () => (int)Economy.CreditLedger.Grade;
                UtilitiesBridges.Household = Household;
                UtilitiesBridges.CloseFor = (reg, hours) => BusinessDirector.Instance != null && BusinessDirector.Instance.CloseFor(reg, hours, ClosureSource.Utilities);
                UtilitiesBridges.Release = reg => BusinessDirector.Instance?.Release(reg, ClosureSource.Utilities);
                BusinessDirector.UtilitiesOn = () => Settings.Utilities && !UtilitiesDirector.Failed;
                UtilitiesBridges.Record = (key, data) => BusinessDesk.Text(key, data, notify: false);
                UtilitiesBridges.Taken = building =>
                {
                    JusticeDirector justice = JusticeDirector.Instance;
                    return justice != null && justice.Court != null && ReferenceEquals(justice.Court.Building, building);
                };
                LifeAccounts.Power = () => UtilitiesDirector.HasPowerAccount;
                HomeTidy.HasPower = reg => UtilitiesDirector.Supplied(reg, Service.Power);
                HomeTidy.HasWater = reg => UtilitiesDirector.Supplied(reg, Service.Water);
                Family.HomeLife.Bills = UtilitiesDirector.DayCost;
                Roommates.RoommateGame.Bills = UtilitiesDirector.DayCost;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                UtilitiesBridges.Reset();
                BusinessDirector.UtilitiesOn = null;
                LifeAccounts.Power = null;
                HomeTidy.HasPower = null;
                HomeTidy.HasWater = null;
                Family.HomeLife.Bills = null;
                Roommates.RoommateGame.Bills = null;
            }

            private static readonly UtilitiesSource Source = new UtilitiesSource();
            private static readonly string[] DebtKeys = { "reallife_econ_debt_util_power", "reallife_econ_debt_util_water", "reallife_econ_debt_util_internet" };

            /// <summary>Power and water owed, a line each, with the days the oldest bill is late.</summary>
            internal static void AddDebts(List<Economy.DebtLine> into)
            {
                UtilitiesState state = UtilitiesDirector.Instance != null ? UtilitiesDirector.Instance.State : null;
                if (state == null)
                {
                    return;
                }
                int today = UtilitiesGame.Today;
                int[] owed = new int[UtilitiesCatalogue.AllServices], late = new int[UtilitiesCatalogue.AllServices];
                for (int i = 0; i < state.Accounts.Count; i++)
                {
                    Account a = state.Accounts[i];
                    if (a.Owed <= 0)
                    {
                        continue;
                    }
                    int s = (int)a.Service;
                    owed[s] += a.Owed;
                    if (a.DueDay > 0 && today > a.DueDay)
                    {
                        late[s] = Math.Max(late[s], today - a.DueDay);
                    }
                }
                for (int s = 0; s < UtilitiesCatalogue.AllServices; s++)
                {
                    if (owed[s] > 0)
                    {
                        into.Add(new Economy.DebtLine { NameKey = DebtKeys[s], Owed = owed[s], LateDays = late[s] });
                    }
                }
            }

            private sealed class UtilitiesSource : IPlannerSource
            {
                public string Id => "utilities";

                public bool Available => Settings.Utilities && UtilitiesDirector.Instance != null && UtilitiesDirector.Instance.State != null && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    UtilitiesDirector d = UtilitiesDirector.Instance;
                    UtilitiesState state = d.State;
                    for (int i = 0; i < state.Accounts.Count; i++)
                    {
                        Account a = state.Accounts[i];
                        int key = Key(a);
                        if (a.Owed > 0 && a.DueDay >= fromDay && a.DueDay <= toDay)
                        {
                            Entry(into, 1, key, a.DueDay, a, d, "reallife_util_plan_due_title", "reallife_util_plan_due_what", a.AutoDebit ? "reallife_util_plan_due_auto" : "reallife_util_plan_due_missed");
                        }
                        int cut = a.DueDay + UtilitiesCatalogue.CutAfterDays;
                        if (a.Owed > 0 && a.DueDay > 0 && a.State != SupplyState.Cut && a.State != SupplyState.Closed && cut >= fromDay && cut <= toDay)
                        {
                            Entry(into, 2, key, cut, a, d, "reallife_util_plan_cut_title", "reallife_util_plan_cut_what", null);
                        }
                        if (a.State == SupplyState.Temporary && a.TemporaryUntil >= fromDay && a.TemporaryUntil <= toDay)
                        {
                            Entry(into, 3, key, a.TemporaryUntil, a, d, "reallife_util_plan_temporary_title", "reallife_util_plan_temporary_what", null);
                        }
                        if (a.State == SupplyState.Pending && a.ConnectDay >= fromDay && a.ConnectDay <= toDay)
                        {
                            Entry(into, 4, key, a.ConnectDay, a, d, "reallife_util_plan_connect_title", "reallife_util_plan_connect_what", null);
                        }
                    }
                    Network n = state.Network;
                    for (int district = 0; n.Running && district < Network.Districts; district++)
                    {
                        int ready = n.ReadyDay(district);
                        PlannerEntry e = n.Digging(district) && ready >= fromDay && ready <= toDay ? into.Add(Id, 5, district, PlannerCategory.Money, ready) : null;
                        if (e != null && into.WantText)
                        {
                            e.Title = UtilitiesText.T("reallife_bb_plan_open_title");
                            e.What = UtilitiesText.T("reallife_bb_plan_open_what", "district", BroadbandCompany.District(district));
                        }
                    }
                }

                private void Entry(PlannerList into, int kind, int key, int day, Account a, UtilitiesDirector d, string title, string what, string missed)
                {
                    PlannerEntry e = into.Add(Id, kind, key, PlannerCategory.Money, day);
                    if (e == null || !into.WantText)
                    {
                        return;
                    }
                    string service = UtilitiesText.Service(a.Service), amount = UtilitiesText.Money(a.Owed);
                    e.Title = UtilitiesText.T(title, "service", service);
                    e.What = UtilitiesText.T(what, "place", d.NameOf(a), "amount", amount);
                    if (a.Owed > 0 && kind <= 2)
                    {
                        e.Money = PlannerText.T("reallife_planner_costs", "amount", amount);
                    }
                    if (missed != null)
                    {
                        e.IfMissed = UtilitiesText.T(missed);
                    }
                }

                // The same number for the same account in every session (it is kept with a reminder).
                private static int Key(Account a)
                {
                    unchecked
                    {
                        int h = 17;
                        string place = a.Place ?? string.Empty;
                        for (int i = 0; i < place.Length; i++)
                        {
                            h = h * 31 + place[i];
                        }
                        return h * 4 + (int)a.Service;
                    }
                }
            }

            // The household lives at the player's first rented home (as HomeTidy.Home has it when they are out); the
            // roommate at the home the two share.
            private static int Household(BuildingRegistration reg)
            {
                if (reg == null)
                {
                    return 0;
                }
                int people = 0;
                if (ReferenceEquals(reg, FirstHome()))
                {
                    people += (Family.FamilyApi.LivesTogether ? 1 : 0) + Family.FamilyApi.Children;
                }
                Roommates.RoommateDirector roommates = Roommates.RoommateDirector.Instance;
                Roommates.RoommateState state = roommates != null ? roommates.State : null;
                if (state != null && state.Living && reg.StreetName == state.HomeStreet && reg.StreetNumber == state.HomeNumber)
                {
                    people++;
                }
                return people;
            }

            private static BuildingRegistration FirstHome()
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || save.BuildingRegistrations == null)
                {
                    return null;
                }
                foreach (BuildingRegistration reg in save.BuildingRegistrations)
                {
                    if (reg != null && reg.RentedByPlayer && reg.HasValidAddress)
                    {
                        Buildings.Building building = Helpers.BuildingHelper.GetBuilding(reg.Address);
                        if (building != null && building.BuildingType == "ba:buildingtype_residential")
                        {
                            return reg;
                        }
                    }
                }
                return null;
            }
        }
    }
}
