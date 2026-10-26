using System;
using System.Collections.Generic;
using System.Reflection;
using Helpers;
using Player.HUD.ItemInfoOverlays;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The front desk on the game's side: the office the player is in, its front desk (the game's own staff at the
    /// workstation nearest the door the player came in by, seated and at work as the game sets them), the pointer over the
    /// person there, the click (an entry at the front of the game's calls to action that answers only for that pointer, with
    /// the game's own "click_to_talk" of its special desks), the walk up to the desk, and the game paused while the player
    /// talks as the game's desk staff pause it (never in a Going Public session, whose clock is everyone's). Nothing here
    /// keeps the desk: the director does. COST: nothing per frame; a pass over the building's items when a desk is looked for.
    /// </summary>
    internal static class BuilderDeskGame
    {
        public const string Talk = "click_to_talk";   // the game's own call to action at a special employee's desk (ManageCtaBehavior)
        private const int MostDesks = 64;              // workstations looked at in one office; the game's largest office layouts have fewer
        private const float StandOff = 1.2f;           // m in front of the person the player stops to talk, about where the game's customer chair stands
        private const float StandSearch = 0.9f;        // m round that place the game's walking mesh is looked for
        private const float TalkReach = 3.5f;          // m: a player this near whom the game finds no way round talks where they stand

        private static readonly List<EmployeeStationController> Stations = new List<EmployeeStationController>(MostDesks);
        private static readonly float[] Xs = new float[MostDesks];
        private static readonly float[] Zs = new float[MostDesks];

        // ------------------------------------------------------------------ where the player is

        /// <summary>The building the player is in now as "street#number"; null outdoors or on the game's way in or out.</summary>
        internal static string Inside()
        {
            BuildingManager manager = Manager;
            BuildingRegistration reg = manager != null ? manager.buildingRegistration : null;
            if (!BuildingManager.IsInsideBuilding || reg == null || reg.Address == null || manager.enteringBuilding || manager.exitingBuilding)
            {
                return null;
            }
            return ConstructionGame.Place(reg.Address.streetName, reg.Address.streetNumber);
        }

        /// <summary>True when the player is inside an office of their own construction company (never another player's).</summary>
        internal static bool InOwnOffice()
        {
            BuildingManager manager = Manager;
            BuildingRegistration reg = manager != null ? manager.buildingRegistration : null;
            return BuildingManager.IsInsideBuilding && ConstructionType.Is(reg) && GoingPublic.Mine(reg);
        }

        private static BuildingManager Manager => InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance : null;

        // ------------------------------------------------------------------ the front desk

        /// <summary>
        /// The workstation of the building the player is in that is its front desk (FrontDeskRules.Front: of those somebody of the
        /// game's sits at, the nearest the door the game lets the player in by), and that person; null when nobody sits at one.
        /// </summary>
        internal static EmployeeStationController Front(out ThirdPersonCharacter person)
        {
            person = null;
            BuildingManager manager = Manager;
            List<ItemController> items = manager != null ? manager.allItemControllers : null;
            if (items == null || !Door(manager, out Vector3 door))
            {
                return null;
            }
            Stations.Clear();
            for (int i = 0; i < items.Count && Stations.Count < MostDesks; i++)
            {
                var station = items[i] as EmployeeStationController;
                if (station != null && station.isActiveAndEnabled && Seated(station) != null)
                {
                    Vector3 at = station.transform.position;
                    Xs[Stations.Count] = at.x;
                    Zs[Stations.Count] = at.z;
                    Stations.Add(station);
                }
            }
            int pick = FrontDeskRules.Front(Xs, Zs, Stations.Count, door.x, door.z);
            EmployeeStationController front = pick >= 0 ? Stations[pick] : null;
            Stations.Clear();
            person = front != null ? Seated(front) : null;
            return front;
        }

        /// <summary>The person of the game's at a workstation, at work there; null when it is empty, its person away, or the player works it.</summary>
        internal static ThirdPersonCharacter Seated(EmployeeStationController station)
        {
            Employee e = station != null ? station.employee : null;
            return e != null && !e.isPlayer && !e.IsAway && e.employeeTpc != null && e.employeeTpc.gameObject.activeInHierarchy ? e.employeeTpc : null;
        }

        // Where the game lets the player in: its first spawn point, as BuildingManager.GetFirstExitZone picks it on foot.
        private static bool Door(BuildingManager manager, out Vector3 door)
        {
            door = default;
            List<ExitZone> zones = manager.exitZones;
            ExitZone first = null;
            for (int i = 0; zones != null && i < zones.Count; i++)
            {
                if (zones[i] == null)
                {
                    continue;
                }
                first = first ?? zones[i];
                if (zones[i].isPrimarySpawnPoint)
                {
                    first = zones[i];
                    break;
                }
            }
            if (first == null)
            {
                return false;
            }
            Transform at = first.playerSpawnPoint != null ? first.playerSpawnPoint : first.door != null ? first.door : first.transform;
            door = at.position;
            return true;
        }

        // ------------------------------------------------------------------ the walk up

        /// <summary>
        /// The player walks up to the desk and then the conversation opens, as the game walks them to its special desks: to a
        /// place on the game's walking mesh in front of the person, on the player's side. A player the game finds no way to
        /// it for talks where they stand when near; otherwise the game's own words say it cannot be reached. False when
        /// nothing started.
        /// </summary>
        internal static bool WalkUp(BuilderDeskPointer pointer, Action then)
        {
            PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
            ThirdPersonCharacter person = pointer != null ? pointer.Person : null;
            if (player == null || person == null || then == null)
            {
                return false;
            }
            Vector3 from = player.transform.position, at = person.transform.position;
            Vector3 toward = from - at;
            toward.y = 0f;
            float distance = toward.magnitude;
            toward = distance > 0.01f ? toward / distance : person.transform.forward;
            if (NavMesh.SamplePosition(at + toward * StandOff, out NavMeshHit hit, StandSearch, PlayerController.navMeshQueryFilter) && player.ExistsRoute(hit.position))
            {
                pointer.StandAt(hit.position);
                pointer.MoveTowardsEntity(Guard.Listener(() => then()));
                return true;
            }
            if (distance <= TalkReach)
            {
                then();
                return true;
            }
            Notifications.ShowError("reallife_bld_desk_no_way");
            return false;
        }

        // ------------------------------------------------------------------ time while talking

        private static bool _held, _paused;

        /// <summary>The game paused and its speed controls locked while the player talks, as its desk staff do it; never in a Going Public session.</summary>
        internal static void Hold()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.gameSpeed == null || _held || GoingPublic.InSession)
            {
                return;
            }
            _paused = !uis.gameSpeed.Paused;
            if (_paused)
            {
                uis.gameSpeed.SetPause(true, false);
            }
            uis.gameSpeed.DisableTimeControl(true);
            _held = true;
        }

        /// <summary>Time as it was before the talk: the controls given back, and running again if the talk paused it.</summary>
        internal static void Release()
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.gameSpeed == null)
            {
                return;
            }
            uis.gameSpeed.DisableTimeControl(false);
            if (_paused)
            {
                uis.gameSpeed.SetPause(false);
            }
            _paused = false;
        }

        // ------------------------------------------------------------------ the click

        private sealed class Behaviour : ICtaBehavior
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("construction", "BuilderDeskGame.Behaviour", typeof(Behaviour));   // the game's calls of it (Core/Guard)

            // asked by the game for whatever the pointer is on: a fault here would break its pointer, so none leaves
            public override bool ShouldShow(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return false;
                }
                try
                {
                    return entityController is BuilderDeskPointer;
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
                    if (!(entityController is BuilderDeskPointer pointer))
                    {
                        return (string.Empty, (Action)null);
                    }
                    _clicked = pointer;
                    return (Talk, Click);
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
        private static BuilderDeskPointer _clicked;

        /// <summary>Adds the entry, once; false if the game's list could not be reached (said once): the desk then cannot be clicked.</summary>
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
                    ConstructionDirector.Logger?.Warn("Construction: the game's list of click actions was not found, so nobody at a builder's desk can be talked to;"
                                                      + " the Construction app hires and quotes as before.");
                    return false;
                }
                _behaviour = new Behaviour();
                _list.Insert(0, _behaviour);   // asked first: it answers only for the desk's pointer
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
            BuilderDeskPointer pointer = _clicked;
            _clicked = null;
            try
            {
                ConstructionDirector.Instance?.DeskClicked(pointer);
            }
            catch (Exception e)
            {
                ConstructionDirector.Logger?.Error(e);
            }
        }
    }
}
