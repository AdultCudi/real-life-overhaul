using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The foot on the brake of the few cars with somebody ahead: the traffic system stops for an obstacle only within 1.25 m
    /// of its sensor touching it. For up to eight cars named by SolidStreets the braking that stops them a gap short
    /// (SolidRules.Brake) is added each physics step as a change of velocity, the traffic system's own step taken back;
    /// nothing on the car is set. Cost: disabled while none is named; else about half a microsecond a car a physics step.
    /// </summary>
    internal sealed class SolidBrakes : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("streets", "SolidBrakes", typeof(SolidBrakes));

        private struct Slot
        {
            public VehicleComponent Car;
            public Rigidbody Body;
            public float X, Z;           // the person
            public float Nose;           // metres from the car's origin to its nose
            public float Gap;            // metres
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

        /// <summary>True if that car is braked for somebody now (a search of eight).</summary>
        public bool Has(VehicleComponent car)
        {
            for (int i = 0; i < _count; i++)
            {
                if ((object)_slots[i].Car == car)
                {
                    return true;
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
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnFixedUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnFixedUpdate()
        {
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
                    // At rest at the gap with only this holding it: the traffic system's next step is taken off, so it does not creep.
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
                // Driving on, the traffic system adds its acceleration step in the same physics step: that is taken off too,
                // so the two come to exactly the braking wanted; in any other state never more is taken than the car has.
                float change = forward ? off + slot.Power : Mathf.Min(off + slot.Power, speed);
                body.AddForce(new Vector3(-fx * change, 0f, -fz * change), ForceMode.VelocityChange);
            }
        }
    }
}
