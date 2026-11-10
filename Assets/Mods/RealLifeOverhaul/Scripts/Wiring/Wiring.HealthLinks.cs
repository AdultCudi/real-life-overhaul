using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Health;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Health (Health/HealthBridges) with the rest of the mod:
        ///
        ///   Illness, Treated, Catch,
        ///   Cure, Living           Home's own illness is the player's case; Home's needs are how the player lives
        ///   HomeBridges.Roll,
        ///   Held, TooIll,
        ///   StatusNote             Home's chances drawn from the save's seed, a serious illness held on, no hard exercise, the line beside the needs
        ///   DoctorVisit.*          the doctor's desk: admission for a serious case, a plan's part of a fee, a case treated, an appointment kept
        ///   LifeEvents.Hurt        every injury of the mod's (a crash, a hit on foot, a mugging) gets a name and a course
        ///   PhoneContacts.DoctorFirst   the doctor's phone line books an appointment
        ///   CareerLinks.Health,
        ///   DayMissed, ShiftWorked,
        ///   EmployerPlan, ShiftOpen     sick leave at the player's job, an accident at a risky trade, the employer's plan
        ///   SickPolicy             the Company's sick pay policy, for the player's own staff
        ///   Reserved               the investors' floor of the player's firms: a bill is paid from the cash over it
        ///   Clinic                 one of the city's hospitals or clinics, for the appointment
        ///   OtherDebts, OtherAccounts,
        ///   Credit, FileClaim      the medical debt in the Finance app, on the credit file and before the court
        ///   Rival*                 the rival tycoons fall ill by the same draw; their line on the rich list and their panel
        ///   News.Extra             the morning's City Ledger: a rival in hospital
        ///   the Planner            the appointment, the premium's day and the day of the discharge
        ///
        /// Asked on an hour's or a day's pass or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class HealthLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static Action<List<DebtLine>> _debtsBefore;
            private static Action<List<ICreditAccount>> _accountsBefore;
            private static ICareerSickNote _noteBefore;
            private static readonly List<HealthStory> Stories = new List<HealthStory>(2);
            private static readonly MedicalDebt Debt = new MedicalDebt();
            private static readonly CareSource Planned = new CareSource();
            private static string _status;
            private static int _statusAt = int.MinValue;

            internal static void Connect()
            {
                HealthBridges.Illness = Illness;
                HealthBridges.Treated = () => HomeState.Loaded && HomeHealth.Ill && HomeState.IllTreated;
                HealthBridges.Catch = Catch;
                HealthBridges.Cure = Cure;
                HealthBridges.Living = Living;
                HealthBridges.DoctorNote = DoctorNote;
                HealthBridges.WasIll = WasIll;
                HealthBridges.EmployerPlan = () => Employer(out int _);
                HealthBridges.EmployerSince = () => { Employer(out int since); return since; };
                HealthBridges.ShiftOpen = ShiftOpen;
                HealthBridges.SickPolicy = SickPolicy;
                HealthBridges.Reserved = Reserved;
                HealthBridges.Clinic = Clinic;
                HealthBridges.FileClaim = amount => EconomyBridges.FileClaim != null ? EconomyBridges.FileClaim(amount) : 0;
                HealthBridges.Credit = Credit;
                HealthBridges.RivalWorth = RivalWorth;
                HealthBridges.RivalPay = RivalPay;
                HealthBridges.RivalRest = RivalRest;
                HealthBridges.RivalName = index => index >= 0 && index < RivalCatalogue.TycoonCount ? RivalCatalogue.Tycoons[index].Name : string.Empty;
                RivalBridges.HealthLine = tycoon => HealthDirector.Instance != null ? HealthDirector.Instance.RivalLine(tycoon) : string.Empty;

                HomeBridges.Roll = Roll;
                HomeBridges.Held = () => HealthDirector.Instance != null && HealthDirector.Instance.Holding;
                HomeBridges.TooIll = () => HealthDirector.Instance != null && HealthDirector.Instance.NoTraining;
                HomeBridges.StatusNote = StatusNote;
                DoctorVisit.Admission = Admission;
                DoctorVisit.Paid = amount => Safe("HealthLinks.Paid", () => HealthDirector.Instance?.DoctorPaid(amount));
                DoctorVisit.Treated = () => Safe("HealthLinks.Treated", () => HealthDirector.Instance?.DoctorTreated());
                DoctorVisit.Wait = Wait;
                LifeEvents.Hurt = hours => Safe("HealthLinks.Hurt", () => HealthDirector.Instance?.Hurt(hours));
                PhoneContacts.DoctorFirst = DoctorFirst;
                CareersBridges.DayMissed = (day, pay, hired) => Safe("HealthLinks.DayMissed", () => HealthDirector.Instance?.DayMissed(day, pay, hired));
                CareersBridges.ShiftWorked = skill => Safe("HealthLinks.ShiftWorked", () => HealthDirector.Instance?.ShiftWorked(Risk(skill)));
                _noteBefore = CareerLinks.Health;
                CareerLinks.Health = new Leave();

                _debtsBefore = EconomyBridges.OtherDebts;
                EconomyBridges.OtherDebts = Debts;
                _accountsBefore = EconomyBridges.OtherAccounts;
                EconomyBridges.OtherAccounts = Accounts;   // again at each start of the credit side, whose ledger forgets every account
                _paperBefore = News.Extra;
                News.Extra = Paper;
                PlannerDirector.Instance?.AddSource(Planned);
            }

            internal static void Disconnect()
            {
                HealthBridges.Reset();
                RivalBridges.HealthLine = null;
                HomeBridges.Roll = null;
                HomeBridges.Held = null;
                HomeBridges.TooIll = null;
                HomeBridges.StatusNote = null;
                DoctorVisit.Admission = null;
                DoctorVisit.Paid = null;
                DoctorVisit.Treated = null;
                DoctorVisit.Wait = null;
                LifeEvents.Hurt = null;
                PhoneContacts.DoctorFirst = null;
                CareersBridges.DayMissed = null;
                CareersBridges.ShiftWorked = null;
                CareerLinks.Health = _noteBefore;
                _noteBefore = null;
                EconomyBridges.OtherDebts = _debtsBefore;
                _debtsBefore = null;
                EconomyBridges.OtherAccounts = _accountsBefore;
                _accountsBefore = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
                _status = null;
                _statusAt = int.MinValue;
            }

            private static void Safe(string where, Action act)
            {
                try
                {
                    act();
                }
                catch (Exception e)
                {
                    Fault(where, e);
                }
            }

            // ---- Home and Life

            // Home's illness as health's condition (the same numbers), or Life's plain sickness as a cold.
            private static int Illness()
            {
                try
                {
                    if (HomeState.Loaded && HomeHealth.Ill)
                    {
                        return (int)HomeState.Ill;
                    }
                    return ModState.IsSick ? (int)Condition.Cold : 0;
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Illness", e);
                    return 0;
                }
            }

            private static bool Catch(int condition, string causeKey)
            {
                try
                {
                    return HomeState.Loaded && Settings.HomeLife && condition > 0 && condition <= (int)RealLifeOverhaul.Illness.Burnout
                           && HomeHealth.Catch((RealLifeOverhaul.Illness)condition, causeKey, false);
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Catch", e);
                    return false;
                }
            }

            private static void Cure()
            {
                try
                {
                    if (HomeState.Loaded && HomeHealth.Ill)
                    {
                        HomeHealth.Over(false);
                    }
                    if (ModState.IsSick && LifeEvents.Instance != null)
                    {
                        LifeEvents.Instance.Cure(null);
                    }
                    ModState.RemoveModifier(ModState.Injured);
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Cure", e);
                }
            }

            private static HealthFactors Living(HealthFactors f)
            {
                try
                {
                    if (!HomeState.Loaded || !Settings.HomeLife)
                    {
                        return f;
                    }
                    f.SleepDebt = Settings.HomeSleep ? (int)Math.Round(HomeState.SleepDebt) : 0;
                    if (Settings.HomeFitness)
                    {
                        float diet = HomeState.DietScore;
                        f.Diet = diet <= HomeBalance.DietPoor ? 0 : diet >= HomeBalance.DietGood ? 2 : 1;
                        f.Fitness = (int)Math.Round(HomeState.Fitness);
                    }
                    f.Treated = HomeHealth.Ill && HomeState.IllTreated;
                    return f;
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Living", e);
                    return f;
                }
            }

            // Home's chances with Health running: a draw from the save's seed, the day and the chance's own slot.
            private static float Roll(int slot)
            {
                try
                {
                    HealthDirector d = HealthDirector.Instance;
                    if (d != null && d.Running)
                    {
                        return HealthRules.Unit(d.State.Seed, HealthGame.Day, slot);
                    }
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Roll", e);
                }
                return UnityEngine.Random.value;   // health is not running: the game's own random number, as before
            }

            // The line beside the needs: asked a second, written again only when health's revision moved.
            private static string StatusNote()
            {
                HealthDirector d = HealthDirector.Instance;
                if (d == null)
                {
                    return null;
                }
                if (d.Revision != _statusAt)
                {
                    _statusAt = d.Revision;
                    try
                    {
                        _status = d.StatusLine();
                    }
                    catch (Exception e)
                    {
                        Fault("HealthLinks.StatusNote", e);
                        _status = null;
                    }
                }
                return _status;
            }

            private static DialogEntry Admission()
            {
                try
                {
                    return HealthDoctor.Admission();
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Admission", e);
                    return null;
                }
            }

            private static DialogEntry DoctorFirst(Func<DialogEntry> before)
            {
                try
                {
                    return HealthDoctor.Call(before);
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.DoctorFirst", e);
                    return null;
                }
            }

            private static int Wait(int usual)
            {
                try
                {
                    return HealthDirector.Instance != null ? HealthDirector.Instance.WaitMinutes(usual) : usual;
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Wait", e);
                    return usual;
                }
            }

            private static bool DoctorNote(int day)
            {
                try
                {
                    if (!HomeState.Loaded || HomeState.SickNoteUntil <= 0f)
                    {
                        return false;
                    }
                    int today = TimeHelper.CurrentDay;
                    float dayBegan = TimeHelper.NowInMinutes() - (today - day) * 1440f;
                    return day >= today ? HomeHealth.SickNoteActive : HomeState.SickNoteUntil >= dayBegan;
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.DoctorNote", e);
                    return false;
                }
            }

            // Ill that day by the mod's older rules: sick now, or for a day gone by still sick when that day began.
            private static bool WasIll(int day)
            {
                try
                {
                    int today = TimeHelper.CurrentDay;
                    float dayBegan = TimeHelper.NowInMinutes() - (today - day) * 1440f;
                    if (day >= today)
                    {
                        return ModState.IsSick || (HomeState.Loaded && HomeHealth.Ill);
                    }
                    return (ModState.SickUntil > 0f && ModState.SickUntil >= dayBegan) || (HomeState.Loaded && HomeHealth.Ill && HomeState.IllSince <= dayBegan + 1440f);
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.WasIll", e);
                    return false;
                }
            }

            // ---- the working life

            // The leave rules decide whether a day off is excused while Health runs; otherwise the answer as it was before.
            private sealed class Leave : ICareerSickNote
            {
                public bool Covers(int day)
                {
                    HealthDirector d = HealthDirector.Instance;
                    if (d != null && d.Running)
                    {
                        return d.Covers(day);
                    }
                    return _noteBefore != null ? _noteBefore.Covers(day) : ModState.IsSick;
                }
            }

            // The plan an employer gives: the public sector's and a tech company's that grants options give the standard plan, and so does the insurance asked for and
            // granted on the career ladder. With the day the job began.
            private static int Employer(out int since)
            {
                since = 0;
                try
                {
                    CareersDirector c = CareersDirector.Instance;
                    Careers.Job j = c != null && c.Work != null ? c.Work.Job : null;
                    if (j != null && j.Def != null && (j.Def.Public || j.Options > 0))
                    {
                        since = j.Hired;
                        return (int)Plan.Standard;
                    }
                    LadderFile f = c != null ? c.Ladder : null;
                    if (f != null && f.Insured)
                    {
                        since = f.Hired;
                        return (int)Plan.Standard;
                    }
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Employer", e);
                }
                return 0;
            }

            private static bool ShiftOpen()
            {
                try
                {
                    int day = TimeHelper.CurrentDay;
                    if (CareerState.Loaded && CareerState.ShiftOpen(day))
                    {
                        return true;
                    }
                    CareersDirector c = CareersDirector.Instance;
                    Careers.Job j = c != null && c.Work != null ? c.Work.Job : null;
                    return j != null && j.Def != null && ProRules.WorkDay(j.Def, day) && day >= j.Hired && j.SettledDay < day && !(j.TodayDay == day && j.TodayMinutes > 0);
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.ShiftOpen", e);
                    return false;
                }
            }

            // How risky a profession's work is, by its first skill: the building trades, warehouse work and guarding the most, kitchens, wards and driving some.
            private static int Risk(int skill)
            {
                switch (skill)
                {
                    case Skills.Electrical:
                    case Skills.Plumbing:
                    case Skills.Joinery:
                    case Skills.Masonry:
                    case Skills.Handling:
                    case Skills.Guarding:
                        return 2;
                    case Skills.Cooking:
                    case Skills.Clinical:
                    case Skills.Driving:
                        return 1;
                    default:
                        return 0;
                }
            }

            private static int SickPolicy()
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    return company != null && company.State != null ? company.State.SickPolicy : 1;
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.SickPolicy", e);
                    return 1;
                }
            }

            private static long Reserved()
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? Giving.GivingRules.Reserve(tech.FloorNow) : 0L;   // cents to whole money, rounded up, as a gift keeps it
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Reserved", e);
                    return 0L;
                }
            }

            // One of the city's hospitals or clinics, the first as the city lists them (the same on every machine): where the working life finds its wards.
            private static bool Clinic(out string street, out int number)
            {
                street = string.Empty;
                number = 0;
                try
                {
                    IReadOnlyList<EmployerRef> places = WorkGame.PublicPlaces();
                    for (int i = 0; i < places.Count; i++)
                    {
                        if (places[i].Type == ProCatalogue.HospitalType || places[i].Type == ProCatalogue.ClinicType)
                        {
                            street = places[i].Street;
                            number = places[i].Number;
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Clinic", e);
                }
                return false;
            }

            // ---- the medical debt

            private sealed class MedicalDebt : ICreditAccount
            {
                public string Key => "medical";
                public string NameKey => "reallife_hc_debt_name";
                public float Balance => Owed(out int _);
                public float Limit => Owed(out int _);
                public float PaymentDue
                {
                    get
                    {
                        float owed = Owed(out int perDay);
                        return Math.Min(owed, perDay);
                    }
                }

                private static float Owed(out int perDay)
                {
                    perDay = 0;
                    HealthDirector d = HealthDirector.Instance;
                    return d != null && d.DebtLine(out long owed, out perDay, out int _) ? owed : 0f;
                }
            }

            private static void Debts(List<DebtLine> into)
            {
                try
                {
                    _debtsBefore?.Invoke(into);
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Debts (the debts before)", e);
                }
                try
                {
                    HealthDirector d = HealthDirector.Instance;
                    if (d != null && d.DebtLine(out long owed, out int perDay, out int late))
                    {
                        into.Add(new DebtLine { NameKey = "reallife_hc_debt_name", Owed = (int)Math.Min(int.MaxValue / 2, owed), PerDay = perDay, LateDays = late });
                    }
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Debts", e);
                }
            }

            private static void Accounts(List<ICreditAccount> into)
            {
                try
                {
                    _accountsBefore?.Invoke(into);
                    into.Add(Debt);
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Accounts", e);
                }
            }

            // The credit file hears of the medical debt: -1 the account again, 0 opened, 1 an instalment paid, 2 missed, 3 paid off, 4 before the court.
            private static void Credit(int kind, int amount)
            {
                try
                {
                    if (!CreditLedger.Ready || SaveGameManager.Current == null)
                    {
                        return;
                    }
                    CreditLedger.Register(Debt);   // again, in case the credit file was opened afresh since the city loaded
                    int day = SaveGameManager.Current.Day;
                    switch (kind)
                    {
                        case 0: CreditLedger.Post(CreditKind.NewCredit, amount, day, Debt.Key); break;
                        case 1: CreditLedger.Post(CreditKind.PaidOnTime, amount, day, Debt.Key); break;
                        case 2: CreditLedger.Post(CreditKind.Missed, amount, day, Debt.Key); break;
                        case 3: CreditLedger.Post(CreditKind.PaidOff, 0, day, Debt.Key); break;
                        case 4: CreditLedger.Post(CreditKind.ClaimFiled, amount, day, Debt.Key); break;
                    }
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Credit", e);
                }
            }

            // ---- the rivals

            // A tycoon's worth while they are forward and the rivals are not resting (a Going Public session); -1 otherwise.
            private static long RivalWorth(int index)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null && !GoingPublic.InSession ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    return t != null && t.Active ? t.Worth : -1L;
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.RivalWorth", e);
                    return -1L;
                }
            }

            // A hospital's bill leaves a tycoon's worth, from the part over their floor only (the rivals raise a worth to the floor each day, so a bill from the floor
            // itself would be made up by the morning). Returns what really left them.
            private static long RivalPay(int index, long amount)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    if (t == null || !t.Active)
                    {
                        return 0L;
                    }
                    long paid = HealthRules.RivalPaid(amount, t.Worth, RivalRules.At(t.Def.StartWorth, Prices.Level));
                    t.Worth -= paid;
                    return paid;
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.RivalPay", e);
                    return 0L;
                }
            }

            // Off sick: no move of theirs before the day they are back, and their fortune grows at half the pace until then (the rivals' own two counters).
            private static void RivalRest(int index, int until)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    if (t != null && t.Active)
                    {
                        t.QuietUntil = Math.Max(t.QuietUntil, until);
                        t.SlowUntil = Math.Max(t.SlowUntil, until);
                    }
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.RivalRest", e);
                }
            }

            // A rival who went into hospital yesterday, in the morning's City Ledger, after the others' pieces.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Paper (the pieces before)", e);
                }
                try
                {
                    HealthDirector director = HealthDirector.Instance;
                    if (director == null)
                    {
                        return;
                    }
                    director.Stories(day, Stories);
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        into.Add(new NewsItem { Key = Stories[i].Key, Data = Stories[i].Data });
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("HealthLinks.Paper", e);
                }
            }

            // ---- the Planner (and through it the Calendar)

            /// <summary>The doctor's appointment, the day the plan's premium is taken and the morning of a discharge.</summary>
            private sealed class CareSource : IPlannerSource
            {
                public string Id => "healthcare";
                public bool Available => HealthDirector.Instance != null && HealthDirector.Instance.Running;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    HealthState s = HealthDirector.Instance.State;
                    HealthAppointment a = s.Appointment;
                    if (a.Booked && a.Day >= fromDay && a.Day <= toDay)
                    {
                        PlannerEntry e = into.Add(Id, 1, a.Day, PlannerCategory.Health, a.Day);
                        if (e != null)
                        {
                            e.Start = a.Hour * 60;
                            e.End = a.Hour * 60 + HealthCatalogue.AppointmentLate;
                            e.Attend = true;
                            e.Done = a.Kept;
                            e.Street = a.Street;
                            e.Number = a.Number;
                            if (into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_hc_planner_gp_title");
                                e.What = PlannerText.T("reallife_hc_planner_gp_what");
                                e.Where = a.Number + " " + a.Street;
                                e.IfMissed = HealthText.Fill("reallife_hc_planner_gp_missed", "amount", HealthGame.Money(HealthRules.At(HealthCatalogue.MissedFee, HealthGame.Level)));
                            }
                        }
                    }
                    HealthPolicy p = s.Policy;
                    if (p.Plan != Plan.None && p.NextDue >= fromDay && p.NextDue <= toDay)
                    {
                        PlannerEntry e = into.Add(Id, 2, p.NextDue, PlannerCategory.Money, p.NextDue);
                        if (e != null)
                        {
                            int premium = HealthCover.Premium(p.Plan, HealthGame.Age, HealthCover.ClaimsLastYear(p, p.NextDue, HealthGame.DaysPerYear), HealthGame.Level);
                            e.Owes = premium;
                            if (into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_hc_planner_premium_title");
                                e.What = HealthText.Fill("reallife_hc_planner_premium_what", "plan", HealthText.PlanName(p.Plan));
                                e.Money = HealthText.Fill("reallife_hc_planner_costs", "amount", HealthGame.Money(premium));
                                e.IfMissed = HealthText.Fill("reallife_hc_planner_premium_missed", "count", HealthCatalogue.LapseAfter.ToString());
                            }
                        }
                    }
                    HealthStay st = s.Stay;
                    int outDay = st.Until / 1440;
                    if (st.Active && outDay >= fromDay && outDay <= toDay)
                    {
                        PlannerEntry e = into.Add(Id, 3, outDay, PlannerCategory.Health, outDay);
                        if (e != null)
                        {
                            e.Start = st.Until % 1440;
                            e.End = e.Start;
                            if (into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_hc_planner_out_title");
                                e.What = PlannerText.T("reallife_hc_planner_out_what");
                            }
                        }
                    }
                }
            }
        }
    }
}
