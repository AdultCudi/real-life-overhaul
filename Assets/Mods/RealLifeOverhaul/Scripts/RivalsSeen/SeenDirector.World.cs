using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The director's part for the world: a rival whose week has them at a place near the player stands there, on the pavement by its door, as one of the living
    /// city's own people exactly as that pool dresses them (no character of their own is made: CityLife/CityPeople), with their name on the game's own panel and a
    /// greeting on its button; their car passes on the lane outside as one of the traffic pool's own (SeenGame.PassBy). Who is where is worked out when the hour turns;
    /// every <see cref="SeenCatalogue.LookSeconds"/> seconds the places are set against where the player is (a subtraction each), and one rival at most is put out a
    /// look, only out of view, never past <see cref="SeenCatalogue.WorldMost"/> at once and never taking the living city's last people. They go when their hour there
    /// is over (out of view), when the player leaves them far behind, indoors, on a time skip, with the option, or in a Going Public session. Nothing a frame.
    /// </summary>
    public sealed partial class SeenDirector
    {
        private struct Standing
        {
            public CityPerson Person;
            public string Key, Name, Place;
            public Vector3 At;
            public SeenSpot Spot;
            public bool Test;    // put out by a test button: it stays until left behind, whatever the hour
        }

        private readonly Standing[] _out = new Standing[SeenCatalogue.WorldMost];
        private readonly List<SeenDue> _due = new List<SeenDue>(32);
        private readonly List<CityBuildingController> _venues = new List<CityBuildingController>(32);
        private readonly List<string> _venueNames = new List<string>(32);
        private int _outCount;
        private int _dueHour = -1, _dueDay = -1;
        private float _nextLook;
        private DoorPanel _door;
        private Action _onHello;
        private Func<string> _doorWhat;
        private int _nearest = -1;      // whose panel is up, by their place in _out; -1 for none
        private string _linesFor;       // the panel's words are made again only when it is over somebody else
        private string _lineName, _lineWhere;

        /// <summary>How many rivals stand in the world now.</summary>
        public int OutNow => _outCount;

        private void World(int day, int hour, float now)
        {
            CityLifeDirector city = CityLifeDirector.Instance;
            PlayerController player = PlayerHelper.PlayerController;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (city == null || city.People == null || !Settings.CityLife || player == null || uis == null || LoadScene.isLoading || BuildingManager.IsInsideBuilding || uis.timeMachine.isRunning)
            {
                ClearWorld("the street is gone");   // indoors, a time skip, or the living city (whose people these are) switched off
                return;
            }
            if (uis.gameSpeed.Paused)
            {
                return;
            }
            Vector3 origin = player.transform.position;
            if (hour != _dueHour || day != _dueDay)
            {
                Places(day, hour);
            }
            Tend(city, origin, day, hour);
            Panel(origin);
            if (now < _nextLook)
            {
                return;
            }
            _nextLook = now + SeenCatalogue.LookSeconds;
            if (!TrafficAccidents.IsActive && !StreetCrime.IsActive)
            {
                PutOne(city, origin, day, hour, false);
            }
        }

        // The hour has turned: who is somewhere now, and the building each is at. A pass over the day's rivals and the district's eating places.
        private void Places(int day, int hour)
        {
            _dueHour = hour;
            _dueDay = day;
            _venues.Clear();
            _venueNames.Clear();
            SeenRules.Due(_file, _subjects, day, hour, _due);
            for (int i = 0; i < _due.Count; i++)
            {
                _venues.Add(SeenGame.City.Venue(_file, _subjects[_due[i].Subject], _due[i], day, hour, out string name));
                _venueNames.Add(name);
            }
        }

        // Those standing: gone with whoever took them (the pool's own clearing), left far behind, or their hour at the place over and nobody looking.
        private void Tend(CityLifeDirector city, Vector3 origin, int day, int hour)
        {
            for (int i = _outCount - 1; i >= 0; i--)
            {
                Standing s = _out[i];
                float sqr = CitySpots.Flat(s.At - origin);
                bool lost = s.Person == null || s.Person.Lost;
                bool over = !s.Test && !Still(s.Key, s.Spot);
                if (lost || SeenRules.LeftBehind(sqr) || (over && !CitySpots.InView(s.At)))
                {
                    Leave(city, i, lost ? "taken by the living city" : over ? "their hour there is over" : "left behind");
                    continue;
                }
                if (sqr < (float)SeenCatalogue.TalkMetres * SeenCatalogue.TalkMetres)
                {
                    SeenRecord r = _file.Of(s.Key);
                    if (r != null)
                    {
                        r.MetDay = day;
                    }
                }
            }
        }

        // True while the hour's list still has that rival at that place.
        private bool Still(string key, SeenSpot spot)
        {
            for (int i = 0; i < _due.Count; i++)
            {
                if (_subjects[_due[i].Subject].Key == key)
                {
                    return _due[i].Where.Spot == spot;
                }
            }
            return false;
        }

        // One rival whose place is near and out of view is put out, if the cap and the pool allow. A test puts them out in view.
        private bool PutOne(CityLifeDirector city, Vector3 origin, int day, int hour, bool test)
        {
            for (int i = 0; i < _due.Count; i++)
            {
                if (!SeenRules.MayPut(_outCount, city.People.Free))
                {
                    return false;
                }
                CityBuildingController venue = _venues[i];
                SeenSubject who = _subjects[_due[i].Subject];
                if (venue == null || IsOut(who.Key) || !SeenRules.InRing(CitySpots.Flat(venue.transform.position - origin)))
                {
                    continue;
                }
                SeenSpot spot = _due[i].Where.Spot;
                bool meal = spot != SeenSpot.Office && spot != SeenSpot.Late;
                if (meal && !BusinessHelper.IsBusinessOpen(venue.buildingRegistration))
                {
                    continue;   // shut today for a reason no timetable knew (a fire, an inspection): nobody eats there
                }
                if (Put(city, venue, who, spot, _venueNames[i], origin, test))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsOut(string key)
        {
            for (int i = 0; i < _outCount; i++)
            {
                if (_out[i].Key == key)
                {
                    return true;
                }
            }
            return false;
        }

        // On the pavement by the door, a step or two to one side of it, facing the street; then their car on the lane outside.
        private bool Put(CityLifeDirector city, CityBuildingController venue, SeenSubject who, SeenSpot spot, string place, Vector3 origin, bool test)
        {
            if (!JobSpots.Front(venue, out Vector3 front) || !CitySpots.Along(front, out Vector3 along) || !JobSpots.DoorOf(venue, out Vector3 door))
            {
                return false;
            }
            Vector3 outward = front - door;
            outward.y = 0f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 at = front + along * (side * 1.7f);
                if (!CitySpots.Stand(at, 0.35f, out at) || (!test && CitySpots.InView(at)))
                {
                    continue;
                }
                city.People.AtNeed = test;   // a test button cannot wait for a look to be made ready
                CityPerson person = city.People.Take(CityLook.Civilian, at, at + (outward.sqrMagnitude > 0.01f ? outward.normalized : along));
                city.People.AtNeed = false;
                if (person == null)
                {
                    return false;   // nobody dressed and free this look: the next look tries again
                }
                _out[_outCount++] = new Standing { Person = person, Key = who.Key, Name = who.Name, Place = place ?? string.Empty, At = at, Spot = spot, Test = test };
                SeenRecord r = _file.Of(who.Key);
                bool car = r != null && r.Car.Length > 0 && SeenGame.PassBy(r.Car, front, origin);
                Logger?.Info("Rivals seen: " + who.Name + " stands at " + (place ?? "?") + " (" + spot + "), " + Mathf.Sqrt(CitySpots.Flat(at - origin)).ToString("F0") + " m away"
                             + (test ? " (test)" : string.Empty) + "; " + _outCount + " of " + SeenCatalogue.WorldMost + " out; their car "
                             + (car ? "(" + r.Car + ") is on the lane outside." : "is not on the road (no idle car of the model in the traffic pool, or no lane there)."));
                if (Verbose)
                {
                    Logger?.Info("Rivals seen check (in the world): " + _outCount + " out of " + SeenCatalogue.WorldMost + " at most, the living city keeps " + city.People.Free
                                 + " of its people free: " + (_outCount <= SeenCatalogue.WorldMost ? "PASS" : "PAST THE CAP"));
                }
                return true;
            }
            return false;
        }

        private void Leave(CityLifeDirector city, int index, string why)
        {
            Standing s = _out[index];
            try
            {
                if (city != null && city.People != null && s.Person != null && !s.Person.Lost)
                {
                    city.People.Release(s.Person);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Logger?.Info("Rivals seen: " + s.Name + " is gone from " + (s.Place.Length > 0 ? s.Place : "the street") + " (" + why + ").");
            for (int k = index; k < _outCount - 1; k++)
            {
                _out[k] = _out[k + 1];
            }
            _out[--_outCount] = default;
            _nearest = -1;
            _linesFor = null;
        }

        private void ClearWorld(string why)
        {
            _door?.Hide(why);
            if (_outCount == 0)
            {
                return;
            }
            CityLifeDirector city = CityLifeDirector.Instance;
            for (int i = _outCount - 1; i >= 0; i--)
            {
                Leave(city, i, why);
            }
        }

        // The game's own panel over the nearest rival within talking distance, on foot: their name, where they are, and Say hello.
        private void Panel(Vector3 origin)
        {
            int near = -1;
            float best = (float)SeenCatalogue.TalkMetres * SeenCatalogue.TalkMetres;
            for (int i = 0; i < _outCount; i++)
            {
                float sqr = CitySpots.Flat(_out[i].At - origin);
                if (sqr < best && _out[i].Person != null && !_out[i].Person.Lost)
                {
                    best = sqr;
                    near = i;
                }
            }
            if (near < 0 || SeenGame.PlayerCar() != null || ThirdPersonCamera.IsUiOpen() || SocialPhone.DialogOpen)
            {
                _door?.Hide(near < 0 ? "nobody near" : "the player is busy");
                _nearest = -1;
                return;
            }
            if (_door == null)
            {
                _door = new DoorPanel(Logger, "Rivals seen check (greeting panel)");
                _onHello = _door.Press(Hello);
                _doorWhat = () => _nearest >= 0 && _nearest < _outCount ? "beside " + _out[_nearest].Name : "beside a rival";
            }
            _nearest = near;
            Standing s = _out[near];
            if (!ReferenceEquals(_linesFor, s.Key))
            {
                _linesFor = s.Key;
                _lineName = s.Name;
                _lineWhere = s.Place.Length > 0 ? SeenText.Fill("reallife_seen_panel_at", "spot", SeenText.Spot(s.Spot), "place", s.Place) : SeenText.Spot(s.Spot);
            }
            WorldPanel panel = _door.Panel;
            panel.Lines(_lineName, _lineWhere, null, WorldPanel.White());
            panel.Button(0, "reallife_seen_btn_hello", _onHello, true, true);
            panel.Button(1, null, null, false, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
            panel.Follow = s.Person.Character.transform;
            panel.FollowLift = 2.1f;
            panel.Meet = true;   // over the person met: nearest of all within talking range (Core/GameUi/PanelPickRules)
            _door.Show(s.At, origin, 2.1f, _doorWhat);
        }

        // Say hello: the greeting counts once a day by the social life's own rule, and the talk opens in the game's dialogue panel.
        private void Hello()
        {
            if (_broken || !Running || _nearest < 0 || _nearest >= _outCount || SocialPhone.DialogOpen)
            {
                return;
            }
            try
            {
                Standing s = _out[_nearest];
                SeenRecord r = _file.Of(s.Key);
                if (r == null || s.Person == null || s.Person.Lost)
                {
                    return;
                }
                int day = SeenGame.Day;
                bool knew = r.Known >= SeenRules.KnowsFrom;
                int gained = SeenRules.Greet(r, day, true);
                r.MetDay = day;
                SeenDialog.Open(new SeenTalk
                {
                    Name = s.Name, Form = r.Form, Known = knew, Car = SeenGame.CarName(r.Car), Pick = SeenRules.HelloPick(_file.Seed, SeenRules.Who(s.Key), day)
                }, s.Person.Character);
                _door.Hide("talking with them");
                Logger?.Info("Rivals seen: the player greets " + s.Name + " (" + r.Form + "): " + (gained / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                             + " gained, they know the player " + (r.Known / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " of " + SeenCatalogue.GreetWarmMost
                             + ", greeting " + r.Greets + ".");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
