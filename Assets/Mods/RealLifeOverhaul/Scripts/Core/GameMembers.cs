using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BAModAPI;
using Buildings;
using UI.InGameUI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The members of the game that a build before the one the mod is made for can lack (Core/GameBuild: Build 3675 lacked
    /// all of these), read here and nowhere else. Mono compiles a method when it is first called, and it cannot compile
    /// one that names a member the running game does not have: it throws at every call instead (TypeLoadException,
    /// MissingFieldException, MissingMethodException), and the whole method is lost, whatever else it does. So each
    /// member is named in a small method of its own, never inlined, called inside a try: on such a game only that
    /// method fails, its first failure is said once in Player.log with the member it names, and the answer is the one
    /// that fits a game without it. Nobody is dead without the game's own funeral, no building stands behind a private
    /// fence without the game's fences, the building card has no target, and a station is named by its older field.
    /// The question box (HudConfirm.Show) is Core/GameConfirm's.
    ///
    /// The entry points the engine calls use <see cref="StandsDown"/> in their catch, so that any member a game lacks
    /// is said once and that part stops for the session, instead of failing every frame or every tick.
    ///
    /// Cost while the game has them all: a call and a flag test for each read. NavCheck holds the mod's sources to
    /// naming these members nowhere else, and finds each in the game's assemblies (GameBuild.Isolated).
    /// </summary>
    public static class GameMembers
    {
        public static IModLogger Logger;

        private static readonly HashSet<string> Told = new HashSet<string>(StringComparer.Ordinal);
        private static bool _noFuneral, _noFence, _noTarget, _noNameKey, _noStationName;

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
                catch (Exception e) when (Missing(e))
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
                catch (Exception e) when (Missing(e))
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
                catch (Exception e) when (Missing(e))
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
            Without(e, "the game's own death and funeral", "the player is taken to be alive and no funeral to be on");
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
            catch (Exception e) when (Missing(e))
            {
                _noFence = true;
                Without(e, "a building's private fence", "no building is taken to be behind one");
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
            catch (Exception e) when (Missing(e))
            {
                _noTarget = true;
                Without(e, "what the game's building card is about", "the mod's lines on the card are left out");
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
                catch (Exception e) when (Missing(e))
                {
                    _noNameKey = true;
                    Without(e, "a subway station's name", "it is put together from the station's name field");
                }
            }
            if (!_noStationName)
            {
                try
                {
                    return StationField(station);
                }
                catch (Exception e) when (Missing(e))
                {
                    _noStationName = true;
                    Without(e, "a subway station's name field", "the station's own object name stands in");
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

        // ---------------------------------------------------------------- the exceptions

        /// <summary>True when the exception says the running game lacks a type, field or method the mod was compiled against (or has made it inaccessible).</summary>
        public static bool Missing(Exception e)
        {
            return e is TypeLoadException || e is MemberAccessException
                   || (e is TypeInitializationException && e.InnerException != null && Missing(e.InnerException));
        }

        /// <summary>
        /// For the catch at an entry point the engine calls: true when the running game lacks a member, said once for
        /// that part, which the caller then stands down for the session. False for any other fault, which the caller
        /// handles as it always has.
        /// </summary>
        public static bool StandsDown(Exception e, string part)
        {
            if (!Missing(e))
            {
                return false;
            }
            Say(part, "Game build: " + part + " is off for the rest of this session: this game lacks " + GameBuild.MemberOf(e.Message) + ". "
                      + GameBuild.Builds(GameBuild.Running, Meta.Changelog.Version));
            return true;
        }

        /// <summary>
        /// For a step that is done without on a game that lacks what it needs: true (and said once for <paramref name="what"/>)
        /// when the exception says so.
        /// </summary>
        public static bool DoesWithout(Exception e, string what)
        {
            return DoesWithout(e, what, "the mod does without it");
        }

        /// <summary>As <see cref="DoesWithout(Exception, string)"/>, with what is done instead.</summary>
        public static bool DoesWithout(Exception e, string what, string answer)
        {
            if (!Missing(e))
            {
                return false;
            }
            Without(e, what, answer);
            return true;
        }

        // "Game build: this game lacks X. What it was for: what is done instead. This game is Build N; ... Build M."
        private static void Without(Exception e, string what, string answer)
        {
            Say(what, "Game build: this game lacks " + GameBuild.MemberOf(e != null ? e.Message : null) + ". " + char.ToUpperInvariant(what[0]) + what.Substring(1)
                      + ": " + answer + ". " + GameBuild.Builds(GameBuild.Running, Meta.Changelog.Version));
        }

        // Once a session for each part or member: a game that lacks something lacks it for good.
        private static void Say(string key, string line)
        {
            lock (Told)
            {
                if (!Told.Add(key))
                {
                    return;
                }
            }
            if (Logger != null)
            {
                Logger.Warn(line);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Mod:Real Life Overhaul] " + line);
            }
        }
    }
}
