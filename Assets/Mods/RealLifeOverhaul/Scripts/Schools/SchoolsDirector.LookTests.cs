using System;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The campus look's test entries (Options > Tests): "stand its own look again" puts the three models away and stands them anew
    /// (the main school's own look shown, then hidden again) and judges each against its model: materials, boxes, the door's place;
    /// "compare its look with the city's designs" asks the copy engine for the hall's and the junior block's designs for a moment, counts
    /// what they and the main school's own building draw, and sets it beside the models'. Each writes one "Schools check (...)" line
    /// with PASS or FAIL and the figures.
    /// </summary>
    public sealed partial class SchoolsDirector
    {
        private const float CompareSeconds = 60f;   // real seconds the copy engine is given to stand the two designs of the comparison

        private static readonly CopyHandle[] Compared = new CopyHandle[2];
        private static float _compareUntil;

        /// <summary>"Birchwood School: stand its own look again": the models away and back, each one judged; the time it takes to stand them is in the line.</summary>
        public static void TestLook()
        {
            const string test = "Schools look";
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                MadeBuilding main = SchoolsGame.Main;
                SchoolsDirector d = Instance;
                string name = SchoolsText.T(SchoolsText.TestLook);
                if (main == null || main.Controller == null)
                {
                    Done("Schools check (look): the main school's building is not there", false, name);
                    return;
                }
                if (!CampusLook.Searched)
                {
                    string why;
                    Done("Schools check (look): the game's materials were not looked for at this load (the look was not stood then: "
                         + (CampusLook.Has(CampusLookRules.MainModel, out why) ? "load the city again" : why) + ")", false, name);
                    return;
                }
                d._lookWait = false;
                d.DropCopies();
                bool cut = d.StandLooks(main) || SchoolsGame.Cut(main);
                d.Ask();
                d._cutCopies = 0;
                var t = new StringBuilder(500);
                bool pass = true;
                for (int i = 0; i < d._looks.Length; i++)
                {
                    LookHandle h = d._looks[i];
                    string said;
                    string problem = CampusLook.Judge(h, out said);
                    pass &= problem == null;
                    t.Append(t.Length > 0 ? "; " : string.Empty).Append(problem == null ? said : (h != null ? h.Name : CampusLookRules.Models[i].Id) + " not whole: " + problem);
                }
                t.Append("; the main school ").Append(cut ? "cut out of the walking meshes" : "not cut").Append("; the game's materials: ").Append(CampusLook.FoundWords);
                Done("Schools check (look): " + t, pass, name);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Birchwood School: compare its look with the city's designs": the triangles and draw calls of each model against the design it replaces.</summary>
        public static void TestCompare()
        {
            const string test = "Schools compare";
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                SchoolsDirector d = Instance;
                string name = SchoolsText.T(SchoolsText.TestCompare);
                if (!d.LookStands(0) && !d.LookStands(1) && !d.LookStands(2))
                {
                    Done("Schools check (compare): no model of the campus stands, so there is nothing to compare", false, name);
                    return;
                }
                if (_compareUntil > 0f)
                {
                    return;   // already waiting for the copy engine
                }
                SchoolGrounds.Blocks(d._plots);
                for (int i = 0; i < Compared.Length; i++)
                {
                    SchoolsGame.Remove(Compared[i]);
                    Compared[i] = i < d._plots.Count ? SchoolsGame.Plot(d._plots[i]) : null;
                }
                if (Compared[0] == null && Compared[1] == null)
                {
                    Done("Schools check (compare): the copy engine does not take requests (New buildings is off)", false, name);
                    return;
                }
                _compareUntil = Time.unscaledTime + CompareSeconds;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Once a second: the comparison's two designs, once they stand (or the copy engine gave up, or its minute is over), counted and taken away.
        private void CompareTick()
        {
            if (_compareUntil <= 0f)
            {
                return;
            }
            bool waiting = false;
            for (int i = 0; i < Compared.Length; i++)
            {
                waiting |= Compared[i] != null && !Compared[i].Ready && !Compared[i].Gone;
            }
            if (waiting && Time.unscaledTime < _compareUntil)
            {
                return;
            }
            _compareUntil = 0f;
            var t = new StringBuilder(500);
            bool pass = true;
            string[] names = { "the main school", "the hall", "the junior block" };
            LookHandle main = _looks[0];
            if (main != null && main.Root != null)
            {
                MadeBuilding made = SchoolsGame.Main;
                t.Append(CompareLine(names[0], main, main.ReplacedTriangles, main.ReplacedDraws, made != null ? made.Design : null));
                pass &= main.Draws <= CampusLookRules.MostDraws && main.ReplacedDraws > 0;
            }
            for (int i = 0; i < Compared.Length; i++)
            {
                LookHandle model = _looks[i + 1];
                CopyHandle copy = Compared[i];
                int triangles = 0, draws = 0;
                if (copy != null && copy.Ready)
                {
                    CampusLook.Measure(copy.Root, ref triangles, ref draws);
                }
                if (model != null && model.Root != null)
                {
                    t.Append(t.Length > 0 ? "; " : string.Empty).Append(CompareLine(names[i + 1], model, triangles, draws, copy != null ? copy.Wears : null));
                    pass &= model.Draws <= CampusLookRules.MostDraws && draws > 0;
                }
                SchoolsGame.Remove(copy);
                Compared[i] = null;
            }
            Done("Schools check (compare): " + (t.Length > 0 ? t.ToString() : "nothing stood to compare"), pass && t.Length > 0, SchoolsText.T(SchoolsText.TestCompare));
        }

        private static string CompareLine(string name, LookHandle model, int designTriangles, int designDraws, string design)
        {
            return name + ": the model " + model.Triangles.ToString(CultureInfo.InvariantCulture) + " triangles in " + model.Draws + " draw call(s); the design " + (design ?? "?") + " "
                   + (designDraws > 0 ? designTriangles.ToString(CultureInfo.InvariantCulture) + " triangles in " + designDraws + " draw call(s)" : "could not be counted");
        }
    }
}
