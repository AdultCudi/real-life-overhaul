using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

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
    /// The player in the city's politics: standing for the council seat of their home's district or for mayor, the run
    /// (papers, platform, party, the fund and the law on donations, posters, doors, rallies, adverts, endorsements, attacks
    /// to answer, the debate), the count of the district's race beside the city's, and an office held (salary, the
    /// council's weekly meetings and their agenda, the city's budget, the register of interests, conflicts of interest
    /// and what comes of not declaring one, approval, recall, standing again). All of it is rules on <see cref="OfficeState"/>
    /// (OfficeRules: game-free), run by the government's own clock: its day and its count call in here
    /// (<see cref="CivicGovernment"/>'s hooks); what is seen is the City Hall contact's pages (OfficePhone), the scenes
    /// (OfficeScenes) and the street (CampaignWorld). Nothing here runs per frame.
    ///
    /// While the option is off, nothing new begins and nothing is offered; a candidacy already filed stays on the ballot
    /// and is counted like any other, and an office held keeps its title until the next count while the council votes
    /// by itself as before.
    /// </summary>
    public sealed class CivicOffice
    {
        // What a moment of the campaign or the office may be posted about (CivicBridges.CampaignPost).
        public const int PostFiled = 0, PostRally = 1, PostDebate = 2, PostWon = 3, PostDoors = 4, PostMeeting = 5;

        public readonly OfficeState State = new OfficeState();
        private readonly CivicGovernment _gov;
        private readonly CivicCity _city;
        private readonly float[] _weights = new float[CivicCatalogue.Issues];
        private readonly float[] _raceScores = new float[OfficeState.Rivals + 1];
        private readonly float[] _raceShares = new float[OfficeState.Rivals + 1];
        private readonly HashSet<int> _knocked = new HashSet<int>();
        private int _knockedDay = -1;
        private const int KnockedMost = 200;

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
            government.DepartmentLevel = d => Enabled ? State.Budget[(int)d] : 0;   // (the budget is the player's to see and set only with the option on)
        }

        /// <summary>Running for office is on: civic life, its elections and this option.</summary>
        public static bool Enabled => Settings.Civic && Settings.CivicElections && Settings.CivicOffice;

        private GovState Gov => _gov.State;

        /// <summary>The player is a candidate in the campaign now on.</summary>
        public bool Campaigning => State.Filed && _gov.Campaigning && State.ForDay == Gov.ElectionDay;

        /// <summary>A run begun whose papers are not in yet.</summary>
        public bool Collecting => State.Running && !State.Filed;

        public int Limit => Prices.At(OfficeRules.DonorLimit(State.Race));

        // ---------------------------------------------------------------- what the city knows of the player

        private static int Recognition => CivicBridges.Ask(CivicBridges.Recognition, 0, 0, 100);

        private static int Integrity => CivicBridges.Ask(CivicBridges.Integrity, 50, 0, 100);

        private static int Influence => CivicBridges.Ask(CivicBridges.Influence, 0, 0, 100);

        /// <summary>The district of the player's home (the first home they rent or own), its place in the city's list; -1 for none.</summary>
        public int HomeDistrict()
        {
            string id = HomeDistrictId();
            return DistrictIndex(id);
        }

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

        /// <summary>True if a building is one the player rents or owns.</summary>
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
                if (r != null && r.Address != null && GoingPublic.Mine(r) && r.Address.streetName == building.building.Address.streetName && r.Address.streetNumber == building.building.Address.streetNumber)
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
        public int RaceDistrict()
        {
            return State.Race == OfficeRace.Council ? DistrictIndex(State.District) : HomeDistrict();
        }

        /// <summary>Where the run is: the council seat's district, or the city for mayor.</summary>
        public string Where()
        {
            return DistrictName(State.Race == OfficeRace.Council ? RaceDistrict() : -1);
        }

        public string DistrictName(int district)
        {
            return district >= 0 && district < _city.Districts.Count ? CivicCity.Name(_city.Districts[district]) : CivicText.T("reallife_civic_office_the_city");
        }

        // ---------------------------------------------------------------- standing

        /// <summary>Whether a run may begin now, and why not.</summary>
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
            me.First = random.Range(0, CivicCatalogue.FirstNames.Length);   // (the pools' names are never shown for the player; a guest of a session would see them)
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

        public bool InPlatform(int policy)
        {
            return CivicGovernment.Promised(State.Me, policy);
        }

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
            OfficeRefusal why = OfficeRules.MayFile(State.Me, State.Race, State.Signatures, State.Incumbent, deposit, true);   // (the payment itself refuses if it cannot be made)
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

        // ---------------------------------------------------------------- the fund

        /// <summary>A step of the player's own money into the fund. Returns the locale key.</summary>
        public string PutIn()
        {
            if (!State.Running)
            {
                return "reallife_civic_office_no_run";
            }
            int step = Prices.At(OfficeRules.OwnStep);
            if (!GameManager.ChangeMoneySafe(-step, new TransactionInfo("reallife_transaction_civic_office_own"), null, null, force: false, showNotification: true))
            {
                return "reallife_civic_office_no_money";
            }
            State.Fund += step;
            State.Own += step;
            State.Raised += step;
            return "reallife_civic_office_put_in";
        }

        /// <summary>The donor's offer is taken. Against the law it is money that can come out later.</summary>
        public string Accept(int day)
        {
            if (State.OfferKind < 0 || State.OfferAmount <= 0)
            {
                return "reallife_civic_office_no_offer";
            }
            int amount = State.OfferAmount;
            bool legal = OfficeRules.Legal(State.OfferKind, amount, Limit);
            State.Fund += amount;
            State.Raised += amount;
            if (!legal)
            {
                State.Dirty += amount;
                State.DirtyDay = day;
                Logger("money taken against the law: " + amount + " (" + State.OfferKind + "); " + State.Dirty + " that could come out.");
            }
            State.OfferKind = -1;
            State.OfferAmount = 0;
            return legal ? "reallife_civic_office_offer_taken" : "reallife_civic_office_offer_taken_dirty";
        }

        public string Decline()
        {
            State.OfferKind = -1;
            State.OfferAmount = 0;
            return "reallife_civic_office_offer_declined";
        }

        public const int BuyPosters = 0, BuyLedger = 1, BuyFlare = 2, BuyRally = 3, BuyCanvassers = 4;

        public static int Price(int what)
        {
            switch (what)
            {
                case BuyPosters: return Prices.At(OfficeRules.PosterRun);
                case BuyLedger: return Prices.At(OfficeRules.LedgerAd);
                case BuyFlare: return Prices.At(OfficeRules.FlareAd);
                case BuyRally: return Prices.At(OfficeRules.RallyHire);
                default: return Prices.At(OfficeRules.Canvassers);
            }
        }

        /// <summary>Spends from the fund. A rally is staged by the caller (CampaignWorld) once this has said yes. Returns the locale key.</summary>
        public string Buy(int what, int day, int hour)
        {
            if (!Campaigning)
            {
                return "reallife_civic_office_no_campaign";
            }
            int price = Price(what);
            if (State.Fund < price)
            {
                return "reallife_civic_office_fund_short";
            }
            switch (what)
            {
                case BuyPosters:
                    State.PostersHeld += OfficeRules.PostersPerRun;
                    break;
                case BuyLedger:
                case BuyFlare:
                    State.Push += (what == BuyLedger ? 0.03f : 0.025f) * Mathf.Pow(0.8f, State.Ads);
                    State.Ads++;
                    if (what == BuyLedger)
                    {
                        State.Story(day + 1, "reallife_civic_news_office_advert", CivicText.PlayerName(), CivicText.T(CivicCatalogue.Slogans[State.Me.Slogan]));
                    }
                    else
                    {
                        Post(PostFiled, CivicText.T(CivicCatalogue.Slogans[State.Me.Slogan]));
                    }
                    break;
                case BuyRally:
                    if (State.LastRally == day)
                    {
                        return "reallife_civic_office_rally_today";
                    }
                    if (hour < OfficeRules.RallyFrom || hour >= OfficeRules.RallyTo)
                    {
                        return "reallife_civic_office_rally_hours";
                    }
                    break;
                default:
                    if (State.CanvassDay == day)
                    {
                        return "reallife_civic_office_canvass_today";
                    }
                    State.CanvassDay = day;
                    Doors(RaceDistrict(), OfficeRules.CanvasserDoors, OfficeRules.CanvasserDoors * 0.0015f);
                    break;
            }
            State.Fund -= price;
            State.Spent += price;
            return what == BuyPosters ? "reallife_civic_office_bought_posters" : what == BuyRally ? "reallife_civic_office_bought_rally"
                : what == BuyCanvassers ? "reallife_civic_office_bought_canvassers" : "reallife_civic_office_bought_advert";
        }

        /// <summary>The rally's money back: no place for it could be found.</summary>
        public void RallyRefund()
        {
            int price = Price(BuyRally);
            State.Fund += price;
            State.Spent = Math.Max(0, State.Spent - price);
        }

        // Doors knocked in a district (by the player or canvassers): support there, a little more of it turning out.
        private void Doors(int district, int doors, float push)
        {
            State.Doors += doors;
            if (district >= 0 && district < State.DistrictPush.Length)
            {
                State.DistrictDoors[district] += doors;
                State.DistrictPush[district] = Mathf.Clamp(State.DistrictPush[district] + push, -0.2f, OfficeRules.MostPush);
            }
            else
            {
                State.Push += push * 0.5f;
            }
        }

        // ---------------------------------------------------------------- the street

        /// <summary>True if the player may knock on doors now (and at this building, of this district): a run on, the hours, not twice a day.</summary>
        public bool MayKnock(int day, int hour, string neighbourhood, int building)
        {
            if (!Enabled || !State.Running || (State.Filed && !Campaigning) || hour < OfficeRules.DoorsFrom || hour >= OfficeRules.DoorsTo)
            {
                return false;
            }
            if (State.Race == OfficeRace.Council && neighbourhood != State.District)
            {
                return false;
            }
            if (_knockedDay != day)
            {
                _knockedDay = day;
                _knocked.Clear();
            }
            return !_knocked.Contains(building) && _knocked.Count < KnockedMost;
        }

        /// <summary>A door was knocked on and answered: what came of it.</summary>
        public void Knocked(int building, string neighbourhood, OfficeGain gain, int day)
        {
            _knocked.Add(building);
            State.LastDoorDay = day;
            Doors(DistrictIndex(neighbourhood), 1, gain.Push);
            if (Collecting)
            {
                State.Signatures += gain.Signatures;
            }
            if (gain.Donation > 0)
            {
                int amount = Prices.At(gain.Donation);
                State.Fund += amount;
                State.Raised += amount;
            }
        }

        /// <summary>A poster of the player's went up on a wall in a district.</summary>
        public void PosterUp(string neighbourhood)
        {
            if (State.PostersHeld <= 0)
            {
                return;
            }
            State.PostersHeld--;
            State.PostersUp++;
            if (State.PostersUp * OfficeRules.PosterPush <= OfficeRules.PosterPushMost)
            {
                int d = DistrictIndex(neighbourhood);
                if (d >= 0)
                {
                    State.DistrictPush[d] = Mathf.Min(OfficeRules.MostPush, State.DistrictPush[d] + OfficeRules.PosterPush);
                }
                else
                {
                    State.Push += OfficeRules.PosterPush * 0.5f;
                }
            }
        }

        /// <summary>The player spoke at their own rally.</summary>
        public void Rallied(OfficeGain gain, int day)
        {
            State.Rallies++;
            State.LastRally = day;
            State.Push += gain.Push;
            CivicBridges.StandingMoved?.Invoke(0, 3);
            Post(PostRally, Where());
        }

        /// <summary>The player took part in the debate, or did not.</summary>
        public void Debated(float gain, bool there, int day)
        {
            State.Debate = there ? 1 : 2;
            State.DebateGain = gain;
            State.Push += gain;
            if (there)
            {
                CivicBridges.StandingMoved?.Invoke(0, 8);
                Post(PostDebate, Where());
            }
            State.Story(day + 1, there ? (gain >= 0.04f ? "reallife_civic_news_office_debate_good" : gain < 0f ? "reallife_civic_news_office_debate_bad" : "reallife_civic_news_office_debate_even")
                : "reallife_civic_news_office_debate_absent", CivicText.PlayerName(), Where());
        }

        // ---------------------------------------------------------------- the race's candidates (the mayor's: the city's slots; the council's: the player and rivals)

        /// <summary>How many are in the race the player is in.</summary>
        public int RaceCount => State.Race == OfficeRace.Council ? State.RivalCount + 1 : Gov.CandidateCount;

        /// <summary>A candidate of the player's race by its number in it (the council's: 0 is the player).</summary>
        public Candidate RaceCandidate(int i)
        {
            if (State.Race == OfficeRace.Council)
            {
                return i == 0 ? State.Me : i - 1 < State.RivalCount && i >= 1 ? State.Rival[i - 1] : null;
            }
            return i >= 0 && i < Gov.CandidateCount ? Gov.Candidates[i] : null;
        }

        /// <summary>The player's number in their race.</summary>
        public int MyIndex => State.Race == OfficeRace.Council ? 0 : Gov.PlayerSlot;

        public float RacePoll(int i)
        {
            if (State.Race == OfficeRace.Council)
            {
                return i >= 0 && i <= State.RivalCount ? State.RacePolls[i] : 0f;
            }
            return i >= 0 && i < Gov.CandidateCount ? Gov.Polls[i] : 0f;
        }

        /// <summary>The player's poll in their race now.</summary>
        public float MyPoll => MyIndex >= 0 ? RacePoll(MyIndex) : 0f;

        /// <summary>The best placed of the others in the player's race; -1 for none.</summary>
        public int Leader()
        {
            int best = -1;
            for (int i = 0; i < RaceCount; i++)
            {
                if (i != MyIndex && (best < 0 || RacePoll(i) > RacePoll(best)))
                {
                    best = i;
                }
            }
            return best;
        }

        // Support for another in the race (an endorsement, an answer that cost them).
        private void Nudge(int i, float push)
        {
            if (State.Race == OfficeRace.Council)
            {
                if (i >= 1 && i <= State.RivalCount)
                {
                    State.RivalPush[i - 1] += push;
                }
            }
            else if (i >= 0 && i < Gov.CandidateCount)
            {
                Gov.Momentum[i] += push;
            }
        }

        // What the others in the race have spent so far, on average: a sum a day since the campaign began, at today's prices.
        private int RivalSpending()
        {
            if (State.Race == OfficeRace.Council)
            {
                int most = 0;
                for (int i = 0; i < State.RivalCount; i++)
                {
                    most = Math.Max(most, State.RivalSpend[i]);
                }
                return most;
            }
            int days = Mathf.Clamp(CivicCatalogue.CampaignDays - (Gov.ElectionDay - TimeHelper.CurrentDay), 0, CivicCatalogue.CampaignDays);
            return days * Prices.At(700);
        }

        // ---------------------------------------------------------------- the government's hooks

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

        private bool Sits()
        {
            return Enabled && State.InOffice;
        }

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

        // ---------------------------------------------------------------- the day

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

        private void CampaignDay(int day)
        {
            int left = Gov.ElectionDay - day;
            int seed = Gov.Seed;
            // A donor's offer, if there is none waiting (one waits a day).
            if (State.OfferKind >= 0 && State.OfferDay < day - 1)
            {
                State.OfferKind = -1;
            }
            if (State.OfferKind < 0 && OfficeRules.Offer(OfficeRules.Roll(seed, day, 1201), OfficeRules.Roll(seed, day, 1203), Recognition, MyPoll, Limit, out int kind, out int amount))
            {
                State.OfferKind = kind;
                State.OfferAmount = amount;
                State.OfferDay = day;
                var names = new CivicRandom(CivicRandom.Mix(seed, day, 1207));
                State.OfferFirst = names.Range(0, CivicCatalogue.FirstNames.Length);
                State.OfferLast = names.Range(0, CivicCatalogue.LastNames.Length);
                CivicText.Toast(NotificationType.Info, "reallife_civic_office_offer_new", "amount", CivicText.Money(amount), 12f);
            }
            // An attack waiting too long stands as it is.
            if (State.AttackKind >= 0 && day > State.AttackDay + OfficeRules.AttackAnswerDays)
            {
                State.AttackKind = -1;
            }
            if (State.AttackKind < 0 && left >= 1 && OfficeRules.Roll(seed, day, 1211) < OfficeRules.AttackAttempt(left))
            {
                Attack(day);
            }
            if (left == OfficeRules.EndorseDecide)
            {
                Endorsers(day);
            }
            if (State.Debate == 0 && left < CivicCatalogue.DebateBefore)
            {
                Debated(OfficeRules.EmptyChair, false, day);   // the debate went by without the player
            }
            if (State.Race == OfficeRace.Council)
            {
                for (int i = 0; i < State.RivalCount; i++)
                {
                    State.RivalSpend[i] += Prices.At(150 + OfficeRules.Roll(seed, day, 1221 + i) / 4);
                    State.RivalPush[i] += 0.004f;
                }
                RaceMove(day, 0.34f);
            }
            if (left == CivicCatalogue.CampaignDays - 1)
            {
                State.Story(day + 1, State.Race == OfficeRace.Mayor ? "reallife_civic_news_office_race_mayor" : "reallife_civic_news_office_race_council",
                    CivicText.PlayerName(), Where());
            }
        }

        // An opponent's attack: on what is true of the player if there is something, else on something thin.
        private void Attack(int day)
        {
            int by = Leader();
            if (by < 0)
            {
                return;
            }
            var kinds = new List<AttackKind>(6);
            bool record = CivicBridges.HasRecord() || CivicBridges.Ask(CivicBridges.CaseOpen);
            if (record) kinds.Add(AttackKind.Record);
            if (State.Dirty > 0) kinds.Add(AttackKind.Money);
            if (State.Missed > 0 || State.Debate == 2) kinds.Add(AttackKind.Absent);
            if (Integrity < 45) kinds.Add(AttackKind.Business);
            if (State.InOffice && Unkept(State.Holds == OfficeRace.Mayor ? Gov.Mayor : State.Held) > 0) kinds.Add(AttackKind.Promises);
            if (State.Wins == 0 && !State.InOffice) kinds.Add(AttackKind.Inexperience);
            int roll = OfficeRules.Roll(Gov.Seed, day, 1213);
            AttackKind kind;
            bool real;
            if (kinds.Count > 0 && roll < 700)
            {
                kind = kinds[roll % kinds.Count];
                real = true;
            }
            else
            {
                kind = roll % 2 == 0 ? AttackKind.Business : AttackKind.Money;
                real = (kind == AttackKind.Business && Integrity < 45) || (kind == AttackKind.Money && State.Dirty > 0);
            }
            State.AttackKind = (int)kind;
            State.AttackBy = by;
            State.AttackDay = day;
            State.AttackTrue = real;
            State.Attacks++;
            State.Push += OfficeRules.AttackHit(real);
            Candidate who = RaceCandidate(by);
            string name = who != null ? CivicText.Name(who) : string.Empty;
            CivicText.Toast(NotificationType.Warning, "reallife_civic_office_attacked", "name", name, "what", CivicText.T(AttackKey(kind)), 14f);
            State.Story(day + 1, "reallife_civic_news_office_attack", name, CivicText.T(AttackKey(kind)));
            Logger("attacked by " + name + " on " + kind + (real ? " (there is something in it)" : " (thin)") + ".");
        }

        public static string AttackKey(AttackKind kind)
        {
            switch (kind)
            {
                case AttackKind.Record: return "reallife_civic_office_attack_record";
                case AttackKind.Business: return "reallife_civic_office_attack_business";
                case AttackKind.Money: return "reallife_civic_office_attack_money";
                case AttackKind.Inexperience: return "reallife_civic_office_attack_inexperience";
                case AttackKind.Absent: return "reallife_civic_office_attack_absent";
                default: return "reallife_civic_office_attack_promises";
            }
        }

        /// <summary>The player answers the attack waiting. Returns the locale key.</summary>
        public string AnswerAttack(OfficeAnswer answer, int day)
        {
            if (State.AttackKind < 0)
            {
                return "reallife_civic_office_no_attack";
            }
            OfficeRules.Answer(answer, State.AttackTrue, out float mine, out float theirs);
            State.Push += mine;
            Nudge(State.AttackBy, theirs);
            bool real = State.AttackTrue;
            State.AttackKind = -1;
            if (answer == OfficeAnswer.Straight && real)
            {
                CivicBridges.StandingMoved?.Invoke(-1, 0);   // a denial of something true
            }
            return answer == OfficeAnswer.Straight ? (real ? "reallife_civic_office_answer_denied" : "reallife_civic_office_answer_rebutted")
                : answer == OfficeAnswer.TurnBack ? "reallife_civic_office_answer_hit_back" : "reallife_civic_office_answer_ignored";
        }

        // The bodies that have not been asked decide, four days out, for whoever fits them best.
        private void Endorsers(int day)
        {
            for (int e = 0; e < OfficeRules.Endorsers; e++)
            {
                if (State.Backs[e] >= 0)
                {
                    continue;
                }
                int best = BestFor(e, true);
                Endorse(e, best, day);
            }
        }

        // The candidate a body likes best (the player among them if asked for), by fit and then by the polls.
        private int BestFor(int endorser, bool withMe)
        {
            int best = -1;
            float bestFit = float.MinValue;
            for (int i = 0; i < RaceCount; i++)
            {
                Candidate c = RaceCandidate(i);
                if (c == null || (!withMe && i == MyIndex))
                {
                    continue;
                }
                float fit = OfficeRules.EndorserFit(endorser, c, c.Player ? Integrity : 50) + RacePoll(i) * 0.5f;
                if (fit > bestFit)
                {
                    bestFit = fit;
                    best = i;
                }
            }
            return best;
        }

        private void Endorse(int endorser, int candidate, int day)
        {
            if (candidate < 0)
            {
                return;
            }
            State.Backs[endorser] = candidate;
            float push = endorser == OfficeRules.Endorsers - 1 ? OfficeRules.EditorialPush : OfficeRules.EndorsementPush;
            if (candidate == MyIndex)
            {
                State.Endorsed |= 1 << endorser;
                State.Push += push;
                CivicText.Toast(NotificationType.Success, "reallife_civic_office_endorsed", "who", CivicText.T(EndorserKey(endorser)), 12f);
                State.Story(day + 1, "reallife_civic_news_office_endorsed", CivicText.PlayerName(), CivicText.T(EndorserKey(endorser)));
            }
            else
            {
                Nudge(candidate, push);
            }
        }

        public static string EndorserKey(int endorser)
        {
            return BodyKeys[Mathf.Clamp(endorser, 0, BodyKeys.Length - 1)];
        }

        private static readonly string[] BodyKeys =
        {
            "reallife_civic_office_body_0", "reallife_civic_office_body_1", "reallife_civic_office_body_2", "reallife_civic_office_body_3",
            "reallife_civic_office_body_4", "reallife_civic_office_body_5", "reallife_civic_office_body_6"
        };

        /// <summary>The departments' names, and a line on what each pays for.</summary>
        public static readonly string[] DepartmentKeys = { "reallife_civic_office_dept_0", "reallife_civic_office_dept_1", "reallife_civic_office_dept_2", "reallife_civic_office_dept_3" };
        public static readonly string[] DepartmentAbout =
        {
            "reallife_civic_office_dept_about_0", "reallife_civic_office_dept_about_1", "reallife_civic_office_dept_about_2", "reallife_civic_office_dept_about_3"
        };

        /// <summary>The player asks a body for its endorsement (once an election). Returns the locale key; the body's choice is in State.Backs.</summary>
        public string Ask(int endorser, int day)
        {
            if (!Campaigning || endorser < 0 || endorser >= OfficeRules.Endorsers)
            {
                return "reallife_civic_office_no_campaign";
            }
            if ((State.Asked & (1 << endorser)) != 0 || State.Backs[endorser] >= 0)
            {
                return "reallife_civic_office_asked_already";
            }
            State.Asked |= 1 << endorser;
            int rival = BestFor(endorser, false);
            float mine = OfficeRules.EndorserFit(endorser, State.Me, Integrity), theirs = rival >= 0 ? OfficeRules.EndorserFit(endorser, RaceCandidate(rival), 50) : 0f;
            bool yes = OfficeRules.Roll(Gov.Seed, day, 1301 + endorser) < OfficeRules.AskOdds(mine, theirs, Influence);
            Endorse(endorser, yes ? MyIndex : rival, day);
            return yes ? "reallife_civic_office_ask_yes" : "reallife_civic_office_ask_no";
        }

        // ---------------------------------------------------------------- the district's race

        private void RaceTargets()
        {
            int d = RaceDistrict();
            for (int i = 0; i < CivicCatalogue.Issues; i++)
            {
                _weights[i] = _gov.Weight(i) * (d >= 0 ? _gov.DistrictLean(d, i) : 1f);
            }
            int cycle = CivicBridges.Cycle();
            int scale = Prices.At(1000);
            _raceScores[0] = OfficeRules.Fit(State.Me, _weights) * 1.1f + Term(d) + OfficeRules.LocalTerm(true, Businesses(d), 0f);
            if (State.Incumbent)
            {
                _raceScores[0] += (State.Approval - 50f) / 40f + OfficeRules.EconomyTerm(cycle, true);
            }
            for (int i = 0; i < State.RivalCount; i++)
            {
                Candidate rival = State.Rival[i];
                var appeal = new CivicRandom(CivicRandom.Mix(Gov.Seed, State.ForDay * 7 + i, 4421));
                float score = OfficeRules.Fit(rival, _weights) * 1.1f + appeal.Range(-0.2f, 0.2f) + State.RivalPush[i]
                              + OfficeRules.SpendingTerm(State.RivalSpend[i], State.Spent, scale) * 0.5f;
                if (rival.Party == Gov.Mayor.Party)
                {
                    score += (Gov.Approval - 50f) / 80f;
                }
                if (rival.Incumbent)
                {
                    score += OfficeRules.EconomyTerm(cycle, true);
                }
                _raceScores[i + 1] = score;
            }
            OfficeRules.Shares(_raceScores, State.RivalCount + 1, _raceShares);
        }

        private void RaceMove(int day, float pull)
        {
            RaceTargets();
            var noise = new CivicRandom(CivicRandom.Mix(Gov.Seed, day, 4431));
            for (int i = 0; i <= State.RivalCount; i++)
            {
                State.RacePolls[i] = OfficeRules.PollStep(State.RacePolls[i], _raceShares[i], pull, noise.Range(-0.012f, 0.012f));
            }
            OfficeRules.Normalise(State.RacePolls, State.RivalCount + 1);
        }

        // ---------------------------------------------------------------- the count

        private void Counted(int day)
        {
            bool mine = State.Filed && State.ForDay == Gov.ElectionDay;
            OfficeRace held = State.Holds;
            if (!mine)
            {
                if (State.Running)
                {
                    State.ClearRun();   // papers never filed
                }
                if (held == OfficeRace.Council || (held == OfficeRace.Mayor && !Gov.MayorIsPlayer))
                {
                    LeaveOffice(day, "reallife_civic_office_term_over");
                }
                return;
            }
            bool won;
            float share;
            if (State.Race == OfficeRace.Mayor)
            {
                int slot = Gov.PlayerSlot;
                won = slot >= 0 && Gov.Winner == slot;
                share = slot >= 0 && Gov.Turnout > 0 ? _gov.CityVotes(slot) / (float)Gov.Turnout : 0f;
            }
            else
            {
                won = CountRace(day, out share);
            }
            State.Runs++;
            State.Result = won ? 1 : 2;
            State.ResultRace = State.Race;
            State.ResultShare = Mathf.RoundToInt(share * 1000f);
            State.ResultDay = day;
            State.ResultShown = false;
            if (State.Deposit && share >= OfficeRules.DepositBack)
            {
                CivicEffects.Pay(Prices.At(OfficeRules.Deposit(State.Race)), "reallife_transaction_civic_office_deposit_back");
            }
            string place = Where();
            if (won)
            {
                State.Wins++;
                if (held != OfficeRace.None && held != State.Race)
                {
                    State.Terms++;   // a councillor elected mayor gives up the seat
                }
                TakeOffice(State.Race, share, day);
                State.Story(day + 1, State.Race == OfficeRace.Mayor ? "reallife_civic_news_office_won_mayor" : "reallife_civic_news_office_won_council", CivicText.PlayerName(), place);
                Post(PostWon, place);
                ModState.AddModifier(ModState.MoodUpBig);
            }
            else
            {
                if (held != OfficeRace.None)
                {
                    LeaveOffice(day, null);
                }
                State.Story(day + 1, "reallife_civic_news_office_lost", CivicText.PlayerName(), place);
                ModState.AddModifier(ModState.MoodDown);
            }
            CivicBridges.StandingMoved?.Invoke(0, won ? 25 : 8);
            if (State.Dirty > 0)
            {
                State.DirtyDay = Math.Max(State.DirtyDay, day);   // money taken against the law can still come out for a while
            }
            Logger("the count: " + (won ? "won" : "lost") + " the race for " + State.Race + " with " + State.ResultShare / 10f + " % of the vote.");
            // The run is over; the race's names and votes stay for result night and City Hall until the next run begins.
            State.Race = OfficeRace.None;
            State.Filed = false;
            State.OfferKind = -1;
            State.AttackKind = -1;
        }

        // The district's race: its voters, its turnout, every candidate's votes. True if the player won; their share out.
        private bool CountRace(int day, out float share)
        {
            RaceMove(day, 0.2f);
            int d = RaceDistrict();
            int electorate = d >= 0 && d < _city.Districts.Count ? _city.Districts[d].Electorate : 9000;
            var roll = new CivicRandom(CivicRandom.Mix(Gov.Seed, Gov.Number, 4441));
            float turnout = OfficeRules.Turnout(roll.Value, d >= 0 && d < State.DistrictDoors.Length ? State.DistrictDoors[d] : 0);
            int total = 0, best = 0;
            for (int i = 0; i <= State.RivalCount; i++)
            {
                State.RaceVotes[i] = OfficeRules.Votes(electorate, turnout, State.RacePolls[i]) + (i == 0 && Gov.Voted >= 0 ? 1 : 0);
                total += State.RaceVotes[i];
                if (State.RaceVotes[i] > State.RaceVotes[best])
                {
                    best = i;
                }
            }
            State.RaceTurnout = total;
            share = total > 0 ? State.RaceVotes[0] / (float)total : 0f;
            return best == 0;
        }

        private void TakeOffice(OfficeRace race, float share, int day)
        {
            State.Holds = race;
            State.Held.CopyFrom(State.Me);
            State.Held.Player = true;
            State.TermStart = day + 1;
            State.PayDay = day + 1 + 7;
            State.Registered = false;
            State.RegisterBy = day + 1 + OfficeRules.RegisterDays;
            State.NextMeeting = day + 1 + 3;
            State.Agenda.Clear();
            State.Missed = 0;
            State.Attended = 0;
            State.VotesRecord = 0f;
            State.LowDays = 0;
            State.RecallDay = 0;
            State.Approval = OfficeRules.StartApproval(share);
            if (race == OfficeRace.Council)
            {
                State.Seat = CivicCatalogue.CouncilSeats - 1;
                Gov.Council[State.Seat] = State.Me.Party;
            }
            else
            {
                State.Seat = -1;
            }
        }

        private void LeaveOffice(int day, string toastKey)
        {
            if (State.Holds == OfficeRace.None)
            {
                return;
            }
            State.Terms++;
            State.Holds = OfficeRace.None;
            State.Seat = -1;
            State.Agenda.Clear();
            State.RecallDay = 0;
            if (toastKey != null)
            {
                CivicText.Toast(NotificationType.Info, toastKey, 12f);
            }
        }

        /// <summary>A post about the player's run or office went out on social media (Wiring/FlareLinks): thousandths of a point of its worth.</summary>
        public void Posted(int weight)
        {
            float worth = Mathf.Clamp(weight, 0, 1500) / 1000f;
            if (Campaigning)
            {
                State.Push += Mathf.Min(0.04f, worth * 0.1f);
            }
            else if (State.Holds == OfficeRace.Council)
            {
                State.Approval = Mathf.Min(95f, State.Approval + Mathf.Min(1.5f, worth * 3f));
            }
        }

        /// <summary>Result night has been seen (or missed).</summary>
        public void ResultSeen()
        {
            State.ResultShown = true;
        }

        // ---------------------------------------------------------------- what can come out

        private void Discoveries(int day)
        {
            if (State.Dirty > 0)
            {
                if (day - State.DirtyDay > OfficeRules.DirtyWindow && !Campaigning)
                {
                    Logger("the money taken against the law never came out (" + State.Dirty + ").");
                    State.Dirty = 0;
                }
                else if (OfficeRules.Roll(Gov.Seed, day, 1401) < OfficeRules.DirtyOdds(State.Dirty, Limit, Recognition))
                {
                    int dirty = State.Dirty;
                    State.Dirty = 0;
                    State.Fund = Math.Max(0, State.Fund - dirty);
                    State.Case = CivicBridges.FileCase != null ? CivicBridges.FileCase(0, dirty) : 0;
                    Scandal(day, "reallife_civic_news_office_money", 10f, -8, 0.2f);
                    CivicText.Toast(NotificationType.Warning, "reallife_civic_office_money_out", "amount", CivicText.Money(dirty), 16f);
                }
            }
            for (int i = State.Secrets.Count - 1; i >= 0; i--)
            {
                OfficeSecret s = State.Secrets[i];
                if (day - s.Day > OfficeRules.UndeclaredWindow)
                {
                    State.Secrets.RemoveAt(i);
                    continue;
                }
                if (OfficeRules.Roll(Gov.Seed, day, 1411 + s.Policy * 7 + s.Day) < OfficeRules.UndeclaredOdds(s.Decisive, Recognition, State.Registered))
                {
                    State.Secrets.RemoveAt(i);
                    PolicyDef policy = CivicCatalogue.At(s.Policy);
                    if (s.Decisive && CivicBridges.FileCase != null)
                    {
                        CivicBridges.FileCase(1, 0);
                    }
                    Scandal(day, "reallife_civic_news_office_interest", 8f, -6, 0.1f);
                    CivicText.Toast(NotificationType.Warning, s.Decisive ? "reallife_civic_office_interest_out_case" : "reallife_civic_office_interest_out",
                        "policy", policy != null ? CivicText.T(policy.Name) : string.Empty, 16f);
                }
            }
        }

        // A scandal: approval in office (or support in a campaign), integrity and the papers.
        private void Scandal(int day, string storyKey, float approval, int integrity, float support)
        {
            State.Scandals++;
            if (Campaigning)
            {
                State.Push -= support;
            }
            if (State.Holds == OfficeRace.Mayor && Gov.MayorIsPlayer)
            {
                Gov.Approval = Mathf.Max(3f, Gov.Approval - approval);
            }
            else if (State.Holds == OfficeRace.Council)
            {
                State.Approval = Mathf.Max(3f, State.Approval - approval);
            }
            CivicBridges.StandingMoved?.Invoke(integrity, 15);
            ModState.AddModifier(ModState.MoodDownBig);
            State.Story(day + 1, storyKey, CivicText.PlayerName(), Where());
        }

        // ---------------------------------------------------------------- office

        private void OfficeDay(int day)
        {
            if (day >= State.PayDay && State.PayDay > 0)
            {
                State.PayDay += 7;
                CivicEffects.Pay(Prices.At(OfficeRules.Salary(State.Holds)), "reallife_transaction_civic_office_salary");
            }
            if (!State.Registered && State.RegisterBy > 0 && day > State.RegisterBy)
            {
                State.RegisterBy = 0;
                Scandal(day, "reallife_civic_news_office_register", 3f, -2, 0f);
                CivicText.Toast(NotificationType.Warning, "reallife_civic_office_register_late", 14f);
            }
            if (State.NextMeeting == Gov.ElectionDay && Gov.Phase == ElectionPhase.Campaign)
            {
                State.NextMeeting++;   // nobody meets on election day: the day after
            }
            // A meeting that went by without the player: held without them.
            while (State.NextMeeting > 0 && State.NextMeeting < day)
            {
                HoldWithout(State.NextMeeting);
            }
            if (State.NextMeeting == day)
            {
                Prepare(day);
            }
            if (State.Holds == OfficeRace.Council)
            {
                CouncillorDay(day);
            }
        }

        private void CouncillorDay(int day)
        {
            Candidate held = State.Held;
            int kept = 0, unkept = 0;
            for (int i = 0; i < held.Planks; i++)
            {
                PolicyDef p = CivicCatalogue.At(held.Platform[i]);
                if (p != null)
                {
                    if (Gov.Levers[(int)p.Lever] == p.Dir)
                    {
                        kept++;
                    }
                    else
                    {
                        unkept++;
                    }
                }
            }
            State.VotesRecord *= 0.95f;
            float target = OfficeRules.CouncillorTarget(Gov.Approval, kept, unkept, State.VotesRecord, State.Scandals, State.Missed, CivicBridges.Cycle());
            var noise = new CivicRandom(CivicRandom.Mix(Gov.Seed, day, 1501));
            State.Approval = OfficeRules.Approach(State.Approval, target, noise.Range(-1f, 1f));
            State.LowDays = State.Approval < OfficeRules.RecallBelow ? State.LowDays + 1 : 0;
            if (State.RecallDay == 0 && State.LowDays >= OfficeRules.RecallDays && !Campaigning && day - State.TermStart > 14)
            {
                State.RecallDay = day + OfficeRules.RecallVoteAfter;
                State.RecallPush = 0f;
                CivicText.Toast(NotificationType.Warning, "reallife_civic_office_recall_called", "day", State.RecallDay.ToString(), 14f);
                State.Story(day + 1, "reallife_civic_news_office_recall", CivicText.PlayerName(), DistrictName(HomeDistrict()));
            }
            else if (State.RecallDay > 0 && day >= State.RecallDay)
            {
                float keep = OfficeRules.RecallKeep(State.Approval, State.RecallPush);
                State.RecallDay = 0;
                State.LowDays = 0;
                if (keep < 0.5f)
                {
                    LeaveOffice(day, "reallife_civic_office_recalled");
                    State.Story(day + 1, "reallife_civic_news_office_recalled", CivicText.PlayerName(), CivicGovernment.Percent(keep));
                }
                else
                {
                    State.Approval = Mathf.Max(State.Approval, 35f);
                    CivicText.Toast(NotificationType.Success, "reallife_civic_office_recall_survived", "share", CivicGovernment.Percent(keep), 14f);
                    State.Story(day + 1, "reallife_civic_news_office_survived", CivicText.PlayerName(), CivicGovernment.Percent(keep));
                }
            }
        }

        /// <summary>The player's approval in office: the city's for a mayor, their own for a councillor.</summary>
        public float Approval => State.Holds == OfficeRace.Mayor ? Gov.Approval : State.Approval;

        /// <summary>Promises of a platform not yet in force.</summary>
        public int Unkept(Candidate c)
        {
            int unkept = 0;
            for (int i = 0; i < c.Planks; i++)
            {
                PolicyDef p = CivicCatalogue.At(c.Platform[i]);
                unkept += p != null && Gov.Levers[(int)p.Lever] != p.Dir ? 1 : 0;
            }
            return unkept;
        }

        // The day of a meeting: the council's own business (for a councillor), the budget when it is due.
        private void Prepare(int day)
        {
            if (State.Holds == OfficeRace.Council && !HasMotion(OfficeMotion.ByCity) && !HasMotion(OfficeMotion.ByMayor))
            {
                int policy = _gov.CityMotion(day);
                if (policy >= 0 && !OnAgenda(policy))
                {
                    Put(policy, CivicGovernment.Promised(Gov.Mayor, policy) ? OfficeMotion.ByMayor : OfficeMotion.ByCity, day);
                }
            }
            if (State.BudgetDay > 0 && State.BudgetDay <= day + OfficeRules.MeetingEvery && !HasBudget())
            {
                var m = new OfficeMotion { Kind = OfficeMotion.KindBudget, By = Gov.MayorIsPlayer ? OfficeMotion.ByPlayer : OfficeMotion.ByMayor, Day = day };
                if (Gov.MayorIsPlayer)
                {
                    Array.Copy(State.Budget, m.Budget, m.Budget.Length);   // as it stands, until the mayor proposes another
                }
                else
                {
                    OfficeRules.CityBudget(Gov.Mayor.Lead, m.Budget);
                }
                Add(m);
            }
        }

        private bool HasMotion(int by)
        {
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                if (State.Agenda[i].Kind == OfficeMotion.KindPolicy && State.Agenda[i].By == by)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasBudget()
        {
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                if (State.Agenda[i].Kind == OfficeMotion.KindBudget)
                {
                    return true;
                }
            }
            return false;
        }

        public bool OnAgenda(int policy)
        {
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                if (State.Agenda[i].Kind == OfficeMotion.KindPolicy && State.Agenda[i].Policy == policy)
                {
                    return true;
                }
            }
            return false;
        }

        private void Put(int policy, int by, int day)
        {
            if (CivicCatalogue.At(policy) != null && !OnAgenda(policy))
            {
                Add(new OfficeMotion { Kind = OfficeMotion.KindPolicy, Policy = policy, By = by, Day = day });
            }
        }

        private void Add(OfficeMotion m)
        {
            if (State.Agenda.Count >= OfficeState.AgendaMost)
            {
                // the oldest of the city's own items makes way
                for (int i = 0; i < State.Agenda.Count; i++)
                {
                    if (State.Agenda[i].By == OfficeMotion.ByCity)
                    {
                        State.Agenda.RemoveAt(i);
                        break;
                    }
                }
            }
            if (State.Agenda.Count < OfficeState.AgendaMost)
            {
                State.Agenda.Add(m);
            }
        }

        /// <summary>True if a lever touches the player's own businesses or property: a vote on it is a conflict of interest.</summary>
        public bool Conflicted(int policy)
        {
            PolicyDef p = CivicCatalogue.At(policy);
            return p != null && OfficeRules.Conflicted(p.Lever, Businesses(-1), CivicBridges.Ask(CivicBridges.Landlord));
        }

        /// <summary>How many motions the player may table now, and why not (a locale key, or null).</summary>
        public string MayTable(out int room)
        {
            room = 0;
            if (!Sits())
            {
                return "reallife_civic_office_not_in_office";
            }
            int mine = 0;
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                mine += State.Agenda[i].By == OfficeMotion.ByPlayer && State.Agenda[i].Kind == OfficeMotion.KindPolicy ? 1 : 0;
            }
            room = (State.Holds == OfficeRace.Mayor ? 2 : 1) - mine;
            return room > 0 ? null : "reallife_civic_office_table_full";
        }

        /// <summary>The player puts a policy before the council's next meeting (as mayor: on the agenda they set). With an interest not declared by a mayor, it is a secret if it passes.</summary>
        public string Table(int policy, bool declared, int day)
        {
            string why = MayTable(out _);
            if (why != null)
            {
                return why;
            }
            PolicyDef p = CivicCatalogue.At(policy);
            if (p == null || _gov.Settled(p.Lever, p.Dir))
            {
                return "reallife_civic_office_table_settled";
            }
            if (OnAgenda(policy))
            {
                return "reallife_civic_office_table_there";
            }
            if (State.Holds == OfficeRace.Mayor)
            {
                // The mayor sets the agenda: the council's own business makes way for theirs.
                for (int i = State.Agenda.Count - 1; i >= 0; i--)
                {
                    if (State.Agenda[i].By == OfficeMotion.ByCity)
                    {
                        State.Agenda.RemoveAt(i);
                    }
                }
            }
            var m = new OfficeMotion { Kind = OfficeMotion.KindPolicy, Policy = policy, By = OfficeMotion.ByPlayer, Day = day, Undeclared = State.Holds == OfficeRace.Mayor && !declared && Conflicted(policy) };
            Add(m);
            State.Tabled = day;
            if (declared && Conflicted(policy))
            {
                CivicBridges.StandingMoved?.Invoke(1, 0);
            }
            return "reallife_civic_office_tabled";
        }

        /// <summary>A mayor's proposed budget goes before the council at the next meeting.</summary>
        public string ProposeBudget(int[] levels, int day)
        {
            if (!Sits() || State.Holds != OfficeRace.Mayor)
            {
                return "reallife_civic_office_not_mayor";
            }
            for (int i = State.Agenda.Count - 1; i >= 0; i--)
            {
                if (State.Agenda[i].Kind == OfficeMotion.KindBudget)
                {
                    State.Agenda.RemoveAt(i);
                }
            }
            var m = new OfficeMotion { Kind = OfficeMotion.KindBudget, By = OfficeMotion.ByPlayer, Day = day };
            for (int d = 0; d < m.Budget.Length; d++)
            {
                m.Budget[d] = Mathf.Clamp(levels[d], -OfficeRules.DepartmentMost, OfficeRules.DepartmentMost);
            }
            Add(m);
            return "reallife_civic_office_budget_proposed";
        }

        /// <summary>A mayor's order on a lever that is theirs to set (once a fortnight).</summary>
        public string Order(int policy, int day)
        {
            PolicyDef p = CivicCatalogue.At(policy);
            if (!Sits() || State.Holds != OfficeRace.Mayor)
            {
                return "reallife_civic_office_not_mayor";
            }
            if (p == null || !OfficeRules.ByOrder(p.Lever))
            {
                return "reallife_civic_office_order_council";
            }
            if (State.OrderDay > 0 && day < State.OrderDay + OfficeRules.OrderEvery)
            {
                return "reallife_civic_office_order_wait";
            }
            if (!_gov.Ordered(p.Lever, p.Dir, day))
            {
                return "reallife_civic_office_table_settled";
            }
            State.OrderDay = day;
            State.Story(day + 1, "reallife_civic_news_office_order", CivicText.PlayerName(), CivicText.T(p.Name));
            return "reallife_civic_office_ordered";
        }

        /// <summary>The register of interests is filed: the player's businesses and property, in public.</summary>
        public string Register()
        {
            if (!State.InOffice)
            {
                return "reallife_civic_office_not_in_office";
            }
            if (State.Registered)
            {
                return "reallife_civic_office_registered_already";
            }
            State.Registered = true;
            CivicBridges.StandingMoved?.Invoke(1, 0);
            return "reallife_civic_office_registered";
        }

        /// <summary>The player stands down from the office they hold.</summary>
        public string StandDown(int day)
        {
            if (!State.InOffice)
            {
                return "reallife_civic_office_not_in_office";
            }
            OfficeRace held = State.Holds;
            int seat = State.Seat;
            LeaveOffice(day, null);
            if (held == OfficeRace.Council && seat >= 0)
            {
                Gov.Council[seat] = Gov.Mayor.Party;   // the mayor's party fills the seat until the next election
            }
            else if (held == OfficeRace.Mayor)
            {
                _gov.Vacate(day);
            }
            State.Story(day + 1, "reallife_civic_news_office_stood_down", CivicText.PlayerName(), DistrictName(HomeDistrict()));
            return "reallife_civic_office_stood_down";
        }

        // ---------------------------------------------------------------- meetings

        /// <summary>True while today's meeting can be held with the player.</summary>
        public bool MeetingDue(int day, int hour)
        {
            return Sits() && State.NextMeeting == day && hour >= OfficeRules.MeetingHour && hour < OfficeRules.MeetingLast && day != Gov.ElectionDay;
        }

        /// <summary>
        /// One motion decided: every other member by their odds, the player as they voted (1 aye, 0 no; anything else is
        /// no vote: an abstention, standing aside, or not there). Applied at once.
        /// </summary>
        public MotionVote Decide(OfficeMotion m, int vote, bool declaredAside, int day, int speak = 0)
        {
            var result = new MotionVote();
            bool mayorTabled = m.By == OfficeMotion.ByMayor || (m.By == OfficeMotion.ByPlayer && State.Holds == OfficeRace.Mayor);
            bool playerTabled = m.By == OfficeMotion.ByPlayer;
            float support = m.Kind == OfficeMotion.KindBudget ? Mathf.Clamp01(0.5f - BudgetDeficit(m.Budget) * 0.3f) : _gov.Support(m.Policy);
            for (int seat = 0; seat < CivicCatalogue.CouncilSeats; seat++)
            {
                if (State.Holds == OfficeRace.Council && seat == State.Seat)
                {
                    continue;
                }
                bool mayorsSide = Gov.Council[seat] == Gov.Mayor.Party && (mayorTabled || (m.Kind == OfficeMotion.KindPolicy && CivicGovernment.Promised(Gov.Mayor, m.Policy)));
                float odds = OfficeRules.MemberOdds(support, m.By == OfficeMotion.ByPetition, mayorsSide, mayorTabled, playerTabled, Influence);
                if (State.Holds == OfficeRace.Mayor && Gov.Council[seat] == Gov.Mayor.Party)
                {
                    odds = Mathf.Clamp(odds + speak * OfficeRules.MayorWord, 0.05f, 0.95f);   // the mayor's word to their own party's members
                }
                var roll = new CivicRandom(CivicRandom.Mix(Gov.Seed, day * 31 + seat, (m.Kind == OfficeMotion.KindBudget ? 977 : m.Policy) + 911));
                if (roll.Value < odds)
                {
                    result.Ayes++;
                }
                else
                {
                    result.Noes++;
                }
            }
            bool voted = State.Holds == OfficeRace.Council && (vote == 0 || vote == 1);
            if (voted)
            {
                if (vote == 1)
                {
                    result.Ayes++;
                }
                else
                {
                    result.Noes++;
                }
            }
            result.Passed = OfficeRules.Passes(result.Ayes, result.Noes);
            if (voted)
            {
                result.Decisive = OfficeRules.Passes(result.Ayes - (vote == 1 ? 1 : 0), result.Noes - (vote == 0 ? 1 : 0)) != result.Passed;
            }
            Apply(m, result, voted, vote, declaredAside, day);
            return result;
        }

        private float BudgetDeficit(int[] levels)
        {
            BudgetWeek week = OfficeRules.Week(_gov.Electorate(), Gov.Levers, levels);
            return week.Revenue > 0 ? Mathf.Clamp(-week.Balance / (float)week.Revenue, 0f, 1f) * 3f : 0f;
        }

        private void Apply(OfficeMotion m, MotionVote result, bool voted, int vote, bool declaredAside, int day)
        {
            if (m.Kind == OfficeMotion.KindBudget)
            {
                if (result.Passed)
                {
                    Array.Copy(m.Budget, State.Budget, State.Budget.Length);
                    State.Story(day + 1, "reallife_civic_news_office_budget", CivicText.PlayerName(), Levels(m.Budget));
                }
                State.BudgetDay = day + OfficeRules.BudgetEvery;
                return;
            }
            PolicyDef policy = CivicCatalogue.At(m.Policy);
            if (policy == null)
            {
                return;
            }
            if (result.Passed)
            {
                _gov.Passed(m.Policy, day);
            }
            if (voted)
            {
                // The district's view of the vote: an aye for what it cares about more than the city, a no against.
                int home = HomeDistrict();
                float lean = home >= 0 ? _gov.DistrictLean(home, (int)policy.Issue) : 1f;
                State.VotesRecord = Mathf.Clamp(State.VotesRecord + (vote == 1 ? 1f : -1f) * (lean - 1f) * 6f, -20f, 20f);
                if (Conflicted(m.Policy) && !declaredAside && State.Secrets.Count < OfficeState.SecretsMost)
                {
                    State.Secrets.Add(new OfficeSecret { Day = day, Policy = m.Policy, Decisive = result.Decisive });
                }
            }
            if (declaredAside)
            {
                CivicBridges.StandingMoved?.Invoke(1, 0);
            }
            if (m.Undeclared && result.Passed && State.Secrets.Count < OfficeState.SecretsMost)
            {
                State.Secrets.Add(new OfficeSecret { Day = day, Policy = m.Policy, Decisive = true });
            }
        }

        public static string Levels(int[] levels)
        {
            var text = new System.Text.StringBuilder(64);
            for (int d = 0; d < levels.Length && d < 4; d++)
            {
                if (d > 0)
                {
                    text.Append(", ");
                }
                text.Append(CivicText.T(DepartmentKeys[d])).Append(' ').Append(levels[d] > 0 ? "+" : string.Empty).Append(levels[d]);
            }
            return text.ToString();
        }

        /// <summary>A meeting is over: the motions were decided at it (by the scene) and the agenda is cleared.</summary>
        public void MeetingHeld(int day)
        {
            State.Agenda.Clear();
            State.Attended++;
            State.NextMeeting = day + OfficeRules.MeetingEvery;
            if (State.Holds == OfficeRace.Council)
            {
                State.Approval = Mathf.Min(95f, State.Approval + 0.5f);
            }
            Post(PostMeeting, DistrictName(HomeDistrict()));
        }

        /// <summary>Apologies sent for today's meeting: the council decides without the player.</summary>
        public void Skip(int day)
        {
            HoldWithout(day);
        }

        // A meeting the player did not come to: the council decides without them.
        private void HoldWithout(int day)
        {
            if (State.NextMeeting == day)
            {
                Prepare(day);
            }
            int passed = 0;
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                passed += Decide(State.Agenda[i], -1, false, day).Passed ? 1 : 0;
            }
            int count = State.Agenda.Count;
            State.Agenda.Clear();
            State.Missed++;
            if (State.Holds == OfficeRace.Council)
            {
                State.Approval = Mathf.Max(3f, State.Approval - 1.5f);
            }
            State.NextMeeting = day + OfficeRules.MeetingEvery;
            CivicText.Toast(NotificationType.Warning, "reallife_civic_office_meeting_missed", "count", count.ToString(), "passed", passed.ToString(), 12f);
        }

        // ---------------------------------------------------------------- the city's budget

        private void Budget(int day)
        {
            if (!State.ReserveSet)
            {
                State.ReserveSet = true;
                State.Reserve = OfficeRules.StartReserve(_gov.Electorate());
                State.WeekDay = day + 7;
                State.BudgetDay = day + OfficeRules.BudgetEvery;
                OfficeRules.CityBudget(Gov.Mayor.Lead, State.Budget);
                return;
            }
            if (day >= State.WeekDay)
            {
                State.WeekDay = day + 7;
                BudgetWeek week = OfficeRules.Week(_gov.Electorate(), Gov.Levers, State.Budget);
                State.Reserve += week.Balance;
            }
            if (!Sits() && day >= State.BudgetDay)
            {
                // A mayor of the city's own sets the budget; nobody the player is asks a vote of them.
                State.BudgetDay = day + OfficeRules.BudgetEvery;
                OfficeRules.CityBudget(Gov.Mayor.Lead, State.Budget);
            }
        }

        // ---------------------------------------------------------------- small things

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

        /// <summary>Test button: the player takes an office now, as if just elected.</summary>
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
