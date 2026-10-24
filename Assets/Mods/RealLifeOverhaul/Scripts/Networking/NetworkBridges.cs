using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Networking
{
    /// <summary>One of the people the player knows by their place in the cast; false if there is nobody there.</summary>
    public delegate bool NetPersonOf(int index, out NetPerson person);

    /// <summary>
    /// What the networking life asks of other features, filled in by Wiring/NetworkLinks and left null while that side is
    /// off: the people (the social life), and the favours the careers and the investors make. Each is called on a meeting
    /// or a call, never on a frame.
    /// </summary>
    public static class NetworkBridges
    {
        public static NetPersonOf Person;
        public static Func<int> CastSeed;   // the social life's seed of the save: the rolls' own

        /// <summary>A text from them under the social life's limits (the day's cap, the quiet hours); false if it was not sent.</summary>
        public static Func<int, string, Dictionary<string, string>, bool> Text;

        /// <summary>A line in their thread without a notification and outside the limits: their first word about their work.</summary>
        public static Action<int, string, Dictionary<string, string>> Thread;

        public static Func<string, int, NetResult> JobLead;      // from whom, the salt: the post and the employer
        public static Func<int, NetResult> Freelance;            // the salt: the client and the fee
        public static Func<int, string, NetResult> Investor;     // the salt, from whom: the investor's name

        public static void Reset()
        {
            Person = null;
            CastSeed = null;
            Text = null;
            Thread = null;
            JobLead = null;
            Freelance = null;
            Investor = null;
        }
    }
}
