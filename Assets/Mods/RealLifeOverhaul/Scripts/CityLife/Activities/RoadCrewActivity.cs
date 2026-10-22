using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Street works on about seven metres of pavement, a crew of three with cones and a toolbox; never the carriageway (Traffic/SceneCordon's).</summary>
    public sealed class RoadCrewActivity : CityActivity
    {
        private const float DigAhead = 0.85f;      // metres: the digger stands this far short of the hole
        private const float HammerAhead = 1.55f;   // metres: the jackhammer this far beyond it, facing back
        private const float FlagAhead = 3.0f;   // metres: the paddle this far short of it
        private const int Workers = 2;             // the two who alternate work and rest

        private static readonly string[] WorkClips = { "shovel_dig", "jackhammer" };
        private static readonly string[] RestClips = { "shovel_rest", "jackhammer_idle" };

        private readonly Vector3[] _stands = new Vector3[3];
        private readonly Vector3[] _faces = new Vector3[3];
        private readonly Vector3[] _cones = new Vector3[4];
        private readonly float[] _until = new float[Workers];
        private readonly bool[] _resting = new bool[Workers];
        private int _coneCount;
        private Vector3 _toolbox;
        private bool _hasToolbox;
        private float _heading;

        public override bool Find(CitySite site)
        {
            if (!CitySpots.Stand(site.Anchor, 0.6f, out Vector3 centre) || !CitySpots.Along(centre, out Vector3 along))
            {
                return false;
            }
            Vector3 across = Vector3.Cross(Vector3.up, along);
            if (!CitySpots.Stand(centre - along * DigAhead, 0.5f, out _stands[0]) || !CitySpots.Stand(centre + along * HammerAhead, 0.5f, out _stands[1])
                || !CitySpots.Stand(centre - along * FlagAhead, 0.5f, out _stands[2]) || !CitySpots.Stand(centre, 0.4f, out _))
            {
                return false;
            }
            _faces[0] = centre;
            _faces[1] = centre;
            _faces[2] = _stands[2] - along;   // outwards, at whoever is coming

            // Cones: one at each end is the least; beside the hole where the pavement is wide enough.
            _coneCount = 0;
            Cone(centre + along * (HammerAhead + 1.5f));
            Cone(centre - along * (FlagAhead + 1.1f));
            if (_coneCount < 2)
            {
                return false;
            }
            Cone(centre + across * 1.05f + along * 0.5f);
            Cone(centre - across * 1.05f + along * 0.5f);
            _hasToolbox = CitySpots.Stand(centre + across * 0.8f - along * 0.9f, 0.3f, out _toolbox) || CitySpots.Stand(centre - across * 0.8f - along * 0.9f, 0.3f, out _toolbox);
            _heading = Quaternion.LookRotation(along).eulerAngles.y;
            Centre = centre;
            return true;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            for (int i = 0; i < 3; i++)
            {
                if (Add(CityLook.RoadWorker, _stands[i], _faces[i]) == null)
                {
                    End();
                    return false;
                }
            }
            if (!Cast[0].Work(WorkClips[0], Props, 0f) || !Cast[1].Work(WorkClips[1], Props, 0f) || !Cast[2].Work("paddle_hold", Props, 0f))
            {
                End();
                return false;
            }
            for (int i = 0; i < _coneCount; i++)
            {
                if (!Put("cone", _cones[i], Random.value * 360f))
                {
                    End();
                    return false;
                }
            }
            if (_hasToolbox)
            {
                Put("toolbox", _toolbox, _heading + Random.Range(60f, 120f));
            }
            for (int i = 0; i < Workers; i++)
            {
                _resting[i] = false;
                _until[i] = In(8f, 16f) + i * 3f;   // never both stopping at the same moment
            }
            return true;
        }

        public override bool Tick(float now)
        {
            if (!Lasting())
            {
                return false;
            }
            for (int i = 0; i < Workers && !Over; i++)
            {
                CityPerson worker = Cast[i];
                if (worker.Lost || now < _until[i])
                {
                    continue;
                }
                // Work, lean on the tool and look about, work again.
                if (worker.Work(_resting[i] ? WorkClips[i] : RestClips[i], Props, 0.5f))
                {
                    _resting[i] = !_resting[i];
                }
                _until[i] = _resting[i] ? In(4f, 8f) : In(9f, 18f);
            }
            return true;
        }

        private void Cone(Vector3 place)
        {
            if (_coneCount < _cones.Length && CitySpots.Stand(place, 0.3f, out Vector3 spot))
            {
                _cones[_coneCount++] = spot;
            }
        }
    }
}
