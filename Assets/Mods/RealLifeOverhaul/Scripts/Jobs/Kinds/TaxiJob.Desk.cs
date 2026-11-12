using RealLifeOverhaul.SideJobs;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Ride-hail as a shift signed on for at the cab office's desk (<see cref="DeskShift"/>, Scripts/SideJobs): driven only in the
    /// car that was signed on with, the fare from the meter by distance and time with the office's share off it, the ride marked
    /// down for lateness and for what the police booked, the tip by that mark, and each fare told to the office's books. Every
    /// sum is SideJobRules'; nothing here is drawn, and nothing runs but when a fare begins, boards or is paid.
    /// </summary>
    public sealed partial class TaxiJob
    {
        private float _pickupUsed;      // the share of the time to the pick-up that had gone when the passenger got in
        private int _offences;          // offences on a police bill during this ride
        private bool _fled;             // ...and whether the driver had driven off from them first
        private bool _otherCarShown;

        // The car a desk shift is driven in: the one signed on with, at the wheel or standing near. Never a hired van.
        private CarController DeskCar(out string reasonKey)
        {
            CarController car = JobDirector.NearestCar(SideJobRules.CarWithin);
            reasonKey = car == null || !Ctx.Desk.Fits(car) ? "reallife_job_desk_no_car_near" : null;
            return reasonKey == null ? car : null;
        }

        // A new fare: what the last one's ride was marked with is forgotten.
        private void DeskFare()
        {
            _pickupUsed = 0f;
            _offences = 0;
            _fled = false;
            _otherCarShown = false;
        }

        // True, with the card saying so once, while a desk shift's driver has pulled up in another car than the one signed on with.
        private bool DeskOtherCar()
        {
            if (Ctx.Desk == null || Ctx.Desk.Fits(Ctx.Car))
            {
                return false;
            }
            if (!_otherCarShown)
            {
                _otherCarShown = true;
                Show(JobText.T("reallife_job_desk_obj_other_car"));
            }
            return true;
        }

        public override void OnOffences(int offences, bool fled)
        {
            if (Ctx.Desk != null && _step == Step.Riding)
            {
                _offences += offences;
                _fled |= fled;
            }
        }

        // The meter of a desk shift's ride. The slip carries what reaches the driver; the answer is what the passenger paid.
        private int DeskMeter(float seconds, float par, ref int comfort, ref PaySlip slip, out int kept)
        {
            int fare = SideJobRules.Fare(Def.UnitPay, Def.PayPerMetre, _metres, seconds, Ctx.Rate);
            kept = SideJobRules.Kept(fare);
            int flag = Mathf.Min(slip.Base, fare - kept);
            slip.Base = flag;
            slip.Distance = fare - kept - flag;
            int ridden = comfort;
            comfort = SideJobRules.Quality(comfort, seconds, par, _pickupUsed, _offences, _fled);
            Ctx.Logger?.Info("Jobs: taxi desk fare: " + _metres.ToString("F0") + " m in " + seconds.ToString("F0") + " s (par " + par.ToString("F0") + "), meter " + fare
                             + ", the office keeps " + kept + "; comfort " + ridden + " marked " + comfort + " (pick-up at " + Mathf.RoundToInt(_pickupUsed * 100f)
                             + "% of the wait, " + _offences + " offence(s) booked" + (_fled ? ", drove off from the police" : string.Empty) + ").");
            return fare;
        }

        private static float DeskTip(int quality) => SideJobRules.TipShare(quality);

        // The office's books hear of a fare as it is paid.
        private void DeskBooked(in PaySlip slip, int fare, int kept)
        {
            if (Ctx.Desk != null)
            {
                Ctx.Desk.Booked(new DeskTake { Units = 1, Gross = fare, Kept = kept, Tips = slip.Tips, Net = slip.Total, Metres = Mathf.RoundToInt(_metres) });
            }
        }

        // The desk gives a driver so many fares a day: the shift ends, done, when they are driven.
        private bool DeskDayDone()
        {
            if (Ctx.Desk == null || Ctx.Desk.Test || Ctx.Desk.Left() > 0)
            {
                return false;
            }
            Ctx.Say("reallife_job_desk_day_done", 9f);
            Finish(JobEnd.Done);
            return true;
        }
    }
}
