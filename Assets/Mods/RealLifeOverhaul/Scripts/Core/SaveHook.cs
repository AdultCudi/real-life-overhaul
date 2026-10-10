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
    /// does not cost the others their save.
    /// </summary>
    /// <summary>
    /// The same idea for any other of the game's event fields a feature must stay on (entering and leaving a
    /// building, the city map, the game's own events): the handler is taken off and put back only when the field
    /// is not the one last seen, so asking every second allocates nothing while nothing has changed. The handler
    /// ends up last in line, as it did when it was put back every time.
    ///
    ///     EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);
    /// </summary>
    public static class EventHold
    {
        public static void Keep<T>(ref T field, T handler, ref T seen) where T : Delegate
        {
            if (handler == null || (seen != null && ReferenceEquals(field, seen)))
            {
                return;
            }
            field = (T)Delegate.Combine(Delegate.Remove(field, handler), handler);
            seen = field;
        }
    }

    public static class SaveHook
    {
        private static readonly List<Action> Handlers = new List<Action>(16);
        private static readonly Action Relay = Run;
        private static Action _seen;   // the game's event as it was when the relay was last put on it

        public static IModLogger Logger;

        public static void Keep(Action handler)
        {
            if (handler != null && !Handlers.Contains(handler))
            {
                Handlers.Add(handler);
            }
            if (!ReferenceEquals(GlobalEvents.onSaveGame, _seen) || _seen == null)
            {
                GlobalEvents.onSaveGame -= Relay;
                GlobalEvents.onSaveGame += Relay;
                _seen = GlobalEvents.onSaveGame;
            }
        }

        public static void Drop(Action handler)
        {
            if (handler != null)
            {
                Handlers.Remove(handler);
            }
            if (Handlers.Count == 0)
            {
                GlobalEvents.onSaveGame -= Relay;
                _seen = null;
            }
        }

        private static void Run()
        {
            SessionWatch.Mark("the game is saving (the mod writes its state into the save first)");
            bool timed = Meta.Dev.Verbose;
            float began = timed ? UnityEngine.Time.realtimeSinceStartup : 0f;
            for (int i = 0; i < Handlers.Count; i++)
            {
                try
                {
                    Handlers[i]();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            if (timed)
            {
                Logger?.Info("Session: the mod wrote its state into the save in " + ((UnityEngine.Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms (" + Handlers.Count + " features); the rest of the save is the game's.");
            }
        }
    }
}
