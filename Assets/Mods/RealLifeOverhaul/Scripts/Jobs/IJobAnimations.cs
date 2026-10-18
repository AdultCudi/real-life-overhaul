using BigAmbitions.Characters;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The motions a job asks of a person, named for what they are, not for the animation that plays them (docs/features/jobs.md, "Job clips").</summary>
    public enum JobClip
    {
        Hail,       // an arm up at the kerb for a car
        HandOver,   // holds something out to the person in front
        Receive,    // takes what is held out
        Refuse,     // a shake of the head and a hand: no thanks
        Thank,      // a nod and a small wave on parting
        Knock,      // raps on a door
        Annoyed,    // a jolt and a glare: that was a bad bit of driving
        HandOverLeft,   // the same as HandOver with the other hand
        ReceiveBox,     // takes a parcel in both hands
        CarEnter,       // opens a rear door and gets in: played at the door; the person is taken off the street when it ends
        CarExit,        // gets out of a rear door and straightens up
        LoadBoot,       // lowers a box into a car
        Stock,          // puts things from a box onto a shelf in front
        Serve,          // hands something over a counter
        CarEnterLeft,   // CarEnter at a door on the car's left side; CarEnter is for its right
        CarExitLeft     // ...and CarExit there
    }

    /// <summary>How job code moves a body: by <see cref="JobClip"/>, never by a game animation (<see cref="ClipJobAnimations"/>).</summary>
    public interface IJobAnimations
    {
        /// <summary>Plays a clip on a character; returns the seconds to give the beat, or zero if this character cannot do it (nothing is faked).</summary>
        float Play(BaseHuman who, JobClip clip);

        /// <summary>Seconds into the beat of a named moment ("release", "grip") for what Play would play now; negative without one.</summary>
        float Moment(JobClip clip, string mark);

        /// <summary>Ends a motion that goes on until told (hailing); nothing happens for one that ends by itself.</summary>
        void Stop(BaseHuman who);
    }
}
