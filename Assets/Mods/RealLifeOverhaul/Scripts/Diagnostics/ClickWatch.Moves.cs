using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BigAmbitions.Items;
using BigAmbitions.PlacementSystem;
using BigAmbitions.Tags;
using Controllers;
using Helpers;
using PlayerActivity;
using UI;
using UI.InteriorDesigner;
using UI.MiniMenu;
using UI.Purchase;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The click check's lines for moving furniture, for "I cannot move it" and "I click to put it down and nothing
    /// happens". A right click on a piece says whether the game lifts it, and if not its own first reason (FurnitureRules), with
    /// what makes it count as in use. A left click while a piece is being placed says whether the game puts it down there,
    /// and if not why, naming what stands in the way and whose it is. When a move ends, the line says where the piece is
    /// and whether the building's own record of it, which the save keeps, says the same. It reads and changes nothing.
    /// Cost: with Developer diagnostics off, nothing; on, one field read a frame, and a line (with one placement check of
    /// the game's own) for a click.
    /// </summary>
    public sealed partial class ClickWatch
    {
        private const string ResidentialType = "ba:buildingtype_residential";

        private static string _ownAssembly;
        private static FieldInfo _places;
        private static bool _placesLooked;

        private Vector2 _pressedAt;         // where the left button went down, for the game's own limit on a place click
        private string _movingId;           // the record of the piece being placed, while a move is watched; null when none is
        private ItemController _moving;
        private BuildingRegistration _movingRoom;   // the building's registration as the move began: a load makes it anew, and then its records are not the ones the move changed
        private string _movingName;
        private Vector3 _movingFrom;        // where it stood as its move began
        private int _endedFrame = -1;       // the frame the move was seen to end in, while its line waits a frame (FurnitureRules.Settled); -1 when none waits

        // The walls, furniture, people, vehicles and unplaceable ground a piece on the floor may not overlap (PlacementSystem.CollisionCheckMask).
        private static int FloorMask => LayerHelper.wallsLayerMask | LayerHelper.interactiveItemsAndOutlinedLayerMask | LayerHelper.humanLayerMask
                                         | LayerHelper.vehiclesMask | LayerHelper.groundUnplacableLayerMask;

        // What a piece on a wall may not overlap (PlacementSystem.UpdateWallItemPosition).
        private static int WallMask => LayerHelper.groundLayerMask | LayerHelper.interactiveItemsAndOutlinedLayerMask | LayerHelper.humanLayerMask | LayerHelper.wallsLayerMask;

        // A move of the game's begun or ended since the last frame: the piece noted as it starts, its line written a frame after it ends.
        private void WatchMove()
        {
            if (_endedFrame >= 0 && FurnitureRules.Settled(Time.frameCount, _endedFrame))
            {
                WriteEnd();   // a piece the game took into the hands is destroyed at the end of the frame it was grabbed in: gone by now, its record taken with it
            }
            if (PlacementSystem.IsInPlacementMode)
            {
                if (_movingId == null)
                {
                    // The interior designer keeps a moved piece's place only once its own animation has played: its moves are not watched.
                    _moving = InteriorDesignerUI.IsOpen ? null : PlacementSystem.CurrentPlaceableItemBeingPlaced as ItemController;
                    _movingId = _moving != null && _moving.ItemInstance != null ? _moving.ItemInstance.id ?? string.Empty : string.Empty;
                    _movingName = _moving != null ? _moving.itemName : string.Empty;
                    _movingFrom = PlacementSystem.initialPlacementPosition;
                    BuildingManager began = InstanceBehavior<BuildingManager>.Instance;
                    _movingRoom = began != null && BuildingManager.IsInsideBuilding ? began.buildingRegistration : null;
                }
                return;
            }
            if (_movingId != null && _endedFrame < 0)
            {
                _endedFrame = Time.frameCount;
            }
        }

        // The move has ended (put down, given back, or into the hands or a box): where the piece is, and the building's record of it.
        private void WriteEnd()
        {
            string id = _movingId;
            ItemController piece = _moving;
            BuildingRegistration room = _movingRoom;
            _movingId = null;
            _moving = null;
            _movingRoom = null;
            _endedFrame = -1;
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration reg = manager != null && BuildingManager.IsInsideBuilding ? manager.buildingRegistration : null;
            if (reg == null || reg.itemInstances == null || id.Length == 0)
            {
                return;
            }
            _text.Length = 0;
            _text.Append("Move check (ended): '").Append(_movingName).Append("' ");
            bool kept = reg.itemInstances.TryGetValue(id, out ItemInstance record);
            bool gone = piece == null;
            Vector3 at = gone ? Vector3.zero : piece.transform.position;
            var signals = new MoveEndSignals
            {
                PieceGone = gone,
                RoomChanged = room != null && !ReferenceEquals(room, reg),
                Listed = kept,
                SameRecord = kept && !gone && ReferenceEquals(record, piece.ItemInstance),
                SamePlace = kept && !gone && ((Vector3)record.position - at).sqrMagnitude <= 0.0001f,
            };
            MoveEnd end = FurnitureRules.Ending(signals);
            if (end == MoveEnd.NotJudged)
            {
                _text.Append("ended in another room than it began in: ").Append(FurnitureRules.Words(end, null));
                Logger?.Info(_text.ToString());
                return;
            }
            if (gone)
            {
                _text.Append("is gone from the room (").Append(WentTo(id, _movingName)).Append("); the building ").Append(FurnitureRules.Words(end, null));
                Logger?.Info(_text.ToString());
                return;
            }
            _text.Append((at - _movingFrom).sqrMagnitude > 0.0001f ? "was put down at " : "is back where it was, ").Append(Where(at))
                 .Append("; the building's own record of it, which the save keeps: ").Append(FurnitureRules.Words(end, kept ? Where(record.position) : null));
            Logger?.Info(_text.ToString());
        }

        // Where a piece the game took out of the room is: the player's hands hold it as the item itself (a box) or as cargo of its name.
        private static string WentTo(string id, string name)
        {
            ItemInstance hands = PlayerHelper.ItemInstanceInHands;
            if (hands == null)
            {
                return "not in the player's hands: a box, a vehicle or a sale";
            }
            if (hands.id == id)
            {
                return "now in the player's hands";
            }
            var cargo = hands.cargoInstances;
            for (int i = 0; cargo != null && i < cargo.Count; i++)
            {
                if (cargo[i] != null && cargo[i].itemName == name)
                {
                    return "now in the player's hands, among the cargo of '" + hands.itemName + "'";
                }
            }
            return "not in the player's hands: a box, a vehicle or a sale";
        }

        // A right click: would the game lift the piece the pointer is on, and if not, why.
        private void WriteMove()
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            EntityController target = MouseController.currentTargetEntity;
            if (game == null || game.playerController == null || manager == null || !BuildingManager.IsInsideBuilding)
            {
                return;
            }
            bool placing = PlacementSystem.IsInPlacementMode;
            if (placing && !ReferenceEquals(PlacementSystem.CurrentPlaceableItemBeingPlaced, target))
            {
                return;   // a right click while placing turns the piece
            }
            _text.Length = 0;
            _text.Append("Move check (right click): ");
            var item = target as ItemController;
            if (item == null)
            {
                EventSystem events = EventSystem.current;
                _text.Append(target != null ? "on " + target.GetType().Name + " '" + target.name + "', not a piece of furniture" : "on nothing the game can pick")
                     .Append("; over the interface: ").Append(events != null && events.IsPointerOverGameObject() ? "YES" : "no").Append('.');
                Logger?.Info(_text.ToString());
                return;
            }
            Piece(item, manager);
            if (placing)
            {
                _text.Append(": the game lifted it: PASS.");
                Logger?.Info(_text.ToString());
                return;
            }
            MoveRefusal refusal = FurnitureRules.First(Signals(item, manager, game.playerController));
            _text.Append(": ").Append(FurnitureRules.Words(refusal));
            if (refusal == MoveRefusal.InUse)
            {
                _text.Append(" (").Append(InUse(item)).Append(')');
            }
            _text.Append(refusal == MoveRefusal.None ? ": PASS." : ".");
            Logger?.Info(_text.ToString());
        }

        // A left click while a piece is being placed: would the game put it down where the pointer is, and if not, why.
        private void WritePlace()
        {
            IPlaceableItem moving = PlacementSystem.CurrentPlaceableItemBeingPlaced;
            var item = moving as ItemController;
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            Camera camera = GameManager.GetMainCamera();
            Item kind = item != null && item.ItemInstance != null ? item.ItemInstance.ItemCached : null;
            if (item == null || kind == null || manager == null || camera == null)
            {
                return;   // put down already in this frame: the end of the move says so
            }
            Vector2 at = Input.mousePosition;
            bool wall = kind.wallMounted;
            bool floor = Physics.Raycast(camera.ScreenPointToRay(at), out RaycastHit hit, 100f, wall ? LayerHelper.wallPlacementLayerMask : LayerHelper.groundLayerMask);
            SizeOrientedBounds plot = PlacementSystem.groundBounds;
            bool fits = (kind.type & ItemType.FlatDecoration) != 0 || (wall ? moving.IsWallPlacementValid(WallMask) : moving.IsGroundPlacementValid(FloorMask));
            Collider blocker = fits ? null : PlacementValidation.CurrentOverlappingCollider;
            EventSystem events = EventSystem.current;
            var signals = new PlaceSignals
            {
                InputSelected = GameManager.HasInputSelected(),
                OverInterface = events != null && events.IsPointerOverGameObject(),
                Floor = floor,
                InsidePlot = wall || plot == null || (floor && plot.Contains(hit.point)),
                Blocked = PlacementSystem.placedInBlockedArea,
                Fits = fits,
                Moved = FurnitureRules.MovedTooFar(at.x - _pressedAt.x, at.y - _pressedAt.y, Screen.height),
            };
            PlaceRefusal refusal = FurnitureRules.First(signals);
            _text.Length = 0;
            _text.Append("Move check (place click): ");
            Piece(item, manager);
            _text.Append(": ").Append(FurnitureRules.Words(refusal));
            if (refusal == PlaceRefusal.InputSelected && events != null && events.currentSelectedGameObject != null)
            {
                _text.Append(" ('").Append(Path(events.currentSelectedGameObject.transform)).Append("')");
            }
            else if (refusal == PlaceRefusal.NoFit)
            {
                _text.Append(": ");
                Blocker(blocker, item, manager);
            }
            _text.Append(refusal == PlaceRefusal.None ? ": PASS." : ".");
            Logger?.Info(_text.ToString());
        }

        // What the game's right click asks of the piece (ItemController.SecondaryInteract and PlacementHelper.StartPlacementMode).
        private static MoveSignals Signals(ItemController item, BuildingManager manager, PlayerController player)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Buildings.Building building = manager.building;
            bool editable = BuildingManager.isBuildingTemporarilyEditable;
            Item kind = item.ItemInstance != null ? item.ItemInstance.ItemCached : null;
            VehicleController vehicle = PlayerHelper.IsUsingVehicle ? VehicleHelper.GetCurrentVehicleBase() : null;
            return new MoveSignals
            {
                Hidden = !item.visible,
                Activity = uis != null && uis.playerActivityUI != null && uis.playerActivityUI.GetCurrentActivity != null,
                Inside = BuildingManager.IsInsideBuilding,
                AtTheDoor = manager.enteringBuilding || manager.exitingBuilding,
                Yours = manager.buildingRegistration != null && manager.buildingRegistration.RentedByPlayer,
                Editable = editable,
                InUse = item.Occupied,
                Placing = PlacementSystem.IsInPlacementMode,
                Buying = PurchaseUI.IsPanelOpen,
                ToHospital = EnergyHelper.goingToHospital,
                Previewing = BuildingPreview.isPreviewing,
                Designer = InteriorDesignerUI.IsOpen,
                ActivityPanel = PlayerActivityUI.IsPanelOpen,
                SmallMenu = MiniMenu.IsOpen,
                Using = player.Character != null && ReferenceEquals(player.Character.CurrentEntityController, item),
                Vehicle = vehicle != null && vehicle.vehicleType != null && !vehicle.vehicleType.spawnInPlayerObject,
                Structure = item.IsPartOfTheStructure(),
                Residential = !editable && !GameManager.IsDevMode && kind != null && kind.HasTag(TagRef.Itemtag.cannotmoveinresidential) && building != null
                              && building.BuildingType == ResidentialType && !building.IsHamptonsHouse(),
            };
        }

        // What makes the game count a piece as in use (ItemController.Occupied, SeatController.Occupied): the first part of its sum that holds.
        private static string InUse(ItemController item)
        {
            Transform[] places = Places(item);
            if (item is SeatController seat && places != null)
            {
                for (int i = 0; i < places.Length; i++)
                {
                    Transform place = places[i];
                    if (place != null && place.childCount == 0 && !seat.IsSittingPositionAvailable(place))
                    {
                        return "somebody sits on it, at '" + place.name + "'";
                    }
                }
            }
            List<ItemController> children = item.childItemControllers;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (children[i] != null && children[i].Occupied)
                {
                    return "'" + children[i].itemName + "' on it is in use";
                }
            }
            if (places != null && places.Length > 0)
            {
                Transform holding = null;
                bool all = true;
                for (int i = 0; i < places.Length; i++)
                {
                    bool holds = places[i] != null && places[i].childCount > 0;
                    all &= holds;
                    holding = holding == null && holds ? places[i] : holding;
                }
                if (all && holding != null)
                {
                    return "each of its places to sit holds something ('" + holding.GetChild(0).name + "' under '" + holding.name + "')";
                }
            }
            SeatSpot[] spots = item.SeatSpots;
            for (int i = 0; spots != null && i < spots.Length; i++)
            {
                if (spots[i] != null && spots[i].occupied)
                {
                    return "seat " + (i + 1) + " of " + spots.Length + " at it is kept for somebody";
                }
            }
            if (item is SeatController && SeatController.IsInCinemaOrTheater())
            {
                return "a seat of a cinema or theatre that is taken";
            }
            return "its own in-use mark is set, which whoever uses it sets: a customer, a worker, an activity, a meal";
        }

        // ItemController.sittingPositions, read through the game contract; null where the piece has none or the game no longer has the field.
        private static Transform[] Places(ItemController item)
        {
            if (!_placesLooked)
            {
                _placesLooked = true;
                _places = GameContract.SittingPositions.Field;
            }
            try
            {
                return _places != null ? _places.GetValue(item) as Transform[] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The piece: its name, where it stands, whether the building lists it and whether its record is the building's own.
        private void Piece(ItemController item, BuildingManager manager)
        {
            List<ItemController> listed = manager.allItemControllers;
            _text.Append('\'').Append(item.name).Append("' (").Append(item.itemName).Append(") at ").Append(Where(item.transform.position)).Append(", ")
                 .Append(listed != null && listed.Contains(item) ? "in the building's list of its furniture"
                     : "NOT in the building's list of its furniture (the game adds a piece put down from the hands to that list only when it next loads the building; its record is kept at once)")
                 .Append(item.loadedInHamptonsHouse ? ", loaded with the house" : string.Empty).Append(", ").Append(Record(item, manager.buildingRegistration));
        }

        // Whether the building's own record of the piece, which the save keeps, is the one the piece holds.
        private static string Record(ItemController item, BuildingRegistration reg)
        {
            ItemInstance mine = item.ItemInstance;
            if (mine == null || reg == null || reg.itemInstances == null)
            {
                return "no record of its own";
            }
            if (!reg.itemInstances.TryGetValue(mine.id ?? string.Empty, out ItemInstance kept))
            {
                return "the building has NO RECORD of it";
            }
            return ReferenceEquals(kept, mine) ? "its record the building's own" : "the building keeps ANOTHER RECORD of it";
        }

        // What stands in the way of a piece being put down, and whose it is by the script on it or above it.
        private void Blocker(Collider c, ItemController moving, BuildingManager manager)
        {
            if (c == null)
            {
                _text.Append("nothing the game names stands in the way (a piece for a wall may have no wall behind it)");
                return;
            }
            _text.Append('\'').Append(c.name).Append("' (layer ").Append(LayerMask.LayerToName(c.gameObject.layer)).Append(c.isTrigger ? ", a trigger" : string.Empty).Append(')');
            ItemController piece = c.GetComponentInParent<ItemController>();
            if (piece != null)
            {
                List<ItemController> listed = manager.allItemControllers;
                _text.Append(ReferenceEquals(piece, moving) ? ", part of the piece itself" : ", part of the furniture '" + piece.itemName + "'")
                     .Append(listed != null && listed.Contains(piece) ? string.Empty : ", which the building does not list");
            }
            MonoBehaviour script = c.GetComponentInParent<MonoBehaviour>();
            Assembly assembly = script != null ? script.GetType().Assembly : null;
            string name = assembly != null ? assembly.GetName().Name : null;
            MoveOwner owner = FurnitureRules.Owner(name, Location(assembly), OwnAssembly, NamedByThisMod(c.transform));
            _text.Append(", ").Append(FurnitureRules.Words(owner, name)).Append(script != null ? " " + script.GetType().Name : string.Empty).Append(", at '").Append(Path(c.transform)).Append('\'');
        }

        private static string OwnAssembly => _ownAssembly ?? (_ownAssembly = typeof(ClickWatch).Assembly.GetName().Name);

        // Where an assembly was loaded from; empty for one made in memory (a mod loaded from its bytes, a patch).
        private static string Location(Assembly assembly)
        {
            try
            {
                return assembly != null ? assembly.Location : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // The mod names what it makes "RealLife ..." (a desk's pointer, the player's double): one of those on the thing or above it.
        private static bool NamedByThisMod(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name.StartsWith("RealLife", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string Where(Vector3 at)
        {
            return "(" + at.x.ToString("F1", CultureInfo.InvariantCulture) + ", " + at.y.ToString("F1", CultureInfo.InvariantCulture) + ", "
                   + at.z.ToString("F1", CultureInfo.InvariantCulture) + ")";
        }
    }
}
