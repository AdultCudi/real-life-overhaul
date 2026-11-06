using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The social director's part for people on their way to the player: met at a door they come out of, introduced, dropping by.</summary>
    public sealed partial class SocialDirector
    {
        private const float PendingSeconds = 150f;   // real seconds somebody due to come over waits for the player to be free
        private const float IntroSeconds = 720f;     // ...and a friend bringing somebody along

        private enum Inside { None, Gym, OwnShop, Home, Other }

        private SocialPerson _pendingMeet;
        private float _pendingFrom, _pendingUntil;
        private SocialPerson _pendingDropBy;
        private bool _dropByAtShop;
        private float _dropByUntil;
        private SocialPerson _introFriend, _introStranger;
        private float _introUntil;
        private SocialPerson _callWhenFree;
        private float _callUntil;
        private Inside _inside;
        private bool _wasInside;
        private bool _wasOnShift;
        private float _nextPendingTry;

        // Coming out of a gym, their shop or home, or off a shift, is when somebody from there can be met.
        private void Doors(float real)
        {
            if (BuildingManager.IsInsideBuilding)
            {
                if (!_wasInside)
                {
                    _wasInside = true;
                    _inside = Classify();
                }
                return;
            }
            bool shift = JobDirector.OnShift;
            if (_wasOnShift && !shift && JobDirector.EarnedToday > 0)
            {
                Pend(SocialOrigin.Work, 0.3f, real, 12f);
            }
            _wasOnShift = shift;
            if (!_wasInside || !TrafficTools.Street)
            {
                return;
            }
            _wasInside = false;
            switch (_inside)
            {
                case Inside.Gym:
                    Pend(SocialOrigin.Gym, 0.35f, real, 4f);
                    break;
                case Inside.Home:
                    Pend(SocialOrigin.Neighbour, 0.15f, real, 4f);
                    break;
                case Inside.OwnShop:
                    SocialPerson friend = Settings.SocialContact && SocialState.LastDropByDay != SocialLife.Day && Random.value < 0.25f ? Visitor() : null;
                    if (friend != null)
                    {
                        _pendingDropBy = friend;
                        _dropByAtShop = true;
                        _dropByUntil = real + PendingSeconds;
                    }
                    else
                    {
                        Pend(SocialOrigin.Customer, 0.2f, real, 4f);
                    }
                    break;
            }
            _inside = Inside.None;
        }

        private static Inside Classify()
        {
            try
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                BuildingRegistration registration = manager != null ? manager.buildingRegistration : null;
                if (registration != null && GoingPublic.Mine(registration) && registration.HasEstablishedBusiness)
                {
                    return Inside.OwnShop;
                }
                if (registration != null && registration.businessTypeName != null && registration.businessTypeName.IndexOf("gym", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Inside.Gym;
                }
                if (manager != null && manager.building != null && manager.building.BuildingType == "ba:buildingtype_residential")
                {
                    return Inside.Home;
                }
            }
            catch (Exception)
            {
                // a building the game has no data for is nowhere in particular
            }
            return Inside.Other;
        }

        // Whoever of that origin is not met yet, or somebody new from there, comes over once the player is on foot in the
        // street. delay: real seconds.
        private void Pend(SocialOrigin origin, float chance, float real, float delay)
        {
            if (!Settings.SocialMeetings || _pendingMeet != null || SocialLife.Day - SocialState.LastMeetDay < SocialRules.MeetGapDays || Random.value > chance)
            {
                return;
            }
            SocialPerson person = SocialState.Unmet(origin);
            if (person == null || Expected(person))
            {
                person = Newcomer(origin, -1);
            }
            if (person == null)
            {
                return;
            }
            _pendingMeet = person;
            _pendingFrom = real + delay;
            _pendingUntil = real + delay + PendingSeconds;
            Logger?.Info("Social: " + person.Name + " (" + origin + ") will come over if the player stays on foot in the street.");
        }

        internal void OnHelped()
        {
            if (!_broken && SocialState.Loaded && Settings.Social)
            {
                Pend(SocialOrigin.Helped, 0.6f, Time.unscaledTime, 25f);
            }
        }

        internal void OnCoffee()
        {
            if (_broken || !SocialState.Loaded || !Settings.Social)
            {
                return;
            }
            SocialState.CoffeeBuys++;
            if (SocialState.CoffeeBuys >= 2)
            {
                Pend(SocialOrigin.CoffeeCart, 0.5f, Time.unscaledTime, 5f);   // a regular notices another
            }
        }

        /// <summary>Somebody on their way to be met, not to be met another way meanwhile.</summary>
        public static bool Expected(SocialPerson person)
        {
            return Instance != null && person != null && (person == Instance._pendingMeet || person == Instance._introStranger);
        }

        /// <summary>
        /// Somebody to meet anywhere: the first of the cast not met yet, or somebody new while there is room; null once
        /// everybody a save can know is met. May add a person to the cast; whoever meets them says where.
        /// </summary>
        public static SocialPerson NextStranger()
        {
            for (int i = 0; i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && !p.Met)
                {
                    return p;
                }
            }
            return Newcomer(SocialOrigin.Street, -1);
        }

        /// <summary>
        /// Whether anybody is left to meet, without making anybody: room in the cast, somebody not met yet, or once it is full an
        /// acquaintance long drifted away who would make room (<see cref="MakeRoom"/>). One pass over the cast, two when full.
        /// </summary>
        public static bool AnyStranger()
        {
            if (SocialState.Count < SocialRules.MaxCast)
            {
                return SocialState.Loaded;
            }
            for (int i = 0; i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && !p.Met)
                {
                    return true;
                }
            }
            return RoomToMake();
        }

        /// <summary>
        /// Whether the cast is full: every slot taken, everybody met, and nobody drifted far enough to go by themselves. Then
        /// nobody new can be met until the player loses touch with somebody.
        /// </summary>
        public static bool CastFull => SocialState.Loaded && !AnyStranger();

        /// <summary>
        /// Somebody new joins the cast, to be met at <paramref name="origin"/>: a woman (<paramref name="sex"/> 1), a man (0)
        /// or either (-1). Once <see cref="SocialRules.MaxCast"/> are known, the slot of an acquaintance long drifted away
        /// (<see cref="MakeRoom"/>), as either; null before the cast is loaded or when nobody can make room. Their contact
        /// picture is made with them.
        /// </summary>
        internal static SocialPerson Newcomer(SocialOrigin origin, int sex)
        {
            if (!SocialState.Loaded)
            {
                return null;
            }
            if (SocialState.Count >= SocialRules.MaxCast)
            {
                return MakeRoom(origin);
            }
            SocialPerson p = SocialCatalogue.MakeNew(SocialState.Count, origin, sex, SocialState.CastSeed, SocialLife.Day, SocialPlaces.DaysPerYear, SocialPlaces.Districts,
                SocialState.People, SocialState.Count);
            if (!SocialState.Add(p))
            {
                return null;
            }
            SocialPhone.Picture(p);
            Logger?.Info("Social: somebody new to meet, " + p.Name + " (" + origin + "), person " + (p.Index + 1) + " of at most " + SocialRules.MaxCast + ".");
            return p;
        }

        /// <summary>The next time the player is on foot in the street. False if one is waiting already.</summary>
        public bool QueueIntroduction(SocialPerson friend, SocialPerson stranger)
        {
            if (_broken || _introStranger != null || friend == null || stranger == null || stranger.Met || !Settings.SocialMeetings)
            {
                return false;
            }
            _introFriend = friend;
            _introStranger = stranger;
            _introUntil = Time.unscaledTime + IntroSeconds;
            return true;
        }

        // Close enough to bother, awake, free, not seen today.
        private static SocialPerson Visitor()
        {
            SocialPerson best = null;
            int hour = SocialLife.Hour;
            int day = SocialLife.Day;
            int count = SocialState.Count;
            int from = Random.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                SocialPerson p = SocialState.Get((from + i) % count);
                if (p != null && p.Met && !p.Drifted && p.Stage >= SocialStage.Friend && !p.Asleep(hour) && p.LastOutingDay != day
                    && p.FreeAt(hour, SocialLife.Weekend(day)) && (best == null || p.Closeness > best.Closeness + 10))
                {
                    best = p;
                }
            }
            return best;
        }

        private void Pending(float real, GameManager gm)
        {
            if (_pendingMeet != null && (real > _pendingUntil || _pendingMeet.Met || !Settings.SocialMeetings))
            {
                _pendingMeet = null;
            }
            if (_pendingDropBy != null && real > _dropByUntil)
            {
                _pendingDropBy = null;
            }
            if (_introStranger != null && (real > _introUntil || _introStranger.Met))
            {
                _introStranger = null;
                _introFriend = null;
            }
            if (_callWhenFree != null && real > _callUntil)
            {
                _callWhenFree = null;
            }
            bool anything = _pendingMeet != null || _pendingDropBy != null || _introStranger != null || _callWhenFree != null;
            if (!anything || _scene != null || _ringing)
            {
                return;
            }
            if (_callWhenFree != null)
            {
                if (StartRing(_callWhenFree, real))
                {
                    _callWhenFree = null;
                }
                return;
            }
            if (real < _nextPendingTry || !StreetReady() || CrimeDirector.IsActive || PoliceAttention.IsBusy || TrafficAccidents.IsActive || !LifeEvents.IsSafeForEvent())
            {
                return;
            }
            _nextPendingTry = real + 5f;   // a place that would not do is not asked about again every second
            RefreshContext(gm, Time.time);
            if (!OnFoot)
            {
                return;
            }
            if (_introStranger != null)
            {
                if (Begin(new MeetScene(MeetScene.Kind.Introduction, _introStranger, _introFriend)))
                {
                    _introStranger = null;
                    _introFriend = null;
                }
            }
            else if (_pendingMeet != null && real >= _pendingFrom)
            {
                if (Begin(new MeetScene(MeetScene.Kind.Stranger, _pendingMeet)))
                {
                    _pendingMeet = null;
                }
            }
            else if (_pendingDropBy != null)
            {
                if (Begin(new MeetScene(MeetScene.Kind.DropBy, _pendingDropBy, null, _dropByAtShop)))
                {
                    _pendingDropBy = null;
                }
            }
        }
    }
}
