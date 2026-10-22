using System;
using System.Collections.Generic;
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
    /// <see cref="KeepEssential"/>. Then the lines a store could not read are put back as read (<see cref="SaveLines"/>).
    /// </summary>
    public static class SaveHook
    {
        private static readonly List<Action> Handlers = new List<Action>(16);
        private static readonly List<FeatureGuard> Guards = new List<FeatureGuard>(16);   // in step with Handlers
        private static readonly HashSet<Action> Essential = new HashSet<Action>();
        private static readonly HashSet<FeatureGuard> Skipped = new HashSet<FeatureGuard>();   // said once each
        private static readonly Action Relay = Run;
        private static Action _seen;   // the game's event when the relay was last put on it

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
                Logger?.Info("Session: the mod wrote its state into the save in "
                             + ((UnityEngine.Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture)
                             + " ms (" + Handlers.Count + " features); the rest of the save is the game's.");
            }
        }
    }
}
