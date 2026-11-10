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
    /// The director's part for the world: a rival whose week has them at a place near the player stands there, on the pavement a few steps along from its door, as
    /// one of the living city's own people exactly as that pool dresses them (no character of their own is made: CityLife/CityPeople), with their name on the
    /// game's own panel and a greeting on its button; their car passes on the lane outside as one of the traffic pool's own (SeenGame.PassBy). Who is where, and
    /// the building each is at, is worked out when the hour turns (SeenTable.Places); every <see cref="SeenCatalogue.LookSeconds"/> seconds one of them at most is
    /// tried (SeenRules.PutPick), only out of view, never past <see cref="SeenCatalogue.WorldMost"/> at once and never taking the living city's last people, and a
    /// placing that fails is not tried again that hour. They go when their hour there is over (out of view), when the player leaves them far behind, indoors, on
    /// a time skip, with the option, or in a Going Public session, but never while the player is talking with them (SeenRules.Leaves). Nothing a frame.
    /// </summary>
    public sealed partial class SeenDirector
    {
        private static readonly int HotGlance = HotSpots.Slot("Rivals seen: the glance at the greeting");

        private struct Standing
        {
            public CityPerson Person;
            public string Key, Name, Place;
            public Vector3 At;
            public SeenSpot Spot;
            public bool Test;    // put out by a test button: it stays until left behind, whatever the hour
        }

        private readonly Standing[] _out = new Standing[SeenCatalogue.WorldMost];
        private readonly string[] _outKeys = new string[SeenCatalogue.WorldMost];
        private readonly List<SeenDue> _due = new List<SeenDue>(32);
        private int _outCount;
        private int _dueHour = -1, _dueDay = -1;
        private float _nextLook;
        private bool _glance;           // a rival stands within GlanceMetres of the player: the greeting's panel is glanced at between looks
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
            Tend(city, origin);
            Panel(origin);
            _glance = _outCount > 0 && SeenRules.Glance(Nearest(origin)) < 10;
            if (now < _nextLook)
            {
                return;
            }
            _nextLook = now + SeenCatalogue.LookSeconds;
            if (!TrafficAccidents.IsActive && !StreetCrime.IsActive)
            {
                PutOne(city, origin);
            }
        }

        // Between two looks while a rival stands near the player: the greeting's panel only, so somebody running past cannot cross its reach between two looks.
        private void Glance()
        {
            using var hotSpot = HotSpots.Time(HotGlance);
            if (!Running || _outCount == 0)
            {
                _glance = false;
                return;
            }
            PlayerController player = PlayerHelper.PlayerController;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (player == null || uis == null || LoadScene.isLoading || BuildingManager.IsInsideBuilding || uis.timeMachine.isRunning || uis.gameSpeed.Paused)
            {
                return;   // (the next look clears the street, or waits for the game to go on)
            }
            Vector3 origin = player.transform.position;
            Panel(origin);
            _glance = SeenRules.Glance(Nearest(origin)) < 10;
        }

        // The nearest of those standing, metres squared on the ground; the most a float holds with nobody out.
        private float Nearest(Vector3 origin)
        {
            float best = float.MaxValue;
            for (int i = 0; i < _outCount; i++)
            {
                best = Mathf.Min(best, CitySpots.Flat(_out[i].At - origin));
            }
            return best;
        }

        // The hour has turned: who is somewhere now and the building each is at, with no placing failed yet. A pass over the day's rivals and the district's eating places.
        private void Places(int day, int hour)
        {
            _dueHour = hour;
            _dueDay = day;
            SeenGame.City.Table.Places(_file, _subjects, day, hour, _due);
        }

        // Those standing: gone with whoever took them (the pool's own clearing), left far behind, or their hour at the place over and nobody looking; never while the
        // player is talking with them.
        private void Tend(CityLifeDirector city, Vector3 origin)
        {
            for (int i = _outCount - 1; i >= 0; i--)
            {
                Standing s = _out[i];
                bool lost = s.Person == null || s.Person.Lost;
                bool talking = !lost && SeenGame.TalkingWith(s.Person.Character);
                bool still = s.Test || SeenRules.Still(_subjects, _due, s.Key, s.Spot);
                bool inView = !lost && !talking && !still && CitySpots.InView(s.At);   // (only asked where it decides)
                SeenLeave leave = SeenRules.Leaves(lost, talking, CitySpots.Flat(s.At - origin), s.Test, still, inView);
                if (leave != SeenLeave.Stays)
                {
                    Leave(city, i, leave == SeenLeave.Taken ? "taken by the living city" : leave == SeenLeave.Behind ? "left behind" : "their hour there is over");
                }
            }
        }

        // At most one placing a look: the first rival due whose place is near, who does not stand already and whose placing has not failed this hour. A placing that
        // finds no spot by the door, or a meal at a place shut for a reason no timetable knew (a fire, an inspection), is not tried again until the hour turns.
        private void PutOne(CityLifeDirector city, Vector3 origin)
        {
            for (int i = 0; i < _outCount; i++)
            {
                _outKeys[i] = _out[i].Key;
            }
            int pick = SeenRules.PutPick(_subjects, _due, _outKeys, _outCount, city.People.Free, origin.x, origin.z);
            if (pick < 0)
            {
                return;
            }
            SeenDue d = _due[pick];
            bool meal = d.Where.Spot != SeenSpot.Office && d.Where.Spot != SeenSpot.Late;
            int put = meal && !SeenGame.OpenNow(d.Building) ? 0 : Put(city, d, origin);
            if (put == 0)
            {
                SeenRules.Fail(_due, pick);
            }
        }

        // On the pavement a few steps along from the door, facing the street; then their car on the lane outside. 1 put out, 0 no spot there (not tried again this
        // hour), -1 nobody of the living city's pool dressed and free this look (the next look may try again).
        private int Put(CityLifeDirector city, SeenDue d, Vector3 origin)
        {
            if (!SeenGame.DoorStand(d.Building, false, out Vector3 at, out Vector3 front, out Vector3 face))
            {
                return 0;
            }
            CityPerson person = city.People.Take(CityLook.Civilian, at, at + face);
            if (person == null)
            {
                return -1;
            }
            SeenSubject who = _subjects[d.Subject];
            _out[_outCount++] = new Standing { Person = person, Key = who.Key, Name = who.Name, Place = d.Place ?? string.Empty, At = at, Spot = d.Where.Spot };
            SeenRecord r = _file.Of(who.Key);
            bool car = r != null && r.Car.Length > 0 && SeenGame.PassBy(r.Car, front, origin);
            Logger?.Info("Rivals seen: " + who.Name + " stands at " + (d.Place ?? "?") + " (" + d.Where.Spot + "), "
                         + Mathf.Sqrt(CitySpots.Flat(at - origin)).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " m away, "
                         + Mathf.Sqrt(CitySpots.Flat(at - front)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " m along from the door; " + _outCount
                         + " of " + SeenCatalogue.WorldMost + " out; their car "
                         + (car ? "(" + r.Car + ") is on the lane outside." : "is not on the road (no idle car of the model in the traffic pool, or no lane there)."));
            if (Verbose)
            {
                Logger?.Info("Rivals seen check (in the world): " + _outCount + " out of " + SeenCatalogue.WorldMost + " at most, the living city keeps " + city.People.Free
                             + " of its people free: " + (_outCount <= SeenCatalogue.WorldMost ? "PASS" : "PAST THE CAP"));
            }
            return 1;
        }

        // Somebody goes: a talk of ours with them ends first, so the game's dialogue never holds somebody let go, then the living city has them back.
        private void Leave(CityLifeDirector city, int index, string why)
        {
            Standing s = _out[index];
            try
            {
                if (s.Person != null)
                {
                    SeenGame.EndTalk(s.Person.Character);
                }
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
            _glance = false;
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

        // Say hello: the greeting counts once a day by the social life's own rule (SeenRules.Hello), and the talk opens in the game's dialogue panel.
        private void Hello()
        {
            if (_broken || !Running || _nearest < 0 || _nearest >= _outCount || SocialPhone.DialogOpen)
            {
                return;
            }
            try
            {
                Standing s = _out[_nearest];
                if (s.Person == null || s.Person.Lost)
                {
                    return;
                }
                SeenTalk talk = SeenRules.Hello(_file, s.Key, s.Name, SeenGame.Day);
                if (talk == null)
                {
                    return;
                }
                SeenDialog.Open(talk, s.Person.Character);
                _door.Hide("talking with them");
                _revision++;
                SeenRecord r = _file.Of(s.Key);
                Logger?.Info("Rivals seen: the player greets " + s.Name + " (" + talk.Form + "): " + (talk.Gained / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
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
