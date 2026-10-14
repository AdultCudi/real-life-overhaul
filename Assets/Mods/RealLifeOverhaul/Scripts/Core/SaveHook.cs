using System;
using System.Collections.Generic;
using BAModAPI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's one place in the game's "a save is being written" event. The game empties that
    /// event on every load, so every feature used to take itself off and put itself back on it
    /// once a second: a dozen features, two new delegates each, every second, for nothing.
    /// Now one relay sits in the game's event and calls the features' handlers in the order
    /// they joined; it is put back only when the game's event is not the one last seen.
    ///
    ///     private Action _onSave;                                   // in the feature
    ///     SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));         // where it used to re-hook, as often as it likes
    ///     SaveHook.Drop(_onSave);                                   // in OnDestroy
    ///
    /// Keep costs a reference comparison and a short list search; nothing is allocated unless
    /// the handler is new or the game's event has changed. A handler that throws is logged and
    /// does not cost the others their save; it counts against its class's guard (Core/Guard), and
    /// the handler of a feature switched off after its faults is not called, so that its line in
    /// the save stays as it last wrote it. <see cref="KeepEssential"/> is for what must be done
    /// whatever is off (the mod's moods taken out of the save, so that it loads without the mod).
    /// Once every handler has written, the lines a store could not read are put back as they
    /// were read (Core/SaveLines).
    ///
    /// Any other of the game's event fields is kept with <see cref="EventHold"/> (Core/EventHold.cs).
    /// </summary>
    public static class SaveHook
    {
        private static readonly List<Action> Handlers = new List<Action>(16);
        private static readonly List<FeatureGuard> Guards = new List<FeatureGuard>(16);   // in step with Handlers
        private static readonly HashSet<Action> Essential = new HashSet<Action>();
        private static readonly HashSet<FeatureGuard> Skipped = new HashSet<FeatureGuard>();   // said once each
        private static readonly Action Relay = Run;
        private static Action _seen;   // the game's event as it was when the relay was last put on it

        public static IModLogger Logger;

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
            if (Handlers.Count == 0)
            {
                GlobalEvents.onSaveGame -= Relay;
                _seen = null;
            }
        }

        private static void Run()
        {
            using var piece = SessionWatch.Piece("the game is saving (the mod writes its state into the save first)");
            bool timed = Meta.Dev.Verbose;
            float began = timed ? UnityEngine.Time.realtimeSinceStartup : 0f;
            for (int i = 0; i < Handlers.Count; i++)
            {
                FeatureGuard guard = Guards[i];
                if (guard != null && guard.Off && !Essential.Contains(Handlers[i]))
                {
                    if (Skipped.Add(guard))
                    {
                        Logger?.Warn("Save: " + guard.Where + " is switched off after its faults; its line in the save is left as it last wrote it.");
                    }
                    continue;
                }
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
            }
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
            if (timed)
            {
                Logger?.Info("Session: the mod wrote its state into the save in " + ((UnityEngine.Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms (" + Handlers.Count + " features); the rest of the save is the game's.");
            }
        }
    }
}
