using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>What is said when Alex or Mom, the two fixed characters (Phone/Contacts/FixedPeople), were put right in an older save.</summary>
    public sealed partial class SocialDirector
    {
        private static readonly List<string> MendedLines = new List<string>(4);
        private static readonly List<string> MendedNames = new List<string>(4);

        /// <summary>One log line for each person put right and one notice for each, once: a record made right stays right, so the next load says nothing. Runs only when a repair waits.</summary>
        private void TellFixedPeople()
        {
            FixedPeople.Take(MendedLines, MendedNames);
            for (int i = 0; i < MendedLines.Count; i++)
            {
                Logger?.Info(MendedLines[i]);
            }
            for (int i = 0; i < MendedNames.Count; i++)
            {
                SocialLife.Toast(NotificationType.Info, "reallife_social_looks_again", new Dictionary<string, string>(1) { { "name", MendedNames[i] } }, 10f);
            }
        }

        /// <summary>
        /// Options, Tests: Alex and Mom, the fixed characters, in every way the mod shows a person: at a gathering (the record
        /// SocialCast dresses from), at home (the roommate's copy, here one made wrong on purpose and mended as a load mends it), on
        /// the phone (the contact's picture). The wedding's Mom is read from the same constants (a source check holds that). Each must be the one look of
        /// FixedPeople. The log line `Social check (one look per person)` says PASS or FAIL for each, and a notice says the same.
        /// </summary>
        public void TestOneLook()
        {
            const string test = "One look for Alex and Mom";
            if (!TestReady(test, false))
            {
                return;
            }
            SocialPerson alex = SocialState.ByOrigin(SocialOrigin.OldFriend);
            SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
            if (alex == null || mom == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_social_test_looks_none", "the cast has no old friend or no Mom");
                return;
            }
            bool all = true;
            var line = new System.Text.StringBuilder(320);
            line.Append("Social check (one look per person):");
            for (int who = 0; who < 2; who++)
            {
                SocialPerson record = who == 0 ? mom : alex;   // the cast's places 0 and 1
                FixedPeople.TryGet(who, out FixedPeople.Entry want);
                SocialCatalogue.FixedFor(record, "the test");   // a gathering dresses them from this
                var home = new SocialPerson { Index = record.Index, Origin = SocialOrigin.Neighbour, Name = record.Name, Female = !record.Female, Age = record.Age + 9, Seed = record.Seed + 1 };
                Roommates.RoommateRules.Mend(new Roommates.Flatmate { Person = home, Friend = record.Index }, record);   // the home: a copy as an old save held it
                bool phone = ContactPictures.TryLook(SocialPhone.IdOf(record), out bool phoneFemale, out int phoneAge, out int phoneSeed);
                bool gathering = record.Female == want.Female && record.Age == want.Age && record.Seed == want.Seed;
                bool house = home.Female == want.Female && home.Age == want.Age && home.Seed == want.Seed;
                bool picture = !phone || (phoneFemale == want.Female && phoneAge == want.Age && phoneSeed == want.Seed);   // (no picture yet: nothing to differ)
                bool ok = gathering && house && picture;
                all &= ok;
                line.Append(' ').Append(record.Name).Append(": ").Append(want.Female ? "a woman" : "a man").Append(" of ").Append(want.Age).Append(", seed ").Append(want.Seed)
                    .Append("; gathering ").Append(gathering ? "same" : "DIFFERENT").Append(", home ").Append(house ? "same" : "DIFFERENT")
                    .Append(", phone ").Append(!phone ? "not drawn yet" : picture ? "same" : "DIFFERENT");
                line.Append(ok ? ": PASS." : ": FAIL.");
            }
            Logger?.Info(line.ToString());
            string result = SocialLife.T(all ? "reallife_social_test_looks_pass" : "reallife_social_test_looks_fail");
            var data = new Dictionary<string, string>(2) { { "name", alex.Name + " and " + mom.Name }, { "result", result } };
            SocialLife.Toast(all ? NotificationType.Success : NotificationType.Error, "reallife_social_test_looks_done", data, 10f);
        }
    }
}
