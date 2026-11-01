using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

// The runner lives in the editor's assembly and draws with the kit's own helpers, which the mod keeps internal.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

// Stand-ins for the three pieces of the mod's core that the kit's Unity layer names (Core/Guard, Core/FeatureGuard,
// Core/HotSpots), so the kit's own files compile unchanged in the preview project, which has no game and no mod around
// them. They do what an offline render needs: nothing is switched off, faults are logged, nothing is timed.
namespace RealLifeOverhaul
{
    public sealed class FeatureGuard
    {
        public bool Off => false;
    }

    public static class Guard
    {
        public static FeatureGuard For(string feature, string where, Type owner = null) => new FeatureGuard();

        public static void Fail(FeatureGuard guard, Exception e, Behaviour component = null) => Debug.LogException(e);

        public static Action<T> Wrap<T>(Action<T> handler) => handler;

        public static Action<T1, T2> Wrap<T1, T2>(Action<T1, T2> handler) => handler;

        public static UnityAction<T> Listener<T>(UnityAction<T> handler) => handler;

        public static void NoMesh(UnityEngine.UI.VertexHelper mesh) => mesh.Clear();

        public static void FailMesh(FeatureGuard guard, Exception e, UnityEngine.UI.VertexHelper mesh)
        {
            Debug.LogException(e);
            mesh.Clear();
        }
    }

    public static class HotSpots
    {
        public struct Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public static int Slot(string name) => 0;

        public static Scope Time(int slot) => default(Scope);
    }
}
