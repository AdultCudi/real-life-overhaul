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

        // What UiCost (Ui/Host) reads: nothing is measured in a preview.
        public static bool On;
        public static int SampleEvery = 1;
        public static Func<int> FrameSource;
        public static Func<long> HeapSource;
        public static long GameTicks => 0L;
        public static long InsideGameTicks => 0L;
    }
}

namespace RealLifeOverhaul
{
    // What UiHost reads from outside the kit: the developer's verbose switch and the typing layer (Core/TypingNow).
    public static class Meta
    {
        public static class Dev
        {
            public static bool Verbose => false;
        }
    }

    public static class TypingNow
    {
        public static void OnGameLayer(GameObject field)
        {
        }
    }
}

namespace RealLifeOverhaul.Ui
{
    public interface IPreviewLogger
    {
        void Info(string line);
    }

    // UiDirector stays out of the preview (it meets the game); UiHost only logs through it.
    public static class UiDirector
    {
        public static IPreviewLogger Logger;
    }
}

namespace RealLifeOverhaul.Ui
{
    // The game file that names another mod's faults stays out of the preview (it meets the game); UiHost only asks it.
    public static class UiForeignGame
    {
        public static bool Caught(Exception e, string screenId, string screenName) => false;
    }
}

// What the showroom's rules read from the bank's product table: the terms a car loan runs over, in game days. The preview builds the showrooms' state and rules, not the bank.
namespace RealLifeOverhaul.Economy
{
    public static class ProductCatalogue
    {
        public const int CarTermLeast = 60, CarTermMost = 240, CarTermStep = 30;
    }
}

// The kit reads the mouse's own place while a button is down (UiView.Pointer); the preview has no input package, and no pointer.
namespace UnityEngine.InputSystem
{
    public sealed class ButtonStub { public bool isPressed => false; }

    public sealed class PositionStub { public Vector2 ReadValue() => default(Vector2); }

    public sealed class Mouse
    {
        public static Mouse current => null;
        public ButtonStub leftButton => null;
        public PositionStub position => null;
    }
}
