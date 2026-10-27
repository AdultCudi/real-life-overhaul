using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BAModAPI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's one place in the game's "a save is being written" event, which the game empties on every load: one relay
    /// calls the features' handlers in the order they joined, and is put back only when the event is not the one last
    /// seen. Keep costs a reference comparison and a short list search.
    ///
    ///     private Action _onSave;                                   // in the feature
    ///     SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));         // as often as it likes
    ///     SaveHook.Drop(_onSave);                                   // in OnDestroy
    ///
    /// A handler that throws counts against its class's guard and costs the others nothing; one of a feature switched
    /// off after its faults is skipped, so its line stays as it last wrote it, unless kept with
    /// <see cref="KeepEssential"/>. Then the lines a store could not read are put back as read (<see cref="SaveLines"/>), and
    /// last the records that name the mod's own places leave the game's lists for the mod's section (Scripts/DistrictSave).
    /// </summary>
    public static class SaveHook
    {
        private static readonly List<Action> Handlers = new List<Action>(16);
        private static readonly List<FeatureGuard> Guards = new List<FeatureGuard>(16);   // in step with Handlers
        private static readonly HashSet<Action> Essential = new HashSet<Action>();
        private static readonly HashSet<FeatureGuard> Skipped = new HashSet<FeatureGuard>();   // said once each
        private static readonly Action Relay = Run;
        private static Action _seen;   // the game's event when the relay was last put on it
        private const int Named = 3;   // the longest steps the timed line names
        private static readonly long[] LongTicks = new long[Named];
        private static readonly string[] LongNames = new string[Named];
        private static readonly Delegate[] LongSteps = new Delegate[Named];

        public static IModLogger Logger;

        /// <summary>True only while save handlers put live projections back for serialization.</summary>
        internal static bool Saving { get; private set; }

        public static void Keep(Action handler)
        {
            if (handler != null && !Handlers.Contains(handler))
            {
                Handlers.Add(handler);
                Guards.Add(Guard.OfHandler(handler));
            }
            if (!ReferenceEquals(GlobalEvents.onSaveGame, _seen) || _seen == null)
            {
                GlobalEvents.onSaveGame -= Relay;
                GlobalEvents.onSaveGame += Relay;
                _seen = GlobalEvents.onSaveGame;
            }
        }

        /// <summary>As <see cref="Keep"/>, for a handler that runs whatever is switched off.</summary>
        public static void KeepEssential(Action handler)
        {
            if (handler != null)
            {
                Essential.Add(handler);
            }
            Keep(handler);
        }

        public static void Drop(Action handler)
        {
            int at = handler != null ? Handlers.IndexOf(handler) : -1;
            if (at >= 0)
            {
                Handlers.RemoveAt(at);
                Guards.RemoveAt(at);
                Essential.Remove(handler);
            }
            if (Handlers.Count == 0 && !DistrictSave.DistrictSaveDirector.Holds)   // the district's records leave every save, whatever stopped
            {
                GlobalEvents.onSaveGame -= Relay;
                _seen = null;
            }
        }

        private static void Run()
        {
            Saving = true;
            try
            {
                using var piece = SessionWatch.Piece("the game is saving (the mod writes its state into the save first)");
                bool timed = Meta.Dev.Verbose;
                float began = timed ? UnityEngine.Time.realtimeSinceStartup : 0f;
                if (timed)
                {
                    Array.Clear(LongTicks, 0, Named);
                    Array.Clear(LongNames, 0, Named);
                    Array.Clear(LongSteps, 0, Named);
                }
                for (int i = 0; i < Handlers.Count; i++)
                {
                    FeatureGuard guard = Guards[i];
                    if (guard != null && guard.Off && !Essential.Contains(Handlers[i]))
                    {
                        if (Skipped.Add(guard))
                        {
                            Logger?.Warn("Save: " + guard.Where + (guard.Away ? " stands down in this city" : " is switched off after its faults")
                                         + "; its line in the save is left as it last wrote it.");
                        }
                        continue;
                    }
                    long step = timed ? Stopwatch.GetTimestamp() : 0;
                    try
                    {
                        Handlers[i]();
                    }
                    catch (Exception e)
                    {
                        if (guard != null)
                        {
                            Guard.Fail(guard, e);
                        }
                        else
                        {
                            Logger?.Error(e);
                        }
                    }
                    if (timed)
                    {
                        Longest(Stopwatch.GetTimestamp() - step, null, Handlers[i]);
                    }
                }
                long district = timed ? Stopwatch.GetTimestamp() : 0;
                try
                {
                    int put = SaveLines.PutBack(SaveGameManager.Current != null ? SaveGameManager.Current.modData : null);
                    if (put > 0)
                    {
                        Logger?.Info("Save: " + put + " line(s) the mod could not read put back as they were read.");
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                long companion = timed ? Stopwatch.GetTimestamp() : 0;
                CompanionGame.Begin(SaveGameManager.Current);   // the readers now, while the mod's places are in the game's lists; the report's text on a worker
                if (timed)
                {
                    Longest(Stopwatch.GetTimestamp() - companion, "the companion's readers", null);
                }
                long strip = timed ? Stopwatch.GetTimestamp() : 0;
                try
                {
                    DistrictSave.DistrictSaveDirector.BeforeWrite();   // last: every feature has written; the mod's places leave the game's lists
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    CompanionGame.End(SaveGameManager.Current);   // the district save joins the report's text itself; if it fell short of that, here (nothing if it did)
                }
                if (timed)
                {
                    Longest(Stopwatch.GetTimestamp() - strip, "the district save and the companion's text awaited", null);
                }
                if (timed)
                {
                    Longest(Stopwatch.GetTimestamp() - district, "the kept lines, the companion and the district save together", null);
                    Logger?.Info("Session: the mod wrote its state into the save in "
                                 + ((UnityEngine.Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture)
                                 + " ms (" + Handlers.Count + " features; the longest: " + LongestText() + "); the rest of the save is the game's.");
                }
            }
            finally
            {
                Saving = false;
            }
        }

        // Keeps a step among the longest of this save (Developer diagnostics only): by its handler, named when the line is written.
        private static void Longest(long ticks, string name, Delegate step)
        {
            int at = Named;
            while (at > 0 && ticks > LongTicks[at - 1])
            {
                at--;
            }
            if (at == Named)
            {
                return;
            }
            for (int k = Named - 1; k > at; k--)
            {
                LongTicks[k] = LongTicks[k - 1];
                LongNames[k] = LongNames[k - 1];
                LongSteps[k] = LongSteps[k - 1];
            }
            LongTicks[at] = ticks;
            LongNames[at] = name;
            LongSteps[at] = step;
        }

        private static string LongestText()
        {
            var text = new System.Text.StringBuilder(128);
            for (int k = 0; k < Named && (LongNames[k] != null || LongSteps[k] != null); k++)
            {
                text.Append(k > 0 ? ", " : string.Empty).Append(LongNames[k] ?? NameOf(LongSteps[k])).Append(' ')
                    .Append((LongTicks[k] * 1000.0 / Stopwatch.Frequency).ToString("F1", CultureInfo.InvariantCulture)).Append(" ms");
            }
            return text.ToString();
        }

        // A handler as its class and method ("MarketDirector.OnSave"); a lambda by the class and method it is written in.
        private static string NameOf(Delegate step)
        {
            System.Reflection.MethodInfo m = step.Method;
            Type t = m.DeclaringType;
            while (t != null && t.Name.StartsWith("<", StringComparison.Ordinal) && t.DeclaringType != null)
            {
                t = t.DeclaringType;
            }
            string method = m.Name;
            int close = method.StartsWith("<", StringComparison.Ordinal) ? method.IndexOf('>') : -1;
            return (t != null ? t.Name : "?") + "." + (close > 1 ? method.Substring(1, close - 1) : method);
        }
    }
}
