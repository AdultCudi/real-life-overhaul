using System;
using System.Collections;
using BAModAPI;
using BigAmbitions.Mods;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>The guard's part for the handlers, coroutines and listeners it wraps, and their HotSpots slots.</summary>
    public static partial class Guard
    {
        /// <summary>The Meta/FeatureList feature a class belongs to (Core/FeatureOwners); null for the mod's own machinery.</summary>
        public static string FeatureOf(Type owner)
        {
            return owner == null ? null : FeatureOwners.Of(owner.Name, owner.Namespace);
        }

        /// <summary>The guard of the class a handler is written in, made on first asking.</summary>
        public static FeatureGuard OfHandler(Delegate handler)
        {
            if (handler == null)
            {
                return null;
            }
            System.Reflection.MethodInfo method = handler.Method;
            lock (ByMethod)
            {
                if (ByMethod.TryGetValue(method, out FeatureGuard known))
                {
                    return known;
                }
            }
            FeatureGuard guard = OfType(method.DeclaringType);
            lock (ByMethod)
            {
                ByMethod[method] = guard;
            }
            return guard;
        }

        private static FeatureGuard OfType(Type owner)
        {
            while (owner != null && owner.DeclaringType != null && owner.Name.IndexOf('<') >= 0)
            {
                owner = owner.DeclaringType;   // a lambda's class is the compiler's, inside the one it is written in
            }
            return OfOwner(owner) ?? For(FeatureOf(owner), owner != null ? owner.Name : "a handler", owner);
        }

        /// <summary>
        /// Calls each handler of one of the mod's own events in a try of its own, against its class's guard, skipping
        /// those switched off. For the economy's hour and day, which a dozen features share.
        /// </summary>
        public static void Each(Action handlers)
        {
            if (handlers == null)
            {
                return;
            }
            Delegate[] list = handlers.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                var handler = (Action)list[i];
                FeatureGuard guard = OfHandler(handler);
                if (guard != null && guard.Off)
                {
                    continue;
                }
                using var hotSpot = HotSpots.Time(HandlerSlot(handler, "Event"));
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            }
        }

        /// <summary>
        /// Runs a coroutine inside the guard of the class it is written in: <c>StartCoroutine(Guard.Routine(Tick()))</c>.
        /// A fault ends it and counts as an entry point's. One under way is never cut short between steps when something
        /// is switched off: it may be half way through a fade that only its own end puts right. A coroutine it yields
        /// runs in the same guard; each step is timed under its own slot (<see cref="RoutineSlot"/>).
        /// COST: one small object per coroutine started; per step a call and a flag test.
        /// </summary>
        public static IEnumerator Routine(IEnumerator routine)
        {
            return routine == null ? null : new GuardedRoutine(OfType(routine.GetType()), routine);
        }

        private sealed class GuardedRoutine : IEnumerator
        {
            private readonly FeatureGuard _guard;
            private readonly IEnumerator _inner;
            private readonly int _slot;
            private object _current;

            public GuardedRoutine(FeatureGuard guard, IEnumerator inner)
            {
                _guard = guard;
                _inner = inner;
                _slot = RoutineSlot(inner.GetType());
            }

            public object Current => _current;

            public bool MoveNext()
            {
                using var hotSpot = HotSpots.Time(_slot);
                try
                {
                    if (!_inner.MoveNext())
                    {
                        return false;
                    }
                    object yielded = _inner.Current;
                    _current = yielded is IEnumerator waited && !(yielded is GuardedRoutine) ? new GuardedRoutine(_guard, waited) : yielded;
                    return true;
                }
                catch (Exception e)
                {
                    Fail(_guard, e);
                    return false;
                }
            }

            public void Reset()
            {
                _inner.Reset();
            }
        }

        /// <summary>
        /// The HotSpots slot of a coroutine, by the class the compiler made of it ("Coroutine: TransitDirector.Tick"),
        /// made once per coroutine method.
        /// </summary>
        public static int RoutineSlot(Type routine)
        {
            lock (RoutineSlots)
            {
                if (RoutineSlots.TryGetValue(routine, out int known))
                {
                    return known;
                }
                int slot = HotSpots.Slot("Coroutine: " + HotSpots.Written(routine, null));
                RoutineSlots[routine] = slot;
                return slot;
            }
        }

        /// <summary>
        /// The HotSpots slot of a wrapped handler ("Event: CareersDirector.OnHour", "Listener: StocksApp.Awake" for a
        /// lambda in Awake), made once per handler method.
        /// </summary>
        public static int HandlerSlot(Delegate handler, string kind)
        {
            if (handler == null)
            {
                return -1;
            }
            System.Reflection.MethodInfo method = handler.Method;
            lock (HandlerSlots)
            {
                if (HandlerSlots.TryGetValue(method, out int known))
                {
                    return known;
                }
                int slot = HotSpots.Slot(kind + ": " + HotSpots.Written(method.DeclaringType, method.Name));
                HandlerSlots[method] = slot;
                return slot;
            }
        }

        /// <summary>As <see cref="Wrap(FeatureGuard, Action)"/>, with the guard of the handler's class.</summary>
        public static Action Wrap(Action handler)
        {
            return handler == null ? null : Wrap(OfHandler(handler), handler);
        }

        public static Action<T> Wrap<T>(Action<T> handler)
        {
            return handler == null ? null : Wrap(OfHandler(handler), handler);
        }

        public static Action<T1, T2> Wrap<T1, T2>(Action<T1, T2> handler)
        {
            return handler == null ? null : Wrap(OfHandler(handler), handler);
        }

        /// <summary>As <see cref="Wrap(Action)"/>, for the game's notification event (Notifications.onShow, eight arguments).</summary>
        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> Wrap<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> handler)
        {
            FeatureGuard guard = OfHandler(handler);
            int slot = HandlerSlot(handler, "Event");
            return (a, b, c, d, e, f, g, h) =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(a, b, c, d, e, f, g, h);
                }
                catch (Exception fault)
                {
                    Fail(guard, fault);
                }
            };
        }

        /// <summary>A handler for one of the game's events that never throws into the game. Make it once and keep it: event holders compare it.</summary>
        public static Action Wrap(FeatureGuard guard, Action handler)
        {
            int slot = HandlerSlot(handler, "Event");
            return () =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        public static Action<T> Wrap<T>(FeatureGuard guard, Action<T> handler)
        {
            int slot = HandlerSlot(handler, "Event");
            return value =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(value);
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        public static Action<T1, T2> Wrap<T1, T2>(FeatureGuard guard, Action<T1, T2> handler)
        {
            int slot = HandlerSlot(handler, "Event");
            return (a, b) =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(a, b);
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        /// <summary>
        /// A UnityEvent listener that never throws into the event's other listeners, with the guard of the class it is
        /// written in. Make it once and keep it for taking off: the event compares it.
        /// </summary>
        public static UnityAction Listener(UnityAction handler)
        {
            if (handler == null)
            {
                return null;
            }
            FeatureGuard guard = OfHandler(handler);
            int slot = HandlerSlot(handler, "Listener");
            return () =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        public static UnityAction<T> Listener<T>(UnityAction<T> handler)
        {
            if (handler == null)
            {
                return null;
            }
            FeatureGuard guard = OfHandler(handler);
            int slot = HandlerSlot(handler, "Listener");
            return value =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(value);
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        /// <summary>One step of the mod's start: a step that throws is logged and the others still run.</summary>
        public static void Step(string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                if (StandsDown(e, "the start step '" + what + "'"))
                {
                    return;
                }
                Logger?.Warn("Guard: the start step '" + what + "' failed; the rest of the mod starts without it.");
                Logger?.Error(e);
            }
        }

        /// <summary>Rows of one feature added to the options page: a feature whose rows cannot be made costs the page only its rows.</summary>
        public static ModOptions Guarded(this ModOptions options, Func<ModOptions, ModOptions> add)
        {
            try
            {
                return add(options) ?? options;
            }
            catch (Exception e)
            {
                Logger?.Warn("Guard: a feature's rows could not be added to the options page; the page is shown without them.");
                Logger?.Error(e);
                return options;
            }
        }

        /// <summary>The options page put in order (Meta/OptionsLayout); as it was added if that fails.</summary>
        public static ModOptions Organised(ModContext context, ModOptions options)
        {
            try
            {
                return Meta.OptionsLayout.Organise(context, options);
            }
            catch (Exception e)
            {
                Logger?.Warn("Guard: the options page could not be put in order; it is shown as the features added it.");
                Logger?.Error(e);
                return options;
            }
        }
    }
}
