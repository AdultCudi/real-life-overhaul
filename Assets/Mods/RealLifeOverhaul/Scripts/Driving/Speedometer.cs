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
        private static readonly FeatureGuard FrameGuard = Guard.For("driving", "Speedometer", typeof(Speedometer));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float MetresPerSecondToKmh = 3.6f;
        private const float MetresPerSecondToMph = 2.23694f;

        private VehicleController _vehicle;
        private Rigidbody _body;
        private bool _imperial;
        private bool _shown;

        private void Update()
        {
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
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
