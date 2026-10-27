using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.BizPace;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>The session record, part: the heap-leap line, the minute's line pieces and the count of the mod's objects.</summary>
    public sealed partial class SessionWatch
    {
        // A frame in which the heap grew by megabytes at once: how much, and how much under which of the mod's own scripts.
        private void Leapt(long grown, float took)
        {
            int frame = Time.frameCount - 1;
            _line.Length = 0;
            _line.Append("Session: the managed heap grew by ").Append(Megabytes(grown)).Append(" MB in one frame of ").Append((took * 1000f).ToString("F0"))
                .Append(" ms; under the mod's own scripts: ");
            long own = HotSpots.FrameBytes(frame, out bool sampled);
            if (!sampled)
            {
                _line.Append(HotSpots.FrameMs(frame) > 0.0 ? "not known (not a sampled frame)" : "nothing (none ran)");
            }
            else
            {
                _line.Append(own / 1024).Append(" KB");
                for (int k = 0; k < 3 && HotSpots.FrameTopBytes(frame, k, out int slot, out long bytes) && bytes >= 1024; k++)
                {
                    _line.Append(k == 0 ? " (" : ", ").Append(HotSpots.Name(slot)).Append(' ').Append(bytes / 1024).Append(" KB");
                    if (k == 2 || !HotSpots.FrameTopBytes(frame, k + 1, out _, out long next) || next < 1024)
                    {
                        _line.Append(')');
                    }
                }
                _line.Append(own * 2 < grown ? "; the rest is the game's, or another mod's" : string.Empty);
            }
            _line.Append("; the mod's last pieces of work: ");
            float began = Time.unscaledTime - took;
            for (int back = 0; back < Marks.Count; back++)
            {
                Marks.Get(back, out string text, out int textFrame, out float at, out _);
                _line.Append(back > 0 ? ", \"" : "\"").Append(text).Append("\" ");
                Ago(textFrame, at, frame, began);
            }
            if (Marks.Count == 0)
            {
                _line.Append("none yet");
            }
            _line.Append("; ").Append(Where()).Append('.');
            Logger?.Info(_line.ToString());
        }

        // "in it", "just after it" or "1.2 s before": when something happened, seen from the long frame.
        private void Ago(int happenedFrame, float happenedAt, int frame, float began)
        {
            if (happenedFrame > frame)
            {
                _line.Append("just after it");
            }
            else if (happenedFrame == frame)
            {
                _line.Append("in it");
            }
            else
            {
                _line.Append(Mathf.Max(0f, began - happenedAt).ToString("F1", CultureInfo.InvariantCulture)).Append(" s before");
            }
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        private void Growers(ObjectKinds tally)
        {
            int found = tally.Rank();
            for (int k = 0; k < found; k++)
            {
                tally.Top(k, out string kind, out int grown, out int now);
                _line.Append(k > 0 ? ", " : string.Empty).Append(kind).Append(" +").Append(grown).Append(" (").Append(now).Append(" now)");
            }
            _line.Append(found == 0 ? "none grew" : string.Empty);
        }

        private static string Signed(long value) => value >= 0 ? "+" + value : value.ToString(CultureInfo.InvariantCulture);

        private static long Megabytes(long bytes) => bytes / (1024L * 1024L);

        // Where the minute's frames went (the frames the step timing covered whole), as the long-frame line splits one.
        private void Went()
        {
            if (_split == 0)
            {
                _line.Append("; where the frames went: not timed");
                return;
            }
            _line.Append("; waiting for the last frame to be shown ").Append(_waitSum.ToString("F0")).Append(" ms in the minute, ").Append(_waitWorst.ToString("F0"))
                .Append(" ms at most in a frame; outside the engine's steps ").Append(_outsideSum.ToString("F0")).Append(" ms, ").Append(_outsideWorst.ToString("F0")).Append(" ms at most");
            _line.Append("; the main thread ran ");
            if (_threadFrames > 0 && _threadSpan > 0.0)
            {
                _line.Append((_threadSum * 100.0 / _threadSpan).ToString("F0")).Append("% of the time");
            }
            else
            {
                _line.Append("for a time ").Append(MachineClock.ThreadWhy);
            }
            _line.Append("; other programs held ");
            if (_othersFrames > 0)
            {
                _line.Append((_othersSum * 100.0 / _othersFrames).ToString("F0")).Append("% of the processors on average and ")
                    .Append((_othersWorst * 100.0).ToString("F0")).Append("% at most in a frame");
            }
            else
            {
                _line.Append("a share ").Append(MachineClock.SystemWhy);
            }
            _line.Append("; page faults ");
            if (_faultFrames > 0)
            {
                _line.Append(_faultsSum).Append(" in the minute, ").Append(_faultsWorst).Append(" at most in a frame");
            }
            else
            {
                _line.Append(MachineClock.MemoryWhy);
            }
            _line.Append("; the frames over 33 ms: ");
            bool any = false;
            for (int c = 0; c < _causes.Length; c++)
            {
                if (_causes[c] > 0)
                {
                    _line.Append(any ? ", " : string.Empty).Append(_causes[c]).Append(' ').Append(FrameRules.Short((FrameCause)c));
                    any = true;
                }
            }
            _line.Append(any ? string.Empty : "none");
            _line.Append(_timing
                ? "; the graphics card " + _gpuWorst.ToString("F1", CultureInfo.InvariantCulture) + " ms at most in a frame (the engine's own figures)"
                : "; the engine's own frame timing: not offered by this build");
        }

        // The minute of the mod's own scripts (Core/HotSpots): their time a frame, the three that took the most, and the
        // heap's growth under them in the sampled frames, scaled to the minute and set beside the growth of the heap in all.
        private void OwnScripts(float seconds)
        {
            int frames = HotSpots.Frames;
            _line.Append("; the mod's own scripts: ").Append((frames > 0 ? HotSpots.TotalMs / frames : 0.0).ToString("F2", CultureInfo.InvariantCulture)).Append(" ms a frame on average and ")
                .Append(HotSpots.WorstFrameMs.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms at most in a frame");
            if (HotSpots.WorstFrameSlot >= 0)
            {
                // Which scope made it: the minute's top three are by their total, so a scope that costs 13 ms once a minute is not among them.
                _line.Append(" (mostly ").Append(HotSpots.Name(HotSpots.WorstFrameSlot)).Append(' ').Append(HotSpots.WorstFrameSlotMs.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms)");
            }
            for (int k = 0; k < 3 && HotSpots.StretchTop(k, false, out int slot, out double total, out double worst, out _); k++)
            {
                _line.Append(k == 0 ? ", the most in the minute: " : ", ").Append(HotSpots.Name(slot)).Append(" (")
                    .Append((frames > 0 ? total / frames : 0.0).ToString("F2", CultureInfo.InvariantCulture)).Append(" ms a frame, ").Append(worst.ToString("F1", CultureInfo.InvariantCulture))
                        .Append(" ms at worst)");
            }
            int sampled = HotSpots.Sampled;
            if (sampled > 0 && seconds > 0f)
            {
                double scale = (double)HotSpots.Frames / sampled / seconds / 1024.0;   // sampled bytes to kilobytes a second
                _line.Append("; the heap grew under them by ").Append(_exact ? string.Empty : "about ").Append((HotSpots.SampledBytes * scale).ToString("F0"))
                    .Append(_exact ? " KB a second (every frame counted)" : " KB a second (one frame in " + HotSpots.AllocEvery + " sampled)")
                    .Append(" of the ").Append((_grown / 1024.0 / seconds).ToString("F0")).Append(" KB a second it grew in all");
                for (int k = 0; k < 3 && HotSpots.StretchTop(k, true, out int slot, out _, out _, out long bytes) && bytes > 0L; k++)
                {
                    _line.Append(k == 0 ? ", the most under " : ", ").Append(HotSpots.Name(slot)).Append(" (").Append((bytes * scale).ToString("F0")).Append(" KB a second)");
                }
            }
            BizPaceWatch.MinuteLine(_line);   // BizMan's opens, switches and tab changes of the minute: what the mod added, the slowest handler named
            HotSpots.ClearStretch();
        }
        // Every object under a root of the mod's ("RealLifeOverhaul ...") in every loaded scene, by holder and by kind; the
        // scene kept across loads is counted for the kinds alone, so the first line's figures keep their meaning.
        private void CountObjects(ref int objects, ref int shown, ref int roots, int minute)
        {
            _holders.Begin();
            _kinds.Begin();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (scene.isLoaded)
                {
                    CountScene(scene, ref objects, ref shown, ref roots);
                }
            }
            try
            {
                Scene kept = KeptScene();
                if (kept.IsValid() && kept.isLoaded)
                {
                    int keptShown = 0, keptRoots = 0;
                    _keptObjects = 0;
                    CountScene(kept, ref _keptObjects, ref keptShown, ref keptRoots);
                }
            }
            catch (Exception e)
            {
                _keptRead = true;
                _kept = default;   // an engine that will not list that scene: left out from now on, the rest of the record goes on
                _roots.Clear();
                _under.Clear();
                Logger?.Warn("Session: the objects kept across loads cannot be counted in this build (" + e.GetType().Name + ").");
            }
            _holders.End(minute);
            _kinds.End(minute);
        }
        // The two pools of characters: how many exist and how many are out.
        private static void People(StringBuilder line)
        {
            ActorPool actors = ActorPool.Instance;
            if (actors != null)
            {
                int made = actors.ActorCount, used = 0;
                for (int i = 0; i < made; i++)
                {
                    used += actors.ActorAt(i).InUse ? 1 : 0;
                }
                line.Append("; people ").Append(used).Append(" out, ").Append(made).Append(" made of ").Append(ActorPool.MaxActors);
            }
            CityLifeDirector city = CityLifeDirector.Instance;
            if (city != null && city.People != null && city.Props != null)
            {
                int made = city.People.Count;
                line.Append("; city people ").Append(made - city.People.Free).Append(" out, ").Append(made).Append(" made of ").Append(CityPeople.MaxPeople)
                    .Append(", city props made ").Append(city.Props.Made);
            }
        }

        private static string Where()
        {
            try
            {
                return (CityMap.IsOpen ? "on the city map, " : string.Empty)
                       + (BuildingManager.IsInsideBuilding ? "inside a building" : PerfGame.InVehicle() ? "in a vehicle" : "on foot in the street")
                       + ", game hour " + TimeHelper.CurrentHour;
            }
            catch (Exception)
            {
                return "place unknown";
            }
        }
    }
}
