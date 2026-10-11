using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Player.HUD.ItemInfoOverlays;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Makes a bus stop's pole, its shelter and the bus standing at it things the player can point at, the way the
    /// game's own objects are (see docs/game-ui.md, sections 7 and 11, and Home/HomeCookOffer, which this follows).
    ///
    /// The game's pointer (MouseController) casts a ray on its interactive layers and asks whatever
    /// <c>EntityController</c> it hits to react: the object is outlined, its name is shown with a "call to action" in
    /// the game's blue under it (SimpleOverlay with CtaOverlay), and a left click runs the action. So a pointer is a
    /// small object of the mod's with a box collider on the game's layer for interactive items and this component on
    /// it. The collider is a trigger: it is hit by the pointer's ray (the game's physics lets queries hit triggers)
    /// and by nothing else, since nobody on foot or in a vehicle is moved by physics against it, and that layer does
    /// not meet the layer of traffic at all. Nothing is added to the bus itself (a vehicle of the traffic pool): its
    /// pointer is a box of the director's, put where the bus stands and switched off when it leaves.
    ///
    /// What is offered, and what a click does, is the director's to say (<see cref="TransitDirector.PointerOffer"/>,
    /// <see cref="TransitDirector.PointerClicked"/>). The game asks only for what the pointer is on, and only when it
    /// comes onto it or clicks.
    /// </summary>
    public sealed class TransitPointer : EntityController
    {
        public enum Kind { Stop, Bench, Bus }

        public const string HeaderStop = "reallife_transit_cta_header_stop";
        public const string HeaderBus = "reallife_transit_cta_header_bus";

        private static readonly List<Renderer> Found = new List<Renderer>(16);

        private int[] _layers;   // the layers the outlined pieces are on when nothing points at them

        public Kind What { get; private set; }

        /// <summary>The dressed stop this is part of (null for the bus's).</summary>
        public BusStopDressing Dressing { get; private set; }

        /// <summary>
        /// A pointer under <paramref name="parent"/>: a trigger box of the given middle and size (in the parent's
        /// own axes), which outlines the parent's pieces when it is pointed at. Null if the game no longer has its layer
        /// for interactive items.
        /// </summary>
        public static TransitPointer Make(Transform parent, Kind what, BusStopDressing dressing, Vector3 centre, Vector3 size, bool outline)
        {
            int layer = LayerMask.NameToLayer("InteractiveItems");
            if (layer < 0 || parent == null)
            {
                return null;
            }
            var made = new GameObject("Pointer");
            made.SetActive(false);   // nothing of it wakes until it is whole
            made.layer = layer;
            made.transform.SetParent(parent, false);
            made.transform.localPosition = centre;   // the game puts the name over the object's own place
            var box = made.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var pointer = made.AddComponent<TransitPointer>();
            pointer.What = what;
            pointer.Dressing = dressing;
            pointer.simpleOverlayType = SimpleOverlayType.ClickToAction;
            pointer.customOverlayHeaderKey = what == Kind.Bus ? HeaderBus : HeaderStop;
            pointer.navMeshTargets = new[] { made.transform };
            Found.Clear();
            if (outline)
            {
                parent.GetComponentsInChildren(true, Found);
                Found.RemoveAll(renderer => renderer == null || renderer.GetComponent<TMP_Text>() != null);   // lettering is not outlined
            }
            pointer.renderers = Found.ToArray();
            pointer._layers = new int[Found.Count];
            for (int i = 0; i < Found.Count; i++)
            {
                pointer._layers[i] = Found[i].gameObject.layer;
            }
            Found.Clear();
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            if (pointer.renderers.Length > 0 && references != null)
            {
                pointer.SetOutlineColor(references.colors.white);   // as the game gives a place it makes clickable in code (TaxiDestinationController)
            }
            made.SetActive(true);
            return pointer;
        }

        // The game's own Awake puts the object on the interactive layer (it is there already) and its Start reads places
        // to walk to that an object placed by the game has; neither is wanted here.
        public override void Awake()
        {
        }

        public override void Start()
        {
        }

        public override bool Interact()
        {
            return true;
        }

        public override bool ShouldReactToIoEnter()
        {
            return primaryInteractionEnabled && TransitDirector.Offer(this) != null;
        }

        // As the game's, and then the outlined pieces go back to the layers they came from (the game's own puts them on
        // the layer for interactive items, which these pieces were never on).
        public override void OnIoExit()
        {
            onIoExitEvent.Invoke();
            onIoExitEvent.RemoveAllListeners();
            OverlayManager overlays = InstanceBehavior<OverlayManager>.Instance;
            if (overlays != null && overlays.IsShowingOverlayOverItem(this))
            {
                overlays.HideSimpleOverlayAndClearCta();
            }
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].gameObject.layer = _layers[i];
                }
            }
        }

        public override void OnDestroy()
        {
            if (!GameManager.isCitySceneBeingUnloaded && MouseController.currentTargetEntity == this)
            {
                MouseController.ResetCurrentEntitySelected(this);
            }
        }
    }

    /// <summary>
    /// The one entry the buses add to the game's list of calls to action (CtaManager.CtaBehaviors, private: reached
    /// by reflection as Home/HomeCookOffer does). Asked by the game for whatever the pointer is on: a type test,
    /// unless it is one of the mod's pointers.
    /// </summary>
    public static class TransitCta
    {
        public const string Choose = "reallife_transit_cta_choose";
        public const string Board = "reallife_transit_cta_board";
        public const string Sit = "reallife_transit_cta_sit";

        public static IModLogger Logger;

        private sealed class Behaviour : ICtaBehavior
        {
            public override bool ShouldShow(EntityController entityController)
            {
                return entityController is TransitPointer;
            }

            public override (string, Action) GetCta(EntityController entityController)
            {
                var pointer = entityController as TransitPointer;
                string key = pointer != null ? TransitDirector.Offer(pointer) : null;
                if (key == null)
                {
                    return ("", null);
                }
                _pointed = pointer;
                return (key, Click);
            }
        }

        private static readonly Action Click = Clicked;
        private static Behaviour _behaviour;
        private static List<ICtaBehavior> _list;
        private static TransitPointer _pointed;   // what the game last asked about: the one a click is on
        private static bool _failed;

        public static bool Installed => _behaviour != null;

        /// <summary>Adds the entry, once. False if the game's list could not be reached: the stops then have their panel and its keys only.</summary>
        public static bool Install()
        {
            if (Installed)
            {
                return true;
            }
            if (_failed)
            {
                return false;
            }
            try
            {
                FieldInfo field = typeof(CtaManager).GetField("CtaBehaviors", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                _list = field != null ? field.GetValue(null) as List<ICtaBehavior> : null;
                if (_list == null)
                {
                    _failed = true;
                    Logger?.Warn("Transit: the game's list of click actions was not found, so a bus stop cannot be clicked. Its panel and the keys on it work as they do.");
                    return false;
                }
                _behaviour = new Behaviour();
                _list.Insert(0, _behaviour);   // asked first: it answers only for a pointer of the buses
                Logger?.Info("Transit check (pointer): the bus stops' click actions are in the game's list (" + _list.Count + " entries): PASS.");
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                _behaviour = null;
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>Takes the entry out of the game's list again.</summary>
        public static void Remove()
        {
            try
            {
                if (_list != null && _behaviour != null)
                {
                    _list.Remove(_behaviour);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _behaviour = null;
            _list = null;
            _pointed = null;
            _failed = false;
        }

        private static void Clicked()
        {
            TransitPointer pointer = _pointed;
            if (pointer == null)
            {
                return;
            }
            try
            {
                TransitDirector.Instance?.PointerClicked(pointer);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
