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
        private enum Mode { None, Standing, Walking, Leaving, Retiring, Talking, Sitting }

        private const int MaxSpots = 6;
        private const int MaxSeats = 2;            // of those, places beside something to sit on
        private const int SeatKind = 3;
        private const int CotKind = 4;             // beside the cot: the one place not found among the furniture
        private const float CotBeside = 0.68f;     // metres from the feet to the cot's middle, to one side, as rock_cot was made
        private const string HoldClip = "hold_baby";
        private const string RockClip = "rock_cot";
        private const float TalkReach = 2.3f;      // metres

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

        private static readonly Vector3[] Spots = new Vector3[MaxSpots + 1];   // the extra one is the cot's
        private static readonly Vector3[] Faces = new Vector3[MaxSpots + 1];
        private static readonly int[] Kinds = new int[MaxSpots + 1];   // 0 kitchen, 1 living, 2 just a clear piece of floor, 3 beside a seat, 4 at the cot
        private static readonly ItemController[] Seats = new ItemController[MaxSpots + 1];   // the seat of a place of kind 3
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
                _entry = player;
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

            switch (_mode)
            {
                case Mode.None:
                    if (where == Whereabouts.Home && !director.Cast.Failed && _spotCount > 0)
                    {
                        Arrive(now, player, _justEntered);
                    }
                    else if (_justEntered && !_toldAway)
                    {
                        _toldAway = true;
                        string key = where == Whereabouts.Asleep ? "reallife_family_home_asleep" : where == Whereabouts.Work ? "reallife_family_home_at_work"
                            : where == Whereabouts.Out ? "reallife_family_home_out" : "reallife_family_home_in";
                        FamilyText.Toast(NotificationType.Info, key, Args.Us.With(FamilyText.T(Partner.JobKey)), 6f);
                    }
                    break;
                case Mode.Standing:
                    if (where == Whereabouts.Asleep)
                    {
                        _mode = Mode.Retiring;
                        _giveUp = now + 120f;
                    }
                    else if (where != Whereabouts.Home)
                    {
                        FamilyText.Toast(NotificationType.Info, where == Whereabouts.Work ? "reallife_family_home_leaving_work" : "reallife_family_home_leaving_out", Args.Us, 6f);
                        Lower(_partner, director.Cast.Props);
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
                case Mode.Leaving:   // out through the door they came in by
                    if (_partner.Near(_entry, 0.9f) || (now >= _giveUp && !CitySpots.InView(_partner.Position)))
                    {
                        ReleasePartner();
                    }
                    break;
                case Mode.Retiring:   // gone to bed, or out, the moment nobody is looking
                    if (!CitySpots.InView(_partner.Position))
                    {
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
            _justEntered = false;
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
        }

        private static void Arrive(float now, Vector3 player, bool wasHome)
        {
            FamilyDirector director = FamilyDirector.Instance;
            // Home when the player walks in: at the place furthest from the door. Coming home: in at the door while it is not looked at.
            int at = 0;
            Vector3 spot;
            if (wasHome)
            {
                float best = -1f;
                for (int i = 0; i < _spotCount; i++)
                {
                    float sqr = CitySpots.Flat(Spots[i] - player);
                    if (sqr > best)
                    {
                        best = sqr;
                        at = i;
                    }
                }
                spot = Spots[at];
            }
            else
            {
                if (CitySpots.InView(_entry))
                {
                    return;   // nobody appears in a doorway that is being looked at: the moment the player looks away, they are in
                }
                if (!FamilyPlaces.Floor(_entry, player, 0.3f, 0.8f, out spot, out int type) || type != AgentType || (spot - player).sqrMagnitude < 1.2f)
                {
                    _giveUp = now + 30f;
                    return;
                }
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
            }
            else
            {
                FamilyText.Toast(NotificationType.Info, "reallife_family_home_arrives", Args.Us, 6f);
                _at = Random.Range(0, _spotCount);
                _mode = person.WalkTo(Spots[_at]) ? Mode.Walking : Mode.Standing;
                _giveUp = now + 30f;
                _nextMove = now + 60f;
            }
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
            if (now >= _nextMove && _spotCount > 1 && sqr > 2.5f * 2.5f)
            {
                int next = (_at + 1 + Random.Range(0, _spotCount - 1)) % _spotCount;
                Lower(_partner, director.Cast.Props);
                if (_partner.WalkTo(Spots[next]))
                {
                    _at = next;
                    _mode = Mode.Walking;
                    _giveUp = now + 30f;
                }
                else
                {
                    _nextMove = now + 30f;
                }
            }
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
                director.Cast.Face(_partner, Faces[_at]);
                Activity(director, _at);
            }
        }

        // Onto the seat they have walked up to, if nobody is on it (the player least of all) and it is within a step.
        private static bool SitDown(FamilyDirector director, float now)
        {
            ItemController item = Seats[_at];
            Transform place = item != null ? Seating.Free(item) : null;
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
            _nextMove = now + Random.Range(70f, 160f);
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
            if (!staying || seatGone || (now >= _nextMove && sqr > 2.5f * 2.5f))
            {
                GetUp(now, seatGone ? "the seat was taken away" : !staying ? "it is time to go" : "to walk on");
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
                case 0:
                    if (!_partner.Work("cup_hold", props, 0.4f))
                    {
                        _partner.Work("arms_crossed", props, 0.4f);
                    }
                    break;
                case 1:
                case SeatKind:   // (standing by a seat that was not free: as in the living room)
                    holding = (TimeHelper.CurrentDay + at) % 2 == 1 && HoldBaby(props);
                    if (!holding && !((TimeHelper.CurrentDay + at) % 2 == 0 ? _partner.Work("read_paper", props, 0.4f) : _partner.Work("watch_nod", props, 0.4f)))
                    {
                        _partner.Work("watch_nod", props, 0.4f);
                    }
                    break;
                default:
                    holding = HoldBaby(props);
                    if (!holding)
                    {
                        _partner.Work("arms_crossed", props, 0.4f);
                    }
                    break;
            }
            Cradle(holding);
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
            if (!FamilyTalk.Open(() => PartnerVoice.OpenAsk() ?? FamilyPhone.PartnerMenu(true), EndTalk, _partner.Character))   // what they asked to talk over, first
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
