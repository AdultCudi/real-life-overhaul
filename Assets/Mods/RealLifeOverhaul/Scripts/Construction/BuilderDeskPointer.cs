using System.Collections.Generic;
using Player.HUD.ItemInfoOverlays;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The person at an office's front desk as a thing to point at: a trigger box on the game's interactive layer round them,
    /// from the desk's top to their head (so a click on the desk itself is still the game's own), the desk and the person
    /// outlined as the game outlines its special desks, with the person's role over the game's "Click to talk". It lives on
    /// the game's workstation and goes with it when the player leaves. No Update, nothing per frame: the game asks it as the
    /// pointer comes, goes or clicks.
    /// </summary>
    public sealed class BuilderDeskPointer : EntityController
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("construction", "BuilderDeskPointer", typeof(BuilderDeskPointer));   // the game's calls of it (Core/Guard)

        private const float LowerShare = 0.4f;   // of the person's height, the part under the desk's top the box leaves to the desk
        private const float LeastWidth = 0.5f;   // m across: a slim person seated side on is still easy to point at

        private static readonly List<Renderer> Found = new List<Renderer>(32);

        private int[] _layers;
        private Transform _stand;

        /// <summary>The workstation the person works at.</summary>
        public EmployeeStationController Station { get; private set; }

        /// <summary>The person of the game's at the desk.</summary>
        public ThirdPersonCharacter Person { get; private set; }

        /// <summary>The rival builder whose office it is, or -1 for the player's own company's.</summary>
        public int Rival { get; private set; }

        /// <summary>A pointer round the person at the desk. Null if the game has no layer for it.</summary>
        public static BuilderDeskPointer Make(EmployeeStationController station, ThirdPersonCharacter person, int rival, string headerKey)
        {
            int layer = LayerMask.NameToLayer("InteractiveItems");
            if (layer < 0 || station == null || person == null)
            {
                return null;
            }
            Bounds body = Body(person);
            var made = new GameObject("RealLife Builder Desk");
            made.SetActive(false);   // nothing of it wakes until it is whole
            made.layer = layer;
            made.transform.SetParent(station.transform, false);
            float low = body.min.y + body.size.y * LowerShare;
            made.transform.position = new Vector3(body.center.x, (low + body.max.y) * 0.5f, body.center.z);
            made.transform.rotation = Quaternion.identity;
            var box = made.AddComponent<BoxCollider>();
            box.isTrigger = true;
            float across = Mathf.Max(LeastWidth, Mathf.Max(body.size.x, body.size.z));
            box.size = new Vector3(across, Mathf.Max(0.6f, body.max.y - low), across);
            var stand = new GameObject("Stand");
            stand.transform.SetParent(made.transform, false);
            var pointer = made.AddComponent<BuilderDeskPointer>();
            pointer.Station = station;
            pointer.Person = person;
            pointer.Rival = rival;
            pointer._stand = stand.transform;
            pointer.simpleOverlayType = SimpleOverlayType.ClickToAction;
            pointer.customOverlayHeaderKey = headerKey;
            pointer.navMeshTargets = new[] { stand.transform };
            Found.Clear();
            station.GetComponentsInChildren(true, Found);   // the desk, its computer and the person sitting at it
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

        // The person's extent as their renderers draw it; a seated person's size round where they sit when none is drawn.
        private static Bounds Body(ThirdPersonCharacter person)
        {
            Found.Clear();
            person.GetComponentsInChildren(false, Found);
            bool any = false;
            var body = new Bounds(person.transform.position + Vector3.up * 0.65f, new Vector3(0.6f, 1.3f, 0.6f));
            for (int i = 0; i < Found.Count; i++)
            {
                if (Found[i] == null || !Found[i].enabled)
                {
                    continue;
                }
                if (!any)
                {
                    body = Found[i].bounds;
                    any = true;
                }
                else
                {
                    body.Encapsulate(Found[i].bounds);
                }
            }
            Found.Clear();
            return body;
        }

        /// <summary>The place in front of the desk the player walks to before they talk.</summary>
        public void StandAt(Vector3 place)
        {
            if (_stand != null)
            {
                _stand.position = place;
            }
        }

        /// <summary>True while the person it was made for still sits at the desk, at work.</summary>
        public bool Staffed => Station != null && Person != null && BuilderDeskGame.Seated(Station) == Person;

        // The game's own Awake and Start set up a layer and walk-to places this object does not need.
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
                ConstructionDirector d = ConstructionDirector.Instance;
                return primaryInteractionEnabled && d != null && d.Running && Staffed && !ThirdPersonCamera.IsUiOpen()
                       && !BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode && !UI.InteriorDesigner.InteriorDesignerUI.IsOpen;
            }
            catch (System.Exception e)
            {
                return Guard.Failed(FrameGuard, e, false);
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
                Guard.Fail(FrameGuard, e);
            }
            try
            {
                for (int i = 0; renderers != null && i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].gameObject.layer = _layers[i];
                    }
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e);
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
}
