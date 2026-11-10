using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Parties
{
    /// <summary>A party the player hosted that was held: where, when, how good, what it cost, and who came (their places in the social life's cast).</summary>
    public struct HostedParty
    {
        public int Id, Day, Hour, Quality, Cost;
        public PartyPlace Place;
        public PartyTier Tier;
        public string Street;
        public int Number;
        public int[] Guests;
    }

    /// <summary>
    /// The one door another feature knocks on to know that the player hosted a party and who came: <see cref="Held"/> is
    /// raised once when a party ends, and <see cref="Since"/> reads the parties still kept (the last
    /// <see cref="PartyCatalogue.HostedMost"/>). It says what happened and changes nothing. Game-free.
    /// </summary>
    public static class PartyHosted
    {
        /// <summary>A party the player hosted has ended, with guests. A listener's fault is its own: the party stands.</summary>
        public static event Action<HostedParty> Held;

        /// <summary>The record of a party as other features read it.</summary>
        public static HostedParty Of(Party p)
        {
            var came = new List<int>(p.Guests.Count);
            for (int i = 0; i < p.Guests.Count; i++)
            {
                if (p.Guests[i].Came)
                {
                    came.Add(p.Guests[i].Person);
                }
            }
            return new HostedParty
            {
                Id = p.Id, Day = p.Day, Hour = p.Hour, Quality = p.Quality, Cost = p.Paid, Place = p.Place, Tier = p.Tier, Street = p.Street, Number = p.Number,
                Guests = came.ToArray()
            };
        }

        /// <summary>The parties held on or after a day that the file still keeps, oldest first, into <paramref name="into"/>; how many.</summary>
        public static int Since(PartyFile f, int day, List<HostedParty> into)
        {
            int n = 0;
            for (int i = 0; f != null && i < f.Parties.Count; i++)
            {
                if (f.Parties[i].State == PartyState.Held && f.Parties[i].Day >= day)
                {
                    into?.Add(Of(f.Parties[i]));
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// The party held on a day that the city talks about: the one with the most guests who came, if they were
        /// <see cref="PartyCatalogue.LargeFrom"/> or more; null with none. For the morning paper's piece.
        /// </summary>
        public static Party Large(PartyFile f, int day)
        {
            Party large = null;
            for (int i = 0; f != null && i < f.Parties.Count; i++)
            {
                Party p = f.Parties[i];
                if (p.State == PartyState.Held && p.Day == day && p.Came >= PartyCatalogue.LargeFrom && (large == null || p.Came > large.Came))
                {
                    large = p;
                }
            }
            return large;
        }

        internal static void Tell(Party p)
        {
            Action<HostedParty> held = Held;
            if (held == null)
            {
                return;
            }
            try
            {
                held(Of(p));
            }
            catch (Exception)
            {
                // a listener's fault: the party was held whatever another feature made of it
            }
        }
    }
}
