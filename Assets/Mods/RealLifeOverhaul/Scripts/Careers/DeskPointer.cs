using System.Collections.Generic;
using Player.HUD.ItemInfoOverlays;
using RealLifeOverhaul.FitOut;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The player's desk on a fitted-out floor (FitOut/FitDesks) as a thing to point at, the way the game's own items are
    /// (as Ledger/LedgerPointer and Transport/TransitPointer do it): a trigger box over the desk on the game's layer for
    /// interactive items, with this component on it. The game's pointer outlines the desk, its computer and its chair,
    /// shows "Your desk" with the game's own "Click to work" under it (Careers/DeskGame's entry in the game's calls to
    /// action), and a left click walks the player to the chair and opens the work panel. The box is a trigger, so it is
    /// hit by the pointer's ray and by nothing else. It has no Update: the game asks it only when the pointer comes onto
    /// it, leaves it or clicks. It goes with the desk.
    /// </summary>
    public sealed class DeskPointer : EntityController
    {
        public const string Header = "reallife_desk_cta_header";

        private static readonly List<Renderer> Found = new List<Renderer>(16);

        private int[] _layers;

        /// <summary>A pointer over the desk. Null if the game has no layer for it.</summary>
        public static DeskPointer Make(FitDesk desk)
        {
            int layer = LayerMask.NameToLayer("InteractiveItems");
            if (layer < 0 || desk == null || desk.Desk == null)
            {
                return null;
            }
            Bounds bounds = GameFurniture.LocalBounds(desk.Desk);
            var made = new GameObject("RealLife Desk Pointer");
            made.SetActive(false);   // nothing of it wakes until it is whole
            made.layer = layer;
            made.transform.SetParent(desk.Desk, false);
            made.transform.localPosition = bounds.center;
            var box = made.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(Mathf.Max(0.4f, bounds.size.x), Mathf.Max(0.8f, bounds.size.y), Mathf.Max(0.4f, bounds.size.z));
            var pointer = made.AddComponent<DeskPointer>();
            pointer.simpleOverlayType = SimpleOverlayType.ClickToAction;
            pointer.customOverlayHeaderKey = Header;
            pointer.navMeshTargets = new[] { made.transform };
            Found.Clear();
            desk.Desk.GetComponentsInChildren(true, Found);
            if (desk.Chair != null)
            {
                var chair = new List<Renderer>(4);
                desk.Chair.GetComponentsInChildren(true, chair);
                Found.AddRange(chair);
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
            try
            {
                return primaryInteractionEnabled && CareersDirector.Instance != null && base.ShouldReactToIoEnter();
            }
            catch (System.Exception e)
            {
                CareersDirector.Logger?.Error(e);
                return false;
            }
        }

        // As the game's, and then the outlined pieces go back to the layers they came from.
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
            }
            catch (System.Exception e)
            {
                CareersDirector.Logger?.Error(e);
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
            OverlayGuard.Release(this);   // (the game's own lets the card go only while it is on screen)
            if (!GameManager.isCitySceneBeingUnloaded && MouseController.currentTargetEntity == this)
            {
                MouseController.ResetCurrentEntitySelected(this);
            }
        }
    }
}
