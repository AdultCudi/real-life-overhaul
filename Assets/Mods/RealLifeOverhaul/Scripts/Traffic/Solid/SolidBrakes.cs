using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The foot on the brake of the few traffic cars that have somebody ahead of them. The traffic system
    /// itself knows only one way to stop for an obstacle: once its sensor box touches it, from whatever
    /// speed, within 1.25 m (DrivingAI.GetActionValue gives StopInDistance 1.25; DriveJob.StopInDistance
    /// multiplies the braking until that is enough). That holds a car, but it is no driver braking for
    /// somebody they have seen from 30 m. So for at most eight cars (named by SolidStreets eight times a
    /// second, each with the person it has ahead) the approach is shaped here, every physics step: the
    /// braking that stops the car a gap short of the person (SolidRules.Brake), taken off its speed.
    ///
    /// HOW, WITHOUT TAKING THE CAR OVER. The car stays the traffic system's in every respect. Each step
    /// that system reads the body's velocity, adds its own acceleration step to it and applies the
    /// difference as a change of velocity (TrafficManager.FixedUpdate: AddForce(vehicleBodyForce,
    /// VelocityChange), from DriveJob.ComputeBodyForce). Here a second change of velocity is added the
    /// same way: the braking for one step, and the acceleration step it would otherwise add back
    /// (VehicleComponent.GetPowerStep), never more than the speed the car has. The two add up whichever
    /// runs first. Nothing on the car is set, so there is nothing to put back: a car that is no longer
    /// named simply gathers speed again as the traffic system has it, and a fault here leaves every car
    /// driving. While the traffic system is itself stopping the car (its sensor has the person, or a car,
    /// or a wall) nothing is added: that stop is the game's, and it is what holds the car at rest.
    ///
    /// COST. Switched off (no physics callback at all) while no car is named. Otherwise, for each named
    /// car each physics step: its action (a field), its velocity, rotation and position, a dozen
    /// multiplications and one AddForce: about half a microsecond a car, four at the most for all eight.
    /// Nothing allocated.
    /// </summary>
    internal sealed class SolidBrakes : MonoBehaviour
    {
        private struct Slot
        {
            public VehicleComponent Car;
            public Rigidbody Body;
            public float X, Z;           // the person
            public float Nose;           // metres from the car's origin to its nose
            public float Gap;
            public float Power;          // the traffic system's acceleration step for this car, m/s a physics step
            public bool Owed;            // an accident is owed, and this is the car: only what the tyres give
            public bool Braking;         // the driver has begun to brake for this person
        }

        private readonly Slot[] _slots = new Slot[SolidRules.Brakes];
        private readonly Slot[] _before = new Slot[SolidRules.Brakes];
        private int _count;
        private int _was;

        /// <summary>Cars named now.</summary>
        public int Count => _count;

        private void Awake()
        {
            enabled = false;
        }

        /// <summary>Starts naming the cars afresh (the ones named before are remembered until End, for what they had begun).</summary>
        public void Begin()
        {
            for (int i = 0; i < _count; i++)
            {
                _before[i] = _slots[i];
            }
            _was = _count;
            _count = 0;
        }

        /// <summary>True if the driver of that car had begun to brake when the cars were last named.</summary>
        public bool Was(VehicleComponent car)
        {
            for (int i = 0; i < _was; i++)
            {
                if ((object)_before[i].Car == car)
                {
                    return _before[i].Braking;
                }
            }
            return false;
        }

        /// <summary>Names a car and the person ahead of it. False if eight are named already.</summary>
        public bool Add(VehicleComponent car, float personX, float personZ, float nose, float gap, bool owed)
        {
            if (_count >= _slots.Length || car == null || car.rb == null)
            {
                return false;
            }
            _slots[_count++] = new Slot
            {
                Car = car,
                Body = car.rb,
                X = personX,
                Z = personZ,
                Nose = nose,
                Gap = gap,
                Power = car.GetPowerStep(),
                Owed = owed,
                Braking = Was(car)
            };
            return true;
        }

        /// <summary>The naming is done: the physics callback runs only if anybody was named.</summary>
        public void End()
        {
            for (int i = _count; i < _slots.Length; i++)
            {
                _slots[i] = default;
            }
            for (int i = 0; i < _before.Length; i++)
            {
                _before[i] = default;
            }
            _was = 0;
            enabled = _count > 0;
        }

        /// <summary>Nobody is braked for any more.</summary>
        public void Clear()
        {
            _count = 0;
            End();
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Traffic: SolidBrakes.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            float step = Time.fixedDeltaTime;
            for (int i = 0; i < _count; i++)
            {
                ref Slot slot = ref _slots[i];
                Rigidbody body = slot.Body;
                if (body == null || body.isKinematic)
                {
                    continue;
                }
                // While the traffic system is stopping the car itself, or backing it, its stop is the one that counts.
                SpecialDriveActionTypes action = slot.Car.GetCurrentAction();
                bool forward = action == SpecialDriveActionTypes.Forward;
                if (!forward && action != SpecialDriveActionTypes.Continue && action != SpecialDriveActionTypes.Follow && action != SpecialDriveActionTypes.Overtake
                    && action != SpecialDriveActionTypes.GiveWay && action != SpecialDriveActionTypes.StopInPoint)
                {
                    continue;
                }
                Vector3 heading = body.rotation * Vector3.forward;
                float flat = Mathf.Sqrt(heading.x * heading.x + heading.z * heading.z);
                if (flat < 0.5f)
                {
                    continue;   // on its side or its nose: nobody is driving it
                }
                float fx = heading.x / flat;
                float fz = heading.z / flat;
                Vector3 velocity = body.velocity;
                float speed = velocity.x * fx + velocity.z * fz;
                Vector3 at = body.position;
                float distance = (slot.X - at.x) * fx + (slot.Z - at.z) * fz - slot.Nose - SolidRules.PersonRadius;
                if (distance < -1.5f)
                {
                    continue;   // behind its nose by now: it has passed them
                }
                if (speed <= 0.02f)
                {
                    // At rest at the gap with nothing but this holding it (the game's sensor does not have the person):
                    // the step the traffic system is about to add is taken off, so that it stays at rest and does not creep.
                    if (forward && slot.Braking && speed > -0.02f && distance <= slot.Gap + 0.5f)
                    {
                        float hold = speed + slot.Power;
                        body.AddForce(new Vector3(-fx * hold, 0f, -fz * hold), ForceMode.VelocityChange);
                    }
                    continue;
                }
                float braking = SolidRules.Brake(speed, distance, slot.Gap, slot.Braking, slot.Owed);
                if (braking <= 0f)
                {
                    continue;
                }
                slot.Braking = true;
                float off = braking * step;
                if (off > speed)
                {
                    off = speed;
                }
                // Driving on, the traffic system adds its acceleration step in the same physics step: that is taken off
                // as well, so the two come to exactly the braking wanted (and to rest, when that is where it leads).
                // In any other state it may be braking a little itself: then never more is taken than the car has.
                float change = forward ? off + slot.Power : Mathf.Min(off + slot.Power, speed);
                body.AddForce(new Vector3(-fx * change, 0f, -fz * change), ForceMode.VelocityChange);
            }
        }
    }
}
