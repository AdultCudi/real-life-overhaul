using System.Collections;
using BigAmbitions.DayNightCycle;
using Timemachine;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The game's time machine (the sleep and the debug console's skiptime go through it).</summary>
    internal static class SelfTestTime
    {
        public static bool Skip(string span)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || uis.timeMachine.isRunning)
            {
                return false;
            }
            TimeMachine.SkipTime(span, true);   // at a constant speed: the ease-out of the player's own skip would make a week take minutes
            return true;
        }

        public static bool SkipTo(int day, int hour, float minute)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || uis.timeMachine.isRunning)
            {
                return false;
            }
            uis.timeMachine.StartTimeMachine(new Timestamp(day, hour, minute), true, null, true, true);
            return true;
        }

        public static bool Running()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.timeMachine != null && (uis.timeMachine.isRunning || uis.timeMachine.isBlockingUi);
        }
    }
}
