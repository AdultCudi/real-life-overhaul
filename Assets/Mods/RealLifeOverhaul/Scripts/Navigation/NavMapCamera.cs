using Cinemachine;
using Helpers;
using RealLifeOverhaul.Map25;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The city map's 2D and 3D button on the game's own map: it turns the map camera flat or back to the game's own tilt by moving the camera along the arc round the
    /// pivot that the game's zoom keeps (CityMapCam recomputes the camera's place from its direction and its own distance, so a pan, a zoom or a turn keeps whatever pitch
    /// is set). Nothing else of the camera is touched, the map is the game's own city at all times, and the game's own tilt is put back when the map shuts. Cost: the
    /// component is on only while the map is open, and a frame is a pitch read and a compare; the camera is moved only in the frame the pitch is not the one asked for.
    /// </summary>
    [DefaultExecutionOrder(30000)]   // after the camera's brain has put the camera where it goes this frame
    public sealed class NavMapCamera : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("navigation", "NavMapCamera", typeof(NavMapCamera));

        private Transform _virtual;
        private bool _pitched;
        private float _flatX, _flatZ = 1f;

        /// <summary>True while the camera is held at a pitch of the mod's (the map check after the map shut reads it: 0 held is as it was before the map opened).</summary>
        public bool Pitched => _pitched;

        /// <summary>The map opened or shut; shut, the game's own tilt is given back first.</summary>
        public void Open(bool open)
        {
            if (!open)
            {
                RestorePitch();
            }
            _virtual = null;
            enabled = open;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Navigation: NavMapCamera.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                _pitched = false;
            }
        }

        private void OnLateUpdate()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityMap map = city != null ? city.cityMap : null;
            if (map == null || !CityMap.IsOpen || map.cityMapCam == null)
            {
                return;
            }
            if (_virtual == null)
            {
                CinemachineVirtualCameraBase vcam = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.citymapCamera : null;
                _virtual = vcam != null ? vcam.transform : null;
                if (_virtual == null)
                {
                    return;
                }
            }
            if (map.isSubwayMode || map.IsTaxiMode)
            {
                RestorePitch();   // those maps are the game's own as they are
                return;
            }
            Tilt(map, NavMapTilt.Pitch(NavTilt.Map, map.cityMapCam.distance), true);
        }

        // The pitch eased to what the button asks. Where the game's own tilt is asked and the camera is not held, nothing is done at all.
        private void Tilt(CityMap map, float want, bool eased)
        {
            if (!_pitched && want == Map25View.TiltPitch)
            {
                return;
            }
            Vector3 pivot = map.cityMapCam.transform.position;
            Vector3 away = pivot - _virtual.position;
            float distance = away.magnitude;
            if (distance < 1f)
            {
                return;
            }
            float pitch = Mathf.Asin(Mathf.Clamp(-away.y / distance, -1f, 1f)) * Mathf.Rad2Deg;
            float flat = Mathf.Sqrt(away.x * away.x + away.z * away.z);
            if (flat > 0.01f)
            {
                _flatX = away.x / flat;
                _flatZ = away.z / flat;
            }
            if (Mathf.Abs(want - pitch) < 0.2f)
            {
                _pitched = want != Map25View.TiltPitch;   // arrived: back at the game's own tilt it holds nothing
                return;
            }
            float next = eased ? pitch + (want - pitch) * Mathf.Min(1f, Time.unscaledDeltaTime * 6f) : want;
            float radians = next * Mathf.Deg2Rad;
            var direction = new Vector3(_flatX * Mathf.Cos(radians), -Mathf.Sin(radians), _flatZ * Mathf.Cos(radians));
            _virtual.position = pivot - direction * distance;
            _virtual.rotation = Quaternion.LookRotation(direction);
            _pitched = want != Map25View.TiltPitch || next != Map25View.TiltPitch;
        }

        // The game's own tilt back on the map camera: the game keeps the direction it last had and has no control of the pitch to undo ours.
        private void RestorePitch()
        {
            if (!_pitched)
            {
                return;
            }
            _pitched = false;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityMap map = city != null ? city.cityMap : null;
            if (map != null && map.cityMapCam != null && _virtual != null)
            {
                Tilt(map, Map25View.TiltPitch, false);   // at once: the map is shutting
                _pitched = false;
            }
        }

        private void OnDisable()
        {
            RestorePitch();
        }
    }
}
