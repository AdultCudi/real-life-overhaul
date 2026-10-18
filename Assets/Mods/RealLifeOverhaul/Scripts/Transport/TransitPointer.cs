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
    /// Makes a bus stop's pole, its shelter and the bus at it things the game's pointer reacts to, as its own objects
    /// (docs/game-ui.md; Home/HomeOffers works the same way): a trigger box on the interactive layer with this component. What
    /// is offered and what a click does is the director's. Costs nothing a frame: the game asks only on pointing and clicking.
    /// </summary>
    public sealed class TransitPointer : EntityController
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("transport", "TransitPointer", typeof(TransitPointer));   // the game's calls of it

        public enum Kind { Stop, Bench, Bus }

        public const string HeaderStop = "reallife_transit_cta_header_stop";
        public const string HeaderBus = "reallife_transit_cta_header_bus";

        private static readonly List<Renderer> Found = new List<Renderer>(16);

        private int[] _layers;   // the outlined pieces' own layers, put back when nothing points at them

        public Kind What { get; private set; }

        public BusStopDressing Dressing { get; private set; }   // the dressed stop this is part of; null for the bus's

        /// <summary>A pointer box under <paramref name="parent"/>, outlining its pieces when pointed at; null if the game has no interactive layer.</summary>
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
                pointer.SetOutlineColor(references.colors.white);   // as the game outlines a place it makes clickable in code (TaxiDestinationController)
            }
            made.SetActive(true);
            return pointer;
        }

        // The game's Awake and Start set up objects the game places; neither is wanted here.
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
            if (FrameGuard.Off)
            {
                return false;
            }
            try
            {
                return primaryInteractionEnabled && TransitDirector.Offer(this) != null;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, false);
            }
        }

        // As the game's, then the outlined pieces go back to their own layers.
        public override void OnIoExit()
        {
            try
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
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public override void OnDestroy()
        {
            OverlayGuard.Release(this);   // the game's own lets the card go only while it is on screen
            if (!GameManager.isCitySceneBeingUnloaded && MouseController.currentTargetEntity == this)
            {
                MouseController.ResetCurrentEntitySelected(this);
            }
        }
    }

    /// <summary>The one entry the buses add to the game's private list of calls to action, reached by reflection as Home/HomeOffers does.</summary>
    public static class TransitCta
    {
        public const string Choose = "reallife_transit_cta_choose";
        public const string Board = "reallife_transit_cta_board";
        public const string Sit = "reallife_transit_cta_sit";

        public static IModLogger Logger;

        private sealed class Behaviour : ICtaBehavior
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("transport", "TransitCta.Behaviour", typeof(Behaviour));   // the game's calls of it

            public override bool ShouldShow(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return false;
                }
                try
                {
                    return entityController is TransitPointer;
                }
                catch (Exception fault)
                {
                    return Guard.Failed(FrameGuard, fault, false);
                }
            }

            public override (string, Action) GetCta(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return (string.Empty, (Action)null);
                }
                try
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
                catch (Exception fault)
                {
                    return Guard.Failed(FrameGuard, fault, (string.Empty, (Action)null));
                }
            }
        }

        private static readonly Action Click = Clicked;
        private static Behaviour _behaviour;
        private static List<ICtaBehavior> _list;
        private static TransitPointer _pointed;   // what the game last asked about: the one a click is on
        private static bool _failed;

        public static bool Installed => _behaviour != null;

        /// <summary>Adds the entry, once. False if the game's list could not be reached.</summary>
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
                FieldInfo field = GameContract.CtaBehaviours.Field;
                _list = field != null ? field.GetValue(null) as List<ICtaBehavior> : null;
                if (_list == null)
                {
                    _failed = true;
                    Logger?.Warn("Transit: the game's list of click actions was not found, so a bus stop cannot be clicked. Its panel and the keys on it work as they do.");
                    return false;
                }
                _behaviour = new Behaviour();
                _list.Insert(0, _behaviour);   // asked first; it answers only for a pointer of the buses
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
