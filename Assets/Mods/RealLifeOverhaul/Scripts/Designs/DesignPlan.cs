using System;
using System.Collections.Generic;
using System.IO;

namespace RealLifeOverhaul.Designs
{
    /// <summary>One ready-made design to be put in the installers' folder: for which type and size, under which name, from what.</summary>
    public sealed class PlannedDesign
    {
        public int Kind;
        public string Size;
        public string Name;
        public DesignSource Source;
        public ShowroomCut Cut;              // what a Car Showroom keeps of its source; null for a design offered whole
        public string Path;                  // under the installers' folder, with '/': building/size/type/name.json
    }

    /// <summary>
    /// The game's design files the ready-made designs are made from, found on disk (System.IO only, so that the offline checks
    /// find exactly what the game's start does), and the plan of every design made from them. Called once, on a worker thread,
    /// when the designs are put in place; nothing of it runs in a frame.
    /// </summary>
    public static class DesignPlan
    {
        /// <summary>
        /// Every source of every kind under the game's StreamingAssets folder, in <see cref="DesignRules.Order"/>, and the
        /// sizes the game has designs for in each kind's building (its size folders), in order. False if the game's
        /// installers' folder is not there.
        /// </summary>
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

        // A size folder holds the game's designs for some business of its own (not only the mod's).
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

        /// <summary>
        /// The plan: for every kind and every size its building has designs for, the designs it is offered. A kind offered
        /// the game's own whole takes the first of each name; a Car Showroom reads each candidate (<paramref name="read"/>,
        /// null for one that cannot be read), cuts it for its lot on the room the size's candidates fill, and takes the best
        /// (<see cref="DesignRules.Showrooms"/>).
        /// </summary>
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
                    DesignRules.Lot(DesignRules.Room(read1), lot);
                    cuts.Clear();
                    for (int i = 0; i < group.Count; i++)
                    {
                        cuts.Add(read1[i] != null ? DesignRules.Cut(read1[i], lot) : null);
                    }
                    DesignRules.Showrooms(group, cuts, picks);
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
