using System;
using System.Reflection;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the mod's buildings out of the city map's rows of units to let and for sale. The mod refuses its buildings
    /// in the interface only, so the game still lights them in those rows; the step of the game's queue
    /// (CityMapFilters.Work) is wrapped once a session so that each building it lights is put out again
    /// (<see cref="FrontSigns.Unlit"/>) in the same frame. SpecialBuildings.Unlist stays behind it for a game whose
    /// queue is not as expected. Nothing is saved or sent. COST: one dictionary look per building the map looks at,
    /// only while the map is open.
    /// </summary>
    internal static class ClaimedMap
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        private static Action<CityBuildingController> _game;   // the game's own step
        private static bool _tried;
        private static bool _failed;

        internal static bool On => _game != null && !_failed;

        /// <summary>Called once a second by SpecialNames; wraps the queue on the first call of a session.</summary>
        internal static void Keep()
        {
            if (_tried)
            {
                return;
            }
            _tried = true;
            try
            {
                DistributedWork<CityBuildingController> work = CityMapFilters.Work;
                // The contract says whether the queue still has its step; the field is read on the generic type itself.
                FieldInfo field = GameContract.ClaimedRows.Off ? null : typeof(DistributedWork<CityBuildingController>).GetField("_updateAction", Hidden);
                var step = field != null && work != null ? field.GetValue(work) as Action<CityBuildingController> : null;
                if (step == null)
                {
                    SpecialBuildings.Logger?.Info("Special buildings: the city map's look at its buildings is not as expected; the mod's buildings are put out of its rows of units to let when each look ends.");
                    return;
                }
                if (step.Method.DeclaringType == typeof(ClaimedMap))
                {
                    return;   // already wrapped
                }
                _game = step;
                field.SetValue(work, new Action<CityBuildingController>(Step));
                SpecialBuildings.Logger?.Info("Special buildings: the city map leaves the mod's buildings out of its rows of units to let and for sale, building by building.");
            }
            catch (Exception e)
            {
                _game = null;
                SpecialBuildings.Logger?.Error(e);
                SpecialBuildings.Logger?.Info("Special buildings: the city map's look could not be followed after the error above; the mod's buildings are put out of its rows when each look ends.");
            }
        }

        // A fault in the mod's part stops it for the session; the game's step always runs.
        private static void Step(CityBuildingController building)
        {
            _game(building);
            if (_failed)
            {
                return;
            }
            try
            {
                FrontSigns.Unlit(building);
            }
            catch (Exception e) when (Guard.StandsDown(e, "the mod's buildings left out of the city map's rows of units to let"))
            {
                _failed = true;
            }
            catch (Exception e)
            {
                _failed = true;
                SpecialBuildings.Logger?.Error(e);
                SpecialBuildings.Logger?.Warn("Special buildings: the city map's rows could not be kept after the error above; the mod's buildings are put out of them when each look ends.");
            }
        }
    }
}
