using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Player.HUD.ItemInfoOverlays;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The copy at home as a thing the player can point at, the way the game's own objects are (as Transport/TransitPointer
    /// and Home/HomeOffers do it): a trigger box on the game's layer for interactive items with this component on it. The
    /// game's pointer outlines the paper, shows "The City Ledger" with the call to action in the game's blue under it, and
    /// a left click opens the page. The box is a trigger, so it is hit by the pointer's ray and by nothing else. It has no
    /// Update: the game asks it only when the pointer comes onto it, leaves it or clicks.
    /// </summary>
    public sealed class LedgerPointer : EntityController
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "LedgerPointer", typeof(LedgerPointer));   // the game's calls of it (Core/Guard)

        public const string Header = "reallife_ledger_cta_header";

        private int[] _layers;

        /// <summary>A pointer under the paper: a trigger box of the given middle and size in the paper's own axes. Null if the game has no layer for it.</summary>
        public static LedgerPointer Make(Transform paper, Vector3 centre, Vector3 size)
        {
            int layer = LayerMask.NameToLayer("InteractiveItems");
            if (layer < 0 || paper == null || !LedgerCta.Install())
            {
                return null;
            }
            var made = new GameObject("Pointer");
            made.SetActive(false);   // nothing of it wakes until it is whole
            made.layer = layer;
            made.transform.SetParent(paper, false);
            made.transform.localPosition = centre;
            var box = made.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var pointer = made.AddComponent<LedgerPointer>();
            pointer.simpleOverlayType = SimpleOverlayType.ClickToAction;
            pointer.customOverlayHeaderKey = Header;
            pointer.navMeshTargets = new[] { made.transform };
            Renderer renderer = paper.GetComponent<Renderer>();
            pointer.renderers = renderer != null ? new[] { renderer } : new Renderer[0];
            pointer._layers = new int[pointer.renderers.Length];
            for (int i = 0; i < pointer.renderers.Length; i++)
            {
                pointer._layers[i] = pointer.renderers[i].gameObject.layer;
            }
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            if (pointer.renderers.Length > 0 && references != null)
            {
                pointer.SetOutlineColor(references.colors.white);
            }
            made.SetActive(true);
            return pointer;
        }

        // The game's own Awake puts the object on the interactive layer (it is there already) and its Start reads places to
        // walk to that an object placed by the game has; neither is wanted here.
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
                return primaryInteractionEnabled && LedgerDirector.Instance != null && LedgerDirector.Instance.Running;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, false);
            }
        }

        // As the game's, and then the outlined paper goes back to the layer it came from.
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
            OverlayGuard.Release(this);   // (the game's own lets the card go only while it is on screen)
            if (!GameManager.isCitySceneBeingUnloaded && MouseController.currentTargetEntity == this)
            {
                MouseController.ResetCurrentEntitySelected(this);
            }
        }
    }

    /// <summary>
    /// The one entry the paper adds to the game's list of calls to action (CtaManager.CtaBehaviors, private: reached by
    /// reflection as Home/HomeOffers does). Asked by the game for whatever the pointer is on: a type test, unless it is the
    /// paper's pointer. Taken out again when the paper stands down.
    /// </summary>
    public static class LedgerCta
    {
        public const string Read = "reallife_ledger_cta_read";

        private sealed class Behaviour : ICtaBehavior
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "LedgerCta.Behaviour", typeof(Behaviour));   // the game's calls of it (Core/Guard)

            public override bool ShouldShow(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return false;
                }
                try
                {
                    return entityController is LedgerPointer;
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
                    return entityController is LedgerPointer ? (Read, Click) : ("", null);
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
        private static bool _failed;

        /// <summary>Adds the entry, once. False if the game's list could not be reached (said once in Player.log): the copy is then not a click away.</summary>
        public static bool Install()
        {
            if (_behaviour != null)
            {
                return true;
            }
            if (_failed)
            {
                return false;
            }
            try
            {
                FieldInfo field = GameContract.CtaBehaviours.Field;   // (resolved once: Core/GameContract)
                _list = field != null ? field.GetValue(null) as List<ICtaBehavior> : null;
                if (_list == null)
                {
                    _failed = true;
                    LedgerDirector.Logger?.Warn("City Ledger: the game's list of click actions was not found, so the copy at home cannot be clicked; it can be read at the kiosk where it was bought.");
                    return false;
                }
                _behaviour = new Behaviour();
                _list.Insert(0, _behaviour);   // asked first: it answers only for the paper's pointer
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                _behaviour = null;
                LedgerDirector.Logger?.Error(e);
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
                LedgerDirector.Logger?.Error(e);
            }
            _behaviour = null;
            _list = null;
            _failed = false;
        }

        private static void Clicked()
        {
            try
            {
                LedgerDirector.Instance?.Read(LedgerPage.FromHome);
            }
            catch (Exception e)
            {
                LedgerDirector.Logger?.Error(e);
            }
        }
    }
}
