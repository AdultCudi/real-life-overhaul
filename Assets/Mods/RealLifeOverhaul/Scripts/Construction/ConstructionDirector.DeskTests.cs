using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The tests of the front desks (Options > Tests; the pages' check also in the city expansion's test run). Each works
    /// when pressed, with no searching: the nearest rival builder's office made the destination, the conversation opened
    /// from anywhere, every builder's pages written out without showing one. Each writes one "Construction check (...)" line
    /// ending PASS or FAIL, and is a public static method, so a runner can call it with no press. Nothing is changed.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string DeskCheck = "Construction check (";   // each line goes on with its test's name and ")"

        /// <summary>Test: the nearest rival builder's office made the game's destination; inside one already, its front desk found.</summary>
        public static bool TestDeskGoNow()
        {
            return DeskTest("take me to a builder's desk", d => d.DeskGo("take me to a builder's desk"));
        }

        /// <summary>Test: the conversation at a builder's front desk opened now: the office's the player is in, else the nearest's.</summary>
        public static bool TestDeskTalkNow()
        {
            return DeskTest("talk to the builder at the desk now", d => d.DeskTalk("talk to the builder at the desk now"));
        }

        /// <summary>Test: every builder's desk pages written out without showing one, each quote the app's own.</summary>
        public static bool TestDeskPagesNow()
        {
            return DeskTest("a builder's desk pages", d => d.DeskPages("a builder's desk pages"));
        }

        private static bool DeskTest(string test, Func<ConstructionDirector, bool> work)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, DeskCheck + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(DeskCheck + test + "): the construction game has not read this save yet: FAIL.");
                return false;
            }
            if (!d.Running)
            {
                TestRefusals.Tell(Logger, DeskCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(DeskCheck + test + "): the construction game is off in the options: FAIL.");
                return false;
            }
            try
            {
                return work(d);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private int NearestRival()
        {
            UnityEngine.Vector3 at = ConstructionGame.Player;
            return ConstructionBridges.RivalNearest != null ? ConstructionBridges.RivalNearest(at.x, at.z) : -1;
        }

        private bool DeskGo(string test)
        {
            int here = ConstructionBridges.RivalHere != null ? ConstructionBridges.RivalHere() : -1;
            if (here >= 0)
            {
                if (_deskPointer == null || !_deskPointer.Staffed)
                {
                    _deskAt = BuilderDeskGame.Inside();
                    _deskRival = here;
                    MakeDesk();
                }
                bool staffed = _deskPointer != null && _deskPointer.Staffed;
                Logger?.Warn(DeskCheck + test + "): the player is in " + ConstructionText.T(BuildCatalogue.RivalKeys[here]) + "'s office; "
                             + (staffed ? "its front desk is the workstation '" + _deskPointer.Station.name + "', " + Distance(_deskPointer.Station.transform.position)
                                          + " m away, its person seated: PASS."
                                 : "nobody sits at a desk there: FAIL."));
                if (staffed)
                {
                    Notifications.Show(NotificationType.Info, "reallife_bld_test_desk_here", Data("rival", ConstructionText.T(BuildCatalogue.RivalKeys[here])), 6f);
                }
                return staffed;
            }
            int r = NearestRival();
            string office = r >= 0 && ConstructionBridges.RivalOffice != null ? ConstructionBridges.RivalOffice(r) : null;
            if (office == null)
            {
                return Failed(DeskCheck, test, "reallife_bld_test_desk_none", "no rival builder's office stands at this load (Uptown is not standing, or its offices are not let)");
            }
            bool set = ConstructionBridges.RivalOfficeGo != null && ConstructionBridges.RivalOfficeGo(r);
            if (set)
            {
                Notifications.Show(NotificationType.Info, "reallife_bld_rival_office_set", Data("rival", ConstructionText.T(BuildCatalogue.RivalKeys[r]), "address", office), 6f);
            }
            Logger?.Warn(DeskCheck + test + "): the nearest rival builder's office is " + ConstructionText.T(BuildCatalogue.RivalKeys[r]) + "'s at " + office
                         + (set ? ", now the game's destination; its front desk is the first workstation inside: PASS." : ", but the game's destination could not be set: FAIL."));
            return set;
        }

        private bool DeskTalk(string test)
        {
            if (BuilderDesk.Talking)
            {
                return Failed(DeskCheck, test, "reallife_bld_test_desk_busy", "a conversation is open already");
            }
            int here = ConstructionBridges.RivalHere != null ? ConstructionBridges.RivalHere() : -1;
            int r = here >= 0 ? here : NearestRival();
            r = r >= 0 ? r : 0;   // no office standing: the conversation is the same at any builder's
            ThirdPersonCharacter person = here >= 0 && _deskPointer != null && _deskPointer.Staffed ? _deskPointer.Person : null;
            DeskStep step = DeskStepNow(out int project);
            ProjectFile p = _state.Project(project);
            string quote = string.Empty;
            bool same = true;
            if (step == DeskStep.Ready && p != null)
            {
                bool desk = DeskQuote(p, r, out TenderBid bid);
                var app = new TenderBid[BuildCatalogue.RivalCount];   // a test's press: the app's look is its own
                int count = Quotes(p, app);
                same = desk && r < count && app[r].Price == bid.Price && app[r].Days == bid.Days;
                quote = "; its quote for project " + p.Id + " " + CommissionRules.Price(bid).ToString(CultureInfo.InvariantCulture) + " in " + bid.Days + " day(s)"
                        + (same ? ", the app's own" : ", not the app's");
            }
            bool open = BuilderDesk.Open(r, person) && BuilderDesk.Talking;
            bool pass = open && same;
            string where = here < 0 ? " (from where the player stands)"
                : person != null ? " (the office the player is in, facing its receptionist)" : " (the office the player is in, nobody at its desk)";
            Logger?.Warn(DeskCheck + test + "): " + ConstructionText.T(BuildCatalogue.RivalKeys[r]) + where + ", the conversation " + (open ? "open" : "not opened") + " on " + step + quote
                         + (pass ? ": PASS." : ": FAIL."));
            return pass;
        }

        private bool DeskPages(string test)
        {
            DeskStep step = DeskStepNow(out int project);
            ProjectFile p = _state.Project(project);
            var app = new TenderBid[BuildCatalogue.RivalCount];   // a test's press: the app's look is its own
            int count = p != null ? Quotes(p, app) : 0;
            bool pass = true;
            int quoted = 0;
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                string words = BuilderDesk.Words(r, out bool hasQuote);
                pass &= !string.IsNullOrEmpty(words) && words.Contains(ConstructionText.T(BuildCatalogue.RivalKeys[r]));
                if (hasQuote)
                {
                    quoted++;
                    pass &= DeskQuote(p, r, out TenderBid bid) && r < count && bid.Price == app[r].Price && bid.Days == app[r].Days;
                }
            }
            pass &= step != DeskStep.Ready || quoted == BuildCatalogue.RivalCount;
            Logger?.Warn(DeskCheck + test + "): " + BuildCatalogue.RivalCount + " builders' pages written, the player's building at " + step
                         + (project > 0 ? " (project " + project + ")" : string.Empty) + ", " + quoted + " quote(s) the app's own" + (pass ? ": PASS." : ": FAIL."));
            return pass;
        }

        private static string Distance(UnityEngine.Vector3 at)
        {
            return UnityEngine.Mathf.Sqrt(CitySpots.Flat(at - ConstructionGame.Player)).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, string> Data(string name, string value, string name2 = null, string value2 = null)
        {
            var data = new Dictionary<string, string>(2) { { name, value ?? string.Empty } };
            if (name2 != null)
            {
                data[name2] = value2 ?? string.Empty;
            }
            return data;
        }
    }
}
