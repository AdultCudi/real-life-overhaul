using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The emergency driver: how it is told to drive, beside a kerb, careless, seeing solids, sliding, swaying or holding at a light.</summary>
    public sealed partial class EmergencyDriver : MonoBehaviour
    {
        /// <summary>
        /// Where the carriageway ends on one side: a point of the kerb and the way across towards it. Whatever is beyond that
        /// line is on the pavement and not in its way while it turns in beside it. Begin, and <see cref="NoKerb"/>, undo it.
        /// </summary>
        public void Kerb(Vector3 point, Vector3 towards)
        {
            towards.y = 0f;
            _kerbed = towards.sqrMagnitude > 0.01f;
            if (_kerbed)
            {
                _kerbTowards = towards.normalized;
                _kerbAt = point.x * _kerbTowards.x + point.z * _kerbTowards.z;
            }
        }

        public void NoKerb()
        {
            _kerbed = false;
        }

        /// <summary>
        /// Makes this drive a careless one: braking with at most stopping m/s2 (and planning to, so late), taking in only that
        /// share of the road ahead, perhaps blind to people on foot, and planning bends and hard turns with the grip given; lanes
        /// to pass in come from RoadMap's grid. The mod's people, bodies and the player on foot are always seen. Call after Begin.
        /// </summary>
        public void Careless(float stopping, float foresight, bool seesPeople, float bendGrip)
        {
            _stopping = Mathf.Clamp(stopping, 1f, Slowing);
            _foresight = Mathf.Clamp(foresight, 0.05f, 1f);
            _seesPeople = seesPeople;
            _bendGrip = Mathf.Max(0f, bendGrip);
            _careful = _bendGrip > 0f ? Mathf.Max(CarefulSpeed, Mathf.Sqrt(_bendGrip * 9f)) : CarefulSpeed;   // (a junction's turn is about 9 m round)
            _gridLanes = true;
            if (_blindMask == 0)
            {
                _blindMask = TrafficTools.VehicleMask | LayerMask.GetMask("Player");
            }
        }

        /// <summary>
        /// The look ahead also takes in buildings and street furniture, with the same query and a wider mask, for a driver who
        /// may leave the lane. While the car is steered such a thing is only measured (<see cref="SolidRoom"/>), as a car turning
        /// at a junction always has a wall somewhere ahead; once it slides (Slide) it counts like anything else. Begin undoes it.
        /// </summary>
        public void SeeSolids()
        {
            if (_solidMask == 0)
            {
                _solidMask = LayerMask.GetMask("Buildings", "StreetProps");
            }
            _solids = true;
        }

        /// <summary>
        /// A bend taken faster than the road holds: from here it is not steered, carries straight on losing speed, and goes on
        /// measuring what is ahead (Room, Ahead). It ends by itself when it has all but stopped. Begin undoes it.
        /// </summary>
        public void Slide()
        {
            _sliding = true;
            _nextSense = 0f;
        }

        /// <summary>Metres between its nose and a building or street furniture straight ahead; float.MaxValue if none, or not looking (SeeSolids).</summary>
        public float SolidRoom => Active ? _solidRoom : float.MaxValue;

        /// <summary>Takes over a moving vehicle at its speed, so nothing shows as it changes hands. Call after Begin and CruiseSpeed.</summary>
        public void Rolling(float speed)
        {
            _speed = Mathf.Clamp(speed, 0f, Mathf.Max(CruiseSpeed, DefaultCruiseSpeed));
        }

        /// <summary>Makes it wander so many metres either side of its line once in so many seconds (a driver who has had a drink); 0 ends it.</summary>
        public void Sway(float metres, float every)
        {
            _sway = Mathf.Max(0f, metres);
            _swayEvery = Mathf.Max(1f, every);
        }

        /// <summary>The route point it must stop short of for now (a red light), or -1: it slows in at an ordinary pace and waits until -1 again.</summary>
        public int HoldAt
        {
            get => _holdAt;
            set
            {
                _holdAt = value;
                if (value < 0)
                {
                    _hold = float.MaxValue;
                }
            }
        }

        /// <summary>True while it stands at the point it was told to hold at.</summary>
        public bool Holding => Active && _holdAt >= 0 && _hold <= 0f && _speed < 0.3f;
    }
}
