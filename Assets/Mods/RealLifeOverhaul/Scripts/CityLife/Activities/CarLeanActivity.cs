using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A driver leaning on the pavement side of a car the game has parked, only where their back measures the clip's 0.14 m from the bodywork.</summary>
    public sealed class CarLeanActivity : CityActivity
    {
        private const float Gap = 0.14f;        // metres from the car's side to the heels (lean_wait)
        private const float GapSlack = 0.07f;   // metres either way

        private Vector3 _stand, _face;

        public override bool Find(CitySite site)
        {
            if (!CrimeCars.FindParked(site.Anchor, 14f, out Vector3 centre, out Vector3 forward, out float halfWidth))
            {
                site.Refusal = CityRefusal.NoParkedCar;
                return false;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 out_ = right * side;
                for (int k = -1; k <= 1; k++)
                {
                    // By the front door, the middle, the back door.
                    Vector3 wanted = centre + out_ * (halfWidth + Gap) + forward * (k * 0.7f);
                    if (!CitySpots.Pavement(wanted, 0.3f, out Vector3 spot))
                    {
                        continue;
                    }
                    // Measured to the bodywork itself at hip height, not to a box round the car.
                    if (!Physics.Raycast(spot + Vector3.up * 0.9f, -out_, out RaycastHit body, 0.6f, 1 << LayerHelper.ParkedVehiclesLayerIndex, QueryTriggerInteraction.Ignore)
                        || Mathf.Abs(body.distance - Gap) > GapSlack || Vector3.Dot(body.normal, out_) < 0.85f || !CitySpots.Stand(spot + out_ * 0.45f, 0.3f, out _))
                    {
                        continue;   // not the measured distance from real bodywork, or no clear pavement in front
                    }
                    _stand = spot;
                    _face = spot + out_;
                    Centre = spot;
                    return true;
                }
            }
            site.Refusal = CityRefusal.NoParkedCar;
            return false;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            CityPerson driver = Add(CityLook.Civilian, _stand, _face);
            if (driver == null)
            {
                return false;
            }
            if (!driver.Work("lean_wait", Props, 0f))
            {
                site.Refusal = CityRefusal.NoProp;
                End();
                return false;
            }
            return true;
        }

        public override bool Tick(float now)
        {
            return Lasting();
        }
    }
}
