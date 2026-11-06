using System;
using System.Collections.Generic;
using System.Reflection;
using Player.HUD.ItemInfoOverlays;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A car park's pay machine as a thing to point at: a trigger box on the game's interactive layer round the booth, the booth outlined
    /// as the game outlines what can be used, with "Use the pay machine" over the game's click. It lives on the booth and goes with it when
    /// the car park is taken away. No Update, nothing per frame: the game asks it as the pointer comes, goes or clicks.
    /// </summary>
    public sealed class CarParkPointer : EntityController
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("construction", "CarParkPointer", typeof(CarParkPointer));   // the game's calls of it (Core/Guard)

        /// <summary>The machine's name over the click.</summary>
        public const string Header = "reallife_park_machine";

        private int[] _layers;
        private Transform _stand;

        /// <summary>The car park (its id in the construction game's state) whose machine this is.</summary>
        public int CarPark { get; private set; }

        /// <summary>A pointer round the booth: a trigger box in the booth's own axes; null if the game has no layer for it or its list of click actions cannot be reached.</summary>
        internal static CarParkPointer Make(Transform booth, Vector3 centre, Vector3 size, int carPark)
        {
            int layer = LayerMask.NameToLayer("InteractiveItems");
            if (layer < 0 || booth == null || !CarParkCta.Install())
            {
                return null;
            }
            var made = new GameObject("RealLife Car Park Machine");
            made.SetActive(false);   // nothing of it wakes until it is whole
            made.layer = layer;
            made.transform.SetParent(booth, false);
            made.transform.localPosition = centre;
            var box = made.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var stand = new GameObject("Stand");
            stand.transform.SetParent(made.transform, false);
            var pointer = made.AddComponent<CarParkPointer>();
            pointer.CarPark = carPark;
            pointer._stand = stand.transform;
            pointer.simpleOverlayType = SimpleOverlayType.ClickToAction;
            pointer.customOverlayHeaderKey = Header;
            pointer.navMeshTargets = new[] { stand.transform };
            pointer.renderers = booth.GetComponentsInChildren<Renderer>(true);
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

        /// <summary>The place in front of the machine the player walks to before the dialogue opens.</summary>
        public void StandAt(Vector3 place)
        {
            if (_stand != null)
            {
                _stand.position = place;
            }
        }

        // The game's Awake sets a layer (set already) and its Start reads walk-to places the game's objects have.
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
                return primaryInteractionEnabled && d != null && d.Running && !ThirdPersonCamera.IsUiOpen() && !BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode
                       && !UI.InteriorDesigner.InteriorDesignerUI.IsOpen;
            }
            catch (Exception e)
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
            catch (Exception e)
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
            catch (Exception e)
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

    /// <summary>
    /// The pay machine's entry in the game's list of calls to action (CtaManager.CtaBehaviors, by reflection as the builders' desk and the
    /// copy at home have theirs): it answers only for a <see cref="CarParkPointer"/>. The click walks the player up to the machine, then the
    /// dialogue opens. COST: nothing per frame; the game asks it as the pointer moves over something.
    /// </summary>
    internal static class CarParkCta
    {
        /// <summary>The call to action over the machine, a text of the mod's own.</summary>
        public const string Use = "reallife_park_cta_use";

        private const float StandOff = 2.0f;     // m in front of the booth the player stops
        private const float StandSearch = 1.2f;  // m round that place the game's walking mesh is looked for
        private const float UseReach = 4.5f;     // m: a player this near whom the game finds no way round uses it where they stand

        private sealed class Behaviour : ICtaBehavior
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("construction", "CarParkCta.Behaviour", typeof(Behaviour));   // the game's calls of it (Core/Guard)

            // asked by the game for whatever the pointer is on: a fault here would break its pointer, so none leaves
            public override bool ShouldShow(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return false;
                }
                try
                {
                    return entityController is CarParkPointer;
                }
                catch (Exception e)
                {
                    return Guard.Failed(FrameGuard, e, false);
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
                    if (!(entityController is CarParkPointer pointer))
                    {
                        return (string.Empty, (Action)null);
                    }
                    _clicked = pointer;
                    return (Use, Click);
                }
                catch (Exception e)
                {
                    return Guard.Failed(FrameGuard, e, (string.Empty, (Action)null));
                }
            }
        }

        private static readonly Action Click = Clicked;
        private static Behaviour _behaviour;
        private static List<ICtaBehavior> _list;
        private static bool _failed;
        private static CarParkPointer _clicked;

        /// <summary>Adds the entry, once; false if the game's list could not be reached (said once): the machine then cannot be clicked, and the build mode's panel has the same actions.</summary>
        internal static bool Install()
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
                FieldInfo field = GameContract.CtaBehaviours.Field;   // resolved once (Core/GameContract)
                _list = field != null ? field.GetValue(null) as List<ICtaBehavior> : null;
                if (_list == null)
                {
                    _failed = true;
                    ConstructionDirector.Logger?.Warn("Construction: the game's list of click actions was not found, so a car park's pay machine cannot be clicked;"
                                                      + " the build mode's panel at the car park has the same actions.");
                    return false;
                }
                _behaviour = new Behaviour();
                _list.Insert(0, _behaviour);   // asked first: it answers only for the machine's pointer
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                _behaviour = null;
                ConstructionDirector.Logger?.Error(e);
                return false;
            }
        }

        /// <summary>Takes the entry out of the game's list again.</summary>
        internal static void Remove()
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
                ConstructionDirector.Logger?.Error(e);
            }
            _behaviour = null;
            _list = null;
            _failed = false;
            _clicked = null;
        }

        private static void Clicked()
        {
            CarParkPointer pointer = _clicked;
            _clicked = null;
            try
            {
                ConstructionDirector.Instance?.ParkClicked(pointer);
            }
            catch (Exception e)
            {
                ConstructionDirector.Logger?.Error(e);
            }
        }

        /// <summary>
        /// The player walks up to the machine and then the dialogue opens, as the game walks them to its special desks: to a place on the
        /// game's walking mesh in front of the booth, on the player's side. A player the game finds no way to it for uses it where they stand
        /// when near; otherwise the game's own words say it cannot be reached. False when nothing started.
        /// </summary>
        internal static bool WalkUp(CarParkPointer pointer, Action then)
        {
            PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
            if (player == null || pointer == null || then == null)
            {
                return false;
            }
            Vector3 from = player.transform.position, at = pointer.transform.position;
            Vector3 toward = from - at;
            toward.y = 0f;
            float distance = toward.magnitude;
            toward = distance > 0.01f ? toward / distance : pointer.transform.forward;
            if (NavMesh.SamplePosition(at + toward * StandOff, out NavMeshHit hit, StandSearch, PlayerController.navMeshQueryFilter) && player.ExistsRoute(hit.position))
            {
                pointer.StandAt(hit.position);
                pointer.MoveTowardsEntity(Guard.Listener(() => then()));
                return true;
            }
            if (distance <= UseReach)
            {
                then();
                return true;
            }
            Notifications.ShowError("reallife_bld_desk_no_way");
            return false;
        }
    }
}
