using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's drivers' foot on the brake: for up to sixteen cars named by CityDrivers, the slowing that brings each to
    /// the game's own sensor at walking pace, or to rest short of what the sensor does not read and keeps it there
    /// (DriverRules.StepBrake), is added each physics step as a change of velocity, the traffic system's own acceleration
    /// step taken back. Nothing on the car is set, so a car let go of is the game's again at once. Between two looks the room
    /// left is worked out from the speeds. Cost: disabled while none is named; else about half a microsecond a car a physics
    /// step.
    /// </summary>
    internal sealed class DriverBrakes : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("streets", "DriverBrakes", typeof(DriverBrakes));

        public const int Most = 16;      // cars braked at once

        private struct Slot
        {
            public int Index;            // the car's place in the pool; -1 for a free slot
            public VehicleComponent Car;
            public Rigidbody Body;
            public Rigidbody Ahead;      // what it comes up behind, if that has a body that moves (a car, the player's)
            public float Room;           // metres to where it is to be at PointSpeed, as now
            public float PointSpeed;     // m/s
            public float AheadSpeed;     // m/s along its road, as at the look (for what has no body)
            public float Keep;           // metres kept behind it beyond the game's sensor (the player's gap)
            public float Power;          // the traffic system's acceleration step for this car, m/s a physics step
            public float Applied;        // m/s2 put on at the last step
            public float MostApplied;    // ...and the most since it began, for the self-check
            public bool Braking;
            public bool Hold;            // with nothing the game's sensor reads ahead: kept standing once it has come to rest
        }

        private readonly Slot[] _slots = new Slot[Most];
        private int _count;

        /// <summary>Cars named now.</summary>
        public int Count => _count;

        private void Awake()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Index = -1;
            }
            enabled = false;
        }

        /// <summary>
        /// Names a car, or brings what it slows for up to date. False if sixteen are named already. A car named again keeps
        /// what it had begun (its braking comes on smoothly once, not at every look).
        /// </summary>
        public bool Set(int index, VehicleComponent car, float room, float pointSpeed, float aheadSpeed, float keep, Rigidbody ahead, bool hold)
        {
            if (car == null || car.rb == null)
            {
                return false;
            }
            int slot = Find(index);
            if (slot < 0)
            {
                slot = Find(-1);
                if (slot < 0)
                {
                    return false;
                }
                _slots[slot] = new Slot { Index = index, Car = car, Body = car.rb, Power = car.GetPowerStep() };
                _count++;
            }
            ref Slot s = ref _slots[slot];
            s.Room = room;
            s.PointSpeed = pointSpeed;
            s.AheadSpeed = aheadSpeed;
            s.Keep = keep;
            s.Ahead = ahead != null && !ahead.isKinematic ? ahead : null;
            s.Hold = hold;
            enabled = true;
            return true;
        }

        /// <summary>That car is no longer slowed; the most it was braked with since it was named, m/s2 (0 if it was not).</summary>
        public float Drop(int index)
        {
            int slot = Find(index);
            if (slot < 0)
            {
                return 0f;
            }
            float most = _slots[slot].MostApplied;
            _slots[slot] = new Slot { Index = -1 };
            _count--;
            enabled = _count > 0;
            return most;
        }

        /// <summary>The most that car has been braked with since it was named, m/s2; 0 if it is not named.</summary>
        public float MostOn(int index)
        {
            int slot = Find(index);
            return slot >= 0 ? _slots[slot].MostApplied : 0f;
        }

        /// <summary>True if that car is named.</summary>
        public bool Has(int index) => Find(index) >= 0;

        /// <summary>Nobody is braked any more.</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new Slot { Index = -1 };
            }
            _count = 0;
            enabled = false;
        }

        private int Find(int index)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Index == index)
                {
                    return i;
                }
            }
            return -1;
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Traffic: DriverBrakes.FixedUpdate");
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
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Index >= 0)
                {
                    Step(ref _slots[i], step);
                }
            }
        }

        private static void Step(ref Slot slot, float step)
        {
            Rigidbody body = slot.Body;
            VehicleComponent car = slot.Car;
            if (body == null || car == null || body.isKinematic || !car.gameObject.activeInHierarchy)
            {
                return;   // gone back to the pool, or held by somebody of the mod: the next look lets it go
            }
            SpecialDriveActionTypes action = car.GetCurrentAction();
            if (DriverGame.GameStops(action))
            {
                return;   // the traffic system is stopping it (its sensor has what is ahead) or backing it: its own stop counts
            }
            Vector3 heading = body.rotation * Vector3.forward;
            float flat = Mathf.Sqrt(heading.x * heading.x + heading.z * heading.z);
            if (flat < 0.5f)
            {
                return;   // on its side or its nose: nobody is driving it
            }
            float fx = heading.x / flat;
            float fz = heading.z / flat;
            Vector3 velocity = body.velocity;
            float speed = velocity.x * fx + velocity.z * fz;
            float ahead = slot.AheadSpeed;
            if (slot.Ahead != null)
            {
                Vector3 going = slot.Ahead.velocity;
                ahead = going.x * fx + going.z * fz;
            }
            float braking = DriverRules.StepBrake(ref slot.Room, ref slot.Braking, ref slot.Applied, speed, ahead, slot.PointSpeed, slot.Keep, slot.Hold, step);
            if (speed <= 0.05f)
            {
                if (slot.Hold && slot.Room <= DriverRules.HoldWithin && speed > -0.05f)
                {
                    // At rest short of something the game's sensor cannot hold it for: the traffic system's next step is taken off.
                    float hold = speed + slot.Power;
                    body.AddForce(new Vector3(-fx * hold, 0f, -fz * hold), ForceMode.VelocityChange);
                }
                return;
            }
            if (braking <= 0f)
            {
                return;
            }
            if (braking > slot.MostApplied)
            {
                slot.MostApplied = braking;
            }
            // In every state it is driven on in, the traffic system adds its acceleration step this physics step: that is
            // taken off too, so the two come to the braking wanted (and never more than the car has).
            float off = braking * step + slot.Power;
            if (off > speed)
            {
                off = speed;
            }
            body.AddForce(new Vector3(-fx * off, 0f, -fz * off), ForceMode.VelocityChange);
        }
    }
}
