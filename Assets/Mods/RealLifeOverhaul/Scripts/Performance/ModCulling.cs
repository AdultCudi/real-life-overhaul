using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's own things in the street drawn by distance, as the game draws its own (Culling.CullingManager): a culling
    /// group of the mod's with the game's two bands (<see cref="CullRules"/>), measured from the player, with the player's
    /// camera. A thing registered here has its renderers hidden beyond the far band (Renderer.forceRenderingOff, only on
    /// renderers that were drawn, given back exactly) and drawn again inside it; nothing else of it changes. While the city
    /// map is open everything is drawn. CullRules.Inventory says which kinds are registered, and why every other kind is not.
    ///
    /// A group of its own rather than the game's: the game's rebuilds every sphere of its list whenever one leaves it, and
    /// bus stops and station fronts come and go. Switched on and off by its performance option (Tweaks/ModCull); things are
    /// registered whether or not it is on, so switching it on later finds them all.
    ///
    /// The cost: per frame one comparison (<see cref="Frame"/>, from PerfDirector) while no thing has changed band; the engine
    /// measures the spheres as it culls. A change of band is applied in a later frame, at most <see cref="PerFrame"/> things
    /// a frame, each a walk over its own renderers. Registering is a few array writes; nothing is allocated but a thing's
    /// own list of renderers, once.
    /// </summary>
    public static class ModCulling
    {
        private const int PerFrame = 8;             // things looked at in one frame at the most
        private const int StartSize = 64;           // slots before the book grows

        private static readonly FeatureGuard EventGuard = Guard.For("performance", "ModCulling", typeof(ModCulling));   // the engine's callback (Core/Guard)
        private static readonly float[] Bands = { CullRules.NearBand, CullRules.FarBand };
        private static readonly CullBook Book = new CullBook(StartSize);
        private static readonly List<Renderer> Found = new List<Renderer>(64);
        private static BoundingSphere[] _spheres = new BoundingSphere[StartSize];
        private static CullEntry[] _byId = new CullEntry[StartSize];
        private static CullingGroup _group;
        private static CullingGroup.StateChanged _changed;   // made once (the engine keeps it)
        private static Action<bool> _mapToggled;
        private static Action _mapClosed;
        private static Camera _camera;
        private static Transform _player;
        private static bool _on, _mapOpen, _hooked;
        private static int _hiddenThings, _hiddenRenderers;

        /// <summary>True while the culling is switched on and its group is measuring.</summary>
        public static bool On => _on && _group != null;

        /// <summary>
        /// Registers a thing of a kind CullRules lets be culled: its renderers are those under <paramref name="root"/>, its
        /// sphere the given centre and radius (metres, covering all of it). Null for any other kind, or no root.
        /// </summary>
        public static CullEntry Add(CullRules.Kind kind, Transform root, Vector3 centre, float radius)
        {
            if (!CullRules.MayCull(kind) || root == null)
            {
                return null;
            }
            int frame = Time.frameCount;
            int id = Book.Add(frame);
            Fit();
            var entry = new CullEntry(kind, root, id);
            _byId[id] = entry;
            _spheres[Book.SlotOf(id)] = new BoundingSphere(centre, Mathf.Max(0.5f, radius));
            _group?.SetBoundingSphereCount(Book.Count);
            return entry;
        }

        /// <summary>
        /// A registered thing moved (a bus stop's dressing put up at another place): drawn at once, and measured again where
        /// it is in a later frame.
        /// </summary>
        public static void Move(CullEntry entry, Vector3 centre, float radius)
        {
            int slot = entry != null ? Book.SlotOf(entry.Id) : -1;
            if (slot < 0 || _byId[entry.Id] != entry)
            {
                return;
            }
            Show(entry);
            _spheres[slot] = new BoundingSphere(centre, Mathf.Max(0.5f, radius));
            Book.Changed(slot, Time.frameCount);
        }

        /// <summary>
        /// The owner switched some of a thing's pieces on while it was hidden (a patrol car parked by a station front): those
        /// are hidden too. Nothing to do while it is drawn.
        /// </summary>
        public static void Refresh(CullEntry entry)
        {
            if (entry != null && entry.Hidden && _byId[entry.Id] == entry)
            {
                Hide(entry);
            }
        }

        /// <summary>Takes a thing out, drawn again as it was. Safe with null and with a thing already taken out.</summary>
        public static void Remove(CullEntry entry)
        {
            if (entry == null || entry.Id < 0 || entry.Id >= _byId.Length || _byId[entry.Id] != entry)
            {
                return;
            }
            Show(entry);
            int slot = Book.SlotOf(entry.Id);
            int last = Book.Count - 1;
            if (slot >= 0 && slot != last)
            {
                _spheres[slot] = _spheres[last];   // the last slot's thing moves into this slot (CullBook.Remove)
            }
            Book.Remove(entry.Id, Time.frameCount);
            _byId[entry.Id] = null;
            entry.Id = -1;
            _group?.SetBoundingSphereCount(Book.Count);
        }

        /// <summary>Switched on or off by its performance option. Off, everything is drawn again and the group is let go.</summary>
        public static void Enable(bool on)
        {
            _on = on;
            if (on)
            {
                Keep();
                return;
            }
            Drop();
            for (int id = 0; id < _byId.Length; id++)
            {
                if (_byId[id] != null)
                {
                    Show(_byId[id]);
                }
            }
        }

        /// <summary>On the option's timer: the group follows the game to a new camera or player, and is made if it is not there.</summary>
        public static void Keep()
        {
            if (!_on)
            {
                return;
            }
            Camera camera = GameManager.GetMainCamera();
            GameManager game = InstanceBehavior<GameManager>.Instance;
            Transform player = game != null && game.playerController != null ? game.playerController.transform : null;
            if (camera == null || player == null)
            {
                return;   // looked for again on the next tick
            }
            if (_group != null && camera == _camera && player == _player)
            {
                return;
            }
            Drop();
            _camera = camera;
            _player = player;
            _group = new CullingGroup();
            _group.onStateChanged = _changed ??= Changed;
            _group.targetCamera = camera;
            _group.SetBoundingDistances(Bands);
            _group.SetDistanceReferencePoint(player);
            _group.SetBoundingSpheres(_spheres);
            _group.SetBoundingSphereCount(Book.Count);
            _mapOpen = CityMap.IsOpen;
            Hook(true);
            Book.QueueAll(Time.frameCount);
        }

        /// <summary>Once a frame, from PerfDirector: the things whose band changed are hidden or drawn. One comparison while none has.</summary>
        public static void Frame()
        {
            if (Book.Waiting == 0)
            {
                return;
            }
            int frame = Time.frameCount;
            int done = 0;
            while (done < PerFrame && Book.Next(frame, out int id))
            {
                done++;
                CullEntry entry = _byId[id];
                if (entry == null)
                {
                    continue;
                }
                if (entry.Root == null)
                {
                    Remove(entry);   // its owner is gone without taking it out (the city going)
                    continue;
                }
                bool drawn = !On || CullRules.Drawn(entry.Kind, _group.GetDistance(Book.SlotOf(id)), _mapOpen);
                if (drawn)
                {
                    Show(entry);
                }
                else if (!entry.Hidden)
                {
                    Hide(entry);
                }
            }
        }

        /// <summary>The city is going: every thing forgotten (their owners go with it), the group let go.</summary>
        public static void Reset()
        {
            Enable(false);
            for (int id = 0; id < _byId.Length; id++)
            {
                if (_byId[id] != null)
                {
                    _byId[id].Id = -1;
                    _byId[id] = null;
                }
            }
            Book.Clear();
            _hiddenThings = _hiddenRenderers = 0;
        }

        /// <summary>One line for the reports: what is held back now.</summary>
        public static string Describe()
        {
            return Book.Count + " things of the mod's measured, " + _hiddenThings + " beyond " + CullRules.FarBand + " m not drawn (" + _hiddenRenderers + " renderers)"
                   + (_mapOpen ? "; the city map is open: all drawn" : string.Empty) + (On ? string.Empty : "; not measuring (no camera or player yet, or off)");
        }

        /// <summary>
        /// For the probe's report (Performance/PerfCensus): the registered things by kind, and every renderer of the mod's in the
        /// street by distance from the player and whether it was drawn. Walks the mod's objects; report time only.
        /// </summary>
        public static void Census(StringBuilder text)
        {
            var things = new int[(int)CullRules.Kind.Count];
            var hidden = new int[(int)CullRules.Kind.Count];
            var renderers = new int[(int)CullRules.Kind.Count];
            for (int id = 0; id < _byId.Length; id++)
            {
                CullEntry entry = _byId[id];
                if (entry == null)
                {
                    continue;
                }
                things[(int)entry.Kind]++;
                hidden[(int)entry.Kind] += entry.Hidden ? 1 : 0;
                renderers[(int)entry.Kind] += entry.Off.Count;
            }
            text.AppendLine("  " + Describe() + "; bands " + CullRules.NearBand + " m and " + CullRules.FarBand + " m from the player, as the game's own");
            for (int k = 0; k < (int)CullRules.Kind.Count; k++)
            {
                text.AppendLine("    " + CullRules.Name((CullRules.Kind)k).PadRight(24) + things[k] + " registered, " + hidden[k] + " not drawn now (" + renderers[k] + " renderers)");
            }
            GameManager game = InstanceBehavior<GameManager>.Instance;
            Vector3 player = game != null && game.playerController != null ? game.playerController.transform.position : Vector3.zero;
            var shown = new int[3];
            var drawn = new int[3];
            var held = new int[3];
            var roots = new List<GameObject>(64);
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }
                scene.GetRootGameObjects(roots);
                foreach (GameObject root in roots)
                {
                    if (root == null || !root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
                    {
                        float metres = Vector3.Distance(renderer.bounds.center, player);
                        int band = metres < CullRules.NearBand ? CullRules.Near : metres < CullRules.FarBand ? CullRules.Middle : CullRules.Far;
                        if (!renderer.enabled)
                        {
                            continue;
                        }
                        if (renderer.forceRenderingOff)
                        {
                            held[band]++;
                            continue;
                        }
                        shown[band]++;
                        drawn[band] += renderer.isVisible ? 1 : 0;
                    }
                }
            }
            text.AppendLine("  the mod's renderers in the street (by their distance from the player): within " + CullRules.NearBand + " m " + shown[0] + " (" + drawn[0]
                            + " drawn this frame), to " + CullRules.FarBand + " m " + shown[1] + " (" + drawn[1] + " drawn), beyond " + shown[2] + " (" + drawn[2]
                            + " drawn); held back: " + (held[0] + held[1] + held[2]) + " (" + held[2] + " of them beyond " + CullRules.FarBand + " m)");
            text.AppendLine("  how each kind of the mod's things is kept cheap far away (CullRules.Inventory):");
            foreach (CullRules.Thing thing in CullRules.Inventory)
            {
                text.AppendLine("    " + thing.Way.ToString().PadRight(12) + thing.What + ": " + thing.Why);
            }
        }

        // The engine's call, made while it culls the camera: only noted, applied in a later frame (Frame).
        private static void Changed(CullingGroupEvent sphere)
        {
            try
            {
                if (sphere.currentDistance != sphere.previousDistance)
                {
                    Book.Changed(sphere.index, Time.frameCount);
                }
            }
            catch (Exception e)
            {
                Guard.Fail(EventGuard, e);
            }
        }

        private static void MapToggled(bool open)
        {
            if (open != _mapOpen)
            {
                _mapOpen = open;
                Book.QueueAll(Time.frameCount);
            }
        }

        private static void MapClosed() => MapToggled(false);

        private static void Hook(bool on)
        {
            if (on == _hooked)
            {
                return;
            }
            _hooked = on;
            _mapToggled ??= Guard.Wrap<bool>(MapToggled);
            _mapClosed ??= Guard.Wrap(MapClosed);
            if (on)
            {
                GlobalEvents.onCityMapToggle += _mapToggled;
                GlobalEvents.onCityMapClosed += _mapClosed;
            }
            else
            {
                GlobalEvents.onCityMapToggle -= _mapToggled;
                GlobalEvents.onCityMapClosed -= _mapClosed;
            }
        }

        private static void Drop()
        {
            Hook(false);
            if (_group != null)
            {
                _group.onStateChanged = null;
                _group.Dispose();
                _group = null;
            }
            _camera = null;
            _player = null;
        }

        // The sphere array and the list of things grow with the book; the group is given the new array.
        private static void Fit()
        {
            if (_spheres.Length >= Book.Capacity)
            {
                return;
            }
            Array.Resize(ref _spheres, Book.Capacity);
            Array.Resize(ref _byId, Book.Capacity);
            _group?.SetBoundingSpheres(_spheres);
        }

        // Every renderer of the thing that is drawn now is held back, and remembered; one already held back by its owner is left.
        private static void Hide(CullEntry entry)
        {
            if (!entry.Hidden)
            {
                _hiddenThings++;
                entry.Hidden = true;
            }
            entry.Root.GetComponentsInChildren(false, Found);
            for (int i = 0; i < Found.Count; i++)
            {
                Renderer renderer = Found[i];
                if (renderer != null && !renderer.forceRenderingOff)
                {
                    renderer.forceRenderingOff = true;
                    entry.Off.Add(renderer);
                    _hiddenRenderers++;
                }
            }
            Found.Clear();
        }

        private static void Show(CullEntry entry)
        {
            if (!entry.Hidden)
            {
                return;
            }
            for (int i = 0; i < entry.Off.Count; i++)
            {
                if (entry.Off[i] != null)
                {
                    entry.Off[i].forceRenderingOff = false;
                }
            }
            _hiddenRenderers -= entry.Off.Count;
            _hiddenThings--;
            entry.Off.Clear();
            entry.Hidden = false;
        }
    }

    /// <summary>A thing registered with <see cref="ModCulling"/>: its owner keeps it, and gives it back when the thing goes.</summary>
    public sealed class CullEntry
    {
        internal readonly CullRules.Kind Kind;
        internal readonly Transform Root;
        internal readonly List<Renderer> Off = new List<Renderer>(8);   // the renderers held back, to be given back exactly
        internal int Id;
        internal bool Hidden;

        internal CullEntry(CullRules.Kind kind, Transform root, int id)
        {
            Kind = kind;
            Root = root;
            Id = id;
        }
    }
}
