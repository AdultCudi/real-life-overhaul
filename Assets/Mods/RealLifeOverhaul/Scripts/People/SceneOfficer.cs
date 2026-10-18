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
    /// markers, the witness, the cordon's opening); alone, both by turns. The one on the post steps out of the open lane and
    /// waves an emergency unit through it. Ticked by AccidentCast.
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

        private const float PointClear = 0.5f;     // metres the lane must be to their left for the pointing clip to be the one that lets a unit past
        private const float PointEvery = 3f;       // seconds between two of those (the clip is 2.6 s long)
        private const float PointInFront = 0.3f;   // cosine: whoever is pointed along must be within about 70 degrees of straight ahead

        private enum Duty { None, Inspect, Interview, Radio, Perimeter, Traffic, Gate, Evidence, Pass, Dismiss }

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

        /// <summary>True while on the traffic post (there, or on the way to it).</summary>
        public bool OnPost => _duty == Duty.Traffic && Role.Present;

        /// <summary>True while stepped out of the open lane, letting a unit past.</summary>
        public bool Passing => _duty == Duty.Pass && Role.Present;

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

        /// <summary>
        /// A response unit is about to pass the scene by the lane traffic is let through in. Whoever is on the traffic post
        /// steps out of that lane to a place beside it, turns to the unit and waves it through (the traffic clip, or the wave
        /// these characters have where that cannot be had), for so many seconds at most. False, and nothing changed, for an
        /// officer who is not on the post.
        /// </summary>
        public bool LetPass(Vector3 aside, Vector3 from, float now, float seconds)
        {
            if (_duty != Duty.Traffic || !Role.Present)
            {
                return false;
            }
            Role.Quiet();
            Role.Drop();
            _duty = Duty.Pass;
            _reached = false;
            _kneeling = false;
            _until = now + seconds;
            // The pointing clip sends whatever passes in front of them along to their own right. That fits where the lane
            // they step out of is on their left as the unit goes: stood facing that lane, the unit comes by from left to
            // right and the arm points the way it is going. On the other side of the lane it would point back up the road,
            // so there (and where the clip cannot be had) they face the unit and work it through as they work traffic.
            Vector3 way = aside - from;
            way.y = 0f;
            Vector3 lane = Role.Position - aside;
            lane.y = 0f;
            bool points = ClipLibrary.Has(SceneClips.PointDirect) && way.sqrMagnitude > 1f
                          && Vector3.Dot(lane, Vector3.Cross(Vector3.up, way.normalized)) < -PointClear;
            if (points)
            {
                Role.Go(aside, Pace.Walk, aside + lane, 6f);
                Role.Repeat(SceneClips.PointDirect, WaveGesture, AnimationType.VictoryStanding, PointEvery, PointEvery + 0.6f);
            }
            else
            {
                Role.Go(aside, Pace.Walk, from, 6f);
                Role.Work(SceneClips.DirectTraffic);
                Role.Repeat(WaveGesture, AnimationType.VictoryStanding, 1.5f, 2.5f);
            }
            ActorPool.Logger?.Info(points ? "People: the officer steps out of the open lane, faces it and points the unit through (point_direct)."
                : "People: the officer steps out of the open lane, faces the unit and waves it through (the traffic clip, or the wave these characters have).");
            return true;
        }

        /// <summary>The unit is past (or is not coming after all): back to the post at the next tick.</summary>
        public void PassDone(float now)
        {
            if (_duty == Duty.Pass)
            {
                _until = now;
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
                if (_duty != Duty.Pass)
                {
                    _until = now + Lasts(_duty);   // (letting a unit past lasts until that unit is past, however long the step aside took)
                }
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
