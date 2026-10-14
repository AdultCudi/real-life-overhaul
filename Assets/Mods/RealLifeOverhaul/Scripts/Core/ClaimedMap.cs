using System;
using System.Reflection;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's city map leaves the mod's buildings out of its rows of units to let (office, retail, warehouse and the
    /// others under "Buildings for rent") and of buildings for sale. The game decides each building in turn, a few a frame,
    /// in one method (CityMapFilters.ApplyFilters, called through its queue CityMapFilters.Work): it lights a building that
    /// is to let when that kind's row is ticked. Since the mod refuses its buildings in the interface only, the game still
    /// holds them as to let, and lit them. So the queue's step is wrapped once per session: the game's own step, then, for
    /// a building of the mod's only, its light put out where the game lit it only for those rows
    /// (FrontSigns.Unlit, FrontSignRules.PutOut), in the same frame, before anything is drawn. A row of the mod's own that
    /// lights the building (the tech companies, the places to study) lights it again in its own colour (MapPlaces.Light).
    /// The game's rows have no counts, so nothing else is to be put right.
    ///
    /// Before this, the lights were put out when the game's look at every building was seen to end (SpecialBuildings.Unlist,
    /// from MapPlaces), which a look done within one frame (a fast machine, a small city) never shows: that stays, behind
    /// this, for a game whose queue is not as expected.
    ///
    /// Nothing is saved and nothing is sent: the same claims light the same buildings on every machine. The cost: one
    /// count test and one dictionary look per building the game looks at, only while the map is open and its look under
    /// way. Set up once, the first second the city is there.
    /// </summary>
    internal static class ClaimedMap
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        private static Action<CityBuildingController> _game;   // the game's own step of the queue
        private static bool _tried;
        private static bool _failed;

        /// <summary>True once the game's queue has been wrapped.</summary>
        internal static bool On => _game != null && !_failed;

        /// <summary>Once a second (SpecialNames), until it has been tried once this session.</summary>
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
                // (the contract's piece says whether the game's queue still has its step; the field itself is read on the
                // queue's own type, a generic type's instance: Core/GameContract.MapLookStep)
                FieldInfo field = GameContract.ClaimedRows.Off ? null : typeof(DistributedWork<CityBuildingController>).GetField("_updateAction", Hidden);
                var step = field != null && work != null ? field.GetValue(work) as Action<CityBuildingController> : null;
                if (step == null)
                {
                    SpecialBuildings.Logger?.Info("Special buildings: the city map's look at its buildings is not as expected; the mod's buildings are put out of its rows of units to let when each look ends.");
                    return;
                }
                if (step.Method.DeclaringType == typeof(ClaimedMap))
                {
                    return;   // (already wrapped)
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

        // The game's step, then the mod's. A fault in the mod's part stops it for the session; the game's own is never held up.
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
