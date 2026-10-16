using System.Collections.Generic;
using BigAmbitions.Characters;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The motions a job asks of a person, named for what they are and not for the animation
    /// that plays them. The wish-list of clips, with length, hand and prop socket for each, is
    /// in docs/features/jobs.md ("Job clips").
    /// </summary>
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
        CarEnter,       // opens a rear door and gets in: played at the door, the person is taken off the street when it ends
        CarExit,        // gets out of a rear door and straightens up
        LoadBoot,       // lowers a box into a car
        Stock,          // puts things from a box onto a shelf in front
        Serve,          // hands something over a counter
        CarEnterLeft,   // CarEnter at a door on the car's left side (CarEnter is for one on its right)
        CarExitLeft     // ...and CarExit there
    }

    /// <summary>
    /// The one door job code goes through to move a person's body. Jobs never name an animation
    /// of the game; they ask for a <see cref="JobClip"/>. Today that is played by the nearest
    /// animation the game's characters have (<see cref="GameJobAnimations"/>). When new clips
    /// are shipped (the city-life ActivityAnimator), a second implementation takes its place in
    /// <see cref="JobDirector"/> and no job changes.
    /// </summary>
    public interface IJobAnimations
    {
        /// <summary>
        /// Plays a clip on a character and returns how long the beat should be given, in
        /// seconds. Zero if this character cannot do it: the caller carries on at once and
        /// nothing is faked in its place.
        /// </summary>
        float Play(BaseHuman who, JobClip clip);

        /// <summary>
        /// Seconds into that beat at which a named moment of it falls ("release": the hand lets go; "grip": it
        /// closes), for whatever Play would play right now. Negative if that has no such moment: the caller
        /// keeps the timing it had before.
        /// </summary>
        float Moment(JobClip clip, string mark);

        /// <summary>Ends a motion that goes on until told (hailing); nothing happens for one that ends by itself.</summary>
        void Stop(BaseHuman who);
    }

    /// <summary>
    /// The clips played with animations the game's street characters and the player already
    /// have. Each is the one the rest of the mod found to read nearest (the reach of an arrest,
    /// the wave of an officer directing traffic, the gesture of an argument). Whether a given
    /// animator has a trigger is asked once per animator controller and remembered.
    /// </summary>
    public sealed class GameJobAnimations : IJobAnimations
    {
        private static readonly int AnimationSpeedId = Animator.StringToHash("AnimationSpeed");

        // clip -> the game's animation, and the seconds the beat is given (measured by eye in the mod's other scenes)
        private static readonly (AnimationType animation, float seconds)[] Map =
        {
            (AnimationType.DJEncouraging, 1.6f),   // Hail
            (AnimationType.PressAction, 1.2f),     // HandOver
            (AnimationType.PressAction, 1.2f),     // Receive
            (AnimationType.IDontThinkSo, 1.8f),    // Refuse
            (AnimationType.DJEncouraging, 1.4f),   // Thank
            (AnimationType.PressAction, 1.0f),     // Knock
            (AnimationType.IDontThinkSo, 1.6f),    // Annoyed
            (AnimationType.PressAction, 1.2f),     // HandOverLeft
            (AnimationType.PressAction, 1.2f),     // ReceiveBox
            (AnimationType.PressAction, 0f),       // CarEnter: nothing of the game's stands in for it
            (AnimationType.PressAction, 0f),       // CarExit
            (AnimationType.PressAction, 1.0f),     // LoadBoot
            (AnimationType.PressAction, 1.2f),     // Stock
            (AnimationType.PressAction, 1.0f),     // Serve
            (AnimationType.PressAction, 0f),       // CarEnterLeft
            (AnimationType.PressAction, 0f)        // CarExitLeft
        };

        private readonly Dictionary<long, bool> _known = new Dictionary<long, bool>(16);

        public float Play(BaseHuman who, JobClip clip)
        {
            // Any of the game's people: a full street character, or one of the plainer ones that only stand or sit.
            if (who == null || (who is ThirdPersonCharacter full && (!full.visible || RagdollSystem.Holds(full))))
            {
                return 0f;
            }
            Animator animator = who.animator;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                return 0f;
            }
            (AnimationType animation, float seconds) = Map[(int)clip];
            if (seconds <= 0f)
            {
                return 0f;
            }
            long key = ((long)animator.runtimeAnimatorController.GetInstanceID() << 8) | (uint)clip;
            if (!_known.TryGetValue(key, out bool known))
            {
                known = HasTrigger(animator, animation.ToString());
                _known[key] = known;
            }
            if (!known)
            {
                return 0f;
            }
            animator.SetFloat(AnimationSpeedId, 1f);
            animator.SetTrigger(animation);
            return seconds;
        }

        public float Moment(JobClip clip, string mark)
        {
            return -1f;   // the game's gestures say nothing about when a hand closes
        }

        public void Stop(BaseHuman who)
        {
        }

        private static bool HasTrigger(Animator animator, string name)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;   // allocates: asked once per controller and clip
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// The clips played with the mod's own Humanoid clips (CityAssets, through the shared
    /// ActivityAnimator) wherever the bundle has one, and with the game's nearest gesture
    /// (<see cref="GameJobAnimations"/>) wherever it does not: per clip, per call, so a bundle
    /// that gains a clip is used from the next play on and nothing else changes. Each JobClip
    /// lists the clips that will do, best first; some are the living city's (a leaflet held
    /// out, a wave). This table is the only place a job motion is tied to a clip name.
    ///
    /// A gesture (hailing, handing over, taking, refusing, thanking, knocking, annoyed) takes
    /// the upper body only, on the masked layer: the legs stay the game's, so it can be made
    /// by somebody who is walking or about to. What moves the whole body by its nature (into
    /// and out of a car, down to a boot, a shelf, a counter) is played whole, standing.
    /// </summary>
    public sealed class ClipJobAnimations : IJobAnimations
    {
        private readonly struct Choice
        {
            public readonly string Name;
            public readonly float Speed;

            public Choice(string name, float speed = 1f)
            {
                Name = name;
                Speed = speed;
            }
        }

        // Indexed by JobClip. A clip played faster than it was made is one that was made for a slower moment.
        private static readonly Choice[][] Table =
        {
            new[] { new Choice("job_hail") },                                         // Hail (a loop: it goes on until Stop)
            new[] { new Choice("job_hand_over"), new Choice("leaflet_offer", 1.7f) }, // HandOver
            new[] { new Choice("job_receive") },                                      // Receive
            new[] { new Choice("job_refuse") },                                       // Refuse
            new[] { new Choice("job_thank"), new Choice("wave_hello", 1.3f) },        // Thank
            new[] { new Choice("job_knock") },                                        // Knock
            new[] { new Choice("job_annoyed") },                                      // Annoyed
            new[] { new Choice("job_hand_over_l") },                                  // HandOverLeft
            new[] { new Choice("job_receive_box"), new Choice("job_receive") },       // ReceiveBox
            new[] { new Choice("job_car_enter") },                                    // CarEnter
            new[] { new Choice("job_car_exit") },                                     // CarExit
            new[] { new Choice("job_load_boot") },                                    // LoadBoot
            new[] { new Choice("stock_shelf") },                                      // Stock
            new[] { new Choice("serve_counter"), new Choice("serve", 1.5f) },         // Serve
            new[] { new Choice("job_car_enter_l") },                                  // CarEnterLeft
            new[] { new Choice("job_car_exit_l") }                                    // CarExitLeft
        };

        private const float Fade = 0.2f;

        private readonly GameJobAnimations _game = new GameJobAnimations();

        /// <summary>True if the bundle has one of the mod's own clips for this motion right now.</summary>
        public static bool Has(JobClip clip)
        {
            return Find(clip, out _, out _);
        }

        public float Play(BaseHuman who, JobClip clip)
        {
            if (who == null || (who is ThirdPersonCharacter full && (!full.visible || RagdollSystem.Holds(full))))
            {
                return 0f;
            }
            Animator animator = who.animator;
            if (animator != null && animator.isActiveAndEnabled && ActivityAnimators.Working && Find(clip, out CityClip found, out float speed))
            {
                ActivityAnimator player = ActivityAnimators.For(animator);
                bool enters = clip == JobClip.CarEnter || clip == JobClip.CarEnterLeft;
                bool exits = clip == JobClip.CarExit || clip == JobClip.CarExitLeft;
                // Into a car: the last pose (seated) is held until whoever asked takes them off the street, or they would
                // be seen to stand up outside the door again first. Out of one: they are in the seat from the first frame
                // they are drawn in, not faded into it from standing at the door.
                if (player != null && player.Usable
                    && (Whole(clip) ? player.Play(found.Clip, exits ? 0f : Fade, speed, enters ? ClipEnd.Hold : ClipEnd.Return)
                                    : player.PlayOver(found.Clip, BodyMask.Upper, Fade, speed)))
                {
                    if (exits)
                    {
                        ActivityAnimators.StepAll(0.001f);   // the clip has the whole pose before the engine evaluates this frame
                    }
                    return found.Length / speed;   // (of a loop: one turn of it)
                }
            }
            return _game.Play(who, clip);
        }

        public float Moment(JobClip clip, string mark)
        {
            if (!ActivityAnimators.Working || !Find(clip, out CityClip found, out float speed))
            {
                return -1f;
            }
            float at = found.Mark(mark);
            return at < 0f ? -1f : at / speed;
        }

        public void Stop(BaseHuman who)
        {
            Animator animator = who != null ? who.animator : null;
            if (animator != null && ActivityAnimators.Working)
            {
                ActivityAnimator player = ActivityAnimators.For(animator);
                if (player != null && player.Over != null)
                {
                    player.StopOver(Fade);
                }
            }
        }

        // What cannot be done with the upper body alone.
        private static bool Whole(JobClip clip)
        {
            switch (clip)
            {
                case JobClip.CarEnter:
                case JobClip.CarExit:
                case JobClip.CarEnterLeft:
                case JobClip.CarExitLeft:
                case JobClip.LoadBoot:
                case JobClip.Stock:
                case JobClip.Serve:
                    return true;
                default:
                    return false;
            }
        }

        private static bool Find(JobClip clip, out CityClip found, out float speed)
        {
            found = null;
            speed = 1f;
            if (!ClipLibrary.Ready)
            {
                return false;
            }
            Choice[] choices = Table[(int)clip];
            for (int i = 0; i < choices.Length; i++)
            {
                // (Has first: a clip a bundle lacks is not worth a warning in the log each time it is looked for.)
                // A loop will do only where the motion is one that goes on (hailing): it is ended with Stop.
                if (ClipLibrary.Has(choices[i].Name) && ClipLibrary.TryGet(choices[i].Name, out found) && (!found.Loop || clip == JobClip.Hail))
                {
                    speed = choices[i].Speed;
                    return true;
                }
            }
            found = null;
            return false;
        }
    }
}
