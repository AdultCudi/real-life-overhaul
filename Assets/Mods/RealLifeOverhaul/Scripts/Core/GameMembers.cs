using System;
using System.Runtime.CompilerServices;
using Buildings;
using UI.InGameUI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game members an older build can lack (Build 3675 lacked all of these), read here and nowhere else. Mono
    /// cannot compile a method that names a missing member and throws at every call of it, so each is named in a small
    /// method of its own, never inlined, called in a try; without it the answer is the one that fits such a game. Each is
    /// a contract line, so <see cref="Known"/> takes that answer at start; one found missing later is said once
    /// (Guard.DoesWithout). The question box is Core/GameConfirm's.
    /// COST: a call and a flag test for each read.
    /// </summary>
    public static class GameMembers
    {
        private static bool _noFuneral, _noFence, _noTarget, _noNameKey, _noStationName;

        /// <summary>From Core/Guard once the contract is resolved, so nothing here is compiled against a missing member.</summary>
        public static void Known()
        {
            _noFuneral = GameContract.PlayerDead.Missing || GameContract.FuneralActive.Missing;
            _noFence = GameContract.FenceIndex.Missing;
            _noTarget = GameContract.ResumeTarget.Missing;
            _noNameKey = GameContract.StationNameKey.Missing;
            _noStationName = GameContract.StationName.Missing;
        }

        /// <summary>FuneralHelper.PlayerDead; false on a game without it, where setting it does nothing.</summary>
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

        /// <summary>FuneralHelper.IsFuneralActive; false on a game without it.</summary>
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

        /// <summary>True when the building stands inside one of the game's private fences; false on a game without them.</summary>
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

        /// <summary>
        /// The key of a subway station's name (SubwayStation.GetNameKey); without it, what that method is made of, and
        /// failing that the station's object name.
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
