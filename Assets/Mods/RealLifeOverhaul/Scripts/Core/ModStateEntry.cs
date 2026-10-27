using System;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Mods;
using RealLifeOverhaul;

[assembly: RegisterModClass(typeof(ModStateEntry))]

namespace RealLifeOverhaul
{
    /// <summary>
    /// Loaded with the main menu, for its unload: a save chosen from the menu is read first (and the game rebuilds its register
    /// of mood types from its own assets, which drops the mod's), then the menu's mods are unloaded, and only then are the city's
    /// scenes built, where PlayerController.Awake looks up the definition of every mood in the save's list. At that unload the
    /// mod's mood definitions go back into the register and a mood the game cannot read is taken out of the list
    /// (<see cref="ModState.BeforeCity"/>), so a save made with a mood of the mod in its list loads and plays. Two copies of the
    /// mod switched on are one assembly started twice: the second does nothing. COST: nothing per frame.
    /// </summary>
    [ModEntryMainMenu]
    public class ModStateEntry : IModBigAmbitions
    {
        private static readonly FeatureGuard EntryGuard = Guard.For(null, "ModStateEntry", typeof(ModStateEntry));
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
                ModState.Logger = Meta.Dev.Gated(context).Logger;   // the city's load sets it again; this one is for the first load of a session
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
                ModState.BeforeCity();   // a save has just been read: the moods' definitions go back before its city is built
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                return Guard.Failed(EntryGuard, e, Task.CompletedTask);
            }
        }
    }
}
