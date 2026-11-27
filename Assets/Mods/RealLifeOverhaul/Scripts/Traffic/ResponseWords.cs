using System.Collections.Generic;
using Helpers;
using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the player is told when help is sent for: the line (one of several, by a seeded roll that moves with the day and with each call) and
    /// when it comes, in words ("within half a minute"), from ResponseRules. The one place every call's text takes its time from: the 911
    /// contact, a person knocked down, a crash, an accident down the road. Cost: a lookup and a small dictionary on each call, never per frame.
    /// </summary>
    internal static class ResponseWords
    {
        private static readonly string[] Whens =
        {
            "reallife_help_when_20", "reallife_help_when_30", "reallife_help_when_60", "reallife_help_when_120", "reallife_help_when_long"
        };

        /// <summary>The 911 contact's reply to a report of an accident.</summary>
        public static readonly string[] Reported =
        {
            "reallife_911_dispatched", "reallife_911_dispatched_2", "reallife_911_dispatched_3", "reallife_911_dispatched_4"
        };

        /// <summary>The 911 contact's reply when the player asks for an ambulance for themselves.</summary>
        public static readonly string[] Yours =
        {
            "reallife_911_ambulance_sent", "reallife_911_ambulance_sent_2", "reallife_911_ambulance_sent_3", "reallife_911_ambulance_sent_4"
        };

        /// <summary>The warning that somebody has called it in when the player has put a pedestrian on the ground.</summary>
        public static readonly string[] Struck =
        {
            "reallife_pedhit_scene", "reallife_pedhit_scene_2", "reallife_pedhit_scene_3", "reallife_pedhit_scene_4"
        };

        private static int _calls;   // calls told this session: the roll's moment, so two in a day read differently

        /// <summary>When the help comes, in the player's words, for a reply that has {when}.</summary>
        public static string When(float seconds)
        {
            return Whens[ResponseRules.When(seconds)].GetLocalization();
        }

        /// <summary>The placeholders a reply with {when} is shown with.</summary>
        public static Dictionary<string, string> Args(float seconds)
        {
            return new Dictionary<string, string>(1) { { "when", When(seconds) } };
        }

        /// <summary>One of the lines, by a seeded roll: the day, the calls told so far this session and the list's own salt.</summary>
        public static string Pick(string[] lines, int salt)
        {
            return lines[ResponseRules.Pick(lines.Length, TimeHelper.CurrentDay, _calls++, salt)];
        }
    }
}
