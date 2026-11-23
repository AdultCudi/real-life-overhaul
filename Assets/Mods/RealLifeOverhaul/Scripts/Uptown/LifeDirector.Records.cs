using System;
using System.Diagnostics;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's buildings findable at the load's first step (docs/features/new-buildings.md, "Findable at the load's first step"). The game runs a load as
    /// the lookups of its buildings and registrations emptied and filled with its own, the district save's early step (the main menu's mods unloading), the
    /// city's scenes built, every mod's city part in turn (this mod's after the older ones), then its "game loaded" calls. A mod whose own start asked for a
    /// district building by its address (a renovated property it had saved) found none, because the buildings were declared and made in this mod's city part
    /// (Alcware's Retail Expansion stopped its setup on "A renovated property is missing", a player's log of 19 Nov 2026). The buildings are now declared at
    /// the district save's step after its early pass, and the record and registration of each ordinary one are put in the game's two lookups then
    /// (<see cref="MadeGame.RegisterEarly"/>); the make behind the loading screen claims them, so the load does the same work in the same time, in another order.
    /// A load that did not pass the main menu (a funeral's backup) has no early step and declares in the city's load as before. COST: nothing per frame; the
    /// declarations and records are the load's own, moved.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static bool _declaredEarly;   // this load's buildings were declared, and their records made, at the district save's step after its early pass

        // The district save's step after its early pass (DistrictPlaces.Recorded): the save is read, its kept records not yet back, the scenes not built.
        private static void Recorded()
        {
            GameInstance save = SaveGameManager.Current;
            if (!_known || save == null || !ReferenceEquals(save, _earlyFor))
            {
                return;   // not a district of the game at this load, or no early step came first: the city's load declares as it always did
            }
            long began = Stopwatch.GetTimestamp();
            try
            {
                ReadState();
                LifeGame.Lots(Plan);   // the ground's plan, game-free and fixed: readable before any ground is laid
                _nearest = BuildingsRules.Nearest(Plan, LifeGame.AwayX, LifeGame.AwayZ);
                Declare();
                _declaredEarly = true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Uptown: its buildings could not be declared at the load's first step; the city's load declares them.");
                ForgetDeclared();
                return;
            }
            int made = LifeGame.RecordsEarly(Declared, out int found);
            double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            Logger?.Info("Uptown: " + Declared.Count + " building(s) declared at the load's first step, before the city's scenes, and " + made + " record(s) put in the game's lookups by address ("
                         + (made - found) + " of them not given back by the game's own look-up), in " + ms.ToString("F0", Plain) + " ms; the rest are made with their buildings.");
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Uptown check (records at the load's first step): " + made + " record(s) and registration(s) in the game's lookups before the city's scenes, " + found
                             + " found by BuildingHelper.GetBuilding and GetBuildingRegistration: " + (made > 0 && found == made ? "PASS" : "FAIL"));
            }
        }

        // The declarations of this load forgotten, as a new load forgets them: nothing of a half-made declaration is left for the city's load to double.
        private static void ForgetDeclared()
        {
            for (int i = 0; i < Declared.Count; i++)
            {
                MadeBuildings.Withdraw(Declared[i].Id);
            }
            Declared.Clear();
            BuiltLots.Clear();
            KeptLots.Clear();
            _declaredEarly = false;
        }
    }
}
