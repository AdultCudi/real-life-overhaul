using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The emergency driver: how it is told to drive, beside a kerb, careless, seeing solids, sliding, swaying or holding at a light.</summary>
    public sealed partial class EmergencyDriver : MonoBehaviour
    {
        /// <summary>Where the carriageway ends on one side (a kerb point, the way towards it): beyond that line nothing is in its way. Begin undoes it.</summary>
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

        /// <summary>A careless drive: braking at most stopping m/s2, seeing that share of the road, perhaps blind to walkers, bends at the grip given.</summary>
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

        /// <summary>Its look also takes in buildings and street furniture, only measured (SolidRoom) while it is steered. For a driver who may leave the lane.</summary>
        public void SeeSolids()
        {
            if (_solidMask == 0)
            {
                _solidMask = LayerMask.GetMask("Buildings", "StreetProps");
            }
            _solids = true;
        }

        /// <summary>A bend lost: from here it is not steered, carries straight on losing speed, and stops by itself. Begin undoes it.</summary>
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
