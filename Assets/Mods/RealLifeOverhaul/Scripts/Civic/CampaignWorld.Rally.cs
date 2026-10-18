using System;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>A rally: when one gathers, its stage out of view, the speech, and the player's own rally.</summary>
    public sealed partial class CampaignWorld
    {
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
                CivicDirector.Logger?.Info("Civic: a rally for candidate " + candidate + " with " + _crew.Count + " people, "
                    + Mathf.RoundToInt(Vector3.Distance(player, spot)) + " m from the player.");
                return true;
            }
            return false;
        }
    }
}
