using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Shows the speed of the vehicle being driven, in km/h or mph following the game's units
    /// setting, as one more reading of the game's own vehicle panel (<see cref="SpeedReading"/>). Reads one velocity per frame; the text
    /// is only rebuilt when the whole number changes.
    /// </summary>
    public class Speedometer : MonoBehaviour
    {
        private const float MetresPerSecondToKmh = 3.6f;
        private const float MetresPerSecondToMph = 2.23694f;

        private VehicleController _vehicle;
        private Rigidbody _body;
        private bool _imperial;
        private bool _shown;

        private static readonly int HotUpdate = HotSpots.Slot("Driving: Speedometer.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            if (vehicle != _vehicle)
            {
                // Looked up once per vehicle, not per frame.
                _vehicle = vehicle;
                _body = vehicle != null && vehicle.vehicleType != null && vehicle.vehicleType.IsMotorVehicle
                    ? vehicle.GetComponent<Rigidbody>()
                    : null;
                _imperial = PlayerPrefSettings.useImperial;
            }

            bool show = Settings.Speedometer && _body != null && !ThirdPersonCamera.IsUiOpen();
            if (!show)
            {
                if (_shown)
                {
                    _shown = false;
                    SpeedReading.Hide();
                }
                return;
            }

            _shown = true;
            float speed = _body.velocity.magnitude * (_imperial ? MetresPerSecondToMph : MetresPerSecondToKmh);
            SpeedReading.Show(Mathf.RoundToInt(speed), _imperial ? "mph" : "km/h");
        }

        private void OnDestroy()
        {
            if (_shown)
            {
                SpeedReading.Hide();
            }
        }
    }
}
