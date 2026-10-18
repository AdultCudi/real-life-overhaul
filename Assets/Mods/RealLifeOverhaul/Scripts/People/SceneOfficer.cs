using BigAmbitions.Characters;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What an officer at a scene can see to; filled in by the scene before each tick.</summary>
    internal sealed class SceneBrief
    {
        public Vector3 Scene;
        public bool HasBody;            // someone is lying in the road
        public Vector3 Body;            // the middle of their chest
        public Vector3 BodyKneel;       // where someone looking them over kneels: beside the chest, clear of the paramedics' side
        public bool BodyAttended;       // ...and the paramedics are with them
        public bool CrewOut;            // the paramedics are out of their ambulance (on their way, at work, or on their way back)
        public bool HasWitness;         // someone on their feet who saw it
        public Vector3 Witness;
        public bool HasCordon;          // the road is taped off: there is a traffic post and an edge to walk
        public Vector3 TrafficPost;
        public Vector3 EntryPost;       // ...and an opening to keep people out of: just outside it
        public Vector3 EntryLook;       // ...and where whoever stands there looks
        public bool CanMark;            // something lies at the point of impact that has no numbered marker by it yet
        public Vector3 Evidence;        // ...here
        public bool MarkerDue;          // set by an officer who is down beside it: the scene puts the marker there
        public Vector3 Oncoming;
        public Vector3 Kerbside;        // ...and a place outside the tape for people who are only waiting
        public readonly Vector3[] Corners = new Vector3[4];
    }

    /// <summary>
    /// One police officer at an accident: with two, one keeps the traffic post and the other works the scene (the casualty,
    /// markers, the witness, the cordon's opening); alone, both by turns. Ticked by AccidentCast.
    /// </summary>
    internal sealed class SceneOfficer
    {
        private const float WalkAllowance = 22f;   // seconds a job may take to be reached before it is done from wherever they are

        // The game has no note-taking, radio or traffic-duty animations: stand-ins for when the mod's clip cannot be played.
        private const PermanentAnimationType RadioPose = PermanentAnimationType.TalkingPhone;
        private const PermanentAnimationType NotesPose = PermanentAnimationType.TextingPhone;
        private const string HandProp = "PhoneOff";
        private const AnimationType HoldGesture = AnimationType.IDontThinkSo;
        private const AnimationType WaveGesture = AnimationType.DJEncouraging;

        private const float PointInFront = 0.3f;   // cosine: whoever is pointed along must be within about 70 degrees of straight ahead

        private enum Duty { None, Inspect, Interview, Radio, Perimeter, Traffic, Gate, Evidence, Dismiss }

        private Duty _duty = Duty.None;
        private float _until;
        private bool _reached;      // standing where the job is, with the time it lasts running
        private bool _kneeling;     // looking the casualty over on one knee
        private int _interviews;    // accounts taken so far

        public readonly ActorRole Role = new ActorRole();

        /// <summary>On the traffic post: kept with a colleague on the scene, come back to when alone.</summary>
        public bool OnTraffic { get; set; }

        public bool Alone { get; set; }

        /// <summary>True while standing with a witness, taking their account.</summary>
        public bool Interviewing => _duty == Duty.Interview && _reached && Role.Present;


        /// <summary>Starts work, with whichever job the scene most needs done.</summary>
        public void Begin(SceneBrief brief, float now)
        {
            _interviews = 0;
            _duty = Duty.None;
            Start(Next(brief), brief, now);
        }

        /// <summary>Back to the car: whatever they were doing is dropped. The owner sends them to their door.</summary>
        public void Stop()
        {
            _duty = Duty.None;
            _kneeling = false;
            Role.Quiet();
            Role.Drop();
        }

        public void WaveOn()
        {
            if (_duty == Duty.Traffic && _reached)
            {
                Role.Gesture(WaveGesture, AnimationType.VictoryStanding);
            }
        }

        // With the account taken, the officer points the witness to the kerb when it lies to their right as they face the
        // witness (the clip points right). False when that does not fit: the witness then goes unshown.
        private bool Dismiss(SceneBrief brief, float now)
        {
            if (!brief.HasCordon || !brief.HasWitness || !ClipLibrary.TryGet(SceneClips.PointDirect, out CityClip clip))
            {
                return false;
            }
            Vector3 here = Role.Position;
            Vector3 kerb = brief.Kerbside - here;
            Vector3 witness = brief.Witness - here;
            kerb.y = 0f;
            witness.y = 0f;
            if (kerb.sqrMagnitude < 4f || witness.sqrMagnitude < 0.04f)
            {
                return false;
            }
            Vector3 face = Vector3.Cross(kerb.normalized, Vector3.up);   // standing so, their right arm points at the kerb
            if (Vector3.Dot(face, witness.normalized) < PointInFront)
            {
                return false;
            }
            Role.Quiet();
            Role.Drop();
            Role.Stand(here + face * 3f);
            if (!Role.Act(SceneClips.PointDirect))
            {
                return false;
            }
            _duty = Duty.Dismiss;
            _reached = true;
            _kneeling = false;
            _until = now + clip.Length;
            ActorPool.Logger?.Info("People: the officer has the witness's account and points them out to the kerb (point_direct).");
            return true;
        }

        public void Tick(SceneBrief brief, float now)
        {
            Role.Tick(now);
            if (_duty == Duty.None || !Role.Present)
            {
                return;
            }
            if (!_reached && Role.Settled)
            {
                _reached = true;
                _until = now + Lasts(_duty);
                if (_duty == Duty.Evidence)
                {
                    brief.MarkerDue = true;   // down beside it: the marker is set
                }
            }
            // Paramedics have reached the casualty: the officer gets up and out of their way.
            bool crowded = _kneeling && brief.BodyAttended;
            if (now < _until && !crowded)
            {
                return;
            }
            if (_duty == Duty.Interview && _reached && Dismiss(brief, now))
            {
                return;
            }
            Duty next = Next(brief);
            if (next == _duty && _reached && next == Duty.Traffic)
            {
                _until = now + Lasts(next);   // still on the post: nothing to walk to, nothing to start again
                return;
            }
            Start(next, brief, now);
        }

        // The officer on traffic stays there; otherwise the first job due that they have not just done.
        private Duty Next(SceneBrief brief)
        {
            if (OnTraffic && !Alone && brief.HasCordon)
            {
                return Duty.Traffic;
            }
            if (brief.HasBody && !brief.BodyAttended && _duty != Duty.Inspect)
            {
                return Duty.Inspect;
            }
            if (brief.CanMark && _duty != Duty.Evidence)
            {
                return Duty.Evidence;
            }
            if (brief.HasWitness && _duty != Duty.Interview && _duty != Duty.Dismiss && (_interviews == 0 || Random.value < 0.4f))
            {
                return Duty.Interview;
            }
            if (brief.HasCordon)
            {
                if (Alone && _duty != Duty.Traffic)
                {
                    return Duty.Traffic;
                }
                return _duty == Duty.Gate ? Duty.Radio : Duty.Gate;
            }
            return _duty == Duty.Radio ? Duty.Perimeter : Duty.Radio;
        }

        private void Start(Duty duty, SceneBrief brief, float now)
        {
            Role.Quiet();
            Role.Drop();
            _duty = duty;
            _reached = false;
            _kneeling = false;
            _until = now + WalkAllowance;
            Vector3 here = Role.Position;

            switch (duty)
            {
                case Duty.Inspect:
                    _kneeling = true;
                    Role.Go(brief.BodyKneel, Pace.Walk, brief.Body);
                    Role.Kneel();
                    break;

                case Duty.Interview:
                    _interviews++;
                    Role.Go(Short(brief.Witness, here, 1.6f), Pace.Walk, brief.Witness);
                    Role.Work(SceneClips.Notes, NotesPose, HandProp);   // the notebook or clipboard clip; texting on a phone without either
                    break;

                case Duty.Gate:
                    // Outside the opening, looking up the road: nobody wanders in past them.
                    Role.Go(brief.EntryPost, Pace.Walk, brief.EntryLook);
                    break;

                case Duty.Evidence:
                    Role.Go(Short(brief.Evidence, here, 0.8f), Pace.Walk, brief.Evidence);
                    Role.Kneel();
                    break;

                case Duty.Radio:
                    // Where they stand, turned to the scene: no wandering into the road for it.
                    Role.Stand(brief.Scene);
                    Role.Work(SceneClips.RadioTalk, RadioPose, HandProp);   // a hand to the collar radio; a phone to the ear without the clip
                    break;

                case Duty.Perimeter:
                    {
                        // No tape to work (a minor bump): a few paces off, looking the scene over.
                        float angle = Random.value * Mathf.PI * 2f;
                        Vector3 spot = brief.Scene + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(4f, 6f);
                        Role.Go(spot, Pace.Walk, brief.Scene);
                    }
                    break;

                case Duty.Traffic:
                    Role.HiVis(true);   // for the rest of the scene; the pool puts the shirt back when they leave the street
                    Role.Go(brief.TrafficPost, Pace.Walk, brief.Oncoming);
                    Role.Work(SceneClips.DirectTraffic);   // one hand holding traffic, the other waving it on; without the clip...
                    Role.Repeat(HoldGesture, 3.5f, 6f);   // ...the flat of the hand, again and again
                    break;
            }
        }

        private float Lasts(Duty duty)
        {
            switch (duty)
            {
                case Duty.Inspect: return Random.Range(5f, 9f);
                case Duty.Interview: return Random.Range(9f, 15f);
                case Duty.Radio: return Random.Range(5f, 9f);
                case Duty.Perimeter: return Random.Range(4f, 7f);
                case Duty.Gate: return Random.Range(9f, 15f);
                case Duty.Evidence: return Random.Range(3f, 5f);
                default: return Alone ? Random.Range(14f, 22f) : Random.Range(22f, 38f);
            }
        }

        private static Vector3 Short(Vector3 target, Vector3 from, float by)
        {
            Vector3 back = from - target;
            back.y = 0f;
            return back.sqrMagnitude < 0.01f ? target + Vector3.forward * by : target + back.normalized * by;
        }
    }
}
