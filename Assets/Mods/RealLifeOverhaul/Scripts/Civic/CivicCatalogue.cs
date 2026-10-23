using System;

namespace RealLifeOverhaul
{
    /// <summary>The thirteen things a mayor can set. Each stands at -1, 0 (as the city came) or +1.</summary>
    public enum Lever
    {
        Policing, Traffic, BusinessTax, TradeLevy, Grants, Rent, Parking, Transit, Cleaning, NightLife, Permits, Events, Wages
    }

    /// <summary>What voters care about. A policy speaks to one of them; events in the save make one matter more.</summary>
    public enum Issue { Safety, Roads, Business, Living, Services, Growth }

    /// <summary>One direction of one lever: what a candidate promises and what it then does.</summary>
    public sealed class PolicyDef
    {
        public Lever Lever;
        public int Dir;          // +1 or -1
        public string Name;      // locale key: its short name
        public string Effect;    // locale key: what it changes for the player, in plain numbers
        public string Say;       // locale key: what a candidate says about it in the debate
        public Issue Issue;      // whom it speaks to
        public int Levy;         // dollars a week on the city levy line: positive is charged, negative is returned
    }

    /// <summary>Every policy and every number a policy changes, game-free (<see cref="CivicEffects"/> applies them). The locales quote these numbers: change both together.</summary>
    public static class CivicCatalogue
    {
        public const int Levers = 13;
        public const int Issues = 6;

        public static readonly PolicyDef[] All =
        {
            new PolicyDef { Lever = Lever.Policing, Dir = 1, Issue = Issue.Safety, Levy = 70, Name = "reallife_civic_policy_policing_up",
                Effect = "reallife_civic_policy_policing_up_fx", Say = "reallife_civic_policy_policing_up_say" },
            new PolicyDef { Lever = Lever.Policing, Dir = -1, Issue = Issue.Living, Levy = -50, Name = "reallife_civic_policy_policing_down",
                Effect = "reallife_civic_policy_policing_down_fx", Say = "reallife_civic_policy_policing_down_say" },
            new PolicyDef { Lever = Lever.Traffic, Dir = 1, Issue = Issue.Roads, Name = "reallife_civic_policy_traffic_up",
                Effect = "reallife_civic_policy_traffic_up_fx", Say = "reallife_civic_policy_traffic_up_say" },
            new PolicyDef { Lever = Lever.Traffic, Dir = -1, Issue = Issue.Business, Name = "reallife_civic_policy_traffic_down",
                Effect = "reallife_civic_policy_traffic_down_fx", Say = "reallife_civic_policy_traffic_down_say" },
            new PolicyDef { Lever = Lever.BusinessTax, Dir = 1, Issue = Issue.Services, Name = "reallife_civic_policy_tax_up",
                Effect = "reallife_civic_policy_tax_up_fx", Say = "reallife_civic_policy_tax_up_say" },
            new PolicyDef { Lever = Lever.BusinessTax, Dir = -1, Issue = Issue.Business, Name = "reallife_civic_policy_tax_down",
                Effect = "reallife_civic_policy_tax_down_fx", Say = "reallife_civic_policy_tax_down_say" },
            new PolicyDef { Lever = Lever.TradeLevy, Dir = 1, Issue = Issue.Services, Levy = -40, Name = "reallife_civic_policy_levy_up",
                Effect = "reallife_civic_policy_levy_up_fx", Say = "reallife_civic_policy_levy_up_say" },
            new PolicyDef { Lever = Lever.TradeLevy, Dir = -1, Issue = Issue.Business, Name = "reallife_civic_policy_levy_down",
                Effect = "reallife_civic_policy_levy_down_fx", Say = "reallife_civic_policy_levy_down_say" },
            new PolicyDef { Lever = Lever.Grants, Dir = 1, Issue = Issue.Business, Name = "reallife_civic_policy_grants_up",
                Effect = "reallife_civic_policy_grants_up_fx", Say = "reallife_civic_policy_grants_up_say" },
            new PolicyDef { Lever = Lever.Grants, Dir = -1, Issue = Issue.Services, Name = "reallife_civic_policy_grants_down",
                Effect = "reallife_civic_policy_grants_down_fx", Say = "reallife_civic_policy_grants_down_say" },
            new PolicyDef { Lever = Lever.Rent, Dir = 1, Issue = Issue.Growth, Name = "reallife_civic_policy_rent_up",
                Effect = "reallife_civic_policy_rent_up_fx", Say = "reallife_civic_policy_rent_up_say" },
            new PolicyDef { Lever = Lever.Rent, Dir = -1, Issue = Issue.Living, Name = "reallife_civic_policy_rent_down",
                Effect = "reallife_civic_policy_rent_down_fx", Say = "reallife_civic_policy_rent_down_say" },
            new PolicyDef { Lever = Lever.Parking, Dir = 1, Issue = Issue.Roads, Name = "reallife_civic_policy_parking_up",
                Effect = "reallife_civic_policy_parking_up_fx", Say = "reallife_civic_policy_parking_up_say" },
            new PolicyDef { Lever = Lever.Parking, Dir = -1, Issue = Issue.Business, Name = "reallife_civic_policy_parking_down",
                Effect = "reallife_civic_policy_parking_down_fx", Say = "reallife_civic_policy_parking_down_say" },
            new PolicyDef { Lever = Lever.Transit, Dir = 1, Issue = Issue.Services, Name = "reallife_civic_policy_transit_up",
                Effect = "reallife_civic_policy_transit_up_fx", Say = "reallife_civic_policy_transit_up_say" },
            new PolicyDef { Lever = Lever.Transit, Dir = -1, Issue = Issue.Living, Name = "reallife_civic_policy_transit_down",
                Effect = "reallife_civic_policy_transit_down_fx", Say = "reallife_civic_policy_transit_down_say" },
            new PolicyDef { Lever = Lever.Cleaning, Dir = 1, Issue = Issue.Services, Levy = 30, Name = "reallife_civic_policy_cleaning_up",
                Effect = "reallife_civic_policy_cleaning_up_fx", Say = "reallife_civic_policy_cleaning_up_say" },
            new PolicyDef { Lever = Lever.Cleaning, Dir = -1, Issue = Issue.Living, Levy = -25, Name = "reallife_civic_policy_cleaning_down",
                Effect = "reallife_civic_policy_cleaning_down_fx", Say = "reallife_civic_policy_cleaning_down_say" },
            new PolicyDef { Lever = Lever.NightLife, Dir = 1, Issue = Issue.Growth, Name = "reallife_civic_policy_night_up",
                Effect = "reallife_civic_policy_night_up_fx", Say = "reallife_civic_policy_night_up_say" },
            new PolicyDef { Lever = Lever.NightLife, Dir = -1, Issue = Issue.Safety, Name = "reallife_civic_policy_night_down",
                Effect = "reallife_civic_policy_night_down_fx", Say = "reallife_civic_policy_night_down_say" },
            new PolicyDef { Lever = Lever.Permits, Dir = 1, Issue = Issue.Growth, Name = "reallife_civic_policy_permits_up",
                Effect = "reallife_civic_policy_permits_up_fx", Say = "reallife_civic_policy_permits_up_say" },
            new PolicyDef { Lever = Lever.Permits, Dir = -1, Issue = Issue.Living, Name = "reallife_civic_policy_permits_down",
                Effect = "reallife_civic_policy_permits_down_fx", Say = "reallife_civic_policy_permits_down_say" },
            new PolicyDef { Lever = Lever.Events, Dir = 1, Issue = Issue.Growth, Levy = 40, Name = "reallife_civic_policy_events_up",
                Effect = "reallife_civic_policy_events_up_fx", Say = "reallife_civic_policy_events_up_say" },
            new PolicyDef { Lever = Lever.Events, Dir = -1, Issue = Issue.Living, Levy = -30, Name = "reallife_civic_policy_events_down",
                Effect = "reallife_civic_policy_events_down_fx", Say = "reallife_civic_policy_events_down_say" },
            new PolicyDef { Lever = Lever.Wages, Dir = 1, Issue = Issue.Living, Name = "reallife_civic_policy_wages_up",
                Effect = "reallife_civic_policy_wages_up_fx", Say = "reallife_civic_policy_wages_up_say" },
            new PolicyDef { Lever = Lever.Wages, Dir = -1, Issue = Issue.Business, Name = "reallife_civic_policy_wages_down",
                Effect = "reallife_civic_policy_wages_down_fx", Say = "reallife_civic_policy_wages_down_say" }
        };

        public static readonly string[] IssueNames =
        {
            "reallife_civic_issue_safety", "reallife_civic_issue_roads", "reallife_civic_issue_business",
            "reallife_civic_issue_living", "reallife_civic_issue_services", "reallife_civic_issue_growth"
        };

        /// <summary>The policy for a lever standing at a position; null at 0.</summary>
        public static PolicyDef Find(Lever lever, int dir)
        {
            if (dir == 0)
            {
                return null;
            }
            int index = (int)lever * 2 + (dir > 0 ? 0 : 1);
            return index >= 0 && index < All.Length ? All[index] : null;
        }

        /// <summary>A policy by its place in <see cref="All"/>; null out of range.</summary>
        public static PolicyDef At(int index)
        {
            return index >= 0 && index < All.Length ? All[index] : null;
        }

        public static int IndexOf(PolicyDef policy)
        {
            return policy == null ? -1 : (int)policy.Lever * 2 + (policy.Dir > 0 ? 0 : 1);
        }

        private static float Pick(int position, float up, float down)
        {
            return position > 0 ? up : position < 0 ? down : 1f;
        }

        public static float PoliceScale(int policing) => Pick(policing, 1.4f, 0.6f);

        public static float CrimeScale(int policing, int nightLife) => Pick(policing, 0.8f, 1.25f) * Pick(nightLife, 1.1f, 0.92f);

        public static float FineScale(int traffic) => Pick(traffic, 1.5f, 0.7f);

        public static float AccidentScale(int traffic) => Pick(traffic, 0.7f, 1.3f);

        /// <summary>The share of the income tax the day's sales ran up that is charged on top (positive) or paid back (negative).</summary>
        public static float TaxShare(int businessTax) => businessTax > 0 ? 0.20f : businessTax < 0 ? -0.20f : 0f;

        /// <summary>The share of sales charged (positive) or paid back (negative).</summary>
        public static float TradeShare(int tradeLevy) => tradeLevy > 0 ? 0.005f : tradeLevy < 0 ? -0.003f : 0f;

        /// <summary>Dollars a week for each business (three at most): positive is a grant, negative a licence fee.</summary>
        public static int GrantPerBusiness(int grants) => grants > 0 ? 150 : grants < 0 ? -60 : 0;

        public const int GrantBusinesses = 3;

        /// <summary>The share of the rent the player pays that is charged on top (positive) or returned (negative).</summary>
        public static float RentShare(int rent) => rent > 0 ? 0.06f : rent < 0 ? -0.06f : 0f;

        /// <summary>The share of the rent the player collects as a landlord that is added (positive) or lost (negative).</summary>
        public static float LandlordShare(int rent) => rent > 0 ? 0.04f : rent < 0 ? -0.04f : 0f;

        public static float ParkingScale(int parking) => Pick(parking, 1.5f, 0.6f);

        /// <summary>Dollars added to (or taken off) each parking ticket.</summary>
        public static int TicketExtra(int parking) => parking > 0 ? 60 : parking < 0 ? -40 : 0;

        /// <summary>Dollars added to (or taken off) each subway ride.</summary>
        public static int FareExtra(int transit) => transit > 0 ? 1 : transit < 0 ? -1 : 0;

        public static float ServiceScale(int transit) => Pick(transit, 1.3f, 0.75f);

        public static float SweeperScale(int cleaning) => Pick(cleaning, 2.5f, 0.3f);

        /// <summary>The hour licensed premises must shut: 4, 2 (as the city came) or 0.</summary>
        public static int ClosingHour(int nightLife) => nightLife > 0 ? 4 : nightLife < 0 ? 0 : 2;

        public static float SiteSpeed(int permits) => Pick(permits, 1.35f, 0.75f);

        public static int SiteOpens(int permits) => permits < 0 ? 8 : 7;

        public static int SiteShuts(int permits) => permits > 0 ? 19 : permits < 0 ? 16 : 17;

        public static float EventScale(int events) => Pick(events, 1.5f, 0.5f);

        /// <summary>The hourly wage floor; 0 for none.</summary>
        public static float WageFloor(int wages) => wages > 0 ? 16f : 0f;

        /// <summary>The share of the wage bill the city pays.</summary>
        public static float WageSubsidy(int wages) => wages < 0 ? 0.05f : 0f;

        // Money at today's prices (Core/Prices); the figures in this class are the start's.

        public static int LevyNow(PolicyDef policy) => policy != null ? Prices.At(policy.Levy) : 0;

        public static int GrantNow(int grants) => Prices.At(GrantPerBusiness(grants));

        public static int TicketExtraNow(int parking) => Prices.At(TicketExtra(parking));

        public static float FareExtraNow(int transit) => Prices.At((float)FareExtra(transit));

        public static float WageFloorNow(int wages) => Prices.At(WageFloor(wages));

        public static int DonationStepNow => Prices.At(DonationStep);

        public static int DonationCapNow => Prices.At(DonationCap);

        /// <summary>True if one more donation of <paramref name="step"/> keeps a campaign's total within <paramref name="cap"/>.</summary>
        public static bool DonationFits(int donated, int step, int cap) => donated + step <= cap;

        /// <summary>Given to the winner's campaign, for each fast-track building permit (two at most), at the start's prices.</summary>
        public const int FavourPer = 1000;

        public static int FavourPerNow => Prices.At(FavourPer);

        public static int RefitPriceNow(int bid) => Prices.At(RefitPrice[Math.Max(0, Math.Min(RefitPrice.Length - 1, bid))]);

        public const int TermWeeks = 8;             // an election every eight in-game weeks
        public const int TermDays = TermWeeks * 7;
        public const int CampaignDays = 7;          // posters go up this many days before election day
        public const int FirstElectionAfter = 14;   // never before the end of the second week the mod has seen in a save
        public const int DebateBefore = 3;          // the debate is this many days before election day, at 19:00
        public const int PollsOpen = 7, PollsClose = 20, ResultHour = 21;   // hours of the day
        public const int CouncilSeats = 5;
        public const int MaxCandidates = 4;
        public const int MaxDistricts = 12;         // the count keeps each district's shares for so many districts (the game's city has fewer)
        public const int DonationStep = 500;        // one donation
        public const int DonationCap = 2500;        // to one campaign, in one election
        public const float PayToPlayOdds = 0.25f;   // the chance a favour called in becomes a newspaper story
        public const float RecallBelow = 25f;       // approval under this...
        public const int RecallDays = 7;            // ...for this many days running brings a recall election
        public const float PetitionShare = 0.02f;   // of the electorate must sign
        public const int PetitionDays = 14;   // days
        public const int CouncilVoteEvery = 12;     // days between council votes on a single issue, give or take

        public static readonly string[] FirstNames =
        {
            "reallife_civic_first_1", "reallife_civic_first_2", "reallife_civic_first_3", "reallife_civic_first_4", "reallife_civic_first_5",
            "reallife_civic_first_6", "reallife_civic_first_7", "reallife_civic_first_8", "reallife_civic_first_9", "reallife_civic_first_10",
            "reallife_civic_first_11", "reallife_civic_first_12", "reallife_civic_first_13", "reallife_civic_first_14", "reallife_civic_first_15",
            "reallife_civic_first_16", "reallife_civic_first_17", "reallife_civic_first_18", "reallife_civic_first_19", "reallife_civic_first_20"
        };

        public static readonly string[] LastNames =
        {
            "reallife_civic_last_1", "reallife_civic_last_2", "reallife_civic_last_3", "reallife_civic_last_4", "reallife_civic_last_5",
            "reallife_civic_last_6", "reallife_civic_last_7", "reallife_civic_last_8", "reallife_civic_last_9", "reallife_civic_last_10",
            "reallife_civic_last_11", "reallife_civic_last_12", "reallife_civic_last_13", "reallife_civic_last_14", "reallife_civic_last_15",
            "reallife_civic_last_16", "reallife_civic_last_17", "reallife_civic_last_18", "reallife_civic_last_19", "reallife_civic_last_20"
        };

        public static readonly string[] Parties =
        {
            "reallife_civic_party_1", "reallife_civic_party_2", "reallife_civic_party_3", "reallife_civic_party_4",
            "reallife_civic_party_5", "reallife_civic_party_6", "reallife_civic_party_7", "reallife_civic_party_8"
        };

        /// <summary>The party of somebody who stands for none: one past the last of <see cref="Parties"/> (only the player stands so).</summary>
        public const int Independent = 8;

        /// <summary>A party's name key, or that of "Independent".</summary>
        public static string PartyKey(int party)
        {
            return party >= 0 && party < Parties.Length ? Parties[party] : "reallife_civic_party_independent";
        }

        /// <summary>Slogans, three for each issue a candidate can lead on (in the order of <see cref="Issue"/>).</summary>
        public static readonly string[] Slogans =
        {
            "reallife_civic_slogan_safety_1", "reallife_civic_slogan_safety_2", "reallife_civic_slogan_safety_3",
            "reallife_civic_slogan_roads_1", "reallife_civic_slogan_roads_2", "reallife_civic_slogan_roads_3",
            "reallife_civic_slogan_business_1", "reallife_civic_slogan_business_2", "reallife_civic_slogan_business_3",
            "reallife_civic_slogan_living_1", "reallife_civic_slogan_living_2", "reallife_civic_slogan_living_3",
            "reallife_civic_slogan_services_1", "reallife_civic_slogan_services_2", "reallife_civic_slogan_services_3",
            "reallife_civic_slogan_growth_1", "reallife_civic_slogan_growth_2", "reallife_civic_slogan_growth_3"
        };

        public static readonly string[] Contractors =
        {
            "reallife_civic_contractor_1", "reallife_civic_contractor_2", "reallife_civic_contractor_3", "reallife_civic_contractor_4",
            "reallife_civic_contractor_5", "reallife_civic_contractor_6", "reallife_civic_contractor_7", "reallife_civic_contractor_8"
        };

        /// <summary>Project names, three for each kind of site (in the order of <see cref="SiteKind"/>).</summary>
        public static readonly string[] Projects =
        {
            "reallife_civic_project_road_1", "reallife_civic_project_road_2", "reallife_civic_project_road_3",
            "reallife_civic_project_scaffold_1", "reallife_civic_project_scaffold_2", "reallife_civic_project_scaffold_3",
            "reallife_civic_project_refit_1", "reallife_civic_project_refit_2", "reallife_civic_project_refit_3",
            "reallife_civic_project_utility_1", "reallife_civic_project_utility_2", "reallife_civic_project_utility_3",
            "reallife_construction_project_rebuild_1", "reallife_construction_project_rebuild_2", "reallife_construction_project_rebuild_3",
            "reallife_construction_project_plot_1", "reallife_construction_project_plot_2", "reallife_construction_project_plot_3"
        };

        /// <summary>The phases of each kind of site, five each (in the order of <see cref="SiteKind"/>).</summary>
        public static readonly string[] Phases =
        {
            "reallife_civic_phase_road_1", "reallife_civic_phase_road_2", "reallife_civic_phase_road_3", "reallife_civic_phase_road_4", "reallife_civic_phase_road_5",
            "reallife_civic_phase_scaffold_1", "reallife_civic_phase_scaffold_2", "reallife_civic_phase_scaffold_3", "reallife_civic_phase_scaffold_4", "reallife_civic_phase_scaffold_5",
            "reallife_civic_phase_refit_1", "reallife_civic_phase_refit_2", "reallife_civic_phase_refit_3", "reallife_civic_phase_refit_4", "reallife_civic_phase_refit_5",
            "reallife_civic_phase_utility_1", "reallife_civic_phase_utility_2", "reallife_civic_phase_utility_3", "reallife_civic_phase_utility_4", "reallife_civic_phase_utility_5",
            "reallife_construction_phase_rebuild_1", "reallife_construction_phase_rebuild_2", "reallife_construction_phase_rebuild_3",
            "reallife_construction_phase_rebuild_4", "reallife_construction_phase_rebuild_5",
            "reallife_construction_phase_plot_1", "reallife_construction_phase_plot_2", "reallife_construction_phase_plot_3",
            "reallife_construction_phase_plot_4", "reallife_construction_phase_plot_5"
        };

        public const int PhasesPerKind = 5;
        public const int ActiveSites = 6;           // in the programme at once, city-wide
        public const int LocalSites = 2;            // of those, started within a walk of where the player was that day

        /// <summary>Working hours a site of each kind needs (ten a weekday as the city came: 40 is four working days).</summary>
        public static readonly int[] SiteHours = { 50, 120, 90, 30, 160, 320 };

        /// <summary>Rebuild sites in the programme at once, at most: a whole building wrapped is a sight, not the street's every day.</summary>
        public const int MostRebuilds = 1;

        /// <summary>The three bids for refitting the player's own shopfront: price and working hours.</summary>
        public static readonly int[] RefitPrice = { 1800, 2600, 3600 };
        public static readonly int[] RefitHours = { 80, 50, 30 };
    }

    /// <summary>
    /// The kinds of building site. Saved as a number: an older version reads a number it does not know as the last it has
    /// (a utility site), so a save with a rebuild or a plot still loads there.
    /// </summary>
    public enum SiteKind { Roadworks, Scaffold, Refit, Utility, Rebuild, Plot }

    /// <summary>A small repeatable random source (xorshift): what is drawn from a save's seed is the same every time it is asked for.</summary>
    public struct CivicRandom
    {
        private uint _state;

        public CivicRandom(int seed)
        {
            _state = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (_state == 0)
            {
                _state = 0x1234567u;
            }
            Next();
            Next();
        }

        public uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>0 inclusive to 1 exclusive.</summary>
        public float Value => (Next() & 0xFFFFFF) / 16777216f;

        /// <summary>min inclusive to max exclusive.</summary>
        public int Range(int min, int max)
        {
            return max <= min ? min : min + (int)(Next() % (uint)(max - min));
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Value;
        }

        /// <summary>One seed from several numbers.</summary>
        public static int Mix(int a, int b, int c = 0)
        {
            unchecked
            {
                uint h = (uint)a * 0x85EBCA6Bu;
                h ^= (uint)b * 0xC2B2AE35u + (h << 6) + (h >> 2);
                h ^= (uint)c * 0x27D4EB2Fu + (h << 6) + (h >> 2);
                h ^= h >> 15;
                return (int)h;
            }
        }
    }
}
