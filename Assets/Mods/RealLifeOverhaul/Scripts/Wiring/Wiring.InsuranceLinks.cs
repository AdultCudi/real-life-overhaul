using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Health;
using RealLifeOverhaul.Insurance;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Rivals;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Insurance (Insurance/InsuranceBridges) with the rest of the mod:
        ///
        ///   Grade, Points          the driver's record: the credit grade and the points on the licence
        ///   Cars, InsureCar,
        ///   EndCar, DrivenCar      the Garage's cars and its policies (CarGarage, CarPhone)
        ///   CarCover.*             the Garage asks what a car's tier makes of its premium, tells of a purchase and a renewal, and asks about a missed premium and a repair
        ///   CrashConsequences.Cover   the damages to another car, before they are taken
        ///   VehicleTow.Cover       a tow's fee, before it is taken
        ///   LifeEvents.RepairCover a life event's repair bill, before it is taken
        ///   DistrictIncome         a home's district, for its premium
        ///   Health, TakeHealth,
        ///   CancelHealth           Health's plan, shown and asked for in the app by Health's own calls
        ///   Business*              the Company's business cover, shown and asked for in the app by the Company's own calls
        ///   Rival*                 the rival tycoons hold a health plan and pay for it from their own worth
        ///   HealthBridges.RivalPay a tycoon's hospital bill costs them only what their plan leaves
        ///   CoverLine              the tycoons' line on the rich list and their panel
        ///   the Planner            the day a home's premium is taken (a car's is the Calendar's own)
        ///
        /// Asked on a day's pass, a claim or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class InsuranceLinks
        {
            private static Func<int, long, long> _rivalPayBefore;
            private static readonly HealthFacts HealthRead = new HealthFacts();
            private static readonly PremiumSource Planned = new PremiumSource();

            internal static void Connect()
            {
                InsuranceBridges.Grade = () => (int)CreditLedger.Grade;
                InsuranceBridges.Points = () => LicenceState.Loaded && SaveGameManager.Current != null ? LicenceState.PointsOn(SaveGameManager.Current.Day) : 0;
                InsuranceBridges.Cars = Cars;
                InsuranceBridges.InsureCar = InsureCar;
                InsuranceBridges.EndCar = EndCar;
                InsuranceBridges.DrivenCar = DrivenCar;
                InsuranceBridges.DistrictIncome = district => Mathf.RoundToInt(CityEconomy.DistrictIncome(district));
                InsuranceBridges.Health = Health;
                InsuranceBridges.TakeHealth = plan => Safe("InsuranceLinks.TakeHealth", () => RealLifeOverhaul.Ui.HealthLive.Instance.TakePlan(plan));
                InsuranceBridges.CancelHealth = () => Safe("InsuranceLinks.CancelHealth", () => RealLifeOverhaul.Ui.HealthLive.Instance.CancelPlan());
                InsuranceBridges.Business = Business;
                InsuranceBridges.BusinessClaims = BusinessClaims;
                InsuranceBridges.BuyBusiness = (place, kind, insurer) => Safe("InsuranceLinks.BuyBusiness", () => CompanyDirector.Instance?.BuyCover(place, kind, insurer));
                InsuranceBridges.CancelBusiness = (place, kind) => Safe("InsuranceLinks.CancelBusiness", () => CompanyDirector.Instance?.CancelCover(place, kind));
                InsuranceBridges.FileBusinessClaim = id => Safe("InsuranceLinks.FileBusinessClaim", () => CompanyDirector.Instance?.FileClaim(id));
                InsuranceBridges.RivalWorth = RivalWorth;
                InsuranceBridges.RivalFloor = RivalFloor;
                InsuranceBridges.RivalPay = RivalPay;
                InsuranceBridges.RivalName = index => index >= 0 && index < RivalCatalogue.TycoonCount ? RivalCatalogue.Tycoons[index].Name : string.Empty;
                InsuranceBridges.RivalQuote = RivalQuote;
                InsuranceBridges.RivalClaim = RivalClaim;
                InsuranceBridges.PlanName = plan => plan > 0 && plan < (int)Plan.Count ? HealthText.PlanName((Plan)plan) : string.Empty;
                RivalBridges.CoverLine = tycoon => InsuranceDirector.Instance != null ? InsuranceDirector.Instance.RivalLine(tycoon) : string.Empty;

                CarCover.Premium = Premium;
                CarCover.Bought = (id, damage, fresh, charged) => Safe("InsuranceLinks.Bought", () => InsuranceDirector.Instance?.CarBought(id, damage, fresh, charged));
                CarCover.Renewed = (id, charged) => Safe("InsuranceLinks.Renewed", () => InsuranceDirector.Instance?.CarRenewed(id, charged));
                CarCover.Missed = Missed;
                CarCover.Repair = Repair;
                CrashConsequences.Cover = Liability;
                VehicleTow.Cover = Tow;
                LifeEvents.RepairCover = RepairCover;
                _rivalPayBefore = HealthBridges.RivalPay;
                HealthBridges.RivalPay = RivalHospital;
                PlannerDirector.Instance?.AddSource(Planned);
            }

            internal static void Disconnect()
            {
                InsuranceBridges.Reset();
                RivalBridges.CoverLine = null;
                CarCover.Reset();
                CrashConsequences.Cover = null;
                VehicleTow.Cover = null;
                LifeEvents.RepairCover = null;
                HealthBridges.RivalPay = _rivalPayBefore;
                _rivalPayBefore = null;
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

            // ---- the Garage's cars

            // The player's own cars as the Garage's page lists them, each with its paid month and the Garage's premium before the tier.
            private static bool Cars(List<CarLook> into)
            {
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    if (!Settings.CarOwnership || !CarGarage.Loaded || save == null || save.VehicleInstances == null)
                    {
                        return false;
                    }
                    int today = save.Day;
                    for (int i = 0; i < save.VehicleInstances.Count; i++)
                    {
                        VehicleInstance car = save.VehicleInstances[i];
                        if (!CarPhone.Runs(car))
                        {
                            continue;
                        }
                        CarRecord record = CarGarage.For(car.id, today);
                        into.Add(new CarLook
                        {
                            Id = car.id, Name = car.vehicleTypeName.GetLocalization(), Price = car.VehicleType.price, Damage = car.damage, Broken = record.Broken,
                            InsuredUntil = record.InsuredUntil, Premium = CarPhone.PremiumOf(car.VehicleType.price, record.Claims, GarageBridges.InsuranceOf(car.id))
                        });
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.Cars", e);
                    return false;
                }
            }

            private static VehicleInstance Car(string id)
            {
                GameInstance save = SaveGameManager.Current;
                for (int i = 0; save != null && save.VehicleInstances != null && i < save.VehicleInstances.Count; i++)
                {
                    VehicleInstance car = save.VehicleInstances[i];
                    if (car != null && car.id == id && CarPhone.Runs(car))
                    {
                        return car;
                    }
                }
                return null;
            }

            // The Garage's own purchase (CarPhone.Insure), so the premium, the booking and the paid month are the Garage's.
            private static float InsureCar(string id)
            {
                try
                {
                    VehicleInstance car = Settings.CarOwnership && CarGarage.Loaded ? Car(id) : null;
                    return car != null ? CarPhone.Insure(car, CarGarage.For(car.id, SaveGameManager.Current.Day), false) : -1f;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.InsureCar", e);
                    return -1f;
                }
            }

            private static void EndCar(string id)
            {
                try
                {
                    if (CarGarage.Loaded && CarGarage.Has(id))
                    {
                        CarGarage.For(id, SaveGameManager.Current.Day).InsuredUntil = 0;   // as the Garage's own page ends a policy
                    }
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.EndCar", e);
                }
            }

            // The car being driven, where it is one of the player's own: a hired or borrowed car, a dealer's stock and a partner's car have no policy of the player's.
            private static string DrivenCar()
            {
                try
                {
                    var game = InstanceBehavior<GameManager>.Instance;
                    var driven = game != null ? game.selectedVehicle as CarController : null;
                    VehicleInstance car = driven != null ? driven.vehicleInstance : null;
                    return car != null && Car(car.id) != null ? car.id : null;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.DrivenCar", e);
                    return null;
                }
            }

            private static float Premium(string id, float garage)
            {
                try
                {
                    InsuranceDirector d = InsuranceDirector.Instance;
                    return d != null ? d.CarPremium(id, garage) : garage;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.Premium", e);
                    return garage;
                }
            }

            private static bool Missed(string id, string name, float premium)
            {
                try
                {
                    InsuranceDirector d = InsuranceDirector.Instance;
                    return d != null && d.CarMissed(id, name, premium);
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.Missed", e);
                    return false;
                }
            }

            private static bool Repair(string id, string name, float price, float before, float after, bool reported, out float payout, out float excess)
            {
                payout = 0f;
                excess = 0f;
                try
                {
                    InsuranceDirector d = InsuranceDirector.Instance;
                    return d != null && d.CarRepair(id, name, price, before, after, reported, out payout, out excess);
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.Repair", e);
                    payout = 0f;
                    excess = 0f;
                    return false;
                }
            }

            // ---- the bills of the others

            private static int Liability(int damages)
            {
                try
                {
                    InsuranceDirector d = InsuranceDirector.Instance;
                    return d != null ? Mathf.Clamp(d.Liability(damages), 0, Mathf.Max(0, damages)) : 0;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.Liability", e);
                    return 0;
                }
            }

            private static int Tow(string id, int fee)
            {
                try
                {
                    InsuranceDirector d = InsuranceDirector.Instance;
                    return d != null ? Mathf.Clamp(d.Tow(id, fee), 0, Mathf.Max(0, fee)) : 0;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.Tow", e);
                    return 0;
                }
            }

            private static int RepairCover(int bill, bool atHome)
            {
                try
                {
                    InsuranceDirector d = InsuranceDirector.Instance;
                    return d != null ? Mathf.Clamp(d.HomeRepair(bill, atHome), 0, Mathf.Max(0, bill)) : 0;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.RepairCover", e);
                    return 0;
                }
            }

            // ---- Health's plan

            private static bool Health(HealthCoverFact h)
            {
                try
                {
                    HealthDirector d = HealthDirector.Instance;
                    if (d == null || !d.Facts(HealthRead))
                    {
                        return false;
                    }
                    HealthFacts f = HealthRead;
                    h.Plan = (int)f.Plan;
                    h.Employer = (int)f.Employer;
                    for (int i = 0; i < InsuranceCatalogue.Tiers && i < f.Plans.Length; i++)
                    {
                        int p = (int)f.Plans[i].Plan;
                        h.Names[i] = HealthText.PlanName(f.Plans[i].Plan);
                        h.Premium[i] = f.Plans[i].Premium;
                        h.HospitalShare[i] = HealthCatalogue.HospitalShare[p];
                        h.DoctorShare[i] = HealthCatalogue.DoctorShare[p];
                        h.Excess[i] = HealthRules.At(HealthCatalogue.Excess[p], f.Level);
                        h.YearCap[i] = HealthRules.At(HealthCatalogue.YearCap[p], f.Level);
                    }
                    h.NextDue = f.NextDue;
                    h.Missed = f.Missed;
                    h.WaitUntil = f.WaitUntil > f.Day ? f.WaitUntil - 1 : 0;
                    h.LapseAfter = HealthCatalogue.LapseAfter;
                    h.PaidYear = f.PlanPaidYear;
                    h.LastBill = f.LastBill;
                    h.LastClaim = f.LastClaim;
                    h.LastBillDay = f.LastBillDay;
                    return true;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.Health", e);
                    return false;
                }
            }

            // ---- the Company's business cover

            private static bool Business(List<BizCoverFact> into, string[] insurers)
            {
                try
                {
                    CompanyDirector d = CompanyDirector.Instance;
                    if (d == null || !d.Running || !d.On(CompanyPart.Cover))
                    {
                        return false;
                    }
                    for (int i = 0; i < insurers.Length && i < CompanyCatalogue.Insurers; i++)
                    {
                        insurers[i] = CompanyText.Insurer(i);
                    }
                    IReadOnlyList<CompanyShop> shops = d.Shops;
                    for (int s = 0; s < shops.Count; s++)
                    {
                        CompanyShop shop = shops[s];
                        for (int k = 0; k < shop.File.Cover.Length && k < CompanyCatalogue.CoverKinds; k++)
                        {
                            Policy p = shop.File.Cover[k];
                            var b = new BizCoverFact
                            {
                                Place = shop.File.Place, Name = shop.Name, Kind = k, KindName = CompanyText.Cover(k), Active = p.Active, Insurer = p.Insurer, Premium = p.Premium,
                                Until = p.Until, Claims = p.Claims, Most = CompanyCatalogue.CoverMost[k]
                            };
                            for (int n = 0; n < InsuranceCatalogue.Tiers && n < CompanyCatalogue.Insurers; n++)
                            {
                                b.Quote[n] = d.Quote(shop, k, n);
                                b.Writes[n] = CoverRules.WillWrite(p, n);
                                b.ExcessShare[n] = CompanyCatalogue.InsurerExcessShare[n];
                                b.ExcessLeast[n] = CompanyCatalogue.InsurerExcessLeast[n];
                                b.PayDays[n] = CompanyCatalogue.InsurerPayDays[n];
                            }
                            into.Add(b);
                        }
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.Business", e);
                    return false;
                }
            }

            private static void BusinessClaims(List<ClaimFact> into)
            {
                try
                {
                    CompanyDirector d = CompanyDirector.Instance;
                    CompanyState state = d != null ? d.State : null;
                    for (int i = 0; state != null && i < state.Claims.Count; i++)
                    {
                        Claim c = state.Claims[i];
                        into.Add(new ClaimFact
                        {
                            Day = c.Day, Line = (int)CoverLine.Business, Kind = c.Kind, What = c.Name, Loss = c.Loss, Excess = c.Excess, Payout = c.Payout, Id = c.Id,
                                KindName = CompanyText.Cover(c.Kind),
                            Stage = c.Stage == ClaimStage.Open ? 1 : c.Stage == ClaimStage.Filed ? 2 : c.Stage == ClaimStage.Paid ? 0 : 3,
                            StageDay = c.Stage == ClaimStage.Open ? c.Day + CompanyCatalogue.ClaimWindowDays : c.PayDay
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.BusinessClaims", e);
                }
            }

            // ---- the rivals

            private static Tycoon TycoonAt(int index)
            {
                RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                return f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
            }

            // A tycoon's worth while they are forward and the rivals are not resting (a Going Public session); -1 otherwise.
            private static long RivalWorth(int index)
            {
                try
                {
                    Tycoon t = GoingPublic.InSession ? null : TycoonAt(index);
                    return t != null && t.Active ? t.Worth : -1L;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.RivalWorth", e);
                    return -1L;
                }
            }

            // The start's worth at today's prices, which the rivals raise a worth to each day: a premium from the floor itself would be made up by the morning.
            private static long RivalFloor(int index)
            {
                try
                {
                    Tycoon t = TycoonAt(index);
                    return t != null ? RivalRules.At(t.Def.StartWorth, Prices.Level) : 0L;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.RivalFloor", e);
                    return 0L;
                }
            }

            private static long RivalPay(int index, long amount)
            {
                try
                {
                    Tycoon t = GoingPublic.InSession ? null : TycoonAt(index);
                    if (t == null || !t.Active)
                    {
                        return 0L;
                    }
                    long paid = Math.Max(0L, Math.Min(amount, t.Worth - RivalRules.At(t.Def.StartWorth, Prices.Level)));
                    t.Worth -= paid;
                    return paid;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.RivalPay", e);
                    return 0L;
                }
            }

            // Health's own premium for a plan, at a tycoon's age and with no claims counted; nothing while Health is off, so no tycoon buys a plan there is no use for.
            private static long RivalQuote(int plan)
            {
                HealthDirector d = HealthDirector.Instance;
                return d != null && d.Running && plan > 0 && plan < (int)Plan.Count ? HealthCover.Premium((Plan)plan, InsuranceCatalogue.RivalAge, 0, Prices.Level) : 0L;
            }

            // Health's own rule for what a plan pays of a hospital stay, given what it has paid this year.
            private static long RivalClaim(int plan, long bill, long paidYear)
            {
                return plan > 0 && plan < (int)Plan.Count ? HealthCover.Claim((Plan)plan, Care.Hospital, bill, paidYear, Prices.Level) : 0L;
            }

            // A tycoon's hospital bill (Health's day): their plan pays its part, and what is left leaves their worth as Health has always taken it.
            private static long RivalHospital(int index, long bill)
            {
                long theirs = bill;
                try
                {
                    InsuranceDirector d = InsuranceDirector.Instance;
                    theirs = d != null ? Math.Max(0L, Math.Min(bill, d.RivalBill(index, bill))) : bill;
                }
                catch (Exception e)
                {
                    Fault("InsuranceLinks.RivalHospital", e);
                }
                Func<int, long, long> before = _rivalPayBefore;
                return before != null ? before(index, theirs) : 0L;
            }

            // ---- the Planner (and through it the Calendar)

            /// <summary>The day each home's premium is taken.</summary>
            private sealed class PremiumSource : IPlannerSource
            {
                public string Id => "insurance";
                public bool Available => InsuranceDirector.Instance != null && InsuranceDirector.Instance.Running;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    InsuranceState s = InsuranceDirector.Instance.State;
                    for (int i = 0; i < s.Homes.Count; i++)
                    {
                        HomePolicy p = s.Homes[i];
                        if (!p.Active || p.NextDue < fromDay || p.NextDue > toDay)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, 1, i, PlannerCategory.Money, p.NextDue);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Owes = p.Premium;
                        if (into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_ins_planner_home_title");
                            e.What = InsuranceText.Fill("reallife_ins_planner_home_what", "tier", InsuranceText.HomeTier(p.Tier), "home", p.Name);
                            e.Money = PlannerText.T("reallife_planner_costs", "amount", InsuranceGame.Money(p.Premium));
                            e.IfMissed = InsuranceText.Fill("reallife_ins_planner_missed", "grace", InsuranceCatalogue.GraceDays.ToString());
                        }
                    }
                }
            }
        }
    }
}
