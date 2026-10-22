using System;

namespace RealLifeOverhaul.Walking
{
    /// <summary>The game's paces on foot, in the order of its ThirdPersonCharacter.WalkingSpeed (WalkGame reads them over).</summary>
    public enum WalkPace
    {
        Zombie,    // out of energy and food, or the permanent zombie walk: slower than a walk already, with its own clip
        Walk,      // the game's walk: its own people's pace; the player is never put on it by the game
        Jog,       // the game's pace indoors, and outdoors while Toggle Running is held or the player has no energy
        Run,       // the game's pace outdoors
        Scooter,   // on a scooter: a vehicle's pace, not walking
    }

    /// <summary>What walking does to the player's speed when it looks.</summary>
    public enum WalkStep
    {
        Leave,     // as it is
        Hold,      // put the walk on
        GiveBack,  // the game's own speed for its pace back, through the game's own call
    }

    /// <summary>Why a press of the walk key did nothing, silently.</summary>
    public enum WalkRefusal
    {
        None,
        Off,        // its option is off
        NotLoaded,  // no city: the main menu, a load
        Window,     // typing in a box, a menu, the phone, the map, a panel of the game's or the mod's
        Vehicle,    // driving, or on a two-wheeler, a hand truck or a seat of a vehicle
        Busy,       // sitting, working, studying, any activity of the game's, or the game holding the player still
    }

    /// <summary>The game's figures on foot (its GlobalReferences): speeds in m/s, and the rates its walk cycle plays at.</summary>
    public struct WalkFigures
    {
        public float Zombie, Walk, WalkFast, Jog, Run, Scooter;     // m/s
        public float AnimWalk, AnimWalkFast, AnimJog, AnimRun;     // the cycle's turns a second at those speeds
    }

    /// <summary>
    /// Walking's rules, game-free for NavCheck. The game writes its pace's speed into the agent only when the pace changes;
    /// walking writes the game's walk over a run or jog and leaves the pace alone, so the game's choices stand. A speed
    /// neither wrote (a job's, another mod's) is never touched. No allocation.
    /// </summary>
    public static class WalkRules
    {
        public const float SlowestWalk = 0.8f;   // m/s: slower and the game's walk cycle slides on the ground
        public const float FastestWalk = 1.6f;   // m/s: faster is no longer a walk (the game's brisk walk is 1.8)
        public const float Same = 0.01f;         // m/s: two speeds this close are one (the agent keeps the float it is given)
        public const float LookAgain = 0.25f;    // s of real time between looks while what stops the walk lasts (Decide's again)
        public const float MostForward = 6f;     // m/s: the most the game's Move gives its animator's "Forward"

        /// <summary>A press of the key: walking if running, running if walking.</summary>
        public static bool Toggled(bool walking)
        {
            return !walking;
        }

        /// <summary>Walking after a load: never, as the game's own pace and auto-run last a session; nothing is saved.</summary>
        public static bool AfterLoad()
        {
            return false;
        }

        /// <summary>Why a press may not switch walking now: the first reason that holds, or None.</summary>
        public static WalkRefusal CanToggle(bool optionOn, bool loaded, bool window, bool vehicle, bool busy)
        {
            if (!optionOn)
            {
                return WalkRefusal.Off;
            }
            if (!loaded)
            {
                return WalkRefusal.NotLoaded;
            }
            if (window)
            {
                return WalkRefusal.Window;
            }
            if (vehicle)
            {
                return WalkRefusal.Vehicle;
            }
            return busy ? WalkRefusal.Busy : WalkRefusal.None;
        }

        /// <summary>The game's walk, clamped to a walk; 0 if that is not slower than its jog and run (walking does nothing).</summary>
        public static float WalkSpeed(WalkFigures f)
        {
            float walk = Math.Max(SlowestWalk, Math.Min(FastestWalk, f.Walk));
            return walk < f.Jog - Same && walk < f.Run - Same ? walk : 0f;
        }

        /// <summary>The speed the game's SetWalkingSpeed writes for a pace (ThirdPersonCharacter.GetSpeed).</summary>
        public static float GameSpeed(WalkPace pace, WalkFigures f)
        {
            switch (pace)
            {
                case WalkPace.Zombie: return f.Zombie;
                case WalkPace.Walk: return f.Walk;
                case WalkPace.Jog: return f.Jog;
                case WalkPace.Run: return f.Run;
                case WalkPace.Scooter: return f.Scooter;
                default: throw new ArgumentOutOfRangeException(nameof(pace));
            }
        }

        /// <summary>
        /// What to do with the agent's speed <paramref name="agent"/>; <paramref name="runHeld"/> is the game's Toggle
        /// Running held. <paramref name="again"/>: what stops the walk may end without the speed changing (a seat left),
        /// so look again shortly.
        /// </summary>
        public static WalkStep Decide(bool walking, bool onFoot, bool busy, bool runHeld, WalkPace pace, float agent, WalkFigures f, out bool again)
        {
            float walk = WalkSpeed(f);
            bool ours = walk > 0f && Near(agent, walk) && !Near(agent, GameSpeed(pace, f));   // a walk the game did not write itself
            again = false;
            if (!walking || walk <= 0f)
            {
                return ours ? WalkStep.GiveBack : WalkStep.Leave;
            }
            if (!onFoot || busy || runHeld)
            {
                again = true;
                return ours ? WalkStep.GiveBack : WalkStep.Leave;
            }
            if (pace != WalkPace.Jog && pace != WalkPace.Run)
            {
                return ours ? WalkStep.GiveBack : WalkStep.Leave;
            }
            if (ours)
            {
                return WalkStep.Leave;
            }
            return Near(agent, GameSpeed(pace, f)) ? WalkStep.Hold : WalkStep.Leave;
        }

        /// <summary>The walk cycle's rate at a speed, as ThirdPersonCharacter.Move works out the animator's "MotionTime".</summary>
        public static float AnimRate(float speed, WalkFigures f)
        {
            float forward = Math.Min(speed, MostForward);
            if (forward < f.WalkFast)
            {
                return Lerp(f.AnimWalk, f.AnimWalkFast, InverseLerp(f.Walk, f.WalkFast, forward));
            }
            if (forward < f.Jog)
            {
                return Lerp(f.AnimWalkFast, f.AnimJog, InverseLerp(f.WalkFast, f.Jog, forward));
            }
            return Lerp(f.AnimJog, f.AnimRun, InverseLerp(f.Jog, f.Run, forward));
        }

        /// <summary>Whether a speed plays the walk end of the animator's blend: below the brisk walk and the jog.</summary>
        public static bool IsWalk(float speed, WalkFigures f)
        {
            return speed > 0f && speed < f.WalkFast && speed < f.Jog;
        }

        public static bool Near(float a, float b)
        {
            return Math.Abs(a - b) <= Same;
        }

        // Unity's Mathf.Lerp and Mathf.InverseLerp, clamped as they are.
        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Max(0f, Math.Min(1f, t));
        }

        private static float InverseLerp(float a, float b, float v)
        {
            return a == b ? 0f : Math.Max(0f, Math.Min(1f, (v - a) / (b - a)));
        }
    }
}
