using System;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Where the mod's panels over a place are offered, with their reach, so a person put out to wait for the player stands
    /// clear of them (<see cref="PanelPickRules.Pick"/>). Each feature names its own (<see cref="Set"/>). Costs nothing a frame.
    /// </summary>
    public static class PanelPlaces
    {
        /// <summary>Adds the places within <paramref name="within"/> metres of <paramref name="around"/> to the arrays from <paramref name="count"/>; returns the new count.</summary>
        public delegate int Finder(Vector3 around, float within, float[] x, float[] z, float[] reach, int count);

        private const int Most = 8;   // kinds of place, one finder each

        private static readonly string[] Kinds = new string[Most];
        private static readonly Finder[] Finders = new Finder[Most];
        private static int _count;

        /// <summary>The finder for one kind of place; one given before for the kind (a city loaded again) is replaced.</summary>
        public static void Set(string kind, Finder finder)
        {
            for (int i = 0; i < _count; i++)
            {
                if (string.Equals(Kinds[i], kind, StringComparison.Ordinal))
                {
                    Finders[i] = finder;
                    return;
                }
            }
            if (_count < Most)
            {
                Kinds[_count] = kind;
                Finders[_count] = finder;
                _count++;
            }
        }

        /// <summary>The places of every kind within <paramref name="within"/> metres into the arrays; their count. A failing finder is left out.</summary>
        public static int Near(Vector3 around, float within, float[] x, float[] z, float[] reach)
        {
            int count = 0;
            for (int i = 0; i < _count; i++)
            {
                if (Finders[i] == null)
                {
                    continue;
                }
                try
                {
                    count = Finders[i](around, within, x, z, reach, count);
                }
                catch (Exception e)
                {
                    Finders[i] = null;   // its feature went with the city, or failed: left out from now on
                    WorldPanel.Logger?.Error(e);
                }
            }
            return count;
        }

        /// <summary>For a finder: adds one place if within reach of the point and the arrays have room.</summary>
        public static int Add(Vector3 around, float within, Vector3 place, float placeReach, float[] x, float[] z, float[] reach, int count)
        {
            float dx = place.x - around.x;
            float dz = place.z - around.z;
            if (count >= x.Length || count >= z.Length || count >= reach.Length || dx * dx + dz * dz > within * within)
            {
                return count;
            }
            x[count] = place.x;
            z[count] = place.z;
            reach[count] = placeReach;
            return count + 1;
        }
    }
}
