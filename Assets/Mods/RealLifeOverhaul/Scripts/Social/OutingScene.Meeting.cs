using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The outing's part for getting there: whoever waits is put out or walks in, and the two say hello.</summary>
    public sealed partial class OutingScene
    {
        private enum Hello { Closing, Turning, Playing }

        private const float Appear = 130f;           // metres from the place at which whoever waits there is put out
        private const float WalkMost = 45f;          // metres of route somebody walking in is given at most...
        private const float WalkSeconds = 40f;       // ...and seconds: then they are put nearer, or met where they are
        private const float AtSpot = 2.5f;           // metres from where they wait that counts as being there
        private const float HoldNudge = 40f;         // seconds the player is indoors after hello before they ask where the player is
        private const float HoldMost = 100f;         // ...and before they give up and go
        private const float WaveWithin = 11f;        // metres
        private const float HelloWithin = 3.4f;      // metres
        private const float GreetGap = 0.80f;        // metres apart, facing, that every greeting clip is made for (CityAssets)
        private const float GreetSlack = 0.10f;      // ...and how far off that a whole-body one is still played

        private bool _early;                         // the player was there first
        private bool _walkingIn;
        private bool _waved;
        private int _placeTries;
        private int _walkTries;
        private float _walkUntil;
        private bool _returning;                     // walking back to the pavement after the car pulled away
        private bool _nudged;                        // "where are you?" went out
        private bool _toldThere;                     // told, while indoors, that they are waiting outside
        private string _greetMine, _greetTheirs;     // the player's half and theirs; null: a wave
        private bool _greetWhole;                    // whole-body clips (a real hug, a kiss on the cheek), not the upper body only
        private Hello _hello;
        private bool _spotSaid;                      // the self-check on the place to wait has been written

        private void Travel(float now)
        {
            float nowMinutes = SocialLife.Now;
            if (PlanGone(now, nowMinutes))
            {
                return;
            }
            Nudge(nowMinutes);
            Vector3 player = D.PlayerPosition;
            if (_friend == null)
            {
                if (JobSpots.Flat(player, _spot) > Appear || now < _nextTry)
                {
                    return;
                }
                if (nowMinutes < _arrives)
                {
                    ShowEarly();
                    return;
                }
                PutOut(now, player, false);
                return;
            }

            float gap = JobSpots.Flat(_friend.Position, player);
            if (_returning)
            {
                // No boarding until they are back on the pavement and standing.
                if ((_friend.Arrived && now - _stepFrom > 1f) || now > _walkUntil)
                {
                    _returning = false;
                    _friend.Stand();
                    WaitingPose();
                }
                else
                {
                    return;
                }
            }
            if (!_greeted)
            {
                MarkPerson(_friend, _first);   // on them, wherever they are, until hello is said
            }
            if (_walkingIn)
            {
                if (gap < WaveWithin)
                {
                    Stood(player);   // the player has come to meet them: that is where they meet
                }
                else if (WalkingIn(now, player))
                {
                    return;
                }
                gap = JobSpots.Flat(_friend.Position, player);
            }
            if (!_waved && gap < WaveWithin)
            {
                _waved = true;
                _friend.Idle(0.3f);
                _friend.Face(player);
                Gesture(_friend, D.Props, "wave_hello");
            }
            if (Rides)
            {
                Prompt = null;
                TryBoard(now, gap);
                return;
            }
            if (!D.OnFoot)
            {
                Prompt = null;
                if (gap < 40f && !_onFootSaid)
                {
                    _onFootSaid = true;
                    Line = SocialLife.T("reallife_social_obj_park", SocialLife.Data(_who));
                }
                return;
            }
            if (gap < HelloWithin)
            {
                if (Prompt == null)
                {
                    Offer(SocialLife.Say("reallife_social_hello_prompt", SocialLife.Data(_who)), "reallife_social_button_hello", _friend);
                    if (!_test && !(_early && _arrives - _time > 8f))
                    {
                        SocialLinks.GuessGreeting?.Invoke(_who, _kind, _venue.Name, Mathf.RoundToInt(_lateBy));
                    }
                }
            }
            else
            {
                Prompt = null;
            }
        }

        // Called off on the phone, or the player never came.
        private bool PlanGone(float now, float nowMinutes)
        {
            if (!_test && !_greeted && !SocialState.Plan.Active)
            {
                Abandon(now);
                return true;
            }
            if (nowMinutes > _time + SocialRules.StoodUpMinutes)
            {
                if (_test)
                {
                    SocialLife.Toast(NotificationType.Warning, "reallife_social_test_expired", null, 6f);
                }
                else
                {
                    SocialLife.StoodUp();
                }
                Abandon(now);
                return true;
            }
            return false;
        }

        private void ShowEarly()
        {
            if (!_early)
            {
                _early = true;
                Line = SocialLife.T("reallife_social_obj_early", SocialLife.Data(_who, "time", SocialLife.Clock(_arrives)));
            }
        }

        // The one warning before being stood up: sent whatever the day's cap on texts.
        private void Nudge(float nowMinutes)
        {
            if (_nudged || _test || _greeted || nowMinutes < Mathf.Max(_time + SocialRules.LateMinutes, _arrives + 10f))
            {
                return;
            }
            _nudged = true;
            SocialLife.SendText(_who, "reallife_social_text_where", SocialLife.Data(_who, "place", _venue.Name), true);
            SocialDirector.Logger?.Info("Social: " + _who.Name + " asked where the player is (" + (nowMinutes - _time).ToString("F0") + " min after the time agreed).");
        }

        // Before hello: whoever was waiting walks off rather than vanish where they stood.
        private void Abandon(float now)
        {
            Prompt = null;
            Line = null;
            Title = null;
            Unmark();
            _settled = true;
            _walkingIn = false;
            _returning = false;
            if (_friend != null)
            {
                _friend.Idle(0.3f);
                Depart(_friend, now);
                _friend = null;
                Go(Step.Parting, now, 0f);
            }
            else
            {
                Over = true;
            }
        }

        // If the player would see them appear, they walk in from out of view; otherwise they are there by the door.
        private void PutOut(float now, Vector3 player, bool unseen)
        {
            _nextTry = now + 1.5f;
            _placeTries++;
            // Clear of the places other panels are offered at (a subway entrance, a stop): the hello's panel then has the game's box alone.
            bool clear = SocialPlaces.StandingClear(_venue.Front, out _spot, out float room, out int busy);
            if (clear)
            {
                SaySpot(room, busy);
            }
            if (!clear && (_placeTries < 3 || !CitySpots.Pavement(_venue.Front, 6f, out _spot)))
            {
                // No room outside just now (a van on the pavement): from the third try any pavement within 6 m will do.
                _spot = _venue.Front;
                if (_placeTries >= 8)
                {
                    NoPlace("no pavement to stand on within 6 m of the door");
                }
                return;
            }
            bool watched = !unseen && (_early || (CitySpots.InView(_spot) && JobSpots.Flat(player, _spot) < 60f));
            if (watched && _placeTries <= 4)
            {
                if (!SocialPlaces.WalkIn(_spot, player, WalkMost, out Vector3 from, out _))
                {
                    return;   // no short way in from out of view this time: asked again in a moment
                }
                _friend = D.Cast.Put(_who, from, _spot);
                // Their own agent has the last word on the route.
                if (_friend != null && SocialCast.Walk(_friend, _spot, out float length, out bool whole) && whole && length <= WalkMost * 1.3f && _friend.WalkTo(_spot))
                {
                    _walkingIn = true;
                    _walkTries = 0;
                    _stepFrom = now;
                    _walkUntil = now + WalkSeconds;
                    MarkPerson(_friend, _first);
                    Line = SocialLife.T("reallife_social_obj_coming", SocialLife.Data(_who));
                    LogPut(_who, _friend, _spot, "is walking in to " + _venue.Name + " (try " + _placeTries + "): put out of view");
                    return;
                }
                D.Cast.Release(_friend);
                _friend = null;
                return;
            }
            if (watched && _placeTries <= 8 && CitySpots.InView(_spot) && JobSpots.Flat(player, _spot) < 60f)
            {
                return;   // they appear by the door once the player looks away, or after four more tries
            }
            Vector3 outward = _spot + (_spot - _venue.Door);
            _friend = D.Cast.Put(_who, _spot, unseen && JobSpots.Flat(outward, _spot) > 0.3f ? outward : player);
            if (_friend == null)
            {
                if (_placeTries >= 12)
                {
                    NoPlace(D.Cast.HasRoom ? "the place found would not take them" : "no character free");
                }
                return;
            }
            _walkingIn = false;
            WaitingPose();
            MarkPerson(_friend, _first);
            Line = SocialLife.T(Rides ? "reallife_social_obj_meet_car" : "reallife_social_obj_there", SocialLife.Data(_who, "place", _venue.Name));
            LogPut(_who, _friend, _spot, "is waiting outside " + _venue.Name + (unseen ? ": put there while the player is indoors"
                : watched ? ": no short way in from out of view in " + (_placeTries - 1) + " tries, so put at the place" : ": put at the place, nobody looking that way"));
        }

        // Once an outing: how clear of other panels' places the place to wait is.
        private void SaySpot(float room, int busy)
        {
            if (_spotSaid || !Meta.Dev.Verbose)
            {
                return;
            }
            _spotSaid = true;
            SocialDirector.Logger?.Info("Social check (meeting place): outside " + _venue.Name + ", " + busy + " place(s) of other panels within 16 m; "
                                        + (busy == 0 ? "none to keep clear of: PASS."
                                            : "the place to wait is " + room.ToString("F1", CultureInfo.InvariantCulture) + " m outside the nearest one's reach (wanted "
                                              + GameUi.PanelPickRules.StandClear.ToString("F1", CultureInfo.InvariantCulture) + "): "
                                              + (room >= GameUi.PanelPickRules.StandClear ? "PASS."
                                                  : "FAIL (no clear pavement near the door; the roomiest place was taken, and the person met still has the box first).")));
        }

        private void NoPlace(string why)
        {
            SocialDirector.Logger?.Info("Social: " + _who.Name + " could not be put outside " + _venue.Name + " after " + _placeTries + " tries (" + why + "); the plan is off.");
            Say(_who, "reallife_social_plan_lost", null, NotificationType.Warning);
            ClearPlan();
            Over = true;
        }

        // True while they are still walking in. A walk that ends short or takes too long is put right out of sight, or
        // they stop where they are and are met there.
        private bool WalkingIn(float now, Vector3 player)
        {
            float left = JobSpots.Flat(_friend.Position, _spot);
            bool ended = now - _stepFrom > 1f && _friend.Arrived;
            if (ended && left <= AtSpot)
            {
                Stood(player);
                return false;
            }
            if (!ended && now < _walkUntil)
            {
                return true;
            }
            string what = ended ? "their route ended " + left.ToString("F0") + " m short" : "still " + left.ToString("F0") + " m away after " + (now - _stepFrom).ToString("F0") + " s";
            bool looking = _friend.Character.visible && CitySpots.InView(_friend.Position);
            if (!looking)
            {
                if (!(CitySpots.InView(_spot) && JobSpots.Flat(player, _spot) < 60f) && D.Cast.Move(_friend, _spot, player))
                {
                    SocialDirector.Logger?.Info("Social: " + _who.Name + " (" + what + ") was put at the place: nobody was looking at them or at it.");
                    Stood(player);
                    return false;
                }
                if (_walkTries < 2 && SocialPlaces.WalkIn(_spot, player, 26f, out Vector3 nearer, out float length) && D.Cast.Move(_friend, nearer, _spot) && _friend.WalkTo(_spot))
                {
                    _walkTries++;
                    _stepFrom = now;
                    _walkUntil = now + 25f;
                    SocialDirector.Logger?.Info("Social: " + _who.Name + " (" + what + ") was put nearer, out of view: " + length.ToString("F0") + " m of route to walk.");
                    return true;
                }
            }
            SocialDirector.Logger?.Info("Social: " + _who.Name + " (" + what + ") stops where they are, " + JobSpots.Flat(_friend.Position, player).ToString("F0")
                                        + " m from the player: the marker is on them and that is where they are met.");
            Stood(player);
            return false;
        }

        private void Stood(Vector3 player)
        {
            _walkingIn = false;
            _friend.Stand();
            _friend.Face(player);
            WaitingPose();
            MarkPerson(_friend, _first);
            bool atPlace = JobSpots.Flat(_friend.Position, _spot) <= AtSpot * 2f;
            Line = SocialLife.T(Rides ? "reallife_social_obj_meet_car" : atPlace ? "reallife_social_obj_there" : "reallife_social_obj_over_there",
                SocialLife.Data(_who, "place", _venue.Name));
        }

        private void WaitingPose()
        {
            string clip = Random.value < 0.5f ? "arms_crossed" : "queue_wait";
            if (ClipLibrary.Has(clip))
            {
                _friend.Work(clip, D.Props, 0.4f);
            }
        }

        // Before hello, with the player indoors: nobody sees the pavement, so whoever is due is simply put by the door.
        private void WhileAway(float now, float nowMinutes)
        {
            Vector3 outward = JobSpots.Flat(_venue.Door, _spot) > 0.3f ? _spot + (_spot - _venue.Door) : _spot + Vector3.forward;
            if (_friend == null)
            {
                if (now < _nextTry)
                {
                    return;
                }
                _nextTry = now + 1.5f;
                Vector3 street = SocialDirector.Where();   // the door of the building the player is in
                if (JobSpots.Flat(street, _spot) > Appear)
                {
                    return;
                }
                if (nowMinutes < _arrives)
                {
                    ShowEarly();
                    return;
                }
                PutOut(now, street, true);
            }
            else if (_walkingIn)
            {
                bool there = now - _stepFrom > 1f && _friend.Arrived && JobSpots.Flat(_friend.Position, _spot) <= AtSpot;
                if (!there && now < _walkUntil && !_friend.Arrived)
                {
                    return;
                }
                if (!there)
                {
                    D.Cast.Move(_friend, _spot, outward);
                }
                Stood(outward);
            }
            if (_friend != null && !_walkingIn && !_toldThere)
            {
                _toldThere = true;
                Line = SocialLife.T(Rides ? "reallife_social_obj_meet_car" : "reallife_social_obj_there", SocialLife.Data(_who, "place", _venue.Name));
                SocialLife.Toast(NotificationType.Info, "reallife_social_waiting_outside", SocialLife.Data(_who, "place", _venue.Name), 9f);
            }
        }

        private void Greet(float now)
        {
            Prompt = null;
            Line = null;
            Unmark();
            _walkingIn = false;
            Arrived();
            Vector3 player = D.PlayerPosition;
            _friend.Idle(0.25f);
            _friend.Stand();
            _friend.Face(player);
            D.Body.Hold();
            D.Body.Turn(D, _friend.Position);

            // The clips are made for two facing GreetGap apart: they come the last steps up, both turn, then it plays.
            ChooseGreeting();
            float gap = JobSpots.Flat(_friend.Position, player);
            if (_greetMine != null && gap > GreetGap + GreetSlack && _friend.StepTo(player, GreetGap))
            {
                _hello = Hello.Closing;
                Go(Step.Greeting, now, 4.5f);
                return;
            }
            _hello = Hello.Turning;
            Go(Step.Greeting, now, 0.3f);
        }

        // A kiss on the cheek for a partner, a hug for close friends and family, else a handshake; null: a wave.
        private void ChooseGreeting()
        {
            bool close = _who.Stage >= SocialStage.CloseFriend || _who.IsFamily;
            _greetWhole = true;
            if (_who.Together && ClipLibrary.Has("social_cheek_kiss"))
            {
                _greetMine = _greetTheirs = "social_cheek_kiss";
            }
            else if (close && ClipLibrary.Has("social_hug_a") && ClipLibrary.Has("social_hug_b"))
            {
                _greetMine = "social_hug_a";      // the one who pats
                _greetTheirs = "social_hug_b";    // the one who is patted
            }
            else
            {
                _greetWhole = false;
                string upper = close ? "social_hug" : "social_handshake";
                _greetMine = _greetTheirs = ClipLibrary.Has(upper) ? upper : null;
            }
        }

        private void Greeting(float now)
        {
            Vector3 player = D.PlayerPosition;
            switch (_hello)
            {
                case Hello.Closing:
                    // Stopped, not just "arrived": an agent says so a quarter of a metre early.
                    if ((now - _stepFrom > 0.4f && _friend.Arrived && _friend.Speed < 0.05f) || now >= _stepEnd)
                    {
                        _friend.Stand();
                        _friend.Face(player);
                        D.Body.Turn(D, _friend.Position);
                        _hello = Hello.Turning;
                        Go(Step.Greeting, now, 0.35f);
                    }
                    break;

                case Hello.Turning:
                    if (now >= _stepEnd)
                    {
                        _hello = Hello.Playing;
                        Go(Step.Greeting, now, PlayGreeting(JobSpots.Flat(_friend.Position, player)) + 0.1f);
                    }
                    break;

                default:
                    if (now >= _stepEnd)
                    {
                        D.Body.Rest(0.2f);
                        OpenTalk(now);
                    }
                    break;
            }
        }

        // Only at the distance the clip was made for: nearer they pass through each other. Returns its length in seconds.
        private float PlayGreeting(float gap)
        {
            string mine = _greetMine, theirs = _greetTheirs;
            bool whole = _greetWhole;
            if (mine != null && whole && Mathf.Abs(gap - GreetGap) > GreetSlack)
            {
                whole = false;
                mine = theirs = ClipLibrary.Has("social_hug") ? "social_hug" : null;
            }
            if (mine != null && !whole && (gap <= 0.65f || gap >= 1.0f))
            {
                mine = theirs = null;
            }
            if (mine != null && whole)
            {
                if (_friend.Work(theirs, D.Props, 0.2f, ClipEnd.Return, false))
                {
                    if (D.Body.Clip(mine, 0.2f))
                    {
                        return Length(mine, 2.6f);
                    }
                    _friend.Idle(0.2f);
                }
                mine = theirs = null;
            }
            if (mine == null)
            {
                mine = theirs = "wave_hello";
            }
            Gesture(_friend, D.Props, theirs);
            D.Body.Over(mine, BodyMask.Upper, 0.2f);
            return Length(mine, 1.8f);
        }

        // What being on time is worth; once, whichever way they come together.
        private void Arrived()
        {
            if (_greeted)
            {
                return;
            }
            _greeted = true;
            _lateBy = SocialLife.Now - _time;
            ClearPlan();
            SocialLife.Contacted(_who);
            if (_test)
            {
                return;
            }
            if (_lateBy > SocialRules.LateMinutes && !_who.Has(SocialTrait.Late))
            {
                float weight = _who.Has(SocialTrait.Punctual) ? 2f : 1f;
                SocialLife.Change(_who, 0f, SocialRules.LateAffection * weight, SocialRules.LateTrust * weight);
                SocialLife.Remember(_who, MemoryCode.Late);
            }
            else if (_lateBy <= SocialRules.LateMinutes)
            {
                SocialLife.Change(_who, 0f, 0f, SocialRules.OnTimeTrust);
            }
        }

        private string GreetingLine()
        {
            if (_lateBy > SocialRules.LateMinutes)
            {
                return SocialLines.Pick(_who.Has(SocialTrait.Late) ? SocialCatalogue.GreetLateEasy : SocialCatalogue.GreetLate);
            }
            if (_early && _arrives - _time > 8f)
            {
                return SocialLines.Pick(SocialCatalogue.TheyAreLate);
            }
            return SocialLines.Pick(_who.Together ? SocialCatalogue.GreetLove
                : _who.Stage >= SocialStage.CloseFriend || _who.IsFamily ? SocialCatalogue.GreetClose
                : _who.Stage == SocialStage.Friend ? SocialCatalogue.GreetFriend
                : SocialCatalogue.GreetAcquaintance);
        }

        private void OpenTalk(float now)
        {
            bool pays = _def.Cost > 0;
            string guessed = SocialLinks.TakeGreeting != null ? SocialLinks.TakeGreeting(_who) : null;   // RLO Texts' greeting; null: the written one
            var talk = new SceneTalk
            {
                Person = _who, Mode = SceneTalk.Outing, Opener = guessed != null ? SocialLinks.WordsKey : GreetingLine(),
                OpenerData = guessed != null ? new Dictionary<string, string>(1) { { "text", guessed } } : SocialLife.Data(_who, "place", _venue.Name),
                Cost = pays ? _def.Cost : 0,
                TheyOffer = pays && (_theirIdea || _who.Has(SocialTrait.Generous)) && !_who.Has(SocialTrait.Careful),
                GoLabel = _kind == PlanKind.Walk ? "reallife_social_btn_lets_walk" : _kind == PlanKind.Moving ? "reallife_social_btn_lets_start" : "reallife_social_btn_go_in"
            };
            Converse(talk, _friend, now);
            Go(Step.Talk, now, 0f);
        }
    }
}
