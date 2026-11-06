using System;
using System.Collections.Generic;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Power and water (UtilitiesBridges) with the credit grade, the household, business closures (the utilities' own,
        /// lasting while the want does, all lifted when they are off or stopped by a fault), the home's taps and screens,
        /// the bills' shares, "what you owe" in the Finance app and the Planner.
        /// </summary>
        public static class UtilityLinks
        {
            internal static void Connect()
            {
                // City Utilities' building, held for it from the start while it is on.
                SpecialBuildings.Reserve(CityReserves.UtilitiesId, CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber, () => Settings.Utilities, SpecialKind.Civic);
                TravelBridges.UtilitiesPicked = () => !Settings.Utilities || UtilitiesDirector.Failed
                    || (UtilitiesDirector.Instance != null && UtilitiesDirector.Instance.Office != null && UtilitiesDirector.Instance.Office.Searched);
                UtilitiesBridges.Grade = () => (int)Economy.CreditLedger.Grade;
                UtilitiesBridges.Household = Household;
                UtilitiesBridges.CloseFor = (reg, hours) => BusinessDirector.Instance != null && BusinessDirector.Instance.CloseFor(reg, hours, ClosureSource.Utilities);
                UtilitiesBridges.Release = reg => BusinessDirector.Instance?.Release(reg, ClosureSource.Utilities);
                BusinessDirector.UtilitiesOn = () => Settings.Utilities && !UtilitiesDirector.Failed;
                UtilitiesBridges.Record = (key, data) => BusinessDesk.Text(key, data, notify: false);
                UtilitiesBridges.FirmWork = FirmWork;
                UtilitiesBridges.ShopMost = ShopMost;
                UtilitiesBridges.Shops = Shops;
                LifeAccounts.Power = () => UtilitiesDirector.HasPowerAccount;
                HomeTidy.HasPower = reg => UtilitiesDirector.Supplied(reg, Service.Power);
                HomeTidy.HasWater = reg => UtilitiesDirector.Supplied(reg, Service.Water);
                HomeTidy.UtilitiesByPhone = () => UtilitiesDirector.Instance != null && UtilitiesDirector.Instance.State != null && UtilitiesDirector.Instance.ByPhone;
                Family.HomeLife.Bills = UtilitiesDirector.DayCost;
                Roommates.RoommateGame.Bills = UtilitiesDirector.DayCost;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                SpecialBuildings.Unreserve(CityReserves.UtilitiesId);
                TravelBridges.UtilitiesPicked = null;
                UtilitiesBridges.Reset();
                BusinessDirector.UtilitiesOn = null;
                LifeAccounts.Power = null;
                HomeTidy.HasPower = null;
                HomeTidy.HasWater = null;
                HomeTidy.UtilitiesByPhone = null;
                Family.HomeLife.Bills = null;
                Roommates.RoommateGame.Bills = null;
            }

            /// <summary>
            /// The one read of a building's internet speed for the work over it (NetSpeedRules), for the tech firms and the
            /// online shops alike: unknown with the utilities or the speed switch off, so both run exactly as before speed
            /// counted. An hour of 0 or more counts an outage then; -1 asks for the plan, outages aside.
            /// </summary>
            internal static int Speed(BuildingRegistration reg, int hour, bool shop)
            {
                try
                {
                    UtilitiesDirector d = UtilitiesDirector.Instance;
                    if (!Settings.Utilities || !Settings.UtilitiesNetSpeed || UtilitiesDirector.Failed || d == null || d.State == null || reg == null)
                    {
                        return NetSpeedRules.Unknown;
                    }
                    return d.Speed(reg, hour, shop);
                }
                catch (Exception e)
                {
                    Fault("UtilityLinks.Speed", e);
                    return NetSpeedRules.Unknown;
                }
            }

            // The work the player's tech firm at a place would do at a speed, for the plan picker; -1 with none there.
            private static int FirmWork(BuildingRegistration reg, int mbps)
            {
                try
                {
                    Tech.TechDirector tech = Tech.TechDirector.Instance;
                    Tech.FirmPlace firm = tech != null && tech.Firms != null && reg != null ? tech.FirmOf(Tech.TalentGame.Place(reg)) : null;
                    return firm != null ? Tech.FirmsRules.Connection(firm.File.Kind, mbps) : -1;
                }
                catch (Exception e)
                {
                    Fault("UtilityLinks.FirmWork", e);
                    return -1;
                }
            }

            // The orders a day the player's online shop at a place could take at a speed, for the plan picker; -1 with none there.
            private static int ShopMost(BuildingRegistration reg, int mbps)
            {
                try
                {
                    Economy.OnlineDirector online = Economy.OnlineDirector.Instance;
                    return online != null && Settings.Economy && Settings.EconomyOnline ? online.MostAt(reg, mbps) : -1;
                }
                catch (Exception e)
                {
                    Fault("UtilityLinks.ShopMost", e);
                    return -1;
                }
            }

            private static bool Shops(List<BuildingRegistration> into)
            {
                try
                {
                    Economy.OnlineDirector online = Economy.OnlineDirector.Instance;
                    return online != null && Settings.Economy && Settings.EconomyOnline && online.Buildings(into);
                }
                catch (Exception e)
                {
                    Fault("UtilityLinks.Shops", e);
                    return false;
                }
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
                            Entry(into, 1, key, a.DueDay, a, d, "reallife_util_plan_due_title", "reallife_util_plan_due_what",
                                a.AutoDebit ? "reallife_util_plan_due_auto" : "reallife_util_plan_due_missed");
                        }
                        int cut = a.DueDay + UtilitiesCatalogue.CutAfterDays;
                        if (a.Owed > 0 && a.DueDay > 0 && a.State != SupplyState.Cut && a.State != SupplyState.Closed && cut >= fromDay && cut <= toDay)
                        {
                            Entry(into, 2, key, cut, a, d, "reallife_util_plan_cut_title", "reallife_util_plan_cut_what", null);
                        }
                        if (a.State == SupplyState.Temporary && a.TemporaryUntil >= fromDay && a.TemporaryUntil <= toDay)
                        {
                            Entry(into, 3, key, a.TemporaryUntil, a, d, "reallife_util_plan_temporary_title", UtilitiesRules.Where("reallife_util_plan_temporary_what", d.ByPhone), null);
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
                        PlannerText.Costs(e, a.Owed, amount);
                    }
                    if (missed != null)
                    {
                        e.IfMissed = UtilitiesText.T(missed);
                    }
                }

                // The same number for the same account in every session: it is kept with a reminder.
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
                try
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
                catch (Exception e)
                {
                    Fault("UtilityLinks.Household", e);
                    return 0;
                }
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
                    if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress)
                    {
                        Buildings.Building building = GameRegistrations.BuildingOf(reg);
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
