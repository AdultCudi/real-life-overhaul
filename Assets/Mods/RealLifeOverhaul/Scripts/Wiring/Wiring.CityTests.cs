using System;
using RealLifeOverhaul.Construction;
using UI.Notification;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The city around the construction company, tested: each a public static method a runner can call without the
        /// interface, as the options page's buttons do. Each works from anywhere at any time and brings forward what it needs,
        /// writes one "... check (...)" line with PASS or FAIL in Player.log and returns whether it passed. The two of the
        /// city's government put it back as it was before they return; the construction game's go back with its own "put
        /// everything back as before the tests" (which takes back the court's cases and claims they filed too).
        /// </summary>
        public static class CityTests
        {
            private static ConstructionDirector Builders => ConstructionDirector.Instance;

            private static bool Fail(string feature, string test, string why)
            {
                Logger?.Info(feature + " check (" + test + "): " + why + ": FAIL.");
                return false;
            }

            // The city's government while its elections run, on the machine that runs it (a Going Public guest follows the host's).
            private static CivicDirector Civic(string test)
            {
                CivicDirector civic = CivicDirector.Instance;
                if (civic == null || !civic.Running || !Settings.CivicElections || civic.Government == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                    return null;
                }
                if (SharedCity.Follows('G'))
                {
                    TestRefusals.TellKey(Logger, test, "reallife_bld_test_host");
                    return null;
                }
                return civic;
            }

            /// <summary>
            /// Test: the building code under each way of the permits policy (fast-track, strict, as the city came), as the
            /// construction game reads it from the elected government: the new district's storeys, the parking asked, the
            /// planning fee and the surcharge on land kept empty; and the new district's voters leaning to growth. The policy
            /// is put back as it stood.
            /// </summary>
            public static bool Code()
            {
                const string test = "building code";
                CivicDirector civic = Civic(test);
                if (civic == null)
                {
                    return Fail("Civic", test, "not started, the city's elections are off or the host's");
                }
                if (Builders == null || !Builders.Running)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                    return Fail("Civic", test, "not started, the construction game is off");
                }
                try
                {
                    CivicGovernment g = civic.Government;
                    int was = g.State.Levers[(int)Lever.Permits];
                    bool pass = true;
                    var line = new System.Text.StringBuilder(256);
                    int[] ways = { 1, -1, 0 };
                    for (int i = 0; i < ways.Length; i++)
                    {
                        g.Set(Lever.Permits, ways[i], false);
                        g.LeversChanged?.Invoke();
                        int code = ConstructionDirector.Code;
                        int storeys = PlansRules.HeightLimit(null, true, code), parking = PlansRules.ParkingNeeded(BuildUse.Homes, 1200, code);
                        long fee = PlansRules.PlanningFee(1000000, 1000, code);
                        int vacant = LandRules.VacantTax(code);
                        bool right = code == ways[i] && storeys == CivicCatalogue.UptownStoreys(ways[i])
                                     && parking == 1200 / BuildCatalogue.ParkingPerSpace[0] * CivicCatalogue.ParkingPermille(ways[i]) / 1000
                                     && fee == PlansRules.PlanningFee(1000000, 1000) * CivicCatalogue.PlanningFeePermille(ways[i]) / 1000
                                     && vacant == BuildCatalogue.VacantTaxPermille * CivicCatalogue.VacantPermille(ways[i]) / 1000;
                        pass &= right;
                        line.Append(i > 0 ? "; " : string.Empty).Append(ways[i]).Append(": ").Append(storeys).Append(" storeys, ").Append(parking).Append(" spaces, fee ").Append(fee)
                            .Append(", empty land ").Append(vacant).Append(" in 1000").Append(right ? string.Empty : " (not as the policy sets it)");
                    }
                    g.Set(Lever.Permits, was, false);
                    g.LeversChanged?.Invoke();
                    int uptown = civic.Office != null ? civic.Office.DistrictIndex(Uptown.DistrictValues.Id) : -1;
                    float lean = uptown >= 0 ? g.DistrictLean(uptown, (int)Issue.Growth) : CouncilZoning.NewDistrictGrowth;
                    pass &= ConstructionDirector.Code == was && lean >= CouncilZoning.NewDistrictGrowth;
                    Logger?.Info("Civic check (building code): " + line + "; the policy back at " + was + "; the new district's care for growth " + lean.ToString("0.00",
                        System.Globalization.CultureInfo.InvariantCulture) + (uptown >= 0 ? string.Empty : " (not a ward at this load)") + ": " + (pass ? "PASS" : "FAIL") + ".");
                    CivicText.Toast(pass ? NotificationType.Success : NotificationType.Warning, "reallife_civic_test_code", "code", CivicText.Code(1), 12f);
                    return pass;
                }
                catch (Exception e)
                {
                    Fault("CityTests.Code", e);
                    return Fail("Civic", test, "a fault (" + e.Message + ")");
                }
            }

            /// <summary>Test: a zoning application heard by the city's one council now (the construction game's test, through Civic's council).</summary>
            public static bool Zoning()
            {
                ConstructionDirector d = Builders;
                return d != null ? d.TestCouncilZoning() : Fail("Construction", "council zoning", "not started, the construction game is not there");
            }

            /// <summary>
            /// Test: the player on the council now with an interest in the building code (a construction company, founded for
            /// the test if there is none): they table the permits motion and a change of the new district's zoning, and the
            /// meeting is held at once, the player voting for both without declaring the interest. The votes are decided and the
            /// undeclared interest kept as a secret that can come out; then the city's government and the player's office are
            /// put back as they were.
            /// </summary>
            public static bool Office()
            {
                const string test = "council motions";
                CivicDirector civic = Civic(test);
                if (civic == null || civic.Office == null || !CivicOffice.Enabled)
                {
                    return Fail("Civic", test, "not started, running for office is off or the host's");
                }
                if (GoingPublic.InSession)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_civic_office_no_session");
                    return Fail("Civic", test, "not started, running for office is not offered in a Going Public session");
                }
                try
                {
                    CivicGovernment g = civic.Government;
                    CivicOffice o = civic.Office;
                    string govBefore = g.State.Write(), officeBefore = o.State.Write();
                    int day = TimeHelper.CurrentDay;
                    bool interest = Builders != null && Builders.TestInterest();
                    o.TestOffice(OfficeRace.Council, day);
                    int permitsWay = g.State.Levers[(int)Lever.Permits] > 0 ? -1 : 1;
                    int secrets = o.State.Secrets.Count;
                    // A councillor tables one motion a meeting: the permits motion at today's, the district's zoning at the next, held at once.
                    string tabled = o.Table(CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.Permits, permitsWay)), true, day);
                    int passed = o.TestMeeting(day, out int decided);
                    int use = -1;
                    for (int u = 0; u < ZoningBridges.Uses && use < 0; u++)
                    {
                        use = o.MayRezone(0, u, true, ZoningBridges.FirstZones(0)) ? u : -1;
                    }
                    bool district = ZoningBridges.DistrictLives() && use >= 0;
                    int classBefore = ZoningBridges.Zones(0);
                    string zoning = district ? o.TableZoning(0, use, true, false, day) : "no district";
                    if (district)
                    {
                        passed += o.TestMeeting(day, out int more);
                        decided += more;
                    }
                    int secretsAfter = o.State.Secrets.Count;
                    int classAfter = ZoningBridges.Zones(0);
                    bool rezoned = district && (classAfter & (1 << use)) != 0 && (classBefore & (1 << use)) == 0;
                    bool pass = tabled == "reallife_civic_office_tabled" && decided >= 1 && (!interest || secretsAfter > secrets);
                    if (rezoned)
                    {
                        ZoningBridges.TellRezoned(0, use, false);   // the district's zoning as it was: the construction game's put back has it too
                    }
                    g.State.Read(govBefore);
                    o.State.Read(officeBefore);
                    g.LeversChanged?.Invoke();
                    Logger?.Info("Civic check (council motions): the permits motion " + tabled + ", the district's zoning " + zoning + "; " + decided + " motion(s) decided at the meeting, " + passed
                                 + " carried" + (rezoned ? " (the small lots' zoning changed, and put back)" : string.Empty) + "; an interest " + (interest ? "kept as " + (secretsAfter - secrets)
                                     + " secret(s)" : "not held") + "; the government and the office put back: " + (pass ? "PASS" : "FAIL") + ".");
                    CivicText.Toast(pass ? NotificationType.Success : NotificationType.Warning, "reallife_civic_test_motions", "count", decided.ToString(), "passed", passed.ToString(), 12f);
                    return pass;
                }
                catch (Exception e)
                {
                    Fault("CityTests.Office", e);
                    return Fail("Civic", test, "a fault (" + e.Message + ")");
                }
            }

            /// <summary>Test: two rigged tenders, one covered for and found out, one reported (the construction game's test).</summary>
            public static bool Ring()
            {
                ConstructionDirector d = Builders;
                return d != null ? d.TestRing() : Fail("Construction", "ring", "not started, the construction game is not there");
            }

            /// <summary>Test: a client's unpaid bill chased and taken to court, and a crew's claim for wages left unpaid (the construction game's test).</summary>
            public static bool Bills()
            {
                ConstructionDirector d = Builders;
                return d != null ? d.TestBills() : Fail("Construction", "bills", "not started, the construction game is not there");
            }

            /// <summary>Test: the City Ledger's building of the year shortlisted, voted on and awarded now (the construction game's test).</summary>
            public static bool YearAward()
            {
                ConstructionDirector d = Builders;
                return d != null ? d.TestYearAward() : Fail("Construction", "building of the year", "not started, the construction game is not there");
            }
        }
    }
}
