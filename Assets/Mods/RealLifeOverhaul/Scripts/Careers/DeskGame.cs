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
using RealLifeOverhaul.FitOut;
using UI.Elements;
using UI.Guiders;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.AI;

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
        private static readonly FieldInfo BlockersField = GameContract.NavBlockers.Field;   // PlayerController._activeNavigationBlockers: the work hold told from the others

        /// <summary>The notice when the game would not seat the player at a desk or take them at a station: the work did not start.</summary>
        public const string SeatRefusedKey = "reallife_desk_seat_refused";

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

        /// <summary>
        /// The player's station: of the kinds asked for, the first the building has, the seed's pick in the order of ids, or
        /// the next one round if the player cannot walk to it (DeskReach.First, as the game asks before it walks them to a
        /// station); null if none.
        /// </summary>
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
                PlayerController player = Player;
                Vector3 from = player != null ? player.transform.position : Vector3.zero;
                int at = DeskReach.First(DeskRules.Pick(Found.Count, seed, job), Found.Count, n => player == null || Reachable(Found[n], from));
                EmployeeStationController picked = at >= 0 ? Found[at] : null;
                Found.Clear();
                if (picked != null)
                {
                    return picked;
                }
                kind = StationKind.None;   // (none of this kind can be walked to: the next kind, or a preset fitted out)
                count = 0;
            }
            Found.Clear();
            return null;
        }

        // True if the player can walk from a point to a station, as the game asks before it walks them to one (PlayerController.SetGoal with its route checked).
        private static bool Reachable(EmployeeStationController station, Vector3 from)
        {
            Vector3 target = station.GetClosestNavMeshTargetPosition(from);
            return target != Vector3.zero && FitReach.Walkable(from, target, DeskReach.SampleRadius, out _);
        }

        /// <summary>A station the game would let somebody work at: standing, not for sale, not at a receptionist's desk of the game's.</summary>
        public static bool Usable(EmployeeStationController station)
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

        /// <summary>
        /// The station's scheduled employee seated again at a business of the player's, as the game's WorkActivity does a frame
        /// after the player stands up from one there; nothing while it is closed (the game seats nobody then either).
        /// </summary>
        public static void Restaff(EmployeeStationController station)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (station == null || manager == null || !BuildingManager.IsInsideBuilding || !manager.IsPlayerOwnedBusiness || !manager.isOpen || station.employee != null)
            {
                return;
            }
            station.UpdateEmployee();
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

        /// <summary>
        /// A computer of the player's home with its chair that the game opens a panel for, the first of them, or null: the one the working day's and the contract's buttons are on
        /// (<see cref="OwnPlaceRules.ComputerPanel"/>). Where there is none the Career app starts the work, so a computer whose panel never opens is not a way in.
        /// </summary>
        public static ComputerController HomeComputer()
        {
            if (!Inside(out BuildingManager manager, out BuildingRegistration reg) || !GoingPublic.Mine(reg))
            {
                return null;
            }
            List<ItemController> items = manager.allItemControllers;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is ComputerController computer && computer.gameObject.activeInHierarchy && OwnPlaceRules.ComputerPanel(computer.FindChair() != null, Usable(computer)))
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

        /// <summary>
        /// A second thing the player can click to work, owned by a feature other than the careers' (the bank branch's counter,
        /// Economy/OwnBank/Branch; a free desk of the player's own tech firm, Tech's OwnDesk), with what a click on it does. Set
        /// by its owner, and cleared only by the owner whose <see cref="OtherClicked"/> it holds; asked about only while it is set.
        /// </summary>
        public static EntityController OtherTarget;
        public static Action<EntityController> OtherClicked;

        private static bool Offers(EntityController e)
        {
            if (e is EducationDoorController door)
            {
                return CareersDirector.Instance != null && CareersDirector.Instance.TakesRoomClick(door);   // a university classroom in a session
            }
            if (e == null || (!ReferenceEquals(e, Target) && !ReferenceEquals(e, OtherTarget)))
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
                if (e != null && ReferenceEquals(e, OtherTarget) && !ReferenceEquals(e, Target))
                {
                    OtherClicked?.Invoke(e);
                    return;
                }
                CareersDirector.Instance?.DeskClicked(e);
            }
            catch (Exception ex)
            {
                CareersDirector.Logger?.Error(ex);
            }
        }

        /// <summary>
        /// The player walks to a station (or the home computer) and then the action runs, as the game's till does it. A
        /// player at the station already whom the game finds no way to walk to it is taken as there (DeskReach.AtDesk).
        /// </summary>
        public static bool WalkTo(EntityController station, Action then)
        {
            PlayerController player = Player;
            if (player == null || station == null || !PlayerActivityUI.CanStartActivity())
            {
                return false;
            }
            Vector3 from = player.transform.position, target = station.GetClosestNavMeshTargetPosition(from);
            if ((target == Vector3.zero || !player.ExistsRoute(target)) && DeskReach.AtDesk(Mathf.Sqrt(CitySpots.Flat(station.transform.position - from))))
            {
                CareersDirector.Logger?.Info("Careers: the game finds no way to the " + station.name + ", and the player is beside it: the work starts where they stand.");
                then();
                return true;
            }
            station.MoveTowardsEntity(Guard.Listener(() => then()));
            return true;
        }

        /// <summary>
        /// True if <see cref="WalkTo(EntityController, Action)"/> would get the player to the station: the game finds a way, or they
        /// are beside it. Asked before a walk the game would refuse silently (its goal call says an error and never calls back), so
        /// a caller that waits for the arrival does not wait for one that cannot come.
        /// </summary>
        public static bool Reaches(EntityController station)
        {
            PlayerController player = Player;
            if (player == null || station == null)
            {
                return false;
            }
            Vector3 from = player.transform.position, target = station.GetClosestNavMeshTargetPosition(from);
            return (target != Vector3.zero && player.ExistsRoute(target)) || DeskReach.AtDesk(Mathf.Sqrt(CitySpots.Flat(station.transform.position - from)));
        }

        /// <summary>
        /// The player walks to a fitted desk's place by its chair and then the action runs. If the game finds no way to that
        /// place, another by the chair it does find a way to is taken (FitReach.StandBy), or, with none, the player beside
        /// the desk already is taken as there; otherwise the game says it cannot reach it, as it does.
        /// </summary>
        public static bool WalkTo(FitDesk desk, Action then)
        {
            PlayerController player = Player;
            if (player == null || desk == null || !PlayerActivityUI.CanStartActivity())
            {
                return false;
            }
            Vector3 from = player.transform.position;
            if (!player.ExistsRoute(desk.Stand))
            {
                if (FitReach.StandBy(desk.Seat, desk.Desk, from, from.y, out Vector3 stand))
                {
                    CareersDirector.Logger?.Info("Careers: the game finds no way to the place by the player's desk; another by its chair it does: " + Where(desk.Stand) + " to " + Where(stand) + ".");
                    desk.Stand = stand;
                }
                else if (desk.Seat != null && DeskReach.AtDesk(Mathf.Sqrt(CitySpots.Flat(desk.Seat.position - from)))
                         && NavMesh.SamplePosition(from, out NavMeshHit here, 1f, PlayerController.navMeshQueryFilter))
                {
                    CareersDirector.Logger?.Info("Careers: the game finds no way to any place by the player's desk, and the player is beside it: they stand up where they stood.");
                    desk.Stand = here.position;
                    then();
                    return true;
                }
            }
            if (!player.ExistsRoute(desk.Stand, showErrorNotification: true))
            {
                return false;
            }
            player.SetGoal(desk.Stand, Guard.Listener(() => then()));
            return true;
        }

        private static string Where(Vector3 at) => at.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

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

        /// <summary>True while the game's work panel is up (an activity on, or one offered) or the player is in a vehicle.</summary>
        public static bool Busy => PlayerActivityUI.IsPanelOpen || PlayerHelper.IsUsingVehicle;

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

        /// <summary>
        /// The player sets to work at a station as WorkActivity does it on arriving: the station's employee, held there by the
        /// work hold, and on its chair where it is a desk (the game's desk seats its employee itself). True when they are at it
        /// so. False when the game would not take them, with the player given back free to walk and the game's notice said: a
        /// caller ends its work then, and marks it begun no later than a true answer, so that each of its ends stands them up
        /// (EconomyCheck's DeskSeatChecks). Never throws.
        /// </summary>
        public static bool SitAt(EmployeeStationController station)
        {
            PlayerController player = Player;
            if (player == null || station == null)
            {
                return false;
            }
            player.SetNavigationBlocker(NavigationBlocker.WorkActivity);
            try
            {
                EnergyHelper.RemoveEnergySpender("move");
                Free(station);   // the game's person sent away; never the bare UnassignEmployee, which a desk's own (WorkstationController) throws in with nobody at it
                station.Occupied = true;
                station.AssignEmployee(player.Character, PlayerHelper.GetPlayerEmployeeInstance());
                player.Character.LinkToPointAndClickObject(station);
                if (Holds(station, player))
                {
                    ScreenOn(station);   // the desk computer's screens lit, as the game lights them for its own employee
                    return true;
                }
                CareersDirector.Logger?.Warn("Careers: the game did not take the player at the " + station.name + (station is DesktopWorkstationController ? " onto its chair" : " as its employee")
                                             + ": they are given back free to walk and the work does not start.");
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            StandFrom(station);
            Refused();
            return false;
        }

        // The screens of a desk computer lit for the player and dark again (Core/DeskScreens); a till or a counter has none of the game's.
        private static void ScreenOn(EmployeeStationController station)
        {
            if (station is DesktopWorkstationController)
            {
                DeskScreens.OnItem(station);
            }
        }

        private static void ScreenOff(EmployeeStationController station)
        {
            if (station is DesktopWorkstationController)
            {
                DeskScreens.OffItem(station);
            }
        }

        // True while the player is the station's employee, and on its chair where it is a desk.
        private static bool Holds(EmployeeStationController station, PlayerController player)
        {
            return IsPlayers(station) && (!(station is DesktopWorkstationController) || player.Character.isSittingOn != null);
        }

        // The player is the station's employee: read by the tag the game's Employee reads its isPlayer from, set only on the frame after.
        private static bool IsPlayers(EmployeeStationController station)
        {
            return station != null && station.employee != null && station.employee.gameObject.CompareTag("Player");
        }

        /// <summary>
        /// ...and stands up from it, as WorkActivity's Finish does: the station let go only while the player is its employee
        /// (a desk's own let-go throws with nobody at it), the pose ended, the work hold off. Each step in its own try, so a
        /// fault in one never skips the next; safe twice, and with a station the player has already left. Never throws.
        /// </summary>
        public static void StandFrom(EmployeeStationController station)
        {
            PlayerController player = Player;
            try
            {
                if (IsPlayers(station))
                {
                    station.UnassignEmployee();
                }
                if (station != null && !Taken(station))
                {
                    station.Occupied = false;
                    ScreenOff(station);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            try
            {
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
            StoodSeen(player, station != null ? station.name : "station");
        }

        /// <summary>
        /// The player sits on a desk's chair seat, held by the work hold; <paramref name="stood"/> is where to stand up. True
        /// when seated; false, with the player given back free to walk and the game's notice said, as <see cref="SitAt"/>. Never throws.
        /// </summary>
        public static bool SitOn(Transform seat, EmployeeStationController station, out Vector3 stood)
        {
            PlayerController player = Player;
            stood = player != null ? player.transform.position : Vector3.zero;
            if (player == null || seat == null)
            {
                return false;
            }
            Vector3 at = stood;
            player.SetNavigationBlocker(NavigationBlocker.WorkActivity);
            try
            {
                EnergyHelper.RemoveEnergySpender("move");
                if (station != null)
                {
                    station.Occupied = true;
                }
                player.Character.SitOnChair(seat);
                if (player.Character.isSittingOn == seat)
                {
                    ScreenOn(station);   // seated by the pose alone, so the game's employee call that lights the screens never ran
                    return true;
                }
                CareersDirector.Logger?.Warn("Careers: the player could not be seated on the " + seat.name + ": they are given back free to walk and the work does not start.");
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            StandUp(at, station);
            Refused();
            return false;
        }

        /// <summary>...and stands up beside the desk; each step in its own try, the work hold off whatever fails before it. Safe twice. Never throws.</summary>
        public static void StandUp(Vector3 at, EmployeeStationController station)
        {
            PlayerController player = Player;
            try
            {
                if (station != null && !Taken(station))
                {
                    station.Occupied = false;
                    ScreenOff(station);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            try
            {
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
            StoodSeen(player, station != null ? station.name : "desk");
        }

        // The work hold off the player, if it is on: the last step of every way they stand up, so a second stand-up is no warning of the game's.
        private static void Unhold(PlayerController player)
        {
            try
            {
                if (player != null && WorkHeld(player))
                {
                    player.UnsetNavigationBlocker(NavigationBlocker.WorkActivity);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        // True while the game holds the player's walking for work, or when its holds cannot be read (then it is taken off regardless).
        private static bool WorkHeld(PlayerController player)
        {
            return !(BlockersField?.GetValue(player) is HashSet<NavigationBlocker> blockers) || blockers.Contains(NavigationBlocker.WorkActivity);
        }

        /// <summary>True while the player is at a station as <see cref="SitAt"/> leaves them: its employee, on its chair where it is a desk, held by the work hold.</summary>
        public static bool SeatedAt(EmployeeStationController station)
        {
            PlayerController player = Player;
            return player != null && station != null && Holds(station, player) && WorkHeld(player);
        }

        /// <summary>
        /// True when the player can walk again after a desk: no work hold (where the game's holds can be read), the navigation
        /// agent on, not seated, not held still; otherwise false with what still holds them. Read on an event, never per frame.
        /// </summary>
        public static bool Walks(out string why) => Walks(Player, out why);

        private static bool Walks(PlayerController player, out string why)
        {
            ThirdPersonCharacter body = player != null ? player.Character : null;
            if (body == null)
            {
                why = "no player";
            }
            else if (BlockersField?.GetValue(player) is HashSet<NavigationBlocker> blockers && blockers.Contains(NavigationBlocker.WorkActivity))
            {
                why = "the work hold is still on";
            }
            else if (body.navmeshAgent == null || !body.navmeshAgent.enabled)
            {
                why = "the navigation agent is off";
            }
            else if (body.isSittingOn != null)
            {
                why = "still seated on " + body.isSittingOn.name;
            }
            else
            {
                why = body.isKinematic ? "held still (kinematic)" : null;
            }
            return why == null;
        }

        // With Developer diagnostics on, one line a stand-up: free to walk again, or what still holds the player.
        private static void StoodSeen(PlayerController player, string what)
        {
            if (!Meta.Dev.Verbose || player == null)
            {
                return;
            }
            bool walks = Walks(player, out string why);
            CareersDirector.Logger?.Info("Desk check (seat): stood up from the " + what + ": " + (walks ? "free to walk (no work hold, on the navigation mesh, not seated): PASS" : "FAIL, " + why));
        }

        // The game's own notice for a seat the game would not give: the work did not start.
        private static void Refused()
        {
            try
            {
                Notifications.ShowError(SeatRefusedKey, SeatRefusedKey);
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
