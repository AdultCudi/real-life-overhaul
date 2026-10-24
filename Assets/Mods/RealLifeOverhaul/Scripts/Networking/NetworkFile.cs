using System.Collections.Generic;

namespace RealLifeOverhaul.Networking
{
    /// <summary>The position somebody the player knows holds. Saved as a number: new ones go at the end.</summary>
    public enum NetRole { None = 0, Buyer = 1, Wholesale = 2, Lawyer = 3, Recruiter = 4, Investor = 5, Official = 6, Landlord = 7 }

    /// <summary>What somebody with a position can do for the player, once each. Saved as a bit each: new ones go at the end.</summary>
    public enum NetFavour { None = 0, JobLead = 1, Freelance = 2, Importer = 3, Wholesaler = 4, Terms = 5, Investor = 6, Property = 7 }

    /// <summary>How they came into the player's life: met by chance, or brought along by a friend or by family. Saved as a number.</summary>
    public enum NetVia { Met = 0, Friend = 1, Family = 2 }

    /// <summary>
    /// One of the people the player knows, as the networking life sees them: given by the social life (through
    /// Wiring/NetworkLinks), never kept. Game-free.
    /// </summary>
    public struct NetPerson
    {
        public int Index;
        public string Name;
        public int Origin;       // SocialOrigin's number
        public int Stage;        // SocialStage's number: 2 a friend, 3 a close friend, 4 and 5 dating and a partner
        public int MetDay;
        public bool Met;
    }

    /// <summary>One person rolled for a position when they were met, and what they have done for the player. Game-free.</summary>
    public sealed class NetContact
    {
        public int Person;            // their index in the social life's cast
        public NetRole Role;
        public NetVia Via;
        public int MetDay;
        public int Given;             // a bit per NetFavour done: never twice
        public int Told;              // a bit per NetFavour they have texted about being ready: never twice
        public string Org = string.Empty;      // where they hold it: a name, or a locale key (the official's office)
        public string Street = string.Empty;   // the importer's or wholesaler's address for a buyer or a manager; empty for the rest
        public int Number;

        public bool Has(NetFavour favour) => (Given & Bit(favour)) != 0;
        public bool Said(NetFavour favour) => (Told & Bit(favour)) != 0;

        public static int Bit(NetFavour favour) => 1 << (int)favour;
    }

    /// <summary>The networking life of a save: everybody rolled, and when the last favour was done. Game-free.</summary>
    public sealed class NetworkFile
    {
        public const int Most = 400;   // as many as the social life's cast (SocialRules.MaxCast)

        public readonly List<NetContact> Contacts = new List<NetContact>(32);
        public int LastFavourDay = -1000;   // the game day the last favour was done, by anybody

        public NetContact Of(int person)
        {
            for (int i = 0; i < Contacts.Count; i++)
            {
                if (Contacts[i].Person == person)
                {
                    return Contacts[i];
                }
            }
            return null;
        }
    }

    /// <summary>What came of asking a favour: done (and the two values its line names), or why not (a locale key).</summary>
    public struct NetResult
    {
        public bool Done;
        public string Why;
        public string A, B;

        public static NetResult Yes(string a, string b = null) => new NetResult { Done = true, A = a ?? string.Empty, B = b ?? string.Empty };
        public static NetResult No(string why) => new NetResult { Why = why };
    }
}
