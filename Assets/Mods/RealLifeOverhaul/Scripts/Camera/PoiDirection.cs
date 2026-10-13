using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Helpers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts the game's on-screen place markers on the right side of the screen when what they mark is
    /// behind the camera. The game was made for a camera that looks down on the player, where nothing
    /// marked is ever behind it; it projects each marker straight to the screen, and a point behind a
    /// camera projects mirrored through the middle, so with the mod's street-level cameras the icon of a car
    /// behind the player showed up ahead of them, on the opposite side.
    ///
    /// After the game has placed its markers each frame (and only while the mod's camera is on and the
    /// map is closed), every marker whose target is behind the camera is moved: direction markers
    /// (destination, deliveries, taxi, the private driver) and the player's own vehicles to the screen
    /// edge on the side the target really is, in the lower half ("turn round"); every other marker out
    /// of the picture, as the game does with what is out of view. Markers in front are never touched.
    /// A few subtractions per marker, nothing allocated.
    /// </summary>
    public sealed class PoiDirection : MonoBehaviour
    {
        private const float NearlyBehind = -0.05f;   // camera space looks down -z: at or behind the lens from here
        private const int Remembered = 8;

        public static IModLogger Logger;

        private static readonly FieldInfo PermanentField = GameContract.PermanentPois.Field;   // (the game's private fields, resolved once: Core/GameContract)
        private static readonly FieldInfo DeadZoneField = GameContract.PoiDeadZone.Field;
        private static readonly FieldInfo PointerField = GameContract.PoiPointer.Field;

        // What was read from a direction marker's private fields, so that is done once per marker.
        private readonly PointOfInterest[] _known = new PointOfInterest[Remembered];
        private readonly Vector4[] _deadZones = new Vector4[Remembered];
        private readonly RectTransform[] _pointers = new RectTransform[Remembered];
        private int _next;

        private List<PointOfInterest> _permanent;   // the game's own list of the markers it shows in the street
        private bool _lookedUp;
        private bool _failed;

        private void OnEnable()
        {
            // After every LateUpdate (the game places its markers in one) and before the interface is drawn.
            Canvas.willRenderCanvases += Correct;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= Correct;
        }

        private void Correct()
        {
            if (!Settings.CameraEnabled || _failed || CityMap.IsOpen || !InstanceBehavior<CityManager>.IsInitialized)
            {
                return;   // the game's own view: its markers are right as they are
            }
            try
            {
                Camera camera = GameManager.GetMainCamera();
                CityMap map = InstanceBehavior<CityManager>.Instance.cityMap;
                if (camera == null || map == null)
                {
                    return;
                }
                if (!_lookedUp)
                {
                    _lookedUp = true;
                    _permanent = PermanentField != null ? PermanentField.GetValue(null) as List<PointOfInterest> : null;
                    if (_permanent == null || DeadZoneField == null || PointerField == null)
                    {
                        Logger?.Warn("Markers: the game's marker code is not as expected ("
                            + (_permanent == null ? "no list of street markers; every marker is looked at instead" : "no dead zone or pointer; direction markers use the whole screen")
                            + ").");
                    }
                }
                List<PointOfInterest> markers = _permanent ?? map.pois;
                Matrix4x4 view = camera.worldToCameraMatrix;
                float width = Screen.width;
                float height = Screen.height;
                for (int i = 0; i < markers.Count; i++)
                {
                    PointOfInterest marker = markers[i];
                    if (marker == null || marker.hidden || !marker.Permanent || marker.target == null)
                    {
                        continue;
                    }
                    Vector3 seen = view.MultiplyPoint3x4(marker.target.position + marker.offset);
                    if (seen.z < NearlyBehind || !marker.gameObject.activeSelf)
                    {
                        continue;   // in front of the camera (or not shown): the game has it right
                    }

                    // Which way the target lies, as the player would have to turn: sideways by where it is to
                    // the camera's left or right, and always downwards, since it is behind.
                    float sideways = seen.x;
                    float down = Mathf.Max(Mathf.Abs(seen.y), Mathf.Abs(seen.x) * 0.3f, 0.01f);
                    if (marker.isGuider)
                    {
                        Place(marker, sideways, -down, width, height);
                    }
                    else if (IsVehicle(marker))
                    {
                        // The player's own cars: at the edge, clear of the game's panels at the bottom.
                        marker.transform.position = OnEdge(sideways, -down,
                            width * 0.06f - width * 0.5f, width * 0.5f - width * 0.06f, height * 0.16f - height * 0.5f, height * 0.5f) + new Vector3(width * 0.5f, height * 0.5f);
                    }
                    else
                    {
                        marker.transform.position = new Vector3(-10000f, -10000f, 0f);   // out of view, like anything else not in the picture
                    }
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Warn("Markers: correcting markers behind the camera failed and is switched off; the game places them as before. " + e);
            }
        }

        // A direction marker: on the edge of the game's own frame for these (the screen less its dead zones), pointing outwards.
        private void Place(PointOfInterest marker, float x, float y, float width, float height)
        {
            int slot = Array.IndexOf(_known, marker);
            if (slot < 0)
            {
                slot = _next;
                _next = (_next + 1) % Remembered;
                _known[slot] = marker;
                _deadZones[slot] = DeadZoneField != null ? (Vector4)DeadZoneField.GetValue(marker) : Vector4.zero;
                _pointers[slot] = PointerField != null ? PointerField.GetValue(marker) as RectTransform : null;
            }
            // The game's frame for these, measured from the middle of the screen, by the game's own sum.
            Vector4 dead = _deadZones[slot];
            float left = dead.z * width - width * 0.5f;
            float bottom = dead.y * height - height * 0.5f;
            float right = left + width - dead.x * width - dead.z * width;
            float top = bottom + height - dead.w * height - dead.y * height;
            if (!(left < 0f) || !(right > 0f) || !(bottom < 0f) || !(top > 0f))
            {
                left = -width * 0.5f;      // a frame that does not hold the middle of the screen: use the screen
                right = width * 0.5f;
                bottom = -height * 0.5f;
                top = height * 0.5f;
            }
            marker.transform.position = OnEdge(x, y, left, right, bottom, top) + new Vector3(width * 0.5f, height * 0.5f);
            RectTransform pointer = _pointers[slot];
            if (pointer != null)
            {
                // The game's own convention: the pointer's rest position points down, turned by the angle from "up".
                pointer.rotation = Quaternion.Euler(0f, 0f, 180f + Vector2.SignedAngle(Vector2.up, new Vector2(x, y)));
            }
        }

        // Where a line from the middle of the screen in a direction leaves a frame given from the middle.
        private static Vector3 OnEdge(float x, float y, float left, float right, float bottom, float top)
        {
            float reach = float.MaxValue;
            if (x > 0.0001f)
            {
                reach = right / x;
            }
            else if (x < -0.0001f)
            {
                reach = left / x;
            }
            if (y > 0.0001f)
            {
                reach = Mathf.Min(reach, top / y);
            }
            else if (y < -0.0001f)
            {
                reach = Mathf.Min(reach, bottom / y);
            }
            return reach == float.MaxValue ? new Vector3(0f, bottom, 0f) : new Vector3(x * reach, y * reach, 0f);
        }

        private static bool IsVehicle(PointOfInterest marker)
        {
            List<VehicleController> vehicles = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleController vehicle = vehicles[i];
                if (vehicle != null && ReferenceEquals(vehicle.poi, marker))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
