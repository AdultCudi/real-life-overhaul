using System.Collections.Generic;

namespace RealLifeOverhaul.Networking
{
    /// <summary>
    /// The networking life's test buttons (Options, Test tools): an introduction of somebody holding a position, here and
    /// now (each press the next position); a call with somebody who holds one, their next favour ready whatever the stage;
    /// and every favour done at once for somebody known. Each writes a "Networking check" line, or says why it cannot.
    /// </summary>
    public static partial class NetworkDirector
    {
        private const string TestName = "Networking";
        private static int _testTurn;

        /// <summary>Set by Wiring/NetworkLinks: a friend brings the next person to be met over, here; the person's index, or -1 when it could not start (the social life said why).</summary>
        public static System.Func<int> TestIntroduce;

        /// <summary>Set by Wiring/NetworkLinks: the phone open on a call with that person; false if it could not be.</summary>
        public static System.Func<int, bool> TestCall;

        private static bool TestReady()
        {
            if (!On)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return false;
            }
            if (File() == null || NetworkBridges.Person == null)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return false;
            }
            return true;
        }

        /// <summary>A friend introduces somebody who holds the next position in turn: met, told, recorded as any introduction.</summary>
        public static void TestIntro()
        {
            if (!TestReady() || TestIntroduce == null)
            {
                return;
            }
            NetRole role = NetworkRules.Roles[_testTurn++ % NetworkRules.Roles.Length];
            int person = TestIntroduce();
            if (person >= 0 && _file.Of(person) == null)
            {
                TestRoles[person] = role;   // (taken when they are met; the conversation says it first)
                Logger?.Info("Networking: the test's introduction is of person " + (person + 1) + ", as " + role + ".");
            }
        }

        /// <summary>A call opened now with somebody who holds a position and has a favour left, ready on this call; their text first.</summary>
        public static void TestFavour()
        {
            if (!TestReady())
            {
                return;
            }
            NetContact c = TestContact(true);
            if (c == null || !NetworkBridges.Person(c.Person, out NetPerson p))
            {
                TestRefusals.TellKey(Logger, TestName, NetworkCatalogue.TestNobody, "nobody met holds a position with a favour left; press the introduction test first");
                return;
            }
            _testReady = c.Person;
            NetFavour favour = Ready(c.Person);
            NetworkBridges.Thread?.Invoke(c.Person, NetworkCatalogue.ReadyKey(favour), Data(p, c));
            bool called = TestCall != null && TestCall(c.Person);
            Check("ready", p.Name + " (" + c.Role + ") has " + favour + " ready on this call, their text in the thread, the call " + (called ? "open" : "not opened"),
                favour != NetFavour.None && called, true);
        }

        /// <summary>Every favour done at once for somebody who holds a position (or anybody met), nothing kept as done: each one's line and result.</summary>
        public static void TestEach()
        {
            if (!TestReady())
            {
                return;
            }
            NetContact c = TestContact(false);
            if (c == null || !NetworkBridges.Person(c.Person, out NetPerson p))
            {
                TestRefusals.TellKey(Logger, TestName, NetworkCatalogue.TestNobody, "nobody met yet");
                return;
            }
            int failed = 0;
            for (int f = (int)NetFavour.JobLead; f <= (int)NetFavour.Property; f++)
            {
                var favour = (NetFavour)f;
                NetResult r = Do(c, p, favour);
                string key = AnswerKey(favour, r);
                Dictionary<string, string> data = Data(p, c);
                data["a"] = r.A ?? string.Empty;
                data["b"] = r.B ?? string.Empty;
                bool ok = r.Done || !string.IsNullOrEmpty(r.Why);
                failed += ok ? 0 : 1;
                Check("each favour", favour + " for " + p.Name + ": " + (r.Done ? "done: " + r.A + ", " + r.B : "refused: " + r.Why) + "; said as " + key, ok, true);
            }
            Check("each favour", "seven favours tried, " + failed + " without a done line or a reason; none kept as done", failed == 0, true);
        }

        // Somebody met who holds a position (with a favour left, when asked for), else for TestEach anybody met but Mom.
        private static NetContact TestContact(bool favourLeft)
        {
            Sweep(NetworkFile.Most);
            NetContact any = null;
            for (int i = 0; i < _file.Contacts.Count; i++)
            {
                NetContact c = _file.Contacts[i];
                if (c.Role != NetRole.None && (!favourLeft || NetworkRules.Next(c, 0, 0, 0, 0, true) != NetFavour.None))
                {
                    return c;
                }
                any = any ?? c;
            }
            return favourLeft ? null : any;
        }
    }
}
