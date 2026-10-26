using System.Diagnostics;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The director's part for the campus's own look (docs/features/schools.md, "The look"): the three models of the second props
    /// bundle stood behind the loading screen with the campus, the main school's over the copy engine's building and the hall and
    /// the junior block on their plots, each falling back to the copy engine's design alone (the main school keeping the look it
    /// has) when the bundle or its model is missing, with one line. One line at the load names what stood, its triangles and
    /// draw calls, the game's materials found and the time it took. Nothing per frame.
    /// </summary>
    public sealed partial class SchoolsDirector
    {
        private const float LookWaitSeconds = 30f;   // real seconds the props bundles are waited for at a load before the copy engine's designs are used

        private readonly LookHandle[] _looks = new LookHandle[3];   // the main school's, the hall's, the junior block's: the order of CampusLookRules.Models
        private GameObject _lookRoot;
        private bool _lookWait;                                      // the props bundles were not read when the campus stood: tried each second
        private float _lookWaitEnd;

        /// <summary>True while the model of a campus building (0 the main school, 1 the hall, 2 the junior block) stands.</summary>
        internal bool LookStands(int slot) => slot >= 0 && slot < _looks.Length && _looks[slot] != null && _looks[slot].Root != null;

        // Whether the props bundles have been read, well or not: what the look is decided from.
        private static bool BundlesSettled => PropLibrary.Settled;

        // Behind the loading screen, with the main school standing: the models stood where the bundle has them. True when the main
        // school's look stands, which then cuts its own ground in the walking meshes (the copy engine's footprint is not its shape).
        private bool StandLooks(MadeBuilding main)
        {
            long began = Stopwatch.GetTimestamp();
            TakeLooks();
            _lookRoot = new GameObject("RealLifeOverhaul Schools campus look");
            CampusLook.Capture(main.Controller);
            SchoolGrounds.Blocks(_plots);
            var said = new StringBuilder(480);
            string[] names = { "the main school", "the hall", "the junior block" };
            string why;
            for (int i = 0; i < CampusLookRules.Models.Length; i++)
            {
                string id = CampusLookRules.Models[i].Id;
                LookHandle h;
                if (!CampusLook.Has(id, out why))
                {
                    h = new LookHandle { Id = id, Name = names[i], Why = why };
                }
                else if (i == 0)
                {
                    h = CampusLook.StandMain(main, names[i], _lookRoot.transform);
                }
                else
                {
                    h = i - 1 < _plots.Count ? CampusLook.StandBlock(id, names[i], _plots[i - 1], _lookRoot.transform) : new LookHandle { Id = id, Name = names[i], Why = "no plot" };
                }
                if (h.Root != null)
                {
                    _looks[i] = h;
                    said.Append(said.Length > 0 ? "; " : string.Empty)
                        .Append(CampusLookRules.StoodLine(h.Name, h.Triangles, h.Draws, h.GameMaterials, h.StandIns, h.ReplacedTriangles, h.ReplacedDraws));
                }
                else
                {
                    Logger?.Info("Schools: " + CampusLookRules.FallbackLine(h.Name, h.Why) + ".");
                }
            }
            bool any = LookStands(0) || LookStands(1) || LookStands(2);
            if (any)
            {
                FarDistricts.Include(_lookRoot.transform);      // out of the picture with the district's far squares, as the copies are
                double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                int triangles = 0, draws = 0;
                for (int i = 0; i < _looks.Length; i++)
                {
                    triangles += _looks[i] != null ? _looks[i].Triangles : 0;
                    draws += _looks[i] != null ? _looks[i].Draws : 0;
                }
                Logger?.Info("Schools: the campus's own look stood: " + said + "; in all " + triangles + " triangles in " + draws + " draw call(s); the game's materials: " + CampusLook.FoundWords
                             + " (found in " + CampusLook.SearchMs.ToString("F1", CultureInfo.InvariantCulture) + " ms); placed in "
                             + ms.ToString("F1", CultureInfo.InvariantCulture) + " ms; lit as the copy engine's renderers "
                             + (CampusLook.Styled ? "are" : "are not known, the defaults used") + ".");
            }
            else
            {
                SchoolsGame.Take(ref _lookRoot);
            }
            return LookStands(0);
        }

        // Every model taken away, the main school's own look shown again.
        private void TakeLooks()
        {
            for (int i = 0; i < _looks.Length; i++)
            {
                CampusLook.Take(ref _looks[i]);
            }
            SchoolsGame.Take(ref _lookRoot);
        }

        // Once a second while the bundles were not read at the load: when they are (or give out), the models stand, or the copy engine's designs are asked for.
        private void LookTick()
        {
            if (!_lookWait)
            {
                return;
            }
            MadeBuilding main = SchoolsGame.Main;
            if (main == null || main.Controller == null)
            {
                _lookWait = false;
                return;
            }
            if (!BundlesSettled && Time.unscaledTime < _lookWaitEnd)
            {
                return;
            }
            _lookWait = false;
            if (!BundlesSettled)
            {
                Logger?.Info("Schools: the props bundles were not read in " + LookWaitSeconds + " s; the campus keeps the copy engine's designs.");
            }
            bool cut = StandLooks(main) || SchoolsGame.Cut(main);
            Ask();
            Logger?.Info("Schools: the campus's look decided late: the main school " + (cut ? "cut out of the walking meshes" : "not cut") + "; the hall and the junior block "
                         + (LookStands(1) ? "from the models" : "asked of the copy engine (" + Asked() + " of " + _plots.Count + ")") + ".");
        }

        // The look's end: everything it stands away and its materials let go.
        private void LeaveLook()
        {
            TakeLooks();
            CampusLook.Forget();
            _lookWait = false;
        }
    }
}
