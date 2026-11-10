using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait for. Each says what happened in a notice and writes one "Rivals seen check" line. Even a
    /// test writes only what is so: a sighting made by a button is of somebody where their week has them at this hour. What a test makes is marked as a test's
    /// (a sighting, a rival put beside the player), so "take back what the tests did" takes back exactly that and nothing the days made.
    /// </summary>
    public sealed partial class SeenDirector
    {
        private bool TestReady()
        {
            if (!Running || _file == null)
            {
                SeenGame.Tell(NotificationType.Warning, GoingPublic.InSession ? "reallife_seen_test_session" : "reallife_seen_test_off");
                return false;
            }
            return true;
        }

        /// <summary>Options, Tests: who is where at this hour, in a notice and in the log.</summary>
        public void TestWhere()
        {
            if (!TestReady())
            {
                return;
            }
            int day = SeenGame.Day, hour = SeenGame.Hour;
            Places(day, hour);
            var names = new System.Text.StringBuilder(160);
            for (int i = 0; i < _due.Count; i++)
            {
                SeenSubject s = _subjects[_due[i].Subject];
                string line = s.Name + ": " + SeenText.Spot(_due[i].Where.Spot);
                string place = _due[i].Place ?? string.Empty;
                Logger?.Info("Rivals seen check (who is where, day " + day + ", " + hour + ":00): " + line + (place.Length > 0 ? " (" + place + ")" : string.Empty)
                             + (_due[i].Building != null ? string.Empty : ", no building of the city to stand at") + ".");
                if (i < 3)
                {
                    names.Append(names.Length > 0 ? "; " : string.Empty).Append(line);
                }
            }
            SeenGame.Tell(NotificationType.Info, _due.Count > 0 ? "reallife_seen_test_where" : "reallife_seen_test_nobody",
                new Dictionary<string, string>(2) { { "count", _due.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) }, { "names", names.ToString() } });
            Logger?.Info("Rivals seen check (who is where): " + _due.Count + " of " + _subjects.Count + " rival(s) are somewhere at " + hour + ":00: PASS");
        }

        /// <summary>Options, Tests: the first rival who is somewhere now stands a few steps from the player, to be greeted.</summary>
        public void TestHere()
        {
            if (!TestReady())
            {
                return;
            }
            TestRefusal refusal = TestRefusals.Street();
            CityLifeDirector city = CityLifeDirector.Instance;
            PlayerController player = PlayerHelper.PlayerController;
            if (refusal != TestRefusal.None || city == null || player == null || !Settings.CityLife)
            {
                TestRefusals.Tell(Logger, "Rivals seen", refusal != TestRefusal.None ? refusal : TestRefusal.FeatureOff);
                return;
            }
            int day = SeenGame.Day, hour = SeenGame.Hour;
            Places(day, hour);
            ClearWorld("a test");
            if (_due.Count == 0)
            {
                SeenGame.Tell(NotificationType.Info, "reallife_seen_test_nobody");
                Logger?.Info("Rivals seen check (test, a rival here): nobody is anywhere at " + hour + ":00, so nobody is put out: PASS");
                return;
            }
            if (!SeenRules.MayPut(_outCount, city.People.Free))
            {
                TestRefusals.Tell(Logger, "Rivals seen", TestRefusal.PoolBusy);
                return;
            }
            Vector3 origin = player.transform.position;
            SeenSubject who = _subjects[_due[0].Subject];
            bool put = false;
            for (int tries = 0; tries < 4 && !put; tries++)
            {
                if (!CitySpots.PavementRing(origin, 3f, 7f, false, out Vector3 at))
                {
                    continue;
                }
                city.People.AtNeed = true;   // a test button cannot wait for a look to be made ready
                CityPerson person = city.People.Take(CityLook.Civilian, at, origin);
                city.People.AtNeed = false;
                if (person != null)
                {
                    _out[_outCount++] = new Standing { Person = person, Key = who.Key, Name = who.Name, Place = _due[0].Place ?? string.Empty, At = at, Spot = _due[0].Where.Spot, Test = true };
                    put = true;
                }
            }
            if (!put)
            {
                TestRefusals.Tell(Logger, "Rivals seen", TestRefusal.NothingFits, 0, "no clear pavement within 7 m, or nobody of the living city's pool could be had");
                return;
            }
            _glance = true;   // (beside the player: the panel is looked at from the next glance)
            SeenGame.Tell(NotificationType.Info, "reallife_seen_test_here", new Dictionary<string, string>(1) { { "name", who.Name } });
            Logger?.Info("Rivals seen check (test, a rival here): " + who.Name + " stands beside the player, " + _outCount + " of " + SeenCatalogue.WorldMost + " out: PASS");
        }

        /// <summary>Options, Tests: a sighting of the first rival who is somewhere now with a line for it, written and sent to Flare at once, on top of the day's posts.</summary>
        public void TestSighting()
        {
            if (!TestReady())
            {
                return;
            }
            int day = SeenGame.Day, hour = SeenGame.Hour;
            Places(day, hour);
            SeenNote n = SeenDay.TestNote(_file, _subjects, _due, day, hour, SeenGame.City.Table);
            if (n == null)
            {
                SeenGame.Tell(NotificationType.Info, "reallife_seen_test_nothing");
                Logger?.Info("Rivals seen check (test, a sighting): nobody is anywhere with a line to write at " + hour + ":00, so nothing is written: PASS");
                return;
            }
            Post(n);
            _revision++;
            string line = SeenText.Line(n);
            SeenGame.Tell(NotificationType.Info, "reallife_seen_test_sighting", new Dictionary<string, string>(1) { { "line", line } });
            Logger?.Info("Rivals seen check (test, a sighting): " + n.Name + " at " + n.Spot + " as their week has them, form " + n.Form + " from their figures: \"" + line + "\": PASS");
        }

        /// <summary>
        /// Options, Tests: what the tests did is taken back: the sightings a test wrote (off the pages, out of the paper) and a rival a test put beside the player. The
        /// days' marks, cars, sightings and greetings stay as the days made them; a post already on Flare stays there.
        /// </summary>
        public void TestBack()
        {
            if (!Running || _file == null)
            {
                SeenGame.Tell(NotificationType.Info, "reallife_seen_test_back_none");
                return;
            }
            int notes = SeenDay.TakeBack(_file), people = 0;
            CityLifeDirector city = CityLifeDirector.Instance;
            for (int i = _outCount - 1; i >= 0; i--)
            {
                if (_out[i].Test)
                {
                    Leave(city, i, "the tests are taken back");
                    people++;
                }
            }
            if (notes == 0 && people == 0)
            {
                SeenGame.Tell(NotificationType.Info, "reallife_seen_test_back_none");
                Logger?.Info("Rivals seen check (test, taken back): nothing of the tests to take back: PASS");
                return;
            }
            _revision++;
            SeenGame.Tell(NotificationType.Info, "reallife_seen_test_back");
            Logger?.Info("Rivals seen check (test, taken back): " + notes + " sighting(s) and " + people + " rival(s) of the tests taken back, the rest as the days made it: PASS");
        }
    }
}
