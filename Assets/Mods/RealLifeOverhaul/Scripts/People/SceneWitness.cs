using BigAmbitions.Characters;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Someone unhurt on their feet at an accident (the other driver, or the one who knocked a person down): checks on
    /// whoever lies in the road, phones for help, looks down the road for the ambulance, watches, and tells an officer
    /// who stands with them. Once the tape is up or the crew is out they wait at the kerb. Ticked by AccidentCast.
    /// </summary>
    internal sealed class SceneWitness
    {
        private const float WalkAllowance = 16f;   // seconds a step may take to be reached before it is done from wherever they are
        private const float ListenRange = 3.5f;    // metres: an officer this close is one they are talking to

        private const PermanentAnimationType PhonePose = PermanentAnimationType.TalkingPhone;
        private const string PhoneProp = "PhoneOff";
        private const AnimationType TellGesture = AnimationType.IDontThinkSo;

        private enum Act { None, Check, Call, Look, Watch, Tell, Aside }

        private ActorRole _role;
        private Act _act = Act.None;
        private float _until;
        private bool _reached;
        private bool _called;       // they have made their call: once is enough
        private bool _telling;      // at the kerb, an officer is with them and they are telling it

        public bool Active => _act != Act.None && _role != null && _role.Present;

        public bool Is(ActorRole role) => role != null && ReferenceEquals(_role, role);

        public void Begin(ActorRole role, SceneBrief brief, float now)
        {
            _role = role;
            _called = false;
            _telling = false;
            Start(brief.HasBody && !brief.BodyAttended ? Act.Check : Act.Call, brief, now);
        }

        public void Stop()
        {
            _act = Act.None;
            _role = null;
        }

        /// <summary>Sent to the kerb now (the ambulance is about to drive through), there until it is over.</summary>
        public void Aside(SceneBrief brief, float now)
        {
            if (Active && _act != Act.Aside)
            {
                Start(Act.Aside, brief, now);
            }
        }

        /// <summary><paramref name="officerHere"/>: an officer standing at <paramref name="officer"/> is taking their account.</summary>
        public void Tick(SceneBrief brief, bool officerHere, Vector3 officer, float now)
        {
            if (!Active)
            {
                return;
            }
            if (!_reached && _role.Settled)
            {
                _reached = true;
                _until = now + Lasts(_act);
            }
            bool listening = officerHere && (officer - _role.Position).sqrMagnitude < ListenRange * ListenRange;
            if (_act == Act.Aside)
            {
                // At the kerb until the scene is over; an officer who comes over gets their account there.
                if (_reached && listening != _telling)
                {
                    _telling = listening;
                    if (listening)
                    {
                        _role.Stand(officer);
                        _role.Repeat(TellGesture, 3.5f, 7f);
                    }
                    else
                    {
                        _role.Quiet();
                    }
                }
                return;
            }
            // Once the tape is up or the crew is out, nobody but the crews belongs by the casualty: to the kerb at once.
            if (Outside(brief))
            {
                _role.Quiet();
                Start(Act.Aside, brief, now);
                return;
            }

            if (listening && _act != Act.Tell)
            {
                _act = Act.Tell;
                _reached = true;
                _until = float.MaxValue;
                _role.Drop();
                _role.Stand(officer);
                _role.Repeat(TellGesture, 3.5f, 7f);
                return;
            }
            if (_act == Act.Tell)
            {
                if (listening)
                {
                    return;
                }
                _role.Quiet();   // the officer has gone: nobody left to tell
                Start(Next(brief, now), brief, now);
                return;
            }

            bool crowded = _act == Act.Check && brief.BodyAttended;
            if (now < _until && !crowded)
            {
                return;
            }
            Start(Next(brief, now), brief, now);
        }

        private static bool Outside(SceneBrief brief)
        {
            return brief.HasCordon || brief.CrewOut;
        }

        private Act Next(SceneBrief brief, float now)
        {
            if (Outside(brief))
            {
                return Act.Aside;
            }
            switch (_act)
            {
                case Act.Check: return _called ? Act.Look : Act.Call;
                case Act.Call: return Act.Look;
                case Act.Look: return Act.Watch;
                default: return brief.HasBody && !brief.BodyAttended && Random.value < 0.6f ? Act.Check : Act.Look;
            }
        }

        private void Start(Act act, SceneBrief brief, float now)
        {
            _role.Quiet();
            _role.Drop();
            _act = act;
            _reached = false;
            _until = now + WalkAllowance;
            Vector3 here = _role.Position;
            Vector3 focus = brief.HasBody ? brief.Body : brief.Scene;

            switch (act)
            {
                case Act.Check:
                    // Down beside them, on the side away from where the crews will work.
                    _role.Go(brief.BodyKneel, Pace.Jog, brief.Body, WalkAllowance);
                    _role.Kneel();
                    break;

                case Act.Call:
                    _called = true;
                    _role.Go(Short(focus, here, 2.4f), Pace.Walk, focus, WalkAllowance);
                    _role.Hold(PhonePose, PhoneProp);
                    break;

                case Act.Look:
                    // A few paces towards where help will come from, looking down the road.
                    _role.Go(Short(focus, brief.Oncoming, Random.Range(3.5f, 5f)), Pace.Walk, brief.Oncoming, WalkAllowance);
                    break;

                case Act.Watch:
                    _role.Go(Short(focus, here, 2.8f), Pace.Walk, focus, WalkAllowance);
                    break;

                case Act.Aside:
                    _role.Go(brief.Kerbside, Pace.Walk, focus, 40f);
                    break;
            }
        }

        private static float Lasts(Act act)
        {
            switch (act)
            {
                case Act.Check: return Random.Range(5f, 8f);
                case Act.Call: return Random.Range(8f, 13f);
                case Act.Look: return Random.Range(4f, 7f);
                case Act.Aside: return float.MaxValue;
                default: return Random.Range(6f, 10f);
            }
        }

        private static Vector3 Short(Vector3 target, Vector3 towards, float by)
        {
            Vector3 back = towards - target;
            back.y = 0f;
            return back.sqrMagnitude < 0.01f ? target + Vector3.forward * by : target + back.normalized * by;
        }
    }
}
