using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps a handler on one of the game's event fields (entering and leaving a building, the city map, a new hour,
    /// the game's own events). The game empties those fields on every load, so a feature asks once a second to be
    /// kept on:
    ///
    ///     EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);
    ///
    /// The field is looked into only when it is not the one this holder last saw, and the handler is put on only
    /// when it is not in it. That second half is what makes it right with several holders on one event (three
    /// features keep the new hour, three the city map): one holder's putting itself on changes the field for all
    /// the others, and when each of them answered a changed field by taking itself off and putting itself back, each
    /// changed it for the rest again, for ever, two new delegates per holder per second. Now a holder that finds
    /// itself still in a changed field only remembers the field, so once every holder is on, the field stands
    /// still and asking costs one reference comparison. A handler stays where it was put: behind everything that was
    /// on the event at the time, as before.
    ///
    /// No Unity or game types: checked offline (NavCheck, HitchCheck.cs). <see cref="Looked"/> and
    /// <see cref="PutBack"/> are counted for the session record's line a minute, where a holder that puts itself
    /// back every second would show as sixty a minute.
    /// </summary>
    public static class EventHold
    {
        /// <summary>Times a changed field was looked into, and times a handler was put on, since the mod started.</summary>
        public static int Looked { get; private set; }
        public static int PutBack { get; private set; }

        public static void Keep<T>(ref T field, T handler, ref T seen) where T : Delegate
        {
            if (handler == null || (seen != null && ReferenceEquals(field, seen)))
            {
                return;   // nothing has touched the event since this holder last looked
            }
            Looked++;
            if (!Holds(field, handler))
            {
                field = (T)Delegate.Combine(field, handler);
                PutBack++;
            }
            seen = field;
        }

        /// <summary>
        /// Whether a handler is in an event's list. Only called for a field that has changed; the list of a field
        /// with several handlers is copied by the runtime to be read (one array), which is the whole cost.
        /// </summary>
        public static bool Holds(Delegate field, Delegate handler)
        {
            if (field == null || handler == null)
            {
                return false;
            }
            if (ReferenceEquals(field, handler))
            {
                return true;
            }
            Delegate[] list = field.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(handler))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
