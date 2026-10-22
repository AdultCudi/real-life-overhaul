using System;

namespace RealLifeOverhaul
{
    /// <summary>Where Driving meets features it must not depend on, set by the wiring; left null, nothing is lifted.</summary>
    public static class DrivingBridges
    {
        /// <summary>The Garage: takes a car's handling parts off it for a moment; true if it had any on.</summary>
        public static Func<VehicleController, bool> LiftParts;

        /// <summary>The Garage: puts back on top of the car whatever <see cref="LiftParts"/> took off it. Nothing if it took nothing.</summary>
        public static Action<VehicleController> LayParts;
    }
}
