using System;
using System.Collections.Generic;
using Characters.EmojiSystem;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The campaign as it is seen in the street near the player, for the seven days before an
    /// election: posters on shopfront walls, campaign boards on the pavement, a canvasser with
    /// leaflets, a rally round a speaker on a box; and on election day the sign outside the
    /// district's polling place.
    ///
    /// Posters and boards (eight and three at most) are put up one at a time, every few
    /// seconds, on walls and pavement the camera is not looking at, 25 to 90 metres from the
    /// player; a candidate who is ahead in the polls has more of them. Each poster is fitted to
    /// its wall by five rays that must agree to within three centimetres; each board stands at
    /// the foot of a wall with room found for it. Those left more than 160 metres behind are
    /// taken down. When the count is in, everything not in view comes down at once and the rest
    /// the moment nobody is looking.
    ///
    /// The canvasser (one person) and the rally (a speaker and up to three listeners) come from
    /// civic life's own cast, walk in from out of view and walk off again. A rally is held
    /// between 12:00 and 15:00 on the fifth, fourth, second and last day before the election,
    /// for a different candidate each time, wherever the player then is; it does not start
    /// while an accident, a crime scene or a street event is on, and the site crew gives its
    /// people up for it.
    /// </summary>
    public sealed class CampaignWorld
    {
        private const int MaxPosters = 8;
        private const int MaxBoards = 3;
        private const float PosterGap = 14f;
        private const float TooFar = 160f;
        private const float CanvasserReach = 2.4f;
        private const float RallyReach = 8f;
        private const float RallyAttend = 20f;      // seconds of standing there that count as having been
        private const float PollReach = 2.2f;
        private const float BoxTop = 0.30f;
        // What the speaker does once somebody is listening. speech_gesture is made for exactly this stage (CityAssets/README.md):
        // feet within 0.36 x 0.26 m on the soapbox (0.60 x 0.45, top 0.30), listeners 2.3 m ahead, 0.30 m lower and 1.15 m
        // apart, 0.65 m clear either side (the boards stand 1.1 m out and 0.9 m back). talk_a where the bundle lacks it.
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
        private int _canvasserFor;
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

        // ---- the player's own campaign (Civic/CivicOffice)
        private bool _own;              // the rally being held is the player's own: no speaker but them
        private bool _ownArrived;
        private float _ownGone;         // when the crowd at the player's rally stops waiting, or goes after the speech
        private int _ownColour;

        /// <summary>The share of the posters and boards put up that are the player's own in a council race (the mayor's race draws them by its polls).</summary>
        public Func<float> OwnShare;

        /// <summary>The colour of the player's posters and boards.</summary>
        public Func<int> OwnColour;

        /// <summary>The player has reached their own rally: the speech is theirs (the number listening, and what to call when it is over).</summary>
        public Func<int, Action<bool>, bool> OwnArrived;

        private Transform _pollSign;
        private Vector3 _pollStand;
        private District _pollDistrict;
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

        /// <summary>True while a rally is being held, and where: for the city map (World/MapLinks).</summary>
        public bool RallyOn(out Vector3 at)
        {
            at = _rallyAt;
            return _rally == Rally.On;
        }

        public int PeopleOut => _crew.Count;

        public int Dressing => _posters.Count + _boards.Count;

        /// <summary>The candidate whose canvasser the player is standing in front of; -1 if none.</summary>
        public int CanvasserInReach(Vector3 player)
        {
            return _canvasser && _crew.First != null && CivicCity.Flat(_crew.First.Position - player) < CanvasserReach * CanvasserReach ? _canvasserFor : -1;
        }

        /// <summary>True if the player stands at the polling place's sign.</summary>
        public bool PollingInReach(Vector3 player)
        {
            return _pollSign != null && Mathf.Abs(_pollStand.y - player.y) < 2f && CivicCity.Flat(_pollStand - player) < PollReach * PollReach;
        }

        // ---------------------------------------------------------------- once a second

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

        /// <summary>Everything away at once. Safe at any time, any number of times.</summary>
        public void Clear()
        {
            _props.ReturnAll(_posters);
            _props.ReturnAll(_boards);
            _props.ReturnAll(_stage);
            _props.Return(_pollSign);
            _pollSign = null;
            _pollDistrict = null;
            _crew.Release();
            _canvasser = false;
            _rally = Rally.None;
            _rallyFor = -1;
            _forced = false;
            _own = false;
        }

        /// <summary>
        /// The player's own rally (paid for from their fund): a box and a crowd near the player, out of view, for them to
        /// walk to and speak at. False if a rally is on or gathering already, or a street scene is on.
        /// </summary>
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

        /// <summary>
        /// A wall in front of the player where a poster of theirs can go (the same five rays as the campaign's own, from
        /// where they stand). Five rays, once a second, only while the player has posters to put up.
        /// </summary>
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
            if (!Agrees(stand, outward, 1.95f, distance) || !Agrees(stand, outward, 1.15f, distance) || !Agrees(stand + across, outward, 1.55f, distance) || !Agrees(stand - across, outward, 1.55f, distance))
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

        /// <summary>Test button: a rally gathers near the player now, whatever the day and hour.</summary>
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

        // ---------------------------------------------------------------- posters and boards

        private void Dress(float now, Vector3 player, bool campaigning)
        {
            // What is far behind, or (once the campaign is over) not being looked at, comes down.
            for (int i = _posters.Count - 1; i >= 0; i--)
            {
                Transform poster = _posters[i];
                if (poster == null || CivicCity.Flat(poster.position - player) > TooFar * TooFar || (!campaigning && !CitySpots.InView(poster.position)))
                {
                    _props.Return(poster);
                    _posters.RemoveAt(i);
                }
            }
            for (int i = _boards.Count - 1; i >= 0; i--)
            {
                Transform board = _boards[i];
                if (board == null || CivicCity.Flat(board.position - player) > TooFar * TooFar || (!campaigning && !CitySpots.InView(board.position)))
                {
                    _props.Return(board);
                    _boards.RemoveAt(i);
                }
            }
            if (!campaigning || now < _nextDressing)
            {
                return;
            }
            _nextDressing = now + 4f;
            if (_posters.Count < MaxPosters)
            {
                TryPoster(player, true, 25f, 90f);
            }
            else if (_boards.Count < MaxBoards)
            {
                TryBoard(player, true, 25f, 90f);
            }
        }

        // A candidate, more often one who is ahead.
        private int DrawCandidate()
        {
            GovState state = _government.State;
            float own = 0f;
            try
            {
                own = OwnShare != null ? Mathf.Clamp01(OwnShare()) : 0f;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            if (own > 0f && Random.value < own)
            {
                return Colour();   // the player's own, in the council race
            }
            float roll = Random.value, sum = 0f;
            for (int c = 0; c < state.CandidateCount; c++)
            {
                sum += state.Polls[c];
                if (roll < sum)
                {
                    return c;
                }
            }
            return Mathf.Max(0, state.CandidateCount - 1);
        }

        private void TryPoster(Vector3 player, bool unseen, float near, float far)
        {
            if (!CitySpots.PavementRing(player, near, far, unseen, out Vector3 spot) || !CitySpots.Shopfront(spot, 0.9f, 0.35f, out Vector3 stand, out Vector3 outward))
            {
                return;
            }
            // The wall exactly, at the poster's height: the middle, top, bottom and both edges must be one flat surface.
            if (!CivicSpots.Wall(stand, -outward, 1.55f, 1.8f, out RaycastHit middle) || Vector3.Dot(middle.normal, outward) < 0.97f)
            {
                return;
            }
            Vector3 across = Vector3.Cross(Vector3.up, outward) * 0.27f;
            if (!Agrees(stand, outward, 1.95f, middle.distance) || !Agrees(stand, outward, 1.15f, middle.distance)
                || !Agrees(stand + across, outward, 1.55f, middle.distance) || !Agrees(stand - across, outward, 1.55f, middle.distance))
            {
                return;
            }
            Vector3 at = middle.point + middle.normal * 0.012f;
            for (int i = 0; i < _posters.Count; i++)
            {
                if (_posters[i] != null && (_posters[i].position - at).sqrMagnitude < PosterGap * PosterGap)
                {
                    return;
                }
            }
            if (unseen && CitySpots.InView(at))
            {
                return;
            }
            Transform poster = _props.Stand(PosterIds[DrawCandidate() % PosterIds.Length], at, middle.normal);
            if (poster != null)
            {
                _posters.Add(poster);
            }
        }

        private static bool Agrees(Vector3 from, Vector3 outward, float height, float distance)
        {
            return CivicSpots.Wall(from, -outward, height, 1.8f, out RaycastHit hit) && Mathf.Abs(hit.distance - distance) < 0.03f && Vector3.Dot(hit.normal, outward) > 0.97f;
        }

        private void TryBoard(Vector3 player, bool unseen, float near, float far)
        {
            // At the foot of a wall, out of the way of people walking.
            if (!CitySpots.PavementRing(player, near, far, unseen, out Vector3 spot) || !CitySpots.Shopfront(spot, 0.55f, 0.45f, out Vector3 stand, out Vector3 outward)
                || (unseen && CitySpots.InView(stand)) || !CitySpots.OnPavement(stand + outward * 1.2f, out _))
            {
                return;
            }
            for (int i = 0; i < _boards.Count; i++)
            {
                if (_boards[i] != null && (_boards[i].position - stand).sqrMagnitude < PosterGap * PosterGap)
                {
                    return;
                }
            }
            Transform board = _props.Stand(BoardIds[DrawCandidate() % BoardIds.Length], stand, Vector3.Cross(Vector3.up, outward));
            if (board != null)
            {
                _boards.Add(board);
            }
        }

        private static readonly string[] PosterIds = { "campaign_poster_0", "campaign_poster_1", "campaign_poster_2", "campaign_poster_3" };
        private static readonly string[] BoardIds = { "campaign_board_0", "campaign_board_1", "campaign_board_2", "campaign_board_3" };

        // ---------------------------------------------------------------- the polling place

        private void Polling(float now, Vector3 player, int day, bool campaigning)
        {
            bool open = campaigning && day == _government.State.ElectionDay;
            if (_pollSign != null && (!open || CivicCity.Flat(_pollStand - player) > TooFar * TooFar) && (!CitySpots.InView(_pollStand) || !open))
            {
                _props.Return(_pollSign);
                _pollSign = null;
                _pollDistrict = null;
            }
            if (!open || _pollSign != null || now < _nextPollLook)
            {
                return;
            }
            _nextPollLook = now + 5f;
            District district = _city.NearestPolling(player, out float distance);
            if (district == null || distance > 130f)
            {
                return;
            }
            // Beside the door, a few steps along the wall, where the sign has room and the voter a place to stand in front of it.
            if (!CitySpots.Pavement(district.Door, 6f, out Vector3 front) || !CitySpots.Along(front, out Vector3 along))
            {
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                if (CitySpots.Stand(front + along * (side * 2.8f), 0.5f, out Vector3 at) && CitySpots.Stand(at - along * (side * 1.1f), 0.4f, out Vector3 stand))
                {
                    _pollSign = _props.Stand("polling_sign", at, -along * side);
                    if (_pollSign != null)
                    {
                        _pollStand = stand;
                        _pollDistrict = district;
                        CivicDirector.Logger?.Info("Civic: the polling place of " + district.Id + " is open at " + district.PollingAddress + ".");
                    }
                    return;
                }
            }
        }

        /// <summary>The district whose polling place has its sign out; null if none.</summary>
        public District PollingDistrict => _pollSign != null ? _pollDistrict : null;

        // ---------------------------------------------------------------- the canvasser

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
                Transform board = _props.Stand(BoardIds[candidate % BoardIds.Length], beside, along);
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
            Candidate candidate = state.Candidates[_canvasserFor];
            CivicText.Toast(NotificationType.Info, "reallife_civic_leaflet", "name", CivicText.Name(candidate), "party", CivicText.Party(candidate),
                "slogan", CivicText.T(CivicCatalogue.Slogans[candidate.Slogan]), 12f);
        }

        // ---------------------------------------------------------------- the rally

        private void TickRally(float now, Vector3 player, int day, int hour, bool onFoot, bool siteCrewOut)
        {
            GovState state = _government.State;
            switch (_rally)
            {
                case Rally.None:
                {
                    int left = state.ElectionDay - day;
                    if (_rallyDay == day || hour < 12 || hour >= 15 || !(left == 5 || left == 4 || left == 2 || left == 1) || state.CandidateCount == 0
                        || CivicBridges.SceneOn() || CivicBridges.WorkStopped() || BuildingManager.IsInsideBuilding || now < _nextPeople)
                    {
                        return;
                    }
                    _nextPeople = now + 6f;
                    _rallyFor = ((left + state.Number) % state.CandidateCount + state.CandidateCount) % state.CandidateCount;
                    _rally = Rally.Gathering;   // from now on the canvasser and the site crew give their people up
                    return;
                }

                case Rally.Gathering:
                    if ((!_forced && !_own && hour >= 15) || CivicBridges.SceneOn() || CivicBridges.WorkStopped() || _stageTries > 12)
                    {
                        if (_stageTries > 12)
                        {
                            CivicDirector.Logger?.Info("Civic: no pavement wide enough for a rally was found near the player; none today.");
                        }
                        if (_own)
                        {
                            OwnGivenUp();
                        }
                        _rally = Rally.None;
                        _rallyDay = day;
                        _stageTries = 0;
                        _own = false;
                        return;
                    }
                    if (_canvasser)
                    {
                        EndPeople();
                        return;
                    }
                    if (siteCrewOut || _crew.Count > 0 || _stage.Count > 0 || now < _nextPeople || !_cast.Ready(4, player, now))
                    {
                        return;
                    }
                    _nextPeople = now + 6f;
                    _stageTries++;
                    if (Stage(player))
                    {
                        _stageTries = 0;
                        _rally = Rally.On;
                        _attended = 0f;
                        _nextClap = now + Random.Range(18f, 30f);
                        _rallyEnds = now + 420f;
                        if (_own)
                        {
                            _ownGone = now + 600f;   // ten minutes to walk there
                            CivicText.Toast(NotificationType.Info, "reallife_civic_office_rally_ready", "metres", Mathf.RoundToInt(Vector3.Distance(player, _rallyAt)).ToString(), 14f);
                            return;
                        }
                        Candidate candidate = state.Candidates[_rallyFor];
                        CivicText.Toast(NotificationType.Info, "reallife_civic_rally_on", "name", CivicText.Name(candidate),
                            "metres", Mathf.RoundToInt(Vector3.Distance(player, _rallyAt)).ToString(), 12f);
                    }
                    return;

                case Rally.On:
                    if (_own)
                    {
                        TickOwn(now, player, day, onFoot);
                        return;
                    }
                    if ((!_forced && hour >= 15) || now >= _rallyEnds || _crew.Disturbed || _crew.Count == 0 || CivicBridges.SceneOn() || CivicBridges.WorkStopped()
                        || CivicCity.Flat(_rallyAt - player) > 140f * 140f)
                    {
                        _rally = Rally.Over;
                        _rallyDay = day;
                        _crew.Dismiss();
                        return;
                    }
                    bool audience = _crew.Present >= 2 && _crew.First != null;
                    if (audience != _speaking)
                    {
                        _speaking = audience;
                        _crew.SetWork(0, !audience ? "arms_crossed" : ClipLibrary.Has(SpeechClip) ? SpeechClip : SpeechFallback);
                        _nextClap = now + Random.Range(18f, 30f);
                    }
                    if (_speaking && now >= _nextClap)
                    {
                        _nextClap = now + Random.Range(20f, 34f);
                        _crew.AllPlay("clap", 1);
                    }
                    if (onFoot && _crew.Present >= 2 && CivicCity.Flat(_rallyAt - player) < RallyReach * RallyReach)
                    {
                        _attended += 1f;
                        if (_attended >= RallyAttend && _government.AttendRally(_rallyFor))
                        {
                            Candidate candidate = state.Candidates[_rallyFor];
                            CivicText.Toast(NotificationType.Success, "reallife_civic_rally_attended", "name", CivicText.Name(candidate),
                                "slogan", CivicText.T(CivicCatalogue.Slogans[candidate.Slogan]), 12f);
                        }
                    }
                    return;

                default:
                    // Over: the box and the boards go when the people have and nobody is looking (see Tick).
                    if (_crew.Count == 0 && _stage.Count == 0)
                    {
                        _rally = Rally.None;
                        _rallyFor = -1;
                        _own = false;
                    }
                    return;
            }
        }

        // The player's own rally: the crowd waits for them; at the box, the speech is theirs (OfficeScenes); then applause, and the crowd goes.
        private void TickOwn(float now, Vector3 player, int day, bool onFoot)
        {
            bool over = _crew.Disturbed || _crew.Count == 0 || CivicBridges.SceneOn() || now >= _ownGone || CivicCity.Flat(_rallyAt - player) > 160f * 160f;
            if (!over && !_ownArrived && onFoot && _crew.Present >= 2 && CivicCity.Flat(_rallyAt - player) < RallyReach * RallyReach)
            {
                _ownArrived = true;
                bool begun = false;
                try
                {
                    begun = OwnArrived != null && OwnArrived(_crew.Present, Spoken);
                }
                catch (Exception e)
                {
                    CivicDirector.Logger?.Error(e);
                }
                if (!begun)
                {
                    _ownArrived = false;   // a conversation or another window is open: they try again in a second
                }
                return;
            }
            if (over)
            {
                if (!_ownArrived)
                {
                    OwnGivenUp();
                }
                _rally = Rally.Over;
                _rallyDay = day;
                _crew.Dismiss();
            }
        }

        // The speech is over: applause, and the crowd goes in half a minute.
        private void Spoken(bool given)
        {
            if (given)
            {
                _crew.AllPlay("clap", 0);
            }
            _ownGone = Time.time + 30f;
        }

        private void OwnGivenUp()
        {
            CivicText.Toast(NotificationType.Warning, "reallife_civic_office_rally_gone", 10f);
        }

        /// <summary>The candidate whose rally is on within reach of the player; -1 if none.</summary>
        public int RallyInReach(Vector3 player)
        {
            return _rally == Rally.On && CivicCity.Flat(_rallyAt - player) < RallyReach * RallyReach ? _rallyFor : -1;
        }

        // Finds a wide piece of pavement out of view, a place for the speaker and for two or three listeners in front, and puts them all there.
        private bool Stage(Vector3 player)
        {
            if (!CitySpots.PavementRing(player, 32f, 70f, true, out Vector3 spot) || !CitySpots.Stand(spot, 0.9f, out spot) || !CitySpots.Along(spot, out Vector3 along))
            {
                return false;
            }
            Vector3 side = Vector3.Cross(Vector3.up, along);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector3 facing = attempt == 0 ? along : attempt == 1 ? -along : attempt == 2 ? side : -side;
                Vector3 wide = Vector3.Cross(Vector3.up, facing);
                int found = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (CitySpots.Stand(spot + facing * 2.3f + wide * ((i - 1) * 1.15f), 0.42f, out Vector3 place))
                    {
                        _listeners[found++] = place;
                    }
                }
                if (found < 2)
                {
                    continue;
                }
                int candidate = _own ? _ownColour : _rallyFor;
                Transform box = _props.Stand("soapbox", spot, facing);
                if (box != null)
                {
                    _stage.Add(box);
                }
                // The speaker waits with folded arms until somebody is standing in front of them (see TickRally): nobody speaks to nobody.
                // At the player's own rally the box is theirs: the crowd waits for them.
                if (!_own && !_crew.Add(CityLook.Civilian, spot, spot + facing, "arms_crossed", null, box != null ? BoxTop : 0f))
                {
                    _props.ReturnAll(_stage);
                    return false;
                }
                for (int i = 0; i < found; i++)
                {
                    _crew.Add(CityLook.Civilian, _listeners[i], spot, i == 0 ? "watch_nod" : i == 1 ? "talk_b" : "arms_crossed", null);
                }
                if (_crew.Count < 2)
                {
                    _crew.Release();   // nobody came to listen: no rally
                    _props.ReturnAll(_stage);
                    return false;
                }
                _speaking = false;
                for (int flank = -1; flank <= 1; flank += 2)
                {
                    if (CitySpots.Stand(spot - facing * 0.9f + wide * (flank * 1.1f), 0.4f, out Vector3 beside))
                    {
                        Transform board = _props.Stand(BoardIds[candidate % BoardIds.Length], beside, facing);
                        if (board != null)
                        {
                            _stage.Add(board);
                        }
                    }
                }
                _rallyAt = spot;
                _stageAt = spot;
                CivicDirector.Logger?.Info("Civic: a rally for candidate " + candidate + " with " + _crew.Count + " people, " + Mathf.RoundToInt(Vector3.Distance(player, spot)) + " m from the player.");
                return true;
            }
            return false;
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

    /// <summary>
    /// An afternoon's canvassing for a candidate: the player's own short job. They stand their
    /// pitch (within 80 metres of where they started) and offer a leaflet to the people who walk
    /// by, one key press each; some take one. Thirty leaflets, or four real minutes, or six in
    /// the evening, whichever comes first. Nobody is stopped, moved or animated: the people are
    /// the game's own pedestrians going about their walk, and each is asked once (a smile or a
    /// shrug over their head says how it went).
    /// Ends on every path: done, time up, walking off the pitch, going indoors, getting into a
    /// vehicle, the campaign ending, the feature going.
    /// </summary>
    public sealed class VolunteerShift
    {
        private const int Bundle = 30;
        private const float Seconds = 240f;
        private const float Pitch = 80f;
        private const float Reach = 3.2f;
        private const float AskAgain = 90f;

        private readonly CivicGovernment _government;
        private readonly MonoBehaviour _host;
        private readonly List<int> _asked = new List<int>(48);
        private readonly List<float> _askedAt = new List<float>(48);
        private Vector3 _centre;
        private float _endsAt;
        private int _taken, _offered;
        private Pedestrian _near;
        private string _prompt, _status;
        private int _statusTaken = -1, _statusSeconds = -1;

        public VolunteerShift(CivicGovernment government, MonoBehaviour host)
        {
            _government = government;
            _host = host;
        }

        public bool Active { get; private set; }
        public int For { get; private set; } = -1;

        /// <summary>The line to show under the status, or null: the key that offers a leaflet to whoever is in reach.</summary>
        public string Prompt => Active && _near != null ? _prompt : null;

        public string Status => Active ? _status : null;

        /// <summary>Starts a shift where the player stands. Returns null, or the locale key of why not.</summary>
        public string Begin(int candidate, int hour)
        {
            if (Active)
            {
                return "reallife_civic_volunteer_already";
            }
            if (!_government.Campaigning || candidate < 0 || candidate >= _government.State.CandidateCount)
            {
                return "reallife_civic_no_campaign";
            }
            if (_government.State.Volunteered >= 0)
            {
                return "reallife_civic_volunteer_done_before";
            }
            if (hour < 10 || hour >= 18)
            {
                return "reallife_civic_volunteer_hours";
            }
            if (PlayerHelper.PlayerController == null || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || JobDirector.OnShift)
            {
                return "reallife_civic_volunteer_street";
            }
            Active = true;
            For = candidate;
            _centre = PlayerHelper.GetPosition();
            _endsAt = Time.time + Seconds;
            _taken = 0;
            _offered = 0;
            _near = null;
            _asked.Clear();
            _askedAt.Clear();
            _prompt = CivicText.T("reallife_civic_volunteer_prompt", "key", Keys.Label(KeyAction.Interact));
            _statusTaken = -1;
            _statusSeconds = -1;
            return null;
        }

        /// <summary>Five times a second while a shift is on.</summary>
        public void Tick(float now, int hour, bool interact)
        {
            if (!Active)
            {
                return;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || !_government.Campaigning
                || CivicCity.Flat(player.transform.position - _centre) > Pitch * Pitch)
            {
                End("reallife_civic_volunteer_left");
                return;
            }
            if (_taken >= Bundle)
            {
                End("reallife_civic_volunteer_done");
                return;
            }
            if (now >= _endsAt || hour >= 18)
            {
                End("reallife_civic_volunteer_time");
                return;
            }
            _near = Find(player.transform.position, now);
            if (interact && _near != null)
            {
                Offer(_near, now);
                _near = null;
            }
            int seconds = Mathf.CeilToInt(_endsAt - now);
            if (_taken != _statusTaken || seconds != _statusSeconds)
            {
                _statusTaken = _taken;
                _statusSeconds = seconds;
                _status = CivicText.T("reallife_civic_volunteer_status", "taken", _taken.ToString(), "bundle", Bundle.ToString(), "seconds", seconds.ToString());
            }
        }

        /// <summary>The shift ends now, whatever was done counts. Safe when none is on.</summary>
        public void Stop()
        {
            if (Active)
            {
                End("reallife_civic_volunteer_left");
            }
        }

        private void End(string key)
        {
            Active = false;
            _near = null;
            int candidate = For;
            For = -1;
            if (_taken > 0 && candidate >= 0 && candidate < _government.State.CandidateCount)
            {
                _government.Volunteered(candidate, _taken);
                ModState.AddModifier(ModState.MoodUp);
                CivicText.Toast(NotificationType.Success, key, "taken", _taken.ToString(), "name", CivicText.Name(_government.State.Candidates[candidate]), 12f);
            }
            else
            {
                CivicText.Toast(NotificationType.Info, "reallife_civic_volunteer_nothing", 9f);
            }
        }

        private void Offer(Pedestrian walker, float now)
        {
            _asked.Add(walker.GetInstanceID());
            _askedAt.Add(now);
            _offered++;
            // More people take one from somebody whose candidate speaks to what the city is worried about.
            Candidate candidate = _government.State.Candidates[For];
            float odds = 0.5f + (candidate.Lead == _government.TopIssue() ? 0.15f : 0f) + _government.State.Polls[For] * 0.2f;
            bool took = Random.value < odds;
            if (took)
            {
                _taken++;
            }
            try
            {
                CharacterEmojiName emoji = took ? CharacterEmojiName.CommonGreenHappy : Random.value < 0.3f ? CharacterEmojiName.CommonRedAngry : CharacterEmojiName.CommonYellowNeutral;
                if (walker.tpc != null && walker.tpc.head != null && _host != null && _host.isActiveAndEnabled && GameManager.GetMainCamera() != null
                    && CharacterEmojiSystem.GetCharacterEmojiByName(emoji) != null)
                {
                    _host.StartCoroutine(CharacterEmojiSystem.ShowEmoji(walker.tpc.head, emoji, false, 1.6f));
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }

        // The nearest of the game's pedestrians within reach who has not been asked (the game reuses a pedestrian's object: after a while it is somebody new).
        private Pedestrian Find(Vector3 player, float now)
        {
            for (int i = _asked.Count - 1; i >= 0; i--)
            {
                if (now - _askedAt[i] > AskAgain)
                {
                    _asked.RemoveAt(i);
                    _askedAt.RemoveAt(i);
                }
            }
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return null;
            }
            Pedestrian best = null;
            float bestSqr = Reach * Reach;
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || !walker.isActiveAndEnabled)
                {
                    continue;
                }
                Vector3 offset = walker.transform.position - player;
                float sqr = CivicCity.Flat(offset);
                if (sqr < bestSqr && Mathf.Abs(offset.y) < 2f && !_asked.Contains(walker.GetInstanceID()))
                {
                    bestSqr = sqr;
                    best = walker;
                }
            }
            return best;
        }
    }
}
