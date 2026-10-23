using System;
using System.Collections.Generic;
using System.IO;

namespace RealLifeOverhaul.Texts
{
    /// <summary>RLO Texts' state file (the speed measured, what was told once) and the status row of the options page.</summary>
    public sealed partial class TextsDirector
    {
        // The state file: the speed measured, and what was told once.
        private static string StateFile => Path.Combine(TextsGame.Home, "state.txt");

        private static TextSpeedResult KnownSpeed(string wanted)
        {
            try
            {
                if (!File.Exists(StateFile))
                {
                    return null;
                }
                foreach (string line in File.ReadAllLines(StateFile))
                {
                    if (line.StartsWith("speed=", StringComparison.Ordinal))
                    {
                        TextSpeedResult r = TextSpeedResult.Read(line.Substring(6));
                        if (r != null && r.For == wanted)
                        {
                            return r;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // no file: a test this time
            }
            return null;
        }

        private static void KeepSpeed(TextSpeedResult speed)
        {
            Rewrite("speed=", "speed=" + speed.Line());
        }

        // Whether RLO Texts Large is among the packs found, run or not: it is then never offered.
        private static bool LargeFound()
        {
            for (int i = 0; _packs != null && i < _packs.Count; i++)
            {
                if (_packs[i].Rank == TextPacks.LargeTier)
                {
                    return true;
                }
            }
            return false;
        }

        // The value of the state file's line starting so; null without one.
        private static string StateLine(string start)
        {
            try
            {
                if (!File.Exists(StateFile))
                {
                    return null;
                }
                foreach (string line in File.ReadAllLines(StateFile))
                {
                    if (line.StartsWith(start, StringComparison.Ordinal))
                    {
                        return line.Substring(start.Length);
                    }
                }
            }
            catch (Exception)
            {
                // unreadable: as if not there
            }
            return null;
        }

        private static bool Told(string what)
        {
            try
            {
                return File.Exists(StateFile) && Array.IndexOf(File.ReadAllLines(StateFile), "told=" + what) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Remember(string what)
        {
            Rewrite("told=" + what, "told=" + what);
        }

        // The state file with the line starting so replaced, or added.
        private static void Rewrite(string start, string line)
        {
            try
            {
                Directory.CreateDirectory(TextsGame.Home);
                var lines = new List<string>(File.Exists(StateFile) ? File.ReadAllLines(StateFile) : new string[0]);
                lines.RemoveAll(l => l.StartsWith(start, StringComparison.Ordinal));
                lines.Add(line);
                File.WriteAllLines(StateFile, lines.ToArray());
            }
            catch (Exception e)
            {
                Logger?.Warn("RLO Texts: the state file could not be written (" + e.Message + ").");
            }
        }

        /// <summary>The status as the options page's status row shows it.</summary>
        public static Dictionary<string, string> Status(out string key)
        {
            var data = new Dictionary<string, string>(10);
            if (!Settings.Texts)
            {
                key = "reallife_texts_status_off";
                return data;
            }
            if (!_found)
            {
                key = "reallife_texts_status_looking";
                return data;
            }
            if (_pack == null && _refused != null)
            {
                key = "reallife_texts_status_failed";
                data["pack"] = _refused.Name;
                data["why"] = Cap("pack files changed: " + _refusedWhy);
                return data;
            }
            if (_pack == null && _passedWhy != null && _verified != null)
            {
                key = "reallife_texts_status_failed";
                data["pack"] = _verified.Name;
                data["why"] = Cap(_passedWhy);
                return data;
            }
            if (_pack == null)
            {
                key = "reallife_texts_status_none";
                data["folder"] = "LocalLow/\u2026/" + TextPacks.Fixed;
                return data;
            }
            data["pack"] = _pack.Name;
            data["device"] = _device != null ? _deviceLabel ?? _device : TextsText.Processor();
            data["threads"] = _threads.ToString();
            data["model"] = Hub.Shown.ToString();
            data["written"] = Hub.Fallbacks.ToString();
            data["hits"] = Hub.Budget.HitPercent.ToString();
            TextServerState state = _server != null ? _server.State : TextServerState.Off;
            TextSpeedResult speed = _server != null ? _server.Speed : null;
            data["seconds"] = speed != null ? speed.Estimate.ToString("0.0") : "?";
            data["speed"] = speed != null ? speed.TokensPerSecond.ToString("0") : "?";
            data["mean"] = _server != null && _server.Written > 0 ? _server.MeanSeconds.ToString("0.0") : "?";
            key = state == TextServerState.Ready ? "reallife_texts_status_ready" : state == TextServerState.Failed ? "reallife_texts_status_failed" : "reallife_texts_status_loading";
            if (state == TextServerState.Failed)
            {
                data["why"] = Cap(_server.Why ?? "?");
            }
            return data;
        }

        // A reason short enough for a notice, which holds 240 characters.
        private static string Cap(string why)
        {
            return why.Length <= WhyMost ? why : why.Substring(0, WhyMost - 1) + "\u2026";
        }

        /// <summary>The options page's status button: the status as one of the game's notices.</summary>
        public static void ShowStatus()
        {
            Dictionary<string, string> data = Status(out string key);
            TextsGame.Notice(key, data, 16f);
            if (Settings.Texts && Settings.TextsService != 0)
            {
                ShowServiceStatus();   // Your own AI's own line beside the pack's
            }
        }
    }
}
