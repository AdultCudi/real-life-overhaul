using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.SoundSystem;
using Cinemachine;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// Everything the showroom's floor, camera and bays do to the game: the game's own camera for looking at a car, with the
    /// walk, pointer, zoom and clock held while it is up and each given back as it was. Nothing per frame by itself.
    /// </summary>
    internal static class ShowroomGame
    {
        private const float Lift = 0.6f;            // metres over the floor a car's middle is taken to be
        private const string AimName = "RealLifeOverhaul Showroom aim";

        private static Transform _aim;              // what the camera looks at: an object of the mod's, made once a city
        private static CinemachineVirtualCameraBase _cam;
        private static Transform _hadLookAt;        // the camera's own aim, place and turn, and the zoom block, to put back
        private static Vector3 _hadAt;
        private static Quaternion _hadTurn;
        private static bool _hadZoomBlock;
        private static bool _taken;
        private static bool _blocked, _zoomWasBlocked, _paused, _timeHeld;
        private static readonly Plane Floor = new Plane(Vector3.up, Vector3.zero);
        private static readonly List<int> Layers = new List<int>(8);
        private static Renderer[] _outlined;

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The save's hour, as the showroom counts hours (day x 24 + hour).</summary>
        public static int Hour => Save != null ? Save.Day * 24 + Save.Hour : 0;

        /// <summary>The minute of the hour (0 to 59).</summary>
        public static int Minute => Save != null ? Mathf.Clamp((int)Save.Minute, 0, 59) : 0;

        /// <summary>The middle of the showroom's bays, on the floor. False if it has none.</summary>
        public static bool FloorMiddle(DealerYard y, out Vector3 middle)
        {
            middle = Vector3.zero;
            if (y == null || y.Spots.Count == 0)
            {
                return false;
            }
            long x = 0, z = 0, h = 0;
            for (int s = 0; s < y.Spots.Count; s++)
            {
                x += y.Spots[s].X;
                z += y.Spots[s].Z;
                h += y.Spots[s].Y;
            }
            int n = y.Spots.Count;
            middle = new Vector3(x / (float)n / 100f, h / (float)n / 100f, z / (float)n / 100f);
            return true;
        }

        public static Vector3 FloorMiddleOf(DealerYard y)
        {
            FloorMiddle(y, out Vector3 middle);
            return middle;
        }

        /// <summary>Where the salesperson waits: in front of the showroom's cash register nearest the middle. False if it has none.</summary>
        public static bool Desk(Vector3 near, out Vector3 spot)
        {
            spot = default;
            BuildingManager building = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            List<ItemController> items = building != null ? building.allItemControllers : null;
            float best = float.MaxValue;
            ItemController desk = null;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item == null || item.Item == null || (item.Item.type & ItemType.PointOfSale) == 0)
                {
                    continue;
                }
                float sqr = (item.transform.position - near).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    desk = item;
                }
            }
            return desk != null && ShopFloor.Standable(desk.transform.position + desk.transform.forward * 1.2f, 2f, out spot);
        }

        /// <summary>The middle of a car of the lot, by its place in the lot's list, as it stands on its bay.</summary>
        public static Vector3 CarMiddle(DealerYard y, int car)
        {
            if (y == null || car < 0 || car >= y.Cars.Count || y.Cars[car].Spot < 0 || y.Cars[car].Spot >= y.Spots.Count)
            {
                return FloorMiddleOf(y);
            }
            return DealerGame.At(y.Spots[y.Cars[car].Spot]) + Vector3.up * Lift;
        }

        /// <summary>A place to stand by a car on its bay (side, nose or tail by <paramref name="variant"/>), and its middle to face.</summary>
        public static bool BesideCar(DealerYard y, int car, int variant, float side, float halfWidth, float halfLength, out Vector3 spot, out Vector3 middle)
        {
            spot = middle = default;
            if (y == null || car < 0 || car >= y.Cars.Count)
            {
                return false;
            }
            StockCar c = y.Cars[car];
            if (c.Spot < 0 || c.Spot >= y.Spots.Count)
            {
                return false;
            }
            LotSpot s = y.Spots[c.Spot];
            DealerDirector director = DealerDirector.Instance;
            bool turned = director != null && director.Bays.TurnedOf(y.Place, c.VehicleId);
            Quaternion facing = DealerGame.Facing(s, turned);
            Vector3 at = DealerGame.At(s), forward = facing * Vector3.forward, right = facing * Vector3.right;
            int which = ((variant % 4) + 4) % 4;
            Vector3 offset = which == 0 ? -right * (halfWidth + side) : which == 1 ? right * (halfWidth + side) : which == 2 ? forward * (halfLength + side) : -forward * (halfLength + side);
            middle = at + Vector3.up * Lift;
            return ShopFloor.Standable(at + offset, 1.5f, out spot);
        }

        /// <summary>A place a little way from someone, towards what they look at: where a salesperson stands to talk to them.</summary>
        public static bool Between(Vector3 person, Vector3 looksAt, float reach, out Vector3 spot)
        {
            Vector3 way = looksAt - person;
            way.y = 0f;
            Vector3 side = way.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, way.normalized) : Vector3.right;
            return ShopFloor.Standable(person + side * reach, 1.2f, out spot);
        }

        /// <summary>The middle and the length of a car's body, from its meshes; false if there is no car.</summary>
        public static bool Body(VehicleController car, out Vector3 middle, out float length)
        {
            middle = car != null ? car.transform.position + Vector3.up * Lift : Vector3.zero;
            length = 4.6f;
            Renderer[] meshes = car != null && car.CarFeatures != null ? car.CarFeatures.bodyMeshes : null;
            if (meshes == null || meshes.Length == 0)
            {
                return car != null;
            }
            bool any = false;
            Bounds b = default;
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null)
                {
                    continue;
                }
                if (!any)
                {
                    b = meshes[i].bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(meshes[i].bounds);
                }
            }
            if (any)
            {
                middle = b.center;
                length = Mathf.Max(b.size.x, b.size.z);
            }
            return true;
        }

        /// <summary>The floor's bounds as the game measured them when the player came in (x and z, in metres) and its height.</summary>
        public static bool FloorBounds(out float minX, out float maxX, out float minZ, out float maxZ, out float floorY)
        {
            minX = maxX = minZ = maxZ = floorY = 0f;
            BuildingManager building = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            if (building == null)
            {
                return false;
            }
            Bounds b = building.interiorBounds;
            if (b.size.x < 1f || b.size.z < 1f)
            {
                return false;
            }
            minX = b.min.x;
            maxX = b.max.x;
            minZ = b.min.z;
            maxZ = b.max.z;
            floorY = b.min.y;
            return true;
        }

        /// <summary>The building the player stands in, to see that they are still in it: one reference.</summary>
        public static Buildings.Building Inside => BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.building : null;

        /// <summary>Where the pointer meets the floor at this height. False if it looks above the level.</summary>
        public static bool Pointed(float floorY, out Vector3 point)
        {
            point = default;
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            Plane floor = Floor;
            floor.distance = -floorY;
            if (!floor.Raycast(ray, out float enter))
            {
                return false;
            }
            point = ray.GetPoint(enter);
            return true;
        }

        /// <summary>The game's car camera made live on this aim and place, what it had kept to give back. False if the game has none.</summary>
        public static bool TakeCamera(Vector3 aim, Vector3 at)
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            CinemachineVirtualCameraBase cam = gm != null ? gm.vehiclePreviewCamera : null;
            if (cam == null)
            {
                return false;
            }
            if (_aim == null)
            {
                var made = new GameObject(AimName);
                _aim = made.transform;
            }
            if (!_taken)
            {
                _cam = cam;
                _hadLookAt = cam.LookAt;
                _hadAt = cam.transform.position;
                _hadTurn = cam.transform.rotation;
                _taken = true;
            }
            MoveCamera(aim, at);
            cam.LookAt = _aim;
            CameraHelper.SetCamera(cam);
            PlayerController player = gm.playerController;
            if (player != null && !_blocked)
            {
                player.ResetNavigation();
                player.SetNavigationBlocker(NavigationBlocker.FreeLookCamera);
                _blocked = true;
            }
            MouseController.SetRestrictedObjectTypes();   // nothing in the world is pointed at or clicked meanwhile, as in the game's placement
            if (!_zoomWasBlocked)
            {
                _zoomWasBlocked = true;
                _hadZoomBlock = CameraControllers.PedestrianCam.blockCameraZoom;
                CameraControllers.PedestrianCam.blockCameraZoom = true;
            }
            return true;
        }

        public static void MoveCamera(Vector3 aim, Vector3 at)
        {
            if (_aim != null)
            {
                _aim.position = aim;
            }
            if (_cam != null)
            {
                _cam.transform.position = at;
            }
        }

        /// <summary>The live camera's bearing round a point (degrees from north), for the showroom's camera to start from.</summary>
        public static float CameraYaw(Vector3 round)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return 0f;
            }
            Vector3 from = camera.transform.position - round;
            return Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
        }

        /// <summary>The camera, the walk and the pointer given back as they were. Safe to call when nothing was taken.</summary>
        public static void GiveCamera()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            if (_taken)
            {
                _taken = false;
                if (_cam != null)
                {
                    _cam.LookAt = _hadLookAt;
                    _cam.transform.SetPositionAndRotation(_hadAt, _hadTurn);
                }
                if (gm != null && gm.indoorCamera != null && _cam != null && _cam.Priority == 1)
                {
                    CameraHelper.SetCamera(BuildingManager.IsInsideBuilding ? gm.indoorCamera : gm.pedestrianCamera);
                }
                _cam = null;
                _hadLookAt = null;
            }
            if (_blocked)
            {
                _blocked = false;
                gm?.playerController?.UnsetNavigationBlocker(NavigationBlocker.FreeLookCamera);
            }
            MouseController.Reset();
            if (_zoomWasBlocked)
            {
                _zoomWasBlocked = false;
                CameraControllers.PedestrianCam.blockCameraZoom = _hadZoomBlock;
            }
        }

        /// <summary>The city is unloading: the aim goes with it.</summary>
        public static void Forget()
        {
            GiveCamera();
            Unpause();
            Outline(null);
            if (_aim != null)
            {
                Object.Destroy(_aim.gameObject);
            }
            _aim = null;
        }

        /// <summary>The game paused as its own placement pauses it, but never in a Going Public session (its clock is everyone's).</summary>
        public static void Pause()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.gameSpeed == null || _timeHeld || GoingPublic.InSession)
            {
                return;
            }
            _paused = !uis.gameSpeed.Paused;
            if (_paused)
            {
                uis.gameSpeed.SetPause(true);
            }
            uis.gameSpeed.DisableTimeControl(true);
            _timeHeld = true;
        }

        public static void Unpause()
        {
            if (!_timeHeld)
            {
                return;
            }
            _timeHeld = false;
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

        /// <summary>A car shown where it would stand, while a bay is chosen. Its own bay comes back with DealerGame.Park.</summary>
        public static void ShowAt(VehicleController car, Vector3 at, Quaternion facing)
        {
            if (car != null)
            {
                car.transform.SetPositionAndRotation(at, facing);
            }
        }

        /// <summary>The car that would make way outlined with the game's own outline, or none (null). Its layers are put back.</summary>
        public static void Outline(VehicleController car)
        {
            if (_outlined != null)
            {
                for (int i = 0; i < _outlined.Length && i < Layers.Count; i++)
                {
                    if (_outlined[i] != null)
                    {
                        _outlined[i].gameObject.layer = Layers[i];
                    }
                }
                _outlined = null;
                Layers.Clear();
            }
            Renderer[] meshes = car != null && car.CarFeatures != null ? car.CarFeatures.bodyMeshes : null;
            if (meshes == null)
            {
                return;
            }
            _outlined = meshes;
            for (int i = 0; i < meshes.Length; i++)
            {
                Layers.Add(meshes[i] != null ? meshes[i].gameObject.layer : 0);
                if (meshes[i] != null)
                {
                    meshes[i].gameObject.layer = LayerHelper.InteractiveItemsOutlinedLayerIndex;
                }
            }
        }

        public static void Sound(SoundType sound, Vector3 at)
        {
            SfxManager sfx = InstanceBehavior<SfxManager>.Instance;
            if (sfx != null)
            {
                sfx.PlayAudio(sound, at);
            }
        }

        /// <summary>The phone's full menu closed, as the game closes it before its own placement and preview.</summary>
        public static void CloseMenu()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis != null && uis.fullMenu != null && UI.Smartphone.FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
        }
    }
}
