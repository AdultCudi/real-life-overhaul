using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>Where the partner is at an hour of a day.</summary>
    internal enum Whereabouts { Home, Asleep, Work, Out }

    /// <summary>
    /// Living together. The partner has a job and a life: a timetable by what they do for a
    /// living says when they are home. While the player is in the family's home and they are
    /// too, they are there as a real character: standing in the kitchen with a coffee, in
    /// front of the television, reading the paper, walking between those places on the
    /// floor's own navigation mesh; they greet the player at the door and can be talked to.
    /// Part of the time they sit: on a chair, an armchair or a sofa the player has put in the
    /// home, seated as the game seats its own people (<see cref="Seating"/>), for a minute or two,
    /// and they get up to walk on, to leave, or when the player speaks to them. Never on a seat
    /// somebody is using. They do not lie down: the game's beds belong to its own sleep
    /// activity, so asleep is a state (they are "in bed", not shown).
    ///
    /// Also here: moving in and out, the shared routines, their share of the rent, chores,
    /// looking after the player when they are ill or low, and evenings out.
    /// </summary>
    internal static class HomeLife
    {
        /// <summary>What a home's power and water come to a day (Utilities, through Wiring/UtilityLinks); null: none known.</summary>
        internal static Func<BuildingRegistration, float> Bills;

        private enum Mode { None, Standing, Walking, Leaving, Retiring, Talking, Sitting }

        private const int MaxSpots = 6;
        private const int MaxSeats = 2;            // of those, places beside something to sit on
        private const int SeatKind = 3;
        private const int CotKind = 4;             // beside the cot, at its long side: the one place that is not found among the furniture
        private const float CotBeside = 0.68f;     // rock_cot: the cot's middle this far to one side of the feet, so its near rail is 0.35 m ahead
        private const string HoldClip = "hold_baby";
        private const string RockClip = "rock_cot";
        private const float TalkReach = 2.3f;

        private static readonly string[] KitchenItems = { "stove", "fridge", "coffeemachine", "microwave", "kitchen", "sink", "oven" };
        private static readonly string[] LivingItems = { "sofa", "couch", "itemname_tv", "television", "armchair", "bookshel", "coffeetable" };

        private static readonly string[] GreetMorning = { "reallife_family_greet_morning_1", "reallife_family_greet_morning_2", "reallife_family_greet_morning_3" };
        private static readonly string[] GreetDay = { "reallife_family_greet_day_1", "reallife_family_greet_day_2", "reallife_family_greet_day_3", "reallife_family_greet_day_4" };
        private static readonly string[] GreetEvening = { "reallife_family_greet_evening_1", "reallife_family_greet_evening_2", "reallife_family_greet_evening_3", "reallife_family_greet_evening_4" };
        private static readonly string[] GreetLate = { "reallife_family_greet_late_1", "reallife_family_greet_late_2", "reallife_family_greet_late_3" };
        private static readonly string[] GreetLow = { "reallife_family_greet_low_1", "reallife_family_greet_low_2", "reallife_family_greet_low_3" };
        private static readonly string[] Encourage = { "reallife_family_encourage_1", "reallife_family_encourage_2", "reallife_family_encourage_3", "reallife_family_encourage_4", "reallife_family_encourage_5" };
        private static readonly string[] SickCare = { "reallife_family_sickcare_1", "reallife_family_sickcare_2", "reallife_family_sickcare_3" };
        private static readonly string[] CookLines = { "reallife_family_cook_1", "reallife_family_cook_2", "reallife_family_cook_3", "reallife_family_cook_4" };
        private static readonly string[] WatchLines = { "reallife_family_watch_1", "reallife_family_watch_2", "reallife_family_watch_3", "reallife_family_watch_4" };
        private static readonly string[] DinnerLines = { "reallife_family_dinner_1", "reallife_family_dinner_2", "reallife_family_dinner_3", "reallife_family_dinner_4", "reallife_family_dinner_5" };
        private static readonly string[] StoodUp = { "reallife_family_stoodup_1", "reallife_family_stoodup_2", "reallife_family_stoodup_3" };
        private static readonly string[] Missing = { "reallife_family_neglect_1", "reallife_family_neglect_2", "reallife_family_neglect_3" };
        private static readonly string[] BabySounds = { "reallife_family_baby_sound_1", "reallife_family_baby_sound_2", "reallife_family_baby_sound_3", "reallife_family_baby_sound_4", "reallife_family_baby_sound_5" };
        private static readonly string[] ChoreAsks = { "reallife_family_chore_1", "reallife_family_chore_2", "reallife_family_chore_3", "reallife_family_chore_4", "reallife_family_chore_5", "reallife_family_chore_6" };
        private static readonly string[] ChoreThanks = { "reallife_family_chore_thanks_1", "reallife_family_chore_thanks_2", "reallife_family_chore_thanks_3" };
        private static readonly string[] ChoreSighs = { "reallife_family_chore_sigh_1", "reallife_family_chore_sigh_2", "reallife_family_chore_sigh_3" };

        private static int _lastGreet = -1, _lastEncourage = -1, _lastSick = -1, _lastCook = -1, _lastWatch = -1, _lastDinner = -1, _lastStood = -1,
            _lastMissing = -1, _lastSound = -1, _lastChore = -1, _lastThanks = -1, _lastSigh = -1;

        private static readonly Vector3[] Spots = new Vector3[MaxSpots + 1];   // (the extra one is the cot's)
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
        private static readonly Action TalkAction = StartTalk;   // made once: asking for the prompt every second allocates nothing

        /// <summary>The partner, while they are shown in the home; null otherwise.</summary>
        public static CityPerson Person => _partner != null && !_partner.Lost ? _partner : null;

        public static bool Present => Person != null;

        // ---------------------------------------------------------------- the timetable

        public static Whereabouts Where(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (hour >= 23 || hour < 6)
            {
                return Whereabouts.Asleep;
            }
            if (s.NewParentUntil >= day)
            {
                return Whereabouts.Home;   // at home with the new arrival
            }
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(day);
            bool weekend = weekday == DayOfWeekOrdered.Saturday || weekday == DayOfWeekOrdered.Sunday;
            int from, to;
            bool works;
            switch (s.PJob)
            {
                case 0:
                case 7:   // nurse, paramedic: earlies, lates, a day off
                    int turn = day % 3;
                    works = turn != 2;
                    from = turn == 0 ? 6 : 14;
                    to = turn == 0 ? 14 : 22;
                    break;
                case 2:   // chef: evenings, Tuesday to Saturday
                    works = weekday != DayOfWeekOrdered.Sunday && weekday != DayOfWeekOrdered.Monday;
                    from = 14;
                    to = 23;
                    break;
                case 9:   // barista: mornings, six days
                    works = weekday != DayOfWeekOrdered.Sunday;
                    from = 6;
                    to = 13;
                    break;
                default:
                    works = !weekend;
                    from = 8;
                    to = 17;
                    break;
            }
            if (FamilyState.Care == Care.Partner && FamilyState.AnyUnder(FamilyBalance.SchoolFrom))
            {
                to = Mathf.Min(to, from + 4);   // short days: they are the one at home with the children
            }
            if (works && hour >= from && hour < to)
            {
                return Whereabouts.Work;
            }
            // An evening of their own now and then: the gym, friends.
            if (hour >= 18 && hour < 21 && (day * 7 + (s.PSeed & 0xff)) % 5 == 0)
            {
                return Whereabouts.Out;
            }
            return Whereabouts.Home;
        }

        // ---------------------------------------------------------------- moving in and out

        /// <summary>The partner moves in. Null if they did; otherwise why not (a locale key).</summary>
        public static string MoveIn(bool test)
        {
            FamilySave s = FamilyState.S;
            if (!FamilyState.Together)
            {
                return "reallife_family_no_partner";
            }
            if (s.LivesIn)
            {
                return "reallife_family_no_already_in";
            }
            BuildingRegistration home = FamilyPlaces.Home();
            if (home == null)
            {
                return "reallife_family_no_home";
            }
            if (!FamilyPlaces.HasRoom(home, 0))
            {
                return "reallife_family_no_room";
            }
            if (!test && FamilyState.Bond == Bond.Dating && !Ready())
            {
                return "reallife_family_no_not_yet";
            }
            s.LivesIn = true;
            s.MovedInDay = TimeHelper.CurrentDay;
            s.LastSharedDay = TimeHelper.CurrentDay;
            FamilyPlaces.MakeHome(home);
            FamilyState.Remember("reallife_family_memory_moved_in", Partner.Name);
            Partner.Tell(FamilyEvent.MovedIn, 5f, 5f);
            FamilyModifiers.Add(FamilyModifiers.Together);
            Relatives.MomSays("reallife_family_mom_moved_in");
            FamilyText.Good("reallife_family_moved_in", Args.Us);
            FamilyDirector.Logger?.Info("Family: the partner moved in.");
            return null;
        }

        /// <summary>Close enough, for long enough, to share a home.</summary>
        public static bool Ready()
        {
            if (Partner.Kept(out PersonInfo p))
            {
                return p.Stage >= 5 || (p.Affection >= FamilyBalance.MoveInAffection && Partner.DaysTogether >= FamilyBalance.MoveInDaysTogether);
            }
            return Partner.Affection >= FamilyBalance.MoveInAffection && Partner.DaysTogether >= FamilyBalance.MoveInDaysTogether;
        }

        public static void MoveOut()
        {
            FamilyState.S.LivesIn = false;
            ReleasePartner();
            FamilyModifiers.Remove(FamilyModifiers.Together);
            Partner.Tell(FamilyEvent.MovedOut, 0f);
        }

        // ---------------------------------------------------------------- at home: who and what is shown

        /// <summary>Once a second while no scene is on.</summary>
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

            // A baby in the house makes itself heard.
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
                case Mode.Leaving:
                    // Out through the door they came in by.
                    if (_partner.Near(_entry, 0.9f) || (now >= _giveUp && !CitySpots.InView(_partner.Position)))
                    {
                        ReleasePartner();
                    }
                    break;
                case Mode.Retiring:
                    // Gone to bed (or out) the moment nobody is looking.
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
                if (_cot != null) director.Props.Return(_cot);
                if (_chair != null) director.Props.Return(_chair);
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
                person.Rise();   // (a scene takes them standing)
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

        // The places they stand: beside what the game calls a cooker, a fridge, a sofa, a television; failing all, clear floor.
        private static void Scan(BuildingManager manager, Vector3 player)
        {
            _spotCount = 0;
            _at = -1;
            _agentType = -1;
            Array.Clear(Seats, 0, Seats.Length);
            List<ItemController> items = manager.allItemControllers;
            int kitchens = 0, livings = 0, seats = 0;
            for (int i = 0; items != null && i < items.Count && _spotCount < MaxSpots; i++)
            {
                ItemController item = items[i];
                if (item == null || !item.isActiveAndEnabled || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }
                // Something to sit on (whatever the game calls it: a chair, an armchair, a sofa) is a place to sit; the rest are places to stand.
                int kind = Seating.IsSeat(item) ? SeatKind : Is(item.itemName, KitchenItems) ? 0 : Is(item.itemName, LivingItems) ? 1 : -1;
                if (kind < 0 || (kind == 0 && kitchens >= 2) || (kind == 1 && livings >= 2) || (kind == SeatKind && seats >= MaxSeats))
                {
                    continue;
                }
                Vector3 at = item.transform.position;
                if (!FamilyPlaces.Floor(at, player, 0.4f, 1.6f, out Vector3 spot, out int type) || !Usable(spot, at, type))
                {
                    continue;
                }
                Seats[_spotCount] = kind == SeatKind ? item : null;
                Add(spot, at, kind, type);
                if (kind == 0) kitchens++; else if (kind == 1) livings++; else seats++;
            }
            if (Meta.Dev.Verbose)
            {
                FamilyDirector.Logger?.Info("People check (seat): this home has " + seats + " place(s) to sit among " + _spotCount + " place(s) for the partner (the game's own seats, at most " + MaxSeats + "): "
                                            + (seats > 0 ? "PASS" : "none: they stand") + ".");
            }
            // Clear floor a few steps in from the door, if the furniture gave nothing (or one place only).
            for (int ring = 0; ring < 2 && _spotCount < 2; ring++)
            {
                float radius = 3f + ring * 1.6f;
                for (int i = 0; i < 8 && _spotCount < 2; i++)
                {
                    float angle = (i + ring * 0.5f) * Mathf.PI / 4f;
                    Vector3 near = player + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (FamilyPlaces.Floor(near, player, 0.55f, 0.5f, out Vector3 spot, out int type) && Usable(spot, spot, type))
                    {
                        Add(spot, player, 2, type);
                    }
                }
            }
            if (_spotCount == 0 && !_placeFailedLogged)
            {
                _placeFailedLogged = true;
                FamilyDirector.Logger?.Info("Family: no clear floor on a navigation mesh was found in this home; the partner is not shown here (they still live here).");
            }
        }

        private static bool Is(string name, string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (name.IndexOf(parts[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Not in the doorway, not on top of another place, on the same mesh as the rest, and within reach of what it is beside.
        private static bool Usable(Vector3 spot, Vector3 item, int type)
        {
            if (_agentType >= 0 && type != (_agentType == 0 ? NavMeshHelperIds.Npc : NavMeshHelperIds.Player))
            {
                return false;
            }
            if (CitySpots.Flat(spot - _entry) < 1.6f * 1.6f || CitySpots.Flat(spot - item) > 2.0f * 2.0f)
            {
                return false;
            }
            for (int i = 0; i < _spotCount; i++)
            {
                if (CitySpots.Flat(Spots[i] - spot) < 1.5f * 1.5f)
                {
                    return false;
                }
            }
            return true;
        }

        private static void Add(Vector3 spot, Vector3 face, int kind, int type)
        {
            _agentType = type == NavMeshHelperIds.Npc ? 0 : 1;
            Spots[_spotCount] = spot;
            Faces[_spotCount] = face;
            Kinds[_spotCount] = kind;
            _spotCount++;
        }

        private static int AgentType => _agentType == 0 ? NavMeshHelperIds.Npc : NavMeshHelperIds.Player;

        // The cot and the high chair, each on a clear piece of floor that people then walk round.
        private static void Nursery(Vector3 player)
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            bool cot = s.Cot && FamilyState.AnyUnder(FamilyBalance.CotUntil);
            bool chair = false;
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                chair |= !c.Grown && c.Age >= FamilyBalance.HighChairFrom && c.Age < FamilyBalance.HighChairUntil;
            }
            if (!cot && !chair)
            {
                return;
            }
            int placed = 0;
            for (int ring = 0; ring < 3 && (cot || chair); ring++)
            {
                float radius = 2.6f + ring * 1.3f;
                for (int i = 0; i < 10 && (cot || chair); i++)
                {
                    float angle = (i * 0.63f + ring * 0.3f + placed) * Mathf.PI * 0.4f;
                    Vector3 near = player + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (!FamilyPlaces.Floor(near, player, 0.85f, 0.5f, out Vector3 spot, out _) || CitySpots.Flat(spot - _entry) < 2.2f * 2.2f || Taken(spot))
                    {
                        continue;
                    }
                    if (cot)
                    {
                        _cot = director.Props.Stand(FamilyProps.Cot, spot, player - spot, true);
                        FamilyProps.Tuck(_cot, FamilyState.AnyUnder(FamilyBalance.BabyUntil));   // a baby in arms is in it; a toddler's cot stands empty
                        cot = false;
                    }
                    else
                    {
                        _chair = director.Props.Stand(FamilyProps.HighChair, spot, player - spot, true);
                        chair = false;
                    }
                    placed++;
                }
            }
        }

        // A place at the cot's long side for rocking it, while a baby lies in it: on the floor within a hand's breadth of
        // where rock_cot has the feet (the rail then comes to the hands), either side, on the same mesh as the other places.
        private static void CotPlace()
        {
            if (_cot == null || _spotCount == 0 || _spotCount > MaxSpots || !FamilyState.AnyUnder(FamilyBalance.BabyUntil) || !ClipLibrary.Has(RockClip))
            {
                return;
            }
            var filter = new NavMeshQueryFilter { agentTypeID = AgentType, areaMask = NavMesh.AllAreas };
            for (int side = 1; side >= -1; side -= 2)
            {
                if (NavMesh.SamplePosition(_cot.position + _cot.right * (side * CotBeside), out NavMeshHit hit, 0.08f, filter))
                {
                    Spots[_spotCount] = hit.position;
                    Faces[_spotCount] = _cot.position;
                    Kinds[_spotCount] = CotKind;
                    Seats[_spotCount] = null;
                    _spotCount++;
                    return;
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

        private static bool Taken(Vector3 spot)
        {
            for (int i = 0; i < _spotCount; i++)
            {
                if (CitySpots.Flat(Spots[i] - spot) < 1.6f * 1.6f)
                {
                    return true;
                }
            }
            return (_cot != null && CitySpots.Flat(_cot.position - spot) < 1.8f * 1.8f) || (_chair != null && CitySpots.Flat(_chair.position - spot) < 1.8f * 1.8f);
        }

        private static void Arrive(float now, Vector3 player, bool wasHome)
        {
            FamilyDirector director = FamilyDirector.Instance;
            // Already home when the player walks in: at one of their places, the one furthest from the door. Coming home
            // while the player is in: in at the door, when the door is not being looked at.
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
            PlayerController controller = PlayerHelper.PlayerController;
            if (sqr < TalkReach * TalkReach && Mathf.Abs(offset.y) < 1.2f && controller != null
                && Vector3.Dot(controller.Character.transform.forward, offset.normalized) > 0.45f)
            {
                director.Prompt("reallife_family_prompt_talk", Partner.Name, TalkAction, _partner.Valid ? _partner.Character.transform : null);
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
            Seating.Check(FamilyDirector.Logger, "the partner sat down on " + item.itemName + ", " + step.ToString("F2", CultureInfo.InvariantCulture) + " m from where they stood, for " + (_nextMove - now).ToString("F0") + " s", true);
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
            PlayerController controller = PlayerHelper.PlayerController;
            if (sqr < TalkReach * TalkReach && Mathf.Abs(offset.y) < 1.2f && controller != null
                && Vector3.Dot(controller.Character.transform.forward, offset.normalized) > 0.45f)
            {
                director.Prompt("reallife_family_prompt_talk", Partner.Name, TalkAction, _partner.Valid ? _partner.Character.transform : null);
            }
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
                    // Both hands on the rail, rocking: the baby is in the cot.
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

        // Standing with the baby in the crook of the left arm (hold_baby brings the bundle), while there is a baby under a year old. Whether it was done.
        private static bool HoldBaby(CityProps props)
        {
            return FamilyState.AnyUnder(FamilyBalance.BabyUntil) && _partner.Work(HoldClip, props, 0.4f);
        }

        private static void StartTalk()
        {
            FamilyDirector director = FamilyDirector.Instance;
            if (_partner != null && !_partner.Lost && _mode == Mode.Sitting)
            {
                GetUp(Time.time, "the player spoke to them");   // they stand up to talk
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
            if (!FamilyTalk.Open(() => FamilyPhone.PartnerMenu(true), EndTalk, _partner.Character))
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

        // ---------------------------------------------------------------- shared routines

        public static bool CanCook => FamilyState.S.LivesIn && Present && FamilyState.S.LastCookDay != TimeHelper.CurrentDay;
        public static bool CanWatch => FamilyState.S.LivesIn && Present && FamilyState.S.LastWatchDay != TimeHelper.CurrentDay;

        public static DialogEntry Cook()
        {
            FamilySave s = FamilyState.S;
            s.LastCookDay = TimeHelper.CurrentDay;
            Shared(FamilyBalance.HarmonyRoutine);
            if (FamilyBridges.Home != null && FamilyBridges.Home.CookTogether(Partner.Name))
            {
                return FamilyTalk.Close("reallife_family_cook_home");
            }
            EnergyHelper.SetCurrentHunger(Mathf.Min(100f, SaveGameManager.Current.Hunger + 30f));
            FamilyModifiers.Add(FamilyModifiers.Joy, 8);
            return FamilyTalk.Close(FamilyText.Pick(CookLines, ref _lastCook));
        }

        public static DialogEntry Watch()
        {
            FamilySave s = FamilyState.S;
            s.LastWatchDay = TimeHelper.CurrentDay;
            Shared(FamilyBalance.HarmonyRoutine);
            if (FamilyBridges.Home != null && FamilyBridges.Home.WatchTogether(Partner.Name))
            {
                return FamilyTalk.Close("reallife_family_watch_home");
            }
            FamilyModifiers.Add(FamilyModifiers.Joy, 8);
            return FamilyTalk.Close(FamilyText.Pick(WatchLines, ref _lastWatch));
        }

        /// <summary>Time spent together: the home is happier, and Social (or the default record) hears of it.</summary>
        public static void Shared(float harmony)
        {
            FamilyState.Harmony(harmony);
            FamilyState.S.LastSharedDay = TimeHelper.CurrentDay;
            FamilyState.S.LastTalkDay = TimeHelper.CurrentDay;
            Partner.Tell(FamilyEvent.SharedTime, harmony * 0.6f, harmony * 0.3f);
        }

        // ---------------------------------------------------------------- an evening out

        public static bool CanPlanDate => FamilyState.Together && FamilyState.FindErrand(Errands.Dinner) == null && FamilyState.S.LastDateDay != TimeHelper.CurrentDay;

        public static DialogEntry PlanDate()
        {
            FamilyDirector director = FamilyDirector.Instance;
            string name = Partner.Name;
            if (FamilyBridges.Leisure != null && FamilyBridges.Leisure.DateNight(name, DateDone))
            {
                return FamilyTalk.Close("reallife_family_date_leisure");
            }
            if (Partner.Kept(out _) && FamilyBridges.Social.StartOuting("dinner", DateDone))
            {
                return FamilyTalk.Close("reallife_family_date_leisure");
            }
            if (!FamilyPlaces.Nearest(FamilyPlaces.Restaurant, PlayerHelper.GetCityPosition(), out Venue venue))
            {
                return FamilyTalk.Close("reallife_family_date_no_place");
            }
            int hour = TimeHelper.CurrentHour;
            int day = hour < 21 ? TimeHelper.CurrentDay : TimeHelper.CurrentDay + 1;
            FamilyState.S.Errands.Add(new Errand
            {
                Kind = Errands.Dinner, Day = day, FromHour = day == TimeHelper.CurrentDay ? Mathf.Max(hour, 12) : 17, ToHour = 23,
                Street = venue.Street, Number = venue.Number
            });
            if (day == TimeHelper.CurrentDay)
            {
                FamilyPlaces.Guide(venue);
            }
            return FamilyTalk.Close("reallife_family_date_set", Args.Us.At(venue.Name).On(FamilyText.When(day)).Cost(FamilyBalance.DinnerCost));
        }

        // An outing that Leisure or Social ran for us has ended.
        private static void DateDone(bool went)
        {
            FamilyDirector.Instance?.Guarded(() =>
            {
                if (went)
                {
                    AfterDate();
                }
                else
                {
                    StoodThemUp();
                }
            });
        }

        /// <summary>Dinner at the restaurant's door: the bill, two hours, and how the evening went.</summary>
        public static void Dinner(Errand errand)
        {
            FamilyDirector director = FamilyDirector.Instance;
            if (!FamilyTalk.Pay(FamilyBalance.DinnerCost, "reallife_transaction_family_dinner"))
            {
                return;   // the errand stays: come back with the money
            }
            FamilyState.MoneyOut(FamilyBalance.DinnerCost, 4);
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            director.Run(director.Passage("reallife_family_passage_dinner", FamilyBalance.DinnerHours,
                () => EnergyHelper.SetCurrentHunger(100f),
                () =>
                {
                    AfterDate();
                    FamilyText.Toast(NotificationType.Success, FamilyText.Pick(DinnerLines, ref _lastDinner), Args.Us, 10f);
                }));
        }

        private static void AfterDate()
        {
            FamilySave s = FamilyState.S;
            s.LastDateDay = TimeHelper.CurrentDay;
            s.Dates++;
            Shared(FamilyBalance.HarmonyDate);
            Partner.Tell(FamilyEvent.SharedTime, FamilyBalance.AffectionDate, 3f);
            FamilyModifiers.Add(FamilyModifiers.Joy);
            if (s.Dates == 1)
            {
                FamilyState.Remember("reallife_family_memory_first_date", Partner.Name);
            }
        }

        public static void StoodThemUp()
        {
            FamilySave s = FamilyState.S;
            s.BrokenPromises++;
            FamilyState.Harmony(FamilyBalance.HarmonyBrokenPromise);
            Partner.Tell(FamilyEvent.BrokenPromise, -6f, -6f);
            FamilyText.Message(FamilyPhone.PartnerContact, FamilyText.Pick(StoodUp, ref _lastStood), Args.Us);
        }

        // ---------------------------------------------------------------- by the hour and the day

        /// <summary>Their share of the rent and bills, once a day in the evening, as a line of income.</summary>
        public static void Contribution(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (!s.LivesIn || !FamilyState.Together || hour < 18 || s.LastContributionDay == day)
            {
                return;
            }
            s.LastContributionDay = day;
            BuildingRegistration home = FamilyPlaces.Home();
            if (home == null)
            {
                return;   // no home just now: nothing to share the cost of
            }
            // What the home costs a day: the game's rent at today's prices, as the city's cost of living charges it, and its
            // power and water (Utilities). With no bills to go by (the feature off) an owned home's are a set sum.
            float rent = Mathf.Max(0f, home.RentPerDay) * Prices.Level / 1000f;
            Func<BuildingRegistration, float> bills = Bills;
            float cost = rent + (bills != null ? Mathf.Max(0f, bills(home)) : 0f);
            float amount = rent > 0f || bills != null ? Mathf.Min(cost * FamilyBalance.RentShare, Partner.Income * FamilyBalance.IncomeShare)
                : Mathf.Min(FamilyBalance.BillsCap, Partner.Income * 0.2f);
            if (FamilyState.Care == Care.Partner && FamilyState.AnyUnder(FamilyBalance.SchoolFrom))
            {
                amount *= 0.5f;   // short days at work
            }
            int paid = Mathf.Clamp(Mathf.RoundToInt(amount), 0, FamilyBalance.ContributionCap);
            if (paid > 0)
            {
                FamilyTalk.Move(paid, "reallife_transaction_family_contribution");
                FamilyState.MoneyIn(paid);
            }
        }

        /// <summary>A small thing asked of the player now and then; doing it is noticed, and so is never doing it.</summary>
        public static void Chore(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (!s.LivesIn || !FamilyState.Together || !Settings.FamilyFriction || hour < 9 || hour > 21 || s.LastChoreDay == day)
            {
                return;
            }
            s.LastChoreDay = day;
            if (Random.value > 0.3f || FamilyDirector.Instance.HasQuestions)
            {
                return;
            }
            FamilyDirector.Instance.Ask("reallife_family_chore_header", FamilyText.Pick(ChoreAsks, ref _lastChore), Args.Us,
                "reallife_family_chore_yes", "reallife_family_chore_no",
                () =>
                {
                    if (SaveGameManager.Current.Energy > 25f)
                    {
                        EnergyHelper.SpentEnergyOnce(4f);
                    }
                    FamilyState.Harmony(FamilyBalance.HarmonyChore);
                    FamilyState.S.LastSharedDay = TimeHelper.CurrentDay;
                    FamilyText.Toast(NotificationType.Success, FamilyText.Pick(ChoreThanks, ref _lastThanks), Args.Us, 6f);
                },
                () =>
                {
                    FamilyState.Harmony(-2f);
                    FamilyText.Toast(NotificationType.Info, FamilyText.Pick(ChoreSighs, ref _lastSigh), Args.Us, 6f);
                });
        }

        /// <summary>After a bad day, or when the player is ill, the person at home notices.</summary>
        public static void Support(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (!s.LivesIn || !FamilyState.Together || hour < 19 || s.LastSupportDay == day || Where(day, hour) != Whereabouts.Home || !LifeEvents.IsSafeForEvent())
            {
                return;
            }
            if (ModState.IsSick && s.LastSickCareDay != day)
            {
                s.LastSupportDay = day;
                s.LastSickCareDay = day;
                if (FamilyBridges.Home == null || !FamilyBridges.Home.CareWhenSick(Partner.Name))
                {
                    ModState.SickUntil = Mathf.Max(TimeHelper.NowInMinutes() + 60f, ModState.SickUntil - 8f * 60f);   // a night of being looked after
                    ModState.AddModifier(ModState.MoodUp);
                }
                FamilyState.Harmony(2f);
                FamilyText.Toast(NotificationType.Success, FamilyText.Pick(SickCare, ref _lastSick), Args.Us, 9f);
                return;
            }
            if (SaveGameManager.Current.Happiness < 35f)
            {
                s.LastSupportDay = day;
                ModState.AddModifier(ModState.MoodUp);
                FamilyText.Toast(NotificationType.Success, FamilyText.Pick(Encourage, ref _lastEncourage), Args.Us, 9f);
            }
        }

        /// <summary>Once a day: a home is kept warm by time spent in it, and cools without.</summary>
        public static void Daily(int day)
        {
            FamilySave s = FamilyState.S;
            if (!FamilyState.Together)
            {
                return;
            }
            int quiet = day - Mathf.Max(s.LastTalkDay, s.LastSharedDay);
            if (quiet <= 1)
            {
                FamilyState.Harmony(0.8f);
            }
            else if (s.LivesIn)
            {
                FamilyState.Harmony(FamilyBalance.HarmonyDriftPerDay);
            }
            if (quiet >= FamilyBalance.NeglectDays)
            {
                FamilyState.Harmony(-1.5f);
                Partner.Tell(FamilyEvent.BrokenPromise, -FamilyBalance.AffectionNeglectPerDay, -0.5f);
                if (day - s.LastNeglectDay >= 3)
                {
                    s.LastNeglectDay = day;
                    FamilyText.Message(FamilyPhone.PartnerContact, FamilyText.Pick(Missing, ref _lastMissing), Args.Us);
                }
            }
            FamilyModifiers.Keep(FamilyModifiers.Together, s.LivesIn && FamilyState.Bond == Bond.Dating && s.Harmony >= 40f);
        }
    }
}
