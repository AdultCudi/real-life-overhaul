using System;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Mods;
using RealLifeOverhaul.DistrictSave;

[assembly: RegisterModClass(typeof(DistrictSaveEntry))]

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// Loaded with the main menu, for its unload: a save chosen from the menu is read first, then the menu's mods are
    /// unloaded, and only then are the city's scenes built (UI.Load/LoadingScreen). At that unload the kept records of
    /// the mod's places go back into the save's lists, so that everything the game builds from those lists as the city
    /// loads (its lookup of staff by id, each business's customers, the rivals, the to-do list) has them. Two copies of
    /// the mod switched on are one assembly started twice: the second does nothing. COST: nothing per frame.
    /// </summary>
    [ModEntryMainMenu]
    public class DistrictSaveEntry : IModBigAmbitions
    {
        private static readonly FeatureGuard EntryGuard = Guard.For(null, "DistrictSaveEntry", typeof(DistrictSaveEntry));
        private static string _running;
        private bool _second;

        public string[] RelativeAssetBundlePaths
        {
            get
            {
                try
                {
                    return Array.Empty<string>();
                }
                catch (Exception e)
                {
                    return Guard.Failed(EntryGuard, e, Array.Empty<string>());
                }
            }
        }

        public Task OnLoadAsync(ModContext context)
        {
            try
            {
                if (_running != null && _running != context.ModId)
                {
                    _second = true;
                    return Task.CompletedTask;
                }
                _running = context.ModId;
                DistrictSaveDirector.Logger = Meta.Dev.Gated(context).Logger;
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                return Guard.Failed(EntryGuard, e, Task.CompletedTask);
            }
        }

        public Task OnUnloadAsync()
        {
            try
            {
                if (_second)
                {
                    _second = false;
                    return Task.CompletedTask;
                }
                _running = null;
                DistrictSaveDirector.Early();   // a save has just been read: its kept records go back before its city is built
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                return Guard.Failed(EntryGuard, e, Task.CompletedTask);
            }
        }
    }
}
