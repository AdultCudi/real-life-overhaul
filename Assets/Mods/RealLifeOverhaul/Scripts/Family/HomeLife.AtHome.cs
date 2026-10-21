using System;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>HomeLife's part for the partner shown at home: arriving, standing, sitting, greeting and talking, and leaving.</summary>
    internal static partial class HomeLife
    {
        private enum Mode { None, Standing, Walking, Leaving, Retiring, Talking, Sitting, Sleeping }

        private const int MaxSpots = 10;
        private const int MaxSeats = 3;            // of those, places beside something to sit on
        private const int SeatKind = 3;
        private const int CotKind = 4;             // beside the cot: the one place not found among the furniture
        private const int CookKind = 5, SportKind = 6, BedKind = 7, DeskKind = 8, PlantKind = 9;   // by the cooker, a mat or weights, the bed, a desk, a plant
        private const float CotBeside = 0.68f;     // metres from the feet to the cot's middle, to one side, as rock_cot was made
        private const string HoldClip = "hold_baby";
        private const string RockClip = "rock_cot";
        private const float TalkReach = 2.3f;      // metres
        private const int AwayAt = MaxSpots + 1;   // a place asked for by name (the seat to watch from): not one of the home's round
        private const int ByYouAt = MaxSpots + 2;  // beside the player, with something to say
        private const float WatchMinutes = 90f;    // game minutes seated together for the programme, then on with the evening
        private const float WaitMinutes = 60f;     // game minutes they wait on the seat for the player to join them

        private static readonly string[] GreetMorning = { "reallife_family_greet_morning_1", "reallife_family_greet_morning_2", "reallife_family_greet_morning_3" };
        private static readonly string[] GreetDay = { "reallife_family_greet_day_1", "reallife_family_greet_day_2", "reallife_family_greet_day_3", "reallife_family_greet_day_4" };
        private static readonly string[] GreetEvening = { "reallife_family_greet_evening_1", "reallife_family_greet_evening_2", "reallife_family_greet_evening_3", "reallife_family_greet_evening_4" };
        private static readonly string[] GreetLate = { "reallife_family_greet_late_1", "reallife_family_greet_late_2", "reallife_family_greet_late_3" };
        private static readonly string[] GreetLow = { "reallife_family_greet_low_1", "reallife_family_greet_low_2", "reallife_family_greet_low_3" };

        private static readonly string[] BabySounds =
        {
            "reallife_family_baby_sound_1", "reallife_family_baby_sound_2", "reallife_family_baby_sound_3", "reallife_family_baby_sound_4", "reallife_family_baby_sound_5"
        };

        private static int _lastGreet = -1, _lastSound = -1;

        private static readonly Vector3[] Spots = new Vector3[MaxSpots + 3];   // the extras are the cot's, AwayAt and ByYouAt
        private static readonly Vector3[] Faces = new Vector3[MaxSpots + 3];
        private static readonly int[] Kinds = new int[MaxSpots + 3];   // 0 kitchen, 1 living, 2 clear floor, 3 beside a seat, 4 at the cot, 5..9 above
        private static readonly ItemController[] Seats = new ItemController[MaxSpots + 3];   // the seat of a place of kind 3
        private static readonly string[] TvItems = { "itemname_tv", "television" };
        private static int _after = -1;            // where they go once the conversation is over: a kind of place (0 the kitchen), or SeatKind to watch
        private static bool _watching;             // on (or going to) the seat facing the television for the programme
        private static bool _watchJoined;          // ...and the player has sat down with them
        private static float _watchUntil;          // the game minute they stop waiting for the player, and once joined, the programme ends
        private static float _nextSit;             // when they sit on the seat they stand by (put there beside it); 0: not asked
        private static float _nextWord;            // when they may next come over with something to say
        private static Whereabouts _wasWhere;      // where they were at the last tick: in or out of the building is what changes things
        private static Vector3 _goneAt;            // where they go to be gone: the door, or the bed
        private static Transform _bedPlace;        // where in the bed they lie: the bed's own sleeping place, or beside it in a bed for two
        private static bool _bedForTwo;            // ...a bed wide enough for both: they stay when the player lies down too
        private const float BedForTwo = 1.3f;      // metres across a bed for two
        private const float BesideInBed = 0.45f;   // metres from the bed's sleeping place to theirs in a bed for two
        private const float BedRetry = 300f;       // seconds before a bed they could not lie in is tried again (not one put and taken away each second)
        private static float _bedTry;              // when they may next be put to bed; kept when they are let go, unlike _nextSit

        private static float _satAt;
        private static int _spotCount;
        private static int _at = -1;
        private static int _agentType;
        private static Vector3 _entry;
        private static bool _inside;
        private static bool _justEntered;
        private static object _building;
        private static CityPerson _partner;
        private static Mode _mode;
        private static float _nextMove;
        private static float _giveUp;
        private static float _nextSound;
        private static bool _toldAway;
        private static bool _placeFailedLogged;
        private static Transform _cot, _chair;
        private static readonly Action TalkAction = StartTalk;   // made once: the prompt is asked for every second

        /// <summary>The partner, while they are shown in the home; null otherwise.</summary>
        public static CityPerson Person => _partner != null && !_partner.Lost ? _partner : null;

        public static bool Present => Person != null;

        /// <summary>Once a second while no scene is on: who and what is shown at home.</summary>
        public static void Tick(float now)
        {
            if (!FamilyPlaces.AtHome())
            {
                if (_inside)
                {
                    Leave();
                }
                return;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager.enteringBuilding || manager.exitingBuilding)
            {
                return;
            }
            FamilyDirector director = FamilyDirector.Instance;
            Vector3 player = PlayerHelper.GetPosition();
            if (!_inside || !ReferenceEquals(_building, manager.building))
            {
                Leave();
                _inside = true;
                _building = manager.building;
                _entry = Door(manager, player);
                _justEntered = true;
                _toldAway = false;
                _nextSound = now + Random.Range(40f, 90f);
                Scan(manager, player);
                Nursery(player);
                CotPlace();
            }

            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;

            if (now >= _nextSound)
            {
                _nextSound = now + Random.Range(150f, 320f);
                ChildRecord baby = FamilyState.Smallest();
                if (baby != null && baby.Age < 1.5f && !Children.AwayThisWeekend(day) && LifeEvents.IsSafeForEvent())
                {
                    FamilyText.Toast(NotificationType.Info, FamilyText.Pick(BabySounds, ref _lastSound), Args.Us.Kid(baby.Name), 6f);
                }
            }

            if (!FamilyState.Together || !s.LivesIn || !Settings.FamilyAtHome)
            {
                ReleasePartner();
                _justEntered = false;
                return;
            }
            Whereabouts where = Where(day, hour);
            if (_partner != null && _partner.Lost)
            {
                _partner = null;
                _mode = Mode.None;
            }

            // In the building (at home, or asleep in their bed) or out of it (at work, out): only the change between the two moves
            // anybody. A new absence is told anew, once the player is awake to see it.
            bool cameHome = _wasWhere == Whereabouts.Work || _wasWhere == Whereabouts.Out;
            if (where != _wasWhere)
            {
                _toldAway = false;
            }
            switch (_mode)
            {
                case Mode.None:
                    if (where == Whereabouts.Home && !director.Cast.Failed && _spotCount > 0)
                    {
                        Arrive(now, player, _justEntered || !cameHome);   // found in, or up from bed: there; back from out: in at the door
                    }
                    else
                    {
                        if (where == Whereabouts.Asleep && !director.Cast.Failed && _spotCount > 0)
                        {
                            PutToBed(director, now);   // asleep in the home: in their bed, where the player can see them
                        }
                        if ((_justEntered && !_toldAway) || ((where == Whereabouts.Work || where == Whereabouts.Out) && !_toldAway && FamilyText.Awake))
                        {
                        // On coming in, and when the player wakes or looks up to find them gone (to work, out): where they are.
                        _toldAway = true;
                        string key = where == Whereabouts.Asleep ? "reallife_family_home_asleep" : where == Whereabouts.Work ? "reallife_family_home_at_work"
                            : where == Whereabouts.Out ? "reallife_family_home_out" : "reallife_family_home_in";
                        FamilyText.Toast(NotificationType.Info, key, Args.Us.With(FamilyText.T(Partner.JobKey)), 6f);
                        }
                    }
                    break;
                case Mode.Sleeping:   // in bed until morning; out of the way while the player lies in a bed for one
                    if (where != Whereabouts.Asleep)
                    {
                        WakeUp(director, now, player);
                    }
                    else if (!_bedForTwo && _at >= 0 && Seats[_at] != null && Seats[_at].Occupied)
                    {
                        ReleasePartner();
                    }
                    break;
                case Mode.Standing:
                    if (where == Whereabouts.Asleep)
                    {
                        ToBed(director, now);
                    }
                    else if (where != Whereabouts.Home)
                    {
                        _toldAway = true;   // (said as they go)
                        FamilyText.Toast(NotificationType.Info, where == Whereabouts.Work ? "reallife_family_home_leaving_work" : "reallife_family_home_leaving_out", Args.Us, 6f);
                        Lower(_partner, director.Cast.Props);
                        _goneAt = _entry;
                        _mode = _partner.WalkTo(_entry) ? Mode.Leaving : Mode.Retiring;
                        _giveUp = now + 25f;
                    }
                    else
                    {
                        Standing(director, now, player);
                    }
                    break;
                case Mode.Sitting:
                    Sitting(director, now, player, where == Whereabouts.Home);
                    break;
                case Mode.Walking:
                    if ((_partner.Arrived && _partner.Near(Spots[_at], Kinds[_at] == CotKind ? 0.45f : 0.7f)) || now >= _giveUp)
                    {
                        Settle(director, now, _partner.Near(Spots[_at], 0.7f));
                    }
                    break;
                case Mode.Leaving:   // out through the door they came in by: gone at it, or when the walk is over (a home seen whole from above is always in view)
                    if (_partner.Near(_entry, 0.9f) || now >= _giveUp)
                    {
                        ReleasePartner();
                    }
                    break;
                case Mode.Retiring:   // to bed (the bed, or the far end of the home), or out: gone there, out of sight, or when the walk is over
                    if (_partner.Near(_goneAt, 0.9f) || now >= _giveUp || !CitySpots.InView(_partner.Position))
                    {
                        if (where == Whereabouts.Asleep && LieDown(director, now))
                        {
                            break;   // asleep in the bed, shown
                        }
                        ReleasePartner();
                        FamilyText.Toast(NotificationType.Info, where == Whereabouts.Asleep ? "reallife_family_home_gone_to_bed" : "reallife_family_home_leaving_out", Args.Us, 6f);
                    }
                    break;
                case Mode.Talking:
                    if (DialogController.current == null && now >= _giveUp)
                    {
                        EndTalk();   // the conversation closed without saying so
                    }
                    break;
            }
            _wasWhere = where;
            _justEntered = false;
        }

        // The flat's own way in: where the game puts the player on coming in through its main door (ExitZone.playerSpawnPoint,
        // the primary one), not wherever the player stood when the home was first seen (a save loaded inside, say).
        private static Vector3 Door(BuildingManager manager, Vector3 player)
        {
            System.Collections.Generic.List<ExitZone> exits = manager.exitZones;
            ExitZone best = null;
            for (int i = 0; exits != null && i < exits.Count; i++)
            {
                ExitZone exit = exits[i];
                if (exit == null || exit.isElevator || exit.isDriveInBay)
                {
                    continue;
                }
                if (best == null || (exit.isPrimarySpawnPoint && !best.isPrimarySpawnPoint))
                {
                    best = exit;
                }
            }
            if (best == null)
            {
                return player;
            }
            return best.playerSpawnPoint != null ? best.playerSpawnPoint.position : best.transform.position;
        }

        /// <summary>The player has left the home (or a scene needs everybody): nobody and nothing of ours stays in it.</summary>
        public static void Leave()
        {
            ReleasePartner();
            FamilyDirector director = FamilyDirector.Instance;
            if (director != null)
            {
                if (_cot != null)
                {
                    director.Props.Return(_cot);
                }
                if (_chair != null)
                {
                    director.Props.Return(_chair);
                }
            }
            _cot = null;
            _chair = null;
            _inside = false;
            _building = null;
            _spotCount = 0;
            _at = -1;
        }

        /// <summary>A scene takes the partner over as they stand (a proposal at home). They are the scene's from then on.</summary>
        public static CityPerson Hand()
        {
            CityPerson person = Person;
            if (person != null)
            {
                person.Rise();
                Lower(person, FamilyDirector.Instance.Cast.Props);
                person.Idle(0.2f);
                person.Stand();
            }
            _partner = null;
            _mode = Mode.None;
            return person;
        }

        /// <summary>The scene gives them back, still standing in the home.</summary>
        public static void TakeBack(CityPerson person, float now)
        {
            if (person == null || person.Lost || !_inside)
            {
                FamilyDirector.Instance.Cast.Release(person);
                return;
            }
            _partner = person;
            _mode = Mode.Standing;
            _nextMove = now + 15f;
        }

        private static void ReleasePartner()
        {
            if (_mode == Mode.Talking)
            {
                FamilyDirector.Instance?.Body.Release();
            }
            Cradle(false);
            if (_partner != null && FamilyDirector.Instance != null)
            {
                FamilyDirector.Instance.Cast.Release(_partner);
            }
            _partner = null;
            _mode = Mode.None;
            _after = -1;
            _watching = false;
            _watchJoined = false;
            _nextSit = 0f;
        }

        private static void Arrive(float now, Vector3 player, bool wasHome)
        {
            FamilyDirector director = FamilyDirector.Instance;
            // Home when the player walks in, or up from bed: at any place out of the doorway's way (not always the same one).
            // Coming home: in at the door, as anybody comes into a building.
            int at = 0;
            Vector3 spot;
            if (wasHome)
            {
                float best = -1f;
                int away = 0, picked = -1;
                for (int i = 0; i < _spotCount; i++)
                {
                    float sqr = CitySpots.Flat(Spots[i] - player);
                    if (sqr > best)
                    {
                        best = sqr;
                        at = i;
                    }
                    if (sqr > 2.5f * 2.5f && Random.Range(0, ++away) == 0)
                    {
                        picked = i;   // one of those further than 2.5 m, each as likely
                    }
                }
                at = picked >= 0 ? picked : at;
                spot = Spots[at];
            }
            else if (!ComeIn(player, out spot))
            {
                return;   // (no floor in this home at all)
            }
            CityPerson person = director.Cast.Put(0, FamilyLook.Partner, spot, wasHome ? Faces[at] : player, AgentType);
            _giveUp = now + 30f;
            if (person == null)
            {
                if (!_placeFailedLogged)
                {
                    _placeFailedLogged = true;
                    FamilyDirector.Logger?.Info("Family: the partner could not be put on the floor of this home; they are not shown here.");
                }
                _spotCount = 0;   // not asked again in this home
                return;
            }
            _partner = person;
            if (wasHome)
            {
                _at = at;
                _mode = Mode.Standing;
                _nextMove = now + Random.Range(35f, 70f);
                Activity(director, at);
                Greet(director, player);
                if (Kinds[at] == SeatKind)
                {
                    _nextSit = now + 8f;   // on the seat beside them once the greeting is over
                }
            }
            else
            {
                FamilyText.Toast(NotificationType.Info, "reallife_family_home_arrives", Args.Us, 6f);
                _at = Next(-1, TimeHelper.CurrentHour);
                _mode = person.WalkTo(Spots[_at]) ? Mode.Walking : Mode.Standing;
                _giveUp = now + 30f;
                _nextMove = now + 60f;
            }
        }

        // Coming home while the player is in: through the door, on the floor by the way in (found from the door itself, so it
        // does not matter where the player is, in bed or in the doorway); with no floor there, at the furthest of their places.
        private static bool ComeIn(Vector3 player, out Vector3 spot)
        {
            if (FamilyPlaces.Floor(_entry, _entry, 0.25f, 1.6f, out spot, out int type) && type == AgentType)
            {
                return true;
            }
            int far = -1;
            float farthest = -1f;
            for (int i = 0; i < _spotCount; i++)
            {
                float sqr = CitySpots.Flat(Spots[i] - player);
                if (Kinds[i] != CotKind && sqr > farthest)
                {
                    farthest = sqr;
                    far = i;
                }
            }
            spot = far >= 0 ? Spots[far] : default;
            return far >= 0;
        }

        private static void Greet(FamilyDirector director, Vector3 player)
        {
            FamilySave s = FamilyState.S;
            int stamp = TimeHelper.CurrentDay * 24 + TimeHelper.CurrentHour;
            if (stamp - s.LastGreetStamp < 2)
            {
                return;
            }
            s.LastGreetStamp = stamp;
            Lower(_partner, director.Cast.Props);
            director.Cast.Face(_partner, player);
            _partner.Work("wave_hello", director.Cast.Props, 0.25f);
            _nextMove = Mathf.Max(_nextMove, Time.time + 12f);
            int hour = TimeHelper.CurrentHour;
            string[] pool = SaveGameManager.Current.Happiness < 30f || ModState.IsSick ? GreetLow
                : hour < 11 ? GreetMorning : hour < 17 ? GreetDay : hour < 22 ? GreetEvening : GreetLate;
            FamilyText.Toast(NotificationType.Info, FamilyText.Pick(pool, ref _lastGreet), Args.Us, 7f);
            s.LastTalkDay = TimeHelper.CurrentDay;
        }

        private static void Standing(FamilyDirector director, float now, Vector3 player)
        {
            // A sip, then the cup held again; the wave over, back to what they were doing.
            if (_partner.ClipOver && _partner.Playing != null)
            {
                Activity(director, _at);
            }
            else if (_partner.Playing == "cup_hold" && Random.value < 0.07f)
            {
                _partner.Work("sip", director.Cast.Props, 0.3f);
            }

            Vector3 offset = _partner.Position - player;
            float sqr = CitySpots.Flat(offset);
            if (OfferTalk(director, offset, sqr))
            {
                return;
            }
            if (_nextSit > 0f && now >= _nextSit)
            {
                _nextSit = 0f;
                if (_at >= 0 && Kinds[_at] == SeatKind && SitDown(director, now))
                {
                    return;
                }
            }
            if (_at != ByYouAt && now >= _nextWord && (Chores.Pending || PartnerVoice.AnyAsk) && sqr > TalkReach * TalkReach && ComeOver(director, now, player))
            {
                return;   // something to say: over to the player, to wait facing them
            }
            if (now < _nextMove)
            {
                return;
            }
            // On to what the home has for the hour (the cooker at dinner time, the mat in the morning, the sofa at night); with
            // nowhere else to go, something else to do where they are, so nobody stands at one thing for days.
            if (_spotCount > 1)
            {
                int next = Next(_at, TimeHelper.CurrentHour);
                Lower(_partner, director.Cast.Props);
                if (_partner.WalkTo(Spots[next]))
                {
                    _at = next;
                    _mode = Mode.Walking;
                    _giveUp = now + 30f;
                    return;
                }
            }
            _nextMove = now + Random.Range(40f, 80f);
            Lower(_partner, director.Cast.Props);
            if (_at >= 0 && Kinds[_at] == SeatKind && Random.value < 0.5f && SitDown(director, now))
            {
                return;
            }
            Activity(director, _at);
        }

        // How much a kind of place draws them at this hour: dinner at the cooker, exercise in the morning, the desk in working
        // hours, a seat or the television in the evening; the bed only at bedtime (ToBed).
        private static float Draw(int kind, int hour)
        {
            switch (kind)
            {
                case CookKind: return hour >= 17 && hour < 21 ? 6f : hour >= 7 && hour < 10 ? 3f : 1f;
                case 0: return 2f;
                case SeatKind: return hour >= 19 ? 5f : 3f;
                case 1: return hour >= 19 ? 3f : 2f;
                case SportKind: return hour >= 6 && hour < 11 ? 4f : hour >= 17 && hour < 20 ? 2f : 1f;
                case DeskKind: return hour >= 9 && hour < 18 ? 3f : 1f;
                case PlantKind: return 1f;
                case CotKind: return 3f;
                case BedKind: return 0f;
                default: return 1f;   // clear floor: a sweep, a stretch, the phone
            }
        }

        // The next place, drawn by the hour, never the one they are at; the first place if the home has only that.
        private static int Next(int from, int hour)
        {
            float sum = 0f;
            for (int i = 0; i < _spotCount; i++)
            {
                sum += i == from ? 0f : Draw(Kinds[i], hour);
            }
            float roll = Random.value * sum;
            for (int i = 0; i < _spotCount; i++)
            {
                float w = i == from ? 0f : Draw(Kinds[i], hour);
                if (w > 0f && (roll -= w) <= 0f)
                {
                    return i;
                }
            }
            return from >= 0 && from < _spotCount ? from : 0;
        }

        private static int BedAt
        {
            get
            {
                for (int i = 0; i < _spotCount; i++)
                {
                    if (Kinds[i] == BedKind)
                    {
                        return i;
                    }
                }
                return -1;
            }
        }

        // Bedtime: to the bed if the home has one (gone once there), else gone the moment nobody is looking.
        private static void ToBed(FamilyDirector director, float now)
        {
            int bed = BedAt;
            if (bed < 0)
            {
                float far = -1f;
                Vector3 player = PlayerHelper.GetPosition();
                for (int i = 0; i < _spotCount; i++)
                {
                    float sqr = CitySpots.Flat(Spots[i] - player);
                    if (sqr > far)
                    {
                        far = sqr;
                        bed = i;   // no bed of the game's: the far end of the home
                    }
                }
            }
            Lower(_partner, director.Cast.Props);
            _mode = Mode.Retiring;
            _giveUp = now + 30f;
            _goneAt = bed >= 0 ? Spots[bed] : _partner.Position;
            if (bed >= 0 && _partner.WalkTo(Spots[bed]))
            {
                _at = bed;
            }
        }

        // Into their bed, in the way the game lays the player down (CityPerson.Lie); in a bed for two beside its sleeping place,
        // so the player can lie down too. Whether they lie there now.
        private static bool LieDown(FamilyDirector director, float now)
        {
            int bed = BedAt;
            var frame = bed >= 0 ? Seats[bed] as BedController : null;
            if (frame == null || !frame.isActiveAndEnabled || !BedPlace(frame))
            {
                _bedTry = now + BedRetry;
                return false;
            }
            if (!_bedForTwo && frame.Occupied)
            {
                return false;   // the player is in a bed for one: tried again once they are up (PutToBed)
            }
            Lower(_partner, director.Cast.Props);
            if (!_partner.Lie(frame, _bedPlace))
            {
                _bedTry = now + BedRetry;
                Seating.Check(FamilyDirector.Logger, "the partner could not lie down on " + frame.itemName, false);
                return false;
            }
            _at = bed;
            _mode = Mode.Sleeping;
            Seating.Check(FamilyDirector.Logger, "the partner lies asleep on " + frame.itemName + (_bedForTwo ? ", a bed for two, beside its sleeping place" : ", a bed for one"), true);
            return true;
        }

        // Asleep in the home with nobody shown: put at the bed and laid in it (the player came in at night, or got up again).
        private static void PutToBed(FamilyDirector director, float now)
        {
            int bed = BedAt;
            var frame = bed >= 0 ? Seats[bed] as BedController : null;
            if (frame == null || now < _bedTry || (!_bedForTwo && frame.Occupied))
            {
                return;
            }
            _bedTry = now + 5f;   // (not tried every second while the bed is taken)
            CityPerson person = director.Cast.Put(0, FamilyLook.Partner, Spots[bed], Faces[bed], AgentType);
            if (person == null)
            {
                return;
            }
            _partner = person;
            _mode = Mode.Standing;
            if (!LieDown(director, now))
            {
                ReleasePartner();
            }
        }

        // Morning: up out of bed, on their feet beside it, and a good morning if the player is about.
        private static void WakeUp(FamilyDirector director, float now, Vector3 player)
        {
            _partner.Rise();
            _mode = Mode.Standing;
            _nextMove = now + Random.Range(20f, 40f);
            _partner.Work("stretch", director.Cast.Props, 0.3f);
            Greet(director, player);
        }

        // Where in this bed they lie: its own sleeping place, or across from it in a bed wide enough for two.
        private static bool BedPlace(BedController frame)
        {
            Transform sleep = frame.SleepPositionTransform;
            if (sleep == null)
            {
                return false;
            }
            Bounds size = new Bounds(frame.transform.position, Vector3.zero);
            Renderer[] parts = frame.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < parts.Length; i++)
            {
                size.Encapsulate(parts[i].bounds);
            }
            _bedForTwo = Mathf.Min(size.size.x, size.size.z) >= BedForTwo;
            Vector3 at = sleep.position;
            if (_bedForTwo)
            {
                Vector3 side = sleep.right * BesideInBed;
                Vector3 one = at + side, other = at - side;
                at = CitySpots.Flat(one - size.center) <= CitySpots.Flat(other - size.center) ? one : other;   // the side still on the bed
            }
            if (_bedPlace == null)
            {
                _bedPlace = new GameObject("RealLifeOverhaul partner's place in bed").transform;
            }
            _bedPlace.SetPositionAndRotation(at, sleep.rotation);
            return true;
        }

        // Something to say: to a piece of floor beside the player, to wait there facing them. Whether they set off.
        private static bool ComeOver(FamilyDirector director, float now, Vector3 player)
        {
            _nextWord = now + 45f;
            if (!FamilyPlaces.Floor(player, player, 0.25f, 1.3f, out Vector3 spot, out int type) || type != AgentType)
            {
                return false;
            }
            Vector3 near = player + (spot - player).normalized * 1.2f;   // a step short of the player, not on them
            if (!FamilyPlaces.Floor(near, player, 0.2f, 0.8f, out spot, out type) || type != AgentType)
            {
                return false;
            }
            Lower(_partner, director.Cast.Props);
            if (!_partner.WalkTo(spot))
            {
                return false;
            }
            Spots[ByYouAt] = spot;
            Faces[ByYouAt] = player;
            Kinds[ByYouAt] = 2;
            Seats[ByYouAt] = null;
            _at = ByYouAt;
            _mode = Mode.Walking;
            _giveUp = now + 20f;
            return true;
        }

        private static void Settle(FamilyDirector director, float now, bool there)
        {
            _partner.Stand();
            _mode = Mode.Standing;
            _nextMove = now + Random.Range(45f, 100f);
            if (there && Kinds[_at] == SeatKind && SitDown(director, now))
            {
                return;
            }
            if (there)
            {
                if (Kinds[_at] == CotKind)
                {
                    // The last few centimetres, on to where the rail comes to the hands (an agent stops a step short of its goal).
                    NavMeshAgent agent = _partner.Character.navmeshAgent;
                    if (agent != null && agent.enabled && agent.isOnNavMesh)
                    {
                        agent.Warp(Spots[_at]);
                    }
                }
                if (_at == ByYouAt)
                {
                    Vector3 player = PlayerHelper.GetPosition();
                    director.Cast.Face(_partner, player);
                    _partner.Work("wave_hello", director.Cast.Props, 0.25f);
                    _nextMove = now + 60f;   // waiting to be talked to, then back to the home
                    return;
                }
                director.Cast.Face(_partner, Faces[_at]);
                Activity(director, _at);
            }
            if (_watching)
            {
                _watching = false;
                Watched();   // no seat after all: the evening is had as before
            }
        }

        // Onto the seat they have walked up to, if a place on it is free (beside the player on a sofa, too) and it is within a step.
        private static bool SitDown(FamilyDirector director, float now)
        {
            ItemController item = Seats[_at];
            Transform place = item != null ? Seating.Beside(item, Faces[_at]) : null;
            if (place == null || !Seating.InReach(_partner.Position, place))
            {
                Seating.Check(FamilyDirector.Logger, "the partner stays standing by " + (item != null ? item.itemName : "a seat that has gone") + " (in use, or out of reach)", true);
                return false;
            }
            Lower(_partner, director.Cast.Props);
            float step = Mathf.Sqrt(CitySpots.Flat(place.position - _partner.Position));
            if (!_partner.Sit(place))
            {
                Seating.Check(FamilyDirector.Logger, "the partner could not be seated on " + item.itemName, false);
                return false;
            }
            _mode = Mode.Sitting;
            _satAt = now;
            _nextMove = now + (_watching ? 36000f : Random.Range(70f, 160f));   // (watching: on the game's clock, Sitting)
            Seating.Check(FamilyDirector.Logger, "the partner sat down on " + item.itemName + ", " + step.ToString("F2", CultureInfo.InvariantCulture)
                                                 + " m from where they stood, for " + (_nextMove - now).ToString("F0") + " s", true);
            return true;
        }

        // Seated: they get up when it is time to move on or to go, when the seat is taken away, and when spoken to (StartTalk).
        private static void Sitting(FamilyDirector director, float now, Vector3 player, bool staying)
        {
            ItemController item = _at >= 0 ? Seats[_at] : null;
            Vector3 offset = _partner.Position - player;
            float sqr = CitySpots.Flat(offset);
            bool seatGone = item == null || !item.isActiveAndEnabled || !_partner.Seated;
            bool over = false;
            if (_watching)
            {
                PlayerController controller = PlayerHelper.PlayerController;
                float minute = TimeHelper.NowInMinutes();
                if (!_watchJoined && controller != null && controller.Character != null && controller.Character.isSittingOn != null && sqr < 2.6f * 2.6f)
                {
                    _watchJoined = true;   // the player sat down beside them: the programme, together
                    _watchUntil = minute + WatchMinutes;
                    Watched();
                }
                else if (minute >= _watchUntil)
                {
                    if (!_watchJoined)
                    {
                        WaitedInVain();   // an hour on the sofa alone
                    }
                    over = true;   // the programme is over, or the wait: up, wherever the player is
                }
            }
            if (!staying || seatGone || over || (!_watching && now >= _nextMove && sqr > 2.5f * 2.5f))
            {
                GetUp(now, seatGone ? "the seat was taken away" : !staying ? "it is time to go" : over ? "the programme is over" : "to walk on");
                _nextMove = now + Random.Range(15f, 30f);
                return;
            }
            OfferTalk(director, offset, sqr);
        }

        // Within reach, level, and the player facing them: the conversation is offered. Whether it was.
        private static bool OfferTalk(FamilyDirector director, Vector3 offset, float sqr)
        {
            PlayerController controller = PlayerHelper.PlayerController;
            if (sqr < TalkReach * TalkReach && Mathf.Abs(offset.y) < 1.2f && controller != null
                && Vector3.Dot(controller.Character.transform.forward, offset.normalized) > 0.45f)
            {
                director.Prompt(PartnerVoice.AnyAsk ? "reallife_family_prompt_talk_ask" : "reallife_family_prompt_talk", Partner.Name, TalkAction,
                    _partner.Valid ? _partner.Character.transform : null);
                return true;
            }
            return false;
        }

        // On their feet where they stood before, and standing like anybody: the next second decides what they do.
        private static void GetUp(float now, string why)
        {
            if (_partner == null)
            {
                return;
            }
            _partner.Rise();
            _watching = false;
            _watchJoined = false;
            if (_mode == Mode.Sitting)
            {
                _mode = Mode.Standing;
                Seating.Check(FamilyDirector.Logger, "the partner got up after " + (now - _satAt).ToString("F0") + " s (" + why + ")", !_partner.Seated);
            }
        }

        // What they do where they stand. A clip that cannot be had leaves them standing as the game stands people.
        private static void Activity(FamilyDirector director, int at)
        {
            if (_partner == null || at < 0)
            {
                return;
            }
            CityProps props = director.Cast.Props;
            bool holding = false;
            switch (Kinds[at])
            {
                case CotKind:
                    if (!_partner.Work(RockClip, props, 0.4f))
                    {
                        _partner.Work("arms_crossed", props, 0.4f);
                    }
                    break;
                case 0:   // the fridge, the sink, the counter: a coffee, or a wipe down
                    Any(props, Random.value < 0.5f ? "counter_wipe" : "cup_hold", "cup_hold", "arms_crossed");
                    break;
                case CookKind:
                    Any(props, "cook_stir", "counter_wipe", "arms_crossed");
                    break;
                case SportKind:
                    Any(props, Pick("stretch", "situp", "pushup", "tai_chi"), "stretch", "arms_crossed");
                    break;
                case DeskKind:
                    Any(props, Random.value < 0.6f ? "desk_plan" : "take_notes", "take_notes", "read_paper");
                    break;
                case PlantKind:
                    Any(props, "water_plants", "watch_nod", "arms_crossed");
                    break;
                case BedKind:
                    Any(props, "stretch", "arms_crossed");
                    break;
                case 1:
                case SeatKind:   // (standing by a seat that was not free: as in the living room)
                    // A different thing each time, never the paper twice running.
                    float roll = Random.value;
                    holding = roll < 0.3f && HoldBaby(props);
                    if (!holding && !(roll < 0.55f && _partner.Playing != "read_paper" ? _partner.Work("read_paper", props, 0.4f)
                            : roll < 0.8f ? _partner.Work("watch_nod", props, 0.4f) : _partner.Work("cup_hold", props, 0.4f)))
                    {
                        _partner.Work("watch_nod", props, 0.4f);
                    }
                    break;
                default:   // clear floor: the baby, a sweep, a stretch, the phone
                    holding = Random.value < 0.4f && HoldBaby(props);
                    if (!holding)
                    {
                        Any(props, Pick("sweep", "mop_floor", "stretch", "phone_answer"), "stretch", "arms_crossed");
                    }
                    break;
            }
            Cradle(holding);
        }

        // The first of the clips that can be had; the last is the game's way of standing if none can. Sweeping, mopping and
        // wiping down really clean the home, a little each time (Cleaned).
        private static void Any(CityProps props, string first, string second, string third = null)
        {
            string played = _partner.Work(first, props, 0.4f) ? first : _partner.Work(second, props, 0.4f) ? second
                : third != null && _partner.Work(third, props, 0.4f) ? third : null;
            if (played == "sweep" || played == "mop_floor")
            {
                Cleaned(CleanSweep);
            }
            else if (played == "counter_wipe")
            {
                Cleaned(CleanWipe);
            }
        }

        private const float CleanSweep = 1.5f, CleanWipe = 0.75f;   // points of a hundred of the home's tidiness, each time
        private const float CleanPerDay = 8f;                         // at most in a day: about what a tidy roommate does
        private static int _cleanDay;
        private static float _cleanToday;

        private static void Cleaned(float points)
        {
            int day = TimeHelper.CurrentDay;
            if (day != _cleanDay)
            {
                _cleanDay = day;
                _cleanToday = 0f;
            }
            points = Mathf.Min(points, CleanPerDay - _cleanToday);
            if (points > 0f && FamilyBridges.Home != null && FamilyBridges.Home.Tidied(points))
            {
                _cleanToday += points;
            }
        }

        private static string Pick(string a, string b, string c, string d)
        {
            switch (Random.Range(0, 4))
            {
                case 0: return a;
                case 1: return b;
                case 2: return c;
                default: return d;
            }
        }

        // The baby in the crook of the arm (hold_baby brings the bundle), while one is under a year old. Whether it was done.
        private static bool HoldBaby(CityProps props)
        {
            return FamilyState.AnyUnder(FamilyBalance.BabyUntil) && _partner.Work(HoldClip, props, 0.4f);
        }

        private static void StartTalk()
        {
            FamilyDirector director = FamilyDirector.Instance;
            if (_partner != null && !_partner.Lost && _mode == Mode.Sitting)
            {
                GetUp(Time.time, "the player spoke to them");
            }
            if (_partner == null || _partner.Lost || _mode != Mode.Standing)
            {
                return;
            }
            Vector3 player = PlayerHelper.GetPosition();
            Lower(_partner, director.Cast.Props);
            _partner.Stand();
            director.Cast.Face(_partner, player);
            _partner.Work("talk_a", director.Cast.Props, 0.3f);
            _mode = Mode.Talking;
            _giveUp = Time.time + 3f;
            director.Body.Hold();
            director.Body.Clip("talk_b");
            FamilyState.S.LastTalkDay = TimeHelper.CurrentDay;
            if (!FamilyTalk.Open(() => PartnerVoice.OpenAsk() ?? Chores.Opening() ?? FamilyPhone.PartnerMenu(true), EndTalk, _partner.Character))   // what they asked to talk over, first
            {
                EndTalk();
            }
        }

        private static void EndTalk()
        {
            FamilyDirector director = FamilyDirector.Instance;
            if (director == null)
            {
                return;
            }
            director.Body.Release();
            if (_mode == Mode.Talking)
            {
                _mode = Mode.Standing;
                _nextMove = Time.time + 25f;
                if (_partner != null && !_partner.Lost)
                {
                    _partner.Idle(0.3f);
                }
            }
            GoAfter(director, Time.time);
        }

        /// <summary>What was agreed in the conversation, done once it is over: to the kitchen to cook, or to the seat facing the television to watch.</summary>
        internal static void AfterTalk(bool watch)
        {
            _after = watch ? SeatKind : 0;
        }

        private static void GoAfter(FamilyDirector director, float now)
        {
            int kind = _after;
            _after = -1;
            if (kind < 0 || _partner == null || _partner.Lost || _mode != Mode.Standing)
            {
                return;
            }
            int to = -1;
            if (kind == SeatKind)
            {
                to = WatchSeat();
            }
            else
            {
                float best = float.MaxValue;
                Vector3 player = PlayerHelper.GetPosition();
                for (int i = 0; i < _spotCount; i++)
                {
                    float sqr = CitySpots.Flat(Spots[i] - player);
                    if (Kinds[i] == kind && sqr < best)
                    {
                        best = sqr;
                        to = i;
                    }
                }
            }
            if (to < 0)
            {
                Seating.Check(FamilyDirector.Logger, "the partner stays where they are: no " + (kind == SeatKind ? "seat with a free place to watch from" : "place in the kitchen"), kind != SeatKind);
                if (kind == SeatKind)
                {
                    Watched();   // nowhere to sit: the evening is had as before
                }
                return;
            }
            Lower(_partner, director.Cast.Props);
            if (_partner.WalkTo(Spots[to]))
            {
                _at = to;
                _mode = Mode.Walking;
                _giveUp = now + 30f;
                _watching = kind == SeatKind;
                _watchJoined = false;
                _watchUntil = TimeHelper.NowInMinutes() + WaitMinutes;
            }
            else if (kind == SeatKind)
            {
                Watched();
            }
        }

        // The seat nearest the television (the player, in a home without one) with a place free, and the floor beside it, as the
        // place AwayAt; -1 if there is none. Read once, when asked for.
        private static int WatchSeat()
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            System.Collections.Generic.List<ItemController> items = manager != null ? manager.allItemControllers : null;
            if (items == null)
            {
                return -1;
            }
            Vector3 player = PlayerHelper.GetPosition();
            Vector3 screen = player;
            for (int i = 0; i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item != null && item.isActiveAndEnabled && !string.IsNullOrEmpty(item.itemName) && Is(item.itemName, TvItems))
                {
                    screen = item.transform.position;
                    break;
                }
            }
            ItemController seat = null;
            float best = float.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item == null || !Seating.IsSeat(item) || Seating.Beside(item, screen) == null)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(item.transform.position - screen);
                if (sqr < best)
                {
                    best = sqr;
                    seat = item;
                }
            }
            if (seat == null || !FamilyPlaces.Floor(seat.transform.position, player, 0.4f, 1.6f, out Vector3 spot, out int type) || type != AgentType)
            {
                return -1;
            }
            Spots[AwayAt] = spot;
            Faces[AwayAt] = screen;
            Kinds[AwayAt] = SeatKind;
            Seats[AwayAt] = seat;
            return AwayAt;
        }

        // What they hold is put away, and a baby they held lies in the cot again.
        private static void Lower(CityPerson person, CityProps props)
        {
            person.PutAway(props);
            Cradle(false);
        }

        // The baby's bundle lies in the cot (a baby under a year old) unless the partner has it in their arms.
        private static void Cradle(bool held)
        {
            FamilyProps.Tuck(_cot, !held && FamilyState.AnyUnder(FamilyBalance.BabyUntil));
        }
    }
}
