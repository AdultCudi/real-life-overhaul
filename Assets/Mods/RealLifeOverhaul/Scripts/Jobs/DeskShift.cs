using System;

namespace RealLifeOverhaul
{
    /// <summary>What one fare or one delivery run of a desk shift came to, told to the desk's books as it is paid.</summary>
    public struct DeskTake
    {
        public int Units;      // fares driven or parcels delivered
        public int Gross;      // what the customers paid on the meter or for the drops
        public int Kept;       // the firm's share of that
        public int Tips;       // the driver's own
        public int Net;        // what reached the driver: the slip's total
        public int Metres;     // the fare's or the run's planned length
    }

    /// <summary>
    /// A shift signed on for at a firm's desk and driven in the player's own vehicle (Scripts/SideJobs): what the desk fixed
    /// when the player signed on, handed to the job with its context as an employer's shift or a booked set is. The job reads
    /// the vehicle's room from it and tells it what each fare or run came to; the sums are SideJobRules'. Holds no object of
    /// the game's (the vehicle is kept by its id) and costs nothing a frame.
    /// </summary>
    public sealed class DeskShift
    {
        /// <summary>True for the cab office's shift (fares), false for the parcel depot's (delivery runs).</summary>
        public bool Cab;

        /// <summary>The seats of the vehicle signed on with, the driver's included.</summary>
        public int Seats;

        /// <summary>Parcels a run the vehicle has room for.</summary>
        public int Drops;

        /// <summary>The save's id of the vehicle signed on with; null for any of the driver's own (a test).</summary>
        public string Vehicle;

        /// <summary>True for the vehicle the shift was signed on with.</summary>
        public bool Fits(VehicleController car)
        {
            return car != null && (Vehicle == null || (car.vehicleInstance != null && car.vehicleInstance.id == Vehicle));
        }

        /// <summary>Started by a test button: the day's amount is not asked.</summary>
        public bool Test;

        /// <summary>Fares or parcels the desk's day still has for this driver; never null.</summary>
        public Func<int> Left;

        /// <summary>The desk's books: called once for each fare or run paid; never null.</summary>
        public Action<DeskTake> Booked;
    }
}
