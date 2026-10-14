using System;
using System.Runtime.CompilerServices;
using Buildings;
using UI.InGameUI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The members of the game that a build before the one the mod is made for can lack (Build 3675 lacked all of these),
    /// read here and nowhere else. Mono compiles a method when it is first called, and it cannot compile one that names a
    /// member the running game does not have: it throws at every call instead (TypeLoadException, MissingFieldException,
    /// MissingMethodException), and the whole method is lost, whatever else it does. So each member is named in a small
    /// method of its own, never inlined, and the answer without it is the one that fits a game without it: nobody is dead
    /// without the game's own funeral, no building stands behind a private fence without the game's fences, the building
    /// card has no target, and a station is named by its older field. The question box (HudConfirm.Show) is
    /// Core/GameConfirm's.
    ///
    /// Each is a line of the game contract (Core/GameContract: PlayerDead, FuneralActive, FenceIndex, ResumeTarget,
    /// StationNameKey, StationName), resolved as the mod starts: on a game that lacks one, <see cref="Known"/> takes its
    /// answer before anything asks, and the method that names it is never called. Should one be found missing only when
    /// it is first used, its catch takes the same answer and says it once (Core/Guard.DoesWithout).
    ///
    /// Cost while the game has them all: a call and a flag test for each read. ContractCheck holds every method of the
    /// mod that names one of these to being such a method, never inlined and called inside a try.
    /// </summary>
    public static class GameMembers
    {
        private static bool _noFuneral, _noFence, _noTarget, _noNameKey, _noStationName;

        /// <summary>
        /// From Core/Guard as the mod starts, once the contract is resolved: each member this game lacks is known from
        /// then on, so that nothing here is ever compiled against it.
        /// </summary>
        public static void Known()
        {
            _noFuneral = GameContract.PlayerDead.Missing || GameContract.FuneralActive.Missing;
            _noFence = GameContract.FenceIndex.Missing;
            _noTarget = GameContract.ResumeTarget.Missing;
            _noNameKey = GameContract.StationNameKey.Missing;
            _noStationName = GameContract.StationName.Missing;
        }

        // ---------------------------------------------------------------- Helpers.FuneralHelper

        /// <summary>The game's own "the player is dead" (FuneralHelper.PlayerDead); false on a game without it, where setting it does nothing.</summary>
        public static bool PlayerDead
        {
            get
            {
                if (_noFuneral)
                {
                    return false;
                }
                try
                {
                    return Dead();
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    NoFuneral(e);
                    return false;
                }
            }
            set
            {
                if (_noFuneral)
                {
                    return;
                }
                try
                {
                    SetDead(value);
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    NoFuneral(e);
                }
            }
        }

        /// <summary>True while the game's funeral is on (FuneralHelper.IsFuneralActive); false on a game without it.</summary>
        public static bool FuneralActive
        {
            get
            {
                if (_noFuneral)
                {
                    return false;
                }
                try
                {
                    return Funeral();
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    NoFuneral(e);
                    return false;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Dead()
        {
            return Helpers.FuneralHelper.PlayerDead;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetDead(bool value)
        {
            Helpers.FuneralHelper.PlayerDead = value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Funeral()
        {
            return Helpers.FuneralHelper.IsFuneralActive;
        }

        private static void NoFuneral(Exception e)
        {
            _noFuneral = true;
            Guard.DoesWithout(e, "the game's own death and funeral", "the player is taken to be alive and no funeral to be on");
        }

        // ---------------------------------------------------------------- Building.locatedInsidePrivateFenceIndex

        /// <summary>True when the building stands inside one of the game's private fences (locatedInsidePrivateFenceIndex 0 or more); false on a game without them.</summary>
        public static bool Fenced(Building building)
        {
            if (_noFence || building == null)
            {
                return false;
            }
            try
            {
                return FenceIndex(building) >= 0;
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                _noFence = true;
                Guard.DoesWithout(e, "a building's private fence", "no building is taken to be behind one");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int FenceIndex(Building building)
        {
            return building.locatedInsidePrivateFenceIndex;
        }

        // ---------------------------------------------------------------- BuildingResume.TargetController

        /// <summary>What the game's building card is about (BuildingResume.TargetController); null on a game without it.</summary>
        public static EntityController TargetOf(BuildingResume card)
        {
            if (_noTarget || ReferenceEquals(card, null))
            {
                return null;
            }
            try
            {
                return Target(card);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                _noTarget = true;
                Guard.DoesWithout(e, "what the game's building card is about", "the mod's lines on the card are left out");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static EntityController Target(BuildingResume card)
        {
            return card.TargetController;
        }

        // ---------------------------------------------------------------- SubwayStation.GetNameKey

        /// <summary>
        /// The key of a subway station's name (SubwayStation.GetNameKey). A game without it is asked what that method
        /// is made of ("subwaystation_" and the station's name field), and failing that the station's object name stands in.
        /// </summary>
        public static string NameKey(SubwayStation station)
        {
            if (ReferenceEquals(station, null))
            {
                return string.Empty;
            }
            if (!_noNameKey)
            {
                try
                {
                    return StationKey(station);
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    _noNameKey = true;
                    Guard.DoesWithout(e, "a subway station's name", "it is put together from the station's name field");
                }
            }
            if (!_noStationName)
            {
                try
                {
                    return StationField(station);
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    _noStationName = true;
                    Guard.DoesWithout(e, "a subway station's name field", "the station's own object name stands in");
                }
            }
            return station.name;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string StationKey(SubwayStation station)
        {
            return station.GetNameKey();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string StationField(SubwayStation station)
        {
            return "subwaystation_" + station.stationName;
        }
    }
}
