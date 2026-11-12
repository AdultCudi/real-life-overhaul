using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// The cars: the tier of each car's policy at the Garage. The Garage keeps the paid month and books the premium and a repair's payout as it always has; this part
    /// answers what the tier and the driver's record make of the premium, what a claim pays, and what becomes of a premium that could not be taken. Each entry is
    /// called by the feature whose bill it is (Wiring/InsuranceLinks), on a purchase, a renewal, a repair, a crash or a tow: never per frame.
    /// </summary>
    public sealed partial class InsuranceDirector
    {
        private readonly List<CarLook> _carLooks = new List<CarLook>(6);

        private static int Grade
        {
            get
            {
                Func<int> grade = InsuranceBridges.Grade;
                return InsuranceCatalogue.Clamp(grade != null ? grade() : InsuranceCatalogue.CreditUnknown, 0, InsuranceCatalogue.CreditShare.Length - 1);
            }
        }

        private static int Points
        {
            get
            {
                Func<int> points = InsuranceBridges.Points;
                return Math.Max(0, points != null ? points() : 0);
            }
        }

        private bool ReadCars()
        {
            _carLooks.Clear();
            Func<List<CarLook>, bool> cars = InsuranceBridges.Cars;
            return cars != null && cars(_carLooks);
        }

        private int LookOf(string id)
        {
            for (int i = 0; i < _carLooks.Count; i++)
            {
                if (_carLooks[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        // A policy the Garage holds that insurance has no record of: one from before the feature, or bought while it was off. It is the standard tier, the Garage's
        // own terms, with no first days and nothing held to be from before it.
        private CarPolicy Adopt(string id)
        {
            CarPolicy p = _state.Car(id);
            if (p == null)
            {
                p = new CarPolicy { Id = id, Live = true };
                _state.Cars.Add(p);
            }
            return p;
        }

        // The day's look at the Garage: a policy that ended there (cancelled on the Garage's page, or run out while nobody looked) ends here, one found insured that
        // was not is taken as new from today, and the record of a car the player no longer owns goes. A policy whose month ended yesterday is not ended here: the
        // Garage's own new day renews it or asks about the missed premium this morning, before or after this look, and says which (CarRenewed, CarMissed).
        private void SyncCars(int day)
        {
            if (!ReadCars())
            {
                return;   // car ownership is off or not read yet: the records wait as they are
            }
            for (int i = 0; i < _carLooks.Count; i++)
            {
                CarLook look = _carLooks[i];
                bool insured = look.InsuredUntil >= day;
                CarPolicy p = _state.Car(look.Id);
                if (p == null)
                {
                    if (insured)
                    {
                        Adopt(look.Id);
                    }
                    continue;
                }
                if (insured && !p.Live)
                {
                    p.Live = true;
                    p.Since = day;
                    p.Pre = InsuranceRules.Damage(look.Damage);
                    p.GraceFrom = 0;
                }
                else if (!insured && p.Live && look.InsuredUntil < day - 1)
                {
                    p.Live = false;
                    p.Since = 0;
                    p.GraceFrom = 0;
                }
            }
            for (int i = _state.Cars.Count - 1; i >= 0; i--)
            {
                if (LookOf(_state.Cars[i].Id) < 0)
                {
                    _state.Cars.RemoveAt(i);
                }
            }
        }

        private int FactorOf(CarPolicy p, int tier, int day)
        {
            return InsuranceRules.CarFactor(tier, Grade, Points, InsuranceRules.Crashes(_state.CrashDays, day), p != null ? p.NoClaimMonths : 0);
        }

        // ---- asked by the Garage

        /// <summary>The Garage's month's premium for a car as its tier and the driver's record make it: what the Garage shows, charges and renews at.</summary>
        public float CarPremium(string id, float garage)
        {
            if (!Running || string.IsNullOrEmpty(id))
            {
                return garage;
            }
            CarPolicy p = _state.Car(id);
            return InsuranceRules.CarPremium(garage, FactorOf(p, p != null ? p.Tier : InsuranceCatalogue.StandardTier, InsuranceGame.Day));
        }

        /// <summary>
        /// A policy was bought at the Garage (its page, Insure all, or this app through it). One bought for a car that had none begins today: its first days pay
        /// nothing, and the damage the car has now is never the insurer's.
        /// </summary>
        public void CarBought(string id, float damage, bool fresh, float charged)
        {
            if (!Running || string.IsNullOrEmpty(id))
            {
                return;
            }
            int day = InsuranceGame.Day;
            CarPolicy p = _state.Car(id);
            if (p == null)
            {
                p = new CarPolicy { Id = id };
                _state.Cars.Add(p);
                fresh = true;
            }
            if (fresh || !p.Live)
            {
                p.Since = day;
                p.Pre = InsuranceRules.Damage(damage);
            }
            p.Live = true;
            p.GraceFrom = 0;
            p.LastPremium = WholeMoney.Round(charged);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Insurance check (a car's policy bought, tier " + p.Tier + "): it began on day " + p.Since + " with " + p.Pre + " thousandths of damage held to be from before it; "
                             + "nothing is paid before day " + (p.Since + InsuranceCatalogue.CarWaitDays) + ": PASS");
            }
        }

        /// <summary>A month's renewal was paid at the Garage: a month without a claim is counted, and the days of grace are over.</summary>
        public void CarRenewed(string id, float charged)
        {
            if (!Running || string.IsNullOrEmpty(id))
            {
                return;
            }
            CarPolicy p = Adopt(id);
            p.NoClaimMonths = Math.Min(p.NoClaimMonths + 1, InsuranceCatalogue.NoClaimMost / InsuranceCatalogue.NoClaimStep);
            p.GraceFrom = 0;
            p.Live = true;
            p.LastPremium = WholeMoney.Round(charged);
            _revision++;
        }

        /// <summary>
        /// A renewal's premium could not be taken this morning. True while the cover holds in its days of grace (the Garage asks again tomorrow); false once they are
        /// over, when the Garage ends the policy and says so.
        /// </summary>
        public bool CarMissed(string id, string name, float premium)
        {
            if (!Running || string.IsNullOrEmpty(id))
            {
                return false;
            }
            int day = InsuranceGame.Day;
            CarPolicy p = Adopt(id);
            GraceStep step = InsuranceRules.Missed(ref p.GraceFrom, day);
            _revision++;
            if (step == GraceStep.Lapsed)
            {
                p.Live = false;
                p.Since = 0;
                return false;
            }
            p.Live = true;   // the cover holds through the days of grace
            if (step == GraceStep.Began)
            {
                InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_note_car_grace", InsuranceGame.Data("car", name, "amount", InsuranceGame.Money(WholeMoney.Round(premium)),
                    "day", InsuranceRules.GraceEnds(p.GraceFrom).ToString()), OpenCars);
            }
            return true;
        }

        /// <summary>
        /// The game mended an insured car and the Garage asks what its policy pays: the tier's share of the bill for the damage that came after the cover began, less
        /// the excess (none with a police report). False while insurance is off, when the Garage pays by its own terms.
        /// </summary>
        public bool CarRepair(string id, string name, float price, float before, float after, bool reported, out float payout, out float excess)
        {
            payout = 0f;
            excess = 0f;
            if (!Running || string.IsNullOrEmpty(id))
            {
                return false;
            }
            int day = InsuranceGame.Day, level = InsuranceGame.Level;
            CarPolicy p = Adopt(id);
            int was = InsuranceRules.Damage(before), now = InsuranceRules.Damage(after), pre = p.Pre;
            int drop = InsuranceRules.CoveredDrop(was, now, pre);
            p.Pre = InsuranceRules.PreAfter(pre, now);
            long bill = WholeMoney.Round(VehicleCostRules.Repair(drop / 1000f, price));
            long pays = InsuranceRules.CarClaim(p, ClaimKind.Repair, bill, true, reported, day, InsuranceGame.DaysPerYear, level, out long borne, out ClaimWhy why);
            payout = pays;
            excess = borne;
            Claimed(CoverLine.Car, ClaimKind.Repair, name, bill, borne, pays, why, p.Since + InsuranceCatalogue.CarWaitDays, false);
            if (Verbose)
            {
                Logger?.Info("Insurance check (a repair, tier " + p.Tier + "): damage " + was + " to " + now + " thousandths, " + pre + " from before the cover, so " + drop
                             + " is the insurer's: a bill of " + bill + ", excess " + borne + ", pays " + pays + " (" + why + "): " + (pays <= bill ? "PASS" : "FAIL, more than the bill"));
            }
            return true;
        }

        // ---- asked by a crash and a tow

        /// <summary>
        /// The player hit another car and stopped: the damages are about to be taken. The crash goes on the driver's record whatever they drive; where the car is
        /// their own and insured, its policy pays its part. Returns what the policy pays, for the bill to be taken less it.
        /// </summary>
        public int Liability(int damages)
        {
            if (!Running || damages <= 0)
            {
                return 0;
            }
            int day = InsuranceGame.Day;
            InsuranceRules.NoteCrash(_state.CrashDays, day);
            _revision++;
            Func<string> driven = InsuranceBridges.DrivenCar;
            return (int)CarLoss(driven != null ? driven() : null, ClaimKind.Liability, damages, day);
        }

        /// <summary>A tow's fee is about to be taken for a car: what its policy pays of it.</summary>
        public int Tow(string id, int fee)
        {
            if (!Running || fee <= 0)
            {
                return 0;
            }
            return (int)CarLoss(id, ClaimKind.Tow, fee, InsuranceGame.Day);
        }

        private long CarLoss(string id, ClaimKind kind, long loss, int day)
        {
            if (string.IsNullOrEmpty(id) || !ReadCars())
            {
                return 0;
            }
            int at = LookOf(id);
            if (at < 0 || _carLooks[at].InsuredUntil < day)
            {
                return 0;   // a hired or borrowed car, or one with no policy today
            }
            CarPolicy p = Adopt(id);
            long pays = InsuranceRules.CarClaim(p, kind, loss, true, false, day, InsuranceGame.DaysPerYear, InsuranceGame.Level, out long borne, out ClaimWhy why);
            Claimed(CoverLine.Car, kind, _carLooks[at].Name, loss, borne, pays, why, p.Since + InsuranceCatalogue.CarWaitDays, false);
            if (Verbose)
            {
                Logger?.Info("Insurance check (" + kind + ", tier " + p.Tier + "): a bill of " + loss + ", excess " + borne + ", pays " + pays + " (" + why + "), "
                             + p.ClaimsYear + " claim(s) and " + p.PaidYear + " paid this year: " + (pays <= loss ? "PASS" : "FAIL, more than the bill"));
            }
            return pays;
        }

        // A claim was worked out: one that paid goes into the history; one refused for the first days or the year's limit is said, so the full bill is no surprise.
        private void Claimed(CoverLine line, ClaimKind kind, string what, long loss, long borne, long pays, ClaimWhy why, int paysFrom, bool say)
        {
            if (pays > 0)
            {
                InsuranceRules.Note(_state, InsuranceGame.Day, line, kind, what, loss, borne, pays);
                if (say)
                {
                    InsuranceGame.Tell(NotificationType.Success, "reallife_ins_note_home_claim", InsuranceGame.Data("home", what, "payout", InsuranceGame.Money(pays),
                        "bill", InsuranceGame.Money(loss), "amount", InsuranceGame.Money(loss - pays)), OpenClaims);
                }
            }
            else if (why == ClaimWhy.Waiting)
            {
                InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_note_refused_wait", InsuranceGame.Data("cover", InsuranceText.Line(line), "what", what,
                    "day", paysFrom.ToString()), OpenClaims);
            }
            else if (why == ClaimWhy.YearLimit)
            {
                InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_note_refused_limit", InsuranceGame.Data("cover", InsuranceText.Line(line), "what", what), OpenClaims);
            }
            _revision++;
        }

        // ---- pressed in the app

        /// <summary>Insures a car at a tier through the Garage, which takes the first month. False if it is insured already or the money is not there.</summary>
        public bool BuyCar(string id, int tier, out int charged)
        {
            charged = 0;
            if (!Running || !ReadCars())
            {
                return false;
            }
            int at = LookOf(id), day = InsuranceGame.Day;
            if (at < 0 || _carLooks[at].InsuredUntil >= day)
            {
                return false;
            }
            CarPolicy p = _state.Car(id);
            bool made = p == null;
            if (made)
            {
                p = new CarPolicy { Id = id };
                _state.Cars.Add(p);
            }
            int was = p.Tier;
            p.Tier = InsuranceCatalogue.Tier(tier);   // before the Garage asks what the premium is
            Func<string, float> insure = InsuranceBridges.InsureCar;
            float paid = insure != null ? insure(id) : -1f;
            if (paid < 0f)
            {
                p.Tier = was;
                if (made)
                {
                    _state.Cars.Remove(p);
                }
                return false;
            }
            charged = WholeMoney.Round(paid);   // (the Garage has told CarBought, which set the day it began and the damage it began with)
            _revision++;
            return true;
        }

        /// <summary>
        /// Moves an insured car to another tier from now. Up: only a sound car (<paramref name="unsound"/> says why not), and the difference for the days left of
        /// the paid month is taken. Down: nothing is taken or given back.
        /// </summary>
        public bool ChangeCar(string id, int tier, out int charged, out bool unsound)
        {
            charged = 0;
            unsound = false;
            if (!Running || !ReadCars())
            {
                return false;
            }
            int at = LookOf(id), day = InsuranceGame.Day;
            tier = InsuranceCatalogue.Tier(tier);
            if (at < 0 || _carLooks[at].InsuredUntil < day)
            {
                return false;
            }
            CarLook look = _carLooks[at];
            CarPolicy p = Adopt(id);
            if (tier == p.Tier)
            {
                return false;
            }
            if (tier > p.Tier)
            {
                if (!InsuranceRules.Sound(InsuranceRules.Damage(look.Damage), look.Broken))
                {
                    unsound = true;
                    return false;
                }
                int before = WholeMoney.Round(InsuranceRules.CarPremium(look.Premium, FactorOf(p, p.Tier, day)));
                int after = WholeMoney.Round(InsuranceRules.CarPremium(look.Premium, FactorOf(p, tier, day)));
                int due = InsuranceRules.UpgradeCharge(before, after, look.InsuredUntil - day + 1, InsuranceGame.MonthDays);
                if (!InsuranceGame.Pay(due, InsuranceGame.CarKey))
                {
                    return false;
                }
                charged = due;
                p.LastPremium = after;
            }
            p.Tier = tier;
            _revision++;
            return true;
        }

        /// <summary>Ends a car's policy at the Garage. Nothing is given back; its record stays, so buying again gives no new year and begins with new first days.</summary>
        public bool CancelCar(string id)
        {
            if (!Running || !ReadCars())
            {
                return false;
            }
            int at = LookOf(id);
            if (at < 0 || _carLooks[at].InsuredUntil < InsuranceGame.Day)
            {
                return false;
            }
            InsuranceBridges.EndCar?.Invoke(id);
            CarPolicy p = _state.Car(id);
            if (p != null)
            {
                p.Live = false;
                p.Since = 0;
                p.GraceFrom = 0;
            }
            _revision++;
            return true;
        }
    }
}
