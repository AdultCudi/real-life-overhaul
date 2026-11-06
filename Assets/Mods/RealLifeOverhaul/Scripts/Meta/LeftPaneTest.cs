using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Options > Tests: the left pane of every app of the mod on the phone, opened in turn and measured where it stands. For each app
    /// the page it wrote when it opened, for Career and Campus the longest pages the mod has (the profession with the most rungs, the
    /// courses with the longest names and timetables), and a page of every kind of part in the longest words each of the nine
    /// languages can ask for (LeftPaneFixture). Each is measured by InfoBox.Audit (no text taller than its place, no two texts'
    /// boxes crossing, nothing past the edge, the scroll reaching the page's end) and logged as one line, "Left pane check (app,
    /// page): PASS" or FAIL and why; the last line says PASS only if every page did. A page the test wrote is replaced when the app
    /// is opened again. Costs nothing unless it is run; the work is spread over frames, one page a frame.
    /// </summary>
    public static class LeftPaneTest
    {
        private const int OpenFrames = 3;   // frames an app is given to write its page after it opens
        private static bool _running;

        /// <summary>Starts the test; a second press while it runs does nothing.</summary>
        public static void Run()
        {
            try
            {
                if (_running)
                {
                    return;
                }
                _running = true;
                MetaUi.Instance.Run(Routine());
            }
            catch (Exception e)
            {
                _running = false;
                InfoBox.Logger?.Error(e);
            }
        }

        private static IEnumerator Routine()
        {
            int apps = 0, pages = 0, faults = 0, skipped = 0;
            try
            {
                var shells = new List<AppShell>(AppShell.Made);
                for (int i = 0; i < shells.Count; i++)
                {
                    AppShell shell = shells[i];
                    if (shell == null || shell.Root == null || shell.Box == null)
                    {
                        continue;
                    }
                    if (!PhoneApp.OpenById(shell.Name))
                    {
                        skipped++;
                        InfoBox.Logger?.Info("Left pane check (" + shell.Name + "): SKIPPED, the app is not on the phone.");
                        continue;
                    }
                    for (int f = 0; f < OpenFrames; f++)
                    {
                        yield return null;
                    }
                    if (!shell.Root.activeInHierarchy)
                    {
                        skipped++;
                        InfoBox.Logger?.Info("Left pane check (" + shell.Name + "): SKIPPED, the app did not open.");
                        continue;
                    }
                    apps++;
                    faults += Measure(shell, "the page it wrote", ref pages);
                    yield return null;
                    List<Action> longest = Longest(shell.Name, out List<string> names);
                    for (int p = 0; p < longest.Count; p++)
                    {
                        longest[p]();
                        for (int f = 0; f < OpenFrames; f++)
                        {
                            yield return null;
                        }
                        faults += Measure(shell, names[p], ref pages);
                        yield return null;
                    }
                    for (int l = 0; l < LeftPaneFixture.Languages.Length; l++)
                    {
                        LeftPaneFixture.Words words = LeftPaneFixture.Languages[l];
                        LeftPaneFixture.Write(shell.Box, words, true);
                        faults += Measure(shell, "every part in " + words.Code, ref pages);
                        yield return null;
                        LeftPaneFixture.Write(shell.Box, words, false);   // no buttons: the foot holds only the Guide's page
                        faults += Measure(shell, "every part in " + words.Code + ", no buttons", ref pages);
                        yield return null;
                    }
                }
                CloseMenu();
                bool ok = faults == 0 && apps > 0;
                InfoBox.Logger?.Info("Left pane check (all apps): " + apps + " apps and " + pages + " pages measured, " + skipped
                                     + " apps skipped, " + faults + " faults: " + (ok ? "PASS" : "FAIL") + ".");
                string said = ok ? "reallife_leftpane_test_pass" : apps == 0 ? "reallife_leftpane_test_none" : "reallife_leftpane_test_fail";
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, said, null, 8f, null, null, true, false);
            }
            finally
            {
                _running = false;
            }
        }

        // One page measured and its line logged; the faults it had.
        private static int Measure(AppShell shell, string page, ref int pages)
        {
            var said = new StringBuilder();
            int faults = shell.Box.Audit(said);
            pages++;
            InfoBox.Logger?.Info("Left pane check (" + shell.Name + ", " + page + "): " + shell.Box.Heights() + (faults == 0 ? ": PASS." : ": FAIL," + said + "."));
            return faults;
        }

        // The longest pages the mod has for the two apps that have them, as ways to open the app on them.
        private static List<Action> Longest(string app, out List<string> names)
        {
            var ways = new List<Action>(3);
            names = new List<string>(3);
            CareersDirector director = CareersDirector.Instance;
            if (director == null)
            {
                return ways;
            }
            if (app == CareersDirector.CareerAppId)
            {
                int best = 0;
                for (int i = 1; i < ProCatalogue.All.Length; i++)
                {
                    best = ProCatalogue.All[i].Ranks.Length > ProCatalogue.All[best].Ranks.Length ? i : best;
                }
                int id = ProCatalogue.All[best].Id;
                ways.Add(() => director.OpenCareer(CareersDirector.TabStart, CareerApp.ProfessionRow + id));
                names.Add("the profession with the most rungs");
            }
            else if (app == CareersDirector.CampusAppId)
            {
                int[] courses = { StudyCatalogue.Chef, StudyCatalogue.ComputerScience, StudyCatalogue.MscComputing };
                string[] what = { "a vocational course with its timetable choice", "a degree with the student loan", "a postgraduate course" };
                for (int i = 0; i < courses.Length; i++)
                {
                    int id = courses[i];
                    ways.Add(() => director.OpenCampus(CareersDirector.TabCourses, CampusApp.CourseRow + id));
                    names.Add(what[i]);
                }
            }
            return ways;
        }

        private static void CloseMenu()
        {
            try
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                if (uis != null && uis.fullMenu != null && UI.Smartphone.FullMenu.IsOpen)
                {
                    uis.fullMenu.Toggle(false);
                }
            }
            catch (Exception e)
            {
                InfoBox.Logger?.Error(e);
            }
        }
    }
}
