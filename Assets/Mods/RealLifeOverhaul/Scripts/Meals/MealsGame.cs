using System;
using System.Collections.Generic;
using BigAmbitions.InputSystem;
using BigAmbitions.Items;
using Character;
using Controllers;
using Dancing;
using Helpers;
using Player.HUD.ItemInfoOverlays;
using Player.Sound.Radio;
using PlayerActivity;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace RealLifeOverhaul.Meals
{
    /// <summary>One seat a meal may be taken to, as found when the player ate: the game's objects and where things go.</summary>
    internal sealed class MealSeat
    {
        public ItemController Item;              // the chair walked to, or the table of a table's own seat
        public SeatSpot Spot;                    // a table's own seat (as the game's customers take), or null
        public OutsideBenchController Bench;     // a street bench, or null
        public Transform Place;                  // where the body sits
        public Vector3 Stand;                    // the point on the walking mesh beside it
        public Transform Display;                // the table's own food display at that seat, or null
        public bool Top;                         // a table top was found in front of the seat
        public Vector3 PlateAt;
        public Quaternion PlateTurn;
        public SeatOption Option;

        public void Clear()
        {
            Item = null;
            Spot = null;
            Bench = null;
            Place = null;
            Display = null;
            Top = false;
            Option = default;
        }

        public string Kind => Spot != null ? "a table's own seat (" + Item.itemName + ")" : Bench != null ? "a street bench" : "a seat (" + Item.itemName + ")" + (Top ? " at a table" : string.Empty);
    }

    /// <summary>
    /// The only place meals and music touch the game, one small method per read (Mono compiles a method naming a game
    /// member whole). Nothing here runs per frame but <see cref="MovePressed"/>, and only while the player is seated at a
    /// meal; the seat search runs once, when the player has eaten, over the building's own list of its furniture (or one
    /// physics query for a street bench), with one walking path for each of the few nearest seats.
    /// </summary>
    internal static class MealsGame
    {
        private const float StreetReach = 9f;   // m: the street bench a meal outside is taken to
        private const int MostSeats = 24;       // seats kept from one search, the nearest

        private static readonly MealSeat[] Seats = MakeSeats();
        private static readonly SeatOption[] Options = new SeatOption[MostSeats];
        private static readonly HashSet<ItemController> AtTables = new HashSet<ItemController>();
        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private static readonly Vector3[] Corners = new Vector3[32];
        private static NavMeshPath _path;
        private static UnityAction _arrived;
        private static UnityAction _moved;
        private static UnityAction _blocked;

        private static MealSeat[] MakeSeats()
        {
            var seats = new MealSeat[MostSeats];
            for (int i = 0; i < seats.Length; i++)
            {
                seats[i] = new MealSeat();
            }
            return seats;
        }

        /// <summary>The local player, never another player's; null outside a city.</summary>
        public static PlayerController Player()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return gm != null ? gm.playerController : null;
        }

        public static bool Loading()
        {
            return UI.Load.LoadScene.isLoading || !ModState.Loaded || SaveGameManager.Current == null;
        }

        public static float Hunger()
        {
            return SaveGameManager.Current != null ? SaveGameManager.Current.Hunger : 0f;
        }

        public static bool Inside => BuildingManager.IsInsideBuilding;

        /// <summary>The building the player is in, or null outdoors.</summary>
        public static BuildingRegistration Building()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return BuildingManager.IsInsideBuilding && manager != null ? manager.buildingRegistration : null;
        }

        /// <summary>Whether the player is free for the mod to walk and seat them: on foot, not seated, nothing of the game's or the mod's holding them.</summary>
        public static bool Free(PlayerController player)
        {
            if (player == null || player.Character == null || player.NavigationDisabled || player.Character.isSittingOn != null || PlayerHelper.IsUsingVehicle
                || RagdollSystem.Holds(player.Character) || EnergyHelper.goingToHospital || GameManager.IsAnyMiniGameActive())
            {
                return false;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis == null || uis.timeMachine == null || !uis.timeMachine.isRunning;
        }

        /// <summary>Empty hands, or a shopping bag, which the game's own seats let the player keep (anything else in the hands is put down first, as the game asks).</summary>
        public static bool HandsFit()
        {
            return !PlayerHelper.IsHoldingItem || PlayerHelper.IsHoldingBag;
        }

        /// <summary>A screen of the game's or the mod's that the player is in: its activity panel, a question, the menus, the map.</summary>
        public static bool ScreenUp()
        {
            return PlayerActivityUI.IsPanelOpen || Wiring.Scenes.PlayerHeld;
        }

        /// <summary>The game's own bite of what was eaten has played out (TryToConsume plays it on the animator's layer 7).</summary>
        public static bool BiteOver(PlayerController player)
        {
            Animator animator = player != null && player.Character != null ? player.Character.animator : null;
            return animator == null || animator.layerCount <= 7 || animator.GetCurrentAnimatorStateInfo(7).IsName("New State");
        }

        /// <summary>A movement key held, or the stick pushed: the player wants to be off.</summary>
        public static bool MovePressed()
        {
            return PlayerAction.Move.Vector().sqrMagnitude >= 0.05f;
        }

        /// <summary>
        /// The director's three answers to the game's walk (arrived, walked elsewhere, a click while seated), each made once
        /// through the guard and kept, so that the one put on the game's events is the one taken off.
        /// </summary>
        public static void Answers(Action arrived, Action moved, Action blocked)
        {
            _arrived = Guard.Listener(() => arrived());
            _moved = Guard.Listener(() => moved());
            _blocked = Guard.Listener(() => blocked());
        }

        public static void Listen(PlayerController player)
        {
            player.PlayerChangedNavigation.AddListener(_moved);
            player.PlayerNavigationWhileDisabled.AddListener(_blocked);
        }

        public static void Unlisten(PlayerController player)
        {
            if (player == null)
            {
                return;
            }
            player.PlayerChangedNavigation.RemoveListener(_moved);
            player.PlayerNavigationWhileDisabled.RemoveListener(_blocked);
        }

        /// <summary>Sends the player walking to a point with the game's own walk order; false if the game would not take it.</summary>
        public static bool WalkTo(PlayerController player, Vector3 to)
        {
            player.SetGoal(to, _arrived);
            return player.hasOnGoalReachedAction;
        }

        public static void StopWalk(PlayerController player)
        {
            if (player != null && player.Character != null)
            {
                player.ResetNavigation();
            }
        }

        // ---------------------------------------------------------------- the seats

        /// <summary>
        /// The seat a meal is taken to from where the player stands, or null: the building's own furniture indoors (the game's
        /// table seats as its customers take them, and its other seats), a street bench outdoors; then
        /// <see cref="MealsRules.Pick"/>. Run once, when the player has eaten. <paramref name="looked"/> says how many were measured.
        /// </summary>
        public static MealSeat Find(PlayerController player, out int looked)
        {
            looked = 0;
            Vector3 from = player.transform.position;
            int count = BuildingManager.IsInsideBuilding ? Indoors(from) : Street(from);
            Nearest(count);
            int measured = count < MealsRules.MostLooked ? count : MealsRules.MostLooked;
            for (int i = 0; i < measured; i++)
            {
                MealSeat seat = Seats[i];
                seat.Option.Path = Walk(player, seat.Stand);
                if (seat.Spot == null && seat.Bench == null)
                {
                    TableAhead(seat, player);
                    seat.Option.Table = seat.Top;
                }
                Options[i] = seat.Option;
            }
            looked = measured;
            int best = MealsRules.Pick(Options, measured);
            return best >= 0 ? Seats[best] : null;
        }

        // The building's own list of its furniture: a table's own free seats first (and the chairs that belong to them), then other seats.
        private static int Indoors(Vector3 from)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            List<ItemController> items = manager != null ? manager.allItemControllers : null;
            AtTables.Clear();
            int count = 0;
            if (items == null)
            {
                return 0;
            }
            float reach = (MealsRules.Reach + 2f) * (MealsRules.Reach + 2f);
            for (int i = 0; i < items.Count && count < MostSeats; i++)
            {
                ItemController table = items[i];
                if (table == null || !table.isActiveAndEnabled || table.Item == null || (table.Item.type & ItemType.Table) == 0 || table.SeatSpots == null
                    || CitySpots.Flat(table.transform.position - from) > reach)
                {
                    continue;
                }
                SeatSpot[] spots = table.SeatSpots;
                for (int s = 0; s < spots.Length && count < MostSeats; s++)
                {
                    SeatSpot spot = spots[s];
                    if (spot == null || spot.seatAttachmentPoint == null)
                    {
                        continue;
                    }
                    ItemController chair = spot.GetAttachedChair;
                    if (chair != null)
                    {
                        AtTables.Add(chair);
                    }
                    Transform place = spot.SeatTransform;
                    if (!spot.IsAvailable || place == null || (chair != null && chair.Occupied))
                    {
                        continue;
                    }
                    MealSeat seat = Seats[count++];
                    seat.Clear();
                    seat.Spot = spot;
                    seat.Item = chair != null ? chair : table;
                    seat.Place = place;
                    seat.Display = spot.seatAttachmentPoint.foodDisplay;
                    seat.Top = true;
                    if (seat.Display != null)
                    {
                        seat.PlateAt = seat.Display.position;
                        seat.PlateTurn = Quaternion.LookRotation(Flat(place.forward));
                    }
                    else
                    {
                        seat.Top = false;
                    }
                    Measure(seat, from, true);
                }
            }
            for (int i = 0; i < items.Count && count < MostSeats; i++)
            {
                ItemController item = items[i];
                if (item == null || AtTables.Contains(item) || !Seating.IsSeat(item) || !item.isActiveAndEnabled || CitySpots.Flat(item.transform.position - from) > reach)
                {
                    continue;
                }
                Transform place = Seating.Beside(item, from);
                if (place == null)
                {
                    continue;
                }
                MealSeat seat = Seats[count++];
                seat.Clear();
                seat.Item = item;
                seat.Place = place;
                Measure(seat, from, false);
            }
            return count;
        }

        // A street bench within reach, its own place free (the game seats a pedestrian on its other place).
        private static int Street(Vector3 from)
        {
            OutsideBenchController bench = Seating.Bench(from, StreetReach);
            if (bench == null || bench.sittingPosition == null || bench.sittingPosition.childCount > 0)
            {
                return 0;
            }
            MealSeat seat = Seats[0];
            seat.Clear();
            seat.Bench = bench;
            seat.Place = bench.sittingPosition;
            Vector3 stand = bench.sittingPosition.position + Flat(bench.sittingPosition.forward) * 0.45f;
            seat.Stand = NavMesh.SamplePosition(stand, out NavMeshHit hit, 1f, NavMesh.AllAreas) ? hit.position : stand;
            seat.Option = new SeatOption { Flat = Mathf.Sqrt(CitySpots.Flat(seat.Place.position - from)), Up = seat.Place.position.y - from.y, Free = true };
            return 1;
        }

        private static void Measure(MealSeat seat, Vector3 from, bool table)
        {
            seat.Stand = seat.Item.GetClosestNavMeshTargetPosition(seat.Place.position);
            seat.Option = new SeatOption
            {
                Flat = Mathf.Sqrt(CitySpots.Flat(seat.Place.position - from)), Up = seat.Place.position.y - from.y, Path = -1f, Table = table, Free = true
            };
        }

        // The nearest first, by the straight distance: a short insertion sort over a handful.
        private static void Nearest(int count)
        {
            for (int i = 1; i < count; i++)
            {
                MealSeat seat = Seats[i];
                int j = i - 1;
                while (j >= 0 && Seats[j].Option.Flat > seat.Option.Flat)
                {
                    Seats[j + 1] = Seats[j];
                    j--;
                }
                Seats[j + 1] = seat;
            }
        }

        // The walk there over the game's walking mesh, in metres; -1 with no way there.
        private static float Walk(PlayerController player, Vector3 to)
        {
            if (to == Vector3.zero)
            {
                return -1f;
            }
            if (_path == null)
            {
                _path = new NavMeshPath();   // made once and used again
            }
            if (!NavMesh.CalculatePath(player.transform.position, to, NavMesh.AllAreas, _path) || _path.status != NavMeshPathStatus.PathComplete)
            {
                return -1f;
            }
            int corners = _path.GetCornersNonAlloc(Corners);
            float length = 0f;
            for (int i = 1; i < corners; i++)
            {
                length += Vector3.Distance(Corners[i - 1], Corners[i]);
            }
            return length;
        }

        // A table top in front of a plain seat, by one ray down onto the spot the plate would take.
        private static void TableAhead(MealSeat seat, PlayerController player)
        {
            Vector3 ahead = Flat(seat.Place.forward);
            Vector3 spot = seat.Place.position + ahead * MealsRules.PlateAhead;
            Vector3 origin = spot + Vector3.up * (MealsRules.TableHigh + 0.4f);
            int found = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, MealsRules.TableHigh + 0.4f - 0.2f, ~0, QueryTriggerInteraction.Ignore);
            float top = -1f;
            Transform body = player != null ? player.transform : null;
            for (int i = 0; i < found; i++)
            {
                RaycastHit hit = Hits[i];
                Hits[i] = default;
                if (hit.collider == null || (body != null && hit.collider.transform.IsChildOf(body)) || hit.collider.transform.IsChildOf(seat.Item.transform))
                {
                    continue;
                }
                float height = hit.point.y - seat.Place.position.y;
                if (MealsRules.TableTop(height) && height > top)
                {
                    top = height;
                }
            }
            seat.Top = top > 0f;
            if (seat.Top)
            {
                seat.PlateAt = new Vector3(spot.x, seat.Place.position.y + top + 0.002f, spot.z);
                seat.PlateTurn = Quaternion.LookRotation(ahead);
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        /// <summary>Still free on arrival: nobody took it while the player walked over.</summary>
        public static bool StillFree(MealSeat seat)
        {
            if (seat.Bench != null)
            {
                return seat.Bench.isActiveAndEnabled && seat.Place != null && seat.Place.childCount == 0;
            }
            if (seat.Item == null || seat.Place == null)
            {
                return false;
            }
            if (seat.Spot != null)
            {
                return true;   // kept for the player since the walk began (Reserve)
            }
            var chair = seat.Item as SeatController;
            return chair != null && chair.isActiveAndEnabled && chair.IsSittingPositionAvailable(seat.Place);
        }

        /// <summary>A table's own seat kept for the player as the game's customers keep theirs, so that none takes it meanwhile.</summary>
        public static void Reserve(MealSeat seat, bool keep)
        {
            if (seat.Spot != null)
            {
                seat.Spot.occupied = keep;
                if (seat.Item != null && seat.Item.Item != null && (seat.Item.Item.type & ItemType.Seat) != 0)
                {
                    seat.Item.Occupied = keep;   // the chair at it, as the game's customers mark theirs (never the table itself)
                }
            }
        }

        /// <summary>The street bench's other place kept empty while the player sits there (the game would seat a pedestrian on it); its own setting given back after.</summary>
        public static bool Bench(MealSeat seat, bool keep, bool before)
        {
            if (seat.Bench == null)
            {
                return before;
            }
            bool was = seat.Bench.spawnPedestrians;
            seat.Bench.spawnPedestrians = keep ? false : before;
            return was;
        }

        /// <summary>The table's own food display at the seat, as the game shows a customer's meal: the tray and one dish on it. False where there is none to show.</summary>
        public static bool ShowDisplay(MealSeat seat, bool show, ref Transform shown)
        {
            Transform display = seat.Display;
            if (display == null)
            {
                return false;
            }
            if (!show)
            {
                if (shown != null)
                {
                    shown.gameObject.SetActive(false);
                }
                display.gameObject.SetActive(false);
                shown = null;
                return true;
            }
            Transform dish = null;
            for (int i = 0; i < display.childCount && dish == null; i++)
            {
                Transform child = display.GetChild(i);
                string name = child.name;
                if (!name.Equals("sodacan", System.StringComparison.OrdinalIgnoreCase) && !name.Equals("cupofcoffee", System.StringComparison.OrdinalIgnoreCase))
                {
                    dish = child;
                }
            }
            if (dish == null)
            {
                return false;
            }
            display.gameObject.SetActive(true);
            dish.gameObject.SetActive(true);
            shown = dish;
            return true;
        }

        // ---------------------------------------------------------------- the music

        /// <summary>Music is playing from the building's speakers: the game's speakers are on, not muted, with a song and the building's volume up.</summary>
        public static bool MusicPlaying()
        {
            BuildingRegistration reg = Building();
            if (reg == null || InstanceBehavior<LoudSpeakersManager>.Instance == null)
            {
                return false;
            }
            return !LoudSpeakersManager.IsMuted && !string.IsNullOrEmpty(LoudSpeakersManager.SongName) && reg.GetBusinessRadioVolume() > 0f;
        }

        /// <summary>The building is the player's own (a home or a business they rent), never another player's in a shared city.</summary>
        public static bool OwnPlace()
        {
            BuildingRegistration reg = Building();
            return reg != null && reg.RentedByPlayer && GoingPublic.Mine(reg);
        }

        /// <summary>A loudspeaker of the building near the player, or null: for the test, read off the building's own list.</summary>
        public static ItemController Speaker(Vector3 near, float within)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            List<ItemController> items = manager != null ? manager.allItemControllers : null;
            ItemController best = null;
            float bestSqr = within * within;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is LoudspeakerController speaker && speaker.isActiveAndEnabled)
                {
                    float sqr = CitySpots.Flat(speaker.transform.position - near);
                    if (sqr <= bestSqr)
                    {
                        bestSqr = sqr;
                        best = speaker;
                    }
                }
            }
            return best;
        }

        /// <summary>The game lets the player dance now (its own dance button is open: on foot, hands empty, free to move).</summary>
        public static bool CanDance()
        {
            return PlayerDances.IsEnabled;
        }

        /// <summary>Too tired to dance: the game's own refusal for its dance list.</summary>
        public static bool TooTired(PlayerController player)
        {
            return player.Character.walkingSpeed == ThirdPersonCharacter.WalkingSpeed.Zombie;
        }

        /// <summary>One of the game's own dances, started the way its dance list starts one (it spends energy as the game's dancing does).</summary>
        public static void StartDance(PlayerController player, int dance)
        {
            player.ResetNavigation();
            PlayerDances.StartDancing((DanceType)dance);
        }

        public static void StopDance()
        {
            PlayerDances.StopDancing();
        }

        /// <summary>The player is dancing: the game's own "Dancing" switch on their animator.</summary>
        public static bool Dancing(PlayerController player)
        {
            Animator animator = player != null && player.Character != null ? player.Character.animator : null;
            return animator != null && animator.GetBool("Dancing");
        }

        public static void InsufficientEnergy()
        {
            Notifications.ShowInsufficientEnergy();
        }

        public static void HidePanel()
        {
            OverlayManager overlays = InstanceBehavior<OverlayManager>.Instance;
            if (overlays != null)
            {
                overlays.HideDetailedOverlay();   // as the game's own buttons of the panel do
            }
        }

        /// <summary>A notice of the game's, kept out of the save.</summary>
        public static void Notice(NotificationType type, string key)
        {
            Notifications.Show(type, key, null, 6f, null, null, notificationSound: true, trackOnSaveGame: false);
        }

        public static float NowMinutes()
        {
            return TimeHelper.NowInMinutes();
        }
    }
}
