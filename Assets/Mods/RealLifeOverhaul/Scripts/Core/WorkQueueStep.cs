using System;
using System.Reflection;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts a wrap round the step of a queue of work: the game's queue on the city map's buildings (CityMapFilters.Work, a DistributedWork) keeps the step it runs for each
    /// building in a private field, and the mod's wraps (ClaimedMap's, NavMapFollow's) take that step and put theirs round it, the game's own always running in it. The queue is
    /// any object with that field, so a check can run the wrap against a replica of the game's class, in either order of the wraps. Game-free. Cost: one reflection read and
    /// write a session.
    /// </summary>
    public static class WorkQueueStep
    {
        /// <summary>The queue's private field that holds its step.</summary>
        public const string Field = "_updateAction";

        /// <summary>
        /// Takes the queue's step and puts <paramref name="wrap"/>'s result in its place. False, with the queue untouched, when the contract says the queue has no such step
        /// (<paramref name="contractOff"/>), when there is no queue, or when the queue holds no step of this kind of item.
        /// </summary>
        public static bool Wrap<T>(object queue, bool contractOff, Func<Action<T>, Action<T>> wrap)
        {
            FieldInfo field = contractOff || queue == null ? null : FieldOf(queue.GetType());
            var step = field != null ? field.GetValue(queue) as Action<T> : null;
            if (step == null)
            {
                return false;
            }
            field.SetValue(queue, wrap(step));
            return true;
        }

        // A private field is found on the class that declares it, so the walk goes up through the classes the queue derives from.
        private static FieldInfo FieldOf(Type type)
        {
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(Field, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }
            return null;
        }
    }
}
