using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy.Dealership;

// The directors' own code runs in the work runs: CareersDirector.People.cs (the week's write, a worked shift, a review's contest, an interview's
// weighing, a seat and its refusal) and HustleDirector.Morning.cs (the Marketplace's morning) are linked as they ship. Only what stands under
// them in the game is substituted here: the save's day, the study file the careers live in, the colleagues' names, the player's employees, the
// notices and the Marketplace's reads of the city.
namespace RealLifeOverhaul.Careers
{
    public sealed partial class CareersDirector
    {
        private static GameInstance Save => SaveGameManager.Current;
        private readonly StudyFile _study;
        public StudyFile Study => _study;
        public WorkFile Work => _study != null ? _study.Work : null;
        private int StudySeed => _study != null ? _study.Seed : 1;
        private int LadderReviewBonus() => 0;
        private int Points(int skill) => StudyRules.Points(_study, skill);
        private int _nameCount;
        private Func<string> _names;
        private Func<string> Names => _names ?? (_names = () => "Colleague " + _nameCount++);
        public readonly List<string> StaffNames = new List<string>();
        private IList<string> EmployeeNames() => StaffNames;
        private void PeopleForgotten() { }
        public int PassedCount, FollowedCount;
        public string LastPassed, LastFollower;
        private void PassedOver(string rival, string employer) { PassedCount++; LastPassed = rival; }
        private void EmployeeFollowed(string who, int course) { FollowedCount++; LastFollower = who; }

        public CareersDirector(StudyFile study)
        {
            _study = study;
        }

        public int NamesRolled { get => _nameCount; set => _nameCount = value; }

        // The calls the director makes on its day turn and at its presses, reached by a run as they are.
        public void RunStandingDay(int day) => StandingDay(day);
        public void RunRecordShift(Job j, int day, int minutes) => RecordShift(j, day, minutes);
        public bool RunStandingDecides(Job j, int day) => StandingDecides(j, day);
        public string RunOtherGotIt(Application a) => OtherGotIt(a);
        public void RunCampusEnrolled(CourseDef c, int day) => CampusEnrolled(c, day);
        public string RunCampusWhy(CourseDef c) => CampusWhy(c);
        public void RunPeopleWrite(Dictionary<string, string> data) => PeopleWrite(data);
        public void RunPeopleForget() => PeopleForget();
    }
}

namespace RealLifeOverhaul.Hustles
{
    public sealed partial class HustleDirector
    {
        private HustleState _state;
        private SellerFile _sellers = new SellerFile();
        private readonly List<HustlePlace> _homes = new List<HustlePlace>();
        private readonly List<MarketModel> _cars = new List<MarketModel>();
        private readonly List<string> _colours = new List<string>();
        private readonly List<RivalDealer> _dealers = new List<RivalDealer>();
        public readonly List<RivalDealer> RivalShops = new List<RivalDealer>();
        private string SellerName(int k) => "Seller " + k;
        private void ReadDealers() { _dealers.Clear(); _dealers.AddRange(RivalShops); }
        private static int ShopLevel => 1000;
        private static int MarketPeople => 1000;

        public HustleDirector(HustleState state, SellerFile sellers, IEnumerable<HustlePlace> homes, IEnumerable<MarketModel> cars, IEnumerable<string> colours)
        {
            _state = state;
            _sellers = sellers;
            _homes.AddRange(homes);
            _cars.AddRange(cars);
            _colours.AddRange(colours);
        }

        public HustleState RunState { get => _state; set => _state = value; }
        public SellerFile RunSellers { get => _sellers; set => _sellers = value; }
        public void RunMorning(int day) => MorningOf(day);
    }
}
