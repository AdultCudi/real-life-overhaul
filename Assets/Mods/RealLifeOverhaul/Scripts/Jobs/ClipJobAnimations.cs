using BigAmbitions.Characters;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The motions played with the mod's own clips (CityAssets, through the shared ActivityAnimator) where the bundle has
    /// one, and with the game's nearest gesture (<see cref="GameJobAnimations"/>) where it does not, per clip and per call.
    /// The table is the only place a motion is tied to a clip name. A gesture takes the upper body only, on the masked
    /// layer, so the legs stay the game's; what moves the whole body (a car, a boot, a shelf, a counter) is played whole.
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

        // Indexed by JobClip, best first. A clip played faster than it was made was made for a slower moment.
        private static readonly Choice[][] Table =
        {
            new[] { new Choice("job_hail") },                                         // Hail: a loop that goes on until Stop
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
                // Into a car the seated pose is held until they are taken off the street, or they would be seen standing up again
                // first; out of one they are in the seat from the first frame drawn, not faded into it.
                if (player != null && player.Usable
                    && (Whole(clip) ? player.Play(found.Clip, exits ? 0f : Fade, speed, enters ? ClipEnd.Hold : ClipEnd.Return)
                                    : player.PlayOver(found.Clip, BodyMask.Upper, Fade, speed)))
                {
                    if (exits)
                    {
                        ActivityAnimators.StepAll(0.001f);   // the clip has the whole pose before the engine evaluates this frame
                    }
                    return found.Length / speed;   // of a loop: one turn of it
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
                // Has first: a clip a bundle lacks is not worth a warning each time it is looked for. A loop will do only for a
                // motion that goes on (hailing), which is ended with Stop.
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
