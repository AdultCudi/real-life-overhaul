using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The roommate in the flat as a real character, as the partner is (Family/HomeLife): at their places, walking, sitting
    /// a while on a free seat, talked to through the game's panel over them; asleep or in their room, in but not shown.
    /// They keep clear of the door, the furniture and the partner. Cost: <see cref="Tick"/> once a second while the player
    /// is in that home; nothing per frame but the cast's turn and hide.
    /// </summary>
    public sealed class RoommateFlat
    {
        private enum Mode { None, Standing, Walking, Leaving, Retiring, Talking, Sitting }

        private const int MaxSpots = 5;
        private const int Kitchen = 0, Living = 1, Floor = 2, SeatKind = 3;
        private const float TalkReach = 2.3f;        // metres
        private const float KeepOffPartner = 1.6f;   // metres from where the partner is

        private static readonly string[] KitchenItems = { "stove", "fridge", "coffeemachine", "microwave", "kitchen", "sink", "oven" };
        private static readonly string[] LivingItems = { "sofa", "couch", "itemname_tv", "television", "armchair", "bookshel", "coffeetable" };
        private static readonly string[] Greets = { "reallife_roommate_greet_morning", "reallife_roommate_greet_day", "reallife_roommate_greet_evening", "reallife_roommate_greet_late" };

        private readonly IModLogger _logger;
        private readonly RoommateCast _cast;
        private readonly DoorPanel _door;
        private readonly Action _onPrompt;
        private readonly Func<string> _promptWhat;
        private readonly Action _talk;
        private readonly Action _talkEnded;

        private readonly Vector3[] _spots = new Vector3[MaxSpots];
        private readonly Vector3[] _faces = new Vector3[MaxSpots];
        private readonly int[] _kinds = new int[MaxSpots];
        private readonly ItemController[] _seats = new ItemController[MaxSpots];
        private int _spotCount;
        private int _at = -1;
        private int _agentType = -1;
        private Vector3 _entry;
        private bool _inside;
        private bool _justEntered;
        private bool _toldAway;
        private bool _placeFailedLogged;
        private object _building;
        private CityPerson _person;
        private Mode _mode;
        private float _nextMove;
        private float _giveUp;
        private float _satAt;
        private string _promptName;
        private string _promptText;
        private bool _promptOn;
        private bool _promptWanted;

        public RoommateFlat(Transform parent, IModLogger logger)
        {
            _logger = logger;
            _cast = new RoommateCast(parent, logger);
            _door = new DoorPanel(logger, "Roommates check (prompt panel)");
            _onPrompt = _door.Press(PromptPressed);
            _promptWhat = () => "talk to the roommate";
            _talk = StartTalk;
            _talkEnded = EndTalk;
        }

        public bool Present => _person != null && !_person.Lost;

        public bool Inside => _inside;

        /// <summary>True while the cast has somebody out: the director then gives it its frame.</summary>
        public bool Out => _cast.Out;

        public bool TakesKey => _door.TakesKey;

        public void Frame(float delta)
        {
            _cast.Tick(delta);
        }

        public void Dispose()
        {
            Leave();
            _cast.Dispose();
        }

        public void Tick(float now, RoommateState s, RoomWhere where)
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || manager.enteringBuilding || manager.exitingBuilding)
            {
                return;
            }
            Vector3 player = PlayerHelper.GetPosition();
            if (!_inside || !ReferenceEquals(_building, manager.building))
            {
                Leave();
                _inside = true;
                _building = manager.building;
                _entry = player;
                _justEntered = true;
                _toldAway = false;
                Scan(manager, player);
            }
            _promptWanted = false;
            if (_person != null && _person.Lost)
            {
                _person = null;
                _mode = Mode.None;
            }
            string name = s.Mate.Person.Name;
            switch (_mode)
            {
                case Mode.None:
                    if (where == RoomWhere.In && !_cast.Failed && _spotCount > 0)
                    {
                        Arrive(now, s, player, _justEntered);
                    }
                    else if (_justEntered && !_toldAway && LifeEvents.IsSafeForEvent())
                    {
                        _toldAway = true;
                        string key = where == RoomWhere.Asleep ? "reallife_roommate_home_asleep" : where == RoomWhere.Work ? "reallife_roommate_home_work"
                            : where == RoomWhere.Out ? "reallife_roommate_home_out" : where == RoomWhere.InRoom ? "reallife_roommate_home_room" : "reallife_roommate_home_in";
                        RoommateText.Toast(NotificationType.Info, key, Fill.For(name).With(RoommateText.Job(s.Mate.Person)), 6f);
                    }
                    break;
                case Mode.Standing:
                    if (RoommateRules.Hidden(where))
                    {
                        _mode = Mode.Retiring;
                        _giveUp = now + 120f;
                    }
                    else if (where != RoomWhere.In)
                    {
                        RoommateText.Toast(NotificationType.Info, where == RoomWhere.Work ? "reallife_roommate_home_leaving_work" : "reallife_roommate_home_leaving_out", Fill.For(name), 6f);
                        _person.PutAway(_cast.Props);
                        _mode = _person.WalkTo(_entry) ? Mode.Leaving : Mode.Retiring;
                        _giveUp = now + 25f;
                    }
                    else
                    {
                        Standing(now, player, name);
                    }
                    break;
                case Mode.Sitting:
                    Sitting(now, player, where == RoomWhere.In, name);
                    break;
                case Mode.Walking:
                    if ((_person.Arrived && _person.Near(_spots[_at], 0.7f)) || now >= _giveUp)
                    {
                        Settle(now, _person.Near(_spots[_at], 0.7f));
                    }
                    break;
                case Mode.Leaving:
                    if (_person.Near(_entry, 0.9f) || (now >= _giveUp && !CitySpots.InView(_person.Position)))
                    {
                        Release();
                    }
                    break;
                case Mode.Retiring:
                    // Gone to bed, to their room (or out) the moment nobody is looking.
                    if (!CitySpots.InView(_person.Position))
                    {
                        Release();
                        RoommateText.Toast(NotificationType.Info, where == RoomWhere.Asleep ? "reallife_roommate_home_to_bed" : where == RoomWhere.InRoom ? "reallife_roommate_home_to_room"
                            : "reallife_roommate_home_leaving_out", Fill.For(name), 6f);
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
            if (!_promptWanted)
            {
                ClearPrompt();
            }
        }

        /// <summary>The player has left the home, or the roommate has: nobody of ours stays in it. Safe at any time.</summary>
        public void Leave()
        {
            Release();
            ClearPrompt();
            _inside = false;
            _building = null;
            _spotCount = 0;
            _at = -1;
            Array.Clear(_seats, 0, _seats.Length);
        }

        private void Release()
        {
            _cast.Release();
            _person = null;
            _mode = Mode.None;
        }

        // The places they stand: beside what the game calls a cooker, a fridge, a sofa, a television; one seat; failing all, clear floor.
        private void Scan(BuildingManager manager, Vector3 player)
        {
            _spotCount = 0;
            _at = -1;
            _agentType = -1;
            Array.Clear(_seats, 0, _seats.Length);
            List<ItemController> items = manager.allItemControllers;
            int kitchens = 0, livings = 0, seats = 0, seatsInHome = 0;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] != null && items[i].isActiveAndEnabled && Seating.IsSeat(items[i]))
                {
                    seatsInHome++;
                }
            }
            for (int i = 0; items != null && i < items.Count && _spotCount < MaxSpots - 1; i++)
            {
                ItemController item = items[i];
                if (item == null || !item.isActiveAndEnabled || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }
                int kind = Seating.IsSeat(item) ? SeatKind : Is(item.itemName, KitchenItems) ? Kitchen : Is(item.itemName, LivingItems) ? Living : -1;
                if (kind < 0 || (kind == Kitchen && kitchens >= 2) || (kind == Living && livings >= 1) || (kind == SeatKind && (seats >= 1 || seatsInHome < 2)))
                {
                    continue;
                }
                Vector3 at = item.transform.position;
                if (!FamilyPlaces.Floor(at, player, 0.4f, 1.6f, out Vector3 spot, out int type) || !Usable(spot, at, type))
                {
                    continue;
                }
                _seats[_spotCount] = kind == SeatKind ? item : null;
                Add(spot, at, kind, type);
                if (kind == Kitchen)
                {
                    kitchens++;
                }
                else if (kind == Living)
                {
                    livings++;
                }
                else
                {
                    seats++;
                }
            }
            for (int ring = 0; ring < 2 && _spotCount < 2; ring++)
            {
                float radius = 3.4f + ring * 1.6f;
                for (int i = 0; i < 8 && _spotCount < 2; i++)
                {
                    float angle = (i + 0.5f + ring * 0.5f) * Mathf.PI / 4f;   // (half a step round from where the partner's places are looked for)
                    Vector3 near = player + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (FamilyPlaces.Floor(near, player, 0.55f, 0.5f, out Vector3 spot, out int type) && Usable(spot, spot, type))
                    {
                        Add(spot, player, Floor, type);
                    }
                }
            }
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Roommates check (places): this home has " + _spotCount + " place(s) for the roommate, " + seats + " of them a seat (" + seatsInHome
                              + " seat(s) in the home; they sit only where there are two or more): " + (_spotCount > 0 ? "PASS" : "FAIL (they are not shown here)") + ".");
            }
            if (_spotCount == 0 && !_placeFailedLogged)
            {
                _placeFailedLogged = true;
                _logger?.Info("Roommates: no clear floor on a navigation mesh was found in this home; the roommate is not shown here (they still live here).");
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
        private bool Usable(Vector3 spot, Vector3 item, int type)
        {
            if (_agentType >= 0 && type != AgentType)
            {
                return false;
            }
            if (CitySpots.Flat(spot - _entry) < 1.8f * 1.8f || CitySpots.Flat(spot - item) > 2.0f * 2.0f)
            {
                return false;
            }
            for (int i = 0; i < _spotCount; i++)
            {
                if (CitySpots.Flat(_spots[i] - spot) < 1.5f * 1.5f)
                {
                    return false;
                }
            }
            return true;
        }

        private void Add(Vector3 spot, Vector3 face, int kind, int type)
        {
            _agentType = type == NavMeshHelperIds.Npc ? 0 : 1;
            _spots[_spotCount] = spot;
            _faces[_spotCount] = face;
            _kinds[_spotCount] = kind;
            _spotCount++;
        }

        private int AgentType => _agentType == 0 ? NavMeshHelperIds.Npc : NavMeshHelperIds.Player;

        private static bool PartnerAt(Vector3 spot)
        {
            Transform partner = RoommateBridges.PartnerBody != null ? RoommateBridges.PartnerBody() : null;
            return partner != null && CitySpots.Flat(partner.position - spot) < KeepOffPartner * KeepOffPartner;
        }

        private void Arrive(float now, RoommateState s, Vector3 player, bool wasHome)
        {
            // Already home when the player walks in: at one of their places, the one furthest from the door that the
            // partner is not at. Coming home while the player is in: in at the door, when the door is not being looked at.
            int at = -1;
            Vector3 spot;
            if (wasHome)
            {
                float best = -1f;
                for (int i = 0; i < _spotCount; i++)
                {
                    float sqr = CitySpots.Flat(_spots[i] - player);
                    if (sqr > best && !PartnerAt(_spots[i]))
                    {
                        best = sqr;
                        at = i;
                    }
                }
                if (at < 0)
                {
                    return;   // the partner has the only place: tried again next second, when they have moved on
                }
                spot = _spots[at];
            }
            else
            {
                if (CitySpots.InView(_entry))
                {
                    return;   // nobody appears in a doorway that is being looked at
                }
                if (!FamilyPlaces.Floor(_entry, player, 0.3f, 0.8f, out spot, out int type) || type != AgentType || (spot - player).sqrMagnitude < 1.2f)
                {
                    return;
                }
            }
            CityPerson person = _cast.Put(s.Mate.Person, spot, wasHome ? _faces[at] : player, AgentType);
            if (person == null)
            {
                if (!_placeFailedLogged)
                {
                    _placeFailedLogged = true;
                    _logger?.Info("Roommates: the roommate could not be put on the floor of this home; they are not shown here.");
                }
                _spotCount = 0;   // not asked again in this home
                return;
            }
            _person = person;
            if (wasHome)
            {
                _at = at;
                _mode = Mode.Standing;
                _nextMove = now + Random.Range(35f, 70f);
                Activity(at);
                Greet(s, player);
            }
            else
            {
                RoommateText.Toast(NotificationType.Info, "reallife_roommate_home_arrives", Fill.For(s.Mate.Person.Name), 6f);
                _at = Random.Range(0, _spotCount);
                _mode = !PartnerAt(_spots[_at]) && person.WalkTo(_spots[_at]) ? Mode.Walking : Mode.Standing;
                _giveUp = now + 30f;
                _nextMove = now + 60f;
            }
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Roommates check (at home): " + s.Mate.Person.Name + " is shown " + (wasHome ? "at place " + at : "coming in at the door") + ", "
                              + Mathf.Sqrt(CitySpots.Flat(person.Position - _entry)).ToString("F1", CultureInfo.InvariantCulture)
                              + " m from the door (wanted more than 0.8 m unless coming in): PASS.");
            }
        }

        private void Greet(RoommateState s, Vector3 player)
        {
            int stamp = RoommateGame.Stamp;
            if (stamp - s.LastGreetStamp < 2 || !LifeEvents.IsSafeForEvent())
            {
                return;
            }
            s.LastGreetStamp = stamp;
            _person.PutAway(_cast.Props);
            _cast.Face(player);
            _person.Work("wave_hello", _cast.Props, 0.25f);
            _nextMove = Mathf.Max(_nextMove, Time.time + 12f);
            int hour = RoommateGame.Hour;
            RoommateText.Toast(NotificationType.Info, Greets[hour < 11 ? 0 : hour < 17 ? 1 : hour < 22 ? 2 : 3], Fill.For(s.Mate.Person.Name), 7f);
        }

        private void Standing(float now, Vector3 player, string name)
        {
            if (_person.ClipOver && _person.Playing != null)
            {
                Activity(_at);
            }
            else if (_person.Playing == "cup_hold" && Random.value < 0.07f)
            {
                _person.Work("sip", _cast.Props, 0.3f);
            }
            float sqr = CitySpots.Flat(_person.Position - player);
            if (Reachable(player))
            {
                Prompt(name);
                return;
            }
            if (now >= _nextMove && _spotCount > 1 && sqr > 2.5f * 2.5f)
            {
                int next = (_at + 1 + Random.Range(0, _spotCount - 1)) % _spotCount;
                if (PartnerAt(_spots[next]))
                {
                    _nextMove = now + 20f;   // the partner is there: stay put and look again
                    return;
                }
                _person.PutAway(_cast.Props);
                if (_person.WalkTo(_spots[next]))
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

        private bool Reachable(Vector3 player)
        {
            Vector3 offset = _person.Position - player;
            PlayerController controller = PlayerHelper.PlayerController;
            return CitySpots.Flat(offset) < TalkReach * TalkReach && Mathf.Abs(offset.y) < 1.2f && controller != null && controller.Character != null
                   && Vector3.Dot(controller.Character.transform.forward, offset.normalized) > 0.45f;
        }

        private void Settle(float now, bool there)
        {
            _person.Stand();
            _mode = Mode.Standing;
            _nextMove = now + Random.Range(45f, 100f);
            if (there && _kinds[_at] == SeatKind && SitDown(now))
            {
                return;
            }
            if (there)
            {
                _cast.Face(_faces[_at]);
                Activity(_at);
            }
        }

        // Onto the seat they have walked up to, if nobody is on it (the player and the partner least of all) and it is within a step.
        private bool SitDown(float now)
        {
            ItemController item = _seats[_at];
            Transform place = item != null ? Seating.Free(item) : null;
            if (place == null || !Seating.InReach(_person.Position, place))
            {
                Seating.Check(_logger, "the roommate stays standing by " + (item != null ? item.itemName : "a seat that has gone") + " (in use, or out of reach)", true);
                return false;
            }
            _person.PutAway(_cast.Props);
            if (!_person.Sit(place))
            {
                Seating.Check(_logger, "the roommate could not be seated on " + item.itemName, false);
                return false;
            }
            _mode = Mode.Sitting;
            _satAt = now;
            _nextMove = now + Random.Range(70f, 160f);
            Seating.Check(_logger, "the roommate sat down on " + item.itemName + " for " + (_nextMove - now).ToString("F0") + " s", true);
            return true;
        }

        private void Sitting(float now, Vector3 player, bool staying, string name)
        {
            ItemController item = _at >= 0 ? _seats[_at] : null;
            float sqr = CitySpots.Flat(_person.Position - player);
            bool seatGone = item == null || !item.isActiveAndEnabled || !_person.Seated;
            if (!staying || seatGone || (now >= _nextMove && sqr > 2.5f * 2.5f))
            {
                GetUp(now, seatGone ? "the seat was taken away" : !staying ? "it is time to go" : "to walk on");
                return;
            }
            if (Reachable(player))
            {
                Prompt(name);
            }
        }

        private void GetUp(float now, string why)
        {
            if (_person == null)
            {
                return;
            }
            _person.Rise();
            if (_mode == Mode.Sitting)
            {
                _mode = Mode.Standing;
                Seating.Check(_logger, "the roommate got up after " + (now - _satAt).ToString("F0") + " s (" + why + ")", !_person.Seated);
            }
        }

        // What they do where they stand. A clip that cannot be had leaves them standing as the game stands people.
        private void Activity(int at)
        {
            if (_person == null || at < 0)
            {
                return;
            }
            CityProps props = _cast.Props;
            int hour = RoommateGame.Hour;
            bool meal = (hour >= 7 && hour < 9) || (hour >= 12 && hour < 14) || (hour >= 18 && hour < 21);
            int turn = (RoommateGame.Day + at + hour) % 3;
            bool done;
            switch (_kinds[at])
            {
                case Kitchen:
                    done = (meal && _person.Work("eat_standing", props, 0.4f)) || _person.Work("cup_hold", props, 0.4f);
                    break;
                case Living:
                case SeatKind:   // (standing by a seat that was not free: as in the living room)
                    done = turn == 0 ? _person.Work("phone_call", props, 0.4f) : turn == 1 ? _person.Work("read_paper", props, 0.4f) : _person.Work("watch_nod", props, 0.4f);
                    done = done || _person.Work("watch_nod", props, 0.4f);
                    break;
                default:
                    done = turn == 0 && _person.Work("phone_call", props, 0.4f);
                    break;
            }
            if (!done)
            {
                _person.Work("arms_crossed", props, 0.4f);
            }
        }

        // The game's panel over them, its one button on the player's key; the partner's, a job's or home life's panel has the key first.
        private void Prompt(string name)
        {
            _promptWanted = true;
            if (!ReferenceEquals(name, _promptName))
            {
                _promptName = name;
                string line = RoommateText.T("reallife_roommate_prompt_talk", Fill.For(name)).Replace("{key}: ", string.Empty);
                _promptText = line.Length > 0 ? char.ToUpperInvariant(line[0]) + line.Substring(1) : line;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (FamilyDirector.CanAct() && !FamilyDirector.TakesInteractKey && !HomeDirector.TakesInteractKey && player != null)
            {
                WorldPanel panel = _door.Panel;
                panel.Lines(_promptText, null, null, WorldPanel.White());
                panel.Button(0, "reallife_roommate_button_talk", _onPrompt, true, true);
                panel.Button(1, null, null, false, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
                panel.Follow = _person.Valid ? _person.Character.transform : null;
                panel.FollowLift = 2.1f;
                _promptOn = _door.Show(player.transform.position, player.transform.position, 2.1f, _promptWhat);
            }
            else if (_promptOn || _door.Up)
            {
                ClearPrompt();
            }
        }

        private void PromptPressed()
        {
            if (!FamilyDirector.CanAct())
            {
                return;
            }
            ClearPrompt();
            RoommateDirector.Instance?.Guarded(_talk);
        }

        private void ClearPrompt()
        {
            if (_promptOn || _door.Up)
            {
                _promptOn = false;
                _door.Panel.Follow = null;
                _door.Hide("nothing to offer here now");
            }
        }

        private void StartTalk()
        {
            if (_person != null && !_person.Lost && _mode == Mode.Sitting)
            {
                GetUp(Time.time, "the player spoke to them");   // they stand up to talk
            }
            if (_person == null || _person.Lost || _mode != Mode.Standing)
            {
                return;
            }
            _person.PutAway(_cast.Props);
            _person.Stand();
            _cast.Face(PlayerHelper.GetPosition());
            _person.Work("talk_a", _cast.Props, 0.3f);
            _mode = Mode.Talking;
            _giveUp = Time.time + 3f;
            if (!RoommateTalk.Open(_talkEnded, _person.Character))
            {
                EndTalk();
            }
        }

        private void EndTalk()
        {
            if (_mode == Mode.Talking)
            {
                _mode = Mode.Standing;
                _nextMove = Time.time + 25f;
                if (_person != null && !_person.Lost)
                {
                    _person.Idle(0.3f);
                }
            }
        }
    }
}
