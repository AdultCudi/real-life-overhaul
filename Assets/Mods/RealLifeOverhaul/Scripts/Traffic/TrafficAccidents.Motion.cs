using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Traffic accidents: the moment physics has the cars, a roll or a shunt, until each is frozen where it came to rest.</summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        private const float TumbleReach = 14f;          // metres of clear road the roll needs ahead of the car
        private const float TumbleRadius = 16f;         // metres: the car is frozen this far from where it started
        private const float TumbleMaxTime = 3.2f;       // seconds
        private const float ShuntLeast = 1f;            // m/s: cars that met slower than this stand where they met
        private const float ShuntMaxTime = 1.6f;        // seconds physics has the cars of a collision on the road before they are frozen
        private const float ShuntRestFrom = 0.25f;      // ...and the least it has them
        private const float ShuntBraking = 9f;          // m/s2 they lose speed with (locked wheels on a dry road)
        private const float ShuntRadius = 12f;          // metres from where they met beyond which a car is frozen at once
        private const float ShuntBounce = 0.2f;         // of the speed they met at that they part with again
        private const float ShuntDamping = 11f;         // per second on the vertical speed: the traffic system's springs have no damper for a car it ignores
        private const float TumblePlayerDistance = 35f; // metres the roll must end from the player
        private const float RollLongest = 5.6f;         // metres: nothing longer than this rolls (vans, trucks, buses, limousines)
        private const float RollTallest = 2.1f;         // ...nor anything taller
        private static readonly List<Collider> RestScratch = new List<Collider>(16);

        // Only a car or an SUV rolls: a van, truck, bus or limousine thrown about rocks on its wheels and ends up leaning.
        private static bool Rolls(VehicleComponent car)
        {
            if (car.vehicleType != VehicleTypes.Car || car.length > RollLongest || car.coliderHeight > RollTallest)
            {
                return false;
            }
            string name = car.prefab != null ? car.prefab.name : string.Empty;
            return name.IndexOf("Truck", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Van", StringComparison.OrdinalIgnoreCase) < 0
                   && name.IndexOf("Bus", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Limo", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private bool CanTumble(VehicleComponent car, Vector3 player)
        {
            Vector3 forward = _heading * Vector3.forward;
            if ((_origin + forward * TumbleReach - player).sqrMagnitude < TumblePlayerDistance * TumblePlayerDistance)
            {
                return false;
            }
            for (float ahead = TumbleReach / 3f; ahead <= TumbleReach + 0.1f; ahead += TumbleReach / 3f)
            {
                if (!IsPoseFree(car, _origin + forward * ahead, _heading))
                {
                    return false;
                }
            }
            return true;
        }

        // The lead car really rolls: physics has it for a couple of seconds, then TickTumble freezes it.
        private void BeginTumble(VehicleComponent car)
        {
            Vector3 forward = _heading * Vector3.forward;
            Rigidbody body = car.rb;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.None;
            body.velocity = forward * Random.Range(9f, 13f) + Vector3.up * Random.Range(2.5f, 3.5f);
            body.angularVelocity = forward * (_side * Random.Range(4.5f, 6.5f)) + Vector3.up * Random.Range(-1f, 1f);

            _tumbleRestFrom = Time.time + 1f;
            _tumbleEnd = Time.time + TumbleMaxTime;
            _phase = Phase.Tumbling;
        }

        private void TickTumble(float now)
        {
            VehicleComponent car = _leadWreck != null ? _leadWreck.Car : null;
            if (car == null)
            {
                Clear(resumeTraffic: true, announce: false, why: "the rolling car was lost");
                return;
            }

            Rigidbody body = car.rb;
            Vector3 offset = body.position - _origin;
            bool strayed = offset.x * offset.x + offset.z * offset.z > TumbleRadius * TumbleRadius || offset.y < -3f;
            bool atRest = now >= _tumbleRestFrom && body.velocity.sqrMagnitude < 0.5f && body.angularVelocity.sqrMagnitude < 0.5f;
            if (!strayed && !atRest && now < _tumbleEnd)
            {
                return;
            }

            // Wherever it ended up is where the crash is: squarely on its side, its roof or its wheels, and on the road.
            string how = RestRolled(_leadWreck);
            Vector3 rest = car.transform.position;
            _scene = new Vector3(rest.x, _origin.y, rest.z);
            Logger?.Info($"Accident: the rolling car came to rest {how}, {Vector3.Distance(rest, _origin):F0} m on.");
            CompleteStaging(tumbled: true);
        }

        // The cars keep moving for a moment: two share their momentum, part with a fifth of the closing speed and turn a
        // little. False (nothing changed) if they met too slowly for it to show, or a body is kinematic.
        private bool BeginShunt(VehicleComponent first, VehicleComponent second, Vector3 firstVelocity, Vector3 secondVelocity)
        {
            firstVelocity.y = 0f;
            secondVelocity.y = 0f;
            Rigidbody lead = first.rb;
            Rigidbody struck = second != null ? second.rb : null;
            if (lead == null || lead.isKinematic || (struck != null && struck.isKinematic)
                || (firstVelocity.sqrMagnitude < ShuntLeast * ShuntLeast && secondVelocity.sqrMagnitude < ShuntLeast * ShuntLeast))
            {
                return false;
            }
            float turn = Mathf.Clamp((firstVelocity - secondVelocity).magnitude * 0.06f, 0.15f, 1.1f) * _side;
            if (struck != null)
            {
                float total = Mathf.Max(1f, lead.mass + struck.mass);
                Vector3 together = (firstVelocity * lead.mass + secondVelocity * struck.mass) / total;
                Vector3 closing = firstVelocity - secondVelocity;
                lead.velocity = together - closing * (ShuntBounce * struck.mass / total);
                struck.velocity = together + closing * (ShuntBounce * lead.mass / total);
                struck.angularVelocity = Vector3.up * (-turn * 0.6f);
            }
            else
            {
                lead.velocity = firstVelocity;
            }
            lead.angularVelocity = Vector3.up * turn;
            _shuntRestFrom = Time.time + ShuntRestFrom;
            _shuntEnd = Time.time + ShuntMaxTime;
            _phase = Phase.Shunting;
            return true;
        }

        // Every frame while the cars still move (1.6 s at most): each slows as on locked wheels and its vertical speed is bled
        // off (nothing damps a car the traffic system ignores). Once all rest, or time or room runs out, each is frozen upright.
        private void TickShunt(float now)
        {
            float step = Time.deltaTime;
            bool moving = false;
            bool strayed = false;
            for (int i = 0; i < _wrecks.Count; i++)
            {
                VehicleComponent car = _wrecks[i].Car;
                Rigidbody body = car != null ? car.rb : null;
                if (body == null || body.isKinematic)
                {
                    continue;
                }
                Vector3 velocity = body.velocity;
                float fall = velocity.y * Mathf.Exp(-ShuntDamping * step);
                velocity.y = 0f;
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, ShuntBraking * step);
                body.velocity = new Vector3(velocity.x, fall, velocity.z);
                float spin = Mathf.MoveTowards(body.angularVelocity.y, 0f, 1.2f * step);
                body.angularVelocity = Vector3.up * spin;
                moving |= velocity.sqrMagnitude > 0.09f || Mathf.Abs(spin) > 0.1f;
                Vector3 offset = body.position - _origin;
                strayed |= offset.x * offset.x + offset.z * offset.z > ShuntRadius * ShuntRadius || offset.y < -3f;
            }
            if (!strayed && now < _shuntEnd && (moving || now < _shuntRestFrom))
            {
                return;
            }

            for (int i = 0; i < _wrecks.Count; i++)
            {
                VehicleComponent car = _wrecks[i].Car;
                if (car != null)
                {
                    Transform at = car.transform;
                    Pin(_wrecks[i], at.position, Quaternion.Euler(0f, at.rotation.eulerAngles.y, 0f));
                }
            }
            VehicleComponent first = _leadWreck != null ? _leadWreck.Car : null;
            if (first == null)
            {
                Clear(resumeTraffic: true, announce: false, why: "the car that crashed was lost");
                return;
            }
            Vector3 rest = first.transform.position;
            float moved = Vector3.Distance(rest, _origin);
            _origin = rest;
            _heading = first.transform.rotation;
            _scene = _origin;
            Logger?.Info(FormattableString.Invariant($"Accident: the cars came to rest {moved:F1} m on from where they met, after {ShuntMaxTime - Mathf.Max(0f, _shuntEnd - now):F1} s")
                         + $"{(strayed ? " (frozen early: one had gone too far)" : string.Empty)}; the scene is measured from there.");
            CompleteStaging(tumbled: false);
        }

        // Freezes a rolled car in the nearest resting pose (side, roof or wheels), its lowest part on the road. Says which.
        private string RestRolled(Wreck wreck)
        {
            VehicleComponent car = wreck.Car;
            Transform body = car.transform;
            Vector3 forward = body.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.05f)
            {
                forward = _heading * Vector3.forward;   // standing on its nose: laid down the way it was going
                forward.y = 0f;
            }
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 up = body.up;
            string how = up.y > 0.7f ? "on its wheels" : up.y < -0.7f ? "on its roof" : "on its side";
            Vector3 roof = up.y > 0.7f ? Vector3.up : up.y < -0.7f ? Vector3.down : Vector3.Dot(up, right) >= 0f ? right : -right;
            Vector3 at = body.position;
            Pin(wreck, at, Quaternion.LookRotation(forward, roof));

            Physics.SyncTransforms();
            float lowest = float.MaxValue;
            car.GetComponentsInChildren(false, RestScratch);
            for (int i = 0; i < RestScratch.Count; i++)
            {
                Collider collider = RestScratch[i];
                if (collider != null && collider.enabled && !collider.isTrigger && !(collider is WheelCollider))
                {
                    lowest = Mathf.Min(lowest, collider.bounds.min.y);
                }
            }
            RestScratch.Clear();
            if (lowest < float.MaxValue && StreetSpots.Ground(at, out float ground) && Mathf.Abs(ground - lowest) < 3f)
            {
                at.y += ground - lowest + 0.02f;
                body.position = at;
            }
            return how;
        }
    }
}
