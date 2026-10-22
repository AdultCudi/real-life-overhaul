using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Two people stopped to talk a pace apart, one talking with their hands while the other listens, then the other way
    /// round. Two are placed or neither is.
    /// </summary>
    public sealed class ChatActivity : CityActivity
    {
        private const float Apart = 1.05f;   // metres

        private Vector3 _first;
        private Vector3 _second;
        private bool _firstTalks;
        private float _until;

        public override bool Find(CitySite site)
        {
            if (!CitySpots.Stand(site.Anchor, 0.9f, out Vector3 centre))
            {
                return false;
            }
            if (!CitySpots.Along(centre, out Vector3 along))
            {
                along = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
            }
            // A little off the line of the pavement, so they do not look drilled.
            along = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * along;
            if (!CitySpots.Stand(centre - along * (Apart * 0.5f), 0.4f, out _first) || !CitySpots.Stand(centre + along * (Apart * 0.5f), 0.4f, out _second))
            {
                return false;
            }
            Centre = centre;
            return true;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            if (Add(CityLook.Civilian, _first, _second) == null || Add(CityLook.Civilian, _second, _first) == null)
            {
                End();
                return false;
            }
            _firstTalks = Random.value < 0.5f;
            if (!Speak(0f))
            {
                End();
                return false;
            }
            return true;
        }

        public override bool Tick(float now)
        {
            if (!Lasting())
            {
                return false;
            }
            if (!Over && now >= _until)
            {
                _firstTalks = !_firstTalks;
                Speak(0.6f);
            }
            return true;
        }

        private bool Speak(float fade)
        {
            _until = In(6f, 12f);
            return Cast[_firstTalks ? 0 : 1].Work("talk_a", Props, fade) & Cast[_firstTalks ? 1 : 0].Work("talk_b", Props, fade);
        }
    }
}
