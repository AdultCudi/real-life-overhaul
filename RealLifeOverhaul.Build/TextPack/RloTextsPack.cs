using System;
using System.Threading.Tasks;
using BAModAPI;
using RealLifeOverhaul.TextPack;

[assembly: RegisterModClass(typeof(RloTextsPack))]

namespace RealLifeOverhaul.TextPack
{
    /// <summary>
    /// An RLO Texts pack as the game sees it: a mod that does one thing when the game loads it, which is to tell Real Life
    /// Overhaul where the pack's folder is (a data slot of the game's process, "RealLifeOverhaul.TextPacks", folders
    /// separated by '|'). Real Life Overhaul reads it when it looks for a pack (Scripts/Texts/TextPacks); the pack's model
    /// and llama.cpp's files are in its subfolders, which the game never loads. It changes nothing in the game; switched
    /// off in the game's mod list, it says nothing, and Real Life Overhaul still finds a pack in its fixed folder.
    /// </summary>
    [ModEntryOnInitializationLoad]
    public sealed class RloTextsPack : IModBigAmbitions
    {
        private const string Slot = "RealLifeOverhaul.TextPacks";

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            try
            {
                string folder = context.ModRootPath;
                string known = AppDomain.CurrentDomain.GetData(Slot) as string;
                if (!string.IsNullOrEmpty(folder) && (known == null || known.IndexOf(folder, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    AppDomain.CurrentDomain.SetData(Slot, string.IsNullOrEmpty(known) ? folder : known + "|" + folder);
                }
                context.Logger?.Info("RLO Texts pack: here (" + folder + "); Real Life Overhaul uses it when RLO Texts is on in its options.");
            }
            catch (Exception e)
            {
                context.Logger?.Warn("RLO Texts pack: could not say where it is (" + e.Message + "); Real Life Overhaul looks in the Workshop's folder itself.");
            }
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            return Task.CompletedTask;
        }
    }
}
