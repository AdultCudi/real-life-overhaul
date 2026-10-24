using System.Collections.Generic;
using BAModAPI;
using UI.Notification;

namespace RealLifeOverhaul.Networking
{
    /// <summary>
    /// The networking life: some of the people the player meets hold a position (a buyer at an importer, a wholesaler's
    /// manager, a law firm's partner, a recruiter, an investor, a city official, a landlord), said when they are met and on
    /// the People page; as the relationship grows each offers what a position can, once: a job lead, freelance work, an
    /// introduction to an importer or a wholesaler, better terms, an investor meeting, a tip about a building for sale.
    /// It has no Update and no save handler: it is asked on a meeting, on a call, and once a game day by the social
    /// life's round (Wiring/NetworkLinks), and writes its line into the save's data when something changes, which is rare.
    /// COST: nothing a frame; a day's round is one pass over the people rolled.
    /// </summary>
    public static partial class NetworkDirector
    {
        public static IModLogger Logger;

        private static NetworkFile _file;
        private static Dictionary<string, string> _data;   // the save's data the file was read from: another save's starts afresh
        private static bool _swept;
        private static readonly Dictionary<int, NetRole> TestRoles = new Dictionary<int, NetRole>(2);   // a test's position for the next meeting; never saved
        private static int _testReady = -1;   // a test's person whose next favour is ready on their next call, whatever the stage

        /// <summary>The option: with it off nobody is rolled, told or asked, and the game is as without the feature.</summary>
        public static bool On => Settings.Social && Settings.SocialNetworking;

        /// <summary>Every save's start: the next ask reads the save afresh.</summary>
        public static void Reset()
        {
            _file = null;
            _data = null;
            _swept = false;
            TestRoles.Clear();
            _testReady = -1;
        }

        // The save's file, read once a save; null without a save or when its line is one this version cannot read.
        private static NetworkFile File()
        {
            Dictionary<string, string> data = NetworkGame.Data();
            if (data == null)
            {
                return null;
            }
            if (!ReferenceEquals(data, _data))
            {
                _data = data;
                _file = NetworkStore.Load(data);
                _swept = false;
                TestRoles.Clear();
                _testReady = -1;
                Logger?.Info("Networking: " + (_file == null ? "the save's line could not be read and is kept as it was; nobody is rolled this session."
                    : _file.Contacts.Count + " people rolled for a position, " + Holding(_file) + " holding one."));
            }
            return _file;
        }

        private static int Holding(NetworkFile f)
        {
            int n = 0;
            for (int i = 0; i < f.Contacts.Count; i++)
            {
                n += f.Contacts[i].Role != NetRole.None ? 1 : 0;
            }
            return n;
        }

        private static void Write()
        {
            NetworkStore.Save(_data, _file);
        }

        private static int Seed => NetworkBridges.CastSeed != null ? NetworkBridges.CastSeed() : 0;

        /// <summary>
        /// The position somebody about to be met would hold, without recording it: the meeting's words say it before
        /// numbers are swapped, and <see cref="Met"/> then records the same. None while the option is off.
        /// </summary>
        public static NetRole Peek(NetPerson p, NetVia via, out string org)
        {
            org = null;
            NetworkFile f = On ? File() : null;
            if (f == null)
            {
                return NetRole.None;
            }
            NetContact known = f.Of(p.Index);
            if (known != null)
            {
                org = OrgName(known);
                return known.Role;
            }
            NetContact c = Roll(p, via);
            org = OrgName(c);
            return c.Role;
        }

        /// <summary>
        /// Somebody has just been met: rolled once and recorded; one who holds a position is told in a notification and
        /// in a first text in their thread. <paramref name="quiet"/> records without telling (people met before).
        /// </summary>
        public static NetContact Met(NetPerson p, NetVia via, bool quiet)
        {
            NetworkFile f = On ? File() : null;
            if (f == null || f.Of(p.Index) != null || f.Contacts.Count >= NetworkFile.Most)
            {
                return f?.Of(p.Index);
            }
            NetContact c = Roll(p, via);
            f.Contacts.Add(c);
            Write();
            if (c.Role == NetRole.None)
            {
                return c;
            }
            Logger?.Info("Networking: " + p.Name + " (" + via + ") is " + c.Role + " at " + c.Org + (quiet ? ", rolled for somebody met before" : string.Empty) + ".");
            if (!quiet)
            {
                Dictionary<string, string> data = Data(p, c);
                NetworkGame.Tell(NotificationType.Success, NetworkCatalogue.Met, data);
                NetworkBridges.Thread?.Invoke(p.Index, NetworkCatalogue.TextKey(c.Role), Data(p, c));   // a dictionary each: the game keeps a message's data
                Check("met", p.Name + " recorded as " + c.Role + " at " + c.Org + ", via " + via + ", told in a notification and in their thread", f.Of(p.Index) == c,
                    TestRoles.Remove(p.Index));
            }
            return c;
        }

        // What a person is: the save's seed and their place in the cast, how they were met, and the city's importers.
        private static NetContact Roll(NetPerson p, NetVia via)
        {
            int seed = Seed;
            NetRole role = TestRoles.TryGetValue(p.Index, out NetRole forced) ? forced : NetworkRules.RoleOf(seed, p.Index, p.Origin, via);
            var c = new NetContact { Person = p.Index, Role = role, Via = via, MetDay = p.MetDay > 0 ? p.MetDay : NetworkGame.Day };
            if (role == NetRole.None)
            {
                return c;
            }
            if ((role == NetRole.Buyer || role == NetRole.Wholesale)
                && NetworkGame.Supplier(role == NetRole.Buyer, seed, p.Index, out string name, out string street, out int number))
            {
                c.Org = name;
                c.Street = street;
                c.Number = number;
                return c;
            }
            string[] orgs = NetworkCatalogue.OrgsOf(role);
            c.Org = orgs[NetworkRules.OrgOf(seed, p.Index, orgs.Length)];
            return c;
        }

        /// <summary>
        /// Everybody met with no record yet (met before the feature, or while it was off) is rolled once, quietly, as met by
        /// chance. Once a save: a pass over the cast.
        /// </summary>
        public static void Sweep(int count)
        {
            if (_swept || !On || File() == null || NetworkBridges.Person == null)
            {
                return;
            }
            _swept = true;
            for (int i = 0; i < count; i++)
            {
                if (NetworkBridges.Person(i, out NetPerson p) && p.Met && _file.Of(i) == null)
                {
                    Met(p, NetVia.Met, true);
                }
            }
        }

        /// <summary>The record of somebody, or null (not rolled, or the option off).</summary>
        public static NetContact Of(int person)
        {
            NetworkFile f = On ? File() : null;
            if (f != null && !_swept)
            {
                Sweep(NetworkFile.Most);   // (the first ask of a save: people met before are rolled now, not at the next day's round)
            }
            return f?.Of(person);
        }

        /// <summary>"a recruiter at Northline Staffing", or null for somebody who holds no position.</summary>
        public static string Position(int person)
        {
            NetContact c = Of(person);
            return c != null && c.Role != NetRole.None ? NetworkGame.T(NetworkCatalogue.RoleKey(c.Role), new Dictionary<string, string> { { "org", OrgName(c) } }) : null;
        }

        /// <summary>The favour somebody would do if asked today, or None.</summary>
        public static NetFavour Ready(int person)
        {
            NetContact c = Of(person);
            if (c == null || c.Role == NetRole.None || NetworkBridges.Person == null || !NetworkBridges.Person(person, out NetPerson p) || !p.Met)
            {
                return NetFavour.None;
            }
            return NetworkRules.Next(c, p.Stage, c.MetDay, NetworkGame.Day, _file.LastFavourDay, _testReady == person);
        }

        /// <summary>
        /// The favour asked on a call, done now through the feature that makes it, and kept as done so it is never done
        /// twice; or why not, and then it can be asked again another day.
        /// </summary>
        public static NetResult Ask(int person)
        {
            NetFavour favour = Ready(person);
            NetContact c = Of(person);
            if (favour == NetFavour.None || c == null || !NetworkBridges.Person(person, out NetPerson p))
            {
                return NetResult.No("reallife_social_net_why_not_now");
            }
            bool test = _testReady == person;
            NetResult r = Do(c, p, favour);
            if (r.Done)
            {
                c.Given |= NetContact.Bit(favour);
                c.Told |= NetContact.Bit(favour);
                _file.LastFavourDay = NetworkGame.Day;
                if (_testReady == person)
                {
                    _testReady = -1;
                }
                Write();
            }
            // Done and kept as done, or refused and left to be asked again: anything else is a fault of the record.
            Check("favour", p.Name + " (" + c.Role + ") asked for " + favour + ": " + (r.Done ? "done, " + r.A + ", " + r.B + "; kept as done" : "not done, " + r.Why + "; left to ask again"),
                r.Done == c.Has(favour), test);
            return r;
        }

        // The favour itself: the careers and the investors through their bridges, the game's own suppliers and listings here.
        private static NetResult Do(NetContact c, NetPerson p, NetFavour favour)
        {
            int salt = NetworkRules.Salt(c.Person, favour);
            switch (favour)
            {
                case NetFavour.JobLead:
                    return NetworkBridges.JobLead != null ? NetworkBridges.JobLead(p.Name, salt) : NetResult.No("reallife_social_net_why_no_lead");
                case NetFavour.Freelance:
                    return NetworkBridges.Freelance != null ? NetworkBridges.Freelance(salt) : NetResult.No("reallife_social_net_why_no_contract");
                case NetFavour.Investor:
                    return NetworkBridges.Investor != null ? NetworkBridges.Investor(salt, p.Name) : NetResult.No("reallife_social_net_why_no_investor");
                case NetFavour.Importer:
                case NetFavour.Wholesaler:
                    return Introduce(c, favour == NetFavour.Importer);
                case NetFavour.Terms:
                    int changed = NetworkGame.WaiveFees(c.Street, c.Number, out float saved);
                    return changed > 0 ? NetResult.Yes(OrgName(c), NetworkGame.Money(saved)) : NetResult.No("reallife_social_net_why_no_terms");
                case NetFavour.Property:
                    return NetworkGame.Tip(Seed, salt, out string address, out float price)
                        ? NetResult.Yes(address, NetworkGame.Money(price)) : NetResult.No("reallife_social_net_why_no_property");
                default:
                    return NetResult.No("reallife_social_net_why_not_now");
            }
        }

        // Their own importer or wholesaler, or for a buyer's wholesaler the city's one drawn for them.
        private static NetResult Introduce(NetContact c, bool importer)
        {
            bool own = importer ? c.Role == NetRole.Buyer : c.Role == NetRole.Wholesale;
            string street = own ? c.Street : null;
            int number = own ? c.Number : 0;
            if (string.IsNullOrEmpty(street) && !NetworkGame.Supplier(importer, Seed, c.Person, out _, out street, out number))
            {
                return NetResult.No("reallife_social_net_why_no_supplier");
            }
            return NetworkGame.Introduce(street, number, out string name, out string address) ? NetResult.Yes(name, address) : NetResult.No("reallife_social_net_why_no_supplier");
        }

        /// <summary>
        /// The day's round: the first person with a favour ready that they have not texted about texts the player to call,
        /// under the social life's limits; one a day at most. One pass over the people rolled.
        /// </summary>
        public static void Daily(int count)
        {
            Sweep(count);
            NetworkFile f = On ? File() : null;
            if (f == null || NetworkBridges.Text == null)
            {
                return;
            }
            for (int i = 0; i < f.Contacts.Count; i++)
            {
                NetContact c = f.Contacts[i];
                if (c.Role == NetRole.None)
                {
                    continue;
                }
                NetFavour favour = Ready(c.Person);
                if (favour == NetFavour.None || c.Said(favour) || !NetworkBridges.Person(c.Person, out NetPerson p))
                {
                    continue;
                }
                if (NetworkBridges.Text(c.Person, NetworkCatalogue.ReadyKey(favour), Data(p, c)))
                {
                    c.Told |= NetContact.Bit(favour);
                    Write();
                    Logger?.Info("Networking: " + p.Name + " texted that they could help: " + favour + ".");
                }
                return;   // (one a day: the first ready waits for tomorrow if the texts were capped)
            }
        }

        /// <summary>The People page's lines about somebody: their position and what they could do next, or null.</summary>
        public static string PeopleLine(int person)
        {
            NetContact c = Of(person);
            if (c == null || c.Role == NetRole.None)
            {
                return null;
            }
            NetFavour next = NetFavour.None;
            for (int step = 0; step < 2 && next == NetFavour.None; step++)
            {
                NetFavour f = NetworkRules.FavourOf(c.Role, step);
                next = c.Has(f) ? NetFavour.None : f;
            }
            var data = new Dictionary<string, string> { { "position", Position(person) }, { "favour", next != NetFavour.None ? NetworkGame.T(NetworkCatalogue.NameKey(next)) : string.Empty } };
            return NetworkGame.T(NetworkCatalogue.PeopleLine, data) + "\n" + NetworkGame.T(next != NetFavour.None ? NetworkCatalogue.PeopleNext : NetworkCatalogue.PeopleDone, data);
        }

        /// <summary>The words of a favour's answer: the done line, or why not.</summary>
        public static string AnswerKey(NetFavour favour, NetResult r)
        {
            if (!r.Done)
            {
                return r.Why ?? "reallife_social_net_why_not_now";
            }
            return favour == NetFavour.Property && !NetworkGame.TipLowers ? "reallife_social_net_done_property_plain" : NetworkCatalogue.DoneKey(favour);
        }

        /// <summary>A line's values: their name, where they work, the position, and the tipped building's share of the price.</summary>
        public static Dictionary<string, string> Data(NetPerson p, NetContact c)
        {
            string first = p.Name ?? string.Empty;
            int space = first.IndexOf(' ');
            return new Dictionary<string, string>
            {
                { "name", p.Name ?? string.Empty }, { "first", space > 0 ? first.Substring(0, space) : first }, { "org", OrgName(c) },
                { "position", c != null && c.Role != NetRole.None ? NetworkGame.T(NetworkCatalogue.RoleKey(c.Role), new Dictionary<string, string> { { "org", OrgName(c) } }) : string.Empty },
                { "percent", NetworkGame.TipPercent.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            };
        }

        private static string OrgName(NetContact c)
        {
            if (c == null || string.IsNullOrEmpty(c.Org))
            {
                return string.Empty;
            }
            return c.Org.StartsWith("reallife_", System.StringComparison.Ordinal) ? NetworkGame.T(c.Org) : c.Org;
        }

        // With Developer diagnostics on, or for a test button, one line an event: what happened against what was wanted, then PASS or FAIL.
        private static void Check(string what, string detail, bool ok, bool test = false)
        {
            if (test)
            {
                Logger?.Warn("Networking check (" + what + ", the test button): " + detail + ": " + (ok ? "PASS" : "FAIL"));
            }
            else if (Meta.Dev.Verbose)
            {
                Logger?.Info("Networking check (" + what + "): " + detail + ": " + (ok ? "PASS" : "FAIL"));
            }
        }
    }
}
