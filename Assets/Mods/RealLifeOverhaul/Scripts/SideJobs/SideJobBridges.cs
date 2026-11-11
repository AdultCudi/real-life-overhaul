using System;
using UnityEngine;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>Where a firm's office is: the help-wanted board the employers' ladder gave it, outside one of the city's buildings.</summary>
    public struct SidePlace
    {
        public bool Known;                           // the firm has a board standing in this city
        public Vector3 BoardFront;                   // where its reader stands
        public Vector3 BoardAt;                      // the board itself, against its wall
        public Vector3 Outward;                      // the way the wall faces
        public CityBuildingController Building;      // the building it stands at
        public string Address;                       // as the game writes it
        public string Shop;                          // the name over that building's door
    }

    /// <summary>
    /// What the taxi and delivery shifts ask of the rest of the mod, filled in by Wiring/Wiring.SideJobLinks: the jobs' director
    /// (the boards, the driver's own vehicle, starting and ending a desk shift) and the pump's price. Each has a default that
    /// leaves the feature standing with nothing to offer. Asked once a second at most, or on a press; never a frame.
    /// </summary>
    public static class SideJobBridges
    {
        /// <summary>True once the help-wanted boards are up and the firms have theirs.</summary>
        public static Func<bool> PlacesReady = () => false;

        /// <summary>Where a firm's office is.</summary>
        public static Func<SideKind, SidePlace> Place = kind => default;

        /// <summary>The driver's own vehicle: the one at the wheel, else the nearest standing within so many metres; null for none.</summary>
        public static Func<float, CarController> Car = within => null;

        /// <summary>Starts a desk shift; the reason as a locale key when it cannot, null when it has.</summary>
        public static Func<SideKind, DeskShift, string> Start = (kind, desk) => "reallife_job_no_off";

        /// <summary>True while a desk shift is being driven.</summary>
        public static Func<bool> OnDeskShift = () => false;

        /// <summary>True while any job is on, or something else of the jobs' has the interaction key (a board, a boss).</summary>
        public static Func<bool> Busy = () => false;

        /// <summary>The driver signs off: as stopping any job by choice.</summary>
        public static Action SignOff = () => { };

        /// <summary>The desk shift that is on ends through nobody's fault (the option went off).</summary>
        public static Action CallOff = () => { };

        /// <summary>The pump's price a litre, in cents.</summary>
        public static Func<int> PumpCents = () => 130;

        public static void Reset()
        {
            PlacesReady = () => false;
            Place = kind => default;
            Car = within => null;
            Start = (kind, desk) => "reallife_job_no_off";
            OnDeskShift = () => false;
            Busy = () => false;
            SignOff = () => { };
            CallOff = () => { };
            PumpCents = () => 130;
        }
    }
}
