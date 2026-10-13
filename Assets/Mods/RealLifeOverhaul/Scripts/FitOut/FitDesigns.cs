using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Blueprints;
using BusinessLayoutSets;
using UnityEngine;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The game's own finished designs for a unit (Scripts/FitOut): every empty unit of a size is the same room (the game
    /// loads one interior per size and version), and the game ships designs for those rooms, the interior installation
    /// firm's (StreamingAssets/InteriorDesigns/&lt;type&gt;/&lt;size&gt;/&lt;business&gt;/&lt;name&gt;.json) and the layouts its own
    /// businesses are furnished with (StreamingAssets/BusinessLayouts/&lt;business&gt;/&lt;size&gt;/&lt;name&gt;.json). Each is the
    /// game's items at their places in that room, and the room's walls and floors. They are read with the game's own
    /// reader (BusinessLayoutSetHelper.Deserialize) on a worker thread, a file once a session, so that nothing of it is
    /// done in a frame: the plan waits for it.
    ///
    /// Which design: of the kinds of business asked for, in that order, the files of the unit's size, the building's own
    /// pick among them (its number), the first whose size and version are the unit's. Nothing here runs by itself.
    /// </summary>
    internal static class FitDesigns
    {
        private static readonly Dictionary<string, List<string>> Files = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Task<BusinessLayoutSet>> Reads = new Dictionary<string, Task<BusinessLayoutSet>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The size of a unit as the game names its folders ("K1").</summary>
        public static string SizeOf(Buildings.Building building)
        {
            return building != null && !string.IsNullOrEmpty(building.BuildingSize) ? new BuildingSizeInfo(building).ToString() : null;
        }

        /// <summary>The design files of one kind of business for a size, from both places the game keeps them, in a fixed order.</summary>
        public static List<string> FilesFor(string kind, string size)
        {
            string key = kind + "|" + size;
            if (Files.TryGetValue(key, out List<string> files))
            {
                return files;
            }
            files = new List<string>(4);
            try
            {
                string root = Application.streamingAssetsPath;
                string designs = Path.Combine(root, "InteriorDesigns");
                if (Directory.Exists(designs))
                {
                    foreach (string type in Directory.GetDirectories(designs))
                    {
                        string folder = Path.Combine(type, size);
                        if (!Directory.Exists(folder))
                        {
                            continue;
                        }
                        foreach (string business in Directory.GetDirectories(folder))
                        {
                            if (string.Equals(Path.GetFileName(business), kind, StringComparison.OrdinalIgnoreCase))
                            {
                                files.AddRange(Directory.GetFiles(business, "*.json", SearchOption.TopDirectoryOnly));
                            }
                        }
                    }
                }
                string layouts = Path.Combine(root, "BusinessLayouts", kind, size);
                if (Directory.Exists(layouts))
                {
                    files.AddRange(Directory.GetFiles(layouts, "*.json", SearchOption.TopDirectoryOnly));
                }
            }
            catch (Exception)
            {
                files.Clear();
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            Files[key] = files;
            return files;
        }

        /// <summary>The reading of a design file, begun on a worker thread the first time it is asked for.</summary>
        public static Task<BusinessLayoutSet> Read(string path)
        {
            if (!Reads.TryGetValue(path, out Task<BusinessLayoutSet> read))
            {
                read = Task.Run(() =>
                {
                    try
                    {
                        return BusinessLayoutSetHelper.Deserialize(path);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                });
                Reads[path] = read;
            }
            return read;
        }

        /// <summary>
        /// The design files that may be the unit's, in the order they are to be tried: each kind in turn, its files
        /// begun from the building's own pick. Their reading is begun at once.
        /// </summary>
        public static void Candidates(string size, IList<string> kinds, int seed, List<string> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(size) || kinds == null)
            {
                return;
            }
            for (int k = 0; k < kinds.Count; k++)
            {
                List<string> files = FilesFor(kinds[k], size);
                for (int tried = 0; tried < files.Count; tried++)
                {
                    string path = files[((seed & 0x7fffffff) + tried) % files.Count];
                    if (!into.Contains(path))
                    {
                        into.Add(path);
                    }
                }
            }
            for (int i = 0; i < into.Count && i < 2; i++)
            {
                Read(into[i]);   // (the first two: the next is seldom wanted)
            }
        }

        /// <summary>True if a design read is of this very room: the unit's size and version.</summary>
        public static bool Fits(BusinessLayoutSet set, Buildings.Building building)
        {
            return set != null && building != null && set.Items != null && set.BuildingSize == building.BuildingSize && set.BuildingVersion == building.BuildingVersion;
        }
    }
}
