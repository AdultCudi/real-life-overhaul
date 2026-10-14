using System;
using UnityEngine;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>The player's own desk on a fitted-out floor: the chair's seat, the desk and its chair, and where to stand to sit down.</summary>
    public sealed class FitDesk
    {
        public Transform Seat;      // where the game's item seats somebody (GameFurniture.SeatName)
        public Transform Desk;      // the desk the chair is drawn up to, the computer on it
        public Transform Chair;
        public Vector3 Stand;       // on the walkable floor by the chair
        public string Place;        // the building's place key (TalentGame.Place)
        public FitKind Kind;
    }

    /// <summary>
    /// Which desk of a fitted-out floor is the player's (Scripts/FitOut): a working day of a job is worked at a desk of
    /// the employer's (Careers), and where that employer's floor is one the mod fits out (a tech company's headquarters,
    /// or a preset desk where the building has no station of the game's that suits the job), one of the floor's desks is
    /// kept for the player: nobody of the cast sits there, and the careers make it a thing to click.
    ///
    /// Asked once when a floor begins to go up (<see cref="Wanted"/>, wired from Careers in Scripts/Wiring), told once when
    /// it stands and when it is taken down. The desk is the same every visit: the plan is the same for the same room, and
    /// the desk is its seats' one picked by the seed given. Nothing is saved, nothing runs by itself.
    /// </summary>
    public static class FitDesks
    {
        /// <summary>The seed of the player's desk on the floor of a building (its place key), or -1 if none of its desks is theirs. Default: none.</summary>
        public static Func<string, int> Wanted;

        /// <summary>The player's desk stands. Default: nobody is told.</summary>
        public static Action<FitDesk> Made;

        /// <summary>...and is taken down with its floor. Default: nobody is told.</summary>
        public static Action<FitDesk> Gone;

        /// <summary>The player's desk standing now, or null.</summary>
        public static FitDesk Current { get; private set; }

        /// <summary>The building a floor that keeps a desk for the player is going up in (its place key), or null.</summary>
        public static string Building { get; private set; }

        /// <summary>The seed of the player's desk in a building, or -1.</summary>
        internal static int Ask(string place)
        {
            try
            {
                return string.IsNullOrEmpty(place) || Wanted == null ? -1 : Wanted(place);
            }
            catch (Exception e)
            {
                FitOutDirector.Logger?.Error(e);
                Wanted = null;   // (asked no more this session)
                return -1;
            }
        }

        /// <summary>The desk of so many seats a seed picks; -1 for none.</summary>
        public static int Pick(int seats, int seed) => FitPlanner.PickDesk(seats, seed);

        internal static void Going(string place)
        {
            Building = place;
        }

        internal static void Stands(FitDesk desk)
        {
            Current = desk;
            Building = null;
            try
            {
                Made?.Invoke(desk);
            }
            catch (Exception e)
            {
                FitOutDirector.Logger?.Error(e);
            }
        }

        internal static void Down(string place)
        {
            if (Building == place)
            {
                Building = null;
            }
            FitDesk desk = Current;
            if (desk == null || desk.Place != place)
            {
                return;
            }
            Current = null;
            try
            {
                Gone?.Invoke(desk);
            }
            catch (Exception e)
            {
                FitOutDirector.Logger?.Error(e);
            }
        }
    }
}
