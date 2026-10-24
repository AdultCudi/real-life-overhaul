using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Shops;

namespace RealLifeOverhaul.Designs
{
    /// <summary>One ready-made design to be put in the installers' folder.</summary>
    public sealed class PlannedDesign
    {
        public int Kind;
        public string Size;
        public string Name;
        public DesignSource Source;
        public ShowroomCut Cut;              // null for a design offered whole
        public string Path;                  // under the installers' folder, with '/': building/size/type/name.json
    }

    /// <summary>The game's design files on disk and the plan made from them; System.IO only, so the checks find what the game does.</summary>
    public static class DesignPlan
    {
        /// <summary>Every kind's sources in <see cref="DesignRules.Order"/>, and each kind's sizes; false with no installers' folder.</summary>
        public static bool Find(string streamingAssets, List<DesignSource> into, Dictionary<int, List<string>> sizes, Dictionary<int, string> buildings)
        {
            into.Clear();
            sizes.Clear();
            buildings.Clear();
            string installer = Path.Combine(streamingAssets, DesignCatalogue.Installer);
            if (!Directory.Exists(installer))
            {
                return false;
            }
            string[] buildingDirs = Directory.GetDirectories(installer);
            Array.Sort(buildingDirs, StringComparer.Ordinal);
            for (int k = 0; k < DesignCatalogue.Kinds.Length; k++)
            {
                DesignKind kind = DesignCatalogue.Kinds[k];
                string building = Match(buildingDirs, DesignRules.FolderOf(kind.Building));
                var found = new List<string>(8);
                sizes[k] = found;
                if (building == null)
                {
                    continue;
                }
                buildings[k] = Path.GetFileName(building);
                string[] sizeDirs = Directory.GetDirectories(building);
                Array.Sort(sizeDirs, StringComparer.Ordinal);
                for (int s = 0; s < sizeDirs.Length; s++)
                {
                    string size = Path.GetFileName(sizeDirs[s]);
                    if (!DesignRules.IsSize(size) || !HasDesigns(sizeDirs[s], kind))
                    {
                        continue;
                    }
                    found.Add(size);
                    for (int d = 0; d < kind.Donors.Length; d++)
                    {
                        Donor donor = kind.Donors[d];
                        string folder = donor.Root == DonorRoot.Installer
                            ? Match(Directory.GetDirectories(sizeDirs[s]), donor.Business)
                            : Path.Combine(streamingAssets, DesignCatalogue.Layouts, donor.Business, size);
                        Add(streamingAssets, folder, k, d, donor.Root, size, into);
                    }
                }
            }
            DesignRules.Order(into);
            return true;
        }

        // Designs for some business of the game's own, not only the mod's.
        private static bool HasDesigns(string sizeDir, DesignKind kind)
        {
            string[] businesses = Directory.GetDirectories(sizeDir);
            for (int b = 0; b < businesses.Length; b++)
            {
                if (!Mine(Path.GetFileName(businesses[b])) && Directory.GetFiles(businesses[b], "*" + DesignCatalogue.Extension, SearchOption.TopDirectoryOnly).Length > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Mine(string folder)
        {
            for (int k = 0; k < DesignCatalogue.Kinds.Length; k++)
            {
                if (folder == DesignRules.FolderOf(DesignCatalogue.Kinds[k].Type))
                {
                    return true;
                }
            }
            return false;
        }

        private static string Match(string[] dirs, string name)
        {
            for (int i = 0; i < dirs.Length; i++)
            {
                if (string.Equals(Path.GetFileName(dirs[i]), name, StringComparison.OrdinalIgnoreCase))
                {
                    return dirs[i];
                }
            }
            return null;
        }

        private static void Add(string streamingAssets, string folder, int kind, int donor, DonorRoot root, string size, List<DesignSource> into)
        {
            if (folder == null || !Directory.Exists(folder))
            {
                return;
            }
            string[] files = Directory.GetFiles(folder, "*" + DesignCatalogue.Extension, SearchOption.TopDirectoryOnly);
            for (int f = 0; f < files.Length; f++)
            {
                string name = DesignRules.NameOf(root, Path.GetFileName(files[f]));
                if (name == null)
                {
                    continue;
                }
                var info = new FileInfo(files[f]);
                into.Add(new DesignSource
                {
                    Kind = kind, Donor = donor, Root = root, Size = size, Name = name,
                    Path = files[f].Substring(streamingAssets.Length).TrimStart('\\', '/').Replace('\\', '/'),
                    Length = info.Length, Ticks = info.LastWriteTimeUtc.Ticks
                });
            }
        }

        /// <summary>Every kind's designs for every size; a showroom, branch or shop reads (<paramref name="read"/>, null if unreadable) and cuts each.</summary>
        public static List<PlannedDesign> Make(List<DesignSource> sources, Dictionary<int, List<string>> sizes, Dictionary<int, string> buildings,
            Func<DesignSource, List<DesignItem>> read)
        {
            var plan = new List<PlannedDesign>(160);
            var group = new List<DesignSource>(32);
            var chosen = new List<DesignSource>(8);
            var cuts = new List<ShowroomCut>(32);
            var read1 = new List<List<DesignItem>>(32);
            var lot = new List<FloorBox>(256);
            var picks = new List<int>(DesignCatalogue.ShowroomMost);
            for (int k = 0; k < DesignCatalogue.Kinds.Length; k++)
            {
                DesignKind kind = DesignCatalogue.Kinds[k];
                if (!sizes.TryGetValue(k, out List<string> kindSizes) || !buildings.TryGetValue(k, out string building))
                {
                    continue;
                }
                for (int s = 0; s < kindSizes.Count; s++)
                {
                    string size = kindSizes[s];
                    group.Clear();
                    for (int i = 0; i < sources.Count; i++)
                    {
                        if (sources[i].Kind == k && sources[i].Size == size)
                        {
                            group.Add(sources[i]);
                        }
                    }
                    if (kind.Way == DesignWay.Copy)
                    {
                        DesignRules.Copies(group, chosen);
                        for (int c = 0; c < chosen.Count; c++)
                        {
                            plan.Add(Planned(k, size, building, chosen[c], null));
                        }
                        continue;
                    }
                    read1.Clear();
                    for (int i = 0; i < group.Count; i++)
                    {
                        read1.Add(read(group[i]));
                    }
                    bool branch = kind.Way == DesignWay.Branch, shop = kind.Way == DesignWay.Stock;
                    if (!branch && !shop)
                    {
                        DesignRules.Lot(DesignRules.Room(read1), lot);
                    }
                    cuts.Clear();
                    ShopKind stock = shop ? ShopsCatalogue.Of(kind.Type) : null;
                    for (int i = 0; i < group.Count; i++)
                    {
                        cuts.Add(read1[i] == null ? null : shop ? DesignRules.StockCut(read1[i], stock) : branch ? DesignRules.BranchCut(read1[i]) : DesignRules.Cut(read1[i], lot));
                    }
                    if (shop)
                    {
                        DesignRules.Stocks(group, cuts, picks);
                    }
                    else if (branch)
                    {
                        DesignRules.Branches(group, cuts, picks);
                    }
                    else
                    {
                        DesignRules.Showrooms(group, cuts, picks);
                    }
                    for (int p = 0; p < picks.Count; p++)
                    {
                        plan.Add(Planned(k, size, building, group[picks[p]], cuts[picks[p]]));
                    }
                }
            }
            return plan;
        }

        private static PlannedDesign Planned(int kind, string size, string building, DesignSource source, ShowroomCut cut)
        {
            return new PlannedDesign
            {
                Kind = kind, Size = size, Name = source.Name, Source = source, Cut = cut,
                Path = DesignRules.Folder(building, size, DesignCatalogue.Kinds[kind]) + "/" + source.Name + DesignCatalogue.Extension
            };
        }
    }
}
