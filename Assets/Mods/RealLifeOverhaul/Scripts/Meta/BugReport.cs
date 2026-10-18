using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// "Report a bug", under Start here and in the Settings app. The mod is public, so it sends nothing and holds no token
    /// or webhook: it gathers the player's words, the facts (<see cref="BugReportGame.Facts"/>), the screen, both logs with the
    /// user folder taken out and the settings into a folder under Presets.Folder/Reports, zipped on a worker thread, for the
    /// player to post in the mod's Discord. Nothing per frame while no report is made; while one is, a look at the worker
    /// every tenth of a second.
    /// </summary>
    public static class BugReport
    {
        private const string TitleKey = "reallife_bug_title";
        private const float WaitStep = 0.1f;       // real seconds between looks at the worker
        private const float MenuStep = 0.25f;      // real seconds between looks at a menu of the game's that is in the way
        private const float GiveUpAfter = 600f;    // real seconds a report waits for a menu to close before it is forgotten

        private static bool _busy;                 // a report is being made: a second press says so
        private static bool _waiting;              // the text box waits for a menu of the game's to close

        /// <summary>The button: asks for the words where the game can, or asks first where it cannot.</summary>
        public static void Press()
        {
            try
            {
                if (_busy)
                {
                    OptionsNotice.Tell(TitleKey, "reallife_bug_busy".GetLocalization());
                    return;
                }
                if (!BugReportGame.CanAsk)
                {
                    OptionsNotice.Ask(TitleKey, "reallife_bug_ask_menu".GetLocalization(), "reallife_bug_make", false, () => Make(null));
                    return;
                }
                if (GuideApp.Blocked)
                {
                    Notifications.Show(NotificationType.Info, "reallife_bug_waits", null, 6f);
                    if (!_waiting)
                    {
                        _waiting = true;
                        MetaUi.Instance.Run(WaitForMenus());
                    }
                    return;
                }
                Ask();
            }
            catch (Exception e)
            {
                Fail(e);   // said: a press that did nothing would look like no button at all
            }
        }

        private static IEnumerator WaitForMenus()
        {
            var wait = new WaitForSecondsRealtime(MenuStep);
            float giveUp = Time.realtimeSinceStartup + GiveUpAfter;
            while (GuideApp.Blocked && BugReportGame.CanAsk && Time.realtimeSinceStartup < giveUp)
            {
                yield return wait;
            }
            _waiting = false;
            if (!GuideApp.Blocked && BugReportGame.CanAsk && !_busy)
            {
                Ask();
            }
        }

        // The game's text box; without one, or when it fails, the report is made without the player's words.
        private static void Ask()
        {
            bool asked;
            try
            {
                asked = BugReportGame.AskWords(Make);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                asked = false;
            }
            if (!asked)
            {
                Make(null);
            }
        }

        /// <summary>What the worker is given and gives back.</summary>
        private sealed class Job
        {
            public BugFacts Facts;
            public string Settings;
            public string Folder;
            public string GameLog;
            public BugReportGame.Shot Shot;
            public string Zip;            // the zip made, or null
            public Exception Fault;       // what stopped the worker, or null
        }

        private static void Make(string words)
        {
            if (_busy)
            {
                return;
            }
            _busy = true;
            try
            {
                MetaUi.Instance.Run(Making(words));
            }
            catch (Exception e)
            {
                _busy = false;
                MetaLog.Error(e);
            }
        }

        private static IEnumerator Making(string words)
        {
            Job job = null;
            Task work = null;
            try
            {
                string reports = Path.Combine(Presets.Folder, "Reports");
                job = new Job
                {
                    Facts = BugReportGame.Facts(words),
                    Settings = Presets.ExportText(),
                    Folder = Path.Combine(reports, BugReportRules.FolderName(DateTime.Now)),
                    GameLog = BugReportGame.GameLog()
                };
            }
            catch (Exception e)
            {
                Fail(e);
                yield break;
            }

            // The screen a frame after the text box closed, at the end of that frame: without the box, as the player saw the bug.
            if (BugReportGame.InCity)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                job.Shot = BugReportGame.TakeShot();
            }

            try
            {
                Job given = job;
                work = Task.Run(() => Write(given));
            }
            catch (Exception e)
            {
                BugReportGame.Drop(job.Shot);
                Fail(e);
                yield break;
            }
            var wait = new WaitForSecondsRealtime(WaitStep);
            while (!work.IsCompleted)
            {
                yield return wait;
            }

            try
            {
                Done(job);
            }
            catch (Exception e)
            {
                Fail(e);
                yield break;
            }
            _busy = false;
        }

        // On the worker: older reports cleared, each file on its own (a failure is said in report.txt), report.txt last, then the zip.
        private static void Write(Job job)
        {
            try
            {
                string reports = Path.GetDirectoryName(job.Folder);
                Directory.CreateDirectory(reports);
                Clear(reports);
                job.Folder = Path.Combine(reports, BugReportRules.UniqueName(Path.GetFileName(job.Folder),
                    name => Directory.Exists(Path.Combine(reports, name)) || File.Exists(Path.Combine(reports, name + ".zip"))));
                Directory.CreateDirectory(job.Folder);
                BugFacts f = job.Facts;
                f.Files.Add("report.txt (this file)");
                string logs = job.GameLog != null ? Path.GetDirectoryName(job.GameLog) : null;
                Part(f, "Player.log", "the game's log of this session", () => BugReportGame.CopyLog(job.GameLog, Path.Combine(job.Folder, "Player.log"), BugReportRules.LogCap, f.Profile));
                Part(f, "Player-prev.log", "the game's log of the session before",
                    () => logs != null && BugReportGame.CopyLog(Path.Combine(logs, "Player-prev.log"), Path.Combine(job.Folder, "Player-prev.log"), BugReportRules.PrevLogCap, f.Profile));
                Part(f, Presets.SettingsFile, "the mod's options, as Export writes them", () =>
                {
                    File.WriteAllText(Path.Combine(job.Folder, Presets.SettingsFile), job.Settings, new System.Text.UTF8Encoding(false));
                    return true;
                });
                if (job.Shot != null)
                {
                    BugReportGame.Shot shot = job.Shot;
                    job.Shot = null;
                    Part(f, "screenshot.jpg", "the screen as the report was made, " + shot.Width + " x " + shot.Height + ", read in " + shot.ReadMs + " ms",
                        () => BugReportGame.WriteShot(shot, Path.Combine(job.Folder, "screenshot.jpg")));
                }
                File.WriteAllText(Path.Combine(job.Folder, "report.txt"), BugReportRules.Text(f), new System.Text.UTF8Encoding(false));
                job.Zip = BugReportGame.Zip(job.Folder);
            }
            catch (Exception e)
            {
                job.Fault = e;
            }
            finally
            {
                BugReportGame.Drop(job.Shot);
            }
        }

        // One file of the report, on its own: written, not there (said nothing of), or failed, which report.txt says with the reason.
        private static void Part(BugFacts f, string name, string what, Func<bool> write)
        {
            try
            {
                if (write())
                {
                    f.Files.Add(name + " (" + what + ")");
                }
            }
            catch (Exception e)
            {
                f.Files.Add(BugReportRules.NotRead(name, e.GetType().Name + ": " + e.Message, f.Profile));
            }
        }

        // The older reports, so that with the new one there are BugReportRules.KeepReports; one that cannot be deleted is left.
        private static void Clear(string reports)
        {
            try
            {
                var names = new List<string>();
                foreach (string entry in Directory.GetFileSystemEntries(reports))
                {
                    names.Add(Path.GetFileName(entry));
                }
                foreach (string stem in BugReportRules.Older(names, BugReportRules.KeepReports))
                {
                    try
                    {
                        string folder = Path.Combine(reports, stem);
                        if (Directory.Exists(folder))
                        {
                            Directory.Delete(folder, true);
                        }
                        if (File.Exists(folder + ".zip"))
                        {
                            File.Delete(folder + ".zip");
                        }
                    }
                    catch (Exception)
                    {
                        // (left as it is: it is deleted with the next report, or by the player)
                    }
                }
            }
            catch (Exception)
            {
                // (the older reports stay: the new one is written all the same)
            }
        }

        // Back on the main thread: handed over, and said.
        private static void Done(Job job)
        {
            bool written = File.Exists(Path.Combine(job.Folder, "report.txt"));
            if (job.Fault != null)
            {
                MetaLog.Error(job.Fault);
                if (!written)
                {
                    Fail(job.Fault);
                    return;
                }
                MetaLog.Warn("the bug report's zip could not be made; its folder holds the report: " + job.Folder);
            }
            string file = job.Zip ?? job.Folder;
            BugFacts f = job.Facts;
            string summary = BugReportRules.Summary(f.ModVersion, f.GameBuild, f.Words, file, f.Profile);
            BugReportGame.HandOver(summary, job.Folder, job.Zip);
            MetaLog.Info("bug report written to " + file + " (" + f.Files.Count + " file(s), " + f.LinesTotal + " warning(s) and error(s) of the mod's this session).");
            OptionsNotice.Tell(TitleKey, (job.Zip != null ? "reallife_bug_done" : "reallife_bug_done_folder").GetLocalization() + "\n\n" + file);
        }

        private static void Fail(Exception e)
        {
            _busy = false;
            MetaLog.Error(e);
            try
            {
                OptionsNotice.Tell(TitleKey, "reallife_bug_failed".GetLocalization().Replace("{error}", BugReportRules.ErrorText(e.Message,
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))));
            }
            catch (Exception inner)
            {
                MetaLog.Error(inner);
            }
        }
    }
}
