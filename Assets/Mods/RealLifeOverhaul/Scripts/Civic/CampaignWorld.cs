using System;
using System.Collections.Generic;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The campaign in the street near the player: posters and boards put up out of view (more for the poll leader), a
    /// canvasser, rallies, and on election day the polling place's sign. Cost: nothing per frame; a one-second tick.
    /// </summary>
    public sealed partial class CampaignWorld
    {
        private const int MaxPosters = 8;
        private const int MaxBoards = 3;
        private const float PosterGap = 14f;   // metres between two posters, or two boards
        private const float TooFar = 160f;   // metres
        private const float CanvasserReach = 2.4f;   // metres
        private const float RallyReach = 8f;   // metres
        private const float RallyAttend = 20f;      // seconds of standing there that count as having been
        private const float PollReach = 2.2f;   // metres
        private const float BoxTop = 0.30f;   // metres: the soapbox's top
        // What the speaker does once somebody listens: speech_gesture is made for this stage (the box, listeners 2.3 m ahead
        // and 0.30 m lower); talk_a where the bundle lacks it.
        private const string SpeechClip = "speech_gesture";
        private const string SpeechFallback = "talk_a";

        private enum Rally { None, Gathering, On, Over }

        private readonly CivicProps _props;
        private readonly CivicGovernment _government;
        private readonly CivicCity _city;
        private readonly CivicCast _cast;
        private readonly CivicCrew _crew;
        private readonly List<Transform> _posters = new List<Transform>(MaxPosters);
        private readonly List<Transform> _boards = new List<Transform>(MaxBoards);
        private readonly List<Transform> _stage = new List<Transform>(4);   // the box and the boards at a rally, or the canvasser's board
        private readonly Vector3[] _listeners = new Vector3[3];
        private float _nextDressing;
        private float _nextPeople;

        private bool _canvasser;
        private int _canvasserFor;      // a candidate's slot, or OfficeRules.OwnCampaign
        private Vector3 _canvasserAt;
        private Vector3 _stageAt;       // where the things in _stage stand
        private bool _canvasserKitted;
        private float _nextOffer;

        private Rally _rally;
        private int _rallyFor = -1;
        private Vector3 _rallyAt;
        private int _rallyDay;          // the day a rally was last held (or given up)
        private float _attended;
        private float _nextClap;
        private bool _speaking;         // the speaker has somebody in front of them and is speaking
        private float _rallyEnds;
        private bool _forced;           // a test button asked: the hours of the day do not apply
        private int _stageTries;

        private bool _own;              // the rally being held is the player's own: no speaker but them
        private bool _ownArrived;
        private float _ownGone;         // when the crowd at the player's rally stops waiting, or goes after the speech
        private int _ownColour;

        // The player's own campaign, set by the director.
        public Func<float> OwnShare;                    // the share of posters and boards that are theirs in a council race
        public Func<int> OwnColour;
        public Func<Candidate> OwnCandidate;            // the player as a candidate, for their own canvasser's leaflet
        public Func<int, Action<bool>, bool> OwnArrived;   // at their rally: the speech is theirs (listeners; what to call after)

        private Transform _pollSign;
        private Vector3 _pollStand;
        private float _nextPollLook;

        public CampaignWorld(CivicProps props, CivicGovernment government, CivicCity city, CivicCast cast)
        {
            _props = props;
            _government = government;
            _city = city;
            _cast = cast;
            _crew = new CivicCrew(cast);
        }

        /// <summary>True while a rally wants (or has) the cast: the site crew stands down.</summary>
        public bool NeedsCast => _rally == Rally.Gathering || _rally == Rally.On;

        /// <summary>True while a rally is on, and where: for the city map.</summary>
        public bool RallyOn(out Vector3 at)
        {
            at = _rallyAt;
            return _rally == Rally.On;
        }

        public int PeopleOut => _crew.Count;

        public int Dressing => _posters.Count + _boards.Count;

        /// <summary>True while the player stands in front of a canvasser.</summary>
        public bool CanvasserInReach(Vector3 player)
        {
            return _canvasser && _crew.First != null && CivicCity.Flat(_crew.First.Position - player) < CanvasserReach * CanvasserReach;
        }

        public bool PollingInReach(Vector3 player)
        {
            return _pollSign != null && Mathf.Abs(_pollStand.y - player.y) < 2f && CivicCity.Flat(_pollStand - player) < PollReach * PollReach;
        }

        /// <param name="onFoot">The player is on foot out of doors (a canvasser speaks to them; a rally counts their standing there).</param>
        /// <param name="siteCrewOut">The construction crew has people out: a rally waits for them to go.</param>
        public void Tick(float now, Vector3 player, int day, int hour, bool onFoot, bool siteCrewOut)
        {
            bool campaigning = _government.Campaigning;
            _crew.Tick(now);
            Dress(now, player, campaigning);
            Polling(now, player, day, campaigning);
            // What stood with people who have gone (a box, a board) is taken away once nobody is looking.
            if (_stage.Count > 0 && _crew.Count == 0 && !_canvasser && _rally != Rally.On && (!CitySpots.InView(_stageAt) || CivicCity.Flat(_stageAt - player) > TooFar * TooFar))
            {
                _props.ReturnAll(_stage);
            }
            if (!campaigning)
            {
                _forced = false;
                if (_canvasser || _rally != Rally.None || _crew.Count > 0)
                {
                    EndPeople();
                    if (_rally == Rally.Over && _crew.Count == 0 && _stage.Count == 0)
                    {
                        _rally = Rally.None;
                    }
                }
                return;
            }
            TickRally(now, player, day, hour, onFoot, siteCrewOut);
            TickCanvasser(now, player, hour, onFoot);
        }

        public void Clear()
        {
            _props.ReturnAll(_posters);
            _props.ReturnAll(_boards);
            _props.ReturnAll(_stage);
            _props.Return(_pollSign);
            _pollSign = null;
            _crew.Release();
            _canvasser = false;
            _rally = Rally.None;
            _rallyFor = -1;
            _forced = false;
            _own = false;
        }

        /// <summary>The player's own rally, paid from their fund: a box and a crowd near them, out of view. False if a rally or a street scene is on.</summary>
        public bool OwnRally()
        {
            if (_rally != Rally.None || CivicBridges.SceneOn() || CivicBridges.WorkStopped() || BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            _own = true;
            _ownArrived = false;
            _ownColour = Colour();
            _rallyFor = -1;
            _rally = Rally.Gathering;
            _nextPeople = 0f;
            _stageTries = 0;
            return true;
        }

        private Candidate Own()
        {
            try
            {
                return OwnCandidate != null ? OwnCandidate() : null;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return null;
            }
        }

        private int Colour()
        {
            try
            {
                return OwnColour != null ? Mathf.Abs(OwnColour()) % PosterIds.Length : 0;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return 0;
            }
        }

        /// <summary>A wall in front of the player where a poster of theirs can go: five rays, asked once a second while they hold posters.</summary>
        public bool WallAhead(Vector3 player, Vector3 forward, out Vector3 at, out Vector3 normal)
        {
            at = normal = Vector3.zero;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f || !CivicSpots.Wall(player, forward.normalized, 1.55f, 1.6f, out RaycastHit middle) || middle.normal.y > 0.2f)
            {
                return false;
            }
            Vector3 outward = new Vector3(middle.normal.x, 0f, middle.normal.z).normalized;
            Vector3 stand = middle.point + outward * 0.6f;
            stand.y = player.y;
            Vector3 across = Vector3.Cross(Vector3.up, outward) * 0.27f;
            float distance = 0.6f;
            if (!Agrees(stand, outward, 1.95f, distance) || !Agrees(stand, outward, 1.15f, distance)
                || !Agrees(stand + across, outward, 1.55f, distance) || !Agrees(stand - across, outward, 1.55f, distance))
            {
                return false;
            }
            at = middle.point + middle.normal * 0.012f;
            normal = middle.normal;
            for (int i = 0; i < _posters.Count; i++)
            {
                if (_posters[i] != null && (_posters[i].position - at).sqrMagnitude < 4f)
                {
                    return false;   // one is up here already
                }
            }
            return true;
        }

        /// <summary>The player puts one of their posters up where <see cref="WallAhead"/> found room. The furthest of the campaign's makes way if there are too many.</summary>
        public bool PutUp(Vector3 at, Vector3 normal, Vector3 player)
        {
            if (_posters.Count >= MaxPosters)
            {
                int far = -1;
                float farSqr = -1f;
                for (int i = 0; i < _posters.Count; i++)
                {
                    float sqr = _posters[i] != null ? CivicCity.Flat(_posters[i].position - player) : float.MaxValue;
                    if (sqr > farSqr)
                    {
                        farSqr = sqr;
                        far = i;
                    }
                }
                if (far >= 0)
                {
                    _props.Return(_posters[far]);
                    _posters.RemoveAt(far);
                }
            }
            Transform poster = _props.Stand(PosterIds[Colour()], at, normal);
            if (poster == null)
            {
                return false;
            }
            _posters.Add(poster);
            return true;
        }

        public void ForceRally()
        {
            _forced = true;
            _nextPeople = 0f;
            _stageTries = 0;
            if (_rally == Rally.None)
            {
                _rallyFor = _government.Leader();
                _rally = Rally.Gathering;
            }
        }

        /// <summary>Test button: posters and boards go up round the player now, in view or not, and a canvasser is sent for.</summary>
        public void Burst(Vector3 player)
        {
            for (int i = 0; i < 24 && _posters.Count < MaxPosters; i++)
            {
                TryPoster(player, false, 8f, 45f);
            }
            for (int i = 0; i < 8 && _boards.Count < MaxBoards; i++)
            {
                TryBoard(player, false, 8f, 40f);
            }
            _nextPeople = 0f;
            _forced = true;
        }

        private void TickCanvasser(float now, Vector3 player, int hour, bool onFoot)
        {
            if (_canvasser)
            {
                CityPerson person = _crew.Count > 0 ? _crew.First : null;
                bool done = _crew.Count == 0 || _crew.Disturbed || (!_forced && (hour < 10 || hour >= 18)) || CivicBridges.WorkStopped() || NeedsCast
                            || CivicCity.Flat(_canvasserAt - player) > 130f * 130f;
                if (done)
                {
                    EndPeople();
                    return;
                }
                if (person == null)
                {
                    return;   // still walking in
                }
                if (!_canvasserKitted)
                {
                    _canvasserKitted = true;
                    _crew.Carry("leaflet_offer");   // one leaflet in the right hand, the stack in the left
                }
                if (now >= _nextOffer && SomeoneAhead(person, 4.2f, 0.6f, onFoot ? player : (Vector3?)null))
                {
                    _crew.Once(0, "leaflet_offer", 2.6f);
                    _nextOffer = now + Random.Range(5f, 9f);
                }
                return;
            }
            if (_rally != Rally.None || now < _nextPeople || (!_forced && (hour < 10 || hour >= 18)) || CivicBridges.WorkStopped() || _crew.Count > 0 || _stage.Count > 0)
            {
                return;
            }
            _nextPeople = now + 15f;
            if (!_cast.Ready(1, player, now))
            {
                _nextPeople = now + 3f;
                return;
            }
            if (!CitySpots.PavementRing(player, 28f, 65f, true, out Vector3 spot) || !CitySpots.Stand(spot, 0.7f, out spot) || !CitySpots.Along(spot, out Vector3 along))
            {
                return;
            }
            int candidate = DrawCandidate();
            if (!_crew.Add(CityLook.Canvasser, spot, spot + along, "leaflet_idle", null))
            {
                return;
            }
            _canvasser = true;
            _canvasserKitted = false;
            _canvasserFor = candidate;
            _canvasserAt = spot;
            _stageAt = spot;
            // Their candidate's board beside them, if there is room for it.
            Vector3 right = Vector3.Cross(Vector3.up, along);
            if (CitySpots.Stand(spot + right * 0.95f - along * 0.2f, 0.4f, out Vector3 beside) || CitySpots.Stand(spot - right * 0.95f - along * 0.2f, 0.4f, out beside))
            {
                Transform board = _props.Stand(BoardIds[Look(candidate) % BoardIds.Length], beside, along);
                if (board != null)
                {
                    _stage.Add(board);
                }
            }
        }

        /// <summary>The player takes a leaflet: the canvasser holds one out, and what it says is on screen.</summary>
        public void TakeLeaflet()
        {
            if (!_canvasser || _crew.First == null)
            {
                return;
            }
            _crew.Once(0, "leaflet_offer", 2.6f);
            GovState state = _government.State;
            Candidate candidate = _canvasserFor == OfficeRules.OwnCampaign ? Own()
                : _canvasserFor >= 0 && _canvasserFor < state.CandidateCount ? state.Candidates[_canvasserFor] : null;
            if (candidate == null)
            {
                return;
            }
            CivicText.Toast(NotificationType.Info, "reallife_civic_leaflet", "name", CivicText.Name(candidate), "party", CivicText.Party(candidate),
                "slogan", CivicText.T(CivicCatalogue.Slogans[candidate.Slogan]), 12f);
        }

        // The canvasser or the rally's people walk off. What stood with them is taken away by Tick once they have gone and nobody is looking.
        private void EndPeople()
        {
            if (_crew.Count > 0)
            {
                _crew.Dismiss();
            }
            _canvasser = false;
            if (_rally == Rally.On)
            {
                _rally = Rally.Over;
            }
            else if (_rally == Rally.Gathering && !_government.Campaigning)
            {
                _rally = Rally.None;
            }
        }

        // Somebody on foot in front of a person: the player, or one of the game's pedestrians.
        private static bool SomeoneAhead(CityPerson person, float distance, float cosine, Vector3? player)
        {
            Vector3 from = person.Position, forward = person.Forward;
            if (player.HasValue && Ahead(player.Value - from, forward, distance, cosine))
            {
                return true;
            }
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return false;
            }
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker != null && walker.isActiveAndEnabled && Ahead(walker.transform.position - from, forward, distance, cosine))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Ahead(Vector3 offset, Vector3 forward, float distance, float cosine)
        {
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            return sqr < distance * distance && sqr > 0.25f && Vector3.Dot(offset, forward) > cosine * Mathf.Sqrt(sqr);
        }
    }
}
