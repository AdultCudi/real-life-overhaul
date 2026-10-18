using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The outing's part for a walk: side by side for 160 m, with a rest on one of the game's street benches.</summary>
    public sealed partial class OutingScene
    {
        private const float WalkGoal = 160f;         // metres walked together
        private const float BenchReach = 4.5f;       // metres from a street bench at which sitting down on it is offered
        private const float BenchRest = 40f;         // metres of the walk a rest on a bench counts as
        private const float BenchMost = 90f;         // seconds they sit before the companion gets up of their own accord

        private readonly PlayerSeat _seat = new PlayerSeat();
        private OutsideBenchController _bench;       // the bench offered, or sat on
        private bool _benchKept;                     // the bench's own "seat a pedestrian here", put back when they get up
        private bool _benchUsed;                     // one rest a walk
        private bool _benchSaid;
        private float _nextBench;
        private float _nextFollow;
        private float _side = 1f;                    // which side of the player they walk on: 1 right, -1 left

        private void StartWalk(float now)
        {
            _metres = 0f;
            _shownTens = -1;
            _lastPlayer = D.PlayerPosition;
            _apartSince = -1f;
            _beats = 0;
            _friend.Idle(0.3f);
            ShowWalk();
            Go(Step.Walking, now, 420f);
        }

        private void ShowWalk()
        {
            int tens = Mathf.FloorToInt(_metres / 10f);
            if (tens != _shownTens)
            {
                _shownTens = tens;
                Dictionary<string, string> data = SocialLife.Data(_who, "metres", (tens * 10).ToString());
                data["goal"] = ((int)WalkGoal).ToString();
                Line = SocialLife.T("reallife_social_obj_walk", data);
            }
        }

        private void LookForBench(float now, Vector3 player, float gap)
        {
            if (_benchUsed || now < _nextBench)
            {
                return;
            }
            _nextBench = now + 1.5f;
            OutsideBenchController bench = D.OnFoot && gap < 6f && !_seat.Seated ? Seating.Bench(player, BenchReach) : null;
            if (bench == _bench)
            {
                return;
            }
            _bench = bench;
            if (bench != null)
            {
                Offer(SocialLife.Say("reallife_social_bench_prompt", SocialLife.Data(_who)), "reallife_social_button_sit", null);
                Seating.Check(SocialDirector.Logger, "a street bench is " + Mathf.Sqrt((bench.sittingPosition.position - player).sqrMagnitude).ToString("F1", CultureInfo.InvariantCulture)
                                                     + " m from the player on a walk: sitting down is offered", true);
            }
            else
            {
                Prompt = null;
            }
        }

        // The companion takes the bench's second place if the game has nobody there, else stands by.
        private void SitOnBench(float now)
        {
            OutsideBenchController bench = _bench;
            Prompt = null;
            if (bench == null || !bench.isActiveAndEnabled || _friend == null || _friend.Lost)
            {
                _bench = null;
                return;
            }
            _benchUsed = true;
            if (_talkUntil > 0f)
            {
                _talkUntil = -1f;
                _friend.StopOver(0.2f);
            }
            if (!_seat.Sit(PlayerHelper.PlayerController, bench.sittingPosition))
            {
                Seating.Check(SocialDirector.Logger, "the player could not be seated on the bench (in a car, hands full, or not on the ground)", false);
                _bench = null;
                return;
            }
            _benchKept = bench.spawnPedestrians;
            bench.spawnPedestrians = false;   // the game seats nobody on top of the companion while they are here
            bool friendSits = Seating.SecondPlaceFree(bench) && Seating.InReach(_friend.Position, bench.aiSittingPosition) && _friend.Sit(bench.aiSittingPosition);
            if (!friendSits)
            {
                _friend.Stand();
                _friend.Face(bench.sittingPosition.position);
            }
            _benchSaid = false;
            Line = SocialLife.T("reallife_social_obj_bench", SocialLife.Data(_who));
            Seating.Check(SocialDirector.Logger, "the player sat down on a street bench; the companion "
                                                 + (friendSits ? "sat down beside them" : "stands by (the bench's second place is taken or out of reach)"), true);
            Go(Step.Bench, now, BenchMost + 30f);
        }

        private void OnBench(float now)
        {
            // Anything that wants the player, or takes the bench away: up at once.
            if (!_seat.Seated || _bench == null || !_bench.isActiveAndEnabled || Wiring.Scenes.PlayerHeld || EnergyHelper.goingToHospital)
            {
                LeaveBench("something else has the player");
                BackToWalk(now);
                return;
            }
            float sat = now - _stepFrom;
            if (!_benchSaid && sat >= 4f)
            {
                _benchSaid = true;
                Say(_who, SocialLines.Pick(SocialCatalogue.WalkTalk));
                Offer(SocialLife.Say("reallife_social_bench_up_prompt", SocialLife.Data(_who)), "reallife_social_button_get_up", null);
            }
            if (sat >= BenchMost)
            {
                LeaveBench("they had sat long enough");
                BackToWalk(now);
            }
        }

        // Safe at any time, any number of times.
        private void LeaveBench(string why)
        {
            bool sat = _seat.Seated;
            _seat.Stand();
            if (_friend != null && !_friend.Lost)
            {
                _friend.Rise();
            }
            if (sat && _bench != null)
            {
                _bench.spawnPedestrians = _benchKept;
            }
            _bench = null;
            if (sat)
            {
                Prompt = null;
                Seating.Check(SocialDirector.Logger, "up from the street bench (" + why + "): the player is free to walk", !_seat.Seated);
            }
        }

        private void BackToWalk(float now)
        {
            _metres = Mathf.Min(WalkGoal, _metres + BenchRest);
            _lastPlayer = D.PlayerPosition;
            _apartSince = -1f;
            _shownTens = -1;
            ShowWalk();
            Go(Step.Walking, now, 420f);
        }

        private void Walking(float now)
        {
            Vector3 player = D.PlayerPosition;
            float moved = JobSpots.Flat(player, _lastPlayer);
            _lastPlayer = player;
            float gap = JobSpots.Flat(_friend.Position, player);
            LookForBench(now, player, gap);
            if (D.OnFoot && gap < 8f && moved < 3f)
            {
                _metres += moved;
            }
            ShowWalk();
            Follow(now, player, gap);

            // A third and two thirds of the way along.
            if (_beats < 2 && _metres >= WalkGoal * (_beats + 1) / 3f && gap < 6f)
            {
                _beats++;
                Say(_who, SocialLines.Pick(SocialCatalogue.WalkTalk));
                // Over the game's own walk: walk_talk for whoever walks on the right, walk_talk_l on the left.
                string talk = _side > 0f ? "walk_talk" : "walk_talk_l";
                if (!_stood && _friend.Speed > 0.3f && ClipLibrary.Has(talk)
                    && _friend.WorkOver(talk, D.Props, 0.3f, ClipEnd.Return, BodyMask.Upper, false))
                {
                    _talkUntil = now + 5f;   // one pass of the loop
                }
                else
                {
                    Gesture(_friend, D.Props, "talk_a");
                }
            }
            if (_talkUntil > 0f && now >= _talkUntil)
            {
                _talkUntil = -1f;
                _friend.StopOver(0.4f);
            }

            if (gap > 30f || !D.OnFoot)
            {
                if (_apartSince < 0f)
                {
                    _apartSince = now;
                }
                else if (now - _apartSince > 15f && !_apartSaid)
                {
                    _apartSaid = true;
                    Say(_who, SocialLines.Pick(SocialCatalogue.WalkBehind));
                }
                else if (now - _apartSince > 45f)
                {
                    LeftBehind(now);
                    return;
                }
            }
            else
            {
                _apartSince = -1f;
                _apartSaid = false;
            }
            if (_metres >= WalkGoal)
            {
                _bench = null;
                Prompt = null;
                _friend.Stand();
                _friend.Face(player);
                Wrap(now, true);
            }
            else if (now >= _stepEnd)
            {
                Wrap(now, _metres > WalkGoal * 0.5f);
            }
        }

        // A new route only when the place beside the player has moved.
        private void Follow(float now, Vector3 player, float gap)
        {
            if (now < _nextFollow)
            {
                return;
            }
            _nextFollow = now + 0.4f;
            Vector3 forward = D.PlayerForward;
            if (gap < 1.9f && D.PlayerSpeed < 0.25f)
            {
                if (!_stood)
                {
                    _stood = true;
                    _friend.Stand();
                    _friend.Face(player);
                }
                return;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (!CitySpots.Pavement(player + right * (_side * 1.1f) - forward * 0.3f, 0.9f, out Vector3 goal))
            {
                _side = -_side;
                if (!CitySpots.Pavement(player + right * (_side * 1.1f) - forward * 0.3f, 0.9f, out goal)
                    && !CitySpots.Pavement(player - forward * 1.3f, 2.5f, out goal))
                {
                    return;   // the player is out on the road: they wait on the pavement
                }
            }
            if (!_stood && JobSpots.Flat(goal, _followGoal) < 0.6f)
            {
                return;
            }
            _followGoal = goal;
            _stood = false;
            _friend.WalkTo(goal, gap > 7f);
        }

        private void LeftBehind(float now)
        {
            Say(_who, SocialLines.Pick(SocialCatalogue.LeftBehind), null, NotificationType.Warning);
            if (!_test)
            {
                SocialLife.Change(_who, 0f, -4f, -3f);
            }
            _settled = true;
            Depart(_friend, now);
            _friend = null;
            Line = null;
            Go(Step.Parting, now, 0f);
        }
    }
}
