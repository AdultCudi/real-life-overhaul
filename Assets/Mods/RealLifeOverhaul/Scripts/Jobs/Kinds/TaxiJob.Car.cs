using System.Globalization;
using BigAmbitions.Characters;
using Characters.EmojiSystem;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Ride-hail: the roof sign, and what a passenger in this car calls a hard stop.</summary>
    public sealed partial class TaxiJob
    {
        private static readonly RaycastHit[] RoofHits = new RaycastHit[12];

        // The sign stands on the roof of the car the shift is driven in, found with one ray from above; moved only when the
        // player changes cars.
        private void Sign(CarController car)
        {
            if (car == null || ReferenceEquals(car, _signOn))
            {
                return;
            }
            Unsign();
            int mask = TrafficTools.VehicleMask;
            Transform body = car.transform;
            JobSpots.CarFrame(car, out Vector3 centre, out _, out _, out _);
            int hits = mask != 0 ? Physics.RaycastNonAlloc(centre + body.up * 4f, -body.up, RoofHits, 5f, mask, QueryTriggerInteraction.Ignore) : 0;
            float nearest = float.MaxValue;
            Vector3 roof = default;
            for (int i = 0; i < hits; i++)
            {
                if (RoofHits[i].collider != null && RoofHits[i].collider.transform.IsChildOf(body) && RoofHits[i].distance < nearest)
                {
                    nearest = RoofHits[i].distance;
                    roof = RoofHits[i].point;
                }
                RoofHits[i] = default;
            }
            if (nearest > 4.5f)
            {
                return;   // nothing of the car's under the ray: no sign rather than one in the air
            }
            _sign = Ctx.Props.Take(SignProp);
            if (_sign == null)
            {
                return;
            }
            _sign.SetParent(body, false);
            _sign.localPosition = body.InverseTransformPoint(roof);
            _sign.localRotation = Quaternion.identity;
            Vector3 scale = body.lossyScale;
            _sign.localScale = new Vector3(1f / Mathf.Max(0.01f, scale.x), 1f / Mathf.Max(0.01f, scale.y), 1f / Mathf.Max(0.01f, scale.z));
            _signOn = car;
        }

        private void Unsign()
        {
            if (_sign != null)
            {
                Ctx.Props.Return(_sign);   // back under the jobs' own root, at scale one, switched off
            }
            _sign = null;
            _signOn = null;
        }

        // What this car can do, and so what a passenger in it calls a hard stop.
        private void Tune(CarController car)
        {
            _harsh = HarshG;
            try
            {
                Rigidbody body = Ctx.Body;
                var vehicle = car != null ? car.vehicleController : null;
                if (vehicle != null && body != null && body.mass > 100f)
                {
                    float brakes = vehicle.brakes != null ? 4f * vehicle.brakes.maxTorque / (WheelRadius * body.mass) : Grip;
                    float engine = vehicle.powertrain != null && vehicle.powertrain.engine != null
                        ? vehicle.powertrain.engine.maxPower * 1000f / (body.mass * LaunchSpeed) : Grip;
                    float most = Mathf.Min(Grip, Mathf.Max(brakes, engine));
                    _harsh = Mathf.Clamp(most * Tolerated, HarshLeast, HarshG);
                    Ctx.Logger?.Info("Jobs: taxi comfort for this car: brakes " + brakes.ToString("F1", CultureInfo.InvariantCulture) + " m/s2, engine "
                                     + engine.ToString("F1", CultureInfo.InvariantCulture)
                                     + " m/s2 at " + LaunchSpeed.ToString("F0") + " m/s, " + body.mass.ToString("F0") + " kg; a hard stop is " + _harsh.ToString("F1", CultureInfo.InvariantCulture)
                                     + " m/s2.");
                }
            }
            catch (System.Exception e)
            {
                Ctx.Logger?.Error(e);
            }
            if (Ctx.Perk(CareerPerk.Premium))
            {
                _harsh = Mathf.Max(HarshLeast * 0.8f, _harsh * CareerCatalogue.PremiumHarsh);   // a premium passenger minds sooner
            }
            _bump = _harsh + BumpOver;
        }
    }
}
