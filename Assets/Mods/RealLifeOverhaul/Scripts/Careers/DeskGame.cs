using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Characters;
using Controllers;
using Entities;
using Helpers;
using Items.SpecialItems;
using Player.HUD.ItemInfoOverlays;
using PlayerActivity;
using UI.Elements;
using UI.Guiders;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Everything a working day's desk reads from or does to the game, as its own job at El Gato's till: the station, the click
    /// (an entry at the front of the game's calls to action), the pose, the boss's texts and the destination marker.
    /// </summary>
    internal static class DeskGame
    {
        private static readonly List<EmployeeStationController> Found = new List<EmployeeStationController>(16);
        private static readonly Comparison<EmployeeStationController> ById = CompareById;

        private static PlayerController Player => InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;

        /// <summary>The building the player is in and its registration; false outside, or while the game is still going in or out.</summary>
        public static bool Inside(out BuildingManager manager, out BuildingRegistration reg)
        {
            manager = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            reg = manager != null ? manager.buildingRegistration : null;
            return reg != null && !manager.enteringBuilding && !manager.exitingBuilding;
        }

        /// <summary>True while the game is putting a building together round the player (its black screen at the door).</summary>
        public static bool Entering()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.enteringBuilding;
        }

        /// <summary>The game's own words on whether the business is open now, as WorkActivity says them; null where it says nothing.</summary>
        public static LabelInfo Closed()
        {
            if (!Inside(out BuildingManager manager, out BuildingRegistration reg) || (!reg.HasEstablishedBusiness && (manager.building == null || manager.building.SpecialService == null)))
            {
                return null;
            }
            Colors colors = InstanceBehavior<GlobalReferences>.Instance != null ? InstanceBehavior<GlobalReferences>.Instance.colors : null;
            if (colors == null)
            {
                return null;
            }
            if (!manager.isOpen)
            {
                return reg.OpensWithinHours(2) ? new LabelInfo("workpanel_business_opening_soon", null, colors.yellow) : new LabelInfo("workpanel_business_is_currently_closed", null, colors.red);
            }
            return reg.temporarilyClosed ? new LabelInfo("workpanel_business_is_currently_closed", null, colors.red) : null;
        }

        /// <summary>A label of the game's words (localised by its panel) in one of its colours: 0 white, 1 yellow, 2 orange, 3 red.</summary>
        public static LabelInfo GameLabel(string key, int colour)
        {
            return new LabelInfo(key, null, Colour(colour));
        }

        /// <summary>A label of final words in one of the game's colours.</summary>
        public static LabelInfo Label(string text, int colour)
        {
            return new LabelInfo(text ?? string.Empty, Colour(colour), false);
        }

        private static Color32 Colour(int which)
        {
            Colors colors = InstanceBehavior<GlobalReferences>.Instance != null ? InstanceBehavior<GlobalReferences>.Instance.colors : null;
            if (colors == null)
            {
                return GameUi.WorldPanel.White();
            }
            switch (which)
            {
                case 1: return colors.yellow;
                case 2: return colors.orange;
                case 3: return colors.red;
                default: return colors.white;
            }
        }

        /// <summary>The player's station: of the kinds asked for, the first the building has, the seed's pick in the order of ids; null if none.</summary>
        public static EmployeeStationController Choose(BuildingManager manager, StationKind[] kinds, int seed, int job, out StationKind kind, out int count)
        {
            kind = StationKind.None;
            count = 0;
            List<ItemController> items = manager != null ? manager.allItemControllers : null;
            for (int k = 0; items != null && k < kinds.Length; k++)
            {
                Found.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] is EmployeeStationController station && Usable(station) && KindOf(station) == kinds[k])
                    {
                        Found.Add(station);
                    }
                }
                if (Found.Count == 0)
                {
                    continue;
                }
                Found.Sort(ById);
                kind = kinds[k];
                count = Found.Count;
                EmployeeStationController picked = Found[DeskRules.Pick(Found.Count, seed, job)];
                Found.Clear();
                return picked;
            }
            Found.Clear();
            return null;
        }

        // A station the game would let somebody work at: standing, not for sale, not at a receptionist's desk of the game's.
        private static bool Usable(EmployeeStationController station)
        {
            return station != null && station.gameObject.activeInHierarchy && !(station.playerItemPurchaserSettings != null && station.playerItemPurchaserSettings.enabled)
                   && station.Item != null && station.Item.assignable && !(station.parentItemController is BusinessEmployeeController);
        }

        /// <summary>The kind of one of the game's stations; a desk computer only with its chair (the game seats nobody at one without).</summary>
        public static StationKind KindOf(EmployeeStationController station)
        {
            switch (station)
            {
                case WorkstationController workstation: return workstation.FindChair() != null ? StationKind.Desk : StationKind.None;
                case DesktopWorkstationController desktop: return desktop.employeeChair != null ? StationKind.Desk : StationKind.None;
                case CashRegisterController _: return StationKind.Register;
                case IRSStationController _: return StationKind.Counter;
                case HairdresserChairController _: return StationKind.Chair;
                case SecurityGuardLockerController _: return StationKind.Locker;
                case FactoryAssemblyMachineController _: return StationKind.Machine;
                case CoatCheckController _: return StationKind.Booth;
                default: return StationKind.None;
            }
        }

        // The building's layout gives every item an id of its own: the same order on every visit and every machine.
        private static int CompareById(EmployeeStationController a, EmployeeStationController b)
        {
            string x = a.ItemInstance != null ? a.ItemInstance.id : null, y = b.ItemInstance != null ? b.ItemInstance.id : null;
            int byId = string.CompareOrdinal(x ?? string.Empty, y ?? string.Empty);
            if (byId != 0)
            {
                return byId;
            }
            Vector3 p = a.transform.position, q = b.transform.position;
            int byX = p.x.CompareTo(q.x);
            return byX != 0 ? byX : p.z.CompareTo(q.z);
        }

        /// <summary>The game's person at a station sent away, as WorkActivity sends them when its player starts: the station is the player's.</summary>
        public static void Free(EmployeeStationController station)
        {
            if (station != null && station.employee != null && !station.employee.isPlayer)
            {
                station.UnassignEmployee();
            }
        }

        /// <summary>One of the game's people back at the station, as the game seats them (the job ended while the player was there).</summary>
        public static void GiveBack(EmployeeStationController station)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (station == null || manager == null || !BuildingManager.IsInsideBuilding || manager.IsPlayerOwnedBusiness || station.employee != null || !manager.isOpen)
            {
                return;
            }
            manager.AssignAiToEmployeeStation(station);
        }

        /// <summary>True if somebody else is at the station now (the game seated one of its people there again).</summary>
        public static bool Taken(EmployeeStationController station)
        {
            return station != null && station.employee != null && !station.employee.isPlayer;
        }

        /// <summary>True if the game lets the player into the building at an address now (it is open to the public, or theirs).</summary>
        public static bool CanEnter(string street, int number)
        {
            try
            {
                return !string.IsNullOrEmpty(street) && BuildingHelper.CanEnterBuilding(new Address(street, number));
            }
            catch (Exception)
            {
                return true;   // nothing to say otherwise: the desk as usual
            }
        }

        /// <summary>A computer of the player's home with its chair, the first of them, or null.</summary>
        public static ComputerController HomeComputer()
        {
            if (!Inside(out BuildingManager manager, out BuildingRegistration reg) || !GoingPublic.Mine(reg))
            {
                return null;
            }
            List<ItemController> items = manager.allItemControllers;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is ComputerController computer && computer.gameObject.activeInHierarchy && computer.FindChair() != null)
                {
                    return computer;
                }
            }
            return null;
        }

        /// <summary>A home computer the game itself would pass the time at, one with its chair first; null if none. Looked for on a press.</summary>
        public static ComputerController UsableComputer()
        {
            if (!Inside(out BuildingManager manager, out BuildingRegistration reg) || !GoingPublic.Mine(reg))
            {
                return null;
            }
            ComputerController standing = null;
            List<ItemController> items = manager.allItemControllers;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is ComputerController computer && computer.gameObject.activeInHierarchy && Usable(computer))
                {
                    if (computer.FindChair() != null)
                    {
                        return computer;
                    }
                    standing = standing ?? computer;
                }
            }
            return standing;
        }

        /// <summary>True if the game would let the player pass the time at this computer (not for sale, nothing it needs missing).</summary>
        public static bool Usable(ComputerController computer)
        {
            return computer != null && !(computer.playerItemPurchaserSettings != null && computer.playerItemPurchaserSettings.enabled)
                   && !ItemHelper.HasAnyMissingRequirements(computer.ItemInstance);
        }

        /// <summary>The school's study desk nearest the player with its chair (the classroom door that opens it), or null. Looked for on a press.</summary>
        public static EducationDoorController NearestStudyDoor()
        {
            if (!Inside(out BuildingManager manager, out _))
            {
                return null;
            }
            PlayerController player = Player;
            Vector3 at = player != null ? player.transform.position : Vector3.zero;
            EducationDoorController best = null;
            float near = float.MaxValue;
            List<ItemController> items = manager.allItemControllers;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is EducationDoorController door && door.gameObject.activeInHierarchy && door.StudyDiploma != null && door.StudyDiploma.chair != null)
                {
                    float d = (door.transform.position - at).sqrMagnitude;
                    if (d < near)
                    {
                        near = d;
                        best = door;
                    }
                }
            }
            return best;
        }

        /// <summary>A study door's own refusals before its panel: true if refused, said in the game's words with <paramref name="say"/>.</summary>
        public static bool StudyDoorRefuses(bool say)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || !manager.isOpen)
            {
                if (say)
                {
                    Notifications.ShowError("educationdoorcontroller_closed", "educationdoorcontroller_closed");
                }
                return true;
            }
            bool driving = SaveGameManager.Current != null && !string.IsNullOrEmpty(SaveGameManager.Current.ActiveVehicleId);
            if ((PlayerHelper.ItemInstanceInHands != null && !PlayerHelper.IsHoldingBag) || driving)
            {
                if (say)
                {
                    Notifications.ShowError("businessschool_holding_item", "educationdoorcontroller_holding_item");
                }
                return true;
            }
            return false;
        }

        private sealed class Behaviour : ICtaBehavior
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("careers_study", "DeskGame.Behaviour", typeof(Behaviour));   // the game's calls of it (Core/Guard)

            // asked by the game for whatever the pointer is on: a fault here would break its pointer, so none leaves
            public override bool ShouldShow(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return false;
                }
                try
                {
                    return Offers(entityController);
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
                    return (string.Empty, null);
                }
                try
                {
                    if (!Offers(entityController))
                    {
                        return (string.Empty, null);
                    }
                    _clicked = entityController;
                    return (entityController is EducationDoorController ? "click_to_learn" : "click_to_work", Click);
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
        private static EntityController _clicked;

        /// <summary>What the player can click to work, set by the director: the station or the desk's pointer.</summary>
        public static EntityController Target;

        private static bool Offers(EntityController e)
        {
            if (e is EducationDoorController door)
            {
                return CareersDirector.Instance != null && CareersDirector.Instance.TakesRoomClick(door);   // a university classroom in a session
            }
            if (e == null || !ReferenceEquals(e, Target))
            {
                return false;
            }
            return !(e is EmployeeStationController) || PlayerHelper.HasPaidForAllItems();   // with something unpaid in hand the till sells first
        }

        /// <summary>Adds the entry, once. False if the game's list could not be reached (said once): the desk then cannot be clicked.</summary>
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
                FieldInfo field = GameContract.CtaBehaviours.Field;   // resolved once (Core/GameContract)
                _list = field != null ? field.GetValue(null) as List<ICtaBehavior> : null;
                if (_list == null)
                {
                    _failed = true;
                    CareersDirector.Logger?.Warn("Careers: the game's list of click actions was not found, so a desk cannot be clicked; the Career app starts the working day instead.");
                    return false;
                }
                _behaviour = new Behaviour();
                _list.Insert(0, _behaviour);   // asked first: it answers only for the player's own desk
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                _behaviour = null;
                CareersDirector.Logger?.Error(e);
                return false;
            }
        }

        /// <summary>True if a desk can be clicked in this game.</summary>
        public static bool Clickable => _behaviour != null;

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
                CareersDirector.Logger?.Error(e);
            }
            _behaviour = null;
            _list = null;
            _failed = false;
            Target = null;
        }

        private static void Clicked()
        {
            EntityController e = _clicked;
            _clicked = null;
            try
            {
                if (e is EducationDoorController door)
                {
                    CareersDirector.Instance?.RoomDoorClicked(door);
                    return;
                }
                CareersDirector.Instance?.DeskClicked(e);
            }
            catch (Exception ex)
            {
                CareersDirector.Logger?.Error(ex);
            }
        }

        /// <summary>The player walks to a station (or the home computer) and then the action runs, as the game's till does it.</summary>
        public static bool WalkTo(EntityController station, Action then)
        {
            PlayerController player = Player;
            if (player == null || station == null || !PlayerActivityUI.CanStartActivity())
            {
                return false;
            }
            station.MoveTowardsEntity(Guard.Listener(() => then()));
            return true;
        }

        /// <summary>The player walks to a place on the floor (a fitted desk's chair) and then the action runs.</summary>
        public static bool WalkTo(Vector3 place, Action then)
        {
            PlayerController player = Player;
            if (player == null || !PlayerActivityUI.CanStartActivity() || !player.ExistsRoute(place, showErrorNotification: true))
            {
                return false;
            }
            player.SetGoal(place, Guard.Listener(() => then()));
            return true;
        }

        /// <summary>The work panel of the game's own on screen with an activity; false if the panel is busy.</summary>
        public static bool Show(IPlayerActivity activity)
        {
            if (PlayerActivityUI.IsPanelOpen)
            {
                return false;
            }
            PlayerActivityUI.Show(activity);
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            return uis != null && uis.playerActivityUI != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity);
        }

        /// <summary>True while the game's panel still has an activity.</summary>
        public static bool Shows(IPlayerActivity activity)
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            return uis != null && uis.playerActivityUI != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity);
        }

        /// <summary>The chair seat of one of the game's desk computers; null for a station whose employee stands at it.</summary>
        public static Transform SeatOf(EmployeeStationController station)
        {
            switch (station)
            {
                case WorkstationController workstation: return Place(workstation.FindChair());
                case DesktopWorkstationController desktop: return Place(desktop.employeeChair);
                default: return null;
            }
        }

        // Where a piece of the game's furniture is, whichever kind of object the game holds it as.
        private static Transform Place(UnityEngine.Object piece)
        {
            if (piece == null)
            {
                return null;
            }
            switch (piece)
            {
                case Component part: return part.transform;
                case GameObject thing: return thing.transform;
                default: return null;
            }
        }

        /// <summary>The player sets to work at a station whose people stand, as WorkActivity does it on arriving: the station's employee, held there.</summary>
        public static void SitAt(EmployeeStationController station)
        {
            PlayerController player = Player;
            if (player == null || station == null)
            {
                return;
            }
            player.SetNavigationBlocker(NavigationBlocker.WorkActivity);
            EnergyHelper.RemoveEnergySpender("move");
            station.UnassignEmployee();
            station.Occupied = true;
            station.AssignEmployee(player.Character, PlayerHelper.GetPlayerEmployeeInstance());
            player.Character.LinkToPointAndClickObject(station);
        }

        /// <summary>...and stands up from it, as WorkActivity's Finish does; the work blocker goes whatever fails before it. Never throws.</summary>
        public static void StandFrom(EmployeeStationController station)
        {
            PlayerController player = Player;
            try
            {
                if (station != null)
                {
                    station.UnassignEmployee();
                    station.Occupied = false;
                }
                if (player != null)
                {
                    player.Character.Reset();
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            finally
            {
                Unhold(player);
            }
        }

        /// <summary>The player sits on a desk's chair seat, held by the work blocker; <paramref name="stood"/> is where to stand up.</summary>
        public static void SitOn(Transform seat, EmployeeStationController station, out Vector3 stood)
        {
            PlayerController player = Player;
            stood = player != null ? player.transform.position : Vector3.zero;
            if (player == null || seat == null)
            {
                return;
            }
            player.SetNavigationBlocker(NavigationBlocker.WorkActivity);
            EnergyHelper.RemoveEnergySpender("move");
            if (station != null)
            {
                station.Occupied = true;
            }
            player.Character.SitOnChair(seat);
        }

        /// <summary>...and stands up beside the desk; the work blocker goes whatever fails before it. Never throws.</summary>
        public static void StandUp(Vector3 at, EmployeeStationController station)
        {
            PlayerController player = Player;
            try
            {
                if (station != null)
                {
                    station.Occupied = false;
                }
                if (player != null)
                {
                    player.Character.Reset();
                    if (at != Vector3.zero)
                    {
                        player.Character.navmeshAgent.Warp(at);
                    }
                    PlayerHelper.GetAnimator().ResetTrigger(AnimationType.UsingDesktopComputer.ToStringFast());
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            finally
            {
                Unhold(player);
            }
        }

        // The work blocker off the player: the last step of every way they stand up.
        private static void Unhold(PlayerController player)
        {
            try
            {
                if (player != null)
                {
                    player.UnsetNavigationBlocker(NavigationBlocker.WorkActivity);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        /// <summary>The player types now and then at a fitted desk, as the game's desk computers have their employees do.</summary>
        public static void Type()
        {
            try
            {
                PlayerHelper.GetAnimator().SetTrigger(AnimationType.UsingDesktopComputer.ToStringFast());
            }
            catch (Exception)
            {
                // the animation is a nicety
            }
        }

        /// <summary>The walk on the way to a station called off, as the game's panel calls it off.</summary>
        public static void StopWalking()
        {
            Player?.ResetNavigation();
        }

        /// <summary>
        /// The self-check of a walk to its place ("Setting off" in docs/features/careers.md), with Developer diagnostics on: PASS
        /// when it got there; FAIL when the panel called it off in the frame its walk order was given (<paramref name="orderFrame"/>).
        /// </summary>
        public static void SetOffSeen(string what, bool there, int orderFrame)
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            if (there)
            {
                CareersDirector.Logger?.Info("Careers check (set off): the walk to " + what + " got there, the game's panel holding it: PASS");
            }
            else if (orderFrame == Time.frameCount)
            {
                CareersDirector.Logger?.Info("Careers check (set off): the game's panel took the walk order to " + what + " for a click elsewhere, in the frame"
                                             + " it was given: FAIL, it was set off later than the panel allows");
            }
        }

        /// <summary>A text from the boss of the employer at an address, as the game's own employer texts its player. False if it could not be sent.</summary>
        public static bool BossText(string employer, string street, int number, string key, Dictionary<string, string> data)
        {
            try
            {
                if (SaveGameManager.Current == null || string.IsNullOrEmpty(employer))
                {
                    return false;
                }
                Contact boss = Contact.GetContact(employer, ContactCategoryName.Employees, "reallife_contact_boss_desc",
                    string.IsNullOrEmpty(street) ? null : new Address(street, number), false, true);
                GameManager.SendTextMessage(boss, key, data);
                return true;
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
                return false;
            }
        }

        /// <summary>The game's own destination marker on a building, as "set destination" puts it there. False if it could not be.</summary>
        public static bool Route(string street, int number)
        {
            try
            {
                if (string.IsNullOrEmpty(street) || InstanceBehavior<GuidersManager>.Instance == null)
                {
                    return false;
                }
                GuidersManager.SetGuiderTarget(new Address(street, number), DirectionGuiderType.Destination);
                return true;
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
                return false;
            }
        }
    }
}
