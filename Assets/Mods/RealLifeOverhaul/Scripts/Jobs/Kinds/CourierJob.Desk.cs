using RealLifeOverhaul.SideJobs;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The courier round as a shift signed on for at the parcel depot's desk (<see cref="DeskShift"/>, Scripts/SideJobs): run after
    /// run in the vehicle that was signed on with, each run collected at one of the city's open businesses (parcels from a shop,
    /// meals from a place that cooks), as many drops as the vehicle has room for, each drop with its own time and paid by its
    /// leg and how punctual it was, and each run told to the depot's books. Every sum is SideJobRules'; nothing here is drawn
    /// beyond the doors the round already picks, and nothing runs but when a run is planned, loaded, delivered or paid.
    /// </summary>
    public sealed partial class CourierJob
    {
        private const float PickupMin = 40f;         // metres from the driver a run's pick-up may be
        private const float PickupMax = 420f;        // metres

        private bool _food;             // this run's load is meals: fewer, quicker, better paid
        private int _deskBase;          // the doors' pay of the drops made on this run, by punctuality
        private int _deskDistance;      // ...and their legs'
        private int _deskBonus;         // ...and what the early ones added
        private int _late;              // drops of this run made after their time
        private int _runs;              // runs paid this shift

        // True between a desk shift's runs, with nothing collected: stopping then leaves no work undone.
        private bool DeskIdle => Ctx != null && Ctx.Desk != null && _step == Step.ToDepot;

        // What the vehicle signed on with has room for, and no more than the desk's day has left.
        private int DeskRoom()
        {
            return Ctx.Desk.Test ? Ctx.Desk.Drops : Mathf.Min(Ctx.Desk.Drops, Ctx.Desk.Left());
        }

        // The vehicle a desk shift is driven in: the one signed on with, at the wheel or standing near. Never a hired one.
        private CarController DeskCar(out string reasonKey)
        {
            CarController car = JobDirector.NearestCar(SideJobRules.CarWithin);
            reasonKey = car == null || !Ctx.Desk.Fits(car) ? "reallife_job_desk_no_car_near" : null;
            return reasonKey == null ? car : null;
        }

        // A run of a desk shift: the pick-up is a business of the city's that is open, its kind says what is carried, then the doors.
        private bool DeskRun(int want)
        {
            if (want <= 0 || !JobSpots.RandomDoor(Ctx.PlayerPosition, PickupMin, PickupMax, null, LaneWithin, out _depot, JobSites.AnyClient))
            {
                return false;
            }
            BuildingRegistration business = _depot.Building != null ? _depot.Building.buildingRegistration : null;
            _food = business != null && SideJobRules.IsFood(business.businessTypeName);
            _deskBase = 0;
            _deskDistance = 0;
            _deskBonus = 0;
            _late = 0;
            if (!PlanRound(_food ? Mathf.Min(want, SideJobRules.FoodMost) : want))
            {
                return false;
            }
            JobText.Toast(NotificationType.Info, _food ? "reallife_job_desk_run_food" : "reallife_job_desk_run_parcels", "place", JobSites.NameOf(_depot), "count", _count.ToString(), 9f);
            Ctx.Logger?.Info("Jobs: courier desk run " + (_runs + 1) + ": " + _count + (_food ? " meal(s)" : " parcel(s)") + " from " + JobSites.NameOf(_depot) + " (" + _depot.Name
                             + "), room for " + Ctx.Desk.Drops + ", " + _route.ToString("F0") + " m planned.");
            return true;
        }

        // The planned order gives each drop its leg and its time (the round's own nearest-neighbour walk calls this for each).
        private void DeskLeg(int drop, int place, float leg)
        {
            if (Ctx.Desk == null)
            {
                return;
            }
            _drops[drop].Leg = leg;
            _drops[drop].DueIn = SideJobRules.DueIn(_route, place, _food);
        }

        // Loaded: every drop's clock starts, and the depot calls the run back only well after its own time.
        private void DeskLoaded(float now)
        {
            if (Ctx.Desk == null)
            {
                return;
            }
            for (int i = 0; i < _count; i++)
            {
                _drops[i].DueAt = now + _drops[i].DueIn;
            }
            _endsAt = now + _window * SideJobRules.RunOvertime;
        }

        // The seconds the drop being headed for still has; nothing once it is late.
        private int DeskSeconds(float now)
        {
            return _target >= 0 && _target < _count ? Mathf.Max(0, Mathf.CeilToInt(_drops[_target].DueAt - now)) : 0;
        }

        // A drop made: paid by its leg and by when it came.
        private void DeskDelivered(int drop, float now)
        {
            if (Ctx.Desk == null)
            {
                return;
            }
            float late = now - _drops[drop].DueAt;
            int mille = SideJobRules.Punctual(late, _drops[drop].DueIn);
            int kept = Mathf.Min(mille, 1000);
            int door = SideJobRules.DropPay(Def.UnitPay, 0f, 0f, Ctx.Rate, kept, _food);
            int whole = SideJobRules.DropPay(Def.UnitPay, Def.PayPerMetre, _drops[drop].Leg, Ctx.Rate, kept, _food);
            int paid = SideJobRules.DropPay(Def.UnitPay, Def.PayPerMetre, _drops[drop].Leg, Ctx.Rate, mille, _food);
            _deskBase += door;
            _deskDistance += whole - door;
            _deskBonus += Mathf.Max(0, paid - whole);
            if (late > 0f)
            {
                _late++;
                JobText.Toast(NotificationType.Warning, "reallife_job_desk_drop_late", "pay", JobText.Money(paid), 6f);
            }
            Ctx.Logger?.Info("Jobs: courier desk drop " + _delivered + " of " + _count + ": leg " + _drops[drop].Leg.ToString("F0") + " m, " + (late > 0f ? late.ToString("F0") + " s late"
                             : (-late).ToString("F0") + " s to spare") + " of " + _drops[drop].DueIn.ToString("F0") + " s, " + mille + " thousandths, pays " + paid + ".");
        }

        // What the card shows as earned on this run so far.
        private int DeskEarned() => _deskBase + _deskDistance + _deskBonus;

        // A desk shift's slip for the run: the drops' own sums in place of the round's flat ones; crashes and stops cost as on any round.
        private void DeskSlip(ref PaySlip slip)
        {
            if (Ctx.Desk == null)
            {
                return;
            }
            slip.Base = _deskBase;
            slip.Distance = _deskDistance;
            slip.Bonus = _deskBonus;
            int each = _delivered > 0 ? (_deskBase + _deskDistance) / _delivered : 0;
            slip.Penalty = Mathf.RoundToInt(each * (_crashes * JobCatalogue.CrashCut + _stops * JobCatalogue.PoliceStopCut) * 2f);
        }

        // The depot's books hear of a run as it is paid: what the clients paid for it and what the depot kept.
        private void DeskBooked(in PaySlip slip)
        {
            if (Ctx.Desk == null)
            {
                return;
            }
            _runs++;
            int pay = slip.Total - slip.Tips > 0 ? slip.Total - slip.Tips : 0;
            int gross = SideJobRules.ClientPaid(pay);
            Ctx.Desk.Booked(new DeskTake { Units = slip.Units, Gross = gross, Kept = gross - pay, Tips = slip.Tips, Net = slip.Total, Metres = Mathf.RoundToInt(_route) });
        }

        // A whole run is done and paid: the next one, if the day, the driver and the city have one. False ends the shift as done.
        private bool DeskNextRun(float now)
        {
            if (Ctx.Desk == null)
            {
                return false;
            }
            if (!Ctx.Desk.Test && Ctx.Desk.Left() <= 0)
            {
                Ctx.Say("reallife_job_desk_day_done", 9f);
                return false;
            }
            if (SaveGameManager.Current.Energy < JobCatalogue.MinEnergy)
            {
                Ctx.Say("reallife_job_done_tired", 9f);
                return false;
            }
            int late = _late;
            _paid = false;
            _delivered = 0;
            _crashes = 0;
            _stops = 0;
            _tips = 0;
            _target = -1;
            _shownTarget = -2;
            _shownDelivered = -1;
            _hint = -1;
            _box = null;
            if (!DeskRun(DeskRoom()))
            {
                _count = 0;
                _paid = true;   // nothing of a run that never was is owed
                Ctx.Say("reallife_job_desk_no_run", 9f);
                return false;
            }
            _step = Step.ToDepot;
            _panelDepot = JobText.T("reallife_job_courier_panel_depot", "place", _depot.Name);
            Show(JobText.T("reallife_job_courier_obj_depot", "count", _count.ToString(), "place", _depot.Name));
            Mark(_depot.Front, _depotLabel);
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs check (desk run): run " + _runs + " paid with " + late + " late drop(s); the next is planned at once, " + Ctx.Desk.Left() + " left of the desk's day: PASS");
            }
            return true;
        }
    }
}
