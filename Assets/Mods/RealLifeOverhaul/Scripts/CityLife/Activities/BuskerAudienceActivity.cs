using Entities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// An audience for one of the game's own street performers (the guitarist, the singer, the
    /// bucket drummer, the juggler: the game has them, with their music and a donation bucket
    /// the player can already tip into). Two or three people stand in an arc in front of the
    /// performer, facing them, moving to the music, and now and then they all clap.
    ///
    /// The performer is found, not assumed: among the things the player can interact with near
    /// the player, one that is a StreetPerformer and is out; and each listener's place is tested
    /// like anybody else's. When the performer packs up (rain, the hour), the audience ends
    /// when nobody is looking.
    /// </summary>
    public sealed class BuskerAudienceActivity : CityActivity
    {
        private const float SearchRadius = 70f;
        private const int MaxListeners = 3;

        private static readonly Collider[] Found = new Collider[96];
        private static readonly float[] Bearings = { 0f, -38f, 38f, -70f, 70f };
        private static int _mask;

        private readonly Vector3[] _stands = new Vector3[MaxListeners];
        private StreetPerformer _performer;
        private int _count;
        private float _next;
        private bool _clapping;

        public override bool Find(CitySite site)
        {
            if (_mask == 0)
            {
                _mask = LayerMask.GetMask("InteractiveItems", "InteractiveItemsOutlined", "Human", "Default");
            }
            _performer = null;
            int hits = Physics.OverlapSphereNonAlloc(site.Player, SearchRadius, Found, _mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits && _performer == null; i++)
            {
                StreetPerformer performer = Found[i].GetComponentInParent<StreetPerformer>();
                if (performer == null || !performer.isActiveAndEnabled || (!site.Test && CitySpots.InView(performer.transform.position)))
                {
                    continue;
                }
                // An arc in front of the performer, two and a half to three metres off.
                Transform stage = performer.transform;
                _count = 0;
                for (int k = 0; k < Bearings.Length && _count < MaxListeners; k++)
                {
                    Vector3 place = stage.position + Quaternion.Euler(0f, Bearings[k], 0f) * stage.forward * Random.Range(2.5f, 3.1f);
                    if (CitySpots.Stand(place, 0.45f, out Vector3 spot))
                    {
                        _stands[_count++] = spot;
                    }
                }
                if (_count >= 2)
                {
                    _performer = performer;
                    Centre = stage.position;
                }
            }
            for (int i = 0; i < hits; i++)
            {
                Found[i] = null;
            }
            return _performer != null;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            if (_performer == null)
            {
                return false;
            }
            Vector3 stage = _performer.transform.position;
            for (int i = 0; i < _count; i++)
            {
                CityPerson listener = Add(CityLook.Civilian, _stands[i], stage);
                if (listener != null && !listener.Work("watch_nod", Props, 0f))
                {
                    End();
                    return false;
                }
            }
            if (Cast.Count < 2)
            {
                End();   // an audience of one is somebody loitering
                return false;
            }
            _clapping = false;
            _next = In(12f, 24f);
            return true;
        }

        public override bool Tick(float now)
        {
            if (_performer == null || !_performer.isActiveAndEnabled)
            {
                Over = true;   // the show is over
            }
            if (!Lasting())
            {
                return false;
            }
            if (!Over && now >= _next)
            {
                _clapping = !_clapping;
                for (int i = 0; i < Cast.Count; i++)
                {
                    if (!Cast[i].Lost)
                    {
                        Cast[i].Work(_clapping ? "clap" : "watch_nod", Props, 0.4f);
                    }
                }
                _next = _clapping ? In(4f, 7f) : In(16f, 30f);
            }
            return true;
        }

        public override void End()
        {
            _performer = null;
            base.End();
        }
    }
}
