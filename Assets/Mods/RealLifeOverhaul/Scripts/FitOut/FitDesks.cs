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
    /// Which desk of a fitted-out floor is the player's, for a job worked at the employer's desk: nobody of the cast sits
    /// there, and the careers make it a thing to click. Asked once as a floor goes up, told when it stands and when it goes.
    /// The same room gives the same plan and the seed the same desk, so it is the same every visit. Nothing is saved.
    /// </summary>
    public static class FitDesks
    {
        /// <summary>The seed of the player's desk in a building (by its place key), or -1 if none is theirs.</summary>
        public static Func<string, int> Wanted;

        /// <summary>The player's desk stands.</summary>
        public static Action<FitDesk> Made;

        /// <summary>The player's desk is taken down with its floor.</summary>
        public static Action<FitDesk> Gone;

        /// <summary>The player's desk standing now, or null.</summary>
        public static FitDesk Current { get; private set; }

        /// <summary>The place key of the building where a floor keeping the player's desk is going up, or null.</summary>
        public static string Building { get; private set; }

        internal static int Ask(string place)
        {
            try
            {
                return string.IsNullOrEmpty(place) || Wanted == null ? -1 : Wanted(place);
            }
            catch (Exception e)
            {
                FitOutDirector.Logger?.Error(e);
                Wanted = null;   // asked no more this session
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
