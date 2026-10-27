using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps a handler on one of the game's event fields, which the game empties on every load; a feature asks once a
    /// second: <c>EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);</c>
    /// The field is looked into only when it changed since this holder last saw it, and the handler is put on only when
    /// it is not in it: several holders on one event would otherwise keep changing the field for each other. Once every
    /// holder is on, asking costs one reference comparison. Game-free, checked by NavCheck; <see cref="Looked"/> and
    /// <see cref="PutBack"/> feed the session record.
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
        /// As <see cref="Keep"/>, with the handler FIRST in the event's list: it runs before every other handler, the game's
        /// own among them (the map's tree guard must run before the game's TreeController.ToggleMapLod). Like Keep it looks only
        /// when the field changed since this holder last saw it; a handler added after it (they are all added at the end)
        /// leaves it first, so a change costs one array copy and mostly no more.
        /// </summary>
        public static void First<T>(ref T field, T handler, ref T seen) where T : Delegate
        {
            if (handler == null || (seen != null && ReferenceEquals(field, seen)))
            {
                return;
            }
            Looked++;
            if (field == null)
            {
                field = handler;
                PutBack++;
            }
            else if (!ReferenceEquals(field, handler))
            {
                Delegate[] list = field.GetInvocationList();
                if (list.Length == 0 || !list[0].Equals(handler))
                {
                    field = (T)Delegate.Combine(handler, Delegate.Remove(field, handler));
                    PutBack++;
                }
            }
            seen = field;
        }

        /// <summary>True if a handler is in an event's list. COST: one array copy of a list of several handlers.</summary>
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
