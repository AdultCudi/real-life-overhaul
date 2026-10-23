using System;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>How a motion went at a meeting.</summary>
    public struct MotionVote
    {
        public int Ayes, Noes;
        public bool Passed;
        public bool Decisive;   // the player's vote took it the way it went
    }

    /// <summary>
    /// The player in the city's politics: a run for their district's council seat or for mayor, its count, and an office held.
    /// Rules on <see cref="OfficeState"/> from <see cref="OfficeRules"/>, run through the government's hooks; nothing per frame.
    /// With the option off nothing new begins, but a run filed is still counted and an office lasts until the next count.
    /// </summary>
    public sealed partial class CivicOffice
    {
        // Moments that may be posted about (CivicBridges.CampaignPost).
        public const int PostFiled = 0, PostRally = 1, PostDebate = 2, PostWon = 3, PostDoors = 4, PostMeeting = 5;

        public readonly OfficeState State = new OfficeState();
        private readonly CivicGovernment _gov;
        private readonly CivicCity _city;
        private readonly float[] _weights = new float[CivicCatalogue.Issues];
        private readonly float[] _raceScores = new float[OfficeState.Rivals + 1];
        private readonly float[] _raceShares = new float[OfficeState.Rivals + 1];

        public CivicOffice(CivicGovernment government, CivicCity city)
        {
            _gov = government;
            _city = city;
            government.PlayerStands = Stands;
            government.PlayerTerm = Term;
            government.PlayerLocal = Local;
            government.DistrictDoors = d => d >= 0 && d < State.DistrictDoors.Length ? State.DistrictDoors[d] : 0;
            government.Counted = Counted;
            government.DayBegins = Day;
            government.OfficeSits = Sits;
            government.ToAgenda = (policy, petition) => Put(policy, petition ? OfficeMotion.ByPetition : OfficeMotion.ByCity, TimeHelper.CurrentDay);
            government.ApprovalExtra = ApprovalExtra;
            government.DepartmentLevel = d => Enabled ? State.Budget[(int)d] : 0;   // the budget is the player's only with the option on
            government.OfficeMeeting = () => State.NextMeeting;
            government.ToPlanning = PutZoning;
            government.OnPlanning = (lot, use) => ZoningMotion(lot, use) != null;
            government.DecideNow = DecideZoningNow;
        }

        /// <summary>Running for office is on: civic life, its elections and this option.</summary>
        public static bool Enabled => Settings.Civic && Settings.CivicElections && Settings.CivicOffice;

        private GovState Gov => _gov.State;

        /// <summary>The player is a candidate in the campaign now on.</summary>
        public bool Campaigning => State.Filed && _gov.Campaigning && State.ForDay == Gov.ElectionDay;

        /// <summary>A run begun whose papers are not in yet.</summary>
        public bool Collecting => State.Running && !State.Filed;

        public int Limit => Prices.At(OfficeRules.DonorLimit(State.Race));

        private static int Recognition => CivicBridges.Ask(CivicBridges.Recognition, 0, 0, 100);

        private static int Integrity => CivicBridges.Ask(CivicBridges.Integrity, 50, 0, 100);

        private static int Influence => CivicBridges.Ask(CivicBridges.Influence, 0, 0, 100);

        /// <summary>The district of the player's home (the first home they rent or own), its place in the city's list; -1 for none.</summary>
        public int HomeDistrict() => DistrictIndex(HomeDistrictId());

        public string HomeDistrictId()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return null;
            }
            for (int i = 0; i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration r = save.BuildingRegistrations[i];
                try
                {
                    if (r != null && GoingPublic.Mine(r) && r.GetBuildingType() == "ba:buildingtype_residential")
                    {
                        return r.Neighborhood;
                    }
                }
                catch (Exception)
                {
                    // a registration the game has no building for is nobody's home
                }
            }
            return null;
        }

        public int DistrictIndex(string id)
        {
            for (int i = 0; id != null && i < _city.Districts.Count && i < CivicCatalogue.MaxDistricts; i++)
            {
                if (_city.Districts[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool IsMine(CityBuildingController building)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null || building == null || building.building == null || building.building.Address == null)
            {
                return false;
            }
            for (int i = 0; i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration r = save.BuildingRegistrations[i];
                if (r != null && r.Address != null && GoingPublic.Mine(r)
                    && r.Address.streetName == building.building.Address.streetName && r.Address.streetNumber == building.building.Address.streetNumber)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The player's businesses in a district (-1: in the city).</summary>
        public int Businesses(int district)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return 0;
            }
            string id = district >= 0 && district < _city.Districts.Count ? _city.Districts[district].Id : null;
            int count = 0;
            for (int i = 0; i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration r = save.BuildingRegistrations[i];
                try
                {
                    if (r != null && GoingPublic.Mine(r) && r.HasEstablishedBusiness && (district < 0 || r.Neighborhood == id))
                    {
                        count++;
                    }
                }
                catch (Exception)
                {
                    // as above
                }
            }
            return count;
        }

        /// <summary>The district the run is for (the council's), else the player's home district; -1 for none.</summary>
        public int RaceDistrict() => State.Race == OfficeRace.Council ? DistrictIndex(State.District) : HomeDistrict();

        /// <summary>Where the run is: the council seat's district, or the city for mayor.</summary>
        public string Where() => DistrictName(State.Race == OfficeRace.Council ? RaceDistrict() : -1);

        public string DistrictName(int district)
        {
            return district >= 0 && district < _city.Districts.Count ? CivicCity.Name(_city.Districts[district]) : CivicText.T("reallife_civic_office_the_city");
        }

        public OfficeRefusal CanStand(int day)
        {
            bool open = OfficeRules.NominationsOpenOn(day, Gov.ElectionDay, _gov.Campaigning, CivicCatalogue.CampaignDays) && Gov.Phase != ElectionPhase.Counted;
            return OfficeRules.MayStand(HomeDistrictId() != null, CivicBridges.Ask(CivicBridges.Convicted), CivicBridges.Ask(CivicBridges.CaseOpen),
                GoingPublic.InSession, open, State.Running);
        }

        /// <summary>A run begins: the race, the district, the platform still to choose. Returns the locale key of the answer.</summary>
        public string Stand(OfficeRace race, int day)
        {
            OfficeRefusal why = CanStand(day);
            if (why != OfficeRefusal.None)
            {
                return RefusalKey(why);
            }
            State.ClearRun();
            State.Race = race;
            State.ForDay = Gov.ElectionDay;
            State.District = HomeDistrictId();
            State.Incumbent = State.Holds == race;
            Candidate me = State.Me;
            me.Player = true;
            me.Party = CivicCatalogue.Independent;
            me.Planks = 0;
            if (State.Incumbent)
            {
                me.CopyFrom(race == OfficeRace.Mayor ? Gov.Mayor : State.Held);   // standing again on the record
                me.Player = true;
                me.Incumbent = true;
            }
            var random = new CivicRandom(CivicRandom.Mix(Gov.Seed, Gov.Number + 1, 919));
            me.First = random.Range(0, CivicCatalogue.FirstNames.Length);   // never shown for the player, but a guest of a session sees them
            me.Last = random.Range(0, CivicCatalogue.LastNames.Length);
            me.Look = (int)(random.Next() & 0x7FFFFFFF);
            Lead();
            CivicDirector.Logger?.Info("Civic office: a run for " + race + " in the election of day " + State.ForDay + (race == OfficeRace.Council ? " in " + State.District : string.Empty) + ".");
            return "reallife_civic_office_stand_begun";
        }

        public static string RefusalKey(OfficeRefusal why)
        {
            switch (why)
            {
                case OfficeRefusal.NoHome: return "reallife_civic_office_no_home";
                case OfficeRefusal.Conviction: return "reallife_civic_office_no_conviction";
                case OfficeRefusal.CaseOpen: return "reallife_civic_office_no_case";
                case OfficeRefusal.Closed: return "reallife_civic_office_no_window";
                case OfficeRefusal.Standing: return "reallife_civic_office_no_already";
                case OfficeRefusal.Session: return "reallife_civic_office_no_session";
                case OfficeRefusal.NoPlatform: return "reallife_civic_office_no_platform";
                case OfficeRefusal.PartyLine: return "reallife_civic_office_no_partyline";
                case OfficeRefusal.Requirements: return "reallife_civic_office_no_signatures";
                case OfficeRefusal.Broke: return "reallife_civic_office_no_money";
                default: return "reallife_civic_office_ok";
            }
        }

        /// <summary>A plank in or out of the platform (one a lever, four at most). False if it cannot be added.</summary>
        public bool Toggle(int policy)
        {
            Candidate me = State.Me;
            PolicyDef def = CivicCatalogue.At(policy);
            if (!Collecting || def == null)
            {
                return false;
            }
            for (int i = 0; i < me.Planks; i++)
            {
                if (me.Platform[i] == policy)
                {
                    for (int j = i; j < me.Planks - 1; j++)
                    {
                        me.Platform[j] = me.Platform[j + 1];
                    }
                    me.Planks--;
                    me.Platform[me.Planks] = -1;
                    Lead();
                    return true;
                }
            }
            if (me.Planks >= me.Platform.Length)
            {
                return false;
            }
            for (int i = 0; i < me.Planks; i++)
            {
                PolicyDef other = CivicCatalogue.At(me.Platform[i]);
                if (other != null && other.Lever == def.Lever)
                {
                    me.Platform[i] = policy;   // the other way on the same lever: it takes that plank's place
                    Lead();
                    return true;
                }
            }
            me.Platform[me.Planks++] = policy;
            Lead();
            return true;
        }

        public bool InPlatform(int policy) => CivicGovernment.Promised(State.Me, policy);

        public void SetParty(int party)
        {
            if (Collecting)
            {
                State.Me.Party = party >= 0 && party < CivicCatalogue.Parties.Length ? party : CivicCatalogue.Independent;
            }
        }

        public void NextSlogan()
        {
            Candidate me = State.Me;
            me.Slogan = me.Lead * 3 + (me.Slogan % 3 + 1) % 3;
        }

        // The issue the most planks speak to is the one the player leads on; the slogan follows it.
        private void Lead()
        {
            Candidate me = State.Me;
            int best = me.Lead, bestCount = 0;
            for (int issue = 0; issue < CivicCatalogue.Issues; issue++)
            {
                int count = 0;
                for (int i = 0; i < me.Planks; i++)
                {
                    PolicyDef p = CivicCatalogue.At(me.Platform[i]);
                    count += p != null && (int)p.Issue == issue ? 1 : 0;
                }
                if (count > bestCount)
                {
                    best = issue;
                    bestCount = count;
                }
            }
            if (best != me.Lead || me.Slogan / 3 != best)
            {
                me.Lead = best;
                me.Slogan = best * 3 + me.Slogan % 3;
            }
        }

        /// <summary>The businesses' sheets and those who know of the player: names on the papers, once a run. Returns the names added.</summary>
        public int Sheets()
        {
            if (!Collecting || State.Sheets)
            {
                return 0;
            }
            State.Sheets = true;
            int names = OfficeRules.SheetSignatures(Businesses(-1), Recognition);
            State.Signatures += names;
            return names;
        }

        /// <summary>The papers go in, with the signatures or the deposit. Returns the locale key of the answer.</summary>
        public string File(bool deposit, int day)
        {
            if (!Collecting)
            {
                return "reallife_civic_office_no_already";
            }
            if (!OfficeRules.NominationsOpenOn(day, State.ForDay, _gov.Campaigning, CivicCatalogue.CampaignDays) || State.ForDay != Gov.ElectionDay)
            {
                return "reallife_civic_office_no_window";
            }
            if (CivicBridges.Ask(CivicBridges.Convicted))
            {
                return "reallife_civic_office_no_conviction";
            }
            if (CivicBridges.Ask(CivicBridges.CaseOpen))
            {
                return "reallife_civic_office_no_case";
            }
            int price = Prices.At(OfficeRules.Deposit(State.Race));
            bool needDeposit = !State.Incumbent && State.Signatures < OfficeRules.SignaturesNeeded(State.Race);
            OfficeRefusal why = OfficeRules.MayFile(State.Me, State.Race, State.Signatures, State.Incumbent, deposit, true);   // the payment refuses itself if it cannot be made
            if (why != OfficeRefusal.None)
            {
                return RefusalKey(why);
            }
            if (needDeposit)
            {
                if (!GameManager.ChangeMoneySafe(-price, new TransactionInfo("reallife_transaction_civic_office_deposit"), null, null, force: false, showNotification: true))
                {
                    return "reallife_civic_office_no_money";
                }
                State.Deposit = true;
            }
            State.Filed = true;
            int grant = Prices.At(OfficeRules.PartyGrant(State.Race, State.Me.Party));
            if (grant > 0)
            {
                State.Fund += grant;
                State.Raised += grant;
            }
            if (State.Race == OfficeRace.Council)
            {
                Rivals();
            }
            else if (_gov.Campaigning)
            {
                _gov.JoinRace(State.Me);
            }
            State.Story(day + 1, State.Race == OfficeRace.Mayor ? "reallife_civic_news_office_filed_mayor" : "reallife_civic_news_office_filed_council", CivicText.PlayerName(), Where());
            Post(PostFiled, Where());
            Logger("papers filed for " + State.Race + (State.Deposit ? " with the deposit" : " with " + State.Signatures + " signatures") + "; the party's grant " + grant + ".");
            return "reallife_civic_office_filed";
        }

        /// <summary>A run whose papers are not in is given up (the deposit is only paid on filing). Returns the locale key.</summary>
        public string Withdraw()
        {
            if (!Collecting)
            {
                return "reallife_civic_office_cannot_withdraw";
            }
            State.ClearRun();
            return "reallife_civic_office_withdrawn";
        }

        // The district's council race: two rivals of other parties than the player's, from the city's pools.
        private void Rivals()
        {
            var random = new CivicRandom(CivicRandom.Mix(Gov.Seed, State.ForDay, 4409 + Math.Max(0, RaceDistrict())));
            int usedParties = State.Me.Party < CivicCatalogue.Parties.Length ? 1 << State.Me.Party : 0, usedLeads = 0;
            State.RivalCount = OfficeState.Rivals;
            for (int i = 0; i < State.RivalCount; i++)
            {
                Candidate rival = State.Rival[i];
                CivicGovernment.Generate(rival, ref random, usedParties, usedLeads);
                rival.Player = false;
                rival.Incumbent = false;
                usedParties |= 1 << rival.Party;
                usedLeads |= 1 << rival.Lead;
                State.RivalPush[i] = 0f;
                State.RivalSpend[i] = 0;
            }
            if (!State.Incumbent && State.Holds != OfficeRace.Council)
            {
                State.Rival[0].Incumbent = true;   // the seat's holder, of the mayor's party, defends it
                State.Rival[0].Party = Gov.Mayor.Party < CivicCatalogue.Parties.Length && Gov.Mayor.Party != State.Me.Party ? Gov.Mayor.Party : State.Rival[0].Party;
            }
            RaceTargets();
            for (int i = 0; i <= State.RivalCount; i++)
            {
                State.RacePolls[i] = _raceShares[i];
            }
        }

        // A campaign begins: the player's candidate if they have filed for mayor (or, as the mayor, must answer a recall).
        private Candidate Stands(bool recall)
        {
            if (recall && Gov.MayorIsPlayer && Enabled && !State.Running)
            {
                // A recall is answered by the mayor: the papers are in at once, on the record.
                State.ClearRun();
                State.Race = OfficeRace.Mayor;
                State.ForDay = Gov.ElectionDay;
                State.District = HomeDistrictId();
                State.Incumbent = true;
                State.Me.CopyFrom(Gov.Mayor);
                State.Me.Player = true;
                State.Filed = true;
                Logger("the recall is answered: the player stands again as mayor.");
                return State.Me;
            }
            if (State.Filed && State.Race == OfficeRace.Mayor && State.ForDay == Gov.ElectionDay)
            {
                return State.Me;
            }
            if (State.Filed && State.ForDay != Gov.ElectionDay)
            {
                State.ForDay = Gov.ElectionDay;   // the calendar moved (a recall brought the election forward): the papers stand
                return State.Race == OfficeRace.Mayor ? State.Me : null;
            }
            return null;
        }

        // The player's own part of a score in a district (-1 the city).
        private float Term(int district)
        {
            float t = OfficeRules.StandingTerm(Recognition, CivicBridges.Standing(), Integrity);
            t += OfficeRules.SpendingTerm(State.Spent, RivalSpending(), Prices.At(1000));
            if (!Gov.MayorIsPlayer)
            {
                t += OfficeRules.OutsiderTerm(State.Me.Party >= CivicCatalogue.Parties.Length, Gov.Approval);
            }
            if (State.Me.Party < CivicCatalogue.Parties.Length)
            {
                t += 0.08f;   // a party's members on the doors and its name on the ballot
            }
            t += State.Push;
            if (district >= 0 && district < State.DistrictPush.Length)
            {
                t += State.DistrictPush[district];
            }
            else
            {
                t += AveragePush();
            }
            if (!State.Incumbent)
            {
                t += OfficeRules.EconomyTerm(CivicBridges.Cycle(), false);
            }
            return t;
        }

        private float AveragePush()
        {
            int n = Math.Min(_city.Districts.Count, CivicCatalogue.MaxDistricts);
            if (n <= 0)
            {
                return 0f;
            }
            float sum = 0f;
            for (int d = 0; d < n; d++)
            {
                sum += State.DistrictPush[d];
            }
            return sum / n;
        }

        // What the player has of their own in a district at the count, against the city: home, businesses, doors and posters.
        private float Local(int district)
        {
            int home = HomeDistrict();
            float push = district >= 0 && district < State.DistrictPush.Length ? State.DistrictPush[district] - AveragePush() : 0f;
            return OfficeRules.LocalTerm(home == district, Businesses(district), push);
        }

        private bool Sits() => Enabled && State.InOffice;

        private float ApprovalExtra()
        {
            if (!State.ReserveSet || !Enabled)
            {
                return 0f;
            }
            _gov.Weights(_weights);
            BudgetWeek week = OfficeRules.Week(_gov.Electorate(), Gov.Levers, State.Budget);
            return OfficeRules.BudgetApproval(State.Budget, _weights, State.Reserve, week.Revenue) * 0.8f;
        }

        private void Day(int day)
        {
            Budget(day);
            Discoveries(day);
            if (!Enabled)
            {
                return;
            }
            if (Collecting && day > OfficeRules.NominationsClose(State.ForDay, CivicCatalogue.CampaignDays))
            {
                CivicText.Toast(NotificationType.Warning, "reallife_civic_office_lapsed", 12f);
                State.ClearRun();
            }
            if (Campaigning)
            {
                CampaignDay(day);
            }
            if (State.InOffice)
            {
                OfficeDay(day);
            }
        }

        private static void Post(int what, string name)
        {
            try
            {
                CivicBridges.CampaignPost?.Invoke(what, name);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }

        private static void Logger(string line)
        {
            CivicDirector.Logger?.Info("Civic office: " + line);
        }

        /// <summary>Test button: the player stands for a race now and the papers are filed at once (a platform made for them if none), whatever the calendar.</summary>
        public string TestFile(OfficeRace race, int day)
        {
            if (!_gov.Campaigning)
            {
                _gov.TestCampaign(day);
            }
            State.ClearRun();
            State.Race = race;
            State.ForDay = Gov.ElectionDay;
            State.District = HomeDistrictId() ?? (_city.Districts.Count > 0 ? _city.Districts[0].Id : null);
            State.Incumbent = State.Holds == race;
            var random = new CivicRandom(CivicRandom.Mix(Gov.Seed, day, 977));
            CivicGovernment.Generate(State.Me, ref random, 0, 0);
            State.Me.Player = true;
            State.Signatures = OfficeRules.SignaturesNeeded(race);
            State.Filed = true;
            State.Fund += Prices.At(2000);
            State.Raised += Prices.At(2000);
            if (race == OfficeRace.Council)
            {
                Rivals();
            }
            else
            {
                _gov.JoinRace(State.Me);
            }
            return "reallife_civic_office_filed";
        }

        public void TestOffice(OfficeRace race, int day)
        {
            if (State.Running)
            {
                State.ClearRun();
            }
            var random = new CivicRandom(CivicRandom.Mix(Gov.Seed, day, 983));
            CivicGovernment.Generate(State.Me, ref random, 0, 0);
            State.Me.Player = true;
            TakeOffice(race, 0.45f, day - 1);
            State.NextMeeting = day;
            if (race == OfficeRace.Mayor)
            {
                Gov.Mayor.CopyFrom(State.Me);
                Gov.Mayor.Player = true;
                Gov.Mayor.Incumbent = true;
                Gov.MayorIsPlayer = true;
                Gov.TermStart = day;
            }
            Prepare(day);
            State.ClearRun();
        }
    }
}
