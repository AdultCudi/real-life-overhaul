using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RealLifeOverhaul.Designs
{
    /// <summary>What the last putting in place or taking away of the designs came to.</summary>
    public sealed class DesignsReport
    {
        public bool On;                      // they were put in place (false: taken away)
        public bool Failed;                  // the game's folder could not be read or written
        public bool Unchanged;               // what was there was still what would be made: nothing was written
        public int Designs;                  // designs in place now
        public int Types;                    // business types with at least one
        public int Written, Removed, Skipped;
        public long Milliseconds;
        public string Why;                   // for the log: what failed
    }

    /// <summary>
    /// The designs put in the game's installers' folder and taken out of it, game-free (System.IO, and the reading and the
    /// cutting of a design handed in), so that the offline checks run exactly this on a copy of the game's files. Only files on
    /// the mod's own list (DesignManifest) are ever written over or taken away: a file of the game's or another mod's is left
    /// as it is, and its design is not offered. The list first names everything a job may write, so a job cut short leaves
    /// nothing the mod does not know of. Called on a worker thread, one job at a time; nothing of it runs in a frame.
    /// </summary>
    public static class DesignFiles
    {
        /// <summary>Writes a Car Showroom's cut of a source to a target, as the game's format wants it; false if it could not.</summary>
        public delegate bool CutWriter(string source, ShowroomCut cut, string target, string type, string name);

        /// <summary>
        /// The designs made from the game's files under <paramref name="root"/> (its StreamingAssets) and put in its installers'
        /// folder, unless they are there already as they would be made (the list's stamp and every file's length).
        /// </summary>
        public static DesignsReport Put(string root, Func<string, List<DesignItem>> read, CutWriter write)
        {
            var watch = Stopwatch.StartNew();
            var report = new DesignsReport { On = true };
            var sources = new List<DesignSource>(256);
            var sizes = new Dictionary<int, List<string>>();
            var buildings = new Dictionary<int, string>();
            if (!DesignPlan.Find(root, sources, sizes, buildings))
            {
                report.Failed = true;
                report.Why = "there is no " + DesignCatalogue.Installer + " folder in " + root;
                return report;
            }
            string installer = Path.Combine(root, DesignCatalogue.Installer);
            string listPath = Path.Combine(installer, DesignCatalogue.Manifest);
            DesignManifest old = DesignManifest.Read(File.Exists(listPath) ? File.ReadAllText(listPath) : null);
            string stamp = DesignRules.Stamp(sources);
            if (old.Stamp == stamp && Present(installer, old))
            {
                report.Unchanged = true;
                Count(old, report);
                report.Milliseconds = watch.ElapsedMilliseconds;
                return report;
            }
            List<PlannedDesign> plan = DesignPlan.Make(sources, sizes, buildings, s => read(Path.Combine(root, s.Path)));

            var next = new DesignManifest { Stamp = string.Empty };
            foreach (KeyValuePair<string, long> file in old.Files)
            {
                next.Files[file.Key] = file.Value;
            }
            var planned = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Count; i++)
            {
                if (File.Exists(Path.Combine(installer, plan[i].Path)) && !old.Holds(plan[i].Path))
                {
                    report.Skipped++;   // somebody else's file of that name: left as it is
                    continue;
                }
                planned.Add(plan[i].Path);
                next.Files[plan[i].Path] = 0;
            }
            File.WriteAllText(listPath, next.Write());

            var done = new DesignManifest { Stamp = stamp };
            for (int i = 0; i < plan.Count; i++)
            {
                PlannedDesign design = plan[i];
                if (!planned.Contains(design.Path))
                {
                    continue;
                }
                string target = Path.Combine(installer, design.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? installer);
                string source = Path.Combine(root, design.Source.Path);
                bool wrote;
                if (design.Cut == null)
                {
                    File.Copy(source, target, true);
                    wrote = true;
                }
                else
                {
                    wrote = write(source, design.Cut, target, DesignCatalogue.Kinds[design.Kind].Type, design.Name);
                }
                if (wrote)
                {
                    done.Files[design.Path] = new FileInfo(target).Length;
                    report.Written++;
                }
                else
                {
                    report.Skipped++;
                }
            }
            foreach (KeyValuePair<string, long> file in next.Files)
            {
                if (!done.Files.ContainsKey(file.Key) && Delete(installer, file.Key))
                {
                    report.Removed++;
                }
            }
            File.WriteAllText(listPath, done.Write());
            Count(done, report);
            report.Milliseconds = watch.ElapsedMilliseconds;
            return report;
        }

        /// <summary>Every design on the mod's list, and nothing else, taken out of the game's installers' folder; then the list.</summary>
        public static DesignsReport Take(string root)
        {
            var watch = Stopwatch.StartNew();
            var report = new DesignsReport { On = false };
            string installer = Path.Combine(root, DesignCatalogue.Installer);
            string listPath = Path.Combine(installer, DesignCatalogue.Manifest);
            if (!File.Exists(listPath))
            {
                report.Milliseconds = watch.ElapsedMilliseconds;
                return report;
            }
            DesignManifest old = DesignManifest.Read(File.ReadAllText(listPath));
            var left = new DesignManifest { Stamp = string.Empty };
            foreach (KeyValuePair<string, long> file in old.Files)
            {
                if (Delete(installer, file.Key))
                {
                    report.Removed++;
                }
                else if (File.Exists(Path.Combine(installer, file.Key)))
                {
                    left.Files[file.Key] = file.Value;
                    report.Skipped++;
                }
            }
            if (left.Files.Count == 0)
            {
                File.Delete(listPath);
            }
            else
            {
                File.WriteAllText(listPath, left.Write());
            }
            report.Milliseconds = watch.ElapsedMilliseconds;
            return report;
        }

        /// <summary>The list of the mod's files in the installers' folder under <paramref name="root"/>; an empty one if there is none.</summary>
        public static DesignManifest List(string root)
        {
            string listPath = Path.Combine(root, DesignCatalogue.Installer, DesignCatalogue.Manifest);
            return DesignManifest.Read(File.Exists(listPath) ? File.ReadAllText(listPath) : null);
        }

        // Every file on the list is there, at the length written.
        private static bool Present(string installer, DesignManifest list)
        {
            if (list.Files.Count == 0)
            {
                return false;
            }
            foreach (KeyValuePair<string, long> file in list.Files)
            {
                var info = new FileInfo(Path.Combine(installer, file.Key));
                if (!info.Exists || info.Length != file.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private static void Count(DesignManifest list, DesignsReport report)
        {
            report.Designs = list.Files.Count;
            var types = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in list.Files.Keys)
            {
                types.Add(path.Split('/')[2]);
            }
            report.Types = types.Count;
        }

        // A design of the mod's list taken away, and its type's folder with it once empty. True if it was there.
        private static bool Delete(string installer, string path)
        {
            if (!DesignRules.Ours(path))
            {
                return false;
            }
            string file = Path.Combine(installer, path);
            if (!File.Exists(file))
            {
                return false;
            }
            try
            {
                File.Delete(file);
                string folder = Path.GetDirectoryName(file);
                if (folder != null && Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                {
                    Directory.Delete(folder);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
