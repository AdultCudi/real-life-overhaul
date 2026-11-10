using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings.Office.Headquarters;
using Entities;
using Extensions;
using Helpers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// The one file of health that touches the game: the clock, the player's cash through the mod's one booking call, the game's own needs and its count of hospital nights,
    /// the energy drain of a serious case, the player's own staff through the game's own sick days, the living population's state and the notices. Nothing per frame.
    /// </summary>
    public static class HealthGame
    {
        public const string PremiumKey = "reallife_transaction_hc_premium";
        public const string BillKey = "reallife_transaction_hc_bill";
        public const string DebtKey = "reallife_transaction_hc_debt_paid";
        public const string SickPayKey = "reallife_transaction_hc_salary_sick";
        public const string MissedKey = "reallife_transaction_hc_missed_fee";
        public const string GameBillKey = "ba:transaction_hospitalbill";   // the game's own bill for its night (GameManager.HospitalRespawn)
        private const string SeriousDrain = "rlo-health-serious";
        private const float NeedsFloor = 55f;   // energy and hunger are not let below this in hospital

        public static IModLogger Logger;

        private static bool _draining;

        private static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => Save != null;

        public static int Day => Save != null ? Save.Day : 0;

        public static int Hour => Save != null ? Save.Hour : 0;

        /// <summary>The game's clock in whole minutes since day 0.</summary>
        public static int Minutes => Save != null ? (int)TimeHelper.NowInMinutes() : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        public static int MonthDays => HealthRules.MonthDays(DaysPerYear);

        public static int Level => Prices.Level;

        /// <summary>The player's age in the save's years.</summary>
        public static int Age
        {
            get
            {
                CharacterData me = PlayerHelper.CharacterData;
                return me != null ? Mathf.Max(0, TimeHelper.GetYearsByDays(me.ageInDays)) : 30;
            }
        }

        /// <summary>The player's cash in whole money, never under nothing.</summary>
        public static long Cash => Save != null ? (long)Math.Floor(Mathf.Max(0f, Mathf.Min(Save.Money, 1e12f))) : 0L;

        /// <summary>The cash a bill may be paid from: over what the investors of the player's firms are owed, which is not the player's to spend on themselves.</summary>
        public static long Spare
        {
            get
            {
                long reserved = 0;
                try
                {
                    reserved = HealthBridges.Reserved != null ? HealthBridges.Reserved() : 0L;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                return Math.Max(0L, Cash - Math.Max(0L, reserved));
            }
        }

        /// <summary>Money out of the player's cash, whole money, through the mod's one call; false, nothing taken, if the spare cash does not cover it.</summary>
        public static bool Pay(long amount, string key)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (amount > Spare || amount > int.MaxValue / 2)
            {
                return false;
            }
            return BizBooks.Book(-(float)amount, new TransactionInfo(key), null, null, force: false, showNotification: false);
        }

        /// <summary>Money into the player's cash: sick pay, in cents.</summary>
        public static void ReceiveCents(long cents, string key)
        {
            if (cents > 0)
            {
                BizBooks.Book(cents / 100f, new TransactionInfo(key), null, null, force: true);
            }
        }

        /// <summary>What a plan pays of the game's own bill comes back to the player: the game took it whole a moment before.</summary>
        public static void Refund(long amount)
        {
            if (amount > 0)
            {
                BizBooks.Book((float)amount, new TransactionInfo(BillKey), null, null, force: true);
            }
        }

        /// <summary>The game's count of the nights it has put the player in hospital (a collapse, an ambulance, a crash).</summary>
        public static int GameStays => Save != null && Save.achievementsData != null ? Save.achievementsData.hospitalization : 0;

        /// <summary>How the player is living: the game's own needs, and Home's where Home runs.</summary>
        public static HealthFactors Factors()
        {
            HealthFactors f = HealthFactors.Middling;
            if (Save != null)
            {
                f.Energy = Mathf.RoundToInt(Save.Energy);
                f.Hunger = Mathf.RoundToInt(Save.Hunger);
            }
            try
            {
                return HealthBridges.Living != null ? HealthBridges.Living(f) : f;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return f;
            }
        }

        /// <summary>The energy a serious case takes, on or off. The game does not save energy drains, so the hour's look sets it again after a load.</summary>
        public static void Drain(bool on)
        {
            if (on == _draining)
            {
                return;
            }
            _draining = on;
            EnergyHelper.RemoveEnergySpender(SeriousDrain);
            if (on)
            {
                EnergyHelper.AddEnergySpender(SeriousDrain, HealthCatalogue.SeriousDrain);
            }
        }

        /// <summary>The city went or a save loads: the drain is the game's to forget.</summary>
        public static void Reset()
        {
            _draining = false;
        }

        /// <summary>Energy and hunger held up while the hospital's clock runs.</summary>
        public static void HoldNeeds()
        {
            GameInstance save = Save;
            if (save == null)
            {
                return;
            }
            save.Energy = Mathf.Max(save.Energy, NeedsFloor);
            save.Hunger = Mathf.Max(save.Hunger, NeedsFloor);
        }

        /// <summary>The injury's mood, held on while the case runs (the game's own modifier of the mod's, as a crash gives it).</summary>
        public static void HoldHurt(bool on)
        {
            List<HappinessModifierData> list = Save != null ? Save.happinessModifiers : null;
            HappinessModifierData found = null;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i].type == ModState.Injured)
                {
                    found = list[i];
                    break;
                }
            }
            if (on && found == null)
            {
                ModState.AddModifier(ModState.Injured, 12);
            }
            else if (on && found.hoursLeft <= 3)
            {
                found.hoursLeft = 12;   // renewed quietly, as Home holds its own
            }
        }

        /// <summary>The living population as health may touch it: running here, this player's own and not in a Going Public session. Null otherwise.</summary>
        public static PopulationState People()
        {
            if (!Settings.LivingPopulation || !CityPopulation.Running || CityPopulation.Following || GoingPublic.InSession)
            {
                return null;
            }
            return CityPopulation.Live;
        }

        // ---- the player's own staff

        private static readonly Dictionary<Address, int> OffAt = new Dictionary<Address, int>(32);

        /// <summary>
        /// The staff's day: each of the player's own people at a place of the player's is drawn once (HealthPeople.StaffCourse) against the wave and the colleagues off at the
        /// same place, and whoever has a case under way is off tomorrow through the game's own sick day, which calls the business and finds cover as it always does. Returns
        /// how many are off with a case of health's. One walk over the game's list of employees a day.
        /// </summary>
        public static int StaffDay(HealthState s, int day, int wave, int policy)
        {
            GameInstance save = Save;
            List<EmployeeInstance> all = save != null ? save.EmployeeInstances : null;
            if (all == null)
            {
                return 0;
            }
            for (int i = s.Staff.Count - 1; i >= 0; i--)
            {
                if (s.Staff[i].Until < day)
                {
                    s.Staff.RemoveAt(i);
                }
            }
            OffAt.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (Counts(e) && (e.isAbsent || ComesInIll(s, e.id, day)))
                {
                    OffAt.TryGetValue(e.assignedAddress, out int n);
                    OffAt[e.assignedAddress] = n + 1;
                }
            }
            int off = 0;
            for (int i = 0; i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (!Counts(e))
                {
                    continue;
                }
                StaffCase c = CaseOf(s, e.id);
                if (c == null && s.Staff.Count < HealthCatalogue.StaffCasesMost)
                {
                    OffAt.TryGetValue(e.assignedAddress, out int colleagues);
                    colleagues -= e.isAbsent ? 1 : 0;
                    int course = HealthPeople.StaffCourse(s.Seed, HealthRules.IdOf(e.id), day, wave, colleagues, PlanTier(e));
                    if (course > 0)
                    {
                        int from = HealthPeople.StaffFrom(day, policy);
                        c = new StaffCase { Id = e.id, From = from, Until = from + course - 1 };
                        s.Staff.Add(c);
                    }
                }
                if (c != null && day + 1 >= c.From && day + 1 <= c.Until)
                {
                    e.nextSickDay = Math.Min(e.nextSickDay, day + 1);   // off tomorrow, by the game's own sick day
                    off++;
                }
            }
            OffAt.Clear();
            return off;
        }

        /// <summary>Somebody of the player's staff falls ill tomorrow, for the test: the first who counts and has no case. Their name, or null.</summary>
        public static string StaffTest(HealthState s, int day)
        {
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (Counts(e) && CaseOf(s, e.id) == null && s.Staff.Count < HealthCatalogue.StaffCasesMost)
                {
                    s.Staff.Add(new StaffCase { Id = e.id, From = day + 1, Until = day + 1 });
                    e.nextSickDay = Math.Min(e.nextSickDay, day + 1);
                    return e.characterData.name;
                }
            }
            return null;
        }

        private static bool Counts(EmployeeInstance e)
        {
            if (e == null || string.IsNullOrEmpty(e.id) || e.IsCandidate || e.isBeingReplaced || e.satisfaction <= 0f || e.characterData == null || e.assignedAddress == null)
            {
                return false;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(e.assignedAddress);
            return reg != null && GoingPublic.Mine(reg);   // never another player's people
        }

        private static StaffCase CaseOf(HealthState s, string id)
        {
            for (int i = 0; i < s.Staff.Count; i++)
            {
                if (s.Staff[i].Id == id)
                {
                    return s.Staff[i];
                }
            }
            return null;
        }

        // With no sick pay somebody ill comes in the first day: they count as a colleague who passes it on.
        private static bool ComesInIll(HealthState s, string id, int day)
        {
            StaffCase c = CaseOf(s, id);
            return c != null && day < c.From;
        }

        // The game's own health plan for an employee: 0 none, 1 bronze, 2 silver, 3 gold (HrManagerPlan.healthInsurancePlan while its manager is in post).
        private static int PlanTier(EmployeeInstance e)
        {
            HrManagerPlan plan = HrManagerHelper.GetPlanFromId(e.assignedHrManagerPlanId);
            return plan != null && plan.healthInsurancePlan != null && plan.HasActiveHealthInsurance() ? (int)plan.healthInsurancePlan.planType + 1 : 0;
        }

        // ---- words

        public static string Money(long amount) => ((float)amount).ToCurrencyFormat();

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, Action click = null)
        {
            Notifications.Show(type, key, data, 10f, null, click != null ? Guard.Wrap(click) : null, true, false);
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var d = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                d[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return d;
        }
    }
}
