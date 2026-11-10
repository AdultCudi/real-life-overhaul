using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait for. Each says what happened in a notice and writes one "Rivals seen check" line. Even a
    /// test writes only what is so: a sighting made by a button is of somebody where their week has them at this hour.
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
            if (_beforeTests == null)
            {
                _beforeTests = SeenStore.Write(_file);   // what "put the rivals seen back as before" restores
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
                string line = s.Name + ": " + SeenText.Spot(_due[i].Where.Spot) + (_venueNames[i].Length > 0 ? " (" + _venueNames[i] + ")" : string.Empty);
                Logger?.Info("Rivals seen check (who is where, day " + day + ", " + hour + ":00): " + line + (_venues[i] != null ? string.Empty : ", no building of the city to stand at") + ".");
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
                    _out[_outCount++] = new Standing { Person = person, Key = who.Key, Name = who.Name, Place = _venueNames[0] ?? string.Empty, At = at, Spot = _due[0].Where.Spot, Test = true };
                    put = true;
                }
            }
            if (!put)
            {
                TestRefusals.Tell(Logger, "Rivals seen", TestRefusal.NothingFits, 0, "no clear pavement within 7 m, or nobody of the living city's pool could be had");
                return;
            }
            SeenGame.Tell(NotificationType.Info, "reallife_seen_test_here", new Dictionary<string, string>(1) { { "name", who.Name } });
            Logger?.Info("Rivals seen check (test, a rival here): " + who.Name + " stands beside the player, " + _outCount + " of " + SeenCatalogue.WorldMost + " out: PASS");
        }

        /// <summary>Options, Tests: a sighting of the first rival who is somewhere now with a line for it, written and sent to Flare at once.</summary>
        public void TestSighting()
        {
            if (!TestReady())
            {
                return;
            }
            int day = SeenGame.Day, hour = SeenGame.Hour;
            Places(day, hour);
            for (int i = 0; i < _due.Count; i++)
            {
                SeenSubject s = _subjects[_due[i].Subject];
                SeenRecord r = _file.Of(s.Key);
                SeenSpot spot = _due[i].Where.Spot;
                bool meal = spot == SeenSpot.Lunch || spot == SeenSpot.Dinner;
                if (r == null || !SeenDay.Writes(spot, r.Form) || (meal && _venueNames[i].Length == 0) || ((spot == SeenSpot.Office || spot == SeenSpot.Late) && s.Firm.Length == 0))
                {
                    continue;
                }
                var n = new SeenNote
                {
                    Day = day, Hour = hour, Kind = SeenNoteKind.Place, Spot = spot, Form = r.Form, Key = s.Key, Name = s.Name, Firm = s.Firm, Place = meal ? _venueNames[i] : string.Empty,
                    District = meal ? SeenDay.DistrictOf(_file, s, SeenGame.City) : string.Empty, Car = r.Car, Posted = true
                };
                _file.Notes.Add(n);
                while (_file.Notes.Count > SeenCatalogue.NotesKept)
                {
                    _file.Notes.RemoveAt(0);
                }
                Post(n);
                string line = SeenText.Line(n);
                SeenGame.Tell(NotificationType.Info, "reallife_seen_test_sighting", new Dictionary<string, string>(1) { { "line", line } });
                Logger?.Info("Rivals seen check (test, a sighting): " + s.Name + " at " + spot + " as their week has them, form " + r.Form + " from their figures: \"" + line + "\": PASS");
                return;
            }
            SeenGame.Tell(NotificationType.Info, "reallife_seen_test_nothing");
            Logger?.Info("Rivals seen check (test, a sighting): nobody is anywhere with a line to write at " + hour + ":00, so nothing is written: PASS");
        }

        /// <summary>Options, Tests: the line as it was before the first test button, and nobody of a test left standing.</summary>
        public void TestBack()
        {
            if (!Running || _beforeTests == null)
            {
                SeenGame.Tell(NotificationType.Info, "reallife_seen_test_back_none");
                return;
            }
            SeenFile before = SeenStore.Read(_beforeTests, out int _);
            ClearWorld("the tests are put back");
            if (before != null)
            {
                _file = before;
            }
            _beforeTests = null;
            _subjectsDay = -1;
            SeenGame.Tell(NotificationType.Info, "reallife_seen_test_back");
            Logger?.Info("Rivals seen check (test, put back): the save's line is as before the tests: " + (before != null ? "PASS" : "FAIL (the copy could not be read)"));
        }
    }
}
