using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BAModAPI;

namespace RealLifeOverhaul.Designs
{
    /// <summary>
    /// Ready-made designs: the game's installation firms offer the mod's business types designs made from the game's own,
    /// put in the folder the firms read; with the option off they are taken out again. Run at the start and on a switch,
    /// on a worker thread, one job at a time; unchanged files mean nothing is written. Nothing per frame, saved or sent.
    /// </summary>
    public static partial class DesignsDirector
    {
        public static IModLogger Logger;

        private static readonly object Gate = new object();      // one job at a time
        private static readonly object FilesGate = new object(); // one writer of the folder at a time
        private static string _root;                             // StreamingAssets, read on the main thread
        private static bool _running, _again;
        private static volatile bool _wanted;
        private static volatile DesignsReport _last;

        public static bool Working
        {
            get
            {
                lock (Gate)
                {
                    return _running;
                }
            }
        }

        /// <summary>Puts the designs in place or takes them away on a worker thread. Called on the main thread, which asks for the folder.</summary>
        public static void Ask(bool on)
        {
            _wanted = on;
            if (_root == null)
            {
                _root = DesignsGame.Root();
            }
            lock (Gate)
            {
                if (_running)
                {
                    _again = true;
                    return;
                }
                _running = true;
            }
            string root = _root;
            Task.Run(() => Work(root));
        }

        // On the worker: again while the option was switched meanwhile.
        private static void Work(string root)
        {
            while (true)
            {
                Run(root, _wanted);
                lock (Gate)
                {
                    if (!_again)
                    {
                        _running = false;
                        return;
                    }
                    _again = false;
                }
            }
        }

        private static DesignsReport Run(string root, bool on)
        {
            DesignsReport report;
            try
            {
                lock (FilesGate)
                {
                    report = on ? DesignFiles.Put(root, DesignsGame.Read, DesignsGame.WriteCut) : DesignFiles.Take(root);
                }
            }
            catch (Exception e)
            {
                report = new DesignsReport { On = on, Failed = true, Why = e.GetType().Name + ": " + e.Message };
            }
            _last = report;
            Say(report);
            return report;
        }

        private static void Say(DesignsReport r)
        {
            if (r.Failed)
            {
                Logger?.Warn("Designs: the ready-made designs could not be " + (r.On ? "put in" : "taken out of") + " the game's design folder ("
                             + r.Why + "); " + (r.On ? "the installation firms offer the mod's businesses none." : "some may be left there."));
                return;
            }
            if (!r.On)
            {
                if (r.Removed > 0 || r.Skipped > 0)
                {
                    Logger?.Info("Designs: " + r.Removed + " ready-made designs taken out of the game's design folder"
                                 + (r.Skipped > 0 ? ", " + r.Skipped + " could not be" : string.Empty) + " (" + r.Milliseconds + " ms).");
                }
                return;
            }
            Logger?.Info("Designs: " + r.Designs + " ready-made designs for " + r.Types + " of the mod's " + DesignCatalogue.Kinds.Length
                         + " business types are with the installation firms" + (r.Unchanged ? ", as they were" : ": " + r.Written + " written, " + r.Removed + " taken away")
                         + (r.Skipped > 0 ? ", " + r.Skipped + " left out (a file of that name is not the mod's)" : string.Empty) + " (" + r.Milliseconds + " ms).");
        }

        /// <summary>For removing the mod: takes the designs away now, on this thread, after any job under way. Returns how many.</summary>
        public static int TakeAway()
        {
            _wanted = false;
            if (_root == null)
            {
                _root = DesignsGame.Root();
            }
            return Run(_root, false).Removed;
        }

        /// <summary>With Developer diagnostics on, a self-check that the game's firm lists what was written. Main thread; a folder listing each.</summary>
        public static void CityLoaded()
        {
            if (!Meta.Dev.Verbose || !Settings.Designs || Working || _root == null)
            {
                return;
            }
            DesignManifest list = DesignFiles.List(_root);
            var groups = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string path in list.Files.Keys)
            {
                string group = path.Substring(0, path.LastIndexOf('/'));
                groups[group] = groups.TryGetValue(group, out int n) ? n + 1 : 1;
            }
            int same = 0;
            string first = null;
            foreach (KeyValuePair<string, int> group in groups)
            {
                string[] parts = group.Key.Split('/');
                DesignKind kind = KindOf(parts[2]);
                int listed = kind != null ? DesignsGame.Listed(kind.Building, parts[1], kind.Type) : -1;
                if (listed == group.Value)
                {
                    same++;
                }
                else if (first == null)
                {
                    first = group.Key + ": the firm lists " + listed + ", " + group.Value + " were written";
                }
            }
            Logger?.Info("Designs: the installation firms' own lists check (" + same + " of " + groups.Count + " business types and sizes list the designs written"
                         + (first != null ? "; " + first : string.Empty) + "): " + (groups.Count > 0 && first == null ? "PASS." : "FAIL."));
        }

        private static DesignKind KindOf(string folder)
        {
            for (int k = 0; k < DesignCatalogue.Kinds.Length; k++)
            {
                if (DesignRules.FolderOf(DesignCatalogue.Kinds[k].Type) == folder)
                {
                    return DesignCatalogue.Kinds[k];
                }
            }
            return null;
        }
    }
}
